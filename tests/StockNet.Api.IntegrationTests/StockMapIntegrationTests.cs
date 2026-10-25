using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Npgsql;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;
using StockNet.Api.Infrastructure.Persistence;
using Xunit;

namespace StockNet.Api.IntegrationTests;

public sealed class StockMapIntegrationTests : IAsyncLifetime
{
    private const double CenterLatitude = 40.7484;
    private const double CenterLongitude = -73.9857;
    private readonly string previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__StockNet") ?? string.Empty;
    private readonly string previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? string.Empty;
    private readonly string previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
    private readonly Guid companyId = Guid.NewGuid();
    private readonly Guid nearbyWarehouseId = Guid.NewGuid();
    private readonly Guid distantWarehouseId = Guid.NewGuid();
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private string normalizedEmail = string.Empty;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__StockNet");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__StockNet to a migrated PostgreSQL/PostGIS test database before running integration tests.");
        }

        Environment.SetEnvironmentVariable("Jwt__SigningKey", "StockNet-integration-test-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var uniquePart = Guid.NewGuid().ToString("N");
        normalizedEmail = $"M4-MAP-{uniquePart}@EXAMPLE.TEST";

        var company = new Company
        {
            Id = companyId,
            Name = "Spatial map integration test",
            Email = normalizedEmail.ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = "unused-integration-test-hash",
            CountryCode = "US",
            CompanyType = CompanyType.Producer,
            IsSubscribed = false
        };

        var nearbyWarehouse = new Warehouse
        {
            Id = nearbyWarehouseId,
            CompanyId = companyId,
            Name = "Nearby integration warehouse",
            AddressText = "Test location",
            Location = new Point(CenterLongitude, CenterLatitude) { SRID = 4326 }
        };
        var distantWarehouse = new Warehouse
        {
            Id = distantWarehouseId,
            CompanyId = companyId,
            Name = "Distant integration warehouse",
            AddressText = "Distant test location",
            Location = new Point(-74.5, 40.7) { SRID = 4326 }
        };

        var now = DateTime.UtcNow;
        using var attributes = JsonDocument.Parse("{\"grade\":\"A\",\"steel_grade\":\"a36\"}");
        using var otherGradeAttributes = JsonDocument.Parse("{\"steel_grade\":\"s235\"}");
        dbContext.Companies.Add(company);
        dbContext.Warehouses.AddRange(nearbyWarehouse, distantWarehouse);
        dbContext.Stocks.AddRange(
            NewStock(nearbyWarehouseId, 1, "Nearby active stock", 100m, now.AddHours(-1), now.AddHours(1), true, attributes),
            NewStock(nearbyWarehouseId, 1, "Nearby different steel grade", 100m, now.AddHours(-1), now.AddHours(1), true, otherGradeAttributes),
            NewStock(nearbyWarehouseId, 1, "Nearby inactive stock", 100m, now.AddHours(-1), now.AddHours(1), false, attributes),
            NewStock(nearbyWarehouseId, 1, "Nearby expired stock", 100m, now.AddDays(-2), now.AddDays(-1), true, attributes),
            NewStock(distantWarehouseId, 1, "Distant active stock", 100m, now.AddHours(-1), now.AddHours(1), true, attributes),
            NewStock(nearbyWarehouseId, 2, "Nearby other category", 125m, now.AddHours(-1), now.AddHours(1), true, attributes));

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Map_returns_only_public_active_in_window_stocks_and_uses_spatial_index()
    {
        var mapPath = string.Create(
            CultureInfo.InvariantCulture,
            $"/api/stocks/map?latitude={CenterLatitude}&longitude={CenterLongitude}&radius=5000&categoryId=1&minPrice=50&maxPrice=200&page=1&pageSize=100");
        var response = await client!.GetAsync(mapPath);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected map search success; received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var stocks = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.Equal(2, stocks!.Count);
        var stock = Assert.Single(stocks, item => item.GetProperty("title").GetString() == "Nearby active stock");

        Assert.Equal("Nearby active stock", stock.GetProperty("title").GetString());
        Assert.Equal("Metals", stock.GetProperty("category").GetString());
        Assert.Equal(CenterLatitude, stock.GetProperty("latitude").GetDouble(), precision: 5);
        Assert.Equal(CenterLongitude, stock.GetProperty("longitude").GetDouble(), precision: 5);
        Assert.InRange(stock.GetProperty("distanceMeters").GetDouble(), 0, 1);
        Assert.Equal("A", stock.GetProperty("attributes").GetProperty("grade").GetString());
        Assert.False(stock.TryGetProperty("companyId", out _));
        Assert.False(stock.TryGetProperty("warehouseId", out _));

        var matchingAttributes = Uri.EscapeDataString("{\"steel_grade\":\"a36\"}");
        var filteredResponse = await client.GetAsync($"{mapPath}&attributeFilters={matchingAttributes}");
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var filteredStocks = await filteredResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.Equal("Nearby active stock", Assert.Single(filteredStocks!).GetProperty("title").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/stocks/my")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/stocks/map?latitude=91&longitude=0&radius=10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/stocks/map?latitude=0&longitude=0&radius=500001")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/stocks/map?latitude=0&longitude=0&radius=10&pageSize=101")).StatusCode);

        await using var scope = factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
        var center = new Point(CenterLongitude, CenterLatitude) { SRID = 4326 };
        var generatedSql = dbContext.Stocks
            .AsNoTracking()
            .Where(item => item.Warehouse.Location.IsWithinDistance(center, 5000))
            .ToQueryString();
        Assert.Contains("ST_DWithin", generatedSql, StringComparison.OrdinalIgnoreCase);
        using var jsonFilter = JsonDocument.Parse("{\"steel_grade\":\"a36\"}");
        var jsonFilterSql = dbContext.Stocks
            .AsNoTracking()
            .Where(item => item.Attributes != null && EF.Functions.JsonContains(item.Attributes, jsonFilter))
            .ToQueryString();
        Assert.Contains(" @> ", jsonFilterSql, StringComparison.Ordinal);

        await dbContext.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setPlanner = new NpgsqlCommand("SET LOCAL enable_seqscan = off", connection, transaction))
        {
            await setPlanner.ExecuteNonQueryAsync();
        }

        const string explainSql = """
            EXPLAIN (COSTS OFF)
            SELECT "Id"
            FROM "Warehouses"
            WHERE ST_DWithin(
                "Location",
                ST_SetSRID(ST_MakePoint(-73.9857, 40.7484), 4326)::geography,
                5000)
            """;
        var planLines = new List<string>();
        await using (var explain = new NpgsqlCommand(explainSql, connection, transaction))
        {
            await using var reader = await explain.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planLines.Add(reader.GetString(0));
            }
        }

        Assert.Contains("IX_Warehouses_Location", string.Join(Environment.NewLine, planLines), StringComparison.OrdinalIgnoreCase);

        const string jsonbExplainSql = "EXPLAIN (COSTS OFF) SELECT \"Id\" FROM \"Stocks\" WHERE \"Attributes\" @> '{\"steel_grade\":\"a36\"}'::jsonb";
        var jsonbPlanLines = await ReadPlanAsync(connection, transaction, jsonbExplainSql);
        Assert.Contains("IX_Stocks_Attributes", string.Join(Environment.NewLine, jsonbPlanLines), StringComparison.OrdinalIgnoreCase);

        var ownershipExplainSql = $"EXPLAIN (COSTS OFF) SELECT s.\"Id\" FROM \"Stocks\" AS s INNER JOIN \"Warehouses\" AS w ON s.\"WarehouseId\" = w.\"Id\" WHERE w.\"CompanyId\" = '{companyId}' ORDER BY s.\"CreatedAt\" DESC, s.\"Id\"";
        var ownershipPlanLines = await ReadPlanAsync(connection, transaction, ownershipExplainSql);
        var ownershipPlan = string.Join(Environment.NewLine, ownershipPlanLines);
        Assert.Contains("IX_Warehouses_CompanyId", ownershipPlan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_Stocks_WarehouseId", ownershipPlan, StringComparison.OrdinalIgnoreCase);
        await transaction.RollbackAsync();
        await dbContext.Database.CloseConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StockNetDbContext>();
                var stocks = await dbContext.Stocks
                    .Where(stock => stock.Warehouse.CompanyId == companyId)
                    .ToListAsync();
                dbContext.Stocks.RemoveRange(stocks);

                var warehouses = await dbContext.Warehouses
                    .Where(warehouse => warehouse.CompanyId == companyId)
                    .ToListAsync();
                dbContext.Warehouses.RemoveRange(warehouses);

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
        Environment.SetEnvironmentVariable("ConnectionStrings__StockNet", string.IsNullOrEmpty(previousConnectionString) ? null : previousConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", string.IsNullOrEmpty(previousSigningKey) ? null : previousSigningKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", string.IsNullOrEmpty(previousEnvironment) ? null : previousEnvironment);
    }

    private static Stock NewStock(
        Guid warehouseId,
        int categoryId,
        string title,
        decimal price,
        DateTime availableFrom,
        DateTime availableTo,
        bool isActive,
        JsonDocument attributes) => new()
        {
            WarehouseId = warehouseId,
            CategoryId = categoryId,
            Title = title,
            Description = "Integration test stock",
            TotalQuantity = 10,
            MinOrderQuantity = 1,
            StepQuantity = 1,
            PackagingType = "Bulk",
            PricePerUnit = price,
            Currency = "EUR",
            Attributes = JsonDocument.Parse(attributes.RootElement.GetRawText()),
            AvailableFrom = availableFrom,
            AvailableTo = availableTo,
            IsActive = isActive
        };

    private static async Task<List<string>> ReadPlanAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string explainSql)
    {
        var planLines = new List<string>();
        await using var explain = new NpgsqlCommand(explainSql, connection, transaction);
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            planLines.Add(reader.GetString(0));
        }

        return planLines;
    }
}
