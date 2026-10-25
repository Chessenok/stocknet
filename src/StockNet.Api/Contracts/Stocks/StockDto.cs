using System.Text.Json;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

public sealed record StockDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    int CategoryId,
    string Title,
    string Description,
    decimal TotalQuantity,
    decimal MinOrderQuantity,
    decimal StepQuantity,
    UnitOfMeasure Unit,
    string PackagingType,
    decimal PricePerUnit,
    string Currency,
    JsonElement? Attributes,
    DateTime AvailableFrom,
    DateTime AvailableTo,
    bool IsActive,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    DateTime UpdatedAt);
