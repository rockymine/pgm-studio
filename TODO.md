# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Thirteen entries, over the cap of twelve, on the author's call.** They are one review of the built maps
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

- [ ] **WE36 — A roof is one material, and its half-course slab is the same one.** The rule (author):
  `roof.body` and `roof.verge` are each a **single solid material** — never a pattern, so an agent cannot
  spread a voronoi across a roof — and `roof.slab` is the body's own material. Body and verge may be the same
  (a whole brick roof) or differ (a dark oak verge over brick reads well). A log or a ground material is
  refused in either, which `HS3` already says for a bare solid and skips for a pattern, since `SolidId`
  answers null. **The gable is not in this rule**: it is the end wall and follows the wall. Lands in
  `HouseStyleValidation.CheckRoof`, `docs/world-export/structures.md` § the roof.

  *26 of 89 styles: `rg-*` a spruce slab under a **snow** roof, `kr-*` sandstone under brick, `ae-*` cobble
  under spruce, `ow-stilt` stonebrick under spruce. `kr-deck`'s roof body is **air**.*

- [ ] **WE37 — A beam is a log, a door head is one material, and an ore is not a building block.** Four
  rulings, one gate. `beams.block` is a **log** — its own docstring already says "the log the ends are cut
  from" and nothing checks it; those beams dock against pillars and that is what they are for. A door head's
  stair and its slab fill are **one material**, and so are a window's block and its host. **No ore anywhere in
  a style.** And a storey whose wall is air carries **no door head** — a stilt house has no wall to put one
  in. `HouseStyleValidation`, `docs/world-export/structures.md`.

  *`sn-compass-keep` uses iron ore for its `post`, its `beams.block` and four wall bands; `sb-assay` for three
  more. `kr-block`/`deck`/`gate`/`vault` head a birch stair with a sandstone slab. `ow-stilt` — Overwall —
  puts an arched head on a ground storey that is five courses of air.*

- [ ] **WE39 — The cobble footing is a default, not a choice, and it is on 54 of 89 styles.**
  `Foundation.Footing` defaults to `new SolidMaterial(Blocks.Cobblestone)`, so every style that says nothing
  about it wears a one-block rim. The rule (author): **null by default**; a footing is for a building whose
  plate is 2–3 courses deep and reads as noise on a one-course plate. `WithoutFooting()` already exists.
  Change the default and the presets that inherit it, and refuse — or complain — where a footing sits on a
  plate of one. `docs/world-export/structures.md` § the foundation.

  *54 of the 89 committed styles carry a footing over a 1-course plate.*

- [ ] **WE31 — A stamped room's foundation is bedrock from `y 0`, through open sky if it has to be.**
  `StructureStamper.StampFoundation` fills `for (var y = 0; y < level; y++)` where `level` is the highest
  surface over the footprint — from the world floor, never from the terrain's own. On a board whose ground
  floats it hangs a solid bedrock pillar under every stamped room; where the cell beside it is void or lower
  it is a sheer bedrock wall a raider cannot climb. Fill from the column's own `YFloor` instead, and not at
  all where the cell carries no ground. Raise a complaint at the columns tier for a perimeter cell that is
  void or more than a step below the floor — `DressingRules.SiteNotLevel` asks the same question of a house.
  `docs/world-export/structures.md`.

  *`opus5-aerie` at `(20, 28)`: **bedrock y0 → y24**, wool at y25. One block out at `(11, 28)`: the terrain's
  own bedrock at y16–17, stone to y22, grass at y25. The room drove 16 courses below the ground it stands on
  and out into the void. `opus5-thornfell`: room floor at `y43` over 42 courses, `x −93` and `x −69` void at
  `z 117`. Export gate open on both.*

### Colour: one family, and the table that names it

- [ ] **WE40 — The two mushroom blocks are filed under a family of neutral greys.** `TerrainPalette.Grouped`
  puts `99:0` and `99:15` in **pale stone**, whose other members are diorite and polished diorite. They are
  warm and the family is not, so a board reaching for "the pale grey" gets a yellow-tan — which is what
  `opus5-sandcaster` painted with. Move both rows to **sand**. Then keep it true: a test that a block's own
  swatch is not a warmth outlier inside a neutral family, with the two deliberate exceptions the docstring
  already names (an ore sits with its stone; gravel and mossy go by use).
  `docs/world-export/terrain-painting.md` § the families.

  *Warmth (max−min channel) inside pale stone: diorite `#acacae` 2, polished diorite `#b7b7b9` 2, **brown
  mushroom `#bfaf95` 42, mushroom stem `#cbc4ab` 32**. `99:15` is already closer to sand (39) than to pale
  stone (53) on the family's own colour.*

