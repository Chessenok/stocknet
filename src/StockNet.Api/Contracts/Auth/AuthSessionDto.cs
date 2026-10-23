namespace StockNet.Api.Contracts.Auth;

public sealed record AuthSessionDto(
    Guid CompanyId,
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    bool IsAdmin);
