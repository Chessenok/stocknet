using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

public abstract class StockContentRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(5000)]
    public string Description { get; init; } = string.Empty;

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? TotalQuantity { get; init; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? MinOrderQuantity { get; init; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? StepQuantity { get; init; }

    public UnitOfMeasure Unit { get; init; } = UnitOfMeasure.Tons;

    [Required, StringLength(64)]
    public string PackagingType { get; init; } = string.Empty;

    [Required, Range(typeof(decimal), "0.0001", "99999999999999.9999", ParseLimitsInInvariantCulture = true)]
    public decimal? PricePerUnit { get; init; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a three-letter currency code.")]
    public string Currency { get; init; } = string.Empty;

    public JsonElement? Attributes { get; init; }

    [Required]
    public DateTimeOffset? AvailableFrom { get; init; }

    [Required]
    public DateTimeOffset? AvailableTo { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Unit))
        {
            yield return new ValidationResult("Unit must be a supported unit of measure.", [nameof(Unit)]);
        }

        if (TotalQuantity is { } total && decimal.Round(total, 3) != total)
        {
            yield return new ValidationResult("TotalQuantity supports at most three decimal places.", [nameof(TotalQuantity)]);
        }

        if (MinOrderQuantity is { } minimum && decimal.Round(minimum, 3) != minimum)
        {
            yield return new ValidationResult("MinOrderQuantity supports at most three decimal places.", [nameof(MinOrderQuantity)]);
        }

        if (StepQuantity is { } step && decimal.Round(step, 3) != step)
        {
            yield return new ValidationResult("StepQuantity supports at most three decimal places.", [nameof(StepQuantity)]);
        }

        if (PricePerUnit is { } price && decimal.Round(price, 4) != price)
        {
            yield return new ValidationResult("PricePerUnit supports at most four decimal places.", [nameof(PricePerUnit)]);
        }

        if (MinOrderQuantity is { } minOrder && TotalQuantity is { } totalQuantity && minOrder > totalQuantity)
        {
            yield return new ValidationResult("MinOrderQuantity cannot exceed TotalQuantity.", [nameof(MinOrderQuantity)]);
        }

        if (Unit is UnitOfMeasure.Pieces or UnitOfMeasure.Units)
        {
            if (TotalQuantity is { } discreteTotal && decimal.Truncate(discreteTotal) != discreteTotal)
            {
                yield return new ValidationResult("TotalQuantity must be a whole number for Pieces and Units.", [nameof(TotalQuantity)]);
            }

            if (MinOrderQuantity is { } discreteMinimum && decimal.Truncate(discreteMinimum) != discreteMinimum)
            {
                yield return new ValidationResult("MinOrderQuantity must be a whole number for Pieces and Units.", [nameof(MinOrderQuantity)]);
            }

            if (StepQuantity is { } discreteStep && (discreteStep < 1 || decimal.Truncate(discreteStep) != discreteStep))
            {
                yield return new ValidationResult("StepQuantity must be a whole number of at least 1 for Pieces and Units.", [nameof(StepQuantity)]);
            }
        }

        if (AvailableFrom is { } from && AvailableTo is { } to && to < from)
        {
            yield return new ValidationResult("AvailableTo must be greater than or equal to AvailableFrom.", [nameof(AvailableTo)]);
        }

        if (Attributes is { } attributes)
        {
            if (attributes.ValueKind != JsonValueKind.Object)
            {
                yield return new ValidationResult("Attributes must be a JSON object.", [nameof(Attributes)]);
            }
            else if (attributes.GetRawText().Length > 16_000)
            {
                yield return new ValidationResult("Attributes cannot exceed 16 KB.", [nameof(Attributes)]);
            }
        }
    }
}
