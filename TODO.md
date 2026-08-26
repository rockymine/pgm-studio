# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Six entries, one group, and the order is the point.** One of them (`WE41`) is parked on a ruling: the
measurement is done and no candidate predicate reproduces the author's judgement, so nothing is built until
one is chosen. They are one review of the built maps
and the order is the point — the house rules are worth nothing until the gate that asks them runs, and the
mirror read cannot compare material until the pattern folds. The board **takes nothing new until this group
drains**, and anything found while working goes to `BACKLOG.md`.

## What a map is made of, read off the maps

The author reviewed the boards this repository has built and ruled on what is wrong with them. The rulings
are the map **as it is played and looked at**, so they are not derivable from the corpus or the code — they
are recorded here as law, with the measurement that found each one beside it.

Two facts shape the whole group. **A rule nobody asks is not a rule**: `HouseStyleValidation` has forbidden a
log verge since it was written, and thirteen committed styles carry one, because the gate is wired to the one
endpoint no authored board goes through. And **a fault the studio cannot see is a fault it teaches**: the
mirror read compares shape and never material, on the stated grounds that a pattern falls where its noise
falls — which is the defect rather than a reason to allow it.

### The house: what it may be built of, and who asks

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

### The void a plan declared

- [ ] **TS33 — An authored shape may fill the ground a plan subtracted, in silence.** A plan's buffer pieces
  compile to **subtract** shapes, which is how a board states its negative space; an `addShapes` entry drawn
  over those cells puts the ground back and nothing says a word. Raise a complaint for an add whose cells fall
  inside a compiled subtract, naming both shapes. `docs/tools/sketch.md` § Refusals and complaints.

  *`opus5-rimegarth` composed a CTW board off the generator, walled the lanes correctly, then filled the void
  down the middle with an ice-and-water pool. The void is what the walls guard, so the walls now guard
  nothing, and every gate answered 200.*

- [ ] **TN8 — A strait is measured on the plan and never again.** `POST /plan/inspect` answers `islandGaps`
  against `CT12`'s 15–40 off the plan's rectangles, before a shape has been drawn. Nothing re-measures it on
  the rasterized board, so a finish that bridges, fills or narrows the gap leaves the plan's verdict standing
  over ground that no longer matches it. Re-read the gap off the raster at the sketch stage and complain where
  it has moved out of band since the plan was checked. `docs/tools/plan.md` § what it compiles to.

  *The same Rimegarth pool: `CT12` passed on a strait the finish then closed. `opus5-aerie` is the case that
  must stay quiet — its four straits are authored gaps and read the same way.*
