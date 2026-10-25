using System.ComponentModel.DataAnnotations;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

public sealed class StockMapQuery : IValidatableObject
{
    [Required, Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; init; }

    [Required, Range(0.000001d, 500_000d)]
    public double? Radius { get; init; }

    [Range(1, int.MaxValue)]
    public int? CategoryId { get; init; }

    [EnumDataType(typeof(UnitOfMeasure))]
    public UnitOfMeasure? Unit { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999", ParseLimitsInInvariantCulture = true)]
    public decimal? MinPrice { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999", ParseLimitsInInvariantCulture = true)]
    public decimal? MaxPrice { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 50;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice is { } minimum && MaxPrice is { } maximum && minimum > maximum)
        {
            yield return new ValidationResult("MinPrice cannot exceed MaxPrice.", [nameof(MinPrice), nameof(MaxPrice)]);
        }
    }
}
