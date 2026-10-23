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
```

`Jwt:Issuer` and `Jwt:Audience` have non-secret defaults in `appsettings.json`; override them per environment when needed. The signing key must contain at least 32 UTF-8 bytes.

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
