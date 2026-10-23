using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Companies;

public sealed record CompanyProfileDto(
    Guid Id,
    string Name,
    string Email,
    string? TaxId,
    string CountryCode,
    CompanyType CompanyType,
    bool IsVerified,
    bool IsAdmin,
    bool IsSubscribed,
    DateTime? SubscriptionExpiresAt,
    DateTime CreatedAt);
