# Local PostgreSQL/PostGIS, Migrations, and Seeding

## 1. Docker Development Database
- Provide a Docker Compose service using a PostgreSQL image with PostGIS installed.
- Configure a persistent local volume, a health check, and a dedicated development database/user. Do not commit production credentials or real secrets.
- Keep local overrides and data volumes out of version control; document required environment variables and safe example configuration.
- The API should use configuration/environment variables for its connection string and fail with a clear error when required configuration is missing.

## 2. Migrations
- Create schema changes as EF Core migrations committed with the code.
- Ensure PostGIS extensions/types, NTS mappings, JSONB columns, foreign keys, uniqueness constraints, and required spatial/lookup indexes are represented in migrations.
- Apply migrations explicitly in local setup and deployment workflows; avoid silently applying production migrations at every API startup.
- Verify migrations against the same PostgreSQL/PostGIS major version used in CI/deployment.

## 3. Development Seeding
- Seed only deterministic, non-sensitive development data, and make the seeder idempotent.
- Use asynchronous EF Core operations. Never seed or print real passwords, access tokens, Stripe credentials, or customer data.
- If a development admin Company is required, take its credentials from local secrets/environment variables and hash the password with ASP.NET Core `PasswordHasher`; never store a plaintext password in source or seed files.
- Seed spatial locations with valid longitude/latitude ordering and SRID 4326. Include sample active/inactive stock only in development/test environments.
- Do not create a separate User record or company membership during seeding: Company is the account identity.

## 4. Test Database
- Spatial integration tests must run against real PostgreSQL/PostGIS because in-memory providers do not reproduce PostGIS translation, geography meter semantics, or index behavior.
- Isolate test data/database lifecycle and never point destructive test setup at a shared or production database.
