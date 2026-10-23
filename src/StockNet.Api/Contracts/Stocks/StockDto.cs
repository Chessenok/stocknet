using System.Text.Json;

namespace StockNet.Api.Contracts.Stocks;

public sealed record StockDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    int CategoryId,
    string Title,
    string Description,
    decimal TotalQuantityTons,
    decimal MinOrderQuantityTons,
    decimal StepQuantityTons,
    string PackagingType,
    decimal PricePerTon,
    string Currency,
    JsonElement? Attributes,
    DateTime AvailableFrom,
    DateTime AvailableTo,
    bool IsActive,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    DateTime UpdatedAt);
