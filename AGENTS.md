# 🤖 Backend Context & Agent Routing Guide

## 📌 Project Overview
**StockNet** (Backend API) — B2B platform for raw-material discovery and direct pre-call messaging between companies.
The backend provides a REST API built with **.NET 10** and **PostgreSQL + PostGIS**, with company-based JWT authentication, spatial stock discovery, and Stripe subscriptions for stock posting.

---

## 🛠 Tech Stack
* **Framework:** .NET 10 (ASP.NET Core Web API).
* **Database & ORM:** PostgreSQL + PostGIS, Entity Framework Core 10 + `NetTopologySuite`.
* **Authentication:** JWT (JSON Web Tokens), ASP.NET Core `PasswordHasher`.
* **Logging:** Serilog (Structured JSON Logging).
* **Integrations:** `Stripe.net` (subscriptions), `MiniExcel` / `ClosedXML` (Excel import).

---

## 🗺 Skills Routing & Module Guide (Where to look)

When working on specific parts of the project, **read and follow the instructions in the corresponding skill files** located in `/Skills`:

| Module / Task | Target Skill File | Description & Rules |
| :--- | :--- | :--- |
| **Database Schema & Models** | `Skills/db/schema-design.md` | Company-as-user identity and Company, Warehouse, Stock, and messaging models. |
| **Spatial / Map Queries** | `Skills/db/postgis-rules.md` | PostGIS/NTS, meter-based distance, and safe map/filter query rules. |
| **Local Environment & Seeding** | `Skills/db/seeding-docker.md` | Docker Compose setup for PostgreSQL/PostGIS, migrations, and safe development seeding. |
| **API Architecture & Errors** | `Skills/dotnet/api-standards.md` | Controller standards, public endpoint boundary, DTO projections, and error handling. |
| **Auth & Access Control** | `Skills/dotnet/auth-roles.md` | CompanyId JWT identity, IsAdmin policy, ownership checks, and stock subscription guard. |
| **Billing & Webhooks** | `Skills/dotnet/stripe-billing.md` | Stripe checkout/webhooks; subscription gates stock writes only. |
| **Logging & Monitoring** | `Skills/dotnet/logging.md` | Serilog configuration, structured logging, and sensitive-data rules. |

---

## 🛡 MVP Business & Security Rules (Authoritative)
1. **Company is the account:** Exactly one Company represents one user account. There is no separate User entity or company-member/role hierarchy. The authenticated identity is the Company ID (`CompanyId` claim). `Company.IsAdmin` is the sole admin flag. Company type/classification (Producer, Buyer, Forwarder) is business data, not an authorization role.
2. **Subscription scope:** A valid company subscription is required only to create stock or update its content. Reject those writes with `403 Forbidden` when the company is unsubscribed or its subscription has expired. `IsSubscribed = true` with `SubscriptionExpiresAt = null` is perpetual access; otherwise expiration must be greater than current UTC time. Archiving/deleting stock does not require a subscription. Buyers and forwarders do not need backend subscriptions for MVP; “Contact transport” is frontend-handled. Do not gate map search, messaging, or unrelated company operations on subscription.
3. **Public API boundary:** The only unauthenticated business endpoint is active-stock map/search, such as `GET /api/stocks/map` with supported filters. Registration and login (`POST /api/auth/register`, `POST /api/auth/login`) are public identity-infrastructure endpoints. Do not expose public company lists or public company records. All other business endpoints require authentication, including stock writes, company-specific data, and messaging.
4. **Ownership and messaging:** A company can modify only stocks owned through its warehouses and access only message threads in which it is the buyer or seller participant. Enforce these checks on every read and write; never trust a caller-supplied CompanyId as identity.
5. **Spatial discovery:** The frontend supplies a center latitude/longitude in degrees and a radius in meters, along with filters. Use PostGIS and NetTopologySuite to query warehouse points by meter distance, return only matching stocks where `IsActive` is true and current UTC time is between `AvailableFrom` and `AvailableTo`, and project results to DTOs with latitude/longitude. The user-selected center may be any location and is not required to be a warehouse.
6. **MVP scope:** No orders or goods checkout workflows. MVP discovery and direct pre-call messaging (`MessageThread`/`Message`) are in scope.

## 🛡 Core Domain Models Summary
1. **Company:** Central identity/account (`Producer`, `Buyer`, `Forwarder` business classification). Contains credentials/contact identity, Tax ID (CUI), verification status, `IsAdmin`, and subscription fields (`IsSubscribed`, `SubscriptionExpiresAt`).
2. **Warehouse:** Relation `Company 1 : N Warehouse`. Holds PostGIS `Point` coordinates.
3. **Stock:** Relation `Warehouse 1 : N Stock`. Holds unit-agnostic quantities (`TotalQuantity`, `MinOrderQuantity`, `StepQuantity`) and `UnitOfMeasure`, `PricePerUnit`, category-validated JSONB `Attributes`, and temporal availability (`AvailableFrom`, `AvailableTo`).
4. **MessageThread & Message:** Direct communication channel linked to a specific stock and restricted to its buyer and seller companies.

---

## 📐 AI Code Generation Rules
1. **No Heavy Queues:** Do NOT introduce Kafka, RabbitMQ, or microservices. Maintain a clean modular monolith.
2. **DTO Separation:** Never expose database Entities (`DbSet`) directly in API responses. Always project to DTOs via LINQ (`.Select()`).
3. **Asynchronous I/O:** All I/O operations in .NET must be asynchronous (`async` / `await`, `ToListAsync()`, etc.).
4. **PostGIS Safety:** All spatial queries must use EF Core + `NetTopologySuite` or parameterized SQL. Never interpolate SQL. Distances and radii are in meters.
