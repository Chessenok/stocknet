using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Contracts.Messaging;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;
using StockNet.Api.Infrastructure.Persistence;
using Xunit;

namespace StockNet.Api.IntegrationTests;

public sealed class MessagingIntegrationTests : IAsyncLifetime
{
    private readonly string previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? string.Empty;
    private readonly string previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
    private readonly List<Guid> companyIds = [];
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private Guid sellerCompanyId;
    private Guid buyerCompanyId;
    private Guid stockId;
    private string sellerToken = string.Empty;
    private string buyerToken = string.Empty;
    private string outsiderToken = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__StockNet")))
        {
            throw new InvalidOperationException("Set ConnectionStrings__StockNet to a migrated PostgreSQL/PostGIS test database before running integration tests.");
        }

        Environment.SetEnvironmentVariable("Jwt__SigningKey", "StockNet-messaging-test-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        client = factory.CreateClient();

        var seller = await RegisterCompanyAsync("Seller", "Producer");
        var buyer = await RegisterCompanyAsync("Buyer", "Buyer");
        var outsider = await RegisterCompanyAsync("Outsider", "Buyer");
        sellerCompanyId = seller.CompanyId;
        buyerCompanyId = buyer.CompanyId;
        sellerToken = seller.AccessToken;
        buyerToken = buyer.AccessToken;
        outsiderToken = outsider.AccessToken;

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var warehouseId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            Id = warehouseId,
            CompanyId = sellerCompanyId,
            Name = "Messaging test warehouse",
            AddressText = "Test location",
            Location = new Point(-73.9857, 40.7484) { SRID = 4326 }
        };
        var now = DateTime.UtcNow;
        using var attributes = JsonDocument.Parse("{\"test\":true}");
        var stock = new Stock
        {
            WarehouseId = warehouseId,
            CategoryId = 1,
            Title = "Messaging test stock",
            Description = "Stock for messaging integration tests",
            TotalQuantityTons = 10,
            MinOrderQuantityTons = 1,
            StepQuantityTons = 1,
            PackagingType = "Bulk",
            PricePerTon = 100,
            Currency = "EUR",
            Attributes = JsonDocument.Parse(attributes.RootElement.GetRawText()),
            AvailableFrom = now.AddHours(-1),
            AvailableTo = now.AddHours(1),
            IsActive = true
        };
        var inactiveStock = new Stock
        {
            WarehouseId = warehouseId,
            CategoryId = 1,
            Title = "Inactive messaging test stock",
            Description = "Must not create a thread",
            TotalQuantityTons = 10,
            MinOrderQuantityTons = 1,
            StepQuantityTons = 1,
            PackagingType = "Bulk",
            PricePerTon = 100,
            Currency = "EUR",
            AvailableFrom = now.AddHours(-1),
            AvailableTo = now.AddHours(1),
            IsActive = false
        };

