using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks", table =>
        {
            table.HasCheckConstraint("CK_Stocks_TotalQuantityTons_Positive", "\"TotalQuantityTons\" > 0");
            table.HasCheckConstraint("CK_Stocks_MinOrderQuantityTons_Valid", "\"MinOrderQuantityTons\" > 0 AND \"MinOrderQuantityTons\" <= \"TotalQuantityTons\"");
            table.HasCheckConstraint("CK_Stocks_StepQuantityTons_Positive", "\"StepQuantityTons\" > 0");
            table.HasCheckConstraint("CK_Stocks_PricePerTon_NonNegative", "\"PricePerTon\" >= 0");
            table.HasCheckConstraint("CK_Stocks_AvailabilityWindow_Valid", "\"AvailableTo\" >= \"AvailableFrom\"");
            table.HasCheckConstraint("CK_Stocks_Currency_Length", "length(\"Currency\") = 3");
        });

        builder.HasKey(stock => stock.Id);
        builder.Property(stock => stock.Id).ValueGeneratedNever();
        builder.Property(stock => stock.Title).HasMaxLength(200).IsRequired();
        builder.Property(stock => stock.Description).HasMaxLength(5000).IsRequired();
        builder.Property(stock => stock.TotalQuantityTons).HasPrecision(18, 3);
        builder.Property(stock => stock.MinOrderQuantityTons).HasPrecision(18, 3);
        builder.Property(stock => stock.StepQuantityTons).HasPrecision(18, 3);
        builder.Property(stock => stock.PackagingType).HasMaxLength(64).IsRequired();
        builder.Property(stock => stock.PricePerTon).HasPrecision(18, 4);
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
