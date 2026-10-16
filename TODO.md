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

- [ ] **TS180 — Places are stored with the map, in its one history.** A `places` document per map, written by
  `PUT /map/{slug}/places` and as a member of `PUT source`, each write a numbered change of the map's change log
  with its `because`, so its diff and restore are the History phase's. A place is an id, a name, a kind, its own
  geometry (point, box, ring or line, in blocks), a text and optionally the play piece it holds; the document may
  state a working area for an agent. `PLAN-REVIEW-DESIGN.md` §2 is the shape. `docs/tools/sketch.md`.

- [ ] **TS181 — Draw's Plan step, before Ground.** The Cameras step's layout: the board top-down in the middle
  over whatever ground exists, the notes step's point, box and lasso tools with polygon and polyline beside them
  to draw a place, the places listed on the left with what a change made new, and the picked place's thread on
  the right. The canvas states what a place holds and nothing else. `docs/tools/sketch.md`.

- [ ] **TS182 — Notes on places.** A note anchors to a place by id; Review's Cameras step draws the places as an
  overlay; a point, box or lasso note is tagged with the place its ground falls in. `docs/tools/sketch.md`.

- [ ] **TS183 — The plan's verdict.** `proposed`, `changes` or `approved` on the places document, one control in
  the Plan step; a convention an agent follows, blocking no write. `docs/tools/sketch.md`.

- [ ] **TS172 — The look is stated before the review.** A palette, a house style per area, tree and rock zones and
  the biome as sketch data, drawn on the review sheet and applied by the build, so the author judges what a board
  is made of before it is built (D12, phase 5). `docs/tools/sketch.md`.

  *Evidence: four of the seven points of Abbeymoor's first review were paint, materials or dressing, and no picture
  before its build showed any of them (`experiment/REVIEW-1.md`).*

- [ ] **WE166 — A room's building is a house.** A spawn's and a wool's `footprint` becomes a house record of the
  shape a dressed house takes — position, wings, its own style — with the room's interior as a part of it: the
  pad, the doors, the chests and the monuments, placed relative to the house. The map-wide `roomStyles` become
  the default a room's house starts from, and a migration carries every stored `footprint` into a one-wing
  house. `docs/world-export/structures.md`.

  *Evidence: the building is a bare `Rect? Footprint` on `SpawnIntent` and `WoolIntent` (`Pgm/Authoring/MapIntent.cs`)
  stamped with one style per kind (`SketchLayout.RoomStyles`), while a dressed house carries wings and a style of its
  own; a wool room's chests are derived at build time in its interior corners (`Minecraft/Stamping/WoolChests.cs`).*

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

