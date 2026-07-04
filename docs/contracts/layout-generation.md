# Layout generation — piece-and-interface plans, rule-based composition

Direction doc for the `G`-series: how full map-layout generation becomes possible. Grounded in the
three test seeds (`tools/seeds/`), the sketch/authoring tool vocabulary, and the author's design
experience — which is the source of the rules. The core claim: **separate the map's topology (the
plan) from its geometry (the realization)**, with the plan expressed as **areal pieces joined by
edge-interval interfaces** — never as a thinned skeleton — and grow generation rule-based from the
deliberately boring rectilinear base the seeds establish.

## 1. Ground rules (decided)

- **The corpus is not an oracle.** The extraction pipeline flattens maps to 2D and loses exactly
  what made them read (spawn structures, wool rooms, terrain, decoration); the decompose tool only
  ever worked on the rectilinear cases; and map quality across the ~350 maps is too mixed to treat
  the distribution as a target. Corpus material is reference imagery at most. The rule set is
  **expert-authored** — encoded from the author's map-making experience, not mined.
- **No skeletons.** A medial-axis / thinned-line representation fails twice. Representationally: a
  build region touching the long side of a wide rectangle projects onto *no* node and no labelable
  skeleton edge — side contact is an interval property that a 1D spine cannot carry. Generatively:
  a system that thinks in spines-with-ribbons is structurally biased toward thin maps, because
  width is only ever a decoration on a line. The plan below is areal by construction.
- **Start from the boring case.** The seeds — rectilinear, grid-aligned, minimal — are the real
  starting point. They already encode the load-bearing relations (spawn/wool lane separation,
  contested raised centre, isolation via a bridge-only connection, symmetry fanning one authored
  unit). Generation grows outward from that base by rules; the organic look is a later,
  separate pass.

## 2. The plan: pieces and interfaces

A small, explicit, persisted artifact. Globals + one per-team unit + the neutral middle:

- **Globals:** symmetry mode, team count, board bbox, coarse grid cell (the seeds' 5-block raster
  cell), base corridor width.
- **Piece** — a rectangle on the coarse grid (polygons later), with:
  - a **role**: `spawn`, `hub`, `lane`, `wool-room`, `mid`, `frontline`;
  - a **plateau height** (its floor level);
  - optional **placements**: objective anchors (spawn point, wool, monuments) as piece-local
    offsets — content is pinned to its piece, so later geometry warps around it.
- **Interface** — a shared **edge interval** between two pieces (or between a piece and a build
  zone). Not a point, not a node: it has a position *and a width*. Kinds:
  - `land` — the pieces merge into one walkable landmass;
  - `gap` — void between the pieces; a build region spans it, with a span distance (the bridge
    length). A piece whose interface set contains **no** `land` entries is *isolated* — reachable
    only by building. That single rule expresses every isolation motif: isolated wool room,
    isolated spawn, an isolated frontline piece fed by two bridges.
  - an elevation transition rides on any interface: `step` (walk up), `ramp` (gradual), `cliff`
    (one-way drop — a deliberate flow device).
- **Build zones** are pieces of a special kind (`buildable void`): the open-mid band is one wide
  zone touching many pieces along long intervals; a directed bridge is a narrow zone touching two.

**Why interfaces carry the design.** The width of an interface is the width of the corridor or
front it creates: a 40-block `gap` interface along a rectangle's whole side is an open front; a
5-block one is a chokepoint. *Player-flow control is the distribution of interface widths along
the attack paths.* The old "directed-flow vs open-mid" axis stops being a category and becomes
geometry.

**Plan invariants** (checkable with zero geometry): every wool reachable from every capturing
team's spawn across `land`+`gap` interfaces; no wool path passes through a `spawn` piece; ≥1 `gap`
on every inter-team path; interface widths ≥ the corridor minimum; spawn depth ≥ some distance
from the nearest frontline interface. The export pipeline's traversability/monument gates stay as
the backstop, but bad topology is rejected at the plan in milliseconds.

