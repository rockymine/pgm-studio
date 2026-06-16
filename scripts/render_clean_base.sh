#!/usr/bin/env bash
# A5 render-comparison pass — render the cleaned-base island outlines (ND2 §6a: noise-excluded base +
# height-aware connectivity that prunes floating builds over void) for a few real maps, to eyeball that
# the detection reads cleanly. Runs the REAL C# pipeline (LayerExtractors.CleanBase → IslandDetector.
# DetectCleaned) via the RoundTrip harness's --clean-base-render mode, writing one SVG per map.
#
# Maps: by Ruediger_LP (pigland, ruedigers_octawool) and rockymine (annealing_iv, agrostid), plus
# mame_i_shrunk_the_pvpers — the floating-eagles + water-bridge case from the ND2 analysis.
#
# Usage:  scripts/render_clean_base.sh
set -euo pipefail
cd "$(dirname "$0")/.."

ROOTS=(/media/sf_repos/CommunityMaps/ctw /media/sf_repos/PublicMaps/ctw)
OUT=scripts/clean-base-renders
MAPS=(mame_i_shrunk_the_pvpers pigland ruedigers_octawool annealing_iv agrostid)

mkdir -p "$OUT"
echo "building harness…"
dotnet build tools/PgmStudio.RoundTrip -c Release -v q >/dev/null

for m in "${MAPS[@]}"; do
  region=""
  for r in "${ROOTS[@]}"; do
    [ -d "$r/$m/region" ] && { region="$r/$m/region"; break; }
  done
  if [ -z "$region" ]; then echo "SKIP $m — no region dir under ${ROOTS[*]}"; continue; fi
  dotnet run --project tools/PgmStudio.RoundTrip -c Release --no-build -- \
    --clean-base-render "$region" "$OUT/$m.svg"
done

echo "done → $OUT/"
