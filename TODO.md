# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Twelve entries, at the cap.** Eleven of them are one group and the group is one cause, so splitting it
across two files would hide the cause; the board **takes nothing new until that group drains**, and anything
found while working goes to `BACKLOG.md`.

## What a board cannot be told, and what it cannot be asked

Eleven boards were authored through `pgm-studio-mapgen/tools/drive.py` across two branches, and the driver
is the record of what the studio does not answer. It carries **fourteen keys of a finish document** and
forty-two lines of spline geometry, under a docstring that says it computes nothing.

**Six of the fourteen are not a gap.** `themes`, `relief`, `roomStyles`, `dressing`, `addShapes` and
`addLayers` are ordinary sketch-document content, and `POST /map/from-documents` takes that document whole —
a headless author states them because a headless author has no canvas, not because the studio cannot hear
them. The other eight are a patch onto a compiled layout, and they sort into two halves this group is ordered
by. **What a board cannot be told** is the first five: a statement an author has to make, with no field to
make it in, so the driver edits a compiled document on its way past. **What a board cannot be asked** is the
six after: a question whose answer is inside a refusal, a solver or a palette and which no read returns — or
one a read does answer in a field no driver printed, which is the same silence from the author's seat.

The pattern under all of them: **not one of these faults was caught by a gate.** The export gate was open,
the mirror clean and the traversability whole on every board named below.

### What a board cannot be told

- [ ] **TS32 — The compile fuses terrain by surface height, so a plan's piece names do not reach the
  layout.** A plan states named rectangles; `PlanCompiler` fuses every abutting run of them at the same
  `base_height` into one polygon and the names are gone, so nothing downstream can address a piece. Carry the originating
  piece ids on the compiled shape (`pieceIds: ["room-w", "ledge-w"]`), or emit one shape per piece and let
  `RasterGroup` fuse at raster time. Either one deletes four of the driver's fourteen keys —
  `themeByHeight`, `themeById` and both `shapeProps` — which exist only to address the accident. Lands in
  the compiled `SketchShape` and in `docs/tools/sketch.md` § Shapes; `docs/tools/plan.md` § *what it
  compiles to* changes in the same commit.

  *`opus5-thornfell`: sixteen pieces → one polygon, `s0`. `opus5-rimegarth`: fourteen → one. `opus5-kiln-row`:
  seven pieces at three heights → four shapes. Shape count tracks height and adjacency, never the piece.
  Downstream cost: **five specs
  carry a private "is this point on land" predicate** rebuilt from the plan's own rectangles, under five
  names — `on_land`, `in_rects`, `land_halfwidth`, `cells_open`, `on_ground` — because the layout cannot
  answer it.*

- [ ] **TN7 — A plan cannot say which storey a goal stands on.** `layer` is a field on all six `MapIntent`
  placements and on `PlacedProp`, and on neither `DestroyablePlacement` nor `CorePlacement`. So the word
  exists everywhere the export reads it and nowhere the plan writes it, and a plan-built goal on a stacked
  board always resolves against `SurfaceTop` — a monument stated for a basement lands on the deck roofing
  it. A nullable string on the two placement records, carried through the compiler. Small.

  *`opus5-interchange` needs the word on four of its five goals a team, and gets it by having
  `tools/drive.py` write `layer` onto the compiled intent by `stamp.unit` before it is stored. Confirmed
  against `GET /api/openapi/v1.json`: the property is on the intent schemas and absent from both plan
  placement schemas.*

- [ ] **WE30 — `TerrainPainter.Paint` trusts document order for a stack that knows its own floors.** The
  painter walks `SurfaceByLayer` in document order and each pass paints its whole column from the bedrock
  course up, so a storey listed *after* one that stands over it finds no stone left to paint. A compiled
  plan emits `layers[0] = ground`, and the compiled ground is not the bottom of every board. Sort ascending
  by each layer's own floor instead: document order is an authoring accident on any board whose ground came
  from a compile. `docs/world-export/terrain-painting.md` states the per-layer scope and does not state that
  the painting is ordered.

  *`opus5-interchange` at `(20, 70)`: yellow stained glass from `y0` to `y25` in one column — a 2×4 door
  panel painting twenty-six courses — because the undercroft slab was appended rather than inserted. After
  moving it to index 0: white clay `y5..3`, hardened clay `y2..1`, corridor brown from `y12`.*

- [ ] **TS30 — An organic coast is either stated twice or computed outside the studio.** The compiler emits a
  staircase of the plan's rectangles, which is the board's *shape* and not its *coast*. `opus5-ravensmere`
  redrew its ring by hand in the finish — the coast stated once in the plan and once beside it, free to
  disagree; `opus5-thornfell` bent the compiled ring instead, which moved the disagreement into forty-two
  lines of Python. `PgmStudio.Geom.RingRounding` already fits the Catmull-Rom handles the driver re-derives —
  and has no caller in `src`, only a test — so the missing thing is a *stated* bow: a seeded, deterministic
  inward wander over a resampled outline, taken at compile or as a sketch operation, handed to `RingRounding`
  rather than to a second copy of it. Two rules make it safe and belong with it — the plan's own vertices
  never move, and nothing ever moves outward.

  *`opus5-thornfell`: 36 compiled vertices → 99 drawn, strait measured at **26–28 blocks** over 23 transects
  against a plan stating a flat 30. A vertex moved outward would have closed it.*

- [ ] **RP58 — The export zip nests the world inside a folder named for the slug.** `--out` is what a server
  is handed and holds `region/`, `level.dat` and `map.xml` at its top, so every caller unwraps it. Emit it
  flat — and say what the browser download becomes, since the same bytes are served as `{slug}.zip` and a flat
  archive unpacks loose into whatever folder it lands in. `MapExportEndpoint.BuildWorldZip` prefixes every
  entry with `{slug}/`, in one place.

  *Both mapgen branches independently wrote the same un-nester into `tools/drive.py`; the merge kept both
  and they ran in sequence until one was deleted.*

