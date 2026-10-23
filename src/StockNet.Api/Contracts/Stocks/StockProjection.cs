using System.Linq.Expressions;
using System.Text.Json;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Contracts.Stocks;

internal sealed record StockProjection(
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
    JsonDocument? Attributes,
    DateTime AvailableFrom,
    DateTime AvailableTo,
    bool IsActive,
    Point Location,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static Expression<Func<Stock, StockProjection>> Projection => stock => new StockProjection(
        stock.Id,
        stock.WarehouseId,
        stock.Warehouse.Name,
        stock.CategoryId,
        stock.Title,
        stock.Description,
        stock.TotalQuantityTons,
        stock.MinOrderQuantityTons,
        stock.StepQuantityTons,
        stock.PackagingType,
        stock.PricePerTon,
        stock.Currency,
        stock.Attributes,
        stock.AvailableFrom,
        stock.AvailableTo,
        stock.IsActive,
        stock.Warehouse.Location,
        stock.CreatedAt,
        stock.UpdatedAt);

    public StockDto ToDto()
    {
        try
        {
            return new StockDto(
                Id,
                WarehouseId,
                WarehouseName,
                CategoryId,
                Title,
                Description,
                TotalQuantityTons,
                MinOrderQuantityTons,
                StepQuantityTons,
                PackagingType,
                PricePerTon,
                Currency,
                Attributes is null ? null : Attributes.RootElement.Clone(),
                AvailableFrom,
                AvailableTo,
                IsActive,
                Location.Y,
                Location.X,
                CreatedAt,
                UpdatedAt);
        }
        finally
        {
            Attributes?.Dispose();
        }
    }
}
