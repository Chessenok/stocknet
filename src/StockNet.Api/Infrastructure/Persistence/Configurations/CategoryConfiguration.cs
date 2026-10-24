using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table =>
        {
            table.HasCheckConstraint("CK_Categories_Name_NotEmpty", "length(trim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Categories_Slug_NotEmpty", "length(trim(\"Slug\")) > 0");
        });

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedOnAdd();
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(500);
        builder.Property(category => category.IsActive).HasDefaultValue(true);

        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasIndex(category => new { category.IsActive, category.SortOrder });

        builder.HasMany(category => category.Stocks)
            .WithOne(stock => stock.Category)
            .HasForeignKey(stock => stock.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(CategorySeedData.Categories.ToArray());
    }
}
