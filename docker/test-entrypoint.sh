#!/usr/bin/env bash
set -euo pipefail

cd /src
rm -rf src/StockNet.Api/bin src/StockNet.Api/obj tests/StockNet.Api.IntegrationTests/bin tests/StockNet.Api.IntegrationTests/obj
dotnet tool restore
dotnet restore src/StockNet.Api/StockNet.Api.csproj
dotnet restore tests/StockNet.Api.IntegrationTests/StockNet.Api.IntegrationTests.csproj
dotnet ef database update --project src/StockNet.Api/StockNet.Api.csproj --startup-project src/StockNet.Api/StockNet.Api.csproj
dotnet test StockNet.slnx --configuration Release --logger "trx;LogFileName=/src/TestResults/integration-tests.trx"