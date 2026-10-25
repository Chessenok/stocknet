using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public static class CategorySeedData
{
    public static IReadOnlyList<Category> Categories { get; } =
    [
        new Category
        {
            Id = 1,
            Name = "Metals",
            Slug = "metals",
            Description = "Ferrous and non-ferrous raw materials",
            SortOrder = 10,
            AttributeRules =
            [
                new CategoryAttributeRule
                {
                    Key = "steel_grade",
                    Type = CategoryAttributeType.String,
                    IsRequired = true,
                    AllowedValues = ["a36", "s235", "s355"]
                }
            ]
        },
        new Category { Id = 2, Name = "Plastics", Slug = "plastics", Description = "Plastic resins and recycled polymers", SortOrder = 20 },
        new Category { Id = 3, Name = "Wood", Slug = "wood", Description = "Timber, lumber, and wood-based materials", SortOrder = 30 },
        new Category { Id = 4, Name = "Paper", Slug = "paper", Description = "Paper, pulp, and cardboard materials", SortOrder = 40 },
        new Category { Id = 5, Name = "Construction Materials", Slug = "construction-materials", Description = "Aggregates and other construction raw materials", SortOrder = 50 },
        new Category { Id = 6, Name = "Chemicals", Slug = "chemicals", Description = "Industrial and agricultural chemicals", SortOrder = 60 },
        new Category { Id = 7, Name = "Agricultural Materials", Slug = "agricultural-materials", Description = "Agricultural raw materials and by-products", SortOrder = 70 },
        new Category { Id = 8, Name = "Other", Slug = "other", Description = "Other raw materials", SortOrder = 80 },
        new Category
        {
            Id = 9,
            Name = "Seeds",
            Slug = "seeds",
            Description = "Seeds and planting material",
            ParentId = 7,
            SortOrder = 10,
            AttributeRules =
            [
                new CategoryAttributeRule { Key = "moisture", Type = CategoryAttributeType.Number, IsRequired = true, Min = 0, Max = 100 },
                new CategoryAttributeRule { Key = "germination_rate", Type = CategoryAttributeType.Number, IsRequired = true, Min = 0, Max = 100 }
            ]
        },
        new Category
        {
            Id = 10,
            Name = "Industrial Equipment",
            Slug = "industrial-equipment",
            Description = "Equipment and discrete industrial goods",
            ParentId = 8,
            SortOrder = 10,
            AttributeRules =
            [
                new CategoryAttributeRule
                {
                    Key = "equipment_type",
                    Type = CategoryAttributeType.String,
                    AllowedValues = ["compressor", "motor", "pump"]
                }
            ]
        }
    ];
}
