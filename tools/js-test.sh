#!/usr/bin/env bash
# JS unit tests for the studio canvas modules. Uses Node's built-in test runner
# (node --test) — zero dependencies, no node_modules, so it runs from the repo on
# the VirtualBox shared folder. Covers the pure geometry + render layers.
set -euo pipefail
cd "$(dirname "$0")/.."
exec node --test tests/js/*.test.js
