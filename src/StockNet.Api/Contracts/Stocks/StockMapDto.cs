using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Contracts.Stocks;

public sealed class StockMapDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal TotalQuantityTons { get; init; }
    public decimal MinOrderQuantityTons { get; init; }
    public decimal StepQuantityTons { get; init; }
    public string PackagingType { get; init; } = string.Empty;
    public decimal PricePerTon { get; init; }
    public string Currency { get; init; } = string.Empty;
    public JsonElement? Attributes { get; init; }
    public DateTime AvailableFrom { get; init; }
    public DateTime AvailableTo { get; init; }
    public double DistanceMeters { get; init; }
    public double Latitude => WarehousePoint.Y;
    public double Longitude => WarehousePoint.X;

    [JsonIgnore]
    public Point WarehousePoint { get; init; } = default!;

    public static Expression<Func<Stock, StockMapDto>> Projection(Point center) => stock => new StockMapDto
    {
        Id = stock.Id,
        Title = stock.Title,
        Description = stock.Description,
        CategoryId = stock.CategoryId,
        Category = stock.Category.Name,
        TotalQuantityTons = stock.TotalQuantityTons,
        MinOrderQuantityTons = stock.MinOrderQuantityTons,
        StepQuantityTons = stock.StepQuantityTons,
        PackagingType = stock.PackagingType,
        PricePerTon = stock.PricePerTon,
        Currency = stock.Currency,
        Attributes = ToJsonElement(stock.Attributes),
        AvailableFrom = stock.AvailableFrom,
        AvailableTo = stock.AvailableTo,
        DistanceMeters = stock.Warehouse.Location.Distance(center),
        WarehousePoint = stock.Warehouse.Location
    };

    private static JsonElement? ToJsonElement(JsonDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        try
        {
            return document.RootElement.Clone();
        }
        finally
        {
            document.Dispose();
        }
    }
}
