using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses", table =>
        {
            table.HasCheckConstraint("CK_Warehouses_Name_NotEmpty", "length(trim(\"Name\")) > 0");
        });

        builder.HasKey(warehouse => warehouse.Id);
        builder.Property(warehouse => warehouse.Id).ValueGeneratedNever();
        builder.Property(warehouse => warehouse.Name).HasMaxLength(120).IsRequired();
        builder.Property(warehouse => warehouse.Location)
            .HasColumnType("geography (point, 4326)")
            .IsRequired();
        builder.Property(warehouse => warehouse.AddressText).HasMaxLength(500).IsRequired();
        builder.Property(warehouse => warehouse.CreatedAt).IsRequired();
        builder.Property(warehouse => warehouse.UpdatedAt).IsRequired();

        builder.HasIndex(warehouse => warehouse.CompanyId);
        builder.HasIndex(warehouse => warehouse.Location).HasMethod("gist");

        builder.HasMany(warehouse => warehouse.Stocks)
            .WithOne(stock => stock.Warehouse)
            .HasForeignKey(stock => stock.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
