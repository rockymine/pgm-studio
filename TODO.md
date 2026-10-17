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

- [~] **TS171 — The plan's rules read the sketch.** The flow's measures (`LN5`, `CT14`) walk the sketch's
  ground through a `PlanNav` stated from it, as numbers on findings and never as a score (`SketchMeasures`); the
  seam and surface rules `ContactGraph` answers on the plan (`G2`, `G5`, `EL1`, `SP8`, `WL11`, the
  wall rules, `BZ5`, `BZ11`, `BZ12`) are rewritten on the raster's seams and relief; a wool room's entries are
  derived from the seams it shares with that ground, and a places document's targets are measured the same way. `sight` is the one new read. `PlanValidator` is the source each rule moves out of.
  `docs/tools/sketch.md`.

- [ ] **RP131 — The plan retires as a level.** The Plan tool, `PUT …/sketch/from-plan` and the `plan` stage go;
  every stored plan stays as read-only history (D9), with the migration carrying no row away. `drive.py`, the
  `pgm-board` skill and `ORDER-OF-WORK.md` move to sketch sources; no plan spec moves (D11), and `POST
  /plan/compile` stays as the one-way import.
  `docs/tools/flow.md`, `docs/tools/plan.md`.

