# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**The board is at its cap and takes nothing new until a group drains.** Two groups plus one entry
finishing, ten entries. They are here together because five authored maps
(`pgm-studio-mapgen/reports/opus5-*`) put the same question to both: *what can an author ask the studio
before it is too late to act on the answer.*

**The first group carries a model rather than a list**, and the model is the author's, stated this session.
It is written into the heading because eight entries in `BACKLOG.md` read one field of what it returns and
two of them said so outright — `S47`'s detour factor and `B169`'s dead share both waited on it. The rest:
`WS1` (the same clearance read), `WS3` (the same traversal from more origins), and `B175` and `B179`
(goal walks, whose bands are now `GO2` and `GO3`).

## The walk: one model, two fidelities, answered in blocks

The model is the author's and it is built: **one traversal, two fidelities, four answers in four units,
nothing weighted** — reachable, distance in blocks, blocks *placed* (a rise of Δ costs Δ−1, void bridged one
a cell), and drops (free to 3, counted beyond it, because a water bucket makes a fall free of damage and not
of time). Eight-connected, because a player walks diagonally; water a speed rather than a weight, at about
×2 and only at the built fidelity; and a third aim, `Comfort`, buying standoff from an edge inside the
ten-block allowance a corridor is already claimed with. It is written out in `docs/world-scan/read-backs.md` and its types are in
`docs/generator/vocabulary.md`.

**Every distance in the studio now asks it**, and the weighted step cost that stood beside it is gone: one
traversal, two fidelities, three aims, nothing to calibrate.

**And `match-flow.md` §6.12 is right about something else.** It says a climb weight cannot be fitted to
recorded play because half the standing samples sit on structure the players built, a median 14–17 blocks up.
That is a fact about *what players do with a map* — the scaffolding at build height by minute thirty — and
this model is about **what the ground offers**. The two are different levels of movement and §6.12 does not
bear on this one.

- [ ] **WE21 — A path prop says whether it is a route or paint.** `Decorator.PlacePath` claims every cell
  it lays as `ClaimKind.Route`, and `DR-ROAD` then keeps a tree three blocks off any claimed cell
  (`TreeProp.RouteStandoff`) and a boulder two. The three is reasoned on the type and it is reasoned
  *about a road* — "nearer and the canopy closes over the route, which stops reading as a road through
  trees and starts reading as trees in the road" — which is right for a road and meaningless for a paint
  stroke. A path already repaints the top block and adds no cell, so it **is** a brush; the rule is being
  asked of the wrong prop.

  One field on `PathProp` naming what the stroke is, and `PlacePath` claiming `Route` only for the first.
  A checkbox on the path inspector, and the paragraph in `decoration.md` that says what a path claims.

  *Not `B249`. A waiver is for a gate that is right and an author who is deliberately off the norm; this
  gate is wrong about what it is looking at, and waiving it hides that for every later board.*

  *`opus5-elderwold` wanted a painted forest floor and could not have one. `opus5-hollowmarch` shows the
  accidental form: twenty-one path props over a 110 × 220 board — fourteen of them grass tongues over
  crags, none of them roads — left **11 plantable cells** on the whole map.*

- [ ] **TS20 — Probe a footprint before a shape is built on it.** There is no way to ask whether a ring
  stands on land, so the authoring runs rebuilt the land model outside the studio from the compiled shapes
  and their own polygons. It disagreed with the **rasterised** coast, which is the only one that matters.

  `POST /map/{slug}/sketch/probe-footprint` taking a ring and answering land · sea · hole against
  `SketchRasterizer`'s own footprint. The hard case is the third word: a composed board's holes — a
  double-hole hub's two slots, a U wool's notch — are made by **arrangement**, so no region marks them and
  an add-shape dropped on one fills in the layout the composer was asked for, with nothing declined. The
  predicate that works is a void cell with land in all four directions within 16 blocks.

  *`opus5-hollowmarch`: `scar-hub-w` overhung the sea by two cells, and a `raise` with no ground to read
  past the coast fell back to the shape's `floor` — seven-block cobble stubs at y0..y6 at **(−32, 70)** and
  **(−24, 70)**, in open void beside the island, declined by nothing.*

