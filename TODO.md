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

- [ ] **TS188 — A house gains a wing the way an outline gains a point.** Hovering the middle of a free wall of a
  picked house shows the polygon editor's round insert marker; a click adds a wing along that wall, 5 blocks deep
  and the wall's length, picked, its corners dragged to resize — a drag never adds one. The same in Decoration for
  a dressed house and in Layout for a room's (`house.wings`), whose panel also lists the wings, deletes any but the
  room, sets a wing's storeys, roof and ridge, and picks the room's own style over the map's (`house.style`).
  Mocked at https://claude.ai/artifact/FNc15rnJMTFYqJJ7gbPTHk. `docs/tools/sketch.md`.

- [ ] **TS185 — A wall's ends drag apart, and a line snaps to shape edges.** A picked wall in Layout shows a
  handle at each end, and dragging one moves that end alone, its orbit images with it, the end snapping to the
  nearest shape edge within a few blocks. The piece route takes the moved end as a `part`.
  `docs/tools/sketch.md`.

- [ ] **TS171 — The plan's rules read the sketch.** The measures (`GO`, `WL`, `CT`, `LN`, `FR`, `MD`) read off the
  sketch's rasterized ground, as `StraitReadback` already reads `CT12`; the refusals (`PL*`, `WL14`, the pad
  fits) are asked between play pieces, and a places document's targets are measured the same way. `sight` is
  the one new read. `PlanValidator` is the source each rule moves out of. `docs/tools/sketch.md`.

- [ ] **RP131 — The plan retires as a level.** The Plan tool, `PUT …/sketch/from-plan` and the `plan` stage go;
  every stored plan stays as read-only history (D9), with the migration carrying no row away. `drive.py`, the
  `pgm-board` skill and `ORDER-OF-WORK.md` move to sketch sources; no plan spec moves (D11), and `POST
  /plan/compile` stays as the one-way import.
  `docs/tools/flow.md`, `docs/tools/plan.md`.

