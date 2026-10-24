using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Companies;

public sealed class ManualSubscriptionOverrideRequest
{
    [Required]
    public bool? IsSubscribed { get; init; }

    public DateTimeOffset? SubscriptionExpiresAt { get; init; }
}
