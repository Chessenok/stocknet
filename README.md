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

`POST /api/stocks/import` accepts a multipart `.xlsx` with a `WarehouseId` field and a `File` field. The first worksheet must have headers `CategoryId`, `Title`, `Description`, `TotalQuantity`, `MinOrderQuantity`, `StepQuantity`, `PackagingType`, `PricePerUnit`, `Currency`, `AvailableFrom`, and `AvailableTo`. `Unit` is optional for legacy files and defaults to `Tons`; supported values are `Tons`, `Kilograms`, `Pieces`, `Units`, `CubicMeters`, and `Liters`. `AttributesJson` is optional when the selected category has no required attributes; otherwise it must satisfy the category's attribute rules. Imports are limited to 10 MB and 1,000 rows; all rows are validated before any are written.

Stock quantities and prices are unit-agnostic. `Pieces` and `Units` require whole-number total, minimum-order, and step quantities; other units accept up to three fractional digits. Categories form a parent/child taxonomy and expose dynamic attribute schemas through public `GET /api/categories` and `GET /api/categories/{id}`. Rules define key, type (`String`, `Number`, `Boolean`), required status, numeric bounds, and allowed string codes. Keys and allowed values are strict lowercase technical codes (for example, `steel_grade`, `a36`, `class_1`); localized labels belong in the frontend. The map endpoint accepts URL-encoded JSON object filters through `attributeFilters`, e.g. `?attributeFilters=%7B%22steel_grade%22%3A%22a36%22%7D`.

## API container and CI

Build the non-root multi-stage API image with `docker build -t stocknet-api .`. The API container listens on port 8080 and expects connection string, JWT, Stripe, and CORS settings through environment configuration. The GitHub Actions workflow builds the solution and runs integration tests against a temporary PostgreSQL/PostGIS service.

## Local performance / load testing

Load tests use [k6](https://k6.io) and live in `tests/k6/`. Start the API and database first, then run:

```sh
k6 run tests/k6/smoke.js
k6 run tests/k6/load-map.js
```

`load-map.js` drives the spatial discovery endpoint with up to 50 VUs. For stable results on macOS, give Colima/Docker at least 4 CPUs and 8 GB RAM; otherwise PostgreSQL can drop connections and Npgsql may report `cache lookup failed` (OID/type desync). If that happens, restart the database and retry:

```sh
docker compose down -v && docker compose up -d db
dotnet ef database update --project src/StockNet.Api/StockNet.Api.csproj --startup-project src/StockNet.Api/StockNet.Api.csproj
```

### JSONB attribute filtering

`tests/k6/load-jsonb.js` filters 100k seeded stocks using string attributes (`steel_grade`, `equipment_type`) and numeric Seeds attributes (`moisture`, `germination_rate`). It verifies every returned row matches the requested JSONB values, tests compound containment and no-match behavior, and applies 200 requests/minute with a 500/minute peak. The seed uses valid category-schema values and a fixed warehouse location for repeatable results. Run a short route/data sanity check or the full load profile:

```powershell
.\scripts\run-jsonb-load.ps1 -Smoke
.\scripts\run-jsonb-load.ps1
```

The integration suite also checks partial/compound JSONB containment, invalid filter shapes, and use of the `IX_Stocks_Attributes` GIN index.

### Index workload

```powershell
.\scripts\run-index-load.ps1 -Smoke
.\scripts\run-index-load.ps1
.\scripts\run-index-plans.ps1
```

`GET /api/stocks/my` accepts `page` and `pageSize` (defaults to 1 and 50; max page size 100). The index workload reads a 100-row page from a supplier inventory of 10k stocks, supplier warehouses, 5k supplier threads, a 50-message page from a 1k-message thread, and a JSONB/spatial map result. The plan report uses `EXPLAIN (ANALYZE, BUFFERS)` against the matching 100k-stock, 50k-thread, and ~101k-message fixture. Paginate company stock reads rather than repeatedly transferring the full inventory; the plan output shows whether PostgreSQL can use the warehouse/stock indexes for the page.
