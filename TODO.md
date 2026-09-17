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

## The composer states distances in cells, and a grid scale moves them

- [~] **G264 — The composer seats a wool room its own lane-width from the hub, so a goal's setback is the
  band's corridor and not a distance.** On a composed board the gap `WL12` names is the **inside corner of a
  bent wool**: `wool-a-room` sits at the far side of the wool lane, so the void between the room and `hub-t1`
  (or `frontline-t1`) is exactly that lane's width — 12 blocks at nano and 16 at milli and centi on the cell-4
  grid, against the author's floor of 16 from the room to the frontline. The **room's setback along its lane**
  takes a block basis of its own, in `WoolBoxEmitter` rather than in the seat clearance.
  `docs/generator/rules.md` and `model.md`.

  *Evidence: `p8 rot_180 seed 0` at nano, wool lane 3 cells — `wool-a-room x[-1,1) z[20,23)` stands off
  `hub-t1 x[-6,6) z[14,17)` by 12 blocks, the lane's own width and four under the floor. Nano is where it
  binds: a 16-block floor is 4 cells against a 3-cell lane, so the room has to move rather than the lane widen.*

## Nothing else is on the board

The walk drained, and so did what a building may stand on — `DR-PASS` is the author's ruling, the passage is
owed round a group of buildings, and `sketch/seats` answers it forwards (`FEATURES.md`).

`docs/backlog-strategy.md` names **the layer word** as the programme to pull next: `B263`, `B264`, `WE28`,
`TS64`, in `BACKLOG.md`. Three of the four want something first — `B263` a ruling on how the canvas says which
storey a prop is on, `B264` and `TS64` a surface — and `WE28` is the one that is settled and backend only.
