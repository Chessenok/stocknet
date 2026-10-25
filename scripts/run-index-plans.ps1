param()

$ErrorActionPreference = "Stop"
$composeFile = "docker-compose.perf.yml"

docker compose -f $composeFile up --build -d db migrate api
if ($LASTEXITCODE -ne 0) {
    $startupExitCode = $LASTEXITCODE
    docker compose -f $composeFile down -v
    exit $startupExitCode
}

docker compose -f $composeFile run --rm plans
$planExitCode = $LASTEXITCODE

docker compose -f $composeFile down -v
exit $planExitCode