- [ ] **WE20 — Dry-run the dressing.** A prop's placement can be refused and never previewed: the resting
  cell, the resolved Y and the decline are all computed inside `Decorator` and reachable only by building
  and exporting a world. `PathStroke.Cells` — which respects style, coverage and seed, and decides every
  `DR-ROAD` distance — is reachable from nowhere at all.

  Answer the dressing document with, per prop, the cell it rests on, the Y it resolves to and the
  `Finding` it would draw, using the same pass and stopping before the export. The claim mask is worth
  returning beside it, since that is the half a keep-out is computed against.

  *Two rebuild cycles on `opus5-hollowmarch`: a keep-out model tuned to the wrong distances drew 13
  declines; retuned the other way it left 11 plantable cells. Neither number is a fact about the board.*

- [ ] **WS10 — Name the places a readback counts.** `relief/read` reports the place count, the largest
  place's share and the ledge count — enough to know something is stranded, not enough to know what. The
  flood is already computed inside the readback and thrown away; a centroid, a cell count and a bounding
  box per place is the whole change.

  **Rides with it:** `render/section` answers `200` and a blank PNG when `at` is off the world rather than
  refusing, which is against the `RQ1`/`RQ2` split `B214` established — a request that cannot be honoured
  should say so.

  *`opus5-cairnmeadow` read **places 3**, largest 0.95: the missing 5% was the quarry floor, droppable into
  and not walkable out of. `opus5-hollowmarch` read **places 7**: one was a wool-room pad of 100 cells that
  nobody could walk onto. Both were found by guessing and confirming with a column transect.*

- [ ] **TS21 — Write layers down, then make a stack readable.** `docs/tools/sketch.md` gives a layer five
  lines and closes with "an agent should author the ground layer only", which is advice rather than a
  description. Two halves, and the first is a day's prose against a feature that otherwise costs three
  probes and a source read to use: the **stone-only invariant** in `TerrainPainter.Paint` is the single
  line that lets an air gap survive between two slabs; `floor` is the underside, measured inside the layer;
  relief solves per layer and returns already shifted into world Y; and `TerrainBuilder.SurfaceTops` keeps
  the **maximum** top per cell, which is why a placement climbs onto the upper layer by itself, a path
  paves the deck, the covered ground is unpainted (`B144`'s cause, one tier up) and nothing can be dressed
  underneath.

  The second half is real work and builds on the walk: **no read can say whether a player can
  get under an overhang**, because `WorldColumns.Membership` projects a column to one cell and
  `relief/read` walks a height field. A layered board's correctness is exactly the question its reads
  cannot ask.

  *`opus5-undercroft` carries a nine-block hall under a terrace, entered at z 58 and open over a void court
  at z 40. `traversability` calls the board one component and says nothing about it; `coverage` counts its
  cells as the terrace's.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under every footprint cell it fills, per layer, so a
  bridge slab across a strait drops its own plate into the abyss and an overhanging deck does the same over
  whatever it hangs past. The theme's `bedrock` value does not reach it — the painter only overwrites
  stone. Condition the course on the cell having no lower segment.

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

- [ ] **WE23 — A line mark's `width` is a radius.** `LineMark.Pins` keeps a cell where
  `distance <= Width`, so the band is **twice** the number; the same quantity on a path prop is called
  `Radius` and its docstring says "half the paved width". `relief.md`'s mark table says only "a band along
  a polyline" and its worked example passes `"width": 7`. Rename the field, or state the reading in the
  table — `CLAUDE.md`'s own rule covers it: a name must not promise the wrong category.

  *`opus5-hollowmarch`: `front-bank`, a line at z 50 with width 12, wrote from z 38 to z 62 over the
  frontline flat; a push stacked on it and left a seven-block wall across the necks of both launch prongs.*

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
