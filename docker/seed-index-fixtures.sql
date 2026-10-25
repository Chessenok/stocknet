-- Index workload fixture: transfer exactly 10k central stocks to the local supplier,
-- then add a selective 50k-thread / ~101k-message workload.
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

    -- Remove prior perf threads first; Messages cascade with MessageThreads.
    DELETE FROM "MessageThreads" WHERE "BuyerCompanyId" = buyer_id;

    INSERT INTO "Companies" (
        "Id", "Name", "Email", "NormalizedEmail", "PasswordHash", "TaxId",
        "CountryCode", "CompanyType", "IsVerified", "IsAdmin", "IsSubscribed",
        "SubscriptionExpiresAt", "CreatedAt", "UpdatedAt")
    VALUES (
        buyer_id, 'Index Fixture Buyer', 'index-fixture-buyer@example.test',
        'INDEX-FIXTURE-BUYER@EXAMPLE.TEST', 'not-used', 'INDEX-FIXTURE-BUYER',
        'RO', 'Buyer', TRUE, FALSE, FALSE, NULL, now(), now())
    ON CONFLICT ("Id") DO NOTHING;

    UPDATE "Warehouses" warehouse
    SET "CompanyId" = seller_id
    FROM "Companies" perf_company
    WHERE warehouse."CompanyId" = perf_company."Id"
      AND perf_company."Email" = 'perf-company-1@example.test'
      AND warehouse."Name" = 'Perf Warehouse 1-1';

    SELECT stock."Id" INTO first_stock_id
    FROM "Stocks" stock
    JOIN "Warehouses" warehouse ON warehouse."Id" = stock."WarehouseId"
    WHERE warehouse."CompanyId" = seller_id
    ORDER BY stock."Id"
    LIMIT 1;

    IF first_stock_id IS NULL THEN
        RAISE EXCEPTION 'Expected supplier-owned performance stocks';
    END IF;

    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    VALUES (test_thread_id, first_stock_id, buyer_id, seller_id, now() - interval '30 days')
    ON CONFLICT ("Id") DO NOTHING;

    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    SELECT gen_random_uuid(), selected."Id", buyer_id, seller_id,
           now() - (selected.row_num * interval '1 minute')
    FROM (
        SELECT stock."Id", row_number() OVER (ORDER BY stock."Id") AS row_num
        FROM "Stocks" stock
        JOIN "Warehouses" warehouse ON warehouse."Id" = stock."WarehouseId"
        WHERE warehouse."CompanyId" = seller_id
          AND stock."Id" <> first_stock_id
        ORDER BY stock."Id"
        LIMIT 4999
    ) AS selected
    ON CONFLICT ("StockId", "BuyerCompanyId") DO NOTHING;

    -- Noise from other sellers makes seller-participant index selectivity realistic.
    INSERT INTO "MessageThreads" ("Id", "StockId", "BuyerCompanyId", "SellerCompanyId", "CreatedAt")
    SELECT gen_random_uuid(), selected."Id", buyer_id, selected."CompanyId",
           now() - (selected.row_num * interval '30 seconds')
    FROM (
        SELECT stock."Id", warehouse."CompanyId", row_number() OVER (ORDER BY stock."Id") AS row_num
        FROM "Stocks" stock
        JOIN "Warehouses" warehouse ON warehouse."Id" = stock."WarehouseId"
        WHERE warehouse."CompanyId" <> seller_id
        ORDER BY stock."Id"
        LIMIT 45000
    ) AS selected
    ON CONFLICT ("StockId", "BuyerCompanyId") DO NOTHING;

    INSERT INTO "Messages" ("Id", "ThreadId", "SenderCompanyId", "Text", "SentAt")
    SELECT gen_random_uuid(), test_thread_id, seller_id,
           'Index fixture message ' || n,
           now() - ((1000 - n) * interval '1 minute')
    FROM generate_series(1, 1000) AS n;

    INSERT INTO "Messages" ("Id", "ThreadId", "SenderCompanyId", "Text", "SentAt")
    SELECT gen_random_uuid(), thread."Id", seller_id,
           'Index fixture message ' || n,
           thread."CreatedAt" + (n * interval '1 minute')
    FROM "MessageThreads" thread
    CROSS JOIN generate_series(1, 20) AS n
    WHERE thread."SellerCompanyId" = seller_id
      AND thread."Id" <> test_thread_id;
END $$;

DO $$
DECLARE
    supplier_stock_count bigint;
    supplier_thread_count bigint;
    message_count bigint;
BEGIN
    SELECT count(*) INTO supplier_stock_count
    FROM "Stocks" stock
    JOIN "Warehouses" warehouse ON warehouse."Id" = stock."WarehouseId"
    JOIN "Companies" company ON company."Id" = warehouse."CompanyId"
    WHERE company."NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL';

    SELECT count(*) INTO supplier_thread_count
    FROM "MessageThreads" thread
    JOIN "Companies" company ON company."Id" = thread."SellerCompanyId"
    WHERE company."NormalizedEmail" = 'SUPPLIER@STOCKNET.LOCAL';

    SELECT count(*) INTO message_count FROM "Messages";

    IF supplier_stock_count <> 10000 THEN
        RAISE EXCEPTION 'Expected 10000 supplier stocks, found %', supplier_stock_count;
    END IF;
    IF supplier_thread_count <> 5000 THEN
        RAISE EXCEPTION 'Expected 5000 supplier threads, found %', supplier_thread_count;
    END IF;
    IF message_count < 100000 THEN
        RAISE EXCEPTION 'Expected at least 100000 messages, found %', message_count;
    END IF;
END $$;

ANALYZE "Companies";
ANALYZE "Warehouses";
ANALYZE "Stocks";
ANALYZE "MessageThreads";
ANALYZE "Messages";
