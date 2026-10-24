using StockNet.Api.Domain.Entities;
using StockNet.Api.Contracts.Billing;

namespace StockNet.Api.Services.Billing;

public interface IStripeGateway
{
    Task<string> CreateCustomerAsync(Company company, CancellationToken cancellationToken);

    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        Company company,
        string customerId,
        CancellationToken cancellationToken);

    Task<StripeSubscriptionSnapshot?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}
