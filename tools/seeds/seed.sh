#!/usr/bin/env bash
# Seed the base 2-island test map into a running studio, then print its export URL.
# See tools/seeds/README.md for the layout + variants.
#
#   ./tools/seeds/seed.sh [API_BASE]        # default http://localhost:7894/api
#   SEED_NAME="My Test" ./tools/seeds/seed.sh
set -euo pipefail

BASE="${1:-http://localhost:7894/api}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LAYOUT="$(cat "$DIR/base-2island.layout.json")"
INTENT="$(cat "$DIR/base-2island.intent.json")"
NAME="${SEED_NAME:-Base 2-Island}"

slug="$(curl -s -X POST "$BASE/sketch" -H 'Content-Type: application/json' \
  -d "{\"name\":\"$NAME\"}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["slug"])')"
echo "slug = $slug"

curl -s -o /dev/null -w "save layout: HTTP %{http_code}\n" \
  -X PUT "$BASE/map/$slug/sketch" -H 'Content-Type: application/json' -d "$LAYOUT"
curl -s -o /dev/null -w "finish:      HTTP %{http_code}\n" \
  -X POST "$BASE/map/$slug/sketch/finish"
curl -s -o /dev/null -w "put intent:  HTTP %{http_code}\n" \
  -X PUT "$BASE/map/$slug/intent" -H 'Content-Type: application/json' -d "$INTENT"

echo "export: GET $BASE/map/$slug/export   (sketch-origin → a {slug}/ world ZIP)"
