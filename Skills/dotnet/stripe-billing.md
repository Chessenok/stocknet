# Stripe Billing & Subscriptions Guide

## 1. MVP Subscription Scope
An active subscription is required only when a company creates stock or updates stock content. Archiving/deleting stock does not require a subscription. `IsSubscribed = true` with `SubscriptionExpiresAt = null` means perpetual access; otherwise expiry must be after current UTC time. A subscription is not required for map/search, messaging, buyers, forwarders, or the frontend-handled “Contact transport” feature. The MVP has no goods orders or goods checkout workflows.

Company is the account and Stripe subscription owner. Resolve the company from the authenticated CompanyId claim; never accept an arbitrary company ID from the client to attach a subscription.

## 2. Creating a Checkout Session
- Require authentication and derive CompanyId from the validated principal.
- Use server-configured Stripe Price IDs and success/cancel URLs. Never accept price, amount, subscription status, or Stripe customer ID as trusted client input.
- Reuse the company's persisted StripeCustomerId when applicable, and include CompanyId as server-set metadata/client reference.
- Use asynchronous Stripe SDK methods.
- Return only the checkout URL/session information needed by the frontend.

```csharp
var companyId = User.GetCompanyId();
var company = await dbContext.Companies.FindAsync(companyId);
if (company is null) return Results.NotFound();

var options = new SessionCreateOptions
{
    Mode = "subscription",
    LineItems = [new SessionLineItemOptions { Price = configuredPriceId, Quantity = 1 }],
    SuccessUrl = configuredSuccessUrl,
    CancelUrl = configuredCancelUrl,
    ClientReferenceId = companyId.ToString()
};

var session = await new SessionService().CreateAsync(options);
```

## 3. Webhooks and Subscription State
- Validate webhook signatures against the raw request body using a server-side webhook secret.
- Read the raw body asynchronously, then use Stripe.net 53's synchronous `EventUtility.ConstructEvent` signature verifier (this pinned SDK version has no `ConstructEventAsync` method).
- Persist processed event IDs in `ProcessedStripeEvent` in the same transaction as any Company subscription update. Duplicate deliveries must return success without applying the update twice.
- Update subscription fields on Company only from verified Stripe events; a browser redirect is not proof of payment.
- Persist the active Stripe subscription ID on Company and ignore lifecycle events for a superseded subscription.
- Handle `checkout.session.completed`, `invoice.payment_succeeded`, `customer.subscription.updated`, and `customer.subscription.deleted`. Resolve the authoritative subscription state for checkout and invoice events.
- Treat Stripe `active`/`trialing` as entitled only when the subscription period end is in the future; store the period end in UTC.
- Apply webhook idempotency and subscription state changes transactionally.
- Treat `active`/`trialing` subscriptions as entitled only when the current period end is in the future; convert Stripe timestamps to UTC.
- The stock write authorization check must read authoritative current company subscription state and reject unsubscribed/expired companies with `403 Forbidden`.
- Do not place subscription gates on unrelated features or infer entitlement from stale JWT claims.
- Never log Stripe secrets, webhook signing secrets, authorization headers, or sensitive payment data.
