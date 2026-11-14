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

## The backlog's fixes, while new capabilities wait
**The author has paused new capabilities** (November 2026): the backlog's fixes are worked first, then
optimisation and the user experience. The generator's behaviour is frozen — a fix or a speed-up that changes
nothing it makes is welcome, a change to what it makes is not — and its layout entries move to
`docs/generator/ideas.md`.

- [ ] **WE168 — A room's interior is placed relative to its house.** The chests (`WoolChests`, `RoomFrames.InteriorCorners`) and the monuments (`RoomFrames.MonumentSlots`) are
  stated on the room's `house` as positions relative to its first wing, movable in Layout, and a migration
  carries each derived position into the stored one. `docs/world-export/structures.md`.
