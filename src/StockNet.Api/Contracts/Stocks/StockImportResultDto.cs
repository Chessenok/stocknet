namespace StockNet.Api.Contracts.Stocks;

public sealed record StockImportResultDto(int ImportedCount, IReadOnlyList<Guid> StockIds);
