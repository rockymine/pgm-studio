# Organic lane generation

The `Organic` sketch archetype grows a team's island **outward from a spawn hub** into a few **wool lanes**,
using a noise grid to place and bend them, and variable-width polygon hulls (with optional diamond holes) for
an organic, reusable-but-varied look. It is the lane-graph reading of real maps
(`docs/generator-archetypes.md`) turned into a generator: **a lane is one hub→tip branch — it can bend and
carry holes — ending in a dead-end wool tip; the spawn sits on its own short spur off the hub** (see
*Playability* below for why the spawn is off the junction, not on it).

**The shape recipe in one line:** drop a near-circular **hub blob** (a 12-gon plaza), radiate a set of
**angle-constrained, variable-width ribbon polygons** out of its centre (mid trunks → bridges, wool lanes →
dead-end tips, one spawn spur), **subtract** diamond holes, **union** the lot into one team's island, then
**`mirror_z`** it to the opponent. Everything below is how each of those pieces is sized and placed — and it
all comes out as a normal `SketchLayout` (the same polygon model a hand-drawn sketch produces); the rasterizer
turns the set-algebra into terrain. Dump the raw polygons with `--gen-sketch Organic <seed> <out.json>`.

Code: `PgmStudio.Pgm.Sketch.OrganicLane` (the engine) + `LaneSketchGenerator.Organic` (wiring) +
`Geom.Rng` (seeded RNG) + `Geom.Lane.Ribbon` (variable-width strip). Preview it with no database:

```
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-preview Organic <seed> <out.json>
```

## The process (and where to tune it)

1. **Spawn hub + plaza.** Placed above the mid line, facing the foe (`hubZ = mid − 2·laneWidth`). All branches
   meet here, so the hub is widened into a small **plaza** blob (radius `1.1·laneWidth`): branches fan out of an
   *area*, not a point — a point hub pinches thin land wedges between adjacent branches and leaves no room.
   *Tune:* `hub`, the plaza radius in `OrganicLane.Grow`.

1b. **Mid trunks → bridges.** `MidBranches` short trunks reach from the hub toward mid, stopping `VoidDistance/2`
   short of it. Two well-separated trunks (the default) give **two crossings** of the void — two angles of
   attack — instead of one chokepoint. Each trunk tip becomes a `"bridge"` hint that `LaneMapGenerator` spans
   across the mid line to its mirror (so AutoBridge's single-edge MST isn't relied on). *Tune:* `MidBranches`,
   `VoidDistance`, the trunk x-spread (`±1.2·laneWidth`).

2. **Far-spread wool tips (the noise grid).** Sample a grid over the far band (top ~24% of the team half),
   keep the cells whose value-noise (`NoiseField`, seeded) exceeds a threshold, then **farthest-point
   sample** `Wools` of them — each new tip maximises its min distance to the hub and the already-chosen tips,
   weighted by the noise, **and is kept ≥ `MinHubAngle` from every other tip and the trunks** (the *hub-fan
   minimum*: no two branches fan out tightly enough to leave a thin land sliver between them; the constraint
   relaxes only if no candidate qualifies). *Tune:* `FarthestTips` band/step/threshold, `MinHubAngle`.

3. **Grow each lane (the bends).** For each tip, a centerline runs hub→tip through 1–2 **waypoints** offset
   perpendicular by the noise field (`±0.55·laneWidth`) — these are the organic bends — then Catmull-Rom
   smoothed. The offset is kept gentle: a sharper fold pinches the lane's inner edge to a thin sliver.
   *Tune:* waypoint count (`rng.Int(1,3)`), offset scale.

4. **Variable-width ribbon (the hull).** The lane body is `Lane.Ribbon`: each centerline point is offset by a
   per-side half-width = base (tapering ~25% toward the tip) + independent noise jitter (`±0.3·laneWidth`),
   so the outline undulates instead of being a clean rectangle. *Tune:* taper, jitter scale.

