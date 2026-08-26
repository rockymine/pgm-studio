# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One entry, parked on a ruling.** The group has drained: everything the author's review of the built boards
called for is in `FEATURES.md`, and what is left is `WE41`, where the measurement is done and no candidate
predicate reproduces the author's judgement. Nothing is built until one is chosen. The next group comes up
from `BACKLOG.md` when it is pulled; anything found while working goes there.

## What a map is made of, read off the maps

The author reviewed the boards this repository has built and ruled on what is wrong with them. The rulings
are the map **as it is played and looked at**, so they are not derivable from the corpus or the code — they
are recorded here as law, with the measurement that found each one beside it.

### Colour: one family, and the table that names it

- [~] **WE41 — A pattern is a family shown off rather than a ground.** The predicate the author has since
  named is not colour distance but **how much of a family a pattern takes**: two blocks is a texture, three a
  mottle, five a family on display. Complain where a pattern's entry list carries more than two members of one
  `TerrainPalette` family. Beside it, two placements the author states absolutely: a **voronoi** belongs in
  the **fill** and is made of stone — never the surface — and a **field** pattern's two blocks must be near
  shades of one ground, so it carries a texture and never a border between two grounds.
  `docs/world-export/terrain-painting.md`.

  *Measured over the 51 boards in `pgm-studio-mapgen/specs` that carry a theme registry: of **277 patterns**,
  85% carry three entries or more — 51 carry five, 8 carry six or seven — and only 15% carry two. Of **50
  voronois**, 44 are on the surface and none is in the fill. The earlier candidates (one family per pattern:
  157/201; a neutral family mixed with a warm one: 54) are superseded.*