### What a board cannot be asked

- [ ] **WE31 — A stamped room's foundation is bedrock to `y 0`, and no gate reads its sides.** A wool room
  fills its whole piece and fills downward in bedrock, so wherever the cell beside it is void or lower that
  plinth is a sheer bedrock wall a raider cannot climb — and nothing anywhere says so. Raise a complaint at
  the columns tier when a stamped room's perimeter cell is void, or more than a step below its floor. The
  gate already exists in shape: `DressingRules.SiteNotLevel` (`DR-SLOPE`) asks the same question of a house.
  `docs/world-export/structures.md`.

  *`opus5-thornfell`, first build: room floor at `y43` over **42 courses of bedrock**, with `x −93` and
  `x −69` void at `z 117`. Export gate open, mirror ✓, buildability ✓, traversability ✓, nothing declined.
  The only read that showed it was a hand-typed column.*

- [ ] **WE32 — A push has two gradients and the read-back reports neither.** A push climbs at `amount /
  falloff` over its skirt and at `crown / half` from the ring's edge to its medial axis, and where those two
  disagree the landform has a step at its own outline — a cliff with a hill on top of it, whatever its height.
  `relief/read` reports the *face* — `faces` carries its facing, width, drop and whether it qualifies as a
  cliff — but says nothing about which push made one, and neither gradient is ever stated, so an author
  holding a step has no way back to the knob that cut it. Answer each push's two gradients, and lint where
  they differ by more than about 2×: that ratio is the number an author is actually choosing.
  `docs/world-export/relief.md` § the push.

  *`opus5-thornfell`: `amounts 22–36 · falloff 12 · crown 16` sections as a vertical face; the same range at
  `13–17 · falloff 10 · crown 12` — both gradients ≈1.7 — sections as a mountainside. Both read identically
  through the five fields the driver prints (`cells`, `low`, `high`, `relief`, `symErr`) of a response
  carrying twelve.*

- [ ] **WE33 — Two flat marks with touching radii build two terraces and a step at the seam.** A mark pins
  every cell in its radius exactly; the relaxation, which is what makes ground roll, owns only what is left
  *between* marks. So two marks placed to describe one slope describe a wall instead, and the wall is reported
  only as terrain — a `steps` bucket and a face, attributed to nothing. Have the relief read answer the worst
  step between any two marks' pinned sets, naming the pair. The neighbouring case belongs with it: **a mark
  placed wholly out of reach does nothing and raises no `SK3`.** `docs/world-export/relief.md`.

  *`opus5-tarnfell`: `shore-lo` at `y8` and `shore-hi` at `y14`, radii touching, transect
  `…7 7 7 7 [+5] 12 13 13…` — a five-course wall right round the lake. Pulled seven blocks apart:
  `7 7 7 7 9 11 12 13 13`.*

- [ ] **WE34 — Nothing answers "where may this stand?" — only "no".** Five declines exist — `DR-SITE`,
  `DR-KEEP`, `DR-CLAIM`, `DR-ROAD`, `DR-SLOPE` — and every one is a filter on a placement already made.
  Run the same predicates forwards: one read taking a footprint and a prop kind and answering the candidate
  cells. Every predicate is already written. The *shape* of the answer exists three times for goals —
  `monument-suggestions`, `core-suggestions`, `wool-suggestions` — but each scores scanned candidate rows
  rather than asking a placement rule, so this is the first read that runs the rules themselves.
  `docs/world-export/decoration.md`.

  *Two agents wrote the same search independently rather than place by eye. `opus5-ravensmere` and
  `opus5-thornfell` got `yards()` — whole footprint inside a flat, outside every push's exclusion, twelve
  blocks off every track. `opus5-rimegarth` got its own after **fourteen declines** on one pass, and the
  honest answer for a hundred-block CTW board turned out to be **nine legal spots**.*

- [ ] **TS31 — A shape drawing ground outside every island is silent.** A one-course add on a cell no region
  shape covers is the only add on that column, so it builds a speck of bedrock standing over the void; a shape
  drawn wholly on the mirrored half is outside the compiled polygon and becomes an island of its own. Both
  pass the sketch PUT without a word — and `SK11` is the rule that should have said so.
  `SketchRasterizer.DetachedMasses` drops any component sharing no column with a second one (`// beside, not
  above`), so it reports a storey whose stair was never drawn and never an island standing *beside* the board,
  which is the case an author actually draws by accident. Report a component that is neither reached nor over
  anything, under `SK11` rather than a new id. `docs/tools/sketch.md` § Refusals and complaints.

  *`opus5-ravensmere`: `GET …/coverage` reported **141 cells at (−24, 91), 364 blocks from used ground** —
  a disconnected island made of paint. Nothing before that read mentioned it, and every spec now clamps and
  folds each stroke by hand to avoid both cases.*

- [ ] **WS14 — A declared route is never read back.** `route: true` on a stroke makes `DR-ROAD` measure
  every other prop's standoff to it, and nothing reads the stroke itself. `GET …/walk` answers a journey the
  walker chooses, which is not the one that was drawn. Add a read that walks a declared route end to end and
  reports its worst step, its material run, and where it leaves the ground. `docs/world-scan/read-backs.md`.

  *Every board verified its paths with a throwaway per-block column transect — `opus5-thornfell`'s four
  tracks, **308 cells**, checked for a step greater than one. Written fresh each time and thrown away each
  time.*

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
