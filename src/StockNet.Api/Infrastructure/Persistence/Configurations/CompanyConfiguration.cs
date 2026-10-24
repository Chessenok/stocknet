using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).ValueGeneratedNever();
        builder.Property(company => company.Name).HasMaxLength(200).IsRequired();
        builder.Property(company => company.Email).HasMaxLength(320).IsRequired();
        builder.Property(company => company.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(company => company.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(company => company.TaxId).HasMaxLength(64);
        builder.Property(company => company.CountryCode).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(company => company.CompanyType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(company => company.IsVerified).HasDefaultValue(false);
        builder.Property(company => company.IsAdmin).HasDefaultValue(false);
        builder.Property(company => company.IsSubscribed).HasDefaultValue(false);
        builder.Property(company => company.CreatedAt).IsRequired();
        builder.Property(company => company.UpdatedAt).IsRequired();
        builder.Property(company => company.StripeCustomerId).HasMaxLength(128);
        builder.Property(company => company.StripeSubscriptionId).HasMaxLength(128);

        builder.HasIndex(company => company.NormalizedEmail).IsUnique();
        builder.HasIndex(company => company.StripeCustomerId).IsUnique();
        builder.HasIndex(company => company.StripeSubscriptionId).IsUnique();
        builder.HasIndex(company => new { company.CountryCode, company.TaxId });

        builder.HasMany(company => company.Warehouses)
            .WithOne(warehouse => warehouse.Company)
            .HasForeignKey(warehouse => warehouse.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(company => company.BuyerMessageThreads)
            .WithOne(thread => thread.BuyerCompany)
            .HasForeignKey(thread => thread.BuyerCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(company => company.SellerMessageThreads)
            .WithOne(thread => thread.SellerCompany)
            .HasForeignKey(thread => thread.SellerCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(company => company.SentMessages)
            .WithOne(message => message.SenderCompany)
            .HasForeignKey(message => message.SenderCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
