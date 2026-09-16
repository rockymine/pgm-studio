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

- [~] **G264 — The composer seats a wool room its own lane-width from the hub, so a goal's setback is a cell
  count and not a distance.** Measured: on a composed board the gap `WL12` names is the **inside corner of a
  bent wool** — `wool-a-room` sits at the far side of a 2-cell lane, so the void between the room and
  `hub-t1` (or `frontline-t1`) is exactly the lane width, 10 blocks at cell 5. `p20 rot_180 seed 3`:
  hub `z[35,65)`, room `z[75,85)`, lane `wool-a-t1 z[65,85) x[-25,-15)` running beside the notch. The author's
  floor is 16 blocks from the room to the frontline, so the **room's setback along its lane** takes a block
  basis — `WoolBoxEmitter`, not the seat clearance. `docs/generator/rules.md` and `model.md`.

  *The seat gap (`UnitSeating.cs:153`, now `seatGapCells`) is a different quantity and was tried first:
  giving it a 16-block floor holds 30/30 yield at 12, 20 and 30 players but **refuses every 8-player board**
  — `ceil(16/5)` is 4 cells of clearance and an 8-player hub has no edge that holds it. 66 of 72 fingerprints
  moved. Reverted; the width/separation split it needed is kept. Whatever lands here must be measured at
  **8 players first**, which is where the budget is tightest, and the same run must decide whether the wool
  lane stays `w2` — the model states it does — while the room moves further out along it.*

## Nothing else is on the board

The walk drained, and so did what a building may stand on — `DR-PASS` is the author's ruling, the passage is
owed round a group of buildings, and `sketch/seats` answers it forwards (`FEATURES.md`).

`docs/backlog-strategy.md` names **the layer word** as the programme to pull next: `B263`, `B264`, `WE28`,
`TS64`, in `BACKLOG.md`. Three of the four want something first — `B263` a ruling on how the canvas says which
storey a prop is on, `B264` and `TS64` a surface — and `WE28` is the one that is settled and backend only.