5. **Diamond holes.** With probability `HoleChance`, a lane carries a rotated square **subtracted** as a
   diamond hole (the Green-Gem reading). The hole half-diagonal is `0.35–0.55·laneWidth`, and the ribbon is
   **bulged so that a path of at least 0.7·laneWidth remains on each side** of the hole (the corpus keeps
   holes inside wide lanes, never as thin necks). *Tune:* `HoleChance`, hole-size range, the `0.7·lw` margin.

6. **Spawn spur (playability).** The spawn does **not** sit on the hub junction. `SpawnSpur` finds the widest
   angular gap among the trunk + wool directions and, preferring a gap that points **away from the mid line**
   (not toward the bridge), places the spawn out along it on its own spur (length `laneWidth·2.3`). Its
   protection then sits in a side/back pocket well off the crossing, so an attacker reaching the hub fans out
   to the wools with room rather than squeezing one corridor past the spawn. *Tune:* spur length (`laneWidth·2.3`),
   spur width (`laneWidth·0.9`).

7. **Inset objectives (cover).** The wool and spawn objective points are **not** the geometric lane/spur tips
   (which sit on the boundary with ~1 block of clearance). Each is inset ≈ `0.5·laneWidth` back along the
   centerline into the lane body, so it carries ~half a lane width of cover; the lane still caps beyond it as a
   backing wall. *Tune:* the `0.5·lw` inset in `InsetAlong`.

8. **Mirror + assemble.** The grown unit is fanned to the opponent by `mirror_z` (`Assemble`), with no mid
   island (the contested centre is the bridged gap). Objective hints: **wool near each tip, spawn on the spur.**

## Playability (spawn protection vs. the bridge)

Each spawn carries a **spawn-protection** region (`<apply enter="deny(enemy)" …>`) the enemy cannot path
through. If the spawn sat on the hub — the single junction where the bridge/trunk meets the wool lanes — its
protection would wall the only way across, so an attacker crossing the bridge could never reach the wool
without entering the enemy spawn: **unplayable.** Putting the spawn on a spur in the widest gap moves the
protection *off* the junction, leaving the hub→wool-lane flow open: an attacker crosses the bridge, reaches
the hub, and fans out to the dead-end wools **around** the spawn — the corpus pattern (Kanto, Annealing IV).
Validate with the `--gen-map-preview` tool (emits spawns+protection, wools+rooms, bridges) and a
protection-aware BFS from each captor's spawn to the enemy wool with the defender's protection removed.

## Guarantees
- **Deterministic:** same `Seed` → identical layout (so a good seed is keepable and iterable).
- **Wools at dead-end tips:** lanes only join at the hub, so every tip is a leaf; hints put a wool near each.
- **Spawn on a spur off the hub** (widest angular gap), so its protection never walls the hub→wool flow.
- **Objectives carry cover:** wools/spawn are inset ≈ half a lane width off the tips (clearance ~½·laneWidth,
  not the boundary), and hole-side paths stay ≥ 0.7·laneWidth — everything scales off `LaneWidth`.
- **Two separate, congruent team islands** (mirror), ready for `finish` (≥2 islands) → the existing pipeline
  (`LaneMapGenerator` adds monuments near the captor's spawn, `AutoBridge` connects the islands, the export
  gate checks the monument + traversability rules).

## Knobs (`LaneLayoutOptions`)
| field | default | effect |
|---|---|---|
| `Seed` | 1 | the noise/RNG seed — the whole layout |
| `Wools` | 2 | wool lanes per team (one dead-end tip each) |
| `LaneWidth` | 12 | base lane width; **everything scales off it** (inset = ½·lw, hole-side path ≥ 0.7·lw, spur = 2.3·lw, hub plaza = 1.1·lw) |
| `HoleChance` | 0.45 | per-lane probability of a diamond hole (held inside a wide section) |
| `MidBranches` | 2 | near-mid trunk branches → bridges (two = two crossings / angles of attack) |
| `VoidDistance` | 16 | the void gap between the two team islands at mid — the span each bridge crosses |
| `MinHubAngle` | 35 | min degrees between branches at the hub (no thin land slivers between lanes) |
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
