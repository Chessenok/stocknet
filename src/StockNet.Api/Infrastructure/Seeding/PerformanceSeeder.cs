using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Infrastructure.Seeding;

public sealed class PerformanceSeeder(StockNetDbContext dbContext, ILogger<PerformanceSeeder> logger)
{
    public async Task SeedAsync(
        int companyCount,
        int warehousesPerCompany,
        int stocksPerWarehouse,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(item => item.IsActive
                && (item.Slug == "metals" || item.Slug == "industrial-equipment" || item.Slug == "seeds"))
            .ToDictionaryAsync(item => item.Slug, cancellationToken);

        if (!categories.TryGetValue("metals", out var metalsCategory))
        {
            metalsCategory = new Category
            {
                Name = "Performance Test Category",
                Slug = "performance-test-category",
                Description = "Generated for load testing.",
                SortOrder = 999,
                IsActive = true
            };
            dbContext.Categories.Add(metalsCategory);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        categories.TryGetValue("industrial-equipment", out var equipmentCategory);
        categories.TryGetValue("seeds", out var seedsCategory);

        var companies = new List<Company>(companyCount);
        for (var i = 0; i < companyCount; i++)
        {
            var company = new Company
            {
                Name = $"Perf Company {i + 1}",
                Email = $"perf-company-{i + 1}@example.test",
                NormalizedEmail = $"PERF-COMPANY-{i + 1}@EXAMPLE.TEST",
                PasswordHash = "not-used",
                CountryCode = "RO",
                CompanyType = Domain.Enums.CompanyType.Producer,
                IsVerified = true,
                IsAdmin = false,
                IsSubscribed = true,
                SubscriptionExpiresAt = null
            };
            companies.Add(company);
        }

        dbContext.Companies.AddRange(companies);
        await dbContext.SaveChangesAsync(cancellationToken);

        var random = new Random(42);
        var warehouses = new List<Warehouse>();
        foreach (var company in companies)
        {
            for (var w = 0; w < warehousesPerCompany; w++)
            {
                warehouses.Add(new Warehouse
                {
                    CompanyId = company.Id,
                    Name = $"Warehouse {w + 1}",
                    AddressText = $"Performance address {w + 1}",
                    Location = company.Name == "Perf Company 1" && w == 0
                        ? new Point(25, 45) { SRID = 4326 }
                        : new Point(
                            20 + random.NextDouble() * 10,
                            43 + random.NextDouble() * 8)
                        { SRID = 4326 },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        dbContext.Warehouses.AddRange(warehouses);
        await dbContext.SaveChangesAsync(cancellationToken);

        var stocks = new List<Stock>();
        var equipmentTypes = new[] { "compressor", "motor", "pump" };
        var steelGrades = new[] { "a36", "s235", "s355" };
        var moistureValues = new[] { 5m, 7.5m, 10m };
        var germinationValues = new[] { 85m, 90m, 95m };
        foreach (var warehouse in warehouses)
        {
            for (var s = 0; s < stocksPerWarehouse; s++)
            {
                var attributeKind = s % 3;
                var isEquipment = equipmentCategory is not null && attributeKind == 0;
                var isSeeds = seedsCategory is not null && attributeKind == 1;
                var attributeValueIndex = (s / 3) % 3;
                var attributes = isEquipment
                    ? new Dictionary<string, object>
                    {
                        ["equipment_type"] = equipmentTypes[attributeValueIndex],
                        ["source"] = "performance-seeder"
                    }
                    : isSeeds
                    ? new Dictionary<string, object>
                    {
                        ["moisture"] = moistureValues[attributeValueIndex],
                        ["germination_rate"] = germinationValues[(s / 9) % 3],
                        ["source"] = "performance-seeder"
                    }
                    : new Dictionary<string, object>
                    {
                        ["steel_grade"] = steelGrades[attributeValueIndex],
                        ["source"] = "performance-seeder"
                    };

                stocks.Add(new Stock
                {
                    WarehouseId = warehouse.Id,
                    CategoryId = isEquipment ? equipmentCategory!.Id : isSeeds ? seedsCategory!.Id : metalsCategory.Id,
                    Title = $"Perf Stock {s + 1}",
                    Description = "Generated for load testing.",
                    TotalQuantity = 100m,
                    MinOrderQuantity = 1m,
                    StepQuantity = 1m,
                    Unit = isEquipment ? Domain.Enums.UnitOfMeasure.Pieces : Domain.Enums.UnitOfMeasure.Tons,
                    PackagingType = isEquipment ? "Equipment" : isSeeds ? "Bagged" : "Bulk",
                    PricePerUnit = 50m + (s % 10),
                    Currency = "EUR",
                    Attributes = JsonDocument.Parse(JsonSerializer.Serialize(attributes)),
                    AvailableFrom = DateTime.UtcNow.AddDays(-1),
                    AvailableTo = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        dbContext.Stocks.AddRange(stocks);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Performance seed data initialized. Companies: {CompanyCount}, Warehouses: {WarehouseCount}, Stocks: {StockCount}.",
            companies.Count,
            warehouses.Count,
            stocks.Count);
    }
}
