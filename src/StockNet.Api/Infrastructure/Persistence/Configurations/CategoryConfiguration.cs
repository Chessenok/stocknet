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

        builder.HasData(
            new Category { Id = 1, Name = "Metals", Slug = "metals", Description = "Ferrous and non-ferrous raw materials", SortOrder = 10 },
            new Category { Id = 2, Name = "Plastics", Slug = "plastics", Description = "Plastic resins and recycled polymers", SortOrder = 20 },
            new Category { Id = 3, Name = "Wood", Slug = "wood", Description = "Timber, lumber, and wood-based materials", SortOrder = 30 },
            new Category { Id = 4, Name = "Paper", Slug = "paper", Description = "Paper, pulp, and cardboard materials", SortOrder = 40 },
            new Category { Id = 5, Name = "Construction Materials", Slug = "construction-materials", Description = "Aggregates and other construction raw materials", SortOrder = 50 },
            new Category { Id = 6, Name = "Chemicals", Slug = "chemicals", Description = "Industrial and agricultural chemicals", SortOrder = 60 },
            new Category { Id = 7, Name = "Agricultural Materials", Slug = "agricultural-materials", Description = "Agricultural raw materials and by-products", SortOrder = 70 },
            new Category { Id = 8, Name = "Other", Slug = "other", Description = "Other raw materials", SortOrder = 80 });
    }
}
