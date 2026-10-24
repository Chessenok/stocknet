using Stripe;
using StockNet.Api.Contracts.Billing;

namespace StockNet.Api.Services.Billing;

public interface IStripeBillingService
{
    Task<CheckoutSessionDto?> CreateCheckoutSessionAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> ProcessWebhookEventAsync(Event stripeEvent, CancellationToken cancellationToken);
}
