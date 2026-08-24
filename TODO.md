# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Ten entries, all about a stack of layers**, and one law now settles the shape of the rest. What is left of
the group five authored maps (`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not
touch: a board whose ground is stacked, where a hall runs under a terrace and every read projects the column
to one cell. A mineshaft built under a meadow is the worked example the entries are measured on.

## A stack of layers: what it writes, and what no read can say about it

**A layer is a slab.** One layer holds exactly one segment per column — a `(Top, Floor)` pair — and a taller
add replaces it outright, floor included. Stacking inside a single layer is not a thing an author may do: two
shapes on one footprint at two floors answer 480 cells, 0 stacked, every column `[16,22)`, the lower shape
erased. A stack is a stack **of layers**, and `base_y`, per-layer relief and per-layer theming are what a slab
is for. *(Settled by the author; this is the law `TS22`'s layer id names and `TS25` writes down.)*

**A depression or a river in the upper layer does not bleed into the layer beneath it.** That is the default;
letting it cut through is the toggle, not the other way round. *(Settled by the author.)*

The mineshaft **builds today with plain adds**: four wall shapes clamped around a low gallery floor, a ramp
climbing out of its east end, and a deck on a second layer with a mouth cut where the ramp arrives. 6,400
cells, 12,656 segments, 6,256 of them stacked. A `subtract`-and-`override` variant builds the identical
geometry and is not needed.

What fails is everything downstream of the geometry, and three of the entries below share one cause: **a
column segment does not carry the layer that produced it.** `SketchRasterizer` iterates layers and drops the
id on the floor (`foreach (var (layout, _) in ResolveLayers(state))`); every read after that is guessing.
`TS22` is the keystone and the seven entries after it are ordered behind it, but **only the last three need
it** — the rest are unblocked and can be taken in any order.

- [ ] **WS13 — Free the word `layer` for the thing that is one.** Four types carry it and only one is a
  layer: `SketchLayer` is the slab, while `TopDownLayer` is a render *subject* (`ground` · `structure` ·
  `foliage` · `objectives`), `ProvenanceLayer` is which build *pass* claimed a column, and `SurfaceLayer`
  reads a scan artifact. Rename the three that borrowed it — `TopDownSubject` with the query word to match,
  `ProvenancePass` (`WorldProvenance`'s claim becomes `(Pass, Owner)`), `SurfaceScan` (the `layer.parquet`
  blob keeps its name). `SketchLayer` and the document's `layers[]` do **not** move: 17 committed layouts
  spell it, and it is the one use where the word is literally true. Fixes `docs/world-scan/read-backs.md`'s
  endpoint table and `docs/world-export/decoration.md` in the same commit. **First**, because `TS22` stamps
  the word across sixteen sites and `WS12` adds another.

  *Each of the four docstrings already uses the right word in its own prose: "one question per image",
  "which pass claimed a column, last", "one surface-scan cell".*

- [ ] **TS22 — A column segment carries its layer, and so does the claim.** `SketchRasterizer.RasterizeColumns`
  answers `(X, Z, YFloor, YTop)`; make it `(X, Z, YFloor, YTop, Layer)`. Sixteen call sites, all mechanical.
  Carry it through to `WorldProvenance`, which keys `(X, Z) → (Pass, Owner)` with no Y in it — that second
  half is what `C48` and `WS12` both need and neither owns. On its own it changes no behaviour, which is the
  point.

  *The layer id exists one line up and is discarded at the `_` in `ResolveLayers`.*

- [ ] **TS27 — Refuse a second segment in one layer instead of erasing it.** `MergeCell` swallows the lower
  of two shapes on one footprint silently: no finding, no complaint, and a board that reads as authored. Now
  that one segment per column per layer is the law, an author who draws a roof over a floor **in the same
  layer** has made a mistake the tool can name — raise it as `SK9` beside the eight sketch rules, pointing at
  both shape ids and saying that a roof is a second layer.

  *Measured both shape orders: 480 cells, 0 stacked, every column `[16,22)`, nothing raised.*

- [ ] **TS25 — Write the layer model down.** `docs/tools/sketch.md` gives a layer five lines and closes with
  "an agent should author the ground layer only", which is advice rather than a description. What a reader
  needs and cannot get without three probes and a source read: a layer is a **slab** holding one
  `(Top, Floor)` per cell and a taller add replaces it outright, so a roofed gallery is walls *clamped around*
  a tucked-in floor rather than a low shape drawn inside a tall one, and it takes two layers rather than two
  floors; `floor` is the underside measured inside the layer, `base_height` is the thickness above it, and
  `base_y` shifts the whole thing; `anchor_heights` slopes a shape between two layers; relief solves per layer
  and returns already shifted into world Y; the set algebra is `((adds − subs) ∪ override-adds) −
  override-subs`, **by category and not document order**; and the stone-only invariant in
  `TerrainPainter.Paint` is the one line that lets an air gap survive between two slabs.

- [ ] **TS24 — Warn where two layers fuse.** A shape's bottom is flat inside its layer, so a cell's segments
  are intervals and the check is a sort and a scan of the rasterizer's own output — about fifteen lines.
  Complain where two segments **overlap**, and where the gap between them is under `Walk.Headroom`. Flush is
  the healthy case and must stay silent: the rock mass meeting the deck underside is how a roof is built.
  Needs no layer id — the segments alone answer it.

  *On the mineshaft: 0 overlapping, 11,600 flush, 528 separated with a thinnest gap of 14.*

- [ ] **TS21 — One cell per column is why a stacked board reads wrong.** `WorldColumns.Membership` projects a
  column to one cell and the walk stands a player on the **lowest** surface carrying headroom, so a stacked
  board is read entirely on its floor. Neither the coverage read nor traversability can say a board has two
  storeys, let alone which one the match is played on.

  A column with two standable surfaces holds two places, and the walk has to carry both plus the ways between
  them: a ramp is an edge from one storey to the other, and a sealed mine has no edge and is genuinely
  separate. Preferring the upper surface is the same bug with the sign flipped. Reads the built world's
  columns, so it needs no layer id.

  *`opus5-undercroft` at `(0, 48)`: the walk stands at **15**, the hall floor, while the terrain top is
  **28** — every distance that board reports is measured through a nine-block undercroft. On the mineshaft the
  same rule sends a player down the mine and up the ramp and reports **no surface route across the middle of
  the board at all**, though 6,400 of 6,400 columns read as passable.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under **every** footprint cell (`world.SetBlock(x, 0,
  z, Blocks.Bedrock)`), so a bridge slab across a strait drops its own plate into the abyss and an
  overhanging deck does the same over whatever it hangs past. The theme's `bedrock` value does not reach it —
  the painter only overwrites stone. Condition the course on the cell having a segment that reaches it.

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

- [ ] **TS23 — Paint every exposed surface, with its own layer's theme.** Two independent blockers, both
  readable from a signature. `TerrainBuilder` collapses a cell to its **maximum** top and `TerrainPainter`
  walks down from that one height, so nothing under the highest slab is ever visited. And
  `SketchRasterizer.ShapeThemeOwners` answers `(x, z) → shapeId` over every layer at once — smallest area
  wins, ties to the first claimer — while `themeAt(x, z)` has no Y to tell two layers apart with. Make
  `SurfaceTop` one entry per segment and key the owner map on `(layer, cell)`. Wants `TS22`.

  *The mineshaft's meadow deck is painted **andesite where a wall stands under it and gravel where the gallery
  runs** — a map of the layer below, cell for cell, because every mine-level shape is smaller than the deck
  that roofs the whole board. Its gallery floor at `y 1..3` and the walls facing into it are raw stone. The
  meadow theme lands on no block anywhere.*

- [ ] **C48 — Toggle a layer in the 3-D view.** `WorldColumnPayload.Of` reads the finished `VoxelWorld` and
  emits runs of blocks with no idea which layer made any of them, so the preview cannot hide one. Carry a
  layer index per run and the toggle is client-side filtering in `sketch-canvas`'s column mesh. Wants `TS22`,
  both halves.

- [ ] **WS12 — A read-back can be asked for one layer.** Every `render/*` route is whole-world, and the only
  cut a caller has is `ymax`, a single height, which separates two storeys just where one lies flat over the
  other. Add a layer word to the four reads that project a column to one cell — `topdown`, `heightmap`,
  `surface`, `structures`. Wants `TS22`, both halves; wants `WS13` to have freed the word first. It is also
  the acceptance test for `TS22` and `TS23`: without a per-layer read, whether a theme landed on the right
  storey can only be inferred from segment counts.

  *On the mineshaft the deck roofs all 6,400 cells, so every top-down read draws the deck alone; the gallery
  at `[0,4)` is reachable only through `ymax`, and only because that deck happens to be flat at `y 20`.*

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
