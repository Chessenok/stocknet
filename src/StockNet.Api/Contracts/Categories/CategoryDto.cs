namespace StockNet.Api.Contracts.Categories;

public sealed record CategoryDto(
    int Id,
    int? ParentId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    IReadOnlyList<CategoryAttributeRuleDto> AttributeRules);
