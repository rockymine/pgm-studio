# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Six entries, all about a stack of layers.** What is left of the group five authored maps
(`pgm-studio-mapgen/reports/opus5-*`) opened is the question the others did not touch: a board whose ground
is stacked, where a hall runs under a terrace and every read projects the column to one cell. A mineshaft
built under a meadow is the worked example, and it is committed on this branch.

## Still open — one, and it is a naming call rather than a decision

| | the question | blocks |
|---|---|---|
| **Q8** | The three type renames are settled and the scan family's own word — *segment* — is already the right one, so what is left is its borrowed `layer_` prefix and the public route `GET /map/{slug}/state`, whose response says which *artifacts* a map holds. **The proposal, absent an objection:** drop the prefix (`SegmentIndex` keeps its name, `scan_segment` becomes `segment`, `SurfaceExtractors` becomes `SegmentExtractors`), and the route becomes `GET /map/{slug}/state` over a `MapArtifacts` record, which is what it already returns. | `WS13` `P7` |

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

**A placement names its layer, and absent a name it takes the top one.** *(Settled by the author, and
built — `WE24`, `FEATURES.md`.)*

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

- [ ] **WS12 — A read-back can be asked for one layer.** Every `render/*` route is whole-world, and the only
  cut a caller has is `ymax`, a single height, which separates two storeys just where one lies flat over the
  other. Add a layer word to the four reads that project a column to one cell — `topdown`, `heightmap`,
  `surface`, `structures`. `traversability` and `walk` are **not** here: the walk answers per storey already
  and a layer word would be a second way to ask.

  *On the mineshaft the deck roofs all 6,400 cells, so every top-down read draws the deck alone and the
  gallery is reachable only through `ymax` — and only because that deck happens to be flat. The committed
  `renders/topdown-mine.png` is that cut at `ymax=19`: it shows the gallery **and both spawn cubes**, which
  stand on the meadow at 26, because provenance keys a claim `(X, Z)` with no Y and the structure layer is
  drawn whole whatever the cut says.*

## What the front door still cannot say

- [~] **RP23 — `docs/tools/capabilities.md` is 707 lines answering "what can I ask for", which the API now
  answers itself.** The schema names every route, its body and its failure codes; `GET /api/rules` names
  every refusal with its fix; `GET /map/{slug}/state` puts the allowed moves on the map's own response.
  What prose is good at and this file is not organised around is the other half: **how to make a good map** —
  what an objective needs around it, what the corpus does — as against **what the system can be asked for**.
  Split it on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.

  The mapgen half landed: `pgm-studio-mapgen`'s six root documents became two, and `AUTHORING-BRIEF.md`
  points at the four self-describing reads instead of restating them. This entry is the studio's own side.

  *Five authored boards never opened it. What they read instead was `relief.md`, `decoration.md`,
  `terrain-painting.md` and the endpoint tables inside them — the split this entry proposes, observed.*
