#!/usr/bin/env bash
# Playwright e2e runner. Ensures the dev server is up (tools/dev.sh) and the Chromium browser is
# installed, then runs the Playwright suite under tests/e2e.
#
# The e2e harness is deliberately isolated: it is the ONLY place node_modules lives, and it is
# gitignored. It does NOT touch the zero-dependency JS unit tests (tools/js-test.sh / `node --test`).
#
#   ./tools/e2e.sh                       # run the whole suite (headless)
#   ./tools/e2e.sh --headed              # any extra args pass through to `playwright test`
#   PGM_E2E_SEED_MAP=thunder ./tools/e2e.sh   # also run the seed-gated Configure → Export flow
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
E2E="$ROOT/tests/e2e"

cd "$E2E"

if [[ ! -d node_modules ]]; then
  echo "installing e2e dependencies (one-time)…"
  npm install
fi

# Install the Chromium binary unless the environment already provides one
# (PLAYWRIGHT_BROWSERS_PATH is set in some sandboxes with browsers pre-staged).
if [[ -z "${PLAYWRIGHT_BROWSERS_PATH:-}" ]]; then
  npx playwright install chromium
fi

# Playwright's webServer block boots the app via tools/dev.sh and waits on /api/health, reusing a warm
# local server. Run it directly to manage the server yourself: PGM_E2E_NO_WEBSERVER=1 ./tools/dev.sh start
exec npx playwright test "$@"