**The plan is a mini layout (a scale proxy).** Map authors already draw exactly this artifact by
hand: a checkered-paper (or one-block-per-cell in-game) miniature whose scale lives in the
author's head and gets adjusted when the real map is built. The plan adopts that semantics:
coordinates are **proxy cells, not block-true dimensions**. Realization applies scale — and the
grid's inherent smell (some distances inexpressible, everything reading "artificial") is resolved
downstream, by the scale pass together with roughen. Open design point: scaling cannot be one
uniform factor (that preserves the grid smell) — it likely needs per-part stretch (lengthening a
specific lane, widening a specific mid) applied to targeted pieces, which makes it a sibling of
the roughen operators rather than a global multiply. See §7.

**The seeds are already plans.** `base-2island`'s H = three rect pieces (two bars + crossbar) with
two `land` interfaces; the raised square = a `mid` piece whose every interface is a `gap`;
`base-2wool`'s L-island = a `wool-room` piece with exactly one interface, a narrow `gap` (the
bridge). Re-expressing the three seeds as plans is the schema's first test.

### Coexistence with the sketch and intent models

The plan is **not** a merge of sketch and intent — it sits upstream and **compiles into both**,
the same meaning→structure move the intent model makes for `map.xml`, applied one level up:

```
plan.json ──compile──► layout.json (SketchLayout) ──rasterize──► world
        └──compile──► intent.json (MapIntent)    ──generate───► map.xml
```

The three artifacts hold disjoint information, each with exactly one consumer:

- **Sketch** (read by the rasterizer) — realized geometry: exact polygon vertices, bézier
  controls, per-anchor heights, layers. After the roughen pass the plan's clean rects and the
  sketch's distorted polygons *deliberately disagree*; the sketch is the truth about shape.
- **Intent** (read by the XML generator) — concrete objectives: block coordinates, yaws, wool
  colours, monument wiring, build rects, meta.
- **Plan** (read by the composer/validator) — the meaning neither holds today: roles, interfaces,
  isolation, elevation transitions. Nothing in a `layout.json`+`intent.json` pair records that a
  wool island connects *only* via its bridge — the correlation is implicit in coordinates. The
  plan states it.

The compile is deterministic: pieces → `SketchShape`s with `floor`/`base_height` (land-connected
components → `SketchIsland`s, mirrors from the symmetry globals); placements → intent spawns/wools
(piece origin + offset, y from the plateau); `gap` interfaces + build zones → `build.areas`;
`ramp` transitions → `anchor_heights`. The seeds' `*.layout.json` + `*.intent.json` pairs become
derived outputs of a `*.plan.json` — compiling the seed plans must reproduce today's files (the
regression anchor).

**Sync is one-way, with a detach point.** While the staged loop runs (edit plan → re-compile →
re-roll roughen/elevation), the plan is authoritative and sketch/intent are regenerated outputs.
Once the author takes the sketch into the editor for hand work, the plan freezes as provenance and
sketch+intent become the working artifacts — the existing flow from there on. Bidirectional sync
(recovering plan meaning from edited geometry) is the abandoned extraction problem in disguise and
is explicitly out of scope; at most, individual plan-aware edit ops (cut+raise ⇒ refine-piece) may
keep the link later. Persistence: a third `map_artifact` blob beside `SketchLayoutJson` and the
intent.

## 3. Generation: rule-based composition on the coarse grid

1. **Globals** — symmetry, teams, board, grid cell.
2. **Grow the team unit** by attachment rules: place the `spawn` piece; attach the `hub`; grow k
   `lane` pieces, each dead-ending in a `wool-room`; attach `frontline` pieces toward the middle.
   Each rule states which roles may attach to which, on which sides, with what interface width and
   what depth ordering (wool behind hub, spawn off the wool paths, frontline nearest mid).
