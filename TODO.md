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

- [~] **TS171 — The plan's rules read the sketch.** A places document's targets are measured off the ground the
  sketch builds, as `SketchSeamCheck` measures the intent's pieces. `sight` is the one new read. `PlanValidator`
  is the source each rule moves out of. `docs/tools/sketch.md`.

- [ ] **TS189 — A room's doors are stated, drawn and placed by hand.** One door shape for both rooms — the
  edge a wall faces and the block along it — replaces `SpawnIntent.Doors` (edge words) and `WoolIntent.Entries`
  (segments), with a migration (dry run first) carrying each spawn door to its wall's centre and each entry to
  its midpoint. A door may stand on any wing's outer wall; `Doorways` already finds the wall facing its edge
  that holds its point. A room stating none gets one door, on the side of its region meeting the most ground,
  ties to the side nearer the symmetry centre — not one per wall (`WorldBuilder.WoolEntries`). In Layout's
  house view doors are drawn on the outline, and shift-click on an outer edge adds or removes one there
  (`room-house-controller.js`, a `PUT …/intent/pieces/{id}/doors`). `WL11` and `ST11` read the doors as a wool's
  entries, and a wool door with no ground before it is a complaint. `docs/tools/sketch.md`,
  `docs/world-export/structures.md`.

- [ ] **RP131 — The plan retires as a level.** The Plan tool, `PUT …/sketch/from-plan` and the `plan` stage go;
  every stored plan stays as read-only history (D9), with the migration carrying no row away. `drive.py`, the
  `pgm-board` skill and `ORDER-OF-WORK.md` move to sketch sources; no plan spec moves (D11), and `POST
  /plan/compile` stays as the one-way import.
  `docs/tools/flow.md`, `docs/tools/plan.md`.

