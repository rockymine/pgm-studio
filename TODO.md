# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## The programme: bringing pgmvox into the studio
**The author put this programme first.** `docs/pgmvox-port.md` is the programme: pgmvox's capabilities move
into the studio one phase at a time, each proven against a pgmvox fixture, until a board built on pgmstudio.de
reaches the ceiling a pgmvox board does. Phase 0 has shipped and Phase 1 is open: the sketch becomes the map's one
document, and the box plan stops being a level (D8). Its entries run in the order below, and each leaves the
studio usable.

### Phase 1 — one document: the sketch, its play pieces and its places

- [ ] **TS175 — One way to move a play piece, and the room stored once.** `PATCH /map/{slug}/intent/pieces/{id}`
  moves or turns any spawn, wool, destroyable or core by its id and moves its whole symmetry orbit, generalizing
  `RoomPieceMove`; a copy broken out of the orbit is moved alone. The room rectangle stops being stored in the
  layout's structural shapes: the sketch, the paint scope and the relief pin read it from the intent, and a
  migration carries an author-corrected `height_authored` onto the intent first. `docs/tools/sketch.md`.

  *Evidence: a Configure edit writes only the intent, so the layout's copy of the room goes stale until the next
  `sketch/from-plan` (`PlanCompiler.AppendStructuralShape`, `sketch-bridge.js` `moveStructural`).*

- [ ] **TS169 — Play pieces are sketch objects.** Every spawn, wool room, destroyable, core, iron cube, build area
  and the observer is drawn in the sketch from the intent, selected, dragged and turned through TS175's route,
  with an inspector beside it and new pieces placed from the toolbar, reusing the dressing controller's grips and
  drag. `docs/tools/sketch.md`.

- [ ] **TS176 — Configure's placement steps become the sketch's inspector.** On a map the studio built, the
  spawn, protection, wool, destroyable, core and build steps are the inspector of a selected piece; on a map
  read from a world, detection and the monument-seat check stay Configure's own flow. `docs/tools/configure.md`.

- [ ] **TS170 — The places layer and the design document.** `design_json` stored inside `PUT source` as numbered
  changes, with diff; places drawn with the sketch's own tools on a layer that never rasterizes, each with its
  kind, what, why, reach and own geometry, linking only to the intent object it holds; the review sheet as the
  layer's read-only view; note anchors on places, routes, targets and sections, and a verdict.
  `pgm-studio-mapgen/analysis/freeform-vs-studio/PLAN-REVIEW-DESIGN.md` is the design. `docs/tools/sketch.md`.

- [ ] **TS172 — The look is stated before the review.** A palette, a house style per area, tree and rock zones and
  the biome as sketch data, drawn on the review sheet and applied by the build, so the author judges what a board
  is made of before it is built (D12, phase 5). `docs/tools/sketch.md`.

  *Evidence: four of the seven points of Abbeymoor's first review were paint, materials or dressing, and no picture
  before its build showed any of them (`experiment/REVIEW-1.md`).*

- [ ] **TS173 — A map's size is stated with its idea.** A size in Sketch Info, beside the symmetry, read by the
  places and the terraform; today Info says there is no size to set. `docs/tools/sketch.md`.

- [ ] **G301 — The generator emits a sketch.** The composer keeps its piece model (`Compose/Composer`) and ends in a
  layout and an intent rather than a stored plan, so a kept candidate opens in Sketch. `docs/generator/model.md`,
  `docs/tools/generator.md`.

- [ ] **TS171 — The plan's rules read the sketch.** The measures (`GO`, `WL`, `CT`, `LN`, `FR`, `MD`) read off the
  sketch's rasterized ground, as `StraitReadback` already reads `CT12`; the refusals (`PL*`, `WL14`, the pad
  fits) are asked between play pieces. `sight` is the one new read, and an in-game note is tagged with the place
  its ground falls in. `PlanValidator` is the source each rule moves out of. `docs/tools/sketch.md`.

- [ ] **RP131 — The plan retires as a level.** The Plan tool, `PUT …/sketch/from-plan` and the `plan` stage go;
  every stored plan stays as read-only history (D9), with the migration carrying no row away. `drive.py`, the
  `pgm-board` skill and `ORDER-OF-WORK.md` move to sketch sources, and which of the 174 plan specs move is D11.
  `docs/tools/flow.md`, `docs/tools/plan.md`.

