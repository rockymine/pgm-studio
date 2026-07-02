#!/usr/bin/env bash
# Seed a test map into a running studio, then print its export URL.
# See tools/seeds/README.md for the layouts + variants.
#
#   ./tools/seeds/seed.sh [API_BASE]                 # default http://localhost:7894/api
#   SEED=base-4team ./tools/seeds/seed.sh            # pick the variant (default base-2island)
#   SEED_NAME="My Test" ./tools/seeds/seed.sh
set -euo pipefail

BASE="${1:-http://localhost:7894/api}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEED="${SEED:-base-2island}"
LAYOUT="$(cat "$DIR/$SEED.layout.json")"
INTENT="$(cat "$DIR/$SEED.intent.json")"
NAME="${SEED_NAME:-$SEED}"

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
