# .NET 10 API Standards & Error Handling

## 1. Endpoint Exposure and Access
- The only unauthenticated business API is active-stock map/search (for example `GET /api/stocks/map`) and its supported filters.
- Registration and login (`POST /api/auth/register`, `POST /api/auth/login`) are public anonymous identity-infrastructure endpoints.
- All other business endpoints require authentication, including stock mutations, company-specific data, and message threads/messages.
- Never add public company-list or public company-detail endpoints. An authenticated `/api/companies/me` endpoint is appropriate for the caller's own company profile.
- Require ownership checks for stock writes and participant checks for message access. Do not trust a CompanyId supplied in a request as the authenticated identity.
- There are no order or goods-checkout endpoints in the MVP.

## 2. DTO Projections (Mandatory)
Never expose database Entity models (`DbSet<T>`) directly in controller responses. Project data into lightweight DTOs using LINQ `.Select()` so the database returns only fields required by the response.

Example:
```csharp
[HttpGet("map")]
public async Task<ActionResult<IEnumerable<StockMapDto>>> GetStocksForMap([FromQuery] MapSearchQuery query)
{
    var stocks = await _context.Stocks
        .Where(s => s.IsActive)
        .Select(s => new StockMapDto
        {
            Id = s.Id,
            Title = s.Title,
            Lat = s.Warehouse.Location.Y,
            Lng = s.Warehouse.Location.X,
            PricePerUnit = s.PricePerUnit,
            Currency = s.Currency
        })
        .ToListAsync();

    return Ok(stocks);
}
```

Map search additionally must apply meter-based spatial filtering as defined in `Skills/db/postgis-rules.md`, impose result limits/pagination, and return only active matching stocks.

## 3. Validation and Errors
- Use request/response DTOs, input validation, and consistent HTTP status codes.
- Use global exception handling with `ProblemDetails`; do not expose stack traces, SQL, secrets, or internal implementation details to clients.
- Use `401 Unauthorized` when authentication is missing/invalid, `403 Forbidden` when authenticated but disallowed, and non-disclosing `404 Not Found` where exposing resource existence would leak another company's data.
- Return `403 Forbidden` when a company without a current subscription attempts to create stock or update stock content. Archiving/deleting stock is not subscription-gated.

## 4. I/O and Query Rules
- All database, network, and filesystem I/O must be asynchronous.
- Avoid loading entities/collections only to transform them in memory; filter, order, paginate, and project in SQL.
- Spatial queries must follow `Skills/db/postgis-rules.md`; never interpolate SQL.
