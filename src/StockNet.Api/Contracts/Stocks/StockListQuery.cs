using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Stocks;

public sealed class StockListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 50;
}
