# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Nine entries, all about a stack of layers**, and one of them unlocks the rest. What is left of the group
five authored maps (`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not touch: a
board whose ground is stacked, where a hall runs under a terrace and every read projects the column to one
cell. A mineshaft built under a meadow is the worked example the entries are measured on.

## A stack of layers: what it writes, and what no read can say about it

The mineshaft is the worked example and it **builds today with plain adds**: four wall shapes clamped around
a low gallery floor, a ramp climbing out of its east end, and a deck on a second layer with a mouth cut where
the ramp arrives. 6,400 cells, 12,656 segments, 6,256 of them stacked. A `subtract`-and-`override` variant
builds the identical geometry and is not needed. A **single layer cannot** hold both: a cell keeps one
`(Top, Floor)` per layer and a taller add replaces it outright, so the same footprint at two floors answers
480 cells, 0 stacked, `[16,22)` — the lower shape erased rather than stacked.

What fails is everything downstream of the geometry, and four of the entries below share one cause: **a column
segment does not carry the layer that produced it.** `SketchRasterizer` iterates layers and drops the id on
the floor (`foreach (var (layout, _) in ResolveLayers(state))`); every read after that is guessing.

- [ ] **TS22 — A column segment carries its layer.** `SketchRasterizer.RasterizeColumns` answers
  `(X, Z, YFloor, YTop)`; make it `(X, Z, YFloor, YTop, Layer)`. Sixteen call sites, all mechanical. On its
  own it changes no behaviour, which is the point: it is what the three entries under it each need and none
  of them can be done without it.

  *The layer id exists one line up and is discarded at the `_` in `ResolveLayers`.*

- [ ] **TS23 — Paint every exposed surface, with its own layer's theme.** Two independent blockers, both
  readable from a signature. `TerrainBuilder` collapses a cell to its **maximum** top and `TerrainPainter`
  walks down from that one height, so nothing under the highest slab is ever visited. And
  `SketchRasterizer.ShapeThemeOwners` answers `(x, z) → shapeId` over every layer at once — smallest area
  wins, ties to the first claimer — while `themeAt(x, z)` has no Y to tell two layers apart with. Make
  `SurfaceTop` one entry per segment and key the owner map on `(layer, cell)`.

  *The mineshaft's meadow deck is painted **andesite where a wall stands under it and gravel where the gallery
  runs** — a map of the layer below, cell for cell, because every mine-level shape is smaller than the deck
  that roofs the whole board. Its gallery floor at `y 1..3` and the walls facing into it are raw stone. The
  meadow theme lands on no block anywhere.*

**Settled by the author:** a depression or a river in the upper layer does **not** bleed into the layer
beneath it. That is the default; letting it cut through is the toggle, not the other way round.

- [ ] **TS24 — Warn where two layers fuse.** A shape's bottom is flat inside its layer, so a cell's segments
  are intervals and the check is a sort and a scan of the rasterizer's own output — about fifteen lines.
  Complain where two segments **overlap**, and where the gap between them is under `Walk.Headroom`. Flush is
  the healthy case and must stay silent: the rock mass meeting the deck underside is how a roof is built.

  *On the mineshaft: 0 overlapping, 11,600 flush, 528 separated with a thinnest gap of 14.*

- [ ] **WS12 — A read-back can be asked for one layer.** Every `render/*` route is whole-world. The word
  `layer` on `render/topdown` names a **category** — `ground` · `structure` · `foliage` · `objectives` — not a
  storey, and two things sharing a name and not a meaning is a name to fix. The only cut a caller has is
  `ymax`, a single height, which separates two storeys just where one lies flat over the other. Add a layer to
  the claim and a query word to the four reads that project a column to one cell — `topdown`, `heightmap`,
  `surface`, `structures`. Wants `TS22`, and shares `C48`'s second half: `WorldProvenance` keys
  `(X, Z) → (Layer, Owner)`, one claim per column with no Y in it, so even the structure read cannot tell a
  hall under a terrace from the terrace.

  *On the mineshaft the deck roofs all 6,400 cells, so every top-down read draws the deck alone; the gallery
  at `[0,4)` is reachable only through `ymax`, and only because that deck happens to be flat at `y 20`.*

- [ ] **C48 — Toggle a layer in the 3-D view.** `WorldColumnPayload.Of` reads the finished `VoxelWorld` and
  emits runs of blocks with no idea which layer made any of them, so the preview cannot hide one. Carry a
  layer index per run and the toggle is client-side filtering in `sketch-canvas`'s column mesh. Wants `TS22`
  first, and wants a provenance that records a layer rather than only a pass.

- [ ] **TS21 — One cell per column is why a stacked board reads wrong.** `WorldColumns.Membership` projects a
  column to one cell and the walk stands a player on the **lowest** surface carrying headroom, so a stacked
  board is read entirely on its floor. Neither the coverage read nor traversability can say a board has two
  storeys, let alone which one the match is played on.

  A column with two standable surfaces holds two places, and the walk has to carry both plus the ways between
  them: a ramp is an edge from one storey to the other, and a sealed mine has no edge and is genuinely
  separate. Preferring the upper surface is the same bug with the sign flipped.

  *`opus5-undercroft` at `(0, 48)`: the walk stands at **15**, the hall floor, while the terrain top is
  **28** — every distance that board reports is measured through a nine-block undercroft. On the mineshaft the
  same rule sends a player down the mine and up the ramp and reports **no surface route across the middle of
  the board at all**, though 6,400 of 6,400 columns read as passable.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under every footprint cell it fills, per layer, so a
  bridge slab across a strait drops its own plate into the abyss and an overhanging deck does the same over
  whatever it hangs past. The theme's `bedrock` value does not reach it — the painter only overwrites
  stone. Condition the course on the cell having no lower segment.

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

- [ ] **TS26 — Decide whether one layer may hold two segments (deferred decision).** `MergeCell` keeps one
  `(Top, Floor)` per cell per layer and a taller add replaces it outright, so two shapes on one footprint at
  different floors answer one segment and the lower is erased. Making it keep disjoint segments would let a
  single layer stack — and would leave `base_y`, per-layer relief and per-layer theming with nothing to be
  for, since a layer would no longer be a slab. **The blocking question is whether a layer is a slab or a
  namespace**; nothing below should be built until that is answered, because `TS22`'s layer id means a
  different thing under each reading.

  *Measured both shape orders: 480 cells, 0 stacked, every column `[16,22)`.*

- [ ] **TS25 — Write the layer model down.** `docs/tools/sketch.md` gives a layer five lines and closes with
  "an agent should author the ground layer only", which is advice rather than a description. What a reader
  needs and cannot get without three probes and a source read: a cell keeps **one `(Top, Floor)` per layer**
  and a taller add replaces it outright, so a roofed gallery is walls *clamped around* a tucked-in floor
  rather than a low shape drawn inside a tall one, and it takes two layers rather than two floors; `floor` is
  the underside measured inside the layer, `base_height` is the thickness above it, and `base_y` shifts the
  whole thing; `anchor_heights` slopes a shape between two layers; relief solves per layer and returns already
  shifted into world Y; the set algebra is `((adds − subs) ∪ override-adds) − override-subs`, **by category
  and not document order**, which is the only reason a `subtract`-carved gallery needs its ramp put back as an
  `override`; and the stone-only invariant in `TerrainPainter.Paint` is the one line that lets an air gap
  survive between two slabs.

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