3. **Fragmentation moves — compose the closure, carve the mid, cut the team sides**
   (layout-rules.md CT1–CT6). The composer's natural order is the author's gesture run forward:
   compose the map as **one connected mass** (the closure), assign the team sides (the islands
   holding spawn + wools plus their minimal connectors; the rest is mid by centre-proximity —
   CT2), then fragment each regime with its own operator. The **mid is carved** into one of its
   interface forms — clean (one connected region, 0..n mid islands), parallel approaches, or the
   hash `#` (fractured/holed region with interconnected mid islands) per CT1; the **team sides
   are cut** — secondary isolation cuts sever wool or spawn behind a bridge (CT5) — keeping G5's
   hop numbers per crossing and leaving fragments standing as stepping stones (CT4's gradient:
   islands grow outward, mid stones thin 17/4/0 toward the team side; stones grid-aligned with
   the team islands per CT7; encased pads between a team's own islands are transient-links, not
   mid). The traversability gate is exactly the check that fragmentation never disconnects the
   closure. Purpose is gameplay, not looks — harder/riskier objective access,
   defenders slowed, retreat over fragile player-made bridges.
4. **Neutral middle** — place `mid` pieces and build zones between the frontlines.
5. **Validate** the plan invariants; reject/repair.
6. **Heights** — per-role plateau defaults (spawn raised for overview, wool approach stepping up
   toward the room, frontline lowest so the interior overlooks the crossing), transitions chosen
   per interface.
7. **Fan by symmetry, emit** `SketchLayout` + `MapIntent` — the existing seed pipeline
   (rasterizer, auto-wired monuments, spawn cubes, wool cages, export) unchanged.
8. **Roughen pass** (separate, last) — see §4.

**Unfolding (exploratory).** Treat a team's side as one connected rectilinear piece — build zones
count as tiles alongside terrain — then *unfold* it: straighten every L corner, so the unfolded
picture keeps only T and `+` intersections and holes `[]`. Well-defined because plans are
rectilinear; valid only when the unfold does not self-overlap. As analysis it exposes the
twisting of lanes and where the cuts sit on the straightened routes, and lets maps be compared
for how nested and connected they are; run backwards it is a composer strategy — assemble an
unfolded shape from tiles, cut it, re-fold (twist) at chosen corners, add elevation. Prototype
tracked as `G30`.

Mutation operators fall out of the same vocabulary, at two levels: *plan operators* change what
the map is (add a wool branch, isolate a piece, widen/narrow an interface, re-side the spawn,
raise a plateau); *distortion operators* change how it reads. Each is small, rule-checked, and
leaves a valid plan — so "mutating default seeds" is the generator's inner loop, not a rival
approach.

**Why not WFC.** WFC solves local-adjacency plausibility on a grid; CTW quality is global and
relational (symmetry, spawn/wool separation, isolation, interface-width flow control), which WFC
can only enforce by post-hoc rejection — and its native output is exactly the tile-grid look the
roughen pass exists to escape. Possible later niche: intra-piece block-level detailing once a
layout exists. Not the skeleton of the system.

## Ground truth: recorded player traffic (evaluation instrument)

A month of server logging (the pgmlogger plugin → parquet) yields per-map **traffic graphs**:
player positions every 2s across hundreds of matches, aggregated on a 3-block grid — nodes carry
occupation, the terrain island under them, and POIs (spawns, wools); edges carry movement
transitions (e.g. ingwaz: 105 matches, 510 players, 17.7h, 199 nodes). **The input is just a zip
of the raw log files per map** — the graph, including the land/void split and the island
partition, derives from the logs alone (validated on ingwaz: islands 6/6, void recall 1.0; the
fall-share method, both formats, and the event-code table are specified in
`docs/contracts/traffic-ground-truth.md` — the original analysis project is not needed). Two
readings matter for generation:

- **The closure, photographed.** Nodes with **no island under them** but high occupation are
  players standing in the void — the map's build regions *emerging from behavior alone* (ingwaz:
  23 hot void cells). A real map's traffic graph is its closure plus usage weights: the land and
  the zones being re-bridged live, match after match, exactly where the author cut. Recovered
  footprints (land + emergent zones) become CT test articles — real plans without hand-tracing
  (their elevation data stays polluted by buildings/organics; the 2-D structure is exactly what
  the plan model captures, and elevation stays expert-authored anyway).
- **Flow priors for the composer.** A handful of per-map scalars — occupancy split across the
  mid/team thirds, approach usage shares, void-vs-land occupancy, the kill/death frontline band —
  to score composer candidates against how players actually flow.

Boundary (deliberate): only per-map log zips, `traffic_graph.json` files, and derived priors
ever enter this repo — no per-match analytics, no identities beyond the logs' anonymous ids. An
instrument for map generation, not a match-analysis revival. Tracked as `G33`; the author
supplies the zips (uploaded per map like ingwaz's, or batch-collected from his archive in a
local session — the only remaining reason for one).

**First test article (validated).** The author's cleaned ingwaz trace + its traffic graph live in
`tools/traffic/` (see its README). Spawn-anchored alignment gives scale plan/real = **1.111**
(cell-5 quantization of a non-gridded build), and the correspondence is essentially exact:
**23/23** hot void cells inside the fanned build zones, **171/171** land nodes inside the fanned
pieces, **6 = 6** islands, real wools at the plan wool-piece centres to the block, and G8
predicting 10–12 players/team from its 950 land/team. The plan model, a real map, and real
player behavior agree — the recovered-footprint pipeline is sound.

## 4. The roughen pass: rectilinear plan → organic read

Applied to the realized polygons of the authored unit only (symmetry re-fans the images); plan
meaning is frozen, objective placements are pinned. Operators:

- **anchor jitter** — displace existing vertices by bounded noise;
- **edge subdivision + displacement** — insert mid-edge anchors, push along the edge normal (1–2
  fractal levels): organic outlines, zero topology change;
- **pull-to-polygon** — one strong anchor displacement that breaks a rectangle into a believable
  quad (the "twist");
- **width profile** — vary a lane piece's width along its length (thin necks, wide rooms);
- **45° chamfer** — soften right-angle corners into diagonal pairs;
- **piece shear/rotate** — a few degrees around the centroid.

Invariants per operator: minimum corridor width preserved (offset test), no self-intersection,
placements stay interior with margin, `gap` interface spans stay within the bridgeable range,
interfaces stay covered (distorted neighbours still overlap their shared interval). Output is
ordinary `SketchShape`s — every intermediate stays hand-editable in the sketch editor.

## 5. Elevation

Vocabulary from tools that already ship: per-shape `floor`/`base_height` (plateaus), splitting a
shape along a seam and offsetting the piece (**cut + raise** — at plan level this is refining one
piece into two joined by a `land` interface with a height delta), `anchor_heights` gradients
(ramps), stacked layers. Rules attach elevation to roles and interfaces, not to geometry: raised
spawn (overview), stepped approach toward a wool room (harder push), low frontline (bridges launch
low, defenders hold high ground), `cliff` interfaces where one-way flow is wanted. Constraint:
walkable steps along any `land` path unless the plan says `cliff`.

## 6. Scope and division of labour

"Making a boring layout interesting" is a **generator stage, not user work**: the roughen pass and
the elevation rules run before the author ever sees an output (and re-roll cheaply), so what the
staged loop presents already reads organic. The author's share is three smaller things:

1. **Rule authorship** (mostly one-time) — the per-role checklist: attachment rules, widths,
   depths, height defaults. On the order of 20–40 rules.
2. **Curation** — reject outputs *and say why*; each articulated rejection becomes a new invariant
   or rule tweak, permanently shrinking the bad-output space. This is why expert-in-the-loop works
   where corpus mining didn't: judgment enters as checkable constraints, not training data.
