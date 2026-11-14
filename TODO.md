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

- [ ] **S46 — A river on the mirror axis is a level canal.** The Terraform River modifier gives a bed that never
  climbs, per-reach water levels with falls between, and one level where bodies meet (`Courses.cs`). Left: a river
  lying on the mirror axis holds a single level, and the dressing fluid prop takes reach levels rather than one
  lowest-surface line. A road draping over what it crosses stays settled as correct. `docs/world-export/decoration.md`.
- [ ] **WE134 — A map's own dressing registry still takes a hand-written `copied` body.** The tree library
  refuses a `copied` save carrying no cut (`DR-COPY`), but a map's `dressing.styles` takes
  `{"kind":"tree","form":"copied","body":…}` as written and nothing asks where the body came from.
  **The author's ruling:** "hand-built props are not wanted" is the library's rule only. A map builds whatever
  prop it needs, and a hand-built one is marked as such: it is a **schematic** (`docs/pgmvox-port.md`, D17), so
  an inline body that names no library tree is stored and served as a schematic rather than as a `copied` tree.
  `docs/world-export/decoration.md`.
- [ ] **TL45 — A house card is drawn on the footprint its editor chose.** `RoomStylePreview.CardOnce` renders the
  default 8×8 sample footprint from a fixed front camera, not the footprint the house editor's dock states.
  `docs/tools/library.md`.
