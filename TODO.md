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

**The author has paused new capabilities** (November 2026): the backlog's fixes are worked first, then
optimisation and the user experience. The generator's behaviour is frozen — a fix or a speed-up that changes
nothing it makes is welcome, a change to what it makes is not — and its layout entries move to
`docs/generator/ideas.md`.

## Bugs: what the studio does wrong now
**The author put the bugs first**: each entry is the product doing something wrong today, on a page a person
uses or in what a build writes. They are worked before the fixes and the user experience that wait in
`BACKLOG.md`.

- [ ] **WE134 — A hand-built prop is a labelled schematic, stated inline or by reference.** A map's
  `dressing.styles` takes `{"kind":"tree","form":"copied","body":…}` as written, so a body a model built itself
  passes as a library tree. **The author's ruling:** a library tree is referenced by its identifier; anything a
  model builds itself (a truck, a balloon) is a **schematic**, written in the made-layer format
  (`SketchLayout.MadeKind`) and labelled as one. It may be stated inline in the map or in a file of its own that
  the map references, as `map.xml` includes another file. A migration backfills what is stored: a `copied` tree
  body that matches a tree the library builds becomes a reference to that tree, and every other body, tree-shaped
  or not, becomes a schematic, since no tree is hand-built. `docs/pgmvox-port.md` D17,
  `docs/world-export/decoration.md`.

## Doors, as the author means them
**What shipped works as written but not as the author intended**, which was never stated and is stated now.

- [ ] **WE181 — A door is stated, not given to every narrow opening.** `HouseStamper.StampDoors` hangs a wooden
  leaf in every one-wide opening and `RoomFrames.DoorWidth` shrinks a stated width to centre it on the wall. **The
  author's intent:** a door is placed only where it is stated, one leaf per block of opening, and only in an
  opening at least two tall; an opening need not be centred and may sit off to one side of its wall. `HS2`
  (`HouseStyleValidation.DoorClearance`) keeps one job: every opening stays passable, at least one wide and two
  tall; a three-wide, three-tall opening hung with cobwebs is as valid as a door. `docs/world-export/structures.md` §7.
