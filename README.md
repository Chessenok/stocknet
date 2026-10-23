# StockNet API

StockNet is a .NET 10 API using PostgreSQL/PostGIS and Entity Framework Core.

## Local database

Requirements: .NET 10 SDK and Docker Compose.

Start the local PostgreSQL/PostGIS service:

```sh
docker compose up -d postgres
```

Configure the API connection string in user-secrets (the Compose credentials are for local development only):

```sh
dotnet user-secrets set "ConnectionStrings:StockNet" "Host=localhost;Port=5432;Database=stocknet;Username=stocknet;Password=stocknet_dev_password" --project src/StockNet.Api/StockNet.Api.csproj
```

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
