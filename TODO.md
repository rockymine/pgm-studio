# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Seven entries, all about a stack of layers.** What is left of the group five authored maps
(`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not touch: a board whose ground
is stacked, where a hall runs under a terrace and every read projects the column to one cell. A mineshaft
built under a meadow is the worked example, and it is committed on this branch.

## Still open — one, and it is a naming call rather than a decision

| | the question | blocks |
|---|---|---|
| **Q8** | The three type renames are settled and the scan family's own word — *segment* — is already the right one, so what is left is its borrowed `layer_` prefix and the public route `GET /map/{slug}/layers`, whose response says which *artifacts* a map holds. **The proposal, absent an objection:** drop the prefix (`SegmentIndex` keeps its name, `layer_segment` becomes `segment`, `LayerExtractors` becomes `SegmentExtractors`), and the route becomes `GET /map/{slug}/state` over a `MapArtifacts` record, which is what it already returns. | `WS13` `P7` |

The word collides there and nowhere else because **a scanned cave and a stacked sketch are the same geometry
seen twice**, which `docs/tools/sketch.md` § Layers now says whichever way `Q8` goes.

## A stack of layers: what it writes, and what no read can say about it

**A layer is a slab.** One layer holds exactly one segment per column — a `(Top, Floor)` pair — and a taller
add replaces it outright, floor included. Stacking inside a single layer is not a thing an author may do: two
shapes on one footprint at two floors answer 480 cells, 0 stacked, every column `[16,22)`, the lower shape
erased. A stack is a stack **of layers**, and `base_y`, per-layer relief and per-layer theming are what a slab
is for. *(Settled by the author, and written down in `docs/tools/sketch.md` § Layers.)*

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

**Two layers meeting flush is how a roof is built, and a sealed room is an author's choice** — a hidden
space holding something useful is a thing someone may want, so neither is a complaint, and neither is a gap
too low to stand in. What is worth one is **standable ground under open sky that no route reaches**: a slab
floating over the board rather than a storey of it. Even that is a complaint and never a refusal, because two
flying islands stacked may be exactly what was drawn. The line is roofed against open — a pocket inside a
layer is a room, a mass with sky above it is an island. *(Settled by the author.)*

**An objective in a place nothing reaches is a different matter, and `EX1` already refuses it.** That rule
names the isolated spawn or objective and stops the export; it needs no amendment, only a walk that can see
storeys. *(Settled by the author.)*

**A layout is composed of layers, and the ground is one of them.** The document holds `layers[]` and
nothing beside it: a flat board is a stack of one, and `layout` as a peer key — the ground shapes sitting
outside the stack they belong to — stops existing. *(Settled by the author.)*

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

What fails is everything downstream of the geometry, and four of the entries below shared one cause: **a
column segment did not carry the layer that produced it.** That is settled — a segment is a `ColumnSegment`
carrying its layer id (`TS22`, `FEATURES.md`), and what is left below is what each read does with it. Under
it sat a second cause nothing had named:
seven places stated what a document's layers were, in three different readings. That is settled: the
document holds `layers[]` and nothing beside it, and `SketchLayout.Stack` is the one reader (`TS28`,
`FEATURES.md`). What is left below is what the layer id is *for*.

**One entry owes a document its prefix does not name.** `TS21`'s standing rule is written in
`docs/world-scan/read-backs.md` § *Where a player stands in a column*. The id stays — it is already cited in
commits — and the entry fixes the document it actually moves, alongside `docs/tools/sketch.md`.

- [ ] **WS13 — Free the word `layer` in the scan family and on the route.** The three type renames landed
  (`FEATURES.md`); `SketchLayer` stays, being the one use where the word is true. What is left is what `Q8`
  decides: the **scan-segment family**, whose own word — *segment* — is already right, so only its borrowed
  prefix is in question (`LayerSegments`, `layer_segment`, `LayerExtractors`, `LayersEndpoints.cs`,
  `LayerParquet`, `scan_layer`, `PgmStudio.Analysis.Layer`), which `P7` also spends; and
  **`GET /map/{slug}/layers`**, whose response says which *authoring artifacts* a map holds while `layers[]`
  in the sketch document means slabs — the collision itself. Both cost a migration and nothing else.

  *Also carrying it and deliberately untouched: the canvas z-stack (`render/layer-stack.js`, `data-layer`),
  which is the graphics term and reads as one; and `LayeredMaterial`'s own `layers[]` inside the theme JSON.*

- [ ] **TS24 — Complain where a mass connects to nothing, and where two segments overlap.** Three of the
  four things this entry once proposed are settled *not* to be complaints — flush is how a roof is built, a
  sealed room is an author's choice, and a gap too low to stand in is that room. Two findings remain, and
  they are different mechanisms.

  **Overlapping segments** is a geometry error whatever was meant: two shapes claiming the same Y in one
  column. A shape's bottom is flat inside its layer, so a cell's segments are intervals and this is a sort
  and a scan of the rasterizer's own output, about fifteen lines, needing no layer id.

  **Standable ground under open sky that no route reaches** is the warning — `EX5`, a **complaint and never
  a refusal**, because two flying islands stacked may be what was drawn. Roofed ground stays silent: that is
  the room. It asks whether a player can get there, so it wants `TS21`'s places. `EX1` owns the other half
  already — it refuses an isolated *spawn or objective* by name — and needs no amendment, only a walk that
  can see storeys.

  *`opus5-mineshaft`: the walls run `[0,25]` flush into the deck, the gallery keeps `[0,3]` · `[20,25]`
  sixteen blocks below, and the adit is the one connection between them. `EX1` passes on this board because
  the traversability read projects the column to one cell.*

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

- [ ] **WE24 — A placement names its layer.** Everything the export puts down resolves its Y from an
  `(x, z) → int` grid and so lands on the top slab whatever the author meant: an objective, a spawn, a room
  and a wall through `PositionSnap.SurfaceYOver`, and every prop through `DressingContext.SurfaceTop` at ten
  sites in `Decorator` plus `DressingScope`. **Two readers, and the layer word has to reach both** or a tree
  and a monument in one hall disagree about where the hall is. The word is optional and defaults to the top
  layer, so nothing already authored moves; a placement naming a layer its cell has no segment on is a
  `Decline` — the map builds and that one thing is not in it.

  *`opus5-undercroft`: "I stated a tree for the hall floor and got it on the roof, which was the point of
  stating it." The same grid put its destroyable on the terrace by itself.*

- [ ] **C48 — Toggle a layer in the 3-D view.** `WorldColumnPayload.Of` reads the finished `VoxelWorld` and
  emits runs of blocks with no idea which layer made any of them, so the preview cannot hide one. Carry a
  layer index per run and the toggle is client-side filtering in `sketch-canvas`'s column mesh. The claim has to be keyed on
  the segment rather than on `(X, Z)`, or a run cannot be attributed — that half is still open.

- [ ] **WS12 — A read-back can be asked for one layer.** Every `render/*` route is whole-world, and the only
  cut a caller has is `ymax`, a single height, which separates two storeys just where one lies flat over the
  other. Add a layer word to the four reads that project a column to one cell — `topdown`, `heightmap`,
  `surface`, `structures`. `traversability` and `walk` project too and are **not** here: they want `TS21`
  rather than a layer word. Wants the provenance claim keyed on the segment (`C48`'s half) and `WS13` to have
  freed the word first.

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
