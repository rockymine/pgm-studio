# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Twelve entries, all about a stack of layers.** What is left of the group five authored maps
(`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not touch: a board whose ground
is stacked, where a hall runs under a terrace and every read projects the column to one cell. A mineshaft
built under a meadow is the worked example, and it is committed on this branch.

## Still open — three the author has not settled

| | the question | blocks |
|---|---|---|
| **Q5b** | A sealed space is an author's choice and a floating slab is a warning, and both are a disconnected component. Is the line **a whole layer connecting to nothing** against **a pocket inside one**, or is it drawn somewhere else? | `TS24` `TS21` |
| **Q7** | A sketch document holds its shapes under `layout` (one slab) **or** under `layers[]` (a stack). A plan compiles to the first; adding a second layer means moving to the second, and `opus5-undercroft`'s driver had to **delete `layout` by hand** or every island counted twice. No stored document carries both — 7 hold `layers`, 45 hold `layout`, 0 hold both — so the case is reachable and unspecified rather than live. Should a document carrying both be **refused**, should `layout` be read as **the ground layer under `layers`**, or should it be **ignored** whenever `layers` is present? | `TS28` |
| **Q8** | The three type renames are settled. Beyond them sit `GET /map/{slug}/layers`, whose response says which *artifacts* a map holds while `layers[]` in the sketch document means slabs — the collision itself — and the internal `layer_segment` table, which nothing outside the studio reads. Rename the route, the table, both, or neither? | `WS13` `P7` |

## A stack of layers: what it writes, and what no read can say about it

**A layer is a slab.** One layer holds exactly one segment per column — a `(Top, Floor)` pair — and a taller
add replaces it outright, floor included. Stacking inside a single layer is not a thing an author may do: two
shapes on one footprint at two floors answer 480 cells, 0 stacked, every column `[16,22)`, the lower shape
erased. A stack is a stack **of layers**, and `base_y`, per-layer relief and per-layer theming are what a slab
is for. *(Settled by the author; this is the law `TS22`'s layer id names and `TS25` writes down.)*

**A depression or a river in the upper layer does not bleed into the layer beneath it.** That is the default;
letting it cut through is the toggle, not the other way round. *(Settled by the author.)*

**A placement names its layer, and absent a name it takes the top one.** An objective, a spawn, a marker and
a prop all state where they go in `(x, z)`, and on a stack that is not enough — the layer word is what lets a
monument sit in a hall and a tree stand on a gallery floor. The top layer stays the default, so nothing
already authored moves. *(Settled by the author.)*

**A second storey is played on.** A tunnel under the ground is a second way to an objective — a flanking
route — so the storeys of a stacked board and the ways between them are part of the map, not scenery under
it. *(Settled by the author.)*

**A plan states no stack.** The plan model stays flat, and its simplicity outranks consistency with the
sketch here: a storey is a fact about ground that has been drawn, never about a plan. *(Settled by the
author.)* So the walk's node is a **place** — a cell and the height stood at — and a plan board simply has
exactly one place per cell. The plan document does not change.

**A column has one theme per layer, not one theme.** A map's surface takes one style and a piece takes
another, and on a stack those sit above one another in the same column: a cell stops having a single owner.
*(Settled by the author.)*

**Two layers meeting flush is how a roof is built, and a sealed space is an author's choice** — a hidden room
holding something useful is a thing someone may want, so neither is a complaint. What is worth one is a layer
nothing connects to, which is a slab floating over the board rather than a storey of it. *(Settled by the
author; `Q5b` is where that line falls.)*

**A slab over void plates nothing.** Falling off a bridge into the void is the honest outcome — a build
region covers the case where it should not be, and a board with ground nobody can walk to is complained
about on its own account. *(Settled by the author.)*

The mineshaft **is committed** — `pgm-studio-mapgen`, `maps/opus5-mineshaft` · `specs/opus5-mineshaft/` ·
`reports/opus5-mineshaft-layers.md`, on this branch. It is eighty blocks square, two layers, six shapes, no
relief and no dressing: four wall rectangles clamped around a tucked-in gallery floor, a ramp climbing out of
the east end on `anchor_heights [4, 26, 26, 4]`, and a deck over all 6,400 cells at `base_y 20` with a mouth
subtracted where the adit surfaces. A gallery column reads `[0,3]` · `[20,25]` — sixteen blocks of air
between two storeys. A `subtract`-and-`override` variant builds the identical geometry and is not needed;
both shapes on **one** layer does not build at all.

**Every entry below is measured on that fixture, and the numbers in it are re-measurable.** Where a figure
here predates the commit and the fixture's own geometry differs, the entry says so rather than carrying the
older number.

What fails is everything downstream of the geometry, and four of the entries below share one cause: **a
column segment does not carry the layer that produced it.** `SketchRasterizer` iterates layers and drops the
id on the floor; every read after that is guessing. Under *that* sits a second cause nothing had named:
**seven places state what a document's layers are, in three different readings** — which is `TS28`, and it
goes first, because `TS22` stamps the layer across sixteen sites and doing that over seven readers stamps it
seven ways.

**Two entries owe a document their prefix does not name.** `TS21`'s standing rule is written in
`docs/world-scan/read-backs.md` § *Where a player stands in a column*, and `TS23`'s painter in
`docs/world-export/terrain-painting.md`. The ids stay — both are already cited in commits — and each entry
fixes the document it actually moves, alongside `docs/tools/sketch.md`.

- [ ] **TS28 — One reader for a document's layers.** Seven places state "the layers of this document, else the
  legacy single `layout`", and they do not agree. `SketchRasterizer.ResolveLayers`,
  `SketchLayout.StructuralHeights` and `TerrainThemeScope.ShapeLists` read `layers` **or** `layout`;
  `SketchLayout.IslandIds`, `SketchLayout.ShapeArrays`, `SketchLayoutCheck.Shapes` and
  `SketchLayoutCheck.Islands` read `layers` **and** `layout`. So the gate quantifies over shapes the
  rasterizer will never build, on a document that carries both. One public reader on `SketchLayout` answering
  `(Id, Name, BaseY, Shapes, Islands)` per layer; all seven take it. It is also where `TS22`'s id comes from —
  `ResolveLayers` never reads `l.Id` — and where a legacy document with no `layers[]` gets a synthesised one.
  `Q7` is the decision it cannot be written without — the reader has to mean one thing. **First.**

  *`opus5-undercroft`'s driver had to delete the `layout` key by hand: `ResolveLayers` reads one or the other
  while `IslandIds` reads both, and every island was counted twice.*

- [ ] **WS13 — Free the word `layer` for the thing that is one.** Seven senses, one true: `SketchLayer` is
  the slab and does **not** move. The rest borrowed it:

  - `TopDownLayer` is a render *subject* (`ground` · `structure` · `foliage` · `objectives`) → `TopDownSubject`,
    with the query word to match;
  - `ProvenanceLayer` is which build *pass* claimed a column → `ProvenancePass` (`WorldProvenance`'s claim
    becomes `(Pass, Owner)`);
  - `SurfaceLayer` reads a scan artifact → `SurfaceScan` (the `layer.parquet` blob keeps its name);
  - **`MapLayers` / `MapState.Layers` / `GET /map/{slug}/layers`** is which *authoring artifact* a map holds
    (plan · sketch · world · intent) — a public route that reads exactly like "this map's sketch layers";
  - the **scan-segment family** — `LayerSegments`, `layer_segment`, `LayerExtractors`, `LayersEndpoints.cs`,
    `LayerParquet`, `scan_layer`, `PgmStudio.Analysis.Layer` — which `P7` also spends;
  - the **canvas z-stack** — `render/layer-stack.js`, `data-layer` — in the same folder as the layer chips;
  - and `LayeredMaterial`'s own `layers[]`, inside the theme JSON of the same document.

  **The three type renames are settled** and are what this entry is. `Q8` says whether the route and the
  table follow — there is no compatibility to keep, so the cost is the migration and nothing else. Fixes
  `docs/world-scan/read-backs.md`'s endpoint table and `docs/world-export/decoration.md` in the same commit.
  **Early**, because `TS22` stamps the word across sixteen sites and `WS12` adds another.

  *Each borrowed docstring already uses the right word in its own prose: "one question per image", "which
  pass claimed a column, last", "one surface-scan cell".*

- [ ] **TS22 — A column segment carries its layer, and so does the claim.** `SketchRasterizer.RasterizeColumns`
  answers `(X, Z, YFloor, YTop)`; make it `(X, Z, YFloor, YTop, Layer)` — eight call sites in `src/`, twenty
  more in `tests/`, all mechanical. Carry it through to `WorldProvenance`, which keys `(X, Z) → (Pass, Owner)`
  with no Y in it; keying the claim on the segment is what `C48` and `WS12` both need and neither owns. On its
  own it changes no behaviour, which is the point.

  **Two walks, not one.** `RasterizeColumns` is the geometry and `ShapeScopeOwners` is the theme walk, and
  each has its own layer loop. This entry takes both, or `TS23` inherits half of it. Wants `TS28` — the id
  and the legacy document's synthesised one both come from that reader. The **placement** word a layer also
  gains is `WE24`'s, not this entry's: a segment is where the layer comes from and a placement is where it is
  spent.

  *`ResolveLayers` never reads `l.Id`: `layers.Select(l => (l.Layout ?? new SketchShapes(), l.BaseY))`. The
  `_` in `ShapeScopeOwners` discards `BaseY`, not the id — the id is dropped a level above it. The claim half
  is visible in `opus5-mineshaft`'s `renders/topdown-mine.png`: two spawn cubes standing at 26 drawn into a
  cut at `ymax=19`.*

- [ ] **TS27 — Say something when a second segment in one layer erases the first.** `MergeCell` swallows the
  lower of two shapes on one footprint: no finding, no complaint, and a board that reads as authored. Now that
  one segment per column per layer is the law, an author who draws a roof over a floor **in the same layer**
  has made a mistake the tool can name — raise it as `SK9` beside the eight sketch rules, naming **the shape
  that did not survive** and saying that a roof is a second layer. A **`Decline`**, not a refusal and not a
  complaint: the board builds and one thing the author drew is gone from it, which is what that severity is
  for. *(Settled by the author.)*

  **It cannot be a sweep, which is why it is not `TS24`.** The erasure happens inside `RasterGroup`/`MergeCell`,
  before any segment list exists: by the time a sweep has data there is one segment and it reads as authored.
  `SketchLayoutCheck` is a pure gate over the document and reaches neither. Where the finding is raised from
  is the work.

  *Both draw orders are committed as fixtures — `opus5-mineshaft.one-layer-a.layout.json` and
  `…-b` — and each answers 480 cells, 0 stacked, every column `[16,22)`, nothing raised.*

- [ ] **TS25 — Write the layer model down.** `docs/tools/sketch.md` gives a layer five lines and closes with
  "an agent should author the ground layer only", which is advice rather than a description. What a reader
  needs and cannot get without three probes and a source read: a layer is a **slab** holding one
  `(Top, Floor)` per cell and a taller add replaces it outright, so a roofed gallery is walls *clamped around*
  a tucked-in floor rather than a low shape drawn inside a tall one, and it takes two layers rather than two
  floors; `floor` is the underside measured inside the layer, `base_height` is the thickness above it, and
  `base_y` shifts the whole thing; `anchor_heights` slopes a shape between two layers; relief solves per layer
  and returns already shifted into world Y; the set algebra is `((adds − subs) ∪ override-adds) −
  override-subs`, **by category and not document order**; and the stone-only invariant in
  `TerrainPainter.Paint` is the one line that lets an air gap survive between two slabs. Plus the half no
  probe can show — a placement names its layer and defaults to the top, a column carries one theme per layer,
  a second storey is played on, a sealed space is allowed and a layer connecting to nothing is not — and
  whatever `Q7` settles about a document holding both `layout` and `layers`.

- [ ] **TS24 — Warn where a layer connects to nothing.** Two thirds of what this entry used to propose is
  now settled *not* to be a complaint: flush is how a roof is built, and a sealed space is an author's choice.
  What is left is two checks of different kinds.

  **Overlapping segments stay a complaint** — two shapes claiming the same Y in one column is a geometry
  error whatever the author meant. A shape's bottom is flat inside its layer, so a cell's segments are
  intervals and this is a sort and a scan of the rasterizer's own output, about fifteen lines, needing no
  layer id.

  **A layer nothing connects to is the real warning**, and it is not an interval check at all: it asks
  whether a player can get from this storey to any other, so it wants `TS21`'s places and the edges between
  them. `Q5b` says where the line falls between a floating slab and a hidden room.

  *`opus5-mineshaft`: the walls run `[0,25]` flush into the deck, the gallery keeps `[0,3]` · `[20,25]`
  sixteen blocks below it, and the adit is the one connection between the two — which is exactly what this
  check has to find, and what a segment sweep cannot see.*

- [ ] **TS21 — One cell per column is why a stacked board reads wrong.** `SegmentIndex.StandingTops` yields the
  **lowest** surface carrying headroom and stops there; `WorldColumns.Membership` discards Y outright. Neither
  the coverage read nor traversability can say a board has two storeys, let alone which one the match is
  played on. Preferring the upper surface is the same bug with the sign flipped: a column with two standable
  surfaces holds two places and the ways between them.

  **The node becomes a place.** `WalkGround` is keyed `(X, Z)` in five places — `Ground`, `Bridgeable`,
  `Surface`, its `Passable` union, `Narrowed` — over nine files, and the same type serves the plan fidelity
  (`PlanNav`, `PlanRoutes`, `GoalDistances`, `SurfaceNav`). Key it on the cell **and the height stood at**: a
  plan board then has one place per cell and states nothing new, a built column one per standable surface,
  and a ramp is an ordinary edge between two of them.
  Reads the built world's columns, so it needs no layer id.

  *`opus5-mineshaft`, spawn to spawn across the middle, answers **reachable, distance 60, blocks 21, drops 1,
  worstDrop 22** straight along x = 0. It is not a route: the standing surface flips from the deck at **26**
  to the mine floor at **4** where the gallery begins, and a twenty-two block fall is scored as one drop on
  one continuous surface. Traversability calls the same chain connected.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under **every** footprint cell (`world.SetBlock(x, 0,
  z, Blocks.Bedrock)`), so a bridge slab across a strait drops its own plate into the abyss and an
  overhanging deck does the same over whatever it hangs past. The theme's `bedrock` value does not reach it —
  the painter only overwrites stone. Condition the course on the cell having a segment that reaches it: a
  player falling off a bridge meets the void, which is the honest outcome. *(Settled by the author.)*

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

- [ ] **WE24 — A placement names its layer.** Everything the export puts down resolves its Y from an
  `(x, z) → int` grid and so lands on the top slab whatever the author meant: an objective, a spawn, a room
  and a wall through `PositionSnap.SurfaceYOver`, and every prop through `DressingContext.SurfaceTop` at ten
  sites in `Decorator` plus `DressingScope`. **Two readers, and the layer word has to reach both** or a tree
  and a monument in one hall disagree about where the hall is. The word is optional and defaults to the top
  layer, so nothing already authored moves; a placement naming a layer its cell has no segment on is a
  `Decline` — the map builds and that one thing is not in it. Wants `TS22`.

  *`opus5-undercroft`: "I stated a tree for the hall floor and got it on the roof, which was the point of
  stating it." The same grid put its destroyable on the terrace by itself.*

- [ ] **TS23 — Paint every exposed surface, with its own layer's theme.** Two independent blockers, both
  readable from a signature. `TerrainBuilder` collapses a cell to its **maximum** top and `TerrainPainter`
  walks down from that one height, so nothing under the highest slab is visited. And `ShapeThemeOwners`
  answers `(x, z) → shapeId` over every layer at once while `themeAt(x, z)` has no Y to tell two layers
  apart. Key the owner map on `(layer, cell)`: a cell stops having one owner, so each layer paints its own.
  Wants `TS22`, both walks.

  **`SurfaceTop` is two questions under one name, and only one is this.** Fifteen files read it; twelve are
  stampers and dressing wanting *one* answer per column — where the thing placed here stands. Only the painter
  wants one per segment, so it takes its own set rather than `BuiltTerrain.SurfaceTop` changing meaning under
  callers with no basis to pick one; those callers gain a layer word instead, which is `WE24`. `B144` is the
  same overlap nested rather than stacked, and smallest-area still arbitrates there — two shapes on **one**
  layer do contest a cell.

  *`opus5-mineshaft` states `meadow` (Grass Block, `0x79C05A`) as its map default over the whole deck, with
  `deepstone` on the walls and `minefloor` on the gallery under it. Its committed `renders/surface.png` holds
  **zero green pixels in 136,960**: every mine-level shape is smaller than the deck, so the deck wears the
  layer below and the meadow theme lands on no block anywhere.*

- [ ] **C48 — Toggle a layer in the 3-D view.** `WorldColumnPayload.Of` reads the finished `VoxelWorld` and
  emits runs of blocks with no idea which layer made any of them, so the preview cannot hide one. Carry a
  layer index per run and the toggle is client-side filtering in `sketch-canvas`'s column mesh. Wants `TS22`,
  both halves — the claim has to be keyed on the segment, not on `(X, Z)`, or a run cannot be attributed.

- [ ] **WS12 — A read-back can be asked for one layer.** Every `render/*` route is whole-world, and the only
  cut a caller has is `ymax`, a single height, which separates two storeys just where one lies flat over the
  other. Add a layer word to the four reads that project a column to one cell — `topdown`, `heightmap`,
  `surface`, `structures`. `traversability` and `walk` project too and are **not** here: they want `TS21`
  rather than a layer word. Wants `TS22`, both halves; wants `WS13` to have freed the word first. It is also
  the acceptance test for `TS22` and `TS23`: without a per-layer read, whether a theme landed on the right
  storey can only be inferred from segment counts.

  *On the mineshaft the deck roofs all 6,400 cells, so every top-down read draws the deck alone and the
  gallery is reachable only through `ymax` — and only because that deck happens to be flat. The committed
  `renders/topdown-mine.png` is that cut at `ymax=19`: it shows the gallery **and both spawn cubes**, which
  stand on the meadow at 26, because provenance keys a claim `(X, Z)` with no Y and the structure layer is
  drawn whole whatever the cut says.*

## What the front door still cannot say

- [~] **RP23 — `docs/tools/capabilities.md` is 707 lines answering "what can I ask for", which the API now
  answers itself.** The schema names every route, its body and its failure codes; `GET /api/rules` names
  every refusal with its fix; `GET /map/{slug}/layers` puts the allowed moves on the map's own response.
  What prose is good at and this file is not organised around is the other half: **how to make a good map** —
  what an objective needs around it, what the corpus does — as against **what the system can be asked for**.
  Split it on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.

  The mapgen half landed: `pgm-studio-mapgen`'s six root documents became two, and `AUTHORING-BRIEF.md`
  points at the four self-describing reads instead of restating them. This entry is the studio's own side.

  *Five authored boards never opened it. What they read instead was `relief.md`, `decoration.md`,
  `terrain-painting.md` and the endpoint tables inside them — the split this entry proposes, observed.*
