#!/usr/bin/env bash
# End-to-end runner: bring up a throwaway instance of the app, seed it, run the specs, tear it down.
#
#   ./tools/e2e.sh              # seed + smoke (the default gate)
#   ./tools/e2e.sh all          # every spec
#   ./tools/e2e.sh smoke        # one spec by name (tests/e2e/<name>.mjs)
#   ./tools/e2e.sh --keep all   # leave the server running afterwards, to poke at it
#
# It uses its OWN database and port so a run can never touch the dev data (`./tools/dev.sh` on :7894,
# `pgm_studio`) — the specs create maps, and left on the dev DB they pile up in the dashboard.
#
#   E2E_PORT=7895  E2E_DB=pgm_studio_e2e  E2E_DB_USER=pgm  E2E_DB_PASS=pgm_dev_pw  PW_CHROMIUM=<path>
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PORT="${E2E_PORT:-7895}"
DB="${E2E_DB:-pgm_studio_e2e}"
DB_USER="${E2E_DB_USER:-pgm}"
DB_PASS="${E2E_DB_PASS:-pgm_dev_pw}"
DB_HOST="${E2E_DB_HOST:-localhost}"
CONN="Server=${DB_HOST};Database=${DB};Uid=${DB_USER};Pwd=${DB_PASS};"

KEEP=0
if [[ "${1:-}" == "--keep" ]]; then KEEP=1; shift; fi
TARGET="${1:-smoke}"

API_CSPROJ="$ROOT/src/PgmStudio.Api/PgmStudio.Api.csproj"
DLL="$ROOT/src/PgmStudio.Api/bin/Debug/net10.0/PgmStudio.Api.dll"
TMP="$ROOT/.tmp"; mkdir -p "$TMP"
LOG="$TMP/e2e-$PORT.log"
PID=""

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export ConnectionStrings__PgmStudio="$CONN"
export E2E_BASE="http://localhost:$PORT"
export E2E_SEED="$TMP/e2e-seed.json"

cleanup() {
  if [[ -n "$PID" && $KEEP -eq 0 ]]; then
    kill "$PID" 2>/dev/null || true
    wait "$PID" 2>/dev/null || true
    echo "· stopped the test server"
  elif [[ -n "$PID" ]]; then
    echo "· left the test server running on :$PORT (pid $PID) — kill it yourself"
  fi
}
trap cleanup EXIT

echo "── database ($DB) ──"
# A fresh schema each run: the specs create maps, and a suite that inherits yesterday's rows is not a
# fixture, it is a history. Dropping is safe precisely because this database is only ever the suite's.
DB_CLIENT="$(command -v mariadb || command -v mysql)"
[[ -n "$DB_CLIENT" ]] || { echo "no mariadb/mysql client on PATH"; exit 1; }
printf '%s\n' \
  "DROP DATABASE IF EXISTS \`$DB\`;" \
  "CREATE DATABASE \`$DB\`;" \
  "GRANT ALL ON \`$DB\`.* TO '$DB_USER'@'localhost';" \
  "FLUSH PRIVILEGES;" \
  | ${E2E_DB_ADMIN:-sudo -n} "$DB_CLIENT" -u "${E2E_DB_ROOT:-root}" \
  || { echo "could not reset $DB — set E2E_DB_ADMIN/E2E_DB_ROOT if root needs different access"; exit 1; }
dotnet run --project "$ROOT/src/PgmStudio.Import" -- --migrate-only >"$TMP/e2e-migrate.log" 2>&1 \
  || { echo "migrations failed — see $TMP/e2e-migrate.log"; exit 1; }
echo "· schema up to date"

echo "── build ──"
dotnet build "$API_CSPROJ" -v q --nologo >"$TMP/e2e-build.log" 2>&1 \
  || { echo "build failed — see $TMP/e2e-build.log"; exit 1; }

echo "── server (:$PORT) ──"
dotnet "$DLL" --urls "http://0.0.0.0:$PORT" >"$LOG" 2>&1 &
PID=$!
for _ in $(seq 1 60); do
  if curl -sf -m 2 "http://localhost:$PORT/api/health" >/dev/null 2>&1; then break; fi
  if ! kill -0 "$PID" 2>/dev/null; then echo "server exited early — see $LOG"; exit 1; fi
  sleep 1
done
curl -sf -m 2 "http://localhost:$PORT/api/health" >/dev/null || { echo "server never became healthy — see $LOG"; exit 1; }
echo "· up"

echo "── seed ──"
node "$ROOT/tests/e2e/seed.mjs"

run_spec() { echo; echo "── ${1} ──"; node "$ROOT/tests/e2e/${1}.mjs"; }

status=0
if [[ "$TARGET" == "all" ]]; then
  for spec in "$ROOT"/tests/e2e/*.mjs; do
    name="$(basename "$spec" .mjs)"
    [[ "$name" == "seed" ]] && continue
    run_spec "$name" || status=1
  done
else
  run_spec "$TARGET" || status=1
fi

echo
[[ $status -eq 0 ]] && echo "e2e: PASS" || echo "e2e: FAIL"
exit $status
