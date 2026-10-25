using System.Text.Json;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Services;

public static class StockAttributeValidator
{
    public static IReadOnlyList<string> GetMissingRequiredKeys(Category category, JsonElement? attributes)
    {
        if (category.RequiredAttributeKeys.Length == 0)
        {
            return [];
        }

        if (attributes is not { ValueKind: JsonValueKind.Object } attributeObject)
        {
            return category.RequiredAttributeKeys;
        }

        return category.RequiredAttributeKeys
            .Where(requiredKey => !attributeObject.EnumerateObject().Any(property =>
                string.Equals(property.Name, requiredKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
