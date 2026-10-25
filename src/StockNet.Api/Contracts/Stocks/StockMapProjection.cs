using System.Linq.Expressions;
using System.Text.Json;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

internal sealed record StockMapProjection(
    Guid Id,
    string Title,
    string Description,
    int CategoryId,
    string Category,
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
    double DistanceMeters,
    Point WarehousePoint)
{
    public static Expression<Func<Stock, StockMapProjection>> Projection(Point center) => stock => new StockMapProjection(
        stock.Id,
        stock.Title,
        stock.Description,
        stock.CategoryId,
        stock.Category.Name,
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
        stock.Warehouse.Location.Distance(center),
        stock.Warehouse.Location);

    public StockMapDto ToDto()
    {
        try
        {
            return new StockMapDto
            {
                Id = Id,
                Title = Title,
                Description = Description,
                CategoryId = CategoryId,
                Category = Category,
                TotalQuantity = TotalQuantity,
                MinOrderQuantity = MinOrderQuantity,
                StepQuantity = StepQuantity,
                Unit = Unit,
                PackagingType = PackagingType,
                PricePerUnit = PricePerUnit,
                Currency = Currency,
                Attributes = Attributes?.RootElement.Clone(),
                AvailableFrom = AvailableFrom,
                AvailableTo = AvailableTo,
                DistanceMeters = DistanceMeters,
                WarehousePoint = WarehousePoint
            };
        }
        finally
        {
            Attributes?.Dispose();
        }
    }
}
