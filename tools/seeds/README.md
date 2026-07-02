# Test seeds

Reusable sketch maps for exercising the studio end-to-end — especially the **sketch world-folder
export** (`docs/contracts/sketch-world-export.md`).

## Base 2-island map (`base-2island.*`)

The most basic CTW design: two team islands + a contested centre.

- **Geometry** (`base-2island.layout.json`) — a sketch layout, **1 raster cell = 5 blocks**, centred on
  `(0, 0)`, bbox **140 × 30** (X ∈ [-70, 70], Z ∈ [-15, 15]):
  - A **distorted-H team island** on the left — a long top arm (Z 5..15), a shorter bottom arm (Z -15..-5),
    and a vertical spine joining them — mirrored by **`rot_180`** into the right island. Terrain height **9**.
  - A **central square island** (X/Z ∈ [-10, 10]), terrain height **13** (raised).
- **Intent** (`base-2island.intent.json`) — 2 teams (`red`/`blue`); red spawns top-left `(-60, 10)` facing
  the centre and defends its wool at `(-60, -10)`, blue is the `rot_180` image. Observer floats dead-centre
  at **y = 24**; max build height **20**; a full-bbox build area (so the void gaps between islands are
  bridgeable and the map is traversable). Monuments are left empty — they're **auto-wired at export** (every
  non-owner team captures each wool).

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
