#!/usr/bin/env bash
set -euo pipefail

cd /src
dotnet tool restore
dotnet restore StockNet.slnx
dotnet test StockNet.slnx \
  --configuration Release \
  --logger "console;verbosity=minimal"
