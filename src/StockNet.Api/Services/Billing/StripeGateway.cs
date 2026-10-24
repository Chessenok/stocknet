using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using StockNet.Api.Contracts.Billing;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Options;

namespace StockNet.Api.Services.Billing;

public sealed class StripeGateway : IStripeGateway
{
    private readonly IOptions<StripeOptions> options;
    private readonly Lazy<StripeClient> stripeClient;

    public StripeGateway(IOptions<StripeOptions> options)
    {
        this.options = options;
        stripeClient = new Lazy<StripeClient>(() =>
        {
            options.Value.ValidateApiKey();
            return new StripeClient(options.Value.ApiKey);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string> CreateCustomerAsync(Company company, CancellationToken cancellationToken)
    {
        options.Value.ValidateApiKey();

        var customer = await new CustomerService(GetStripeClient()).CreateAsync(
            new CustomerCreateOptions
            {
                Name = company.Name,
                Email = company.Email,
                Metadata = new Dictionary<string, string>
                {
                    ["CompanyId"] = company.Id.ToString()
                }
            },
            new RequestOptions { IdempotencyKey = $"stocknet-company-customer-{company.Id}" },
            cancellationToken);

        return customer.Id;
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        Company company,
        string customerId,
        CancellationToken cancellationToken)
    {
        options.Value.ValidateApiKey();
        var companyMetadata = new Dictionary<string, string>
        {
            ["CompanyId"] = company.Id.ToString()
        };

        var session = await new SessionService(GetStripeClient()).CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                LineItems = [new SessionLineItemOptions { Price = options.Value.PriceId, Quantity = 1 }],
                SuccessUrl = options.Value.SuccessUrl,
                CancelUrl = options.Value.CancelUrl,
                ClientReferenceId = company.Id.ToString(),
                Metadata = companyMetadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = companyMetadata }
            },
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException("Stripe did not return a Checkout URL.");
        }

        return new CheckoutSessionDto(session.Id, session.Url);
    }

    public async Task<StripeSubscriptionSnapshot?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        options.Value.ValidateApiKey();
        try
        {
            var subscription = await new SubscriptionService(GetStripeClient())
                .GetAsync(subscriptionId, cancellationToken: cancellationToken);

            return subscription is null ? null : StripeSubscriptionSnapshot.FromSubscription(subscription);
        }
        catch (StripeException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private StripeClient GetStripeClient() => stripeClient.Value;
}
