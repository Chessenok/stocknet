using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Stocks;

public sealed class CreateStockRequest : StockContentRequest
{
    [Required]
    public Guid? WarehouseId { get; init; }
}
