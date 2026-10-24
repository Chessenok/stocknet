using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Stripe;
using Stripe.Checkout;
using StockNet.Api.Contracts.Billing;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Options;

namespace StockNet.Api.Services.Billing;

public sealed class StripeBillingService(
    StockNetDbContext dbContext,
    IStripeGateway stripeGateway,
    IOptions<StripeOptions> stripeOptions) : IStripeBillingService
{
    public async Task<CheckoutSessionDto?> CreateCheckoutSessionAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        stripeOptions.Value.ValidateCheckoutConfiguration();
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);

        if (company is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(company.StripeCustomerId))
        {
            company.StripeCustomerId = await stripeGateway.CreateCustomerAsync(company, cancellationToken);
            company.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await stripeGateway.CreateCheckoutSessionAsync(
            company,
            company.StripeCustomerId,
            cancellationToken);
    }

    public async Task<bool> ProcessWebhookEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stripeEvent.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(stripeEvent.Type);

        if (await dbContext.ProcessedStripeEvents
            .AsNoTracking()
            .AnyAsync(item => item.EventId == stripeEvent.Id, cancellationToken))
        {
            return false;
        }

        var subscriptionUpdate = await ResolveSubscriptionUpdateAsync(stripeEvent, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.ProcessedStripeEvents
            .AsNoTracking()
            .AnyAsync(item => item.EventId == stripeEvent.Id, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.ProcessedStripeEvents.Add(new ProcessedStripeEvent
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ProcessedAtUtc = DateTime.UtcNow
        });

        if (subscriptionUpdate is not null)
        {
            var company = await FindCompanyAsync(subscriptionUpdate, cancellationToken);
            if (company is not null && CanApplyUpdate(company, subscriptionUpdate))
            {
                ApplySubscriptionUpdate(company, subscriptionUpdate);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEvent(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        return true;
    }

    private async Task<SubscriptionUpdate?> ResolveSubscriptionUpdateAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                {
                    if (stripeEvent.Data.Object is not Session session
                        || session.Mode != "subscription"
                        || string.IsNullOrWhiteSpace(session.SubscriptionId))
                    {
                        return null;
                    }

                    var subscription = await stripeGateway.GetSubscriptionAsync(session.SubscriptionId, cancellationToken);
                    if (subscription is null)
                    {
                        return null;
                    }

                    Guid? companyId = Guid.TryParse(session.ClientReferenceId, out var parsedCompanyId)
                        ? parsedCompanyId
                        : null;
                    return FromSnapshot(subscription, companyId, session.CustomerId, allowSubscriptionReplacement: true);
                }

            case "invoice.payment_succeeded":
                {
                    if (stripeEvent.Data.Object is not Invoice invoice)
                    {
                        return null;
                    }

                    var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
                    if (string.IsNullOrWhiteSpace(subscriptionId))
                    {
                        return null;
                    }

                    var subscription = await stripeGateway.GetSubscriptionAsync(subscriptionId, cancellationToken);
                    return subscription is null
                        ? null
                        : FromSnapshot(
                            subscription,
                            companyId: null,
                            customerId: invoice.CustomerId,
                            allowSubscriptionReplacement: false);
                }

            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                {
                    if (stripeEvent.Data.Object is not Subscription eventSubscription)
                    {
                        return null;
                    }

                    // Stripe does not guarantee delivery order; query the current subscription state.
                    var subscription = await stripeGateway.GetSubscriptionAsync(eventSubscription.Id, cancellationToken)
                        ?? StripeSubscriptionSnapshot.FromSubscription(eventSubscription);

                    return FromSnapshot(
                        subscription,
                        companyId: null,
                        customerId: eventSubscription.CustomerId,
                        allowSubscriptionReplacement: false);
                }

            default:
                return null;
        }
    }

    private static SubscriptionUpdate FromSnapshot(
        StripeSubscriptionSnapshot subscription,
        Guid? companyId,
        string? customerId,
        bool allowSubscriptionReplacement) => new(
            companyId,
            customerId ?? subscription.CustomerId,
            subscription.Id,
            subscription.Status,
            subscription.CurrentPeriodEndUtc,
            allowSubscriptionReplacement);

    private async Task<Company?> FindCompanyAsync(
        SubscriptionUpdate update,
        CancellationToken cancellationToken)
    {
        if (update.CompanyId is { } companyId)
        {
            var company = await dbContext.Companies
                .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
            if (company is not null)
            {
                return company;
            }
        }

        if (!string.IsNullOrWhiteSpace(update.CustomerId))
        {
            var company = await dbContext.Companies
                .SingleOrDefaultAsync(item => item.StripeCustomerId == update.CustomerId, cancellationToken);
            if (company is not null)
            {
                return company;
            }
        }

        return string.IsNullOrWhiteSpace(update.SubscriptionId)
            ? null
            : await dbContext.Companies
                .SingleOrDefaultAsync(item => item.StripeSubscriptionId == update.SubscriptionId, cancellationToken);
    }

    private static bool CanApplyUpdate(Company company, SubscriptionUpdate update)
    {
        if (!string.IsNullOrWhiteSpace(update.CustomerId)
            && !string.IsNullOrWhiteSpace(company.StripeCustomerId)
            && company.StripeCustomerId != update.CustomerId)
        {
            return false;
        }

        return update.AllowSubscriptionReplacement
            || string.IsNullOrWhiteSpace(company.StripeSubscriptionId)
            || company.StripeSubscriptionId == update.SubscriptionId;
    }

    private static void ApplySubscriptionUpdate(Company company, SubscriptionUpdate update)
    {
        company.StripeCustomerId ??= update.CustomerId;
        company.StripeSubscriptionId = update.SubscriptionId ?? company.StripeSubscriptionId;
        var terminalStatus = update.Status is "canceled" or "incomplete_expired";
        company.SubscriptionExpiresAt = terminalStatus ? null : update.CurrentPeriodEndUtc;

        var subscriptionIsCurrent = (update.Status is "active" or "trialing")
            && update.CurrentPeriodEndUtc is { } expiry
            && expiry > DateTime.UtcNow;
        company.IsSubscribed = subscriptionIsCurrent;
        company.UpdatedAt = DateTime.UtcNow;
    }

    private static bool IsDuplicateEvent(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && postgresException.ConstraintName == "PK_ProcessedStripeEvents";

    private sealed record SubscriptionUpdate(
        Guid? CompanyId,
        string? CustomerId,
        string? SubscriptionId,
        string Status,
        DateTime? CurrentPeriodEndUtc,
        bool AllowSubscriptionReplacement);
}
