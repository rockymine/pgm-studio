# Recovered footprints + traffic ground truth (G33)

Pairs of a **recovered plan** (the author's cleaned trace of a real map's footprint — structure
only: pieces/zones/markers, no elevation) and its **traffic graph** (player positions from real
matches, aggregated on a 3-block grid by the CTWAnalysis `match_analysis` suite; nodes carry
occupation / terrain-island id / POIs, edges carry movement transitions).

Kept **separate from `tools/seeds/`** deliberately: seeds are authored intent (the rule corpus,
frozen); these are ground-truth pairs for validating derived structure and, later, scoring
composer candidates (flow priors). Only aggregated graph JSONs live here — never raw match data.

## ingwaz (105 matches · 510 players · 17.7h · grid 3)

Alignment plan↔traffic (spawn-anchored): centre (−10.5, 46.5), scale plan/real = **1.111** (cell-5
quantization of a non-gridded build). Correspondence: **23/23** hot void cells (occupation ≥ 30,
no island underneath) inside the fanned build zones; **171/171** land nodes inside the fanned
pieces; **6 = 6** islands; real wools at exactly the plan wool-piece centres (−10, 50)/(10, −50)
in plan coordinates; G8 predicts 10–12 players/team from its 950 land/team.

## Logs-only reconstruction (verdict: viable — no footprint needed)

Investigated on the ingwaz raw logs (143 parquet files, 27,635 events; schema: timestamp s ·
event_type · player · x/y/z · held/inv · wool_id; codes inferred from data alone — 0/1 match
markers, 2 spawn (y=4), 3 kill, 4 death, 5 position @2s, 6 wool touch, 7 capture (y=5)):

- **Occupancy + edges**: positions (type 5, y ≥ 2) — trivially reproducible.
- **POIs**: spawns = type-2 clusters (match the graph POIs to the block); wool rooms = type-6
  clusters; capture points = type-7 clusters (sit beside the owning team's spawn).
- **Void / build regions**: the **fall-share** signal — sub-zero-y samples (mid-fall positions +
  deaths) relative to standing traffic per cell, **symmetry-augmented** (centre derived from the
  spawn clusters; each cell pooled with its rot_180 image). Deaths alone (the original idea)
  reach only R=0.43 — the 2s position sampler catches fallers mid-drop and lifts recall to
  **1.00** at share ≥ 0.08 (P 0.39) / **R 0.86, P 0.52** at ≥ 0.12. Errors are boundary-band:
  the false positives are island **rim cells** (a 3-block cell straddling the void lip collects
  falls), 15/22 directly adjacent to true void; all 4 false negatives adjacent to predicted void.
- **Islands**: connected components of traffic-minus-void = **6/6**, matching the oracle
  partition (sizes 55/54/14/12/9/9 vs 60/54/18/16/14/9 — rims eaten by the dilated void mask).

Net: the traffic graph **can be built from the logs alone** — the external footprint extraction
(the hard part of the original analysis; G9/G12-class work here) is not required. Void precision
is grid-resolution-limited (±1 cell at grid 3); the known fix is classifying falls at block
resolution before aggregating (kills the rim aliasing) — for the local session, along with
multi-map validation.
