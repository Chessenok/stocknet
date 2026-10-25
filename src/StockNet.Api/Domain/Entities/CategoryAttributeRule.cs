using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Domain.Entities;

public sealed record CategoryAttributeRule
{
    public string Key { get; init; } = string.Empty;
    public CategoryAttributeType Type { get; init; } = CategoryAttributeType.String;
    public bool IsRequired { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public string[] AllowedValues { get; init; } = [];
}
