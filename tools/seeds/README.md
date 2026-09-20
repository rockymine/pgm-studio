# Test seeds

Two different things live here. The three `base-*` **sketch layouts** below are fixtures for exercising the
studio end to end, especially the **sketch world-folder export**
(`docs/world-export/sketch-world-export.md`). Everything else is **plans**, and they are worked examples
rather than fixtures.

## The plans, which are where "how is a board actually stated" is answered

Forty-nine `*.plan.json` files: sixteen here, sixteen under `traced/` and seventeen under `teaching/`.

**Sixteen are real published maps traced into plan space** — `traced/acapulco`, `aether`, `ad-astra`,
`after-hours`, `3084` and the rest. They are the ground truth for what a real board's structure looks like
as pieces: how many, how large, how they connect, and — the part no generated board has reproduced —
**how their heights step**. `traced/bridgid-ii.plan.json` carries 36 pieces across sixteen height tiers
from 11 to 41.

**Height is stated per piece, and the global stays where it is.** Every seed leaves `globals.surface` at 9
and varies `PlanPiece.Surface` instead, which is what makes a step a step: `mirror-big-board` runs 39
pieces over tiers 11·13·15·17·19, and `mirror-tiny-map-cliff` puts its pieces at 3·5·7·11 — at and **below**
the global, which is how ground goes down rather than up. A relief dropped onto a board whose pieces all sit
at the default has nowhere to cut to.

**`teaching/` demonstrates structures rather than maps.** Seventeen plans built to show one thing each —
build interfaces, build regions, a crammed frontline, a middle void with and without steps, mid rotations —
with their own shopping list of what is covered. It is where to look for what a named structure is supposed
to look like before authoring one.

## The sketch fixtures

## Base 2-island map (`base-2island.*`)

The most basic CTW design: two team islands + a contested centre.

- **Geometry** (`base-2island.layout.json`) — a sketch layout, **1 raster cell = 5 blocks**, centred on
  `(0, 0)`, bbox ≈ **40 × 140** (X ∈ [-20, 20], Z ∈ [-70, 70]). One team unit is authored and **`rot_180`**
  mirrors it to the other side:
  - A **distorted-H team island** (polygon) — two vertical bars of different lengths (right X[5,15]·Z[25,55],
    left X[-15,-5]·Z[20,65]) joined by a crossbar (X[-5,5]·Z[35,45]). Terrain height **9**.
  - A small **square island** (X[5,15]·Z[5,15]), terrain height **13** (raised).
- **Intent** (`base-2island.intent.json`) — 2 teams (`red`/`blue`); red spawns at `(10, 50)` facing the
  enemy (−Z) and defends its wool at `(-10, 60)`, blue is the `rot_180` image. Observer floats dead-centre at
  **y = 24** (no sketch island — created purely from the intent). Max build height **20**; a central build
  area (X[-15,15]·Z[-25,25]) that spans the mid void + both squares and overlaps each H's inner end, so the
  spawn↔wool chain is bridgeable/traversable. Monuments are left empty
  — they're **auto-wired at export** (every non-owner team captures each wool).

### Run
Against a running studio (`./tools/dev.sh` on :7894, DB migrated):
```bash
./tools/seeds/seed.sh
# → prints the slug + GET .../export URL; download it for the {slug}/ world ZIP.
```

Run a different variant with `SEED`:
```bash
SEED=base-4team ./tools/seeds/seed.sh
```

## 4-team map (`base-4team.*`)

The same team unit (H + square), fanned by **`rot_90`** into a four-arm pinwheel around the centre. bbox
≈ **140 × 140** (square). Four teams (`red`/`blue`/`yellow`/`green`), one arm each; spawn/wool/build coords
are the base rotated 90° CCW per team (`(x,z) → (-z,x)`, matching `Geom.Symmetry`). Because each wool is
captured by the other three teams (auto-wired), every spawn cube holds **3 monuments** — the 3-monument
back-wall placement case. The build region is done "cheap" as **four explicit rects** (base `(-15,0)‥(15,25)`
rotated per arm) that overlap at the centre, so all four teams are connected (verified: 1 component). Observer
+ y=20 build cap unchanged.

## 2 teams × 2 wools (`base-2wool.*`)

`SEED=base-2wool ./tools/seeds/seed.sh`. Reuses the base distorted-**H** (spawn + wool1) and adds a small
**L-shaped wool island** for wool2, linked by a **bridge** build region — `rot_180`-mirrored → 2 teams each
defending **2 wools**. Red defends `red` (on the H) + `orange` (on the L island at `(40,30)`); blue is the
`rot_180` image (`blue` + `light_blue`). Bridge `X[15,25]·Z[35,45]` connects the H's right bar to the L island;
build regions = the central band + the two bridges. Each team captures the other's two wools, so every spawn
cube holds **2 monuments**. The two central **square islands** (from the base) are kept as contested terrain.
Observer y=24, y=20 build cap.
