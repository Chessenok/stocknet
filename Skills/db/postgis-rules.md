# PostGIS and Spatial Query Rules

## 1. Coordinate and Distance Contract
- API inputs use latitude/longitude in degrees; distance and radius values use meters.
- NTS `Point` coordinates are ordered X = longitude and Y = latitude. Set SRID 4326 on stored and query points.
- The map search center is supplied by the frontend and may be any location; it does not need to match a warehouse or company address.
- Validate finite latitude in `[-90, 90]`, longitude in `[-180, 180]`, and a positive, product-bounded radius before querying.

## 2. Meter-Based Database Queries
- Use PostgreSQL/PostGIS through EF Core, Npgsql, and NetTopologySuite. Never compute distance in application memory after loading all stocks.
- Use a PostGIS `geography(Point, 4326)` representation (or an explicitly meter-based equivalent) for meter distances. A plain planar `geometry(Point, 4326)` distance is in degrees and must not be treated as meters.
- Apply an index-usable PostGIS radius predicate such as `ST_DWithin` through an EF/Npgsql-translated NTS operation, then apply remaining stock filters and project results in the database.
- Verify the generated SQL and query plan on PostgreSQL. Ensure a GiST index exists on warehouse location and the NTS/provider mapping can use it.
- If sorting by distance, compute distance server-side in meters and use deterministic tie-breaking (for example, StockId).
- Do not interpolate SQL. If raw SQL is unavoidable, use parameterized queries and preserve the spatial index opportunity.

## 3. Map Endpoint Behavior
- Anonymous search is limited to stocks where `IsActive == true` and the current UTC time is within `[AvailableFrom, AvailableTo]`, plus supported search filters. Never include inactive or out-of-window stock in anonymous results.
- Project directly to a map DTO (stock ID/title, price/currency and approved summary fields, latitude, longitude, and optionally distance in meters); never serialize EF entities.
- Use pagination/limits to cap results. Validate filter/radius combinations and avoid unbounded map dumps.
- Do not expose company lists or private company data as part of stock search. Include only explicitly approved public seller summary fields if product requirements later call for them.
- Keep the query asynchronous (`ToListAsync`, etc.).

## 4. Bounding Boxes
- Validate west/east/south/north ranges and handle antimeridian-crossing boxes explicitly if supported.
- Use spatial predicates compatible with SRID 4326 and the configured geography/geometry type. Keep bounding-box behavior documented and tested independently from radius-in-meters behavior.
