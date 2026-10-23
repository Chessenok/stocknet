using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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
    public decimal? TotalQuantityTons { get; init; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? MinOrderQuantityTons { get; init; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? StepQuantityTons { get; init; }

    [Required, StringLength(64)]
    public string PackagingType { get; init; } = string.Empty;

    [Required, Range(typeof(decimal), "0", "99999999999999.9999", ParseLimitsInInvariantCulture = true)]
    public decimal? PricePerTon { get; init; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a three-letter currency code.")]
    public string Currency { get; init; } = string.Empty;

    public JsonElement? Attributes { get; init; }

    [Required]
    public DateTimeOffset? AvailableFrom { get; init; }

    [Required]
    public DateTimeOffset? AvailableTo { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TotalQuantityTons is { } total && decimal.Round(total, 3) != total)
        {
            yield return new ValidationResult("TotalQuantityTons supports at most three decimal places.", [nameof(TotalQuantityTons)]);
        }

        if (MinOrderQuantityTons is { } minimum && decimal.Round(minimum, 3) != minimum)
        {
            yield return new ValidationResult("MinOrderQuantityTons supports at most three decimal places.", [nameof(MinOrderQuantityTons)]);
        }

        if (StepQuantityTons is { } step && decimal.Round(step, 3) != step)
        {
            yield return new ValidationResult("StepQuantityTons supports at most three decimal places.", [nameof(StepQuantityTons)]);
        }

        if (PricePerTon is { } price && decimal.Round(price, 4) != price)
        {
            yield return new ValidationResult("PricePerTon supports at most four decimal places.", [nameof(PricePerTon)]);
        }

        if (MinOrderQuantityTons is { } minOrder && TotalQuantityTons is { } totalQuantity && minOrder > totalQuantity)
        {
            yield return new ValidationResult("MinOrderQuantityTons cannot exceed TotalQuantityTons.", [nameof(MinOrderQuantityTons)]);
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
