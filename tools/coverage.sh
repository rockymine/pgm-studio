#!/usr/bin/env bash
# Line coverage for the C# side. `dotnet test` is unavailable on the .NET 10 SDK (the VSTest bridge is
# gone), which also rules out `--collect:"XPlat Code Coverage"`; dotnet-coverage instead wraps the TUnit
# test executable as an ordinary process, so no test project needs a coverage package reference.
#
#   tools/coverage.sh              collect every test project, merge, print the report
#   tools/coverage.sh --report     re-print from the existing merge (no test run)
#
# JS coverage is separate and needs no tooling: `npm test -- --experimental-test-coverage`. Note it only
# lists files a test imported — a module no test loads is absent, not zero.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=${COVERAGE_OUT:-.tmp/coverage}
MERGED="$OUT/merged.cobertura.xml"
PROJECTS=(Geom Domain Analysis Pgm Minecraft Import Data Api)

command -v dotnet-coverage >/dev/null || export PATH="$PATH:$HOME/.dotnet/tools"
command -v dotnet-coverage >/dev/null || {
  echo "dotnet-coverage not found. Install it with:" >&2
  echo "  dotnet tool install --global dotnet-coverage" >&2
  exit 1
}

if [[ ${1:-} != --report ]]; then
  mkdir -p "$OUT"
  rm -f "$OUT"/*.cobertura.xml
  dotnet build PgmStudio.slnx -v q --nologo
  for p in "${PROJECTS[@]}"; do
    echo "── PgmStudio.$p.Tests"
    dotnet-coverage collect --output "$OUT/$p.cobertura.xml" --output-format cobertura \
      -- dotnet run --project "tests/PgmStudio.$p.Tests" --no-build \
      | grep -E '  (total|failed):' || true
  done
  dotnet-coverage merge --output "$MERGED" --output-format cobertura "$OUT"/*.cobertura.xml >/dev/null
fi

[[ -f $MERGED ]] || { echo "no merged report at $MERGED — run without --report first" >&2; exit 1; }

python3 - "$MERGED" <<'PY'
import sys, xml.etree.ElementTree as ET
from collections import defaultdict

# Only first-party production assemblies count: the merge also instruments linq2db, MySqlConnector and
# friends, which alone drag a first-party 79% down to a reported 28%.
keep = lambda a: a.startswith('PgmStudio.') and not a.endswith('.Tests')
asm = defaultdict(lambda: [0, 0])
files = defaultdict(lambda: [0, 0])

for pkg in ET.parse(sys.argv[1]).getroot().iter('package'):
    a = pkg.get('name') or '?'
    if not keep(a):
        continue
    for c in pkg.iter('class'):
        fn = c.get('filename') or '?'
        if 'src/' in fn:
            fn = fn[fn.index('src/') + 4:]
        if '/obj/' in fn:        # generated (regex source generators) — not authored code
            continue
        cov = tot = 0
        for ln in c.iter('line'):
            tot += 1
            cov += int(ln.get('hits') or 0) > 0
        asm[a][0] += cov;  asm[a][1] += tot
        files[fn][0] += cov; files[fn][1] += tot

pct = lambda c, t: f"{100.0*c/t:5.1f}%" if t else "  n/a"
print(f"\n{'ASSEMBLY':<28}{'COVERED':>9}{'LINES':>8}{'RATE':>8}")
print("-" * 53)
gc = gt = 0
for a, (c, t) in sorted(asm.items(), key=lambda kv: -kv[1][1]):
    gc += c; gt += t
    print(f"{a:<28}{c:>9}{t:>8}{pct(c,t):>8}")
print("-" * 53)
print(f"{'TOTAL':<28}{gc:>9}{gt:>8}{pct(gc,gt):>8}")
print("\nPgmStudio.Client is absent by design: no test project references it (C28).")

print(f"\n\nWORST FILES (>=50 uncovered lines)\n{'FILE':<58}{'UNCOV':>7}{'RATE':>8}")
print("-" * 73)
for fn, (c, t) in sorted(files.items(), key=lambda kv: kv[1][0] - kv[1][1]):
    if t - c < 50:
        break
    print(f"{fn[:57]:<58}{t-c:>7}{pct(c,t):>8}")
PY
