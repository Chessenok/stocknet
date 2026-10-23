using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Warehouses;

public sealed class WarehouseUpsertRequest
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(500)]
    public string AddressText { get; init; } = string.Empty;

    [Required, Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; init; }
}
