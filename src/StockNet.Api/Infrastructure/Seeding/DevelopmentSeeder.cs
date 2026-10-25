using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Infrastructure.Persistence.Configurations;

namespace StockNet.Api.Infrastructure.Seeding;

public sealed class DevelopmentSeeder(
    StockNetDbContext dbContext,
    IPasswordHasher<Company> passwordHasher,
    IConfiguration configuration,
    ILogger<DevelopmentSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var categoriesAdded = 0;
        foreach (var categorySeed in CategorySeedData.Categories)
        {
            var exists = await dbContext.Categories
                .AnyAsync(
                    category => category.Slug == categorySeed.Slug || category.Name == categorySeed.Name,
                    cancellationToken);
            if (!exists)
            {
                dbContext.Categories.Add(new Category
                {
                    Name = categorySeed.Name,
                    Slug = categorySeed.Slug,
                    Description = categorySeed.Description,
                    ParentId = categorySeed.ParentId,
                    SortOrder = categorySeed.SortOrder,
                    IsActive = categorySeed.IsActive,
                    RequiredAttributeKeys = categorySeed.RequiredAttributeKeys.ToArray()
                });
                categoriesAdded++;
            }
        }

        var adminCreated = await SeedCompanyAsync(
            configuration["Seed:AdminEmail"] ?? "admin@stocknet.local",
            "StockNet Development Admin",
            "Seed:AdminPassword",
            CompanyType.Producer,
            isAdmin: true,
            cancellationToken);
        var supplierCreated = await SeedCompanyAsync(
            configuration["Seed:SupplierEmail"] ?? "supplier@stocknet.local",
            "StockNet Development Supplier",
            "Seed:SupplierPassword",
            CompanyType.Producer,
            isAdmin: false,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        if (categoriesAdded > 0 || adminCreated || supplierCreated)
        {
            logger.LogInformation(
                "Development seed data initialized. Added {CategoryCount} categories and {CompanyCount} companies.",
                categoriesAdded,
                (adminCreated ? 1 : 0) + (supplierCreated ? 1 : 0));
        }
    }

    private async Task<bool> SeedCompanyAsync(
        string email,
        string name,
        string passwordConfigurationKey,
        CompanyType companyType,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var exists = await dbContext.Companies
            .AnyAsync(company => company.NormalizedEmail == normalizedEmail, cancellationToken);
        if (exists)
        {
            return false;
        }

        var password = configuration[passwordConfigurationKey];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                $"Development seeding requires {passwordConfigurationKey} with at least 12 characters in user-secrets or environment configuration.");
        }

        var company = new Company
        {
            Name = name,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            TaxId = isAdmin ? "DEV-ADMIN" : "DEV-SUPPLIER",
            CountryCode = "RO",
            CompanyType = companyType,
            IsVerified = true,
            IsAdmin = isAdmin,
            IsSubscribed = true,
            SubscriptionExpiresAt = null
        };
        company.PasswordHash = passwordHasher.HashPassword(company, password);
        dbContext.Companies.Add(company);
        return true;
    }
}
