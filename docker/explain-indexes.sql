-- Run after seed-performance.sql and seed-index-fixtures.sql.
-- Plans are not forced: they show whether the cost-based planner chooses each index.

\echo '=== GET /api/stocks/my: all stocks owned by supplier ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT s."Id", s."WarehouseId", w."Name" AS "WarehouseName", s."CategoryId",
       s."Title", s."Description", s."TotalQuantity", s."MinOrderQuantity",
       s."StepQuantity", s."Unit", s."PackagingType", s."PricePerUnit",
       s."Currency", s."Attributes", s."AvailableFrom", s."AvailableTo",
       s."IsActive", w."Location", s."CreatedAt", s."UpdatedAt"
FROM "Stocks" s
JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
WHERE w."CompanyId" = (
    SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY s."CreatedAt" DESC, s."Id"
LIMIT 100 OFFSET 0;

\echo '=== GET /api/warehouses: supplier warehouses ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT w."Id", w."Name", w."AddressText", w."Location"
FROM "Warehouses" w
WHERE w."CompanyId" = (
    SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY w."Name", w."Id";

\echo '=== GET /api/message-threads: supplier participation OR query ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT thread."Id", thread."StockId", thread."BuyerCompanyId",
       thread."SellerCompanyId", thread."CreatedAt"
FROM "MessageThreads" thread
WHERE thread."BuyerCompanyId" = (
          SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
   OR thread."SellerCompanyId" = (
          SELECT "Id" FROM "Companies" WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL')
ORDER BY thread."CreatedAt" DESC, thread."Id";

\echo '=== GET messages: total count by thread ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT count(*)
FROM "Messages"
WHERE "ThreadId" = '00000000-0000-4000-8000-000000000002';

\echo '=== GET messages: first page ordered by thread/time ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT message."Id", message."ThreadId", message."SenderCompanyId",
       message."Text", message."SentAt"
FROM "Messages" message
WHERE message."ThreadId" = '00000000-0000-4000-8000-000000000002'
ORDER BY message."SentAt", message."Id"
LIMIT 50 OFFSET 0;

\echo '=== JSONB GIN candidate: category-specific containment ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT stock."Id"
FROM "Stocks" stock
WHERE stock."CategoryId" = 9
  AND stock."Attributes" @> '{"moisture":7.5}'::jsonb;

\echo '=== GET /api/stocks/map: geography + category + unit + JSONB + price ==='
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT stock."Id", stock."CategoryId", stock."Unit", stock."Attributes",
       ST_Distance(warehouse."Location", ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography) AS distance_meters
FROM "Stocks" stock
JOIN "Warehouses" warehouse ON warehouse."Id" = stock."WarehouseId"
WHERE stock."IsActive" = TRUE
  AND stock."AvailableFrom" <= now()
  AND stock."AvailableTo" >= now()
  AND stock."CategoryId" = 9
  AND stock."Unit" = 'Tons'
  AND stock."PricePerUnit" BETWEEN 50 AND 59
  AND stock."Attributes" @> '{"moisture":7.5}'::jsonb
  AND ST_DWithin(
      warehouse."Location",
      ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography,
      5000)
ORDER BY ST_Distance(warehouse."Location", ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography), stock."Id"
LIMIT 50;
