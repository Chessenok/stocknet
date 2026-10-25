#!/usr/bin/env bash
set -euo pipefail

cd /src
dotnet tool restore
dotnet restore src/StockNet.Api/StockNet.Api.csproj
dotnet ef database update \
  --project src/StockNet.Api/StockNet.Api.csproj \
  --startup-project src/StockNet.Api/StockNet.Api.csproj
