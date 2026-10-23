namespace StockNet.Api.Contracts.Warehouses;

public sealed record WarehouseDto(
    Guid Id,
    string Name,
    string AddressText,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    DateTime UpdatedAt);
