using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace StockNet.Api.Contracts.Stocks;

public sealed class ImportStocksRequest
{
    [Required]
    public Guid? WarehouseId { get; init; }

    [Required]
    public IFormFile? File { get; init; }
}
