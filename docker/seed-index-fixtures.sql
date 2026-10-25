-- Add authenticated index fixtures after the API creates the local supplier.
-- The perf supplier owns 10k stocks; seed 50k threads and ~101k messages for selective plans.

DO $$
DECLARE
    seller_id uuid;
    buyer_id uuid := '00000000-0000-4000-8000-000000000001';
    first_stock_id uuid;
    test_thread_id uuid := '00000000-0000-4000-8000-000000000002';
BEGIN
    SELECT "Id" INTO seller_id
    FROM "Companies"
    WHERE "NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL';

    IF seller_id IS NULL THEN
        RAISE EXCEPTION 'Development supplier was not seeded';
    END IF;

    INSERT INTO "Companies" (
        "Id", "Name", "Email", "NormalizedEmail", "PasswordHash", "TaxId",
        "CountryCode", "CompanyType", "IsVerified", "IsAdmin", "IsSubscribed",
        "SubscriptionExpiresAt", "CreatedAt", "UpdatedAt")
    VALUES (
        buyer_id, 'Index Test Buyer', 'index-buyer@example.test', 'INDEX-BUYER@EXAMPLE.TEST',
        'not-used', 'INDEX-TEST-BUYER', 'RO', 'Buyer', TRUE, FALSE, FALSE,
        NULL, now(), now())
    ON CONFLICT ("Id") DO NOTHING;

    -- Transfer two warehouses (5k stocks each) to the real local supplier account.
    UPDATE "Warehouses" w
    SET "CompanyId" = seller_id
    FROM "Companies" perf_company
    WHERE w."CompanyId" = perf_company."Id"
      AND perf_company."Email" = 'perf-company-1@example.test';

    SELECT s."Id" INTO first_stock_id
    FROM "Stocks" s
    JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
    WHERE w."CompanyId" = seller_id
    ORDER BY s."Title", s."Id"
    LIMIT 1;

    IF first_stock_id IS NULL THEN
        RAISE EXCEPTION 'Performance stocks were not seeded or transferred';
    END IF;

    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    VALUES (test_thread_id, first_stock_id, buyer_id, seller_id, now() - interval '30 days')
    ON CONFLICT ("Id") DO NOTHING;

    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    SELECT gen_random_uuid(), selected."Id", buyer_id, seller_id,
           now() - (selected.row_num * interval '1 minute')
    FROM (
        SELECT s."Id", row_number() OVER (ORDER BY s."Id") AS row_num
        FROM "Stocks" s
        JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
        WHERE w."CompanyId" = seller_id
          AND s."Id" <> first_stock_id
        ORDER BY s."Id"
        LIMIT 4999
    ) AS selected
    ON CONFLICT ("StockId", "BuyerCompanyId") DO NOTHING;

    -- Add non-supplier threads so seller-participant lookups select a meaningful subset.
    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    SELECT gen_random_uuid(), selected."Id", buyer_id, selected."CompanyId",
           now() - (selected.row_num * interval '30 seconds')
    FROM (
        SELECT s."Id", w."CompanyId", row_number() OVER (ORDER BY s."Id") AS row_num
        FROM "Stocks" s
        JOIN "Warehouses" w ON w."Id" = s."WarehouseId"
        WHERE w."CompanyId" <> seller_id
        ORDER BY s."Id"
        LIMIT 45000
    ) AS selected
    ON CONFLICT ("StockId", "BuyerCompanyId") DO NOTHING;

    INSERT INTO "Messages" ("Id", "ThreadId", "SenderCompanyId", "Text", "SentAt")
    SELECT gen_random_uuid(), test_thread_id, seller_id,
           'Performance fixture message ' || n,
           now() - ((1000 - n) * interval '1 minute')
    FROM generate_series(1, 1000) AS n
    ON CONFLICT ("Id") DO NOTHING;

    INSERT INTO "Messages" ("Id", "ThreadId", "SenderCompanyId", "Text", "SentAt")
    SELECT gen_random_uuid(), t."Id", seller_id,
           'Performance fixture message ' || n,
           t."CreatedAt" + (n * interval '1 minute')
    FROM "MessageThreads" t
    CROSS JOIN generate_series(1, 20) AS n
    WHERE t."SellerCompanyId" = seller_id
      AND t."Id" <> test_thread_id
    ON CONFLICT ("Id") DO NOTHING;
END $$;

ANALYZE "Companies";
ANALYZE "Warehouses";
ANALYZE "Stocks";
ANALYZE "MessageThreads";
ANALYZE "Messages";