- [ ] **WE41 — A pattern is filled from one family, and nothing says so.** The palette's own docstring states
  it — "a family is the unit a pattern's stops, bands or patches are filled from" — and `MaterialRecipes`
  builds a voronoi that way, but an author writing stops by hand is never checked. Raise a `TP` complaint for
  a pattern whose stops resolve to more than one family, naming them, and a second for a cell size too small
  for the contrast between them (the contrast is arithmetic over the family colours). The bucket case is the
  same rule: a bucket named for a colour filled from another family — white stained clay is **186 units** from
  `bright`, where snow and quartz sit, and `opus5-ravensmere` painted snow with it.
  `docs/world-export/terrain-painting.md`.

  *`opus5-ravensmere`: gravel + sand + dirt in one voronoi — cobble, sand and dirt families. `opus5-rimegarth`:
  snow, dirt, stone and mossy cobble over the whole surface — four. `opus5-deepcut`: white clay, quartz and
  light grey clay at a small cell — sand, bright and dirt.*

- [ ] **B163 — A surface block never appears below the course it surfaces.** A `cell` is a **pick, not a
  stack**, so the chosen block is written to every course of its depth. The rule (author): a grass course is
  exactly one block thick and grass never appears below it — and **podzol is the same block by another name**,
  so the check takes the whole set of surface-only blocks (grass, podzol, mycelium, path, farmland) rather
  than grass alone. A palette holding one is invalid at any depth over one unless it is the top layer of a
  layered stack. Checkable at the theme, before a build. `docs/world-export/terrain-painting.md`.

  *author, 2026-08-14 · four maps carry the grass half, written by three models: `corvid-hollow`
  (`rookwood`), `sable-marsh` (`sable-reeds`), `sonnet-briarlock` (`briarlock`), `tallow-weirgate`
  (`weir-silt`). Probed at `(−55, −5)` on Corvid and `(30, −35)` on Weirgate — grass at all three courses in
  each. The podzol half is `opus5-ravensmere` and `opus5-rimegarth`. The same map's `hollow-turf` uses
  `layered` with grass at thickness 1 and is correct.*

### What the ground is shaped like

- [ ] **WE38 — Nothing says what smooth is, so a quarry and a mountainside read the same.** The relief read
  answers everything needed and names no target: an island cannot state what landform it is meant to be and
  nothing measures whether it is one. Two numbers do it, both already in the response. **Elevation for the
  board's size** — `relief / √cells`: plain ≤ 0.10, rolling 0.15–0.30, hills 0.35–0.50, mountain above.
  **Whether that elevation was smoothed** — the `steps` histogram's `scramble : barrier` ratio, which is
  independent of class: at or above 2 : 1 the ground rolls, below 1 : 1 it steps. Let an island declare its
  class and lint both. `docs/world-export/relief.md`.

  *Measured over 52 islands. `opus5-thornfell` (author: good rolling hills) 0.232 at **7.6 : 1**;
  `opus5-tarnfell` (smooth-ish) 0.402 at 3.0 : 1; `opus5-whinnymoor` (plains) 0.065; **`opus5-deepcut` 0.407
  at `0 : 7.85%`** — Tarnfell's elevation class on a board a seventh the size, with **not one** scramble
  transition on it. `opus5-ravensmere` 0.445 at 1.2 : 1, worst drop 15.*

### The two halves of a mirrored board

- [ ] **WE42 — A pattern samples world coordinates, so it cannot be symmetric.**
  `VoronoiMaterial.Resolve` calls `Voronoi.NearestTwo(ctx.X, ctx.Z, …)`, and every other pattern does the
  same, so a cell falls where the noise falls rather than where the mirror is: floor patterns do not match
  across the board and the middle is not symmetric with itself. Give `BucketContext` a sample point the
  painter fills by folding `(x, z)` into the primary image through **`Geom.Symmetry`** — the one canonical
  leaf — and have the patterns read it. Team-tinted materials keep reading `TeamData`, so each side keeps its
  colour, and a cell on the axis folds to itself. `TerrainPainter.Paint` takes the mode and centre from the
  layout `WorldBuilder` already holds. `docs/world-export/terrain-painting.md`.

- [ ] **WS16 — The mirror read compares shape and never material, because it could not.**
  `MirrorReport` states it outright: comparing blocks "would paint the whole map as a fault", since a voronoi
  cell falls where its noise falls. Once `WE42` folds the sample that stops being true, and the read can
  answer the other half of what a mirrored board claims — that the two halves are painted alike. Add a
  material pass over the paired columns, team-tinted materials excluded, answering unpaired-by-material beside
  the unpaired-by-shape it already counts. `docs/world-scan/read-backs.md`.

### Where a thing may stand

- [ ] **WE43 — A building may close the only way through and nothing asks.** `DR-ROAD` measures a prop's
  standoff to a **declared** route stroke; a building dropped across a corridor nobody drew a stroke on passes
  every gate on the board. Walk the ground with the props and without them — `WorldWalk` and `Walk.Components`
  already do it, and coverage already walks every waypoint pair — and decline a prop that disconnects a pair
  or lifts a route's cost past a stated margin. `docs/world-export/decoration.md`.

  *`opus5-whinnymoor` and `opus5-rimegarth` both stand houses across the paths through the map. Both exported
  with the gate open, the mirror clean and traversability whole, because traversability asks whether the
  objectives connect and never whether the way between them is the one the board was drawn to have.*

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
