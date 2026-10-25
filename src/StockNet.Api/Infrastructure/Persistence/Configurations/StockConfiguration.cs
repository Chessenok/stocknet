using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks", table =>
        {
            table.HasCheckConstraint("CK_Stocks_TotalQuantity_Positive", "\"TotalQuantity\" > 0");
            table.HasCheckConstraint("CK_Stocks_MinOrderQuantity_Valid", "\"MinOrderQuantity\" > 0 AND \"MinOrderQuantity\" <= \"TotalQuantity\"");
            table.HasCheckConstraint("CK_Stocks_StepQuantity_Positive", "\"StepQuantity\" > 0");
            table.HasCheckConstraint("CK_Stocks_PricePerUnit_Positive", "\"PricePerUnit\" > 0");
            table.HasCheckConstraint(
                "CK_Stocks_Unit_Allowed",
                "\"Unit\" IN ('Tons', 'Kilograms', 'Pieces', 'Units', 'CubicMeters', 'Liters')");
            table.HasCheckConstraint(
                "CK_Stocks_DiscreteUnits_IntegralQuantities",
                "\"Unit\" NOT IN ('Pieces', 'Units') OR (\"TotalQuantity\" = trunc(\"TotalQuantity\") AND \"MinOrderQuantity\" = trunc(\"MinOrderQuantity\") AND \"StepQuantity\" = trunc(\"StepQuantity\") AND \"StepQuantity\" >= 1)");
            table.HasCheckConstraint("CK_Stocks_AvailabilityWindow_Valid", "\"AvailableTo\" >= \"AvailableFrom\"");
            table.HasCheckConstraint("CK_Stocks_Currency_Length", "length(\"Currency\") = 3");
        });

        builder.HasKey(stock => stock.Id);
        builder.Property(stock => stock.Id).ValueGeneratedNever();
        builder.Property(stock => stock.Title).HasMaxLength(200).IsRequired();
        builder.Property(stock => stock.Description).HasMaxLength(5000).IsRequired();
        builder.Property(stock => stock.TotalQuantity).HasPrecision(18, 3);
        builder.Property(stock => stock.MinOrderQuantity).HasPrecision(18, 3);
        builder.Property(stock => stock.StepQuantity).HasPrecision(18, 3);
        builder.Property(stock => stock.Unit)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(UnitOfMeasure.Tons)
            .IsRequired();
        builder.Property(stock => stock.PackagingType).HasMaxLength(64).IsRequired();
        builder.Property(stock => stock.PricePerUnit).HasPrecision(18, 4);
        builder.Property(stock => stock.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(stock => stock.Attributes).HasColumnType("jsonb");
        builder.Property(stock => stock.AvailableFrom).IsRequired();
        builder.Property(stock => stock.AvailableTo).IsRequired();
        builder.Property(stock => stock.IsActive).HasDefaultValue(true);
        builder.Property(stock => stock.CreatedAt).IsRequired();
        builder.Property(stock => stock.UpdatedAt).IsRequired();

        builder.HasIndex(stock => new { stock.IsActive, stock.AvailableFrom, stock.AvailableTo });
        builder.HasIndex(stock => new { stock.CategoryId, stock.IsActive });
        builder.HasIndex(stock => stock.WarehouseId);

        builder.HasMany(stock => stock.MessageThreads)
            .WithOne(thread => thread.Stock)
            .HasForeignKey(thread => thread.StockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
