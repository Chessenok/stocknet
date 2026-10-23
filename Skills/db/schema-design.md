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
Database-managed lookup table pre-seeded in the initial migration. It has an integer ID, unique `Name` and `Slug`, optional description, sort order, and an `IsActive` flag. Stock references one category; category deletion is restricted while stocks reference it.

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
- `Title` (string)
- `Description` (string)
- `TotalQuantityTons` (decimal, 3 fractional digits)
- `MinOrderQuantityTons` (decimal, 3 fractional digits)
- `StepQuantityTons` (decimal, 3 fractional digits)
- `PackagingType` (string / Enum)
- `PricePerTon` (decimal)
- `Currency` (string, e.g., "EUR", "RON")
- `Attributes` (`JsonDocument?`, PostgreSQL JSONB for dynamic properties)
- `AvailableFrom` (DateTime, UTC)
- `AvailableTo` (DateTime, UTC)
- `IsActive` (bool)

Search-active stock means `IsActive == true` and `AvailableFrom <= current UTC time <= AvailableTo`.

Only the owning company (resolved through `Stock -> Warehouse -> Company`) can modify a stock. Creating or updating stock content requires a current subscription; archiving or deleting stock does not. `IsSubscribed = true` with no expiry is perpetual access; otherwise the expiry must be after current UTC time. Do not apply subscription checks to search or messaging.

### MessageThread
Represents a direct pre-call communication channel between a Buyer and a Seller around a specific Stock.
- `Id` (Guid, PK)
- `StockId` (Guid, FK -> Stock)
- `BuyerCompanyId` (Guid, FK -> Company)
- `SellerCompanyId` (Guid, FK -> Company)
- `CreatedAt` (DateTime, UTC)

### Message
Represents an individual text message sent within a specific MessageThread.
- `Id` (Guid, PK)
- `ThreadId` (Guid, FK -> MessageThread)
- `SenderCompanyId` (Guid, FK -> Company)
- `Text` (string)
- `SentAt` (DateTime, UTC)

No Order, Cart, or goods-checkout entities are part of the MVP. A message thread is accessible only to its `BuyerCompanyId` and `SellerCompanyId`; the authenticated CompanyId must match one of those participants for reads and writes.
