# Map decoration — the idea pool (dressing-stage gaps)

The dressing stage (G161, `decoration.md`) shipped five tools as prototypes — flora, paths, boulders, trees,
water. Each works on its own. This file is the pool of what turns them from five soloists into one coherent
stage: the correctness constraints a competitive map cannot skip, the connective tissue between the passes,
and the axes the flat-plane prototypes never touch. One idea per few lines, grouped, **ids in the G track**
(continuing G157/G161), **preserved** — an id here is never reused; pull one onto `BACKLOG.md` when it
becomes the focus. Same discipline as `docs/generator/ideas.md`, for the world-export/dressing track.

Everything here is **designed, not built** — the prototype is `tools/decorate/prototype.html`, and the model
is `decoration.md`. Where an idea depends on a pass that does not exist yet (the elevation pass, G32-C), it
says so.

## Correctness — a competitive map cannot ship without these

- **G162 — true symmetry / fairness** *(priority)* — the dressing prototypes scatter freely; the studio fans
  **everything else** across the symmetry orbit (`Geom.Symmetry`, the orbit machinery in
  `docs/contracts/new-map-authoring.md`). For a competitive map that is a correctness bug, not a style choice:
  cover, sightlines and movement must be identical per team, or one spawn approach having a boulder for cover
  and a treeline blocking a sightline the mirror lacks decides fights unfairly. The fix is a
  **cosmetic-vs-gameplay split** — every prop declares whether it affects play (trees, boulders, water
  bodies, tall grass = collision / cover / vision) or is purely visual (flowers, lily pads, ground pattern,
  reeds). Gameplay-affecting dressing is generated on the **authored unit only** and re-fanned through
  `Geom.Symmetry` like the layout itself (the roughen pass G142 already states this "authored unit only,
  symmetry re-fans, placements pinned" architecture); the cosmetic layer may be placed freely per image so the
  two halves are not eerily identical. The hash-from-cell determinism makes the free layer reproducible without
  being mirrored. **This gates the rest**: no dressing ships for competitive use until it respects the orbit.
  *Prototyped* in `tools/decorate/prototype.html` §6 — free-scatter vs orbit-fanned tree/boulder cover on a
  **real** board (`Composer.ComposeStages`, 40 players, `rot_180`), with a fairness check that counts cover
  cells whose mirror image is bare: free scatter racks them up, the fanned pass is zero by construction, and
  cosmetic flowers stay free. The board is emitted by `tools/decorate/dress-map.cs`.

- **G167 — readability & playability budget** — nothing yet stops decoration from narrowing a corridor below
  the bridge-width minimum, walling a lane, burying a monument, or over-cluttering until the map is
  unreadable. The layout generator has a two-currency budget and the `BZ`/`LN`/traversability laws; dressing
  has neither. Add a **dressing budget** (density caps per region kind — a lane gets far less than a
  backfield) and a small set of `DR` guardrail rules checked against the same harness
  (`tools/PgmStudio.RoundTrip --traversability`): no prop reduces a corridor below its minimum, no
  gameplay-affecting prop inside a bridge / build zone, every objective keeps a clear framing radius.
  Restraint as a first-class control, not an afterthought.

## The missing dimension — vertical surfaces

- **G163 — vertical-surface dressing** — every tool but water assumes a flat top plane; a cliff or wall (the
  `EL` laws, G32-C) dresses completely differently. The vocabulary: moss and lichen creeping on stone faces
  (a pattern, shaded / `PerimeterArc`-aware), vines and hanging foliage dripping from ledges and overhangs
  (the tree's hanging-strand idea turned vertical), the occasional ledge shrub or exposed root, and
  **scree / rubble** — the boulder scatter shed at a cliff base. It attaches to **faces and interfaces**, not
  the top grid, so it needs the elevation pass (G32-C) to exist first — the same dependency water has for its
  carved bed.

## The connective tissue — composition

- **G164 — the arbitration & order contract** — the five passes are demos in isolation; the missing piece is
  the score: the run-order and the masks each pass hands the next. Water carves first and owns its footprint;
  paths clear flora along their band; a dense canopy **suppresses** tall grass beneath it and seeds
  shade-ferns instead; reeds are water-edge flora, not a separate system; every pass yields to objectives,
  spawns and the minimum corridor. Written as an explicit pass order plus an **exclusion / affinity mask**
  protocol (each pass reads the accumulated mask, writes its own), it is the difference between five features
  and one stage. Belongs in `decoration.md` as the composition contract more than as a figure.

- **G165 — dressing theme / biome driver** — each tool picks materials and species by hand, so nothing
  prevents snow ground under oak trees beside liquid (un-frozen) water. One **biome / season selector** —
  temperate, desert, snow, autumn, swamp — should harmonize all five DR-* palettes at once (grass →
  dead-bush / cactus, water → ice, oak → spruce, sand → snow, and so on), riding the terrain-paint theme
  scopes (TP10) that already resolve a theme per piece. The natural home is the same `TerrainThemeScope`
  per-cell resolution the painter uses, extended from materials to the dressing palette and species tables.

## Placement intelligence — the "grew vs scattered" gap

- **G166 — context-aware placement (the affinity model)** — placement today is context-free blue-noise plus a
  density field; a believable map keys placement on **context**. Give each prop an **affinity** over derivable
  fields — slope, height / elevation, distance-to-water, border proximity, canopy cover, distance-to-lane — so
  trees thin along lanes and thicken at the edges, boulders follow cliff bases and ridgelines, water settles
  in the lowest ground, flowers avoid shade, moss prefers shaded faces. This is the jump from "scattered" to
  "grew", and the fields it needs (slope, height, distance transforms) are cheap derivations off the
  `SurfaceTop` grid and the plan's regions. The single biggest quality lever left after G162.

## Framing & edges

- **G168 — border & POI framing** — two specific jobs the tools do not name. The **void edge / map border**
  should read as a boundary — a treeline wall, a cliff or beach at the void — so players read the play-space
  limit without an invisible wall (a readability concern PGM maps already solve with terrain). And dressing
  should **frame** the important places rather than bury them: a clearing around a monument, flowers marking a
  spawn, a path leading to a wool room — POIs as things the dressing emphasizes, not merely exclusion holes.
  Both lean on the affinity fields of G166 (distance-to-border, distance-to-objective).
