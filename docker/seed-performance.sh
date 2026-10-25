#!/usr/bin/env bash
set -euo pipefail

export PGPASSWORD="${POSTGRES_PASSWORD:-stocknet}"
psql -h "${POSTGRES_HOST:-db}" -U "${POSTGRES_USER:-stocknet}" -d "${POSTGRES_DB:-stocknet}" -v ON_ERROR_STOP=1 -f /seed.sql