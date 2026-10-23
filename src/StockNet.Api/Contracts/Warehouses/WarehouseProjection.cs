using System.Linq.Expressions;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Contracts.Warehouses;

internal sealed record WarehouseProjection(
    Guid Id,
    string Name,
    string AddressText,
    Point Location,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static Expression<Func<Warehouse, WarehouseProjection>> Projection => warehouse => new WarehouseProjection(
        warehouse.Id,
        warehouse.Name,
        warehouse.AddressText,
        warehouse.Location,
        warehouse.CreatedAt,
        warehouse.UpdatedAt);

    public WarehouseDto ToDto() => new(
        Id,
        Name,
        AddressText,
        Location.Y,
        Location.X,
        CreatedAt,
        UpdatedAt);
}
