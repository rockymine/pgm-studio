#!/usr/bin/env bash
# Dev runner for the PgmStudio API host (serves the API + the hosted Blazor WASM client).
#
# Builds once, then runs the built binary directly — avoids `dotnet run`'s per-launch
# build/JIT cold-start, which is slow on the VirtualBox shared folder. Mirrors the Python
# app's tools/studio-dev.sh.
#
#   ./tools/dev.sh restart   # build + (re)start in the background
#   ./tools/dev.sh start
#   ./tools/dev.sh stop
#   ./tools/dev.sh status
#   ./tools/dev.sh logs
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PORT="${PGM_STUDIO_PORT:-7894}"
API_CSPROJ="$ROOT/src/PgmStudio.Api/PgmStudio.Api.csproj"
DLL="$ROOT/src/PgmStudio.Api/bin/Debug/net10.0/PgmStudio.Api.dll"
TMP="$ROOT/.tmp"; mkdir -p "$TMP"
PIDFILE="$TMP/dev-$PORT.pid"
LOGFILE="$TMP/dev-$PORT.log"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export ASPNETCORE_ENVIRONMENT=Development

is_running() { [[ -f "$PIDFILE" ]] && kill -0 "$(cat "$PIDFILE")" 2>/dev/null; }

stop() {
  if is_running; then kill "$(cat "$PIDFILE")" 2>/dev/null || true; sleep 1; fi
  rm -f "$PIDFILE"
  echo "stopped"
}

start() {
  if is_running; then echo "already running (pid $(cat "$PIDFILE")) on :$PORT"; return; fi
  echo "building…"
  dotnet build "$API_CSPROJ" -v q
  echo "starting on :$PORT"
  nohup dotnet "$DLL" --urls "http://0.0.0.0:$PORT" > "$LOGFILE" 2>&1 &
  echo $! > "$PIDFILE"
  # wait for the health endpoint to come up (cold first hit can take a few seconds)
  for _ in $(seq 1 30); do
    if curl -sf -m 2 "http://localhost:$PORT/api/health" >/dev/null 2>&1; then
      echo "up: http://localhost:$PORT  (health http://localhost:$PORT/api/health)"; return
    fi
    sleep 1
  done
  echo "WARNING: health check did not pass within 30s; see $LOGFILE"
}

case "${1:-restart}" in
  start)   start ;;
  stop)    stop ;;
  restart) stop; start ;;
  status)  if is_running; then echo "running (pid $(cat "$PIDFILE")) on :$PORT"; else echo "not running"; fi ;;
  logs)    tail -n "${2:-40}" "$LOGFILE" ;;
  *) echo "usage: $0 {start|stop|restart|status|logs}"; exit 1 ;;
esac
