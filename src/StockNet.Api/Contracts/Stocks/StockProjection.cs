using System.Linq.Expressions;
using System.Text.Json;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

internal sealed record StockProjection(
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
        stock.TotalQuantity,
        stock.MinOrderQuantity,
        stock.StepQuantity,
        stock.Unit,
        stock.PackagingType,
        stock.PricePerUnit,
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
                TotalQuantity,
                MinOrderQuantity,
                StepQuantity,
                Unit,
                PackagingType,
                PricePerUnit,
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
