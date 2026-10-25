#!/usr/bin/env bash
set -euo pipefail

cd /src
rm -rf src/StockNet.Api/bin src/StockNet.Api/obj
dotnet tool restore
dotnet restore src/StockNet.Api/StockNet.Api.csproj
dotnet ef database update --project src/StockNet.Api/StockNet.Api.csproj --startup-project src/StockNet.Api/StockNet.Api.csproj