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

- [~] **WE41 — A pattern that clashes, and no rule yet that names one.** The three boards the author called
  out are real and measured; what is not settled is the predicate. **One family per pattern** is what the
  palette's docstring says a family is for, and it fires on **157 of the 201** scatter patterns in the
  committed themes, Thornfell's praised moor among them — a flood. **Colour spread** between the families
  ranks Rimegarth and Deepcut top at 388 but puts `fable-r5-whitebarrow`'s all-grey scar above both at 401.
  **A neutral family mixed with a warm one** fits the three complaints and still flags 54, including eight on
  Thornfell and ten on Sandcaster. The measurement is in the commit that opened this; the ruling is the
  author's, and the entry stays parked until it lands.
  `docs/world-export/terrain-painting.md`.

  *`opus5-ravensmere`: gravel + sand + dirt — cobble, sand, dirt, spread 293. `opus5-rimegarth`: snow, dirt,
  stone, mossy cobble — bright + dirt, 388. `opus5-deepcut`: white clay, quartz, light grey clay — bright,
  dirt, sand, 388, at cell size 6. Against them: `opus5-thornfell`'s `moor` spans dirt, loam and verdant at
  spread 121 and is a meadow.*
