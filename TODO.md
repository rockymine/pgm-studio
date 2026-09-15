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

## What a building may stand on

`DR-PASS` is the author's ruling as of this board: **eight blocks** of passable ground along the whole run of
**every** side, measured from what the building stamps, along the run of its walls. A side the ground stops
flush against is the map's edge or a hole, and a building may stand against one of those but not against two
facing each other. The lane a building stands in is the ground players arrive on, not a way round it, which is
why every side is asked (`FEATURES.md`).

- [ ] **WE126 — a village is a row of declines.** *Parked on a ruling: whether the passage is owed around each
  building or around the block of them.* Two buildings in a row now need **eleven blocks between their walls**
  — eight of passage, an eave each, and the block of ring the first one holds — so a village street or a
  town square is a row of `DR-PASS` declines. The rule reads a building against terrain and other buildings
  alike, and a player walks *round* a village rather than between every pair of its houses, so the unit the
  passage is owed around may be the **cluster**: buildings whose stamps lie within the passage of each other
  are one block of buildings, and the eight is asked around what they make together. That lands in
  `Decorator.HasPassage`, which would grow a pre-pass grouping the images before it judges them, and in
  `docs/world-export/decoration.md`.

  *Five 5×5 houses in a row on open ground: at 12 blocks between walls all five stand, at 10 only the two
  ends do. `opus5-automaton` and the other mapgen boards carrying rows are where this bites.*
