using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Stocks;

public sealed class StockMapDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public decimal MinOrderQuantity { get; init; }
    public decimal StepQuantity { get; init; }
    public UnitOfMeasure Unit { get; init; }
    public string PackagingType { get; init; } = string.Empty;
    public decimal PricePerUnit { get; init; }
    public string Currency { get; init; } = string.Empty;
    public JsonElement? Attributes { get; init; }
    public DateTime AvailableFrom { get; init; }
    public DateTime AvailableTo { get; init; }
    public double DistanceMeters { get; init; }
    public double Latitude => WarehousePoint.Y;
    public double Longitude => WarehousePoint.X;

    [JsonIgnore]
    public Point WarehousePoint { get; init; } = default!;

}
