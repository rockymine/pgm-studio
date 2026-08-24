# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Twelve entries, all about a stack of layers**, and one law settles the shape of the rest. What is left of
the group five authored maps (`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not
touch: a board whose ground is stacked, where a hall runs under a terrace and every read projects the column
to one cell. A mineshaft built under a meadow is the worked example the entries are measured on.

## Questions only the author can answer

**Nine decisions sit under the twelve entries, and none of them is derivable here.** The corpus shows what
authors did and the code shows what the tool does; neither says what a stacked board is *for*. Each row names
the entries that cannot be finished without it.

| | the question | blocks |
|---|---|---|
| **Q1** | A thing placed at `(x, z)` on a stacked board lands on the **highest** surface, silently. Should a placement be able to name a layer — a monument in the hall, a tree on the gallery floor — or is the top surface always where a placement goes? | `TS22` `TS23` `WE24` |
| **Q2** | Is the ground under a deck **ground a player uses**, or is it dead? If dead, the walk's job is to say a board has a second storey nobody plays on; if used, it needs both storeys and the ways between them. | `TS21` `WE24` |
| **Q3** | Can a **plan** ever state a stack — an overpass, a tunnel — or is stacking a sketch-only idea? `WalkGround` is one type serving both fidelities, and a plan board is one storey by construction. | `TS21` |
| **Q4** | Two themed shapes cover one cell on two layers. "Most specific scope" means **smallest area** today. On a stack, should it mean **highest column** instead — or does a cell simply stop having one theme? | `TS23` `B144` |
| **Q5** | Two layers meeting with a **zero gap** is how a roof is built — the mineshaft's walls hold its deck up that way over most of the board. It is also exactly what a hall filled in by mistake looks like, and nothing in the geometry separates them. Warn, stay silent, or is there something else the tool can read? | `TS24` |
| **Q6** | A second shape on one layer's footprint erases the lower one. Is that a **`Refusal`** (the map does not build), a **`Decline`** (it builds and the shape is gone from it), or a **`Complaint`**? `Decline`'s own docstring — *"the input did not survive, and a caller reading a 2xx has no other way to learn that"* — reads as written for this case. | `TS27` |
| **Q7** | A document carrying **both** `layers` and `layout`: malformed, or is `layout` the ground layer under `layers`? Seven readers answer this three ways today. | `TS28` |
| **Q8** | How far does freeing the word `layer` go — the public route `GET /map/{slug}/layers` and the `layer_segment` DB family too, or only the three types? A route and a table are a cost this board cannot price. | `WS13` `P7` |
| **Q9** | A slab over void would stop plating the bottom of the world, so a player falling off a bridge meets PGM's void filter instead of landing on bedrock. Wanted? | `WE22` |

## A stack of layers: what it writes, and what no read can say about it

**A layer is a slab.** One layer holds exactly one segment per column — a `(Top, Floor)` pair — and a taller
add replaces it outright, floor included. Stacking inside a single layer is not a thing an author may do: two
shapes on one footprint at two floors answer 480 cells, 0 stacked, every column `[16,22)`, the lower shape
erased. A stack is a stack **of layers**, and `base_y`, per-layer relief and per-layer theming are what a slab
is for. *(Settled by the author; this is the law `TS22`'s layer id names and `TS25` writes down.)*

**A depression or a river in the upper layer does not bleed into the layer beneath it.** That is the default;
letting it cut through is the toggle, not the other way round. *(Settled by the author.)*

The mineshaft **is committed** — `pgm-studio-mapgen`, `maps/opus5-mineshaft` · `specs/opus5-mineshaft/` ·
`reports/opus5-mineshaft-layers.md`, on `claude/map-relief-terrain-generation-lv9a39` and not yet merged. It
is eighty blocks square, two layers, six shapes, no relief and no dressing: four wall rectangles clamped
around a tucked-in gallery floor, a ramp climbing out of the east end on `anchor_heights [4, 26, 26, 4]`, and
a deck over all 6,400 cells at `base_y 20` with a mouth subtracted where the adit surfaces. A gallery column
reads `[0,3]` · `[20,25]` — sixteen blocks of air between two storeys. A `subtract`-and-`override` variant
builds the identical geometry and is not needed; both shapes on **one** layer does not build at all.

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
  `Q7` is the decision it cannot be written without. **First.**

  *`opus5-undercroft`'s driver had to delete the `layout` key by hand: `ResolveLayers` reads one or the other
  while `IslandIds` reads both, and every island was counted twice.*

- [ ] **WS13 — Free the word `layer` for the thing that is one.** Seven senses, one of them true.
  `SketchLayer` is the slab and does **not** move — the document's `layers[]`, the editor's chips and every
  stored layout spell it. The rest borrowed it:

  - `TopDownLayer` is a render *subject* (`ground` · `structure` · `foliage` · `objectives`) → `TopDownSubject`,
    with the query word to match;
  - `ProvenanceLayer` is which build *pass* claimed a column → `ProvenancePass` (`WorldProvenance`'s claim
    becomes `(Pass, Owner)`);
  - `SurfaceLayer` reads a scan artifact → `SurfaceScan` (the `layer.parquet` blob keeps its name);
  - **`MapLayers` / `MapState.Layers` / `GET /map/{slug}/layers`** is which *authoring artifact* a map holds
    (plan · sketch · world · intent) — a public route that reads exactly like "this map's sketch layers";
  - the **scan-segment family** — `LayerSegments` table, `layer_segment`, `LayerExtractors`,
    `LayersEndpoints.cs`, `LayerParquet`, `scan_layer`, namespace `PgmStudio.Analysis.Layer` — which `P7`
    also spends;
  - the **canvas z-stack** — `render/layer-stack.js`, `data-layer`, Sketch's 19 of them — in the same client
    folder as the sketch layer chips;
  - and `LayeredMaterial`'s own `layers[]`, inside the theme JSON of the same document.

  `Q8` says how far this goes; the three types are the floor. Fixes `docs/world-scan/read-backs.md`'s endpoint
  table and `docs/world-export/decoration.md` in the same commit. **Early**, because `TS22` stamps the word
  across sixteen sites and `WS12` adds another.

  *Each borrowed docstring already uses the right word in its own prose: "one question per image", "which
  pass claimed a column, last", "one surface-scan cell".*

- [ ] **TS22 — A column segment carries its layer, and so does the claim.** `SketchRasterizer.RasterizeColumns`
  answers `(X, Z, YFloor, YTop)`; make it `(X, Z, YFloor, YTop, Layer)` — eight call sites in `src/`, twenty
  more in `tests/`, all mechanical. Carry it through to `WorldProvenance`, which keys `(X, Z) → (Pass, Owner)`
  with no Y in it; keying the claim on the segment is what `C48` and `WS12` both need and neither owns. On its
  own it changes no behaviour, which is the point.

  **Two walks, not one.** `RasterizeColumns` is the geometry and `ShapeScopeOwners` is the theme walk, and
  each has its own layer loop. This entry takes both, or `TS23` inherits half of it. Wants `TS28` — the id
  and the legacy document's synthesised one both come from that reader. `Q1` decides whether a *placement*
  gains a layer word too, or only a segment does.

  *`ResolveLayers` never reads `l.Id`: `layers.Select(l => (l.Layout ?? new SketchShapes(), l.BaseY))`. The
  `_` in `ShapeScopeOwners` discards `BaseY`, not the id — the id is dropped a level above it. The claim half
  is visible in `opus5-mineshaft`'s `renders/topdown-mine.png`: two spawn cubes standing at 26 drawn into a
  cut at `ymax=19`.*

- [ ] **TS27 — Say something when a second segment in one layer erases the first.** `MergeCell` swallows the
  lower of two shapes on one footprint: no finding, no complaint, and a board that reads as authored. Now that
  one segment per column per layer is the law, an author who draws a roof over a floor **in the same layer**
  has made a mistake the tool can name — raise it as `SK9` beside the eight sketch rules, pointing at both
  shape ids and saying that a roof is a second layer. `Q6` picks the severity.

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
  `TerrainPainter.Paint` is the one line that lets an air gap survive between two slabs. Plus whatever `Q1`,
  `Q4` and `Q7` settle, which is the half a reader cannot infer from any probe.

- [ ] **TS24 — Warn where two layers fuse.** A shape's bottom is flat inside its layer, so a cell's segments
  are intervals and the check is a sort and a scan of the rasterizer's own output — about fifteen lines.
  Complain where two segments **overlap**, and where the gap between them is under `Walk.Headroom`. Needs no
  layer id — the segments alone answer it.

  **`Q5` is the whole of it.** A wall meeting the deck it holds up is flush and healthy; a hall filled in by
  mistake is flush and wrong, and the geometry does not separate them.

  *`opus5-mineshaft` is the fixture: its walls run `[0,25]` flush into the deck while its gallery keeps
  `[0,3]` · `[20,25]`, sixteen blocks apart. The overlap/flush/gap census over it is this entry's acceptance
  number and has not been taken on the committed board. On `opus5-undercroft`, a ground mark raised two blocks
  too far turned a nine-block hall into solid rock and merged in silence.*

- [ ] **TS21 — One cell per column is why a stacked board reads wrong.** `SegmentIndex.StandingTops` yields the
  **lowest** surface carrying headroom and stops there; `WorldColumns.Membership` discards Y outright. Neither
  the coverage read nor traversability can say a board has two storeys, let alone which one the match is
  played on. Preferring the upper surface is the same bug with the sign flipped: a column with two standable
  surfaces holds two places and the ways between them — a ramp is an edge from one storey to the other, and a
  sealed mine has no edge and is genuinely separate.

  **The shape is the question, not the fix.** `WalkGround` is keyed `(X, Z)` in five places — `Ground`,
  `Bridgeable`, `Surface`, its `Passable` union, `Narrowed` — over nine files, and it is the **same type the
  plan fidelity walks** (`PlanNav`, `PlanRoutes`, `GoalDistances`, `SurfaceNav`). `Q3` says whether a storey
  belongs in the shared type or only on the built side; `Q2` whether a covered storey is a place at all.
  Reads the built world's columns, so it needs no layer id.

  *`opus5-mineshaft`, spawn to spawn across the middle, answers **reachable, distance 60, blocks 21, drops 1,
  worstDrop 22** straight along x = 0. It is not a route: the standing surface flips from the deck at **26**
  to the mine floor at **4** where the gallery begins, and a twenty-two block fall is scored as one drop on
  one continuous surface. Traversability calls the same chain connected.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under **every** footprint cell (`world.SetBlock(x, 0,
  z, Blocks.Bedrock)`), so a bridge slab across a strait drops its own plate into the abyss and an
  overhanging deck does the same over whatever it hangs past. The theme's `bedrock` value does not reach it —
  the painter only overwrites stone. Condition the course on the cell having a segment that reaches it, once
  `Q9` says the consequence is wanted.

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

- [ ] **WE24 — Nothing can be dressed under a roof.** Every prop resolves against `DressingContext.SurfaceTop`,
  an `(x, z) → int` grid, at ten sites in `Decorator` and again in `DressingScope`. A tree stated for a gallery
  floor lands on the deck over it and the census records it there — not declined, not warned, somewhere else.
  This is the third leg of the cause `TS23` (paint) and `TS21` (the walk) each take one of, and the only one
  that had no entry. `Q1` decides the shape: if a placement may name a layer this is a placement rule, and if
  it may not it is a `Decline` saying the prop could not be seated where it was stated.

  *`opus5-undercroft`: "I stated a tree for the hall floor and got it on the roof, which was the point of
  stating it."*

- [ ] **TS23 — Paint every exposed surface, with its own layer's theme.** Two independent blockers, both
  readable from a signature. `TerrainBuilder` collapses a cell to its **maximum** top and `TerrainPainter`
  walks down from that one height, so nothing under the highest slab is visited. And `ShapeThemeOwners`
  answers `(x, z) → shapeId` over every layer at once — smallest area wins — while `themeAt(x, z)` has no Y
  to tell two layers apart. Key the owner map on `(layer, cell)`; `Q4` says what wins where two layers claim
  one cell. Wants `TS22`, both walks.

  **`SurfaceTop` is two questions under one name, and only one is this.** Fifteen files read it; twelve are
  stampers and dressing (`PositionSnap`, `ObjectiveStamper`, `DressingScope`, …) wanting *one* answer per
  column — where the thing placed here stands. Only the painter wants one per segment, so it takes its own set
  rather than `BuiltTerrain.SurfaceTop` changing meaning under callers with no basis to pick one. `Q1` says
  whether the placement answer moves; `B144` holds the nested half of `Q4`.

  *`opus5-mineshaft` states `meadow` (Grass Block, `0x79C05A`) as its map default over the whole deck, with
  `deepstone` on the walls and `minefloor` on the gallery under it. Its committed `renders/surface.png` holds
  **zero green pixels in 136,960**: every mine-level shape is smaller than the deck roofing the board, so the
  deck wears the layer below and the meadow theme lands on no block anywhere. On `opus5-undercroft` the hall
  floor at y14 is stone brick in the terrace's `flag` theme.*

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
