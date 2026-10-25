param(
    [string]$Scenario = "load-business"
)

$ErrorActionPreference = "Stop"

docker compose -f docker-compose.perf.yml up --build -d db migrate api
docker compose -f docker-compose.perf.yml run --rm k6 run /scripts/$Scenario.js

$exitCode = $LASTEXITCODE

docker compose -f docker-compose.perf.yml down -v

exit $exitCode
