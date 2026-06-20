# Organic lane generation

The `Organic` sketch archetype grows a team's island **outward from a spawn hub** into a few **wool lanes**,
using a noise grid to place and bend them, and variable-width polygon hulls (with optional diamond holes) for
an organic, reusable-but-varied look. It is the lane-graph reading of real maps
(`docs/generator-archetypes.md`) turned into a generator: **a lane is one hub→tip branch — it can bend and
carry holes — ending in a dead-end wool tip; the spawn sits at the hub where the lanes meet.**

Code: `PgmStudio.Pgm.Sketch.OrganicLane` (the engine) + `LaneSketchGenerator.Organic` (wiring) +
`Geom.Rng` (seeded RNG) + `Geom.Lane.Ribbon` (variable-width strip). Preview it with no database:

```
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-preview Organic <seed> <out.json>
```

## The process (and where to tune it)

1. **Spawn hub.** Placed near the mid line, facing the foe (`hubZ = mid − 2·laneWidth`). Lanes grow *up/out*
   from here toward the far edge; a short **trunk** reaches toward the mid so the island is close enough for
   short Configure bridges. *Tune:* `hub`/`trunkTip` in `OrganicLane.Grow`.

2. **Far-spread wool tips (the noise grid).** Sample a grid over the far band (top ~24% of the team half),
   keep the cells whose value-noise (`NoiseField`, seeded) exceeds a threshold, then **farthest-point
   sample** `Wools` of them — each new tip maximises its min distance to the hub and the already-chosen tips,
   weighted by the noise. This spreads the wool dead-ends across the far edge. *Tune:* `FarthestTips` (band
   height `H*0.24`, `step`, threshold, the noise weight `0.5 + N`).

3. **Grow each lane (the bends).** For each tip, a centerline runs hub→tip through 1–2 **waypoints** offset
   perpendicular by the noise field (`±0.8·laneWidth`) — these are the organic bends — then Catmull-Rom
   smoothed. *Tune:* waypoint count (`rng.Int(1,3)`), offset scale.

4. **Variable-width ribbon (the hull).** The lane body is `Lane.Ribbon`: each centerline point is offset by a
   per-side half-width = base (tapering ~25% toward the tip) + independent noise jitter (`±0.3·laneWidth`),
   so the outline undulates instead of being a clean rectangle. *Tune:* taper, jitter scale.

5. **Diamond holes.** With probability `HoleChance`, a lane bulges around its midpoint and a rotated square is
   **subtracted**, leaving a diamond hole inside a widened section (the Green-Gem reading). The hole radius is
   a fraction of the bulged half-width so the lane stays connected around it. *Tune:* `HoleChance`, the bulge
   window/size, hole radius `0.55`.

6. **Mirror + assemble.** The grown unit is fanned to the opponent by `mirror_z` (`Assemble`), with no mid
   island (the contested centre is the bridged gap). Objective hints: **wool at each tip, spawn at the hub.**

## Guarantees
- **Deterministic:** same `Seed` → identical layout (so a good seed is keepable and iterable).
- **Wools at dead-end tips:** lanes only join at the hub, so every tip is a leaf; hints put a wool at each.
- **Spawn at the hub.**
- **Two separate, congruent team islands** (mirror), ready for `finish` (≥2 islands) → the existing pipeline
  (`LaneMapGenerator` adds monuments near the captor's spawn, `AutoBridge` connects the islands, the export
  gate checks the monument + traversability rules).

## Knobs (`LaneLayoutOptions`)
| field | default | effect |
|---|---|---|
| `Seed` | 1 | the noise/RNG seed — the whole layout |
| `Wools` | 2 | wool lanes per team (one dead-end tip each) |
| `LaneWidth` | 12 | base lane width; everything scales off it |
| `HoleChance` | 0.45 | per-lane probability of a diamond hole |
| `Width`/`Height` | auto → 120×150 | board size; Organic upsizes the 60×90 default so lanes read as distinct corridors |

## How to iterate
- Sweep seeds with `--gen-preview Organic <seed> out.json` + `gen_render.py` to eyeball variety.
- The geometry knobs above are all local to `OrganicLane`; start with hub/tip placement and the jitter/offset
  scales. Lower jitter + offset → cleaner, straighter lanes; higher → more organic (and more blob risk).

## Known limitations / next ideas
- Lanes are a **star from one hub** (no sub-branching); a lane that itself forks (a wool off a mid-lane
  junction) isn't modelled yet.
- Bends are free-angle; snapping waypoints to the 45°/octant family (from the primitive study) would match
  the corpus aesthetic more tightly.
- Holes are single diamonds; multi-hole "rotated rectangle with two holes" lanes (Green Gem) could chain
  bulges.
- Only `mirror_z` (two teams) so far; a rot_90 organic board would reuse the same growth per quadrant.
