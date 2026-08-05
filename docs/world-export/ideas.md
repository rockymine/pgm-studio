# Map decoration — the idea pool (dressing-stage gaps)

The dressing stage (G161, `decoration.md`) ships four tools — paths, ground cover, boulders and trees — each
*placed* on the sketch tool's Dressing canvas and run over the realized world. This file is the pool of what
turns them from four soloists into one coherent stage: the connective tissue between the passes, the axes
the flat-plane model never touches, and the fifth tool. One idea per few lines, grouped, **ids in the G
track** (continuing G157/G161), **preserved** — an id here is never reused; pull one onto `BACKLOG.md` when
it becomes the focus. Same discipline as `docs/generator/ideas.md`, for the world-export/dressing track.

Everything here is **designed, not built** — the prototype is `tools/decorate/prototype.html`, and the model
is `decoration.md`. Where an idea depends on a pass that does not exist yet (the elevation pass, G32-C), it
says so.

## Correctness — a competitive map cannot ship without these

- **G162 — fairness at the symmetry axis** — the fan shipped with G161 (`decoration.md` §2): every prop is
  placed once and stamped at each image of its orbit, turned, so a prop is identical per team by construction
  and the canvas shows the images as ghosts while placing. What it does not yet handle is the **axis itself**: a prop whose orbit images overlap — a
  boulder sitting on or within a radius of the mirror line — is stamped twice into the same cells, so the two
  copies fuse into one shape that is neither's. It reads as a lump, and on a `rot_180` map it is exactly
  where the centre objective usually sits. Three candidate answers: refuse a representative whose prop
  bounds cross the axis, place one prop *on* the axis built symmetric in its own frame, or merge the images
  and accept the fused mass as intentional. Cheap to detect (the prop's own bounds against the axis), and
  cheap to test — the fairness check that counts unmirrored cover cells already exists.

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

- **G166 — context-aware placement (the affinity model)** — placement today is by hand, which is right for the
  props that decide a fight and tedious for the ones that only fill a hillside. What is missing is the
  *optional* half: a brush that scatters cosmetic dressing over an area by affinity rather than one at a time.
  The original text below still describes what that affinity model would read; the blue-noise scatter it needs
  is already in `Geom` and unused. Placement was context-free blue-noise plus a
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

## The fifth tool

- **G169 — water: the richer reads and ponds (`DR-WA`)** — the **channel** shipped (the fifth tool: a
  dragged centerline that cuts a U-bed and fills it to a level line, three forms, `Geom.WaterBed` +
  `Decorator.PlaceWater`; see `decoration.md` §7 and `FEATURES.md`). What remains is the read that takes a
  channel from "a filled cut" to "water that looks like water," and the closed form. **Depth shading** warped
  off-centre so one bank runs deeper than the other; an **irregular shoreline** whose width wanders to zero in
  places; a **voronoi-patterned** bed and shore (sand, pale gravel, coarse dirt) showing through the shallows;
  **edge life** reusing the §3 flora overlay masked to the bank (reeds, lily pads); and **ponds** — the closed
  version, the §5 blob read concave with an FBM-wandered outline, scattered onto low ground and joined to
  channels into one watershed. The bed carve reads best once the **G32-C** elevation pass gives layouts their
  heights, so a channel becomes a cut valley rather than a trench in a flat.
