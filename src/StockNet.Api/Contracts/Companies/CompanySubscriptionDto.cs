namespace StockNet.Api.Contracts.Companies;

public sealed record CompanySubscriptionDto(
    Guid CompanyId,
    bool IsSubscribed,
    DateTime? SubscriptionExpiresAt);
