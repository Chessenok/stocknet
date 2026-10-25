using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniExcelLibs;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Contracts.Companies;
using StockNet.Api.Contracts.Stocks;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Infrastructure.Seeding;
using Xunit;

namespace StockNet.Api.IntegrationTests;

public sealed class MvpCompletionIntegrationTests : IAsyncLifetime
{
    private readonly string previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? string.Empty;
    private readonly string previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
    private readonly string previousAdminEmail = Environment.GetEnvironmentVariable("Seed__AdminEmail") ?? string.Empty;
    private readonly string previousSupplierEmail = Environment.GetEnvironmentVariable("Seed__SupplierEmail") ?? string.Empty;
    private readonly string previousAdminPassword = Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? string.Empty;
    private readonly string previousSupplierPassword = Environment.GetEnvironmentVariable("Seed__SupplierPassword") ?? string.Empty;
    private readonly string previousCorsOrigin = Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0") ?? string.Empty;
    private readonly List<Guid> companyIds = [];
    private readonly string adminEmail = $"m7-admin-{Guid.NewGuid():N}@example.test";
    private readonly string supplierEmail = $"m7-supplier-{Guid.NewGuid():N}@example.test";
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__StockNet")))
        {
            throw new InvalidOperationException("Set ConnectionStrings__StockNet to a migrated PostgreSQL/PostGIS test database before running integration tests.");
        }

