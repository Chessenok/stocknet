using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Categories;

public sealed record CategoryAttributeRuleDto(
    string Key,
    CategoryAttributeType Type,
    bool IsRequired,
    decimal? Min,
    decimal? Max,
    IReadOnlyList<string> AllowedValues);
