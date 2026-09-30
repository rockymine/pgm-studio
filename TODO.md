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

## The ground: one copy, and every read standing on it

A board's ground exists three times — the plan's footprint, the layout's raster stored as the scan, and the
built world — and the reads each trust a different copy. The scan is a derived copy stored beside the layout,
checks run once against whichever copy existed at that moment, and two walks judge one board on two grounds.
The entries below are that one cause, in the order a wrong answer costs most.

- [ ] **TN24 — The findings list asks what it can, and a refresh re-runs what Finish judged.**
  `GET …/findings` promises a silent list means nothing is wrong, yet neither asks nor names under `unasked`
  the strait re-read (`CT12`, `StraitReadback`) or `EZ1`/`EZ2`, all cheap off the scan. And `StraitReadback`
  runs only inside Finish, so a vertex edit or a bend after it is never judged. `docs/tools/plan.md`.

- [ ] **RP86 — `drive.py` finishes the board it edited.** In `pgm-studio-mapgen`, `tools/drive.py` stores
  through `from-documents` (which runs Finish) and then replays `editShapes` and bends, so the counts and the
  Finish complaints it prints describe the board before its own edits. Post `POST …/sketch/finish` after the
  last edit and print what it answers. `pgm-studio-mapgen/tools/README.md`.

`docs/backlog-strategy.md` names **the layer word** as the programme after this one: `B264`, `WE28` and
`TS64`, in `BACKLOG.md`.
