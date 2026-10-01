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

## The programme: a board's source, every change to it, and the words it is stated in
**The author put this programme first.** The studio keeps the source a board is stated in and every change to
it, hands the author's hand edits to the agent's next round, and hands out the vocabulary the source is
written in. The source, its changes, the kit it is written in, and coasts, basins, outlines and the fan as
statements have shipped; what remains is `pgm-studio-mapgen` moving onto all of it. The layer word (`B264`, `WE28`,
`TS64`) waits behind it.

### The authoring repository moves onto the studio's source and kit

- [ ] **RP96 — `pgm-studio-mapgen` states its boards in the kit, and a drive shows what it would change
  before it changes it.** `drive.py` runs the board's `build-spec.py` first, dry-runs the source and prints
  the edits before it applies, and reads one report (`WS80`); its `@name` loader goes, a style named as a library
  row instead (`{"library": …}`). **One picture a board is committed** (author): the board seen from its side through
  `render/eye`, beside the documents, and `maps/<slug>/` keeps the export's `map.png` (`WE155`); the debug
  renders stay in the studio, drawn again from any change on request. The boards that still compile (plan
  version 2) import the fetched kit; `specs/opus55_kit.py`, `specs/sonnet55_kit.py` and the copied `common.py`
  and `opus5c.py` retire. `AUTHORING-BRIEF.md`, `tools/README.md` and both skills follow, and the skill's
  first moment reads the changes since the last apply beside the open notes.

  *Evidence: one drive re-sent the previous pass's documents because its script had not been run
  (`pgm-studio-mapgen/reports/opus55-notes-run.md`, the fourth pass).*

- [ ] **TL33 — The house styles `pgm-studio-mapgen` keeps become the library's.** 81 house styles sit in
  `pgm-studio-mapgen/tools/styles` against 13 room styles in the deployed library, and `drive.py`'s `@name`
  and the kit's `house_style()` read them from disk. The author picks the ones worth keeping,
  `tools/seed-studio.py` loads them into the library, boards name them as `{"library": …}`, and the folder goes.
  `docs/tools/library.md`.
