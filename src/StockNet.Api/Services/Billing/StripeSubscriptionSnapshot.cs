using Stripe;

namespace StockNet.Api.Services.Billing;

public sealed record StripeSubscriptionSnapshot(
    string Id,
    string CustomerId,
    string Status,
    DateTime? CurrentPeriodEndUtc)
{
    public static StripeSubscriptionSnapshot FromSubscription(Subscription subscription)
    {
        var items = subscription.Items?.Data ?? new List<SubscriptionItem>();
        var periodEnd = items
            .Select(item => ToUtc(item.CurrentPeriodEnd))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        return new StripeSubscriptionSnapshot(
            subscription.Id,
            subscription.CustomerId,
            subscription.Status,
            periodEnd == default ? null : periodEnd);
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