3. **Post-detach polish** — the character pass in the editor (set pieces, themed builds, terrain
   detail). Always manual; the system generates layout, not the world's art.

Viable scope, tiered:

- **Tier 1 (high confidence).** Parameterized seed families end-to-end: schema + compiler +
  validator, the rule checklist, a composer sampling valid plans across the archetype space
  (2/4 teams, 1–3 wools, isolation variants, open-mid vs directed, elevation profiles), roughen +
  elevation, into the existing export pipeline. Deliverable: a "new map" button producing a valid,
  walkable map that reads as *competent but plain* — the first ~60–70% of layout work.
- **Tier 2 (viable via the curation loop).** Re-roll workflow (same plan, new geometry; same
  geometry, new elevation), plan-level mutation ops in the UI, rules converging from feedback
  rounds. Ceiling: maps the author tweaks for an hour instead of building for days, with
  tactical (not decorative) elevation.
- **Tier 3 (not promised).** Signature interestingness — novel set pieces, new motifs, themed
  identity. Rule systems recombine encoded motifs; they don't coin new ones. "Boring" splits in
  two: **boring-looking** is solved by the generator (roughen + elevation); **boring-conceptually**
  is bounded by the motif library, which grows only when the author adds to it.

## 7. Open questions

1. **The scale pass.** Answered in part: the grid cell is a parameter (5 default, 4 viable) and
   the plan is a scale *proxy* (§2, "mini layout"). Open: the design of the de-proxying scale
   pass — a uniform factor preserves the grid smell, so it likely means targeted per-part
   stretches (lengthen this lane, widen that mid) sitting beside the roughen operators. Where
   does it run (before roughen? interleaved?), and what pins the parts that must *not* stretch
   (wool-room stamp plateaus, corridor minimums)?
2. **The rule set itself.** The attachment rules, minimum widths, depth orderings, and elevation
   defaults must come from the author's experience. Fastest capture: a working session that turns
   "years of making these" into an explicit checklist (per role: what it may touch, how wide, how
   deep, how high) — that checklist *is* the generator's content. When?
3. **Build zones as pieces** — confirm modeling build regions as first-class plan pieces (vs
   attributes on `gap` interfaces). Pieces make the open-mid band and multi-piece contact natural;
   it's the current lean.
4. **Interaction model.** One-shot generate-then-edit, or staged: approve/edit the plan (a small
   form/canvas over rects), then re-roll roughening and elevation independently? Lean: staged —
   every layer stays editable and re-rollable.
5. **v1 scope.** Plateaus + steps only, or ramps and cliffs too?

## 8. Phasing

1. **Seed studio** — the plan schema + the plan→(layout, intent) compiler, with the three existing
   seeds re-expressed as plans whose compiled output reproduces today's files exactly (the
   regression anchor); then a **minimal plan editor** over the existing canvas infra: 5-block-grid
   rect drawing, role palette, per-piece plateau heights, a bridge tool (gap interfaces — `land`
   interfaces are inferred from grid abutment), spawn/wool markers, symmetry pick with mirror
   ghost, compile button. Plan JSON imports/exports as files so seeds live in `tools/seeds/`.
   Purpose: the author hand-builds the **boring-seed corpus** (~10–15 layouts spanning wools ×
   isolation variants × flow axis × team count × elevation) — each authored seed is
   simultaneously plan-library content and a compiler regression case.
2. **Rule capture** (`docs/contracts/layout-rules.md`) → attachment rules + invariants encoded.
3. **Composer** — rule-based unit growth + neutral middle + isolation moves; every output passes
   the validator and the existing export gates.
4. **Roughen pass** operators + invariant checks, iterated visually in the sketch editor.
5. **Elevation rules** (plateaus, cut&raise, role defaults; ramps/cliffs if in scope).
6. **Mutation surfaced in the UI** — plan ops + re-roll buttons per layer.
