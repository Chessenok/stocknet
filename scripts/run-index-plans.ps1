param()

$ErrorActionPreference = "Stop"

docker compose -f docker-compose.perf.yml up --build -d db migrate api
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

docker compose -f docker-compose.perf.yml run --rm plans
$exitCode = $LASTEXITCODE

docker compose -f docker-compose.perf.yml down -v

exit $exitCode
