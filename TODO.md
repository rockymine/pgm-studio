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

`DR-PASS` is the author's ruling as of this board (`FEATURES.md`): **eight blocks** of passable ground along
the whole run of **every** side, measured from what the building stamps, along the run of its walls. A side the
ground stops flush against is the map's edge or a hole, and a building may stand against one of those but not
against two facing each other. The lane a building stands in is the ground players arrive on, not a way round
it, which is why every side is asked. It is owed round the **group** of buildings a building stands in, so a
village is a block of buildings rather than a row of corridors, and it is a **complaint**: the building is in
the world and where it stands is something an author or an agent moves.

What is left is the other half of that last sentence — a mover needs to be told where a building may go.

- [ ] **WE127 — `sketch/seats` does not answer `DR-PASS`, and now it can.** The forward read runs the pass's
  five *seat* rules over the whole board so a placement is found rather than guessed at, and leaves the four a
  building meets after it seats to the pass. Three of those read the built world. `DR-PASS` no longer does:
  it is a predicate over the terrain surface and a footprint box, which is exactly what `ClaimRaster`'s grid
  already carries. It lands in `ClaimRaster.Stops`, gated on the asked kind being a building, with the box
  taken as the walls and grown by the minimum eave. `docs/tools/sketch.md` § seats and
  `docs/world-scan/read-backs.md` both say today that it is not answered.

  **Decide first:** a candidate joins the group of any building within a passage of it, and the forward read
  has only per-cell classes rather than each standing building's extent. Either it labels the grid's structure
  cells into groups once per request, or it answers for a building standing alone and says so.

  *`DR-PASS` is a complaint now, so an agent that cannot find where a house may go leaves it where it is.
  On `example-3`, 1,072 of 4,510 seats admit a lone 5×5 building.*
