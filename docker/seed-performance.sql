-- Isolated performance data: 10 companies, 20 warehouses, 100000 stocks.
-- Safe to rerun: removes previous performance rows first.

DELETE FROM "Stocks" WHERE "Title" LIKE 'Perf Stock %';
DELETE FROM "Warehouses" WHERE "Name" LIKE 'Perf Warehouse %';
DELETE FROM "Companies" WHERE "Email" LIKE 'perf-company-%@example.test';

-- Configure two categories with required JSONB attributes for validation tests.
UPDATE "Categories" SET "RequiredAttributeKeys" = '["steel_grade"]'::jsonb WHERE "Id" = 1;
UPDATE "Categories" SET "RequiredAttributeKeys" = '["equipment_type", "power_kw"]'::jsonb WHERE "Id" = 10;

INSERT INTO "Companies" (
    "Id", "Name", "Email", "NormalizedEmail", "PasswordHash", "TaxId",
    "CountryCode", "CompanyType", "IsVerified", "IsAdmin", "IsSubscribed",
    "SubscriptionExpiresAt", "CreatedAt", "UpdatedAt", "StripeCustomerId")
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
    now(),
    NULL
FROM generate_series(1, 10) AS i;

INSERT INTO "Warehouses" (
    "Id", "CompanyId", "Name", "AddressText", "Location", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    c."Id",
    'Perf Warehouse ' || w,
    'Performance address ' || w,
    CASE
        WHEN c."Email" = 'perf-company-1@example.test' AND w = 1
            THEN ST_SetSRID(ST_MakePoint(25, 45), 4326)::geography
        ELSE ST_SetSRID(ST_MakePoint(20 + random() * 10, 43 + random() * 8), 4326)::geography
    END,
    now(),
    now()
FROM "Companies" c
CROSS JOIN generate_series(1, 2) AS w
WHERE c."Email" LIKE 'perf-company-%@example.test';

WITH warehouses AS (
    SELECT array_agg("Id" ORDER BY "Id") AS ids
    FROM "Warehouses"
    WHERE "Name" LIKE 'Perf Warehouse %'
)
INSERT INTO "Stocks" (
    "Id", "WarehouseId", "CategoryId", "Title", "Description",
    "TotalQuantity", "MinOrderQuantity", "StepQuantity", "Unit",
    "PackagingType", "PricePerUnit", "Currency", "Attributes",
    "AvailableFrom", "AvailableTo", "IsActive", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    w.ids[((i - 1) % array_length(w.ids, 1)) + 1],
    CASE WHEN i % 2 = 0 THEN 10 ELSE 1 END,
    'Perf Stock ' || i,
    'Generated for load testing.',
    CASE WHEN i % 2 = 0 THEN 100 ELSE 1000 END,
    CASE WHEN i % 2 = 0 THEN 1 ELSE 10 END,
    CASE WHEN i % 2 = 0 THEN 1 ELSE 5 END,
    CASE WHEN i % 2 = 0 THEN 'Pieces' ELSE 'Tons' END,
    CASE WHEN i % 2 = 0 THEN 'Equipment' ELSE 'Bulk' END,
    50 + (i % 10),
    'EUR',
    CASE
        WHEN i % 2 = 0 THEN '{"equipment_type":"industrial","power_kw":15,"source":"performance-seeder"}'::jsonb
        ELSE '{"steel_grade":"A","source":"performance-seeder"}'::jsonb
    END,
    now() - interval '1 day',
    now() + interval '30 days',
    TRUE,
    now(),
    now()
FROM generate_series(1, 100000) AS i
CROSS JOIN warehouses w;
