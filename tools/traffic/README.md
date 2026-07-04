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
