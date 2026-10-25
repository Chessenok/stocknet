-- Isolated PostGIS performance dataset: 100k active stocks with valid JSONB category attributes.
-- A known warehouse is centered at (longitude=25, latitude=45) for deterministic map filters.

DELETE FROM "Stocks" WHERE "Title" LIKE 'Perf Stock %';
DELETE FROM "Warehouses" WHERE "Name" LIKE 'Perf Warehouse %';
DELETE FROM "Companies" WHERE "Email" LIKE 'perf-company-%@example.test';

INSERT INTO "Companies" (
    "Id", "Name", "Email", "NormalizedEmail", "PasswordHash", "TaxId",
    "CountryCode", "CompanyType", "IsVerified", "IsAdmin", "IsSubscribed",
    "SubscriptionExpiresAt", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    'Perf Company ' || i,
    'perf-company-' || i || '@example.test',
    'PERF-COMPANY-' || i || '@EXAMPLE.TEST',
    'not-used',
    'PERF-' || i,
    'RO',
    'Producer',
    TRUE,
    FALSE,
    TRUE,
    NULL,
    now(),
    now()
FROM generate_series(1, 10) AS i;

INSERT INTO "Warehouses" (
    "Id", "CompanyId", "Name", "AddressText", "Location", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    company."Id",
    'Perf Warehouse ' || company_index.i || '-' || warehouse_index.w,
    'Performance test location',
    CASE
        WHEN company_index.i = 1 AND warehouse_index.w = 1
            THEN ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography
        ELSE ST_SetSRID(ST_MakePoint(20 + random() * 10, 43 + random() * 8), 4326)::geography
    END,
    now(),
    now()
FROM generate_series(1, 10) AS company_index(i)
JOIN "Companies" company
    ON company."Email" = 'perf-company-' || company_index.i || '@example.test'
CROSS JOIN generate_series(1, 2) AS warehouse_index(w);

WITH warehouse_map AS (
    SELECT
        (SELECT "Id" FROM "Warehouses" WHERE "Name" = 'Perf Warehouse 1-1') AS center_id,
        (SELECT array_agg("Id" ORDER BY "Id")
         FROM "Warehouses"
         WHERE "Name" LIKE 'Perf Warehouse %' AND "Name" <> 'Perf Warehouse 1-1') AS other_ids
)
INSERT INTO "Stocks" (
    "Id", "WarehouseId", "CategoryId", "Title", "Description",
    "TotalQuantity", "MinOrderQuantity", "StepQuantity", "Unit",
    "PackagingType", "PricePerUnit", "Currency", "Attributes",
    "AvailableFrom", "AvailableTo", "IsActive", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    CASE
        WHEN stock_index.i % 20 IN (1, 2) THEN warehouse_map.center_id
        ELSE warehouse_map.other_ids[((stock_index.i - 1) % array_length(warehouse_map.other_ids, 1)) + 1]
    END,
    CASE
        WHEN stock_index.i % 3 = 0 THEN 10
        WHEN stock_index.i % 3 = 1 THEN 1
        ELSE 9
    END,
    'Perf Stock ' || stock_index.i,
    'Generated for isolated JSONB load testing.',
    CASE WHEN stock_index.i % 3 = 0 THEN 100 ELSE 1000 END,
    CASE WHEN stock_index.i % 3 = 0 THEN 1 ELSE 10 END,
    CASE WHEN stock_index.i % 3 = 0 THEN 1 ELSE 5 END,
    CASE WHEN stock_index.i % 3 = 0 THEN 'Pieces' ELSE 'Tons' END,
    CASE WHEN stock_index.i % 3 = 0 THEN 'Equipment' WHEN stock_index.i % 3 = 2 THEN 'Bagged' ELSE 'Bulk' END,
    50 + (stock_index.i % 10),
    'EUR',
    CASE
        WHEN stock_index.i % 3 = 0 THEN jsonb_build_object(
            'equipment_type', (ARRAY['compressor', 'motor', 'pump'])[1 + ((stock_index.i / 3) % 3)],
            'source', 'performance-seeder')
        WHEN stock_index.i % 3 = 1 THEN jsonb_build_object(
            'steel_grade', (ARRAY['a36', 's235', 's355'])[1 + ((stock_index.i / 3) % 3)],
            'source', 'performance-seeder')
        ELSE jsonb_build_object(
            'moisture', (ARRAY[5.0, 7.5, 10.0])[1 + ((stock_index.i / 3) % 3)],
            'germination_rate', (ARRAY[85, 90, 95])[1 + ((stock_index.i / 9) % 3)],
            'source', 'performance-seeder')
    END,
    now() - interval '1 day',
    now() + interval '30 days',
    TRUE,
    now(),
    now()
FROM generate_series(1, 100000) AS stock_index(i)
CROSS JOIN warehouse_map;

ANALYZE "Companies";
ANALYZE "Warehouses";
ANALYZE "Stocks";
