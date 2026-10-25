using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private static readonly JsonSerializerOptions AttributeRulesJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
        var attributeRules = builder.Property(category => category.AttributeRules)
            .HasColumnType("jsonb")
            .HasConversion(
                rules => JsonSerializer.Serialize(rules, AttributeRulesJsonOptions),
                json => JsonSerializer.Deserialize<CategoryAttributeRule[]>(json, AttributeRulesJsonOptions) ?? Array.Empty<CategoryAttributeRule>())
            .HasDefaultValueSql("'[]'::jsonb");
        attributeRules.Metadata.SetValueComparer(new ValueComparer<CategoryAttributeRule[]>(
            (left, right) => JsonSerializer.Serialize(left, AttributeRulesJsonOptions)
                == JsonSerializer.Serialize(right, AttributeRulesJsonOptions),
            value => JsonSerializer.Serialize(value, AttributeRulesJsonOptions).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<CategoryAttributeRule[]>(
                JsonSerializer.Serialize(value, AttributeRulesJsonOptions),
                AttributeRulesJsonOptions) ?? Array.Empty<CategoryAttributeRule>()));

        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasIndex(category => new { category.IsActive, category.SortOrder });

        builder.HasOne(category => category.Parent)
            .WithMany(category => category.Children)
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(category => category.ParentId);

        builder.HasMany(category => category.Stocks)
            .WithOne(stock => stock.Category)
            .HasForeignKey(stock => stock.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(CategorySeedData.Categories.ToArray());
    }
}
