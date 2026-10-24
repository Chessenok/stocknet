using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Contracts.Billing;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Services.Billing;
using Xunit;

namespace StockNet.Api.IntegrationTests;

public sealed class BillingIntegrationTests : IAsyncLifetime
{
    private const string TestWebhookSecret = "whsec_m6-integration-test-secret";
    private readonly string previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? string.Empty;
    private readonly string previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
    private readonly string previousStripeApiKey = Environment.GetEnvironmentVariable("Stripe__ApiKey") ?? string.Empty;
    private readonly string previousStripePriceId = Environment.GetEnvironmentVariable("Stripe__PriceId") ?? string.Empty;
    private readonly string previousStripeWebhookSecret = Environment.GetEnvironmentVariable("Stripe__WebhookSecret") ?? string.Empty;
    private readonly string eventPrefix = $"evt_m6_{Guid.NewGuid():N}";
    private Guid companyId;
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private FakeStripeGateway fakeGateway = new();
    private string accessToken = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__StockNet")))
        {
            throw new InvalidOperationException("Set ConnectionStrings__StockNet to a migrated PostgreSQL/PostGIS test database before running integration tests.");
        }

        Environment.SetEnvironmentVariable("Jwt__SigningKey", "StockNet-billing-test-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Stripe__ApiKey", "sk_test_integration_fake_key");
        Environment.SetEnvironmentVariable("Stripe__PriceId", "price_test_subscription");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", TestWebhookSecret);

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStripeGateway>();
                services.AddSingleton<IStripeGateway>(fakeGateway);
            });
        });
        client = factory.CreateClient();

        var email = $"m6-{Guid.NewGuid():N}@example.test";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "M6 Billing Test",
            email,
            password = "M6-integration-secure-password!",
            taxId = $"M6-{Guid.NewGuid():N}",
            countryCode = "RO",
            companyType = "Producer"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var session = await registration.Content.ReadFromJsonAsync<AuthSessionDto>();
        Assert.NotNull(session);
        companyId = session.CompanyId;
        accessToken = session.AccessToken;
    }

    [Fact]
    public async Task Billing_checks_auth_validates_signature_and_processes_events_idempotently()
    {
        using var anonymousCheckout = await client!.PostAsync("/api/billing/checkout-session", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCheckout.StatusCode);

        using var signedInCheckout = await SendAuthorizedAsync(HttpMethod.Post, "/api/billing/checkout-session", accessToken);
        Assert.Equal(HttpStatusCode.OK, signedInCheckout.StatusCode);
        var checkout = await signedInCheckout.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        Assert.Equal("cs_test_session", checkout!.SessionId);
        Assert.Equal("https://checkout.stripe.test/session", checkout.Url);
        Assert.Equal(companyId, fakeGateway.LastCustomerCompanyId);
        Assert.Equal("cus_m6_test", fakeGateway.LastCheckoutCustomerId);

        using (var scope = factory!.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
            var company = await dbContext.Companies.SingleAsync(item => item.Id == companyId);
            Assert.Equal("cus_m6_test", company.StripeCustomerId);
        }

        var invalidEventId = $"{eventPrefix}_invalid_signature";
        var invalidPayload = CreateEventJson(invalidEventId, "customer.subscription.updated", SubscriptionObject(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            DateTime.UtcNow.AddDays(20)));
        using var invalidSignatureResponse = await SendWebhookAsync(invalidPayload, "t=1,v1=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidSignatureResponse.StatusCode);

        var periodEndAfterCheckout = DateTime.UtcNow.AddDays(30);
        fakeGateway.Subscriptions["sub_m6_test"] = new StripeSubscriptionSnapshot(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            periodEndAfterCheckout);
        var checkoutEventId = $"{eventPrefix}_checkout";
        var checkoutEvent = CreateEventJson(checkoutEventId, "checkout.session.completed", new
        {
            id = "cs_test_session",
            @object = "checkout.session",
            client_reference_id = companyId.ToString(),
            customer = "cus_m6_test",
            mode = "subscription",
            subscription = "sub_m6_test",
            status = "complete",
            payment_status = "paid"
        });
        using var checkoutWebhookResponse = await SendValidWebhookAsync(checkoutEvent);
        Assert.Equal(HttpStatusCode.OK, checkoutWebhookResponse.StatusCode);
        Assert.True(await IsCompanySubscribedAsync(periodEndAfterCheckout));
        Assert.Equal(1, fakeGateway.SubscriptionLookupCount);

        fakeGateway.Subscriptions["sub_m6_test"] = new StripeSubscriptionSnapshot(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            DateTime.UtcNow.AddDays(90));
        using var duplicateWebhookResponse = await SendValidWebhookAsync(checkoutEvent);
        Assert.Equal(HttpStatusCode.OK, duplicateWebhookResponse.StatusCode);
        using var duplicateBody = await duplicateWebhookResponse.Content.ReadFromJsonAsync<JsonDocument>()
            ?? throw new InvalidOperationException("Missing duplicate webhook result.");
        Assert.True(duplicateBody.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal(1, fakeGateway.SubscriptionLookupCount);
        Assert.True(await IsCompanySubscribedAsync(periodEndAfterCheckout));

        var invoiceExpiry = DateTime.UtcNow.AddDays(60);
        fakeGateway.Subscriptions["sub_m6_test"] = new StripeSubscriptionSnapshot(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            invoiceExpiry);
        using var invoiceResponse = await SendValidWebhookAsync(CreateEventJson($"{eventPrefix}_invoice", "invoice.payment_succeeded", new
        {
            id = "in_m6_test",
            @object = "invoice",
            customer = "cus_m6_test",
            parent = new
            {
                type = "subscription_details",
                subscription_details = new { subscription = "sub_m6_test" }
            }
        }));
        Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);
        Assert.True(await IsCompanySubscribedAsync(invoiceExpiry));

        var updatedExpiry = DateTime.UtcNow.AddDays(40);
        var stripeUpdatedExpiry = DateTimeOffset.FromUnixTimeSeconds(new DateTimeOffset(updatedExpiry).ToUnixTimeSeconds()).UtcDateTime;
        fakeGateway.Subscriptions["sub_m6_test"] = new StripeSubscriptionSnapshot(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            stripeUpdatedExpiry);
        using var updateResponse = await SendValidWebhookAsync(CreateEventJson($"{eventPrefix}_updated", "customer.subscription.updated", SubscriptionObject(
            "sub_m6_test",
            "cus_m6_test",
            "active",
            updatedExpiry)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(await IsCompanySubscribedAsync(stripeUpdatedExpiry));

        fakeGateway.Subscriptions["sub_m6_test"] = new StripeSubscriptionSnapshot(
            "sub_m6_test",
            "cus_m6_test",
            "canceled",
            stripeUpdatedExpiry);
        using var deletedResponse = await SendValidWebhookAsync(CreateEventJson($"{eventPrefix}_deleted", "customer.subscription.deleted", SubscriptionObject(
            "sub_m6_test",
            "cus_m6_test",
            "canceled",
            updatedExpiry)));
        Assert.Equal(HttpStatusCode.OK, deletedResponse.StatusCode);

        using var scopeAfterDelete = factory!.Services.CreateAsyncScope();
        var dbContextAfterDelete = scopeAfterDelete.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var companyAfterDelete = await dbContextAfterDelete.Companies.SingleAsync(item => item.Id == companyId);
        Assert.False(companyAfterDelete.IsSubscribed);
        Assert.Null(companyAfterDelete.SubscriptionExpiresAt);
        Assert.Equal(4, await dbContextAfterDelete.ProcessedStripeEvents.CountAsync(item => item.EventId.StartsWith(eventPrefix)));
        Assert.False(await dbContextAfterDelete.ProcessedStripeEvents.AnyAsync(item => item.EventId == invalidEventId));
        Assert.Equal(4, fakeGateway.SubscriptionLookupCount);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
                var events = await dbContext.ProcessedStripeEvents
                    .Where(item => item.EventId.StartsWith(eventPrefix))
                    .ToListAsync();
                dbContext.ProcessedStripeEvents.RemoveRange(events);
                var company = await dbContext.Companies.SingleOrDefaultAsync(item => item.Id == companyId);
                if (company is not null)
                {
                    dbContext.Companies.Remove(company);
                }

                await dbContext.SaveChangesAsync();
            }

            await factory.DisposeAsync();
        }

        client?.Dispose();
        Environment.SetEnvironmentVariable("Jwt__SigningKey", Restore(previousSigningKey));
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Restore(previousEnvironment));
        Environment.SetEnvironmentVariable("Stripe__ApiKey", Restore(previousStripeApiKey));
        Environment.SetEnvironmentVariable("Stripe__PriceId", Restore(previousStripePriceId));
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", Restore(previousStripeWebhookSecret));
    }

    private async Task<bool> IsCompanySubscribedAsync(DateTime expectedExpiry)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var company = await dbContext.Companies.SingleAsync(item => item.Id == companyId);
        return company.IsSubscribed
            && company.SubscriptionExpiresAt == expectedExpiry
            && company.StripeSubscriptionId == "sub_m6_test";
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendValidWebhookAsync(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestWebhookSecret), signedPayload);
        var signature = $"t={timestamp},v1={Convert.ToHexString(digest).ToLowerInvariant()}";
        return await SendWebhookAsync(payload, signature);
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string payload, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return await client!.SendAsync(request);
    }

    private static string CreateEventJson(string eventId, string eventType, object dataObject) => JsonSerializer.Serialize(new
    {
        id = eventId,
        @object = "event",
        api_version = "2025-06-30.basil",
        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        data = new { @object = dataObject },
        livemode = false,
        pending_webhooks = 1,
        type = eventType
    });

    private static object SubscriptionObject(string id, string customerId, string status, DateTime periodEnd) => new
    {
        id,
        @object = "subscription",
        customer = customerId,
        status,
        items = new
        {
            @object = "list",
            data = new[]
            {
                new
                {
                    id = $"si_{id}",
                    @object = "subscription_item",
                    current_period_start = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds(),
                    current_period_end = new DateTimeOffset(periodEnd).ToUnixTimeSeconds()
                }
            },
            has_more = false,
            total_count = 1,
            url = "/v1/subscription_items"
        }
    };

    private static string? Restore(string value) => string.IsNullOrEmpty(value) ? null : value;

    private sealed class FakeStripeGateway : IStripeGateway
    {
        private int subscriptionLookupCount;

        public Guid? LastCustomerCompanyId { get; private set; }
        public string? LastCheckoutCustomerId { get; private set; }
        public int SubscriptionLookupCount => Volatile.Read(ref subscriptionLookupCount);
        public ConcurrentDictionary<string, StripeSubscriptionSnapshot> Subscriptions { get; } = new();

        public Task<string> CreateCustomerAsync(Company company, CancellationToken cancellationToken)
        {
            LastCustomerCompanyId = company.Id;
            return Task.FromResult("cus_m6_test");
        }

        public Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
            Company company,
            string customerId,
            CancellationToken cancellationToken)
        {
            LastCheckoutCustomerId = customerId;
            return Task.FromResult(new CheckoutSessionDto("cs_test_session", "https://checkout.stripe.test/session"));
        }

        public Task<StripeSubscriptionSnapshot?> GetSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref subscriptionLookupCount);
            Subscriptions.TryGetValue(subscriptionId, out var subscription);
            return Task.FromResult(subscription);
        }
    }
}