        dbContext.Warehouses.Add(warehouse);
        dbContext.Stocks.AddRange(stock, inactiveStock);
        await dbContext.SaveChangesAsync();
        stockId = stock.Id;
    }

    [Fact]
    public async Task Message_threads_enforce_participants_duplicate_prevention_and_stock_cascade_delete()
    {
        var selfThread = await SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", sellerToken, new { stockId });
        Assert.Equal(HttpStatusCode.BadRequest, selfThread.StatusCode);

        var missingStock = await SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", buyerToken, new { stockId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, missingStock.StatusCode);

        var inactiveStockId = await GetInactiveStockIdAsync();
        var inactiveThread = await SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", buyerToken, new { stockId = inactiveStockId });
        Assert.Equal(HttpStatusCode.NotFound, inactiveThread.StatusCode);

        var concurrentCreates = await Task.WhenAll(
            SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", buyerToken, new { stockId }),
            SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", buyerToken, new { stockId }));
        var createResponse = Assert.Single(concurrentCreates, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrentCreates, response => response.StatusCode == HttpStatusCode.Conflict);
        var thread = await createResponse.Content.ReadFromJsonAsync<MessageThreadDto>();
        Assert.NotNull(thread);
        Assert.Equal(buyerCompanyId, thread.BuyerCompanyId);
        Assert.Equal(sellerCompanyId, thread.SellerCompanyId);
        Assert.Equal(stockId, thread.StockId);

        var duplicateResponse = await SendAuthorizedAsync(HttpMethod.Post, "/api/message-threads", buyerToken, new { stockId });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        foreach (var response in concurrentCreates)
        {
            response.Dispose();
        }

        using var buyerThreadsResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/message-threads", buyerToken);
        using var sellerThreadsResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/message-threads", sellerToken);
        using var outsiderThreadsResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/message-threads", outsiderToken);
        var buyerThreads = await buyerThreadsResponse.Content.ReadFromJsonAsync<List<MessageThreadDto>>();
        var sellerThreads = await sellerThreadsResponse.Content.ReadFromJsonAsync<List<MessageThreadDto>>();
        var outsiderThreads = await outsiderThreadsResponse.Content.ReadFromJsonAsync<List<MessageThreadDto>>();
        Assert.Single(buyerThreads!);
        Assert.Single(sellerThreads!);
        Assert.Empty(outsiderThreads!);

        using var buyerThreadResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/api/message-threads/{thread.Id}", buyerToken);
        Assert.Equal(HttpStatusCode.OK, buyerThreadResponse.StatusCode);

        var outsiderThreadResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/api/message-threads/{thread.Id}", outsiderToken);
        var outsiderMessagesResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/api/message-threads/{thread.Id}/messages", outsiderToken);
        var outsiderSendResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/message-threads/{thread.Id}/messages",
            outsiderToken,
            new { text = "Unauthorized message" });
        Assert.Equal(HttpStatusCode.NotFound, outsiderThreadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderMessagesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderSendResponse.StatusCode);

        var buyerMessageResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/message-threads/{thread.Id}/messages",
            buyerToken,
            new { text = "Hello seller" });
        var sellerMessageResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/message-threads/{thread.Id}/messages",
            sellerToken,
            new { text = "Hello buyer" });
        Assert.Equal(HttpStatusCode.Created, buyerMessageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, sellerMessageResponse.StatusCode);

        using var firstPageResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/message-threads/{thread.Id}/messages?page=1&pageSize=1",
            buyerToken);
        using var secondPageResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/message-threads/{thread.Id}/messages?page=2&pageSize=1",
            sellerToken);
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<MessagePageDto>();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<MessagePageDto>();
        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal("Hello seller", Assert.Single(firstPage.Items).Text);
        Assert.Equal("Hello buyer", Assert.Single(secondPage!.Items).Text);
        Assert.Equal(buyerCompanyId, firstPage.Items[0].SenderCompanyId);
        Assert.Equal(sellerCompanyId, secondPage.Items[0].SenderCompanyId);

        var outsiderDelete = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/stocks/{stockId}", outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDelete.StatusCode);
        var ownerDelete = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/stocks/{stockId}", sellerToken);
        Assert.Equal(HttpStatusCode.NoContent, ownerDelete.StatusCode);

        var deletedThreadResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/api/message-threads/{thread.Id}", buyerToken);
        Assert.Equal(HttpStatusCode.NotFound, deletedThreadResponse.StatusCode);

        await using var scope = factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        Assert.False(await dbContext.MessageThreads.AnyAsync(item => item.Id == thread.Id));
        Assert.False(await dbContext.Messages.AnyAsync(item => item.ThreadId == thread.Id));
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
                var participantIds = companyIds.ToArray();
                var threads = await dbContext.MessageThreads
                    .Where(thread => participantIds.Contains(thread.BuyerCompanyId) || participantIds.Contains(thread.SellerCompanyId))
                    .ToListAsync();
                var threadIds = threads.Select(thread => thread.Id).ToArray();
                var messages = await dbContext.Messages
                    .Where(message => threadIds.Contains(message.ThreadId) || participantIds.Contains(message.SenderCompanyId))
                    .ToListAsync();
                dbContext.Messages.RemoveRange(messages);
                dbContext.MessageThreads.RemoveRange(threads);

                var stocks = await dbContext.Stocks
                    .Where(stock => stock.Warehouse.CompanyId == sellerCompanyId)
                    .ToListAsync();
                dbContext.Stocks.RemoveRange(stocks);
                var warehouses = await dbContext.Warehouses
                    .Where(warehouse => warehouse.CompanyId == sellerCompanyId)
                    .ToListAsync();
                dbContext.Warehouses.RemoveRange(warehouses);
                var companies = await dbContext.Companies
                    .Where(company => participantIds.Contains(company.Id))
                    .ToListAsync();
                dbContext.Companies.RemoveRange(companies);
                await dbContext.SaveChangesAsync();
            }

            await factory.DisposeAsync();
        }

        client?.Dispose();
        Environment.SetEnvironmentVariable("Jwt__SigningKey", string.IsNullOrEmpty(previousSigningKey) ? null : previousSigningKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", string.IsNullOrEmpty(previousEnvironment) ? null : previousEnvironment);
    }

    private async Task<Guid> GetInactiveStockIdAsync()
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        return await dbContext.Stocks
            .Where(stock => stock.Warehouse.CompanyId == sellerCompanyId && !stock.IsActive)
            .Select(stock => stock.Id)
            .SingleAsync();
    }

    private async Task<(Guid CompanyId, string AccessToken)> RegisterCompanyAsync(string name, string companyType)
    {
        var email = $"m5-{Guid.NewGuid():N}@example.test";
        var response = await client!.PostAsJsonAsync("/api/auth/register", new
        {
            name,
            email,
            password = "M5-integration-secure-password!",
            taxId = $"M5-{Guid.NewGuid():N}",
            countryCode = "RO",
            companyType
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AuthSessionDto>();
        Assert.NotNull(session);
        companyIds.Add(session.CompanyId);
        return (session.CompanyId, session.AccessToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client!.SendAsync(request);
    }
}
