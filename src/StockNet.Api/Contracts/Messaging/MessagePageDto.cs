namespace StockNet.Api.Contracts.Messaging;

public sealed record MessagePageDto(
    int Page,
    int PageSize,
    long TotalCount,
    IReadOnlyList<MessageDto> Items);
