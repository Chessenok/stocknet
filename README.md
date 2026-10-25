# StockNet API

StockNet is a .NET 10 API using PostgreSQL/PostGIS and Entity Framework Core.

## Local database

Requirements: .NET 10 SDK and Docker Compose.

Restore the repository-pinned EF Core tool:

```sh
dotnet tool restore
```

Start the local PostgreSQL/PostGIS service:

```sh
docker compose up -d db
```

Configure the API connection string in user-secrets (the Compose credentials are for local development only):

```sh
dotnet user-secrets set "ConnectionStrings:StockNet" "Host=localhost;Port=5432;Database=stocknet;Username=stocknet;Password=stocknet" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Stripe:ApiKey" "sk_test_your_key" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Stripe:PriceId" "price_your_subscription_price" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_your_webhook_secret" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Seed:AdminPassword" "$(openssl rand -base64 32)" --project src/StockNet.Api/StockNet.Api.csproj
dotnet user-secrets set "Seed:SupplierPassword" "$(openssl rand -base64 32)" --project src/StockNet.Api/StockNet.Api.csproj
```

Development startup idempotently seeds categories and the `admin@stocknet.local` / `supplier@stocknet.local` accounts. The admin and supplier seed passwords are required in user-secrets and are never logged.

`Jwt:Issuer` and `Jwt:Audience` have non-secret defaults in `appsettings.json`; override them per environment when needed. The signing key must contain at least 32 UTF-8 bytes.

Stripe success/cancel URLs have local development defaults in `appsettings.json` and can be overridden with `Stripe:SuccessUrl` and `Stripe:CancelUrl` user-secrets. To test signed webhooks locally, run `stripe listen --forward-to http://localhost:5172/api/billing/webhooks`, then use the `whsec_...` value printed by Stripe CLI as `Stripe:WebhookSecret`.

Configure browser CORS per environment with exact origins, for example `Cors__AllowedOrigins__0=https://app.example.com`. The default allowed-origin list is empty and credentials are not enabled.

Create/update the database from the committed migrations, including the PostGIS extension and initial categories:

```sh
dotnet ef database update --project src/StockNet.Api/StockNet.Api.csproj --startup-project src/StockNet.Api/StockNet.Api.csproj
```

Run or build the API:

```sh
dotnet run --project src/StockNet.Api/StockNet.Api.csproj
dotnet build StockNet.slnx
```

The category seed is managed in `CategoryConfiguration` and included in the initial EF migration. Spatial columns use PostGIS `geography(Point, 4326)`; create query points with X = longitude, Y = latitude, and SRID 4326.

## Integration tests

The integration suite uses the configured PostgreSQL/PostGIS database and removes its test data on completion. Start the `db` service and apply migrations first, then run:

```sh
export ConnectionStrings__StockNet="Host=localhost;Port=5432;Database=stocknet;Username=stocknet;Password=stocknet"
export Jwt__SigningKey="$(openssl rand -base64 48)"
dotnet test StockNet.slnx
```

The spatial map integration test verifies active/availability filters, public DTO fields, radius validation, generated `ST_DWithin` SQL, and use of the warehouse GiST index. Messaging integration tests cover participant access, duplicate thread prevention, paginated message order, and stock-delete cascade behavior.

## Excel stock import

`POST /api/stocks/import` accepts a multipart `.xlsx` with a `WarehouseId` field and a `File` field. The first worksheet must have headers `CategoryId`, `Unit`, `Title`, `Description`, `TotalQuantity`, `MinOrderQuantity`, `StepQuantity`, `PackagingType`, `PricePerUnit`, `Currency`, `AvailableFrom`, and `AvailableTo`. `AttributesJson` is optional when the selected category has no required attributes; otherwise it must contain every category-required key. Imports are limited to 10 MB and 1,000 rows; all rows are validated before any are written. `Unit` values are `Tons`, `Kilograms`, `Pieces`, `Units`, `CubicMeters`, or `Liters`.

Stock quantities and prices are unit-agnostic. `Pieces` and `Units` require whole-number total, minimum-order, and step quantities; other units accept up to three fractional digits. Categories form a parent/child taxonomy and define required JSONB attribute keys (for example, Metals requires `steel_grade`; Seeds requires `moisture` and `germination_rate`).

## API container and CI

Build the non-root multi-stage API image with `docker build -t stocknet-api .`. The API container listens on port 8080 and expects connection string, JWT, Stripe, and CORS settings through environment configuration. The GitHub Actions workflow builds the solution and runs integration tests against a temporary PostgreSQL/PostGIS service.

## Local performance / load testing

Isolated performance runs use Docker Compose, PostgreSQL/PostGIS, and [k6](https://k6.io). The harness rebuilds the API image, migrates and seeds 100k stocks, and caps the full test containers at a combined 2 CPUs / 4 GB (API: 0.9 CPU / 1.5 GB, DB: 0.9 CPU / 2 GB, k6: 0.2 CPU / 512 MB):

```powershell
.\scripts\run-performance-tests.ps1 -Scenario load-business
.\scripts\run-performance-tests.ps1 -Scenario load-indexes
.\scripts\run-index-plans.ps1
```

To quickly verify the authenticated index endpoints before a full run, start the perf stack and run `docker compose -f docker-compose.perf.yml run --rm -e K6_SMOKE=true k6 run /scripts/load-indexes.js`.

`load-business.js` exercises spatial search with category, price, radius, and paging filters at 100–200 requests/minute with a 500/minute peak. `load-indexes.js` logs in as the local seeded supplier and exercises company stock/warehouse reads, message-thread lists, message pages, and spatial/category/price search at 200 requests/minute with a 500/minute peak. The index fixtures give that supplier 10k stocks and add 50k message threads plus about 101k messages. `run-index-plans.ps1` prints PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` plans for those query patterns.

The company stock endpoint currently returns all matching stocks (no pagination), so that scenario intentionally measures both index lookup and response serialization/transfer. An index can reduce lookup work, but returning `k` matching rows still costs at least O(k); inspect the actual query plan and timings at multiple table sizes rather than inferring complexity from a single k6 run. Performance Compose cleanup removes its isolated volume when the runner exits.
