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

- [ ] **TS169 — Play pieces are sketch objects that write the intent.** A spawn, a wool room, a monument, a core,
  iron, a build zone and a protection zone are placed, dragged and turned in the sketch like a house or a tree,
  and each writes its entry of `MapIntent` (`Spawns`, `Wools`, `Destroyables`, …), so moving one recompiles
  nothing. Configure's placement steps become the inspector of a selected piece. `docs/tools/sketch.md`,
  `docs/tools/configure.md`.

  *Evidence: a monument stated in a plan moves only through `PlanCompiler.Compile` and `sketch/from-plan`, which
  replaces every shape the plan made and answers 409 per relief it would orphan (`docs/tools/flow.md`).*

- [ ] **TS170 — The places layer and the design document.** `design_json` stored inside `PUT source` as numbered
  changes, with diff; places drawn with the sketch's own tools on a layer that never rasterizes, each with its
  kind, what, why, reach and own geometry, linking only to the intent object it holds; the review sheet as the
  layer's read-only view; note anchors on places, routes, targets and sections, and a verdict.
  `pgm-studio-mapgen/analysis/freeform-vs-studio/PLAN-REVIEW-DESIGN.md` is the design. `docs/tools/sketch.md`.

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

