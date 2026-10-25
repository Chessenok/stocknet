using System.Text.Json;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Services;

public static class StockAttributeValidator
{
    public static IReadOnlyList<string> Validate(Category category, JsonElement? attributes)
    {
        var errors = new List<string>();
        var hasObject = attributes is { ValueKind: JsonValueKind.Object };

        foreach (var rule in category.AttributeRules)
        {
            if (!IsTechnicalCode(rule.Key))
            {
                errors.Add($"The configured attribute key '{rule.Key}' is not a valid technical code.");
                continue;
            }

            if (rule.AllowedValues.Any(allowedValue => !IsTechnicalCode(allowedValue)))
            {
                errors.Add($"The configured allowed values for '{rule.Key}' must be technical codes.");
                continue;
            }

            if (!hasObject || !attributes!.Value.TryGetProperty(rule.Key, out var value))
            {
                if (rule.IsRequired)
                {
                    errors.Add($"Attributes.{rule.Key} is required.");
                }

                continue;
            }

            switch (rule.Type)
            {
                case CategoryAttributeType.String:
                    ValidateString(rule, value, errors);
                    break;
                case CategoryAttributeType.Number:
                    ValidateNumber(rule, value, errors);
                    break;
                case CategoryAttributeType.Boolean:
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        errors.Add($"Attributes.{rule.Key} must be a boolean.");
                    }
                    break;
                default:
                    errors.Add($"The configured type for Attributes.{rule.Key} is unsupported.");
                    break;
            }
        }

        return errors;
    }

    public static bool IsTechnicalCode(string value) =>
        value.Length > 0
        && value == value.ToLowerInvariant()
        && value.Split('_').All(part => part.Length > 0 && part.All(char.IsAsciiLetterOrDigit));

    private static void ValidateString(CategoryAttributeRule rule, JsonElement value, ICollection<string> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"Attributes.{rule.Key} must be a string code.");
            return;
        }

        var code = value.GetString() ?? string.Empty;
        if (rule.AllowedValues.Length > 0 && !rule.AllowedValues.Contains(code, StringComparer.Ordinal))
        {
            errors.Add($"Attributes.{rule.Key} must be one of the allowed codes: {string.Join(", ", rule.AllowedValues)}.");
        }
    }

    private static void ValidateNumber(CategoryAttributeRule rule, JsonElement value, ICollection<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            errors.Add($"Attributes.{rule.Key} must be a number.");
            return;
        }

        if (rule.Min is { } min && number < min)
        {
            errors.Add($"Attributes.{rule.Key} must be at least {min}.");
        }

        if (rule.Max is { } max && number > max)
        {
            errors.Add($"Attributes.{rule.Key} must be at most {max}.");
        }
    }
}
