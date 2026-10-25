# Database Schema Design & Domain Models

## 1. Core Entities (PostgreSQL + EF Core)

### Company
Represents both the platform account and the participant company. MVP identity is strictly one Company per user account; do not introduce a separate User entity or company-membership/RBAC hierarchy. Business classification (Producer, Buyer, Forwarder) is not an authorization role.
- `Id` (Guid, PK)
- `Name` (string)
- `Email` (string, unique, normalized for login)
- `NormalizedEmail` (string, unique, uppercase invariant email used for login lookup)
- `PasswordHash` (string, generated with ASP.NET Core `PasswordHasher`)
- `TaxId` (string, CUI/VAT)
- `CountryCode` (string, ISO)
- `CompanyType` (enum/string, business classification only; not an access-control role)
- `IsVerified` (bool)
- `IsAdmin` (bool, default: false) - grants access to admin-only operations
- `IsSubscribed` (bool) - subscription state used only to authorize stock creation/update
- `SubscriptionExpiresAt` (DateTime?, UTC)
- `CreatedAt` (DateTime, UTC)
- `StripeCustomerId` (string?, null for non-paying users)

The authenticated identity and authorization subject is `Company.Id` (JWT `CompanyId` claim). Never accept a request-body CompanyId as proof of identity.

### Category
Database-managed lookup table pre-seeded in migrations. It has an integer ID, unique `Name` and `Slug`, optional description, sort order, an `IsActive` flag, optional self-reference `ParentId`, and JSONB `AttributeRules` (key, data type, required flag, optional number bounds, and allowed string codes). Stock references one category; category deletion is restricted while stocks or children reference it. Keys and allowed values are strict lowercase technical codes; localized display labels belong to the frontend. Public `GET /api/categories` and `GET /api/categories/{id}` expose these rules for dynamic forms and filters.

### Warehouse
Relation: `Company (1) -> Warehouse (N)`
- `Id` (Guid, PK)
- `CompanyId` (Guid, FK)
- `Name` (string)
- `Location` (`NetTopologySuite.Geometries.Point`, PostGIS `geography(Point, 4326)`)
- Set SRID 4326 and use X = longitude, Y = latitude; see `postgis-rules.md`.
- `AddressText` (string)

### Stock
Relation: `Warehouse (1) -> Stock (N)`
- `Id` (Guid, PK)
- `WarehouseId` (Guid, FK)
- `CategoryId` (int/Guid)
- `Unit` (`Tons`, `Kilograms`, `Pieces`, `Units`, `CubicMeters`, `Liters`; persisted as the enum's string name)
- `Title` (string)
- `Description` (string)
- `TotalQuantity` (decimal, 3 fractional digits)
- `MinOrderQuantity` (decimal, 3 fractional digits)
- `StepQuantity` (decimal, 3 fractional digits)
- `Unit` (`Tons`, `Kilograms`, `Pieces`, `Units`, `CubicMeters`, `Liters`; discrete units require whole quantities)
- `PackagingType` (string / Enum)
- `PricePerUnit` (decimal, positive)
- `Currency` (string, e.g., "EUR", "RON")
- `Attributes` (`JsonDocument?`, PostgreSQL JSONB for dynamic properties)
- `AvailableFrom` (DateTime, UTC)
- `AvailableTo` (DateTime, UTC)
- `IsActive` (bool)

Search-active stock means `IsActive == true` and `AvailableFrom <= current UTC time <= AvailableTo`.

Only the owning company (resolved through `Stock -> Warehouse -> Company`) can modify a stock. Creating or updating stock content requires a current subscription; archiving or deleting stock does not. Hard deletion cascades through its message threads and messages; archiving preserves them. `IsSubscribed = true` with no expiry is perpetual access; otherwise the expiry must be after current UTC time. Do not apply subscription checks to search or messaging.

### MessageThread
Represents a direct pre-call communication channel between a Buyer and a Seller around a specific Stock.
- `Id` (Guid, PK)
- `StockId` (Guid, FK -> Stock)
- `BuyerCompanyId` (Guid, FK -> Company)
- `SellerCompanyId` (Guid, FK -> Company)
- `CreatedAt` (DateTime, UTC)

At most one thread may exist per `(StockId, BuyerCompanyId)`. Creating a thread requires the caller to be distinct from the stock owner and the stock to be currently active and available.

### Message
Represents an individual text message sent within a specific MessageThread.
- `Id` (Guid, PK)
- `ThreadId` (Guid, FK -> MessageThread)
- `SenderCompanyId` (Guid, FK -> Company)
- `Text` (string)
- `SentAt` (DateTime, UTC)

### ProcessedStripeEvent
Webhook idempotency ledger keyed by Stripe Event ID. Stores event type and UTC processing time; it intentionally has no Company FK because webhook events may be unrelated or refer to deleted accounts.

No Order, Cart, or goods-checkout entities are part of the MVP. A message thread is accessible only to its `BuyerCompanyId` and `SellerCompanyId`; the authenticated CompanyId must match one of those participants for reads and writes.
