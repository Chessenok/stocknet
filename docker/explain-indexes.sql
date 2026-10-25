-- Run after seed-performance.sql and seed-index-fixtures.sql.
-- Compare actual plans/timings on PostgreSQL; do not force index scans.

\echo '=== Stocks owned by one company (/api/stocks/my) ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT s."Id", s."Title", s."CreatedAt", s."UpdatedAt", s."CategoryId",
       s."TotalQuantity", s."MinOrderQuantity", s."StepQuantity", s."Unit",
       s."PackagingType", s."PricePerUnit", s."Currency", s."Attributes",
       s."AvailableFrom", s."AvailableTo", s."IsActive", s."WarehouseId"
FROM "Stocks" s
JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
WHERE w."CompanyId" = (SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY s."CreatedAt" DESC, s."Id";

\echo '=== Warehouses owned by one company (/api/warehouses) ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT w."Id", w."Name", w."AddressText", w."Location"
FROM "Warehouses" w
WHERE w."CompanyId" = (SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY w."Name", w."Id";

\echo '=== Company message threads (/api/message-threads) ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT t."Id", t."StockId", t."BuyerCompanyId", t."SellerCompanyId", t."CreatedAt"
FROM "MessageThreads" t
WHERE t."BuyerCompanyId" = (SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
   OR t."SellerCompanyId" = (SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY t."CreatedAt" DESC, t."Id";

\echo '=== Message count and paged messages (/api/message-threads/{id}/messages) ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT count(*)
FROM "Messages"
WHERE "ThreadId" = '00000000-0000-4000-8000-000000000002';

EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT m."Id", m."ThreadId", m."SenderCompanyId", m."Text", m."SentAt"
FROM "Messages" m
WHERE m."ThreadId" = '00000000-0000-4000-8000-000000000002'
ORDER BY m."SentAt", m."Id"
LIMIT 50 OFFSET 0;

\echo '=== Spatial/category/price filtered map search (/api/stocks/map) ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT s."Id", s."Title", s."CategoryId", s."PricePerUnit",
       ST_Distance(w."Location", ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography) AS distance_meters
FROM "Stocks" s
JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
WHERE s."IsActive" = TRUE
  AND s."AvailableFrom" <= now()
  AND s."AvailableTo" >= now()
  AND s."CategoryId" = 1
  AND s."PricePerUnit" BETWEEN 40 AND 80
  AND ST_DWithin(w."Location", ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography, 25000)
ORDER BY ST_Distance(w."Location", ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography), s."Id"
LIMIT 50;
