using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace StockNet.Api.Contracts.Stocks;

public sealed class StockImportRow : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(5000)]
    public string Description { get; set; } = string.Empty;

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? TotalQuantityTons { get; set; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? MinOrderQuantityTons { get; set; }

    [Required, Range(typeof(decimal), "0.001", "999999999999999.999", ParseLimitsInInvariantCulture = true)]
    public decimal? StepQuantityTons { get; set; }

    [Required, StringLength(64)]
    public string PackagingType { get; set; } = string.Empty;

    [Required, Range(typeof(decimal), "0", "99999999999999.9999", ParseLimitsInInvariantCulture = true)]
    public decimal? PricePerTon { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string Currency { get; set; } = string.Empty;

    [StringLength(16_000)]
    public string? AttributesJson { get; set; }

    [Required]
    public DateTime? AvailableFrom { get; set; }

    [Required]
    public DateTime? AvailableTo { get; set; }

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

        if (!string.IsNullOrWhiteSpace(AttributesJson))
        {
            var validJson = true;
            var isJsonObject = false;
            try
            {
                using var attributes = JsonDocument.Parse(AttributesJson);
                isJsonObject = attributes.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                validJson = false;
            }

            if (!validJson)
            {
                yield return new ValidationResult("AttributesJson must contain valid JSON.", [nameof(AttributesJson)]);
            }
            else if (!isJsonObject)
            {
                yield return new ValidationResult("AttributesJson must contain a JSON object.", [nameof(AttributesJson)]);
            }
        }
    }
}
