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

- [ ] **G267 — Every element width the composer emits is a cell count, so one grid emits one ladder of
  widths and the player count never reaches it.** `UnitTuning.WoolLaneCells = 2`, `FaceMinCells = 4` and
  `TeamUnitAllocator.cs:46`'s `laneWidthCells` (2, or 3 above 2500 land) are the whole width vocabulary.
  Measured over 132 composed boards, the tenth percentile of a board's local thickness is **exactly 10 blocks
  at every count from 5 to 40** — two cells at cell 5 — while the corpus runs 8 · 10 · 14 · 16 · 16 by mode
  from sub-nano to centi (`docs/world-scan/map-size-ladder.md`). The failures are opposite at the two ends:
  at 5 players the composed mode is 20 against the corpus's 8, at 32 it is 12 against 16. State the widths in
  **blocks** and divide by the cell where the grid is laid, which is also what makes cell 4 worth having —
  it reaches 8/12/16/20 where cell 5 reaches 10/15/20. `docs/generator/rules.md` `G2`, `model.md` §2.

## The budget: what a player count buys, and what it steers

- [ ] **G265 — The land budget's ladders saturate at 20 players and floor below 10, so most of the player
  range composes the same board.** Every consumer of `LandPerTeam` is a step function
  (`TeamUnitAllocator.cs:46-56`, `UnitTuning.HubCapCells`/`HubWideCap`/`WoolCount`), and the top rung of all
  of them is **3000** land while the bottom is **800**. `land/team` is 3500 at 20 players and 5920 at 32, so
  those two sit on identical rungs; 325 at 5 players and 664 at 8 sit on identical rungs too. Give the
  ladders a continuous basis, or rungs that reach the ends of the range. `docs/generator/rules.md`,
  `model.md` and `audit.md`'s ladder note (`G109` is the same ladders, as targets).

  *Measured, 24 seeds a row, `rot_180`, cell 5 — board extent and land built are the composed unit's own:*

  | players | land/team | board | land built | vs budget | thinnest piece |
  |---|---|---|---|---|---|
  | 5 | 325 | 45×38 | 842 | **2.6×** | 5b |
  | 8 | 664 | 45×39 | 840 | 1.3× | 5b |
  | 12 | 1260 | 58×57 | 1512 | 1.2× | 5b |
  | 16 | 2480 | 73×59 | 1866 | 0.75× | 5b |
  | 20 | 3500 | 88×73 | 2735 | 0.78× | 5b |
  | 30 | 5500 | 87×72 | 2719 | **0.49×** | 5b |
  | 32 | 5920 | 87×72 | 2719 | 0.46× | 5b |

  *Three readings in one table. The board stops growing after 20 and does not start before 10. The budget is
  overspent 2.6× at the bottom and half-spent at the top, so it is not one number's worth of contract in
  either direction — `G149` measured the overshoot at 12 players alone and did not see it invert. And the
  **thinnest piece is one cell at every count**: nothing scales a minimum element width, which `G267` carries.*

- [ ] **G266 — The land budget is a quarter to a half of what a map of that size is built with, and its
  curve is one the corpus does not have.** `Envelope.LandPerPlayerAnchors` runs 65 blocks²/player at 5 to 185
  at 32 — a 2.8× rise. Measured over 331 corpus maps, land per team fits `176 × players^1.12` (r = 0.83 on
  log–log): about **250 blocks² a player, flat** — 225 at nano, 270 at milli. Per group, land per team is
  **nano 2250 · micro 4026 · milli 7076 · centi 8732** (`docs/world-scan/map-size-ladder.md`), against a
  composed 1250 / 1450 / 2050 / 2050. Take the anchors to the measured coefficient, per size band rather than
  per count — the bands are the author's: nano 6–13, micro 14–21, milli 22–31, centi 32–47, hecto 48+. The
  board follows: composed coverage is 28–33% against a measured 32–41%, so a board too small is a budget too
  small and not a second fault. `G265`'s ladders move in the same commit or the extra land has no rung to
  spend itself on. `docs/generator/rules.md` `G8`/`G3`, `model.md`.

  *Evidence, at the group medians: `winterbane` 10/team 172×80 land/team 2254 · `röntgen` 18/team 114×235
  land/team 4088 · `icecream_sandwiched_ii` 24/team 292×138 land/team 7032 · `green_gem_ctw` 32/team 156×370
  land/team 8787. The hecto row has three witnesses and is an extrapolation, not a measurement.*

## Nothing else is on the board

The walk drained, and so did what a building may stand on — `DR-PASS` is the author's ruling, the passage is
owed round a group of buildings, and `sketch/seats` answers it forwards (`FEATURES.md`).

`docs/backlog-strategy.md` names **the layer word** as the programme to pull next: `B263`, `B264`, `WE28`,
`TS64`, in `BACKLOG.md`. Three of the four want something first — `B263` a ruling on how the canvas says which
storey a prop is on, `B264` and `TS64` a surface — and `WE28` is the one that is settled and backend only.
