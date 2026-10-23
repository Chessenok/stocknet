using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Messaging;

public sealed class MessagePageQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 50;
}
