# Test seeds

Reusable sketch maps for exercising the studio end-to-end — especially the **sketch world-folder
export** (`docs/contracts/sketch-world-export.md`).

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
  **y = 24** (no sketch island — created purely from the intent). Max build height **20**; a full-bbox build
  area (so the void gaps between islands are bridgeable and the map is traversable). Monuments are left empty
  — they're **auto-wired at export** (every non-owner team captures each wool).

### Run
Against a running studio (`./tools/dev.sh` on :7894, DB migrated):
```bash
./tools/seeds/seed.sh
# → prints the slug + GET .../export URL; download it for the {slug}/ world ZIP.
```

## Variants (same base, different symmetry)

The base is deliberately a single orbit unit + a centre-symmetric square, so it doubles as the seed for:

- **4-team (`rot_90`)** — reframe to a square bbox and set the layout `mirror_mode` to `rot_90`: the team
  island fans to 4 sides around the central square → each team captures **3 wool monuments** (exercises the
  3–4-wool back-wall monument placement).
- **2 wools each (mirror along the spawn axis)** — mirror the layout to the right to form an **H with a
  centre spine**, giving the common **2 teams × 2 wools** setup.

Both need a matching intent (more teams / wools); the base files are the starting point.
