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

## The programme: bringing pgmvox into the studio
**The author put this programme first.** `docs/pgmvox-port.md` is the programme: pgmvox's capabilities move
into the studio one phase at a time, each proven against a pgmvox fixture, until a board built on pgmstudio.de
reaches the ceiling a pgmvox board does. Phase 0 is open; it adds no concept, and the author notices only better
boards. The phases after it are pulled up as one group each when the one ahead drains.

### Phase 0 — the framed isometric and the rule tiers

- [ ] **WS90 — An isometric of any box.** `render/isometric` and `render/xray` draw the whole board
  (`BoardIsometric`); give both a `box` of `x0,z0,x1,z1[,y0,y1]`, refused 422 where it misses the world as
  `render/section`'s `at` is, so one place is drawn at four or five pixels a block (`scale` up to 8, today's
  ceiling). `docs/world-scan/read-backs.md`.

  *Evidence: both reads frame the whole world today, so a place is as small in the picture as the board is
  large; the port asks for one picture per place at four or five pixels a block.*

- [ ] **RP128 — A rule's third level: a measure.** `RuleLevels` holds `refuse` and `hint`; add `measure`, a
  finding that carries a number and its target and never refuses, rendered as the number against the target.
  The author's tiers (D4): hard — valid XML and pieces, nothing built inside a house, a sensible goal, float and
  reach; hint — spacing, ground under a house, sketch overlaps, style; measure — walks, distances, gap widths,
  front lines (`GO`, `WL`, `CT`, `LN`, `FR`, `MD`). `docs/refusals.md` § levels.
