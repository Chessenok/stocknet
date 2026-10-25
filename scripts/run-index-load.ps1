param(
    [switch]$Smoke
)

$ErrorActionPreference = "Stop"
$composeFile = "docker-compose.perf.yml"

docker compose -f $composeFile up --build -d db migrate api
if ($LASTEXITCODE -ne 0) {
    $startupExitCode = $LASTEXITCODE
    docker compose -f $composeFile down -v
    exit $startupExitCode
}

if ($Smoke) {
    docker compose -f $composeFile run --rm -e K6_SMOKE=true k6 run /scripts/load-indexes.js
} else {
    docker compose -f $composeFile run --rm k6 run /scripts/load-indexes.js
}
$testExitCode = $LASTEXITCODE

docker compose -f $composeFile down -v
exit $testExitCode
