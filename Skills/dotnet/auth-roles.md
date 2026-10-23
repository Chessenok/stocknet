# Authentication & Access Control Guidelines (MVP)

## 1. Company-Based JWT Identity
- Use standard ASP.NET Core JWT Bearer authentication and `Authorization: Bearer <token>`.
- One Company is exactly one user account. Do not add a separate User entity, company membership, or per-company RBAC hierarchy.
- The authenticated Company ID is the identity (`CompanyId` claim and/or `ClaimTypes.NameIdentifier`). Derive it from the validated principal for every operation; never use a request-provided CompanyId for authorization.
- Admin access is controlled only by `Company.IsAdmin`. Enforce it with an admin policy/check; Producer/Buyer/Forwarder is business classification, not an authorization role.
- Do not rely on a stale JWT claim for current company ownership, admin status, or subscription state where a database check is required.

## 2. Subscription Guard for Stock Writes
Any attempt to create a stock or update its content must validate the authenticated company's subscription. Archiving/deleting stock is exempt. This is the only MVP feature gated by subscription:

```csharp
public async Task<bool> ValidateCompanyCanPostStockAsync(Guid companyId)
{
    var company = await _dbContext.Companies.FindAsync(companyId);
    if (company == null) return false;

    return company.IsSubscribed &&
           (company.SubscriptionExpiresAt == null || company.SubscriptionExpiresAt > DateTime.UtcNow);
}
```

Reject unauthorized stock create/update requests with `403 Forbidden`. Apply the guard to create and content updates, and verify the target stock belongs to the authenticated company before mutation. Archiving or deleting a stock does not require an active subscription. Do not gate stock search, messaging, buyers, forwarders, or frontend-only “Contact transport” on subscription.

`IsSubscribed == true` with `SubscriptionExpiresAt == null` grants perpetual access. Otherwise, a subscription is current only when `SubscriptionExpiresAt > DateTime.UtcNow`.

## 3. Endpoint Authentication Boundary
- Public/anonymous business access is limited to active stock map/search endpoints and their supported filters (for example `GET /api/stocks/map`).
- `/api/auth/register` and `/api/auth/login` are public anonymous identity-infrastructure endpoints; they do not expose business data.
- Require authentication for every other business endpoint, including company-specific data, stock writes, thread creation, and message reads/writes.
- Never expose a public company list or company dump. Expose only the authenticated caller's own company profile unless an operation is explicitly admin-only.
- Do not expose OpenAPI or health/diagnostic routes as anonymous public business endpoints; keep operational endpoints internal or protected as appropriate.

## 4. Ownership and Messaging Authorization
- Stock ownership is determined through `Stock.Warehouse.CompanyId`; only that company may update or delete the stock.
- For every message-thread or message operation, verify the authenticated CompanyId equals the thread's `BuyerCompanyId` or `SellerCompanyId`.
- Validate participation on reads as well as writes; thread/message IDs are not authorization credentials.
- For new threads, derive the buyer from the caller and the seller from the stock owner. Reject invalid/self-participant combinations.
- Return consistent `403 Forbidden` or non-disclosing `404 Not Found` responses for unauthorized resources; do not leak another company's data.