        Environment.SetEnvironmentVariable("Jwt__SigningKey", "StockNet-mvp-test-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", adminEmail);
        Environment.SetEnvironmentVariable("Seed__SupplierEmail", supplierEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", "M7-seeded-admin-password-123!");
        Environment.SetEnvironmentVariable("Seed__SupplierPassword", "M7-seeded-supplier-password-123!");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "https://frontend.stocknet.test");

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        client = factory.CreateClient();
        await client.GetAsync("/health");
    }

    [Fact]
    public async Task M7_profile_admin_cors_import_and_seeding_work_end_to_end()
    {
        await using var setupScope = factory!.Services.CreateAsyncScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var admin = await dbContext.Companies.SingleAsync(company => company.NormalizedEmail == adminEmail.ToUpperInvariant());
        var supplier = await dbContext.Companies.SingleAsync(company => company.NormalizedEmail == supplierEmail.ToUpperInvariant());
        Assert.True(admin.IsAdmin);
        Assert.True(admin.IsSubscribed);
        Assert.Null(admin.SubscriptionExpiresAt);
        Assert.False(supplier.IsAdmin);
        Assert.True(supplier.IsSubscribed);
        Assert.Null(supplier.SubscriptionExpiresAt);
        companyIds.Add(admin.Id);
        companyIds.Add(supplier.Id);

        var seeder = setupScope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        await seeder.SeedAsync(CancellationToken.None);
        Assert.Equal(1, await dbContext.Companies.CountAsync(company => company.NormalizedEmail == adminEmail.ToUpperInvariant()));
        Assert.Equal(1, await dbContext.Companies.CountAsync(company => company.NormalizedEmail == supplierEmail.ToUpperInvariant()));
        Assert.Equal(10, await dbContext.Categories.CountAsync());
        var seedsCategory = await dbContext.Categories.SingleAsync(category => category.Slug == "seeds");
        Assert.Equal(7, seedsCategory.ParentId);
        Assert.Equal(new[] { "moisture", "germination_rate" }, seedsCategory.RequiredAttributeKeys);
        var equipmentCategory = await dbContext.Categories.SingleAsync(category => category.Slug == "industrial-equipment");
        Assert.Equal(8, equipmentCategory.ParentId);

        var adminSession = await LoginAsync(adminEmail, "M7-seeded-admin-password-123!");
        var supplierSession = await LoginAsync(supplierEmail, "M7-seeded-supplier-password-123!");
        var newCompany = await RegisterCompanyAsync("M7 Import Company", "Producer");

        var profileUpdate = new UpdateCompanyProfileRequest
        {
            Name = "M7 Updated Company",
            TaxId = "M7-UPDATED-CUI",
            CountryCode = "us",
            CompanyType = CompanyType.Producer
        };
        using var updateProfileResponse = await SendJsonAsync(
            HttpMethod.Put,
            "/api/companies/me",
            supplierSession.AccessToken,
            profileUpdate);
        Assert.Equal(HttpStatusCode.OK, updateProfileResponse.StatusCode);
        var updatedProfile = await updateProfileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("M7 Updated Company", updatedProfile.GetProperty("name").GetString());
        Assert.Equal("M7-UPDATED-CUI", updatedProfile.GetProperty("taxId").GetString());
        Assert.Equal("US", updatedProfile.GetProperty("countryCode").GetString());
        Assert.False(updatedProfile.GetProperty("isAdmin").GetBoolean());

        using var anonymousAdminRequest = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            null,
            new ManualSubscriptionOverrideRequest { IsSubscribed = true });
        using var nonAdminRequest = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            supplierSession.AccessToken,
            new ManualSubscriptionOverrideRequest { IsSubscribed = true });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAdminRequest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdminRequest.StatusCode);

        using var perpetualOverride = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            adminSession.AccessToken,
            new ManualSubscriptionOverrideRequest { IsSubscribed = true, SubscriptionExpiresAt = null });
        Assert.Equal(HttpStatusCode.OK, perpetualOverride.StatusCode);
        var perpetualState = await perpetualOverride.Content.ReadFromJsonAsync<CompanySubscriptionDto>();
        Assert.True(perpetualState!.IsSubscribed);
        Assert.Null(perpetualState.SubscriptionExpiresAt);

        using var futureOverride = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            adminSession.AccessToken,
            new ManualSubscriptionOverrideRequest { IsSubscribed = true, SubscriptionExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        Assert.Equal(HttpStatusCode.OK, futureOverride.StatusCode);

        using var disabledOverride = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            adminSession.AccessToken,
            new ManualSubscriptionOverrideRequest { IsSubscribed = false, SubscriptionExpiresAt = null });
        Assert.Equal(HttpStatusCode.OK, disabledOverride.StatusCode);

        using var allowedCorsRequest = new HttpRequestMessage(HttpMethod.Options, "/api/companies/me");
        allowedCorsRequest.Headers.TryAddWithoutValidation("Origin", "https://frontend.stocknet.test");
        allowedCorsRequest.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
        allowedCorsRequest.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization");
        using var allowedCorsResponse = await client!.SendAsync(allowedCorsRequest);
        Assert.Equal(HttpStatusCode.NoContent, allowedCorsResponse.StatusCode);
        Assert.Equal("https://frontend.stocknet.test", allowedCorsResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var deniedCorsRequest = new HttpRequestMessage(HttpMethod.Options, "/api/companies/me");
        deniedCorsRequest.Headers.TryAddWithoutValidation("Origin", "https://untrusted.example.test");
        deniedCorsRequest.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
        using var deniedCorsResponse = await client.SendAsync(deniedCorsRequest);
        Assert.False(deniedCorsResponse.Headers.Contains("Access-Control-Allow-Origin"));

        var warehouseResponse = await SendJsonAsync(HttpMethod.Post, "/api/warehouses", newCompany.AccessToken, new
        {
            name = "M7 import warehouse",
            addressText = "M7 import test location",
            latitude = 45.0,
            longitude = 25.0
        });
        Assert.Equal(HttpStatusCode.Created, warehouseResponse.StatusCode);
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var warehouseId = warehouse.GetProperty("id").GetGuid();

        var workbook = CreateWorkbook(validRow: true);
        using var unsubscribedImport = await SendImportAsync(newCompany.AccessToken, warehouseId, workbook);
        Assert.Equal(HttpStatusCode.Forbidden, unsubscribedImport.StatusCode);

        using var enabledSubscription = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/companies/{newCompany.CompanyId}/subscription",
            adminSession.AccessToken,
            new ManualSubscriptionOverrideRequest { IsSubscribed = true, SubscriptionExpiresAt = null });
        Assert.Equal(HttpStatusCode.OK, enabledSubscription.StatusCode);

        using var importResponse = await SendImportAsync(newCompany.AccessToken, warehouseId, workbook);
        Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);
        var importResult = await importResponse.Content.ReadFromJsonAsync<StockImportResultDto>();
        Assert.Equal(1, importResult!.ImportedCount);

        using var invalidWorkbookImport = await SendImportAsync(newCompany.AccessToken, warehouseId, CreateWorkbook(validRow: false));
        Assert.Equal(HttpStatusCode.BadRequest, invalidWorkbookImport.StatusCode);

        using var importedStocksResponse = await SendJsonlessAsync(
            HttpMethod.Get,
            "/api/stocks/my",
            newCompany.AccessToken);
        var importedStocks = await importedStocksResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.Equal("M7 imported stock", Assert.Single(importedStocks!).GetProperty("title").GetString());

        var fractionalPiecesRequest = await SendJsonAsync(HttpMethod.Post, "/api/stocks", newCompany.AccessToken, new
        {
            warehouseId,
            categoryId = 8,
            unit = "Pieces",
            title = "Fractional pieces",
            description = "Must reject fractional discrete quantities",
            totalQuantity = 10,
            minOrderQuantity = 1,
            stepQuantity = 0.5m,
            packagingType = "Box",
            pricePerUnit = 1m,
            currency = "EUR",
            availableFrom = DateTimeOffset.UtcNow.AddDays(-1),
            availableTo = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.BadRequest, fractionalPiecesRequest.StatusCode);

        var missingSeedAttributesRequest = await SendJsonAsync(HttpMethod.Post, "/api/stocks", newCompany.AccessToken, new
        {
            warehouseId,
            categoryId = 9,
            unit = "Kilograms",
            title = "Seeds without technical attributes",
            description = "Must reject missing category attributes",
            totalQuantity = 10,
            minOrderQuantity = 1,
            stepQuantity = 0.5m,
            packagingType = "Bag",
            pricePerUnit = 1m,
            currency = "EUR",
            attributes = new { moisture = 10 },
            availableFrom = DateTimeOffset.UtcNow.AddDays(-1),
            availableTo = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingSeedAttributesRequest.StatusCode);

        var validSeedStockRequest = await SendJsonAsync(HttpMethod.Post, "/api/stocks", newCompany.AccessToken, new
        {
            warehouseId,
            categoryId = 9,
            unit = "Pieces",
            title = "Validated seed stock",
            description = "Has category technical attributes and whole quantities",
            totalQuantity = 100,
            minOrderQuantity = 10,
            stepQuantity = 5,
            packagingType = "Bag",
            pricePerUnit = 2.5m,
            currency = "EUR",
            attributes = new { moisture = 10, germination_rate = 95 },
            availableFrom = DateTimeOffset.UtcNow.AddDays(-1),
            availableTo = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, validSeedStockRequest.StatusCode);
        var createdSeedStock = await validSeedStockRequest.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pieces", createdSeedStock.GetProperty("unit").GetString());
        Assert.Equal(100m, createdSeedStock.GetProperty("totalQuantity").GetDecimal());
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
                var companyIdsForCleanup = companyIds.ToArray();
                var stocks = await dbContext.Stocks
                    .Where(stock => companyIdsForCleanup.Contains(stock.Warehouse.CompanyId))
                    .ToListAsync();
                dbContext.Stocks.RemoveRange(stocks);
                var warehouses = await dbContext.Warehouses
                    .Where(warehouse => companyIdsForCleanup.Contains(warehouse.CompanyId))
                    .ToListAsync();
                dbContext.Warehouses.RemoveRange(warehouses);
                var companies = await dbContext.Companies
                    .Where(company => companyIdsForCleanup.Contains(company.Id))
                    .ToListAsync();
                dbContext.Companies.RemoveRange(companies);
                await dbContext.SaveChangesAsync();
            }

            await factory.DisposeAsync();
        }

        client?.Dispose();
        Restore("Jwt__SigningKey", previousSigningKey);
        Restore("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        Restore("Seed__AdminEmail", previousAdminEmail);
        Restore("Seed__SupplierEmail", previousSupplierEmail);
        Restore("Seed__AdminPassword", previousAdminPassword);
        Restore("Seed__SupplierPassword", previousSupplierPassword);
        Restore("Cors__AllowedOrigins__0", previousCorsOrigin);
    }

    private async Task<AuthSessionDto> LoginAsync(string email, string password)
    {
        using var response = await client!.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthSessionDto>())!;
    }

    private async Task<AuthSessionDto> RegisterCompanyAsync(string name, string companyType)
    {
        var email = $"m7-{Guid.NewGuid():N}@example.test";
        using var response = await client!.PostAsJsonAsync("/api/auth/register", new
        {
            name,
            email,
            password = "M7-integration-secure-password!",
            taxId = $"M7-{Guid.NewGuid():N}",
            countryCode = "RO",
            companyType
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<AuthSessionDto>())!;
        companyIds.Add(session.CompanyId);
        return session;
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string? token, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendJsonlessAsync(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendImportAsync(string token, Guid warehouseId, byte[] workbook)
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(warehouseId.ToString()), "WarehouseId");
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(file, "File", "stocks.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/stocks/import") { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client!.SendAsync(request);
    }

    private static byte[] CreateWorkbook(bool validRow)
    {
        using var stream = new MemoryStream();
        var minOrderQuantity = validRow ? 2m : 20m;
        MiniExcel.SaveAs(stream, new[]
        {
            new
            {
                CategoryId = 1,
                Title = "M7 imported stock",
                Description = "Imported test stock",
                TotalQuantity = 10m,
                MinOrderQuantity = minOrderQuantity,
                StepQuantity = 1m,
                Unit = "Tons",
                PackagingType = "Bulk",
                PricePerUnit = 100m,
                Currency = "EUR",
                AttributesJson = "{\"steel_grade\":\"A\"}",
                AvailableFrom = DateTime.UtcNow.AddDays(-1),
                AvailableTo = DateTime.UtcNow.AddDays(30)
            }
        });

        return stream.ToArray();
    }

    private static void Restore(string name, string value) =>
        Environment.SetEnvironmentVariable(name, string.IsNullOrEmpty(value) ? null : value);
}
