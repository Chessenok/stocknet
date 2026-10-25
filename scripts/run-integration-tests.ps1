param()

$ErrorActionPreference = "Stop"

docker compose -f docker-compose.test.yml up --build --abort-on-container-exit --exit-code-from tests
$exitCode = $LASTEXITCODE

docker compose -f docker-compose.test.yml down -v

exit $exitCode