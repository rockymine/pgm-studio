# pgm-studio — Backlog (later)

The **long tail** — open work that isn't in the current focus. The active slice is in **`TODO.md`**;
shipped capabilities are in **`FEATURES.md`** (the Done column). Flow: **`BACKLOG.md` → `TODO.md` →
`FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` started-but-parked — **never `[x]`.** A task lives in
exactly **one** of the three files; pull one up into `TODO.md` when it becomes now/next (its id does not
change). Parked and deferred items stay here, flagged inline. Board rules live in `CLAUDE.md`
(§ "Status & task board").

**The sections below are concepts, not categories.** Each heading names one foundation — the house, the walk
every distance is taken with, what a gate fails to say — and gathers whatever entries spend it, whatever
their ids say. That is also how they are emptied: read the shared ground for duplication first, fix that, and
the entries above it come apart into work that fits in a paragraph. Pull a whole group up into `TODO.md`, not
a task at a time.

Task ids are a prefix + number, **globally unique and stable** across all three files; never renumber or
reuse. The prefix names the document the task must leave correct, catalogued in `CLAUDE.md`, and says nothing
about which section the entry sits in — the retired prefixes still on entries here (`B`, `S`, `N`, `C`, `CV`,
`P`, `A`) keep theirs untouched.

## The configure wizard: a map built from what an author states it is

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/pgm/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit. Only
the focus-integration polish remains.

- [ ] **TC2 — The Identity editor cannot state a pseudonym, and drops one in silence.** PGM takes a person
  as an account (a `uuid`) or a pseudonym (the element's own text), and both codec halves plus
  `PATCH /map/{slug}/metadata` accept either. The editor does not: `AuthorsEditor.ResolveName` clears the
  uuid and sets `Error` when Mojang does not know the typed value, and `IdentityPhase.razor.cs:59` then
  filters the row out with `p.Uuid.Length > 0` — so `Opus 5` is typed, flagged, and dropped without a word
  reaching the saved intent. Decide what a pseudonym row looks like (an accepted unresolved name, or an
  explicit "pseudonym" toggle beside the lookup), then stop the silent filter: an unusable row is refused
  out loud or it is kept. `Components/Forms/AuthorsEditor.razor.cs`, `Features/Configure/IdentityPhase.razor.cs`,
  `Features/Edit/`. Evidence: `PUT /map/x/intent` with `meta.authors = ["Opus 5"]` round-trips to
  `<author>Opus 5</author>`; the same name typed into Configure Identity never reaches the intent.

- [ ] **N08 — Monument Y via side-view + per-side focus.** The side-view (`SliceView`) already sets Y on
  **spawn** and **wool-spawn** (`SpawnStep`/`WoolSpawnStep`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsStep`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)

- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamAssignStep.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.

- [~] **N11 — Monument Y must seat on terrain; coord-input moves must re-snap.** The **point tool** now
  seats every spawn it places — team spawns + orbit copies, the observer, and wool spawns — on the target
  column's floor via the shared `ColumnFloor` helper. Still open: monuments aren't seated at all; and moving
  a spawn (team or wool) via the **coord inputs** rewrites X/Z without re-snapping Y to the new column, so
  only the point tool re-seats. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side
  of the same problem).

- [~] **N12 — Configure has no destroyable phase.** Wools and Cores each have one and the objective phases
  are a group sharing one gate (`FEATURES.md`), so this is now the third phase slotting into machinery that
  already exists: add `destroyables` to `ConfigurePhases` + `IsObjective`, a `DestroyableAuthoring` slice
  beside `CoreAuthoring`, and the steps. A destroyable is the core's shape with a different structure — one
  region per defending team, no per-capturing-team monuments — but its knobs are style/materials/float
  rather than a casing. A DTM map authored in the plan tool can already be configured (the slice rides
  through untouched); what it cannot be is *seen* or edited there. Detection is a separate question and is
  `B58`: unlike a core, a destroyable has no signature of its own, so the phase should offer manual
  placement first and adopt candidates when that ranker lands.

## The sketch tool: shapes, islands, and the ground they become

The depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection highlight);
what is gathered here is the parked and dormant slices of the same surface.

- [ ] **WE28 — A relief is keyed by island id, and on a stacked board two storeys hold the same island.**
  `SketchReliefJson` rides top-level on the layout keyed by island, which is right when an island is a
  landmass: it is the unit the solve runs over, and a recompile that re-fuses the board genuinely produces a
  different one. It reads badly on a stack, where the ground and the storey under it are the *same footprint
  one layer up* and their islands are told apart only by an id that nothing in the geometry distinguishes —
  both are centred on the same place, both cover the same cells, and only a string says which is which.

  That fragility has already cost one bug (`C49`, fixed): a centroid match adopted the wrong storey's id and
  the relief silently detached. The fix is correct and the shape it defends is still a string equality
  between two documents that a recompile, a rename, a fork or a hand-edit can each break on their own, with
  no gate to notice — an orphaned relief is caught only on the compile path (`SK1`), not on a plain save.

  **The question to settle before building anything**: whether the key should be the *layer* plus the island
  (which is what an author means — "the ground of the ground storey"), or whether a relief should ride on the
  layer that carries it rather than at the document root. Either would make the pairing structural instead of
  nominal. Wants the model in `docs/world-export/relief.md` amended first; there is no code task until the
  key is decided.

- [ ] **S42 — Relief: the carve and the graded road fold too.** The solve folds, and so now does the stair cut
  (`FEATURES.md`) — the first later pass to land, and the one that showed the rule is real rather than
  theoretical. The other two are still open: a carve and a graded road each decide things by **walking** the
  map, and a walk has a direction the half-turn does not preserve, so each folds again or it undoes what the
  solve established (`world-export/relief.md` §8). Measured on the designed map, a carve that did not re-fold left
  the two halves **9 blocks** apart. Belongs with S46, which lands both passes; the fold itself needs no new
  machinery — `ReliefSolver.FoldBlocks` is the shape of it.

- [ ] **S46 — Water reads the relief; a river on the axis is a canal.** A dressing path draping over whatever
  it crosses is **settled as correct** — it repaints the top block of each column and adds no cell, which is
  what lets a road cross a slope without becoming a ramp, and routing or grading it would be the tool
  deciding where the author's road goes. Terrain that a route *emits* is the draw phase's path primitive
  (`FEATURES.md`) and the erected-shape modes, not this. Water is the half that is genuinely wrong on a
  relief, because it has to obey the ground rather than sit on it. It needs three things the flat model never
  did: routing on a **depression-filled** copy, because steepest descent stops at the first grain-made pit after 2 cells
  where the filled run covers 65; a bed floor forced non-increasing downstream; and **per-pool** water levels
  replacing `decoration.md` §7's single lowest-surface line — the measured run holds 14 distinct levels, and a
  basin is an outlet alongside the map edge, which is what a pond is. The exception is the case that matters
  most: **a river on the mirror axis cannot both fall and be fair**, because a half-turn reverses the flow, so
  on the axis it is a canal at one level and falling water belongs to the flanks. And the cheapest good idea
  here runs the other way — a drawn channel handed to the solver as a line mark below base level makes the
  terrain form a valley around it (`world-export/relief.md` §9).

- [ ] **S56 — A path's height varies along it.** The path primitive takes a uniform `base_height` over its
  whole band (`FEATURES.md`), so a causeway is one thickness end to end and a ramp cannot be drawn as the
  ramp it is. A polygon already solves the equivalent problem with `anchor_heights` index-aligned to its
  vertices and TIN-interpolated across the footprint, but a path's footprint is not its vertices — the band
  is derived from a smoothed centerline, so the interpolation runs **along the arc** rather than over a
  triangulation: each band point knows how far along it sits (`PathHit.Along` already carries this for the
  stroke), and the height is read between the two authored vertices that bracket it. That gives a graded road
  that is authored, not inferred, which is the distinction that keeps it out of S46. The erected modes then
  compose on top as they do for any other shape, so a sunk tilted path is a cutting and a raised one an
  embankment.

  *the workaround, built: `opus5-undercroft`'s two causeways are `line` **relief marks** with heights
  `16/28/16` and a width, because a path could not be the ramp they are. That puts a road in the terrain
  document, where it cannot be moved without re-solving the island.*

- [ ] **S34 — Reuse a sketch paint's column classification across the edits of one drag.** `TerrainProfile`
  construction is what a paint now costs — ~60 ms of the ~164 ms a 40k-cell board takes (S33, `FEATURES.md`),
  and roughly 35 ms of that is its two `GridComponents.Label` passes: one flood fill for plateaus, a second for
  landmasses, each sorting its seeds and hashing a coordinate pair per neighbour edge. They re-run from
  scratch on every step of a drag, though only the moved shape's neighbourhood changed and the plateau
  components are already a refinement of the landmass ones (equal-top cells are 4-connected), so the second
  pass could be merged out of the first. Whether the rest is worth an incremental cache depends on a number
  nobody has: a typical board is ~93 ms end to end now, so this is the 200×200 case, not the common one.

- [ ] **S25b — Make the surfaced spawn/wool pieces movable, writing the move back to the intent.** S25 landed
  the pieces as **locked** read-only rectangles (`FEATURES.md`; `role`/`intentRef` on `SketchShape`, projected
  by `PlanCompiler`, skipped by the rasterizer, rendered as labelled boxes). The next slice makes them
  draggable: a move writes the new rect back to the intent's `Piece`, from which `Protection`/`Room`/marker
  re-derive, so the sketch and the intent don't diverge. **Resize stays deferred** even here — a spawn/wool's
  `at` is a fractional offset into the piece rect, so resizing shifts the marker and needs its own handling.
  Needs a write path (sketch → intent); the read projection already exists. Then extend beyond spawn/wool to
  the other intent entities (protection / build / monuments / iron) as they each earn a sketch surface.

- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.

- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** Most of the weight
  the original review named is gone: the shape palette was retired outright and Setup moved into its own Info
  phase, so the only panel still above **Islands** is **Layers**. Collapse it behind a `<details>` accordion,
  or pin the tree above it — the tree is read on every edit and the layer list is set once.

- [ ] **S57 — The measure tool is a ruler where the question is a gap.** `sketch-canvas.js` measures the raw
  drag between two freely-placed points and reports `Math.hypot` as "N blocks" (`#renderMeasureLabel`). The
  question it exists to answer is how wide a void gap is — the 10–15 block lane, the jump a player can make —
  and a free ruler eyeball-aimed at two edges is barely better than reading the cursor twice. Three parts,
  each usable alone: **snap the endpoints** to nearby shape and island edges, reusing the `#snapTargets` /
  `bestSnap` machinery the drag already has; **decompose the readout** into ΔX × ΔZ beside the diagonal, since
  a lane's width is an axis extent and not a hypotenuse; and, the real target, **measure the gap rather than
  the drag** — hover or select two islands and draw the nearest-point line between their outlines with the
  block count on it. The islands are already computed live in JS (`computeIslands`), so the nearest-point
  pass is the only new geometry.

- [ ] **S58 — The sketch canvas has no shortcut surface, and its best affordances are secrets.** Escape,
  Delete, `P` to promote, arrows to nudge (Shift for sixteen), double-click to close a polygon, **Ctrl-drag a
  vertex handle for a Bézier tangent** and **Alt to bypass snapping** are all live, and they are spread across
  `sketch-canvas.js`, `sketch-bridge.js` and `sketch-edit-controller.js` with nothing in the UI naming them —
  a grep for "shortcut" across `Features/Sketch` and `Components` returns nothing. The last two matter most:
  Bézier editing is the thing that makes an outline stop being rectilinear, and it is reachable only by
  someone who already knows. A "?" popover on the canvas chrome listing the set, plus tool-contextual hints
  where a tool has a non-obvious step ("click to add · double-click to close" while the polygon tool is
  armed), is the whole of it.

- [ ] **S59 — Per-vertex height is the headline feature and is found by accident.** The path is: select a
  polygon, read the one conditional sentence in the inspector, click a vertex on the canvas without moving it,
  then type into a field that appears in the panel. On the canvas a vertex handle looks exactly like a drag
  handle and its height is a bare text label, so nothing says a click-without-drag does something a drag does
  not. Make the height labels read as interactive (a pill or a hover state), and ideally let the label itself
  be edited or scrolled in place rather than round-tripping to the inspector. The shift-click 2–3 vertices
  slope-fit has the same problem and the same fix. The 3-D preview is where a height edit is actually legible
  and it now draws the built world (`FEATURES.md`), but it is a modal swap rather than a companion view, so it
  confirms an edit after the fact rather than while it is being made.

## The canvas: what every tool draws through

The shared browser half, serving **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit
editor (`/maps/{id}/edit`). `C12`/`C14` are cross-cutting; `C9`/`C11` are Edit's own. Full canvas spec:
`docs/client/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*

- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
  *Held pending the Edit tool's own question: the author has never driven it, and the intent model is what
  authors a map now. Wiring three inspectors is work on a surface that may be retired whole.*

- [~] **B107 — Make a structural piece selectable on the sketch canvas, and draw an absolutely-placed goal.**
  The backend half is landed (`FEATURES.md`): a structural shape's stated height survives a recompile, marked
  per field and carried by `intentRef`. Three reaches are missing, all in the canvas and render layers.

  **Selection.** `sketch-canvas.js` documents structural pieces as render-only — never hit-tested, never
  selected, never edited — so nothing can write the flag a correction would set. It wants selection, a drag,
  and an inspector row for the stated height.

  **The destroy goals.** A destroyable and a core carry **no rect in the plan** — `Anchor` is a bare point,
  and that is correct: neither has a footprint. So their sketch presence is a **movable point with a stated
  height**, not a rect. The height is the interesting half, being the one thing the plan cannot know before
  the relief runs.

  **The raster.** `GET /plans/{id}/png` draws `tallow-mirefast`'s five pieces, both spawns and the legend, and
  nothing at `(0, −50)` where the wardstone stands. `B128`'s empty-`piece` marker is how a landform carries an
  objective without a tier manufactured to hold it, and the one picture the plan layer offers cannot show it.

  *Moving a piece rather than raising it is `S25b`: rect and position keep tracking the plan, so a recompile
  stays authoritative about where while the author stays authoritative about how high.*

- [ ] **RP49 — `doc-status.md` describes a `docs/` tree that no longer exists.** The file whose subject is
  whether the documents are current is itself the stalest thing in the repository. Of its **50 rows citing a
  document path, 33 name a file that is not there** — the whole `docs/contracts/` folder it is written around
  was split by subject long ago, so `contracts/plan-editor.md`, `contracts/sketch-relief.md`,
  `contracts/canvas-interaction.md` and thirty more resolve to nothing. Of the 17 that do resolve, **15 carry
  the wrong line count** and two are right: `generator/ideas.md` is listed at 191 lines and is 471,
  `world-export/decoration.md` at 504 and is 744, `tools/capabilities.md` at 478 and is 707.

  A reader cannot tell which rows to trust, so the whole file reads as noise — and a row being right is
  indistinguishable from a row being stale, which is the failure *Documents rot silently* describes, in the
  document that exists to catch it. **The line counts are the part that cannot survive by hand**: they are
  wrong between every pair of commits, exactly as `project-structure.md`'s size table was before `RP8` made
  `tools/census.sh` write it. Either generate the table the same way, or drop the counts and keep only what
  prose can hold — the path and what the document covers. Decide which before rewriting the 50 rows, because
  hand-restoring counts buys a table that is wrong again by the next commit.

  *Measured 2026-08-22 by resolving each cited path against the tree and counting the lines behind the ones
  that resolve.*

- [ ] **RP47 — The history sweep's grep was one phrasing of several.** `RP10` swept
  `used to |had grown|until now|was (previously|formerly)|no longer (does|did)` and left the tree clean on
  it. A second reading finds **31 comments across 27 files** outside `Migrations/` carrying the same fault in
  other words — `previously`, `formerly`, `the old …`, `had been`, `stopped being`, `before this said`:
  `DocAccess.cs:7` ("Previously copy-pasted…"), `PlanValidator.cs:112` ("Both were previously separate…"),
  `FannedGraph.cs:100` ("The old full-corridor-width floor here misread…"), `UnitPlacement.cs:18`,
  `Composer.cs:32`, `HubBoxEmitter.cs:184`, `SpawnTerms.cs:54` and twenty-four more. Rewrite each as a fact
  about the present shape, the way `RP10` did.

  **`Migrations/` is excluded and stays so.** A migration's subject *is* the shape it converts from, so "the
  old columns are read as the defaults" is a statement about the data it meets rather than about a state that
  no longer exists.

  *The grep: `^\s*(///|//).*(previously|formerly|before this said|the old |used to be|had been|stopped being)`
  over `src/`, minus `Migrations/`. Worth leaving in the commit message so the next reading starts where this
  one stopped.*

- [ ] **C46 — The Export button says nothing while the world is being built.** `GET /map/{slug}/export`
  answers in 0.3–0.7 s on a 100×140 board (`docs/tools/configure.md`), which is short enough that no job or
  poll is warranted and long enough that the control sits inert with no sign the click landed. Disable it for
  the duration and show the same busy affordance the other long-ish reads use. The same holds for
  `GET /xml` on a sketch-origin map, which builds the identical world.

- [ ] **CV16 — the authoring canvases have no frame budget, only habits.** The zoom stall (fixed in
  `FEATURES.md`) was two unrelated per-event costs that happened to land on the same handler, and neither was
  visible until measured: a grid rebuild whose memo was written for pan, and a `.NET` interop call per wheel
  tick. Both are the same class of mistake — doing work per *input event* rather than per *frame* — and
  nothing in the canvases prevents the next one. Two guards worth having: a debug overlay (or an e2e probe)
  that reports main-thread ms per interaction burst, so a regression shows up as a number rather than as
  someone noticing the picture go soft; and a rule that anything crossing into Blazor from a canvas handler
  goes through the frame coalescer, since interop is the expensive edge and its cost is invisible from the JS
  side. The screenshot approach does **not** work for this class of bug — `page.screenshot()` forces a fresh
  raster, so a transient compositor artifact never appears in the capture; measure the handler, not the pixels.

## Mapgen authoring tasks

**These came out of the mapgen authoring runs** — `pgm-studio-mapgen/reports/`, Grok run 1 and the five
Opus 5 authoring records. Each was reproduced against the tree rather than taken from the report; one
finding those reports carry (a house past `HouseProp.MaxFootprint` dropped in silence) is **already fixed**
by `HousePropRules.PastCap` and is not filed. The section is grouped by **the concept an entry spends**
rather than by which pass found it, so a heading is one object and a pass over it is one job.

**What the fifth run added is on `TODO.md` rather than here**, because it was the current focus: seven ids
were opened by it — `WE20` `WE21` `WE22` `WE23` `TS20` `TS21` `WS10`.
Four entries in this file gained a measured case from the same runs and moved nowhere: `B103`, `B144`,
`B96` and `S56`.

**The `B141`–`B188` audit pool no longer has a heading of its own**, because every entry in it turned out to
be about a house, a distance, a tree, a destroy stamp, the plan model, a gate or the paint — and it now sits
under whichever of those owns it. What left the pool entirely, so nobody refiles it: `B142` `B147` `B148`
`B149` `B152` `B155` `B156` `B157` `B158` `B160` `B161` `B164` `B167` `B168` `B172` `B180` `B186` `B188`
`B201` all carry a `FEATURES.md` line; `B136` `B153` `B170` `B173` `B182` `B183` are composition law and live
in `pgm-studio-mapgen/AUTHORING-BRIEF.md` § *What the studio checks for you, and what it does not*, which
`B189` was filed to keep true and is withdrawn with them; `B199` is withdrawn because the surface's inward band (`B200`) already
says what a concentric house floor would have; and four folded into a neighbour — `B176` into `B162`, `B97`
into `B166`, an absolutely-placed goal invisible in a plan raster into `B107`, and a walled wool room reading
as an isolated marker into `B99`.

- [ ] **RP59 — The authoring call a headless caller wants is documented as a way back in.** `POST
  /map/from-documents` stores a plan, a layout and an intent under a named slug, replacing whatever is there,
  and applies the authors in the same body (`RP13`). `docs/tools/flow.md` presents it under *The three
  documents are also the way back in*, which reads as a re-import, so every authored board took six calls
  instead: `POST /plan` — a fresh slug each time — then `PUT …/plan`, `PUT …/sketch/from-plan`, `POST
  …/sketch/finish`, `PUT …/intent/from-plan`, and `PATCH …/metadata` last, because storing an intent projects
  the map document over whatever the metadata said. Say in `flow.md` and `docs/architecture.md` that it is the
  authoring call as well, and what the six-call path is for: a map walked through the tools one stage at a
  time, which is the editor's path and not a driver's.

  *`opus5-thornfell` was corrected three times and left `thornfell`, `thornfell-2` and `thornfell-3` in the
  database; every render, provenance sidecar and column read had to be traced to the right one by hand.*

- [ ] **B103 — Bound the top-down on ground, not on every column carrying a block.**
  `TopDownRender.ReadColumns` takes `SurfaceExtractors.Surface` with no exclusions and derives the frame from
  `columns.Keys.Min/Max`, so a column whose only block is a **floor marker** at y≤2 counts as extent. The
  frame then reaches past the ground and the margin is painted as void — which is what makes the important
  corner of a narrow board read as empty. `HeightProfileRender` already gets this right on the same world.
  `SurfaceExtractors` has the rule to reuse: `FloorMarkerMaxY` already distinguishes a marker sheet at the world
  floor from the same block used as terrain.

  *measured on a board built from `marlstone-steps`' plan: the top-down frames **144 × 190** blocks
  (x −72..71) against the height profile's **140 × 190** (x −70..69), and the four extra columns are **108
  cells of redstone wire (id 55) at y1**, running z −12..11 at each end. The top-down reports its surface span
  as y 1..53 where the height profile reports y 9..53.*

  *and again on `opus5-undercroft`, whose only structures are two spawn rooms: the frame reaches to the
  redstone lines at y1 under the markers, so the board's own margin paints as void on a board 80 wide.*

### What a gate says, and what it fails to say

- [ ] **WE45 — `DR-PASS` measures the wrong rectangle and asks the wrong question.** Three faults, one rule.
  It measures the **wall rectangle** rather than the stamped extent, so a roof overhanging the passage is not
  counted: `opus5-rimegarth`'s `hall` has zero clear blocks on all four sides once eaves count and passes
  today. It takes the **widest** side, so a building with three sides open and a two-block ledge on the fourth
  passes. And its width is **absolute**, so a twenty-block passage with a fifteen-wide house in it leaves five
  and passes — which the author has ruled is not a way past.
  `docs/world-export/decoration.md`.

  The author's number is **ten blocks** of way round a building, and every side is judged rather than the
  widest. Not urgent while `DR-CROSS` fires on the boards this was found on.

  *122 buildings on 32 boards: 4 fail today. A side with ground and under 3 clear blocks fails 51, under 5
  fails 76. `whinnymoor/hut-w` reads E=24 W=23 S=2 N=22.*

- [ ] **WS17 — A walk reads a building as a hill.** *(the author's ruling: a house is not walked over.)* `Walk.Standing` calls any surface with two blocks of air
  over it a place to stand, roofs included, and `Walk` prices a climb rather than refusing it — so
  `traversability` and `coverage`, which walk the finished world, route straight over a house and report a
  board whole that a player cannot cross. Bound the rise the way `Walk.Components` already can, or exclude the
  columns provenance calls a structure. `docs/world-scan/read-backs.md`.

  *`opus5-whinnymoor` and `opus5-rimegarth` both exported with traversability whole and buildings standing
  across their roads.*


- [ ] **G231 — `EL1` measures a piece against the global surface, not against its neighbour, so it
  complains about half a flight of stairs.** `PlanValidator.LintEl1` takes `p.Surface − globals.Surface` and
  raises where that is odd. EL1's own text in `docs/generator/rules.md` states the measured quantity as the
  **land-interface** delta — "every one of the 137 measured land-interface deltas is even" — which is a
  different number: a piece two tiers up from base sits at delta 2 over its own neighbour and delta 4 over
  the global.

  **Evidence.** `pgm-studio-mapgen`'s `showcase/05-steps`: four treads at surfaces 10, 11, 12, 13 over a
  global of 9, every interface delta 1. EL1 complains at `tread-1` (delta 1) and `tread-3` (delta 3) and
  says nothing about `tread-2` or `tread-4` — half a uniform flight flagged and half not, which is the tell
  that the quantity is wrong.

  **And a stair is not a plateau.** EL1's evidence is a corpus histogram of where one *plateau* meets
  another; a flight of one-block treads is the shape a 2-block riser exists to avoid, since no player walks
  up two blocks. So the lint should measure the interface, and it should not fire on a run of pieces whose
  interface deltas are uniform and 1. Amending the rule text goes through `rules.md`'s own correction
  protocol.

- [ ] **B249 — An author can force a compile and an export past its refusals; an agent cannot.** The gates
  are right to refuse an agent — an unenterable board or a wall through a wool room is a defect it cannot see
  — but they also refuse **an author doing something deliberately off the norm**, and there is no way past
  them. `sunspit` in `pgm-studio-mapgen` cannot be rebuilt against today's tree for exactly that reason —
  `PL13`, a wall on the wool room's interface (`B186`) — and `firnline` now rebuilds without the house it was
  authored with, which the keep-out mask declines as `DR-KEEP` (a house in a spawn door's approach). Both
  worlds load and play; only the pipeline that made them disagrees.

  **So the override reaches the keep-out mask as well as the refusals.** A decline is not a refusal, but it
  is the same question one layer down: the author placed the thing deliberately, and there is no way to say
  so.

  **The shape: a per-call override that names what it is waiving, not a global off switch.** It reaches the
  two places a refusal is raised — `PlanCompiler` (a `PL*` refusal) and `MapExportComposer` (`OB17`, `OB19`,
  the playability judgement) — and every waived finding is still **reported**, as a warning carrying
  its rule id, so a forced build says what it forced rather than going quiet. A refusal about the `map.xml`
  contract itself is not waivable: PGM has to be able to read the result.

  **It stays out of the agent's vocabulary.** Not in `docs/tools/capabilities.md`, not in the endpoint tables
  the briefs hand an agent, not in `PgmStudio.RoundTrip`'s `--help` (`WS6`). The authoring briefs already tell an agent
  that a refusal is a fault to fix; an override an agent knows about is an override an agent will reach for.

  **Five agent-authored boards asked for it exactly zero times, which narrows it rather than weakening it.**
  Across `opus5-elderwold` … `opus5-undercroft` no gate refused something that was right to build. The one
  case that looked like a waiver — a texture brush declining every tree it touched — was a gate asking
  `DR-ROAD` of the wrong prop, and a stroke now says whether it is a route (`WE21`). So the entry belongs to
  **human authoring**, where its two
  named worlds live, and it is not a dependency of anything on the agent track. Filed here as a schedule
  note rather than a change of scope: a waiver reached for in place of a fix hides the fix, and the whole
  value of this entry is that it does not.

Three gates, three ways of being wrong about their own verdict: one that misreports its cause, one that cannot
see half the board, and one that refuses what the rule document recommends. They are the last of the
`B141`–`B188` audit findings that are not about a house, a distance, a tree, a destroy stamp or the plan
model — everything else from that pool has moved to the heading its subject owns.

- [ ] **TN2 — `structural-integrity` carries one sentence where several refusals fired.** The term folds
  every `PlanValidator` refusal into one hard violation, and where there is more than one its message is
  `"{n} structural errors ({first})"` — so `/plan/evaluate` tells an agent the count and one of them, and the
  other n−1 arrive only at the compile's 422 a stage later. The evidence rides already: `SubjectIds` is the
  union over all of them, so the count and the subjects are right and only the sentences are dropped. Carry
  them — a violation already wraps a `Finding`, so either the term answers the list or the DTO gains the rest
  beside the one it names. `StructuralTerms.cs:22-32`.

- [ ] **TN3 — The driver states two bands as literals the studio already serves.** `tools/drive.py`
  (`pgm-studio-mapgen`) prints `(GO1 wants 3.0-4.0)` beside each goal ratio and `(CT12 wants 15-40 on a
  direct strait)` beside each island gap, both hard-coded in the f-string. `GET /api/rules/terms` answers
  `{"term":"goal-spawn-ratio","rule":"GO1","band":[3,4],"bandSource":"authored"}` — the enforced number,
  read through the scorer's own resolution — and `GET /api/rules?rule=CT12` answers the rule's own sentence.
  A band the author moves leaves the driver telling every future run the old one. Read them once at the top
  of the run and print what came back.

- [ ] **B143 — `SP1` must say that a plan declares no build zone, not that its wools are unreachable.** The
  rule needs a declared zone before it has frontline pieces to start from, so a zone-less plan has **all** its
  wools reported unreachable regardless of actual reachability — and an agent reads that as a geometry fault
  and redesigns the board. Correct about itself, wrong about what it tells the author.

  *`sonnet-run2` #5 · `sable-marsh`'s first plan: adding one `zones` entry cleared all eight findings on a
  board whose geometry never changed.*

- [ ] **B150 — `G8` fill-ratio measures the plan's rectangles, not the board that gets built.**
  `FillRatio.Value` reads `ctx.Board` — `BoardStructure`, derived from the `PlanModel` — and answers
  `Filled.Count / bbox`, in **plan cells**. The sketch is where a board's ground actually is: its organic
  `add` shapes push the coast well past the plan pieces and its `subtract` cuts the holes, and none of that
  reaches the term. So the one number describing how much board there is describes a different board, and it
  is not only the holes that are missing — the additions are too. Settle it before any evaluator score is
  trusted on a sketch-authored map, which is every map an agent authors through the documented loop.

  *measured on `basalt-reach`, 2026-08-16: the plan is **five pieces tiling edge to edge with no hole at all**
  — 522 filled cells over a 30×21 cell bbox, **G8 = 0.829** — while its sketch carries **eleven shapes, ten
  `add` and one `subtract`**. The plan's bbox is **150 × 105 blocks**; the built world is **150 × 204**, with
  23,417 ground columns (0.77 of its own frame) and a void hole through the lower middle. The term is
  describing half the board.*

- [ ] **B151 — Decide whether `WL8` is a hard term or the rule document is wrong.** `ClosureTerms.cs:5–9`
  makes wool-ringed-hole hard; `rules.md` § "Function is read from the hole's ring" describes the same
  arrangement as the two-approaches device, and the same plan compiles 200. Nothing says which, so an author
  reads a refusal for a shape the law recommends.

  *`opus-run2` §1.7 and §5 #7.*

### Painting: the theme a document states is not what lands

Four places the paint and the document disagree — an overlap resolved by opposite rules, a shape whose
interior is never themed, a palette stacked down a column it should top, and a band stack the editor cannot
author. Beside them, one where the *reader* disagrees with a document that is right.

- [ ] **B144 — Settle how height and paint resolve an overlap, and warn where they disagree.** Height takes the
  **taller** add-shape (`MergeCell`); paint takes the **smallest-area** shape (`ShapeScopeOwners`, "the most
  specific scope"). The documented way to give a tier an organic edge is to let the tier below run *under* it —
  and where that lower tier is the smaller shape, it keeps its own paint over ground the upper tier owns. No
  field scopes paint to the visible surface rather than to a shape, and nothing warns.

  *`opus5-run2` §5 #2 · re-probed on `marlstone-steps` against the committed region files: `(0, 58)` is
  sandstone at y21 where `(0, 70)` — the same shelf at the same height — is quartz.*

  **The stacked half of this is `TS23`**, whose `Q4` is this entry's question asked over two layers rather
  than two nested tiers. Whatever settles one settles the other, and neither may answer it alone.

- [ ] **B145 — Paint a spawn or wool shape's interior with its theme.** The shape is simply unthemed — not the
  known bedrock case, and conflating them is why it keeps being dismissed:
  `WoolStructureStamper.StampFoundation` filling a wool-room piece with bedrock from y0 is documented and
  intended, and here there is no foundation at all. Two mechanisms, one symptom, and the second has no owner.

  *re-probed on `marlstone-steps`: the column under the red wool at `(0, 85)` is raw `1:0` Stone from y24 down
  to y1, on a board whose `crest` theme is quartz. Reported independently by four runs.*

- [ ] **B200 — Let the Theme phase author an inward band stack.** The JSON accepts one and the painter draws
  it, so the only way to author a ring stack today is to edit the document by hand — the same reach fault
  the sketch's height controls have. `Components/Terrain/MaterialEditor.razor`'s `Layered` case
  offers a list of layers and one number per layer captioned *Courses*: no axis control, no `beyond` slot, no
  ending control, and its help text still states the depth reading as the only one. `ThemeVocabulary.NewMaterial`
  seeds a layered node with `kind` and `layers` and nothing else.

  *reported by the author while theming a board — "a cobble rim, then two rings of stone brick, then a grass
  field". The worked JSON is in `docs/world-export/terrain-painting.md`.*

### The house: what it stamps, where it stands, and what an author can say

- [ ] **WE15 — A hand-built core stamps nothing, because its size defaults to zero.**
  `CoreIntent.Size`, `Height` and `Shell` are plain `int`s defaulting to **0**, so a core assembled anywhere
  other than `PlanCompiler` — which fills 5/5/1 — casts no blocks and resolves an empty `Box`. Nothing says
  so: the export answers 200 and the `map.xml` carries a `<core>` over a region holding nothing, which is a
  goal at zero health. `CorePlacement`'s own schema documents the defaults a caller may omit (`size` null = 5,
  `shell` null = 1, "65% of corpus cores"), so the intent record is the one layer where absent means nothing
  rather than the default. Give the three fields their initializers, or refuse a core of no size at the same
  gate `OB24` is asked at.

  It bites the workflow the authoring brief describes: an agent patches a compiled intent by hand, and a
  round-trip through a tool that drops zero-valued keys leaves a core that builds nothing.

  *Found writing `OB24`'s test: a hand-built `CoreIntent` at the same anchor as a destroyable produced no
  overlap, because it produced no box at all.*

- [ ] **WE12 — A spawn may stand without a house and a wool may not.** The two are the same shape — a source
  (a spawn point, a wool spawner), a protection region, and a structure over them — and the structure is
  decoupled on one and welded to the other: an author can already say a spawn has no housing, and a wool
  always comes with one. Nothing about the objective needs it. The house is what the studio *defaults* to,
  not what the wool *is*: the wool is the spawner, its protection and its monument, and
  `region-categorization.md`'s wool `room` is the protection region rather than the building. Give the wool
  the same optional structure the spawn has, so a wool can sit in the open, in a tower, or in a house because
  the author said so.

- [ ] **B37 — Every family's resolver should answer one resolved-stamp record, and only iron does.**
  `IronResolution(MarkerX, MarkerZ, MinX, MinZ, Size, Placeable)` is the shape and the only instance, with
  four consumers, all iron; the wall, the rooms and the objectives each resolve their placement inline. The
  record wanted is **kind, footprint box, `Placeable`, source marker**, produced by each family's resolver.
  The stampers stay heterogeneous — a wall owns a seam, a room a piece + marker + entries, an objective a
  marker + style — which is why the *resolution* is the thing to share and not the stamping.

  Two things need it. `PlanStructurePreview.StructureBox` assembles its own boxes for iron, destroyables and
  cores; consuming the record instead reaches placeability into the iso view for free. And the
  objective↔objective and objective↔monument **minimum distances** — a core merging with a wool monument they
  must read apart from — have nowhere to live until every placement answers in one shape.

  **It is the same rule one layer up.** `PlanStructurePreview.StructureBox` re-derives iron, destroyable and
  core boxes the builder already computes, which is a second derivation of one geometry — the failure
  `PlacementClaim` names for a claim and that has now cost two fixes, the goal anchor and the spawn room's
  claim rect. A resolver answering one record is what lets the preview *read* the placement instead. Every
  entry already carries a `StampId`, so the record has an identity to travel under.

  *Not this: `OB17` already refuses a goal in void, in a spawn room or in a wool room over a shared
  `ObjectiveFootprint`, the unwinnable `block="never"` case included; `PlacementClaim` answers which
  columns a stamp owns; `B142` answers what the dressing pass declined. The editor half shipped (`B59`,
  `C44`) and what remains of it is timing — structural findings do not run in the live feed (`G161`), so a
  refusal appears at Compile rather than as the marker is dragged. `G65` is adjacent and separate: whether two
  pieces touch, not how far apart two placements stand.*

  *moved here by the human because it sounds related and no other category fits*

One object with three unfinished sides. The **stamper** decides what blocks come out, and how a wing's roof
meets a neighbour's is still argued rather than drawn; **placement** decides where a building may stand and
what it reserves; and **authoring** is what a style can state and what a library shows back, where the model
has outgrown the editor. The three share `HouseStyle`, `HouseStamper` and `BuildingPlan`, so a pass over any
one of them opens the others.

**The stamper.**

- [ ] **WE2 — Settle whether the eave should descend by `pitch` at all** (author). Distances are measured from
  the wall line and go negative, so the overhang tip drops one course per unit of pitch while the wall top
  stays put: a steeper roof reaches further down the wall rather than sitting on it. The eave courses are the
  roof's material now, so nothing shows through — the open question is the geometry, and it is a question
  about what a roof looks like rather than one the code can answer. If the answer is that it should not, the
  change is in `RoofField.Rise`, holding the distance at zero outside the wall line instead of letting it go
  negative; the overhang then runs flat at the wall's own course.

  *measured on a 12×9 wing, `overhang: 1`, wall 5 courses on a floor at y8 (wall top y13): pitch 2 puts the
  overhang tip at y12, pitch 3 at y11, pitch 4 at y10 — probed at `(7, y, −1)`.*

- [ ] **B225 — A march tests another wing's walls where the primary pass tests its whole roof.**
  `Overtopped` asks `otherField.Covers` — the wing's walls **plus its overhang** — while `OtherRoofCrownOver`,
  which decides where a march stops, asks `otherRoofed.Holds`, the walls alone. Reading `RoofField` settles
  what the difference is and not whether it is right: `Covers` is `Holds` grown by the overhang on all four
  sides, so the two disagree only on the overhang ring, and the march never reaches that ring on its own
  account because `Marched` breaks on `body.Holds` — the union of the *wall* rectangles. The one case that
  survives is a ring lying over a third wing's walls, where that wing answers for itself with its own crown
  and the overhanging wing's is never asked. Whether a course marching under a neighbour's verge should stop
  there is a question about what a roof looks like, so it wants a built figure rather than an argument:
  stamp a wing whose march runs beneath another's gable overhang and read the valley.

- [ ] **B92 — Give `HouseStyle` a fill material, so a building can be a mass rather than a place.**
  `HouseStamper` leaves the volume its walls enclose as air, which is right for a village and wrong for a
  scenery building that is not enterable and for a run of buildings sealing the edge of the board — the only
  way scenery does the work of a boundary in a mode where nothing may be placed. **The facade is kept**: the
  windows and door stay where they are and the fill sits *behind* them, so a window reads as an unlit interior
  rather than a hole into rock. A dark fill (black wool) is the idiom, which is why it is a knob and not a
  constant. A style field rather than a stamper flag, so a style carries whether it is a place or a mass.

  Two things to settle: whether the fill respects the storey stack (a building filled to its top course and one
  filled to its first floor are different buildings), and how deep behind an opening the fill starts (flush and
  one course back read differently through the gap). `DressingScope` already protects the ground under a
  stamped building, so nothing downstream needs teaching.

**Placement — where a building may stand, and what it reserves.**

- [ ] **B178 — Give a spawn building its own stated footprint, so the plan piece describes only the ground.**
  The piece does two jobs at once — the ground a spawn stands on, and the building raised on it — so an author
  who wants a wide platform and a small hall cannot ask. Same class as the iron marker: something welded to a
  plan rectangle drawn for a different purpose. The `ST9` cap (20×20) suppresses the symptom by forbidding
  large pieces and is the workaround until this lands; both are wanted.

  *author, 2026-08-14 · Ashfall Scar's spawn piece `x −40…40` builds an 80-block hall; same on `sable-marsh`
  (90) and `tallow-weirgate` (80).*

- [ ] **G178 — A wing has no doorway into its neighbour.** Where two wings meet the plan is simply open between
  them, which is right; where one projects into another its gable end is a wall from the ground up, which is
  also right and leaves the projecting wing reachable only from outside. A doorway cut between two wings —
  through the shared wall a projecting wing's gable end stands in, or through the wall a taller wing's storey
  carries above a stopped neighbour (`structures.md` §7.6) — wants a run to sit in and a rule for which wall it
  is cut through, and belongs with the openings work rather than with the roof (`G172`).

- [ ] **G156 — cell-size-aware generator room sizing (WX2's generator half).** The stamped-room minimum is
  8×8 **blocks** (`docs/world-export/structures.md` WX2) but the emitters size rooms in **cells**
  (`ShapeEmitter.RoomDepthCells` = 2, corridor widths in cells), so a small-cell board (cell ≤ 3) can emit
  a wool room or spawn its own export refuses. Make the room depth/width floors cell-size-aware — enough
  cells to reach the block minimum — through `MinBox` and the spawn profile; the composer's cell-5 boards
  already clear it by construction, so this binds only when boards go small-cell.

**Authoring — what a style can say, and what a library shows back.**

- [ ] **B221 — The style libraries preview a stamped world, and a part editor frames a section of it.**
  Authoring a **whole style** — a house, a wool cage, a spawn shell — wants the building as it will stand, so
  the library builds a small world with the house in it and draws that: the path `B165` was found down, and
  now that the 3-D preview draws the world the export builds (`S54`) the library can show the real thing
  rather than a stamp of a fixed 10×10 sample. Authoring a **part** — `RoofStyle`, `Storey`, `PorchStyle`,
  `Foundation`, each a record of its own — wants a **section** through that world at the part, because the
  part is currently lost inside the whole: `RoomStylePreview.Views` takes `Outer(style)`, the entire shell,
  whichever of the three part libraries is open, and nothing on that path asks which part is being edited.

  Where a Y range is the right cut the bands are public: a storey is `LevelBases[i]` to `+ Clear`, a roof is
  `WallCourses` upward; a porch is an XZ restriction instead. Stamping the part alone is the wrong design — a
  roof's eave sits on the summed storey stack and the porch decides the front the body is split on, so an
  isolated part synthesises the context that decides its geometry anyway.

  *One trap: `WorldViews.Isometric`'s `Opaque()` reads `world.GetBlock` unbounded, so a face at the cut plane
  sees solid beyond it and is not drawn — a box restriction leaves the cut open unless out-of-box reads as
  air.*

- [~] **B70 — The room-style *card* cannot show a porch or a window.** The open editor draws four views now
  (B71), the cutaway among them, so a style's porch and its windows read there. A library **card** still
  carries the section alone, and a section projected onto the front wall shows a window as a patch of the same
  colour as the wall around it. The sample is the other half: `RoomStylePreview` stamps the shipped 10×10
  piece's 8×8 shell, which is small enough that a porch leaves little room behind it. The library therefore
  still has knobs whose *card* does not change when they are turned, which is the one thing the preview exists
  to prevent. Wants a larger sample footprint, and a card that is not the one view those knobs are invisible in.

  **And one footprint is the wrong number, not merely a small one.** `Sample` is a single `static readonly`
  field, so every style in the library is judged at 10×10 and at no other proportion — while a style states
  nothing about the footprint it will be stamped over, only storey heights and a roof's pitch. That would be a
  gap even if the shapes agreed, and they do not: `Wing.RidgeAlongX` derives the ridge from the rectangle's own
  proportions, so one style on 10×10 and on 5×10 is two different roofs rather than one roof stretched, and an
  author has no way to see the second. So the sample wants to be a parameter with a few proportions behind it —
  square, long, narrow — rather than one bigger square.

- [ ] **S40 — Offer "no building" in the Rooms step.** A bound room style has three answers — a style, absent
  (the built-in shell), and an explicit null meaning the pad stands on open ground with nothing over it
  (`docs/world-export/structures.md` §9). The export reads all three and the stampers have always accepted
  the third, but the step can only *bind* or *clear*, and clearing means the built-in rather than none. So a
  map can be authored open only by writing its layout by hand. The step needs a third control per kind, and
  `ReadBindings` needs to tell a null snapshot from a missing one — today the bridge state drops both, so an
  open room displays as unpicked (harmless until the author touches it, since the save preserves what it
  loaded).

- [ ] **S60 — A building prop can state more than one wing; the canvas can still only drag one.** `HouseProp`
  carries `wings`, a list of touching rectangles, and `Decorator` composes them into one `Footprint` and stamps
  once (`G177`) — an L, a T or a U is authorable today by anyone writing the document directly. The dressing
  tool itself still only ever drags a single rectangle: there is no way on the canvas to add a second wing to a
  placed building, drag one of several independently, or see a proper L/T/U outline rather than one rectangle
  per wing (`wingRings` in `dressing-render.js` draws each wing's own box rather than the traced silhouette a
  build actually stamps). Wants a second interaction — add-a-wing, probably a drag that starts touching an
  existing wing's edge — and a handle set that knows which wing a grip belongs to.

### World import: reading a map the studio did not build

The scan is the one path where the studio is a **reader** rather than an author, and it is judged against maps
whose authors never met it. So the failures are all of one kind: a block that means something to a person and
nothing to the extractor, a derivation that was tuned before stair-awareness landed, and a ranker left half
finished. Diagnostics rather than gates — telling an author their imported map is broken is the value;
refusing to re-export it is the studio overreaching. Include resolution sits here for the same reason: an
`<include>` is something the reader must follow to see the map a server actually plays.

- [ ] **B57 — `scan_segment` counts a build-region marker as solid ground.** Island detection now separates
  terrain from markers and from what a map erases before play (`FEATURES.md`,
  `docs/world-scan/terrain-ground-truth.md`), but that runs on `CleanColumns` → `islands_json` only. The other
  ingest derivation, `FeatureExtractors.Segments` → `scan_segment`, has its own exclusion set and applies
  neither rule, so a floor sheet at `y=0` persists as a solid span. Everything reading it at query time
  (`SegmentIndex.BaseColumns` → `IslandDetector.CleanedBaseFootprint`) therefore walks on a marker. Narrower
  than it sounds — that path feeds kit-reach, not the island picture the configure tool draws — which is why
  it is filed rather than fixed alongside. The two derivations should agree on what ground is, and the fix is
  to route the floor-marker rule through both. **Blocked in practice by re-import**: `scan_segment` is
  written once at ingest from a world that is then discarded, so changing it reaches existing maps only when
  a map can be re-imported.

- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), and the stored decompositions
  predate it. What remains: (a) **re-scan the corpus** so the stored `islands.json` / `island_sketch_json`
  reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy detection — needs the
  source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide whether to refresh the
  `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated raised-decor specks**
  (≈37-block grid bits with no walkable connection, which a per-island prune could drop).

  *What a detected island **is** stays the subject; what it is **for** is not. The size and gameplay-role
  classifiers were deleted with the routes that surfaced them (`RP18`), and the under-split read went with
  them — the author's call, and the reason is that a better decomposition is worth having and a label over a
  bad one is not.*

- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.

- [~] **B58 — Finish the destroyable ranker.** The core half has shipped — gathered at ingest, stored in
  `core_candidate`, and confirmed in the Cores phase (`FEATURES.md`). What remains is the other objective,
  and it is measured but unbuilt (`docs/world-scan/objective-suggestion.md`).
  **Destroyables: the discriminating signals are measured, the detector is not written.** They are not
  identified by anything about the structure — size spans 1 to 31,105 blocks and fill is uninformative — but by
  their **neighbourhood**, dumped 10 blocks outward and down to `y=0` for all 614 declared structures.
  *Isolation*: a declared destroyable has a median of 6 same-material blocks within 10, against 65+ for a false
  cluster, because decoration repeats and a goal is placed once. *Elevation*: it sits a median +5 blocks above
  the surrounding terrain, against −2 for false clusters. Together, with **no size cap and no air-face test**
  (both of which were discarding truth), `same ≤ 8 & elevation ≥ +2` keeps 553 of 1,062 true clusters against
  600 false — 48% precision at 52% recall, a four-fold precision gain on the previous best. `same ≤ 0 &
  elevation ≥ +2` reaches 65.6% precision if a stricter list is wanted.
  Build the detector at those operating points, gather at ingest into a `destroyable_candidate` table beside
  `core_candidate`, and validate the same two ways cores are (corpus + a composed plan). **Scope honestly to
  84%**: obsidian, emerald, gold and ender stone carry that share of declared destroyables, and the wool /
  stained-clay / stained-glass remainder must stay out — admitting wool takes the candidate set from 15,488
  clusters to 439,440, because a CTW map is made of wool.

- [ ] **B24e — Flag an *imported* map whose objective region holds none of its material (a warning, not a
  gate).** Scoped down: the authored half of this is **already covered by tests** — `DestroyableWorldTests`
  and `CoreWorldTests` walk each emitted region with PGM's `[min, max)` and count the blocks, which is
  exactly the assertion this task was filed to add. For a generated map the region *is* the stamper's box
  (OB8), so a runtime gate would re-check something true by construction. **What has no cover is the import
  side**: the corpus sweep found **10 destroyables whose region contains none of its declared material**.
  Those are the author's own maps, already broken before we touched them — so this is a **diagnostic on
  import**, not a block on re-export. Blocking someone's export over a pre-existing dud is the studio
  overreaching; telling them is the value.
  Never "the region is full": by OB12 a region is legitimately mostly air (a 3×3×3 region holding a 1×3×1
  pillar is correct and common), so anything stricter flags most of the corpus.
  **Note the category difference** before extending `MapValidity`: its one existing rule (a wool needs a
  monument) is *"PGM refuses to load this map"* — an `InvalidXMLException`, so the map is unloadable. This
  one is *"PGM loads it fine and the goal has zero health"*, which PGM itself only logs a warning for. Two
  different severities of truth; do not blur them into one list without saying which is which. World access
  is **not** the blocker it was originally filed as — 14 test files already read blocks out of a built
  world. (OB3, OB11, OB12)

- [ ] **B55 — Decide which API paths read a map *as played*, then wire `Includes:Root`.** `IncludeLibrary`
  and the resolved parse are in and gated by tests (`FEATURES.md`), and the harness uses them
  (`--resolve-includes`, `--water-lanes --includes-dir`). The API does not, because which reading each
  endpoint wants is a real question and the wrong answer corrupts data: **a resolved document must never be
  written back** (the include references are still emitted, so the fragments' content would be applied
  twice). Safe by construction today — nothing in the API passes a library — and the work is to choose per
  path rather than flip a global. Rule-level analysis (region categorisation, filter wiring, apply-rule
  order) wants resolved; anything that saves, exports or re-emits a document wants written; geometry
  (islands, layout, the seed corpus) does not care, since maps declare their own regions. Add
  `Includes:Root` beside `MapsRoots` in `Program.cs`, thread it only to the chosen paths, and make the
  distinction unmissable at the call site. (`docs/pgm/include-resolution.md` §2)

- [ ] **B56 — Parse `<score>` and `<flags>` so an include-supplied objective is actually read.** Include
  resolution landed (`FEATURES.md`) and measured its own limit: **82 corpus maps take their objective from a
  fragment** (`bridge`, `touchdown`, `ffb`, `flag-battles`, `5cp`), and resolving them changes nothing about
  what the studio reports, because `<score>` (TDM) and `<flags>` (CTF) have no parser here. Splicing is not
  what closes that; a parser is. Until one exists those maps read as objective-less — which the
  supported-range gate deliberately tolerates, since a module arriving from a fragment round-trips through the
  include reference and cannot be silently lost. Add each tag to `ParsedObjectiveModules` as its parser lands.
  (`docs/pgm/include-resolution.md` §4)

- [ ] **P7 — [Deferred decision] Consolidate the scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-pass extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-pass default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4. The naming half is settled — the family is the *scan*
  (`SurfaceExtractors`, `SurfaceParquet`, `PgmStudio.Analysis.Scan`, `WS13`), and its rows are named for what
  a row is rather than for the pass that wrote it (`segment`, `Q8`) — so what is left here is only whether
  the passes themselves consolidate.

### The plan model: pieces, and the edges between them

`PieceInterfaces` turned every seam between two plan pieces into a read — its height delta, its typed wall,
each side's frontline share, the straits between bridged islands — and the lint table quantifies over it
(`SP8`/`SP9`/`ST8`/`ST9`/`BZ11`/`FR8`/`CT12`). What is left is one number nobody has stated, one read the
seams support and nothing asks for, the word the model uses for a seam — and the rules that are about a
piece's own geometry rather than about what is stamped on it: what a spawn's ray faces, what a wall seals,
and what a `subtract` takes away.

- [ ] **B243 — Set the absolute minimum width of a frontline crossing.** `FR8` lands the *share* rule — a
  crossing covering under a third of the face it docks against — and deliberately states no absolute width,
  because board scale decides it and the example boards were never sized honestly (author). So a 10-block
  crossing on a 30-block face passes the share and is still a funnel nobody reads as a crossing. The width is
  a number the author states; `PieceInterfaces.Frontages` already serves `FrontlineBlocks` raw on
  `POST /plan/inspect`, so the rule is one comparison once the number exists.

- [ ] **G143 — the board deriver calls segments "edges", which is the one word the model reserves.**
  `model.md` fixes the vocabulary: an **edge** is one full side end to end, a **run** is a contiguous
  stretch along a boundary, an **interval** is where two things touch. `BoardStructure` breaks it —
  `FrontEdges`, `IntraEdges`, `SelfEdges` and `RedstoneEdges` are all `List<(X1,Z1,X2,Z2)>` of
  **cell-boundary segments**, not edges, and the deriver's own comments already call the grouped result
  a *run* (`GroupFrontlineRuns` → `FrontlineRuns`). So the code contradicts itself in one file: the raw
  list is named for a full extent, the grouped one for what it actually is. Rename the four to
  `FrontSegments` / `IntraSegments` / `SelfSegments` / `RedstoneSegments` (or `…BoundarySegments`), and
  sweep the comments that call a segment an edge. Mechanical — the type is a tuple list with a handful
  of consumers (`BoardDeriver`, the deriver gallery, the evaluator terms reading front edges) — but it
  has to land in one commit with the doc, or `model.md` will assert a rule the code visibly breaks.
  Check `BoxEdgeInterface`/`EdgeSpan`/`EdgeInterval` in the same pass: those name a genuine full edge
  and its sub-intervals, so they are correct and should stay, which is exactly why the deriver's misuse
  is worth removing rather than tolerating.

- [ ] **B177 — Implement `SP7`: a spawn's iron stands beside or ahead of it, never behind.** No code anywhere
  matches `SP7` — it is written in `rules.md` and served as prose by `GET /api/rules?rule=SP7`. The ray is
  already walked: `LintSp8`/`LintSp9` take a spawn's `Facing` through `DoorDirection` and step out of the
  piece along it, so this is that ray asked of the **iron marker** — the offset along the door's axis,
  complained about when negative. Separately, an iron cube enclosed by the spawn's own protection union is a
  resource nobody may contest; that is `WX9` placeability's question and `IronResolution.Placeable` is where it
  is answered.

  *author, 2026-08-14 · Haiku CTW Rush's iron at `(−10, −65)`, five blocks behind the spawn point and inside
  the map's `red-spawn` rectangle `(−20,−70)`–`(20,−40)`.*

- [ ] **B213 — Keep a wall's seam intact, or make the wall follow the shape.** A wall's rect is fixed at
  compile from the interface its two plan pieces share, and nothing afterwards holds that seam: resize or
  re-bow either shape in the sketch tool and the wall stays where it was, spanning less than the lane it was
  drawn across, with no refusal and no warning. A wall slows an attack and gives defenders a base to build on
  **without players tunnelling around it** (author) — both halves need the bedrock line cutting its lane in
  full, so a shortened wall is a gameplay failure, not a cosmetic one.

  *`opus5-coldharbour-v2-authoring.md` §6: an organic pass bowed a wool lane's coasts past both ends of its
  wall, players could walk round it, every call answered 200, and the only symptom was traversability moving
  from 2 isolated markers to **0** — the direction that reads as an improvement. The workaround was to veto
  any edge within 10 blocks of a wall rect, read out of `POST /plan/inspect`'s structures feed.*

  **Four ways out, all the author's, and they compose rather than compete.** *(a)* Stop fusing plan pieces
  into one sketch shape when a wall sits on the seam between them, even at equal height — the fusion is what
  destroys the seam — and lock the four corners that bound it. *(b)* Make the wall a **dressing prop**, which
  lets it take a diagonal, and refuse it when its cut does not cross a shape in full, i.e. when either end
  does not land in void. *(c)* The combination: it stays authored in the plan, the shapes either side are not
  combined, and the sketch tracks it as a prop from there on. *(d)* Store the wall's position and let the
  **export auto-extend it** to whatever the shape has become. Settle which before building; *(a)* is the
  smallest and *(b)* is the one that adds a capability.

- [ ] **B171 — Document how a wool approach attaches to a hub, in the shapes endpoint's terms.** An agent
  placing one **reads `GET /shapes/catalog`** for the valid base shapes and how each attaches, and authors from
  that; it does not run the generator (author). Reach rather than capability, and upstream of `PL13`: a dock
  seated against the hub has a face to wall that is not its own door, so getting the attachment right removes
  the wall fault as a side effect.

  *author, 2026-08-14 · Weirgate's `dock-w` touches only `front` and `lane-w`; `hub` is a lane away, and the
  dock's south edge sits flush on the build region's northern line at `z −20`.*

- [ ] **B181 — Measure what a `subtract` cuts away and what passage it leaves.** A cut that separates the two
  teams and a cut across one team's own approach are the same operation and no check distinguishes them. Two
  numbers do: **the share of the board's width the cut takes**, and **the passable ground left around it** —
  both computable from the layout before a build.

  *author, 2026-08-14 · Kilnrow's `flue` subtracts `x −44…44` at `z −39…−16` and its mirror — 88 blocks of a
  136-block board, 65% of its width — leaving two 24-wide side channels, while `z −16…16`, where the two sides
  meet, is solid ground.*

### The destroy stamps: what a goal is built out of, and what sits under it

A destroyable and a core are the one structure the studio builds from a marker rather than a footprint, and
every entry here is about the blocks around that marker: what an author may ask for, and what the editor lets
them ask for it with.

- [ ] **WE3 — A core's plate caps the dig its own float/leak pair can ask for** (author). The goal's bedrock
  plate sits three courses under the ground (`StructureStamper.PlatformDepth`), so the diggable terrain under
  a core is three courses and a `float`/`leak` pair stating a deeper dig states one the bedrock refuses —
  `DC2`'s `max(0, leak + 1 − float)` is unbounded above. Two numbers, one of them the author's, so which gives
  is a ruling rather than a derivation: cap the pair at the plate's depth and refuse past it, deepen the plate
  to whatever the dig asks for, or drop the plate under cores and keep it under destroyables. The check, once
  ruled, is a plan rule beside `DC2` in `PlanValidator` — both numbers are stated there.

  *the shipped pair asks for no dig at all (`float 6`, `leak 5`), so nothing built today trips it; `leak 9`
  over `float 6` asks for 4 and gets 3.*

- [ ] **G161 — the casing panel lets an author build a core the compiler will refuse.** `G160` put the
  casing knobs in the plan's marker panel, clamped independently — size 1..64, shell 1..16 — so a 5×5 casing
  with a shell of 3 is one keystroke away and leaves `size − 2·shell = −1`, a solid block of obsidian that
  can never leak. `PlanValidator` catches it with a good message, but only at the **compile** gate (422):
  the live inspect/evaluate feed does not run `Validate`, so the author sets the number, sees nothing, and
  finds out a phase later. Either state the interior in the panel as the two numbers move (it is the honest
  readout — "3×3×3 lava inside", or the reason there is none), or run the structural findings in the live
  feed so every rule reports where the edit happened. The second is the general fix and covers the
  destroyable style and float/leak rules too.

*moved here by the author*

- **G76** — the marker inspector exposes a structure's knobs (destroyable styles, core size/shell,
  wool colour) instead of silently defaulting.

### Trees: what a species means, and what a grown one is made of

The template trees select a material and a profile together; a grown tree is a recursive skeleton whose shape
is the author's. Three of these are the same defect measured on different subjects — **wood against leaf**,
counted rather than eyeballed — and two are the material a species is supposed to pick.

- [ ] **B96 — Density wants measuring as canopy share, not as a leaf count.** The leaf count is the only
  honest measure of *whether a forest was planted* — nothing but a tree lays a leaf, and a building's corner
  posts are logs — but it is a poor measure of **how wooded a board reads**, and two measurements on one board
  size prove it: a spruce forest at 17,600 leaves over many sites rendered as one solid mass with the routes
  buried, while `thornwake` at 17,897 leaves over 72 trees renders as a wood a player walks through. Nearly
  the same count, opposite maps, because the leaves are divided among a tenth as many trees. The number that
  would decide it is the share of ground columns standing under a leaf, which is a cheap read over the same
  voxels the census already walks, and it is scale-free in a way a raw count is not — a 120×240 board and a
  240×240 board do not want the same leaf count to read the same. Report it beside the leaf count and give the
  README a band in those terms; the two numbers disagreeing is itself informative, since a high count at a low
  share is a few enormous trees and a low count at a high share is scrub.

  *the author's side of the same gap: `opus5-elderwold` was briefed "as dense as you can make it without
  throwing any warnings", and with no measure of how wooded a board reads the density was tuned by looking
  at renders and counting declines.*

- [ ] **B154 — `species: "dark_oak"` must build a dark oak tree.** The species selects the right template and
  the wrong material. Filed as measured rather than diagnosed — `docs/world-export/tree-corpus.md` was not read
  and it may be intended. Cheap to settle.

  *`opus5-run2` §5 #9 · `basalt-reach` at `(−60, 12)` reads `17:12` log and `18:4` leaves under a nine-block
  trunk and a broad crown.*

- [ ] **B174 — Reopen `G173`/`MG28`: the whorled tree is four parts wood to one part leaf.** Over one board,
  one species, one build:

  | form | trees | logs | leaves | leaves per log |
  |---|---|---|---|---|
  | `grown` + `whorled` | 5 | 228 | 287 | **1.26** |
  | template spruce | 3 | 42 | 222 | 5.29 |

  Forty-six logs per whorled tree against fourteen per template one. `MG28` closed the trunks-with-no-crown
  report as "does not reproduce" because "whorled lands 1136 leaves" — but an absolute leaf count cannot
  distinguish a leafy tree from a wooden one, which is `B96`'s fault recurring on a different subject. **Neither
  closes again on a leaf count.**

  *author, 2026-08-14 · block census over both groves in `maps/tallow-mirefast/region`.*

- [ ] **G173 — the conifer's bulk sits at mid-height, not in the bottom third.** The whorled tree now rings
  its whole trunk (three to five branches per ring, 5.2 courses apart, each ring shorter than the last, none
  forking, a spire at the apex) and clears the first of the two measures the corpus separates families on:
  **63% of its foliage in the lower half against the staggered form's 49%**, inside the hand-built conifers'
  60–77%. The second is still out: its **widest tenth is #4.4** where a hand-built conifer's is #1–#3. The
  ring-length taper and the apex are already spent; what is left to try is the ring *spacing* (a hand-built
  conifer's tiers are evenly spaced but its lowest tiers are much the longest, which a linear taper across a
  short axis cannot express) and the leaf cluster's own size per ring. `tools/tree-corpus/crown-profile.cs`
  measures the corpus side.

- [ ] **G176 — a grown tree still carries twice an author's wood at mid heights.** The corpus is nearly flat
  in height — 23 blocks of wood at 5–9 courses, 36 at 10–13, then 51, 53, 53 to 40 — and the grower now runs
  13, 19, 47, 82, 113, 173, 221, 322 over heights 6 to 40. The trunk radius and the lateral count no longer
  scale steeply, so what remains is limb *count*: an author's median tree carries three limbs off its stem
  where a 20-course generated one carries thirteen. Cutting them further trades against crown coverage, since
  each tip is a cluster, so the honest next step is to let a cluster cover more than one tip rather than to
  starve the crown. `tools/tree-corpus/grower-gate.cs --by-height` against `census.cs` is the reading.

### Distance, and the walk every measure is taken with

`Geom.Walk` is the traversal now — eight-connected and octile, charging a climb in the blocks a player places,
counting a fall, slowing through water, narrowed per team where an `enter` rule bars one — and it runs over a
set that reads a surface as somewhere a player can stand rather than as any column holding a block. `KitReach`
and the two walk reads ask it.

Most of the studio does not. A goal's walk to a spawn, a wool separation, the corridors the coverage read
paints, the detour factor a relief budget would need: all of those still come from `Cells.PathLength`, an
**unweighted 4-connected BFS** over the flat Manhattan proxy. The entries below share that cause and want
reading together — the callers still on it, the bands they are compared against, and the demand set they are
asked over.

- [ ] **WS1 — The corridor allowance wants restating where a map runs thinner than kanto.**
  `GroundCoverage` now reads a ribbon at an absolute `Walk.Detour` of 10 blocks, calibrated against
  `wheal-hazel` and its rebuild (`FEATURES.md`). What is left is the one thing the author flagged and the
  calibration cannot settle: **10 blocks is right for a board of that size, and maps exist with thinner ways**.
  A lane genuinely 8 blocks wide pays the same allowance a 40-block one does, so a route treats the thin map
  as loosely as the wide one.

  The likely shape is to scale the allowance by the clearance actually available across the lane the journey
  is in — `Cells.Clearance` already answers that per cell — rather than by a constant. It wants a traced board
  with thin ways to test against; nothing in `tools/seeds/traced/` has one measured.

- [ ] **WS3 — A board has fork points, plural, they belong to a demand set, and `RouteFork` reports one.**
  `PlanRoutes.Fork` takes the last cell common to *every* option and the first common to every option from the
  target, so a journey with several decision points lands a split between them and describes none of them.
  Compute a branch point **per option pair** — the last cell that pair shares — and report the set, each with
  the pair it separates and how long the choice is live.

  **And a fork is not a property of the board.** townside carries three (author): one leaving spawn, round the
  hole the build zone frames; one at the second hole by the wool; and a third at *that same hole* for the run
  back out with the wool, which is a different choice over the same ground. So a fork has to be reported
  against the demand set it was read for — attack, defend, or the back-run — and the same hole can answer
  differently for each.

  *measured: townside's per-team lateral spread across the attack runs 41 · 49 | 5 · 5 · 11 · 3 | 14 · 41 · 33
  | 7 — wide, narrow, wide. The single split reads (3,−8), inside the narrow stretch that is neither of the
  two the attack actually has.*

  **The narrow middle is not a funnel and must not be scored as one.** The two teams' median lines run 35–50
  blocks apart through it and converge only at the objective: the crossing carries two ways and neither team
  chooses between them, the same one-per-team partition ingwaz shows. Per-team spread cannot separate *one
  way* from *two ways, one each*.

  *Blocks the same-road read: `d(defender→fuse) + d(fuse→wool)` equals the defender's own walk on townside
  (210 = 165 + 45) and exceeds it on kanto (115 against 95), which is the difference between the two sides
  sharing an approach and the defender arriving from behind the objective. That test rests on a fuse position
  this entry says is wrong on townside, so it wants re-checking once the forks are per pair.*

- [ ] **B175 — Add a goal↔goal walk to `GoalDistances`, and a rule over it for a team's own goals.**
  `GoalDistances` already walks the fanned closure to each spawn in the settled unit; the goal↔goal walk is the
  same traversal with a different target, and `GoalDistances` already walks a `Walk.Field` out of each goal,
  so a second read of the same field answers it. The band lives on the term as `SoftTerm.AuthoredBand`, the
  way `GO1` does, and the number is stated: **`GO2`, 35–65 blocks by walk**. It is the destroy-side
  counterpart of `WL7`, which already separates a team's wools.

  *author, 2026-08-14 · Haiku DTM Tower seats a destroyable and a core on one piece, `red-monument-region`
  ending at `x −9` and `red-core-region` starting at `x −1` — eight blocks of clear ground, both sky markers
  ten apart. Two well-spaced boards read 70 (`tallow-kilnrow`) and 74.3 (`basalt-reach`) in the same retired
  unit.*

- [ ] **B179 — State how far opposing goals stand apart, and how much of the board the contest uses.** The
  first is `B175`'s walk read across the axis instead of within a team — one traversal serves both, and
  `GoalDistances` already fans the closure so a route may cross the boundary. The second is `GroundCoverage`
  (`B241`), which already classes every ground cell reached, decorated or dead: "how much of this board is the
  contest using" is measurable there rather than inferable from a bounding box. The number is stated:
  **`GO3`, 85–150 blocks by walk** between opposing goals.

  *author, 2026-08-14 · on a 240 × 190 board every Ashfall Scar objective sits on `x = 0`, the objective set
  spans `z −37 … 38`, and opposing monuments stand 19 blocks apart with no obstacle between them. The author's
  reading — a stalemate rather than a rush, and a board too large for what it uses — is a gameplay judgment and
  is the author's; the numbers are what it rests on. The goals being visible from spawn is the good half and
  the door's approach keep-out protects it.*

- [ ] **B169 — Complain about spawn ground that carries nothing and contests nothing.** Raw size is not the
  test (author): a spawn seated on a large rectangle that *is* the map is fine, and Mirefast's 92-wide
  `steading` at least carries nine houses and two ramps. What fails is flat dead area around a spawn placed at
  the back. The rule id exists — **`SP2`**, "a spawn sits near the back of its lane, because the space behind
  a spawn is dead space" — and it composes with `ST9` (piece ≤ 20×20) and the door's approach (the first
  20×20 in front of the door kept clear), so the measure to add is *what is this ground for*, not how wide it is. The 15-block
  figure is a rule of thumb for the common case, not the rule.

  **`GroundCoverage` answers this directly once it is honest.** *Dead* is already exactly "ground with no
  route through it, no objective near it and nothing on it", named per patch with an area, a centroid and a
  walk to the nearest used ground — which is the measure this entry asks for, phrased as the picture rather
  than as a width. The walk it draws corridors with now prices a climb and a crossing and knows which
  ground is granted, so what remains is the picture itself rather than the measure under it.

  *author, 2026-08-14 · Weirgate's `yard` spans `x −40…40` against a spawn piece of `x −10…10`; Mirefast's
  `steading` is 92 wide for a 20-block spawn. The corpus does not support a spawn-isolation rule: `dtcm` puts a
  spawn a median 7.5 blocks from the board edge and the generated ones sit 5–15 out.*

- [ ] **S47 — A pressure budget for relief.** S43 measures what terrain charges; nothing says how much
  charging is too much. The dressing stage has the identical gap (`world-export/ideas.md` G167) and the two
  should share an answer. The materials exist — the share of the board at each passability tier, the detour
  factor between key places, the ford count and direction on a barrier, the reachable share per team side —
  and the corpus pass has now run on the right surface (`world-export/relief.md` §12, 105 maps, natural ground):
  body relief median **19 blocks**, walk median **72.6%**, barrier median **18.3%**, largest walkable place
  median **29.4%**, **8** cliffs. Filtering the architecture out made the terrain read *steeper*, not gentler
  — a building's flat roof was smoothing the reading — so the tier shares were never the distorted numbers;
  the **cliff count** was, and heavily (Alpine Mining II: 36 cliffs off the built surface, 13 off natural
  ground). What is still missing is the shape of a rule: a median is not a target, and a map at the 25th
  percentile for walkable share is not thereby worse than one at the 75th. That needs labelled examples of a
  *bad* map rather than more measurement — and the **detour factor between key places**, which is the
  material most likely to separate them, is measurable now that the walk prices a climb — a detour factor
  reads ≈1 only on ground that is genuinely flat.

  *Filed under `S` and living here because the measure is what blocks it; the id does not move with the heading.*

- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) still diverges from the rect-layer authority `ContactGraph` on one count: any area overlap
  connects regardless of surface delta, while `Components` unions an overlap only at `SurfaceDelta == 0`.
  (The corridor-width half was reconciled — `LandAdjacent` now accepts Narrow seams, matching `Components`.)
  Pick one rule for the overlap case and add a test; needs per-node surface carried into the fanned graph and
  validation against the traversability harness (`tools/PgmStudio.RoundTrip --traversability`).

  **It gates route enumeration, which raises it from a consistency chore.** `G127`'s flow read counts attack
  routes at piece fidelity — four on `p30-s374`, from two frontline legs × two wool doors — and a route count
  is an enumeration over piece adjacency. While the two graphs disagree about what "adjacent" means for an
  overlap, the count depends on which one was asked, and nothing at the call site would say so. Whichever
  rule is picked, the route reader must name the graph it read.

- [ ] **G187 — Plan-tier flow: the cut, the ways round a hole, and the terms over them.** Every route
  measure in the repo runs on a built world; `ContactGraph.CorridorMin` is a contact-width threshold and not
  a corridor, so a plan is evaluated with no flow read at all. The inputs are already here —
  `PlanBoxAnnotation.Apply`, `StructureSummary.Derive`, `PlanModel.Boxes` and `ContactGraph`'s proxy-cell
  mask — and `WS1` supplies the ribbon. What is missing is two more `Geom.Cells` primitives:
  **`MinVertexCut`** (unit-capacity vertex max-flow, the funnel capacity `match-flow.md` §2 asks for) and
  **`WaysRound`** (the ray-cut connectivity test). Then a flow derive beside `BoardDeriver`, which makes
  `G164` a short consumer rather than a project, and lets a dead-share term fire at `POST /plan/evaluate` —
  the first call in the loop, before a map row exists.

  **Do not count the connected components of the minimum cut for ways-round.** That was tried and it gave
  the opposite answer on the same corpus: "rotation never splits on any ring board" against "splits on
  nearly all of them". An uncuttable door cell inside a single barrier splits it into two fragments with no
  second route, and a real second way is missed whenever the cheapest cut lies elsewhere.

  *Two-legged frontlines: 265 objectives, **97%** reachable more than one way; a plain bar, 375 objectives,
  **38%**. Second ways are a median 1.31× the first and never worse than 1.92× — routes, not escape hatches.*

- [ ] **G164 — interference: how much of one side's route the other side's route covers.** Every flow
  measure so far reads one traversal at a time, and a single route cannot express tension. Tension is two
  corridors laid over each other: the attacker pushing from a captured wool room toward the remaining
  objective, and the defender travelling from spawn to the same objective. The measurable is the fraction of
  the defender's corridor that the attacker's corridor also covers, computed on the cell mask the same way
  the corridors already are. Measured over 453 two-wool boards at `marker-id-1`: median **34%**, half or more
  on 27%, and **no board reaches zero** — passing the reinforcement lane is unavoidable on generated output.
  This is the term that gives a hub void a purpose the ways-round-a-void count cannot: on a holed hub the
  near way leaves 76% interference and the far way 37%, and the far way measurably reduces the collision on
  74% of the boards offering one, so a layout whose two ways collide equally has bought nothing. Derive side
  belongs beside `BoardDeriver`; the term belongs in `Evaluate/Terms`. It reads a pair of routes rather than
  one, so the origin "a captured wool room" comes from G168's post-capture state — until that exists,
  computing it once per wool treated as captured is the honest stand-in. Background and the full numbers:
  `docs/gameplay/match-flow.md` §2, §4.9.

- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.

## What a board cannot be told, and what it cannot be asked

Eleven boards were authored through `pgm-studio-mapgen/tools/drive.py`, and the driver is the record of what
the studio does not answer: a statement an author has to make with no field to make it in, and a question
whose answer is inside a refusal, a solver or a palette and which no read returns — or one a read does answer
in a field no driver printed. Not one of these faults was caught by a gate; the export gate was open, the
mirror clean and the traversability whole on every board named below.

**These were the focus until the map review opened the one on `TODO.md`.** `WE31` went up with it, because the
review measured the fault it describes. The rest wait here, and the two relief entries — `WE32` and `WE33` —
are the mechanisms under `WE38`'s definition, so they are the first back when it lands.

- [ ] **TS32 — The compile fuses terrain by surface height, so a plan's piece names do not reach the
  layout.** A plan states named rectangles; `PlanCompiler` fuses every abutting run of them at the same
  `base_height` into one polygon and the names are gone, so nothing downstream can address a piece. Carry the originating
  piece ids on the compiled shape (`pieceIds: ["room-w", "ledge-w"]`), or emit one shape per piece and let
  `RasterGroup` fuse at raster time. Either one deletes four of the driver's fourteen keys —
  `themeByHeight`, `themeById` and both `shapeProps` — which exist only to address the accident. Lands in
  the compiled `SketchShape` and in `docs/tools/sketch.md` § Shapes; `docs/tools/plan.md` § *what it
  compiles to* changes in the same commit.

  *`opus5-thornfell`: sixteen pieces → one polygon, `s0`. `opus5-rimegarth`: fourteen → one. `opus5-kiln-row`:
  seven pieces at three heights → four shapes. Shape count tracks height and adjacency, never the piece.
  Downstream cost: **five specs
  carry a private "is this point on land" predicate** rebuilt from the plan's own rectangles, under five
  names — `on_land`, `in_rects`, `land_halfwidth`, `cells_open`, `on_ground` — because the layout cannot
  answer it.*

- [ ] **TN7 — A plan cannot say which storey a goal stands on.** `layer` is a field on all six `MapIntent`
  placements and on `PlacedProp`, and on neither `DestroyablePlacement` nor `CorePlacement`. So the word
  exists everywhere the export reads it and nowhere the plan writes it, and a plan-built goal on a stacked
  board always resolves against `SurfaceTop` — a monument stated for a basement lands on the deck roofing
  it. A nullable string on the two placement records, carried through the compiler. Small.

  *`opus5-interchange` needs the word on four of its five goals a team, and gets it by having
  `tools/drive.py` write `layer` onto the compiled intent by `stamp.unit` before it is stored. Confirmed
  against `GET /api/openapi/v1.json`: the property is on the intent schemas and absent from both plan
  placement schemas.*

- [ ] **WE30 — `TerrainPainter.Paint` trusts document order for a stack that knows its own floors.** The
  painter walks `SurfaceByLayer` in document order and each pass paints its whole column from the bedrock
  course up, so a storey listed *after* one that stands over it finds no stone left to paint. A compiled
  plan emits `layers[0] = ground`, and the compiled ground is not the bottom of every board. Sort ascending
  by each layer's own floor instead: document order is an authoring accident on any board whose ground came
  from a compile. `docs/world-export/terrain-painting.md` states the per-layer scope and does not state that
  the painting is ordered.

  *`opus5-interchange` at `(20, 70)`: yellow stained glass from `y0` to `y25` in one column — a 2×4 door
  panel painting twenty-six courses — because the undercroft slab was appended rather than inserted. After
  moving it to index 0: white clay `y5..3`, hardened clay `y2..1`, corridor brown from `y12`.*

- [ ] **TS30 — An organic coast is either stated twice or computed outside the studio.** The compiler emits a
  staircase of the plan's rectangles, which is the board's *shape* and not its *coast*. `opus5-ravensmere`
  redrew its ring by hand in the finish — the coast stated once in the plan and once beside it, free to
  disagree; `opus5-thornfell` bent the compiled ring instead, which moved the disagreement into forty-two
  lines of Python. `PgmStudio.Geom.RingRounding` already fits the Catmull-Rom handles the driver re-derives —
  and has no caller in `src`, only a test — so the missing thing is a *stated* bow: a seeded, deterministic
  inward wander over a resampled outline, taken at compile or as a sketch operation, handed to `RingRounding`
  rather than to a second copy of it. Two rules make it safe and belong with it — the plan's own vertices
  never move, and nothing ever moves outward.

  *`opus5-thornfell`: 36 compiled vertices → 99 drawn, strait measured at **26–28 blocks** over 23 transects
  against a plan stating a flat 30. A vertex moved outward would have closed it.*

- [ ] **RP58 — The export zip nests the world inside a folder named for the slug.** `--out` is what a server
  is handed and holds `region/`, `level.dat` and `map.xml` at its top, so every caller unwraps it. Emit it
  flat — and say what the browser download becomes, since the same bytes are served as `{slug}.zip` and a flat
  archive unpacks loose into whatever folder it lands in. `MapExportEndpoint.BuildWorldZip` prefixes every
  entry with `{slug}/`, in one place.

  *Both mapgen branches independently wrote the same un-nester into `tools/drive.py`; the merge kept both
  and they ran in sequence until one was deleted.*

- [ ] **WE32 — A push has two gradients and the read-back reports neither.** A push climbs at `amount /
  falloff` over its skirt and at `crown / half` from the ring's edge to its medial axis, and where those two
  disagree the landform has a step at its own outline — a cliff with a hill on top of it, whatever its height.
  `relief/read` reports the *face* — `faces` carries its facing, width, drop and whether it qualifies as a
  cliff — but says nothing about which push made one, and neither gradient is ever stated, so an author
  holding a step has no way back to the knob that cut it. Answer each push's two gradients, and lint where
  they differ by more than about 2×: that ratio is the number an author is actually choosing.
  `docs/world-export/relief.md` § the push.

  *`opus5-thornfell`: `amounts 22–36 · falloff 12 · crown 16` sections as a vertical face; the same range at
  `13–17 · falloff 10 · crown 12` — both gradients ≈1.7 — sections as a mountainside. Both read identically
  through the five fields the driver prints (`cells`, `low`, `high`, `relief`, `symErr`) of a response
  carrying twelve.*

- [ ] **WE33 — Two flat marks with touching radii build two terraces and a step at the seam.** A mark pins
  every cell in its radius exactly; the relaxation, which is what makes ground roll, owns only what is left
  *between* marks. So two marks placed to describe one slope describe a wall instead, and the wall is reported
  only as terrain — a `steps` bucket and a face, attributed to nothing. Have the relief read answer the worst
  step between any two marks' pinned sets, naming the pair. The neighbouring case belongs with it: **a mark
  placed wholly out of reach does nothing and raises no `SK3`.** `docs/world-export/relief.md`.

  *`opus5-tarnfell`: `shore-lo` at `y8` and `shore-hi` at `y14`, radii touching, transect
  `…7 7 7 7 [+5] 12 13 13…` — a five-course wall right round the lake. Pulled seven blocks apart:
  `7 7 7 7 9 11 12 13 13`.*

- [ ] **WE34 — Nothing answers "where may this stand?" — only "no".** Five declines exist — `DR-SITE`,
  `DR-KEEP`, `DR-CLAIM`, `DR-ROAD`, `DR-SLOPE` — and every one is a filter on a placement already made.
  Run the same predicates forwards: one read taking a footprint and a prop kind and answering the candidate
  cells. Every predicate is already written. The *shape* of the answer exists three times for goals —
  `monument-suggestions`, `core-suggestions`, `wool-suggestions` — but each scores scanned candidate rows
  rather than asking a placement rule, so this is the first read that runs the rules themselves.
  `docs/world-export/decoration.md`.

  *Two agents wrote the same search independently rather than place by eye. `opus5-ravensmere` and
  `opus5-thornfell` got `yards()` — whole footprint inside a flat, outside every push's exclusion, twelve
  blocks off every track. `opus5-rimegarth` got its own after **fourteen declines** on one pass, and the
  honest answer for a hundred-block CTW board turned out to be **nine legal spots**.*

- [ ] **TS31 — A shape drawing ground outside every island is silent.** A one-course add on a cell no region
  shape covers is the only add on that column, so it builds a speck of bedrock standing over the void; a shape
  drawn wholly on the mirrored half is outside the compiled polygon and becomes an island of its own. Both
  pass the sketch PUT without a word — and `SK11` is the rule that should have said so.
  `SketchRasterizer.DetachedMasses` drops any component sharing no column with a second one (`// beside, not
  above`), so it reports a storey whose stair was never drawn and never an island standing *beside* the board,
  which is the case an author actually draws by accident. Report a component that is neither reached nor over
  anything, under `SK11` rather than a new id. `docs/tools/sketch.md` § Refusals and complaints.

  *`opus5-ravensmere`: `GET …/coverage` reported **141 cells at (−24, 91), 364 blocks from used ground** —
  a disconnected island made of paint. Nothing before that read mentioned it, and every spec now clamps and
  folds each stroke by hand to avoid both cases.*

- [ ] **WS14 — A declared route is never read back.** `route: true` on a stroke makes `DR-ROAD` measure
  every other prop's standoff to it, and nothing reads the stroke itself. `GET …/walk` answers a journey the
  walker chooses, which is not the one that was drawn. Add a read that walks a declared route end to end and
  reports its worst step, its material run, and where it leaves the ground. `docs/world-scan/read-backs.md`.

  *Every board verified its paths with a throwaway per-block column transect — `opus5-thornfell`'s four
  tracks, **308 cells**, checked for a step greater than one. Written fresh each time and thrown away each
  time.*

- [~] **RP23 — `docs/tools/capabilities.md` is 707 lines answering "what can I ask for", which the API now
  answers itself.** The schema names every route, its body and its failure codes; `GET /api/rules` names
  every refusal with its fix; `GET /map/{slug}/state` puts the allowed moves on the map's own response.
  What prose is good at and this file is not organised around is the other half: **how to make a good map** —
  what an objective needs around it, what the corpus does — as against **what the system can be asked for**.
  Split it on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.

  The mapgen half landed: `pgm-studio-mapgen`'s six root documents became two, and `AUTHORING-BRIEF.md`
  points at the four self-describing reads instead of restating them. This entry is the studio's own side.

  *Five authored boards never opened it. What they read instead was `relief.md`, `decoration.md`,
  `terrain-painting.md` and the endpoint tables inside them — the split this entry proposes, observed.*

## The remainder: work no concept above has claimed

- [ ] **WE13 — The catalogue map cannot export, and both doors agree on why.** `tools/library-map.cs` emits a
  grid of 37 unconnected plots; `GET /map/{slug}/export` refuses it **409 `EX1`** — *3 spawn/objective
  point(s) are not reachable from the rest*, naming `spawn red-team`, `wool red` and `wool blue`. It is the
  map's own shape rather than a route's: a catalogue is a row of islands nothing bridges, and `EX1` asks
  whether a match can walk between its spawns and its objectives. Either the wool rooms and spawns move onto one plot,
  or a board that is a catalogue says so and is exempted. Which is the author's — the map exists to be walked
  plot by plot, not played.

### User Experience and Graphical User Interface

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
`/maps/{slug}/plan` from a single component through five `@if (MapBacked)` branches, and the two render as
different products. Map-backed gets the phase rail (Info · Draw), the flow bar, and the three panels as chips;
the bare route gets no flow bar, no phases, the same three panels as **rail buttons**, and a collapsible
sidebar the map-backed one cannot have (`SidebarOpen => MapBacked || leftOpen`). Same panels, two navigation
models, one file — the thing the tool-consistency alignment exists to prevent.
Unify on the phase-rail + flow-bar + chips structure and keep the collapsible sidebar for both. The route may
change **only** the topbar — its crumbs and which actions exist — because that is where the binding genuinely
differs: a map-backed plan saves into its map's artifact, while a plan row saves as a row and forks when it
was generated or imported. Rename the bare route to `/plans/{id}` (and `/plans/new`), which says what it is
bound to where `/plan-editor` says nothing, updating the generator hand-off, the smoke sweep's route list and
the plan schema doc with it.
**Do not delete the route.** It is the only surface that opens a **plan row**, which is what the generator
hands a candidate off as and what `G119`'s fork-on-edit rule operates on; routing candidates through
`/maps/{slug}/plan` would mint a map per candidate looked at, and New, Import, Open and the origin badge have
no home on a map-backed plan.

- [ ] **B79 — The plan tool must not offer Compile before the document it would compile has loaded.**
Reached by the SPA hop from the Configuring list, the tool's canvas is in the DOM before its plan document
is. Click **Compile** in that window and it posts `pieces: 0`, the validator correctly answers `422` `PL1`
*"this plan has no pieces — there is no land to build"*, and the drawer opens anyway because its tabs render
the source document. The draft button still reads **Rebuild this map** — `BuildLabel` comes from the map —
and is `Disabled="@(compiledLayout is null || draftBusy)"`: present, correctly labelled, not actionable. A
user who clicks quickly is told their board has no land, about a board with land. Gate the button, or the
post, on the document having arrived.

The suite half is one missing wait. `map-layers.mjs:75` waits for `.map-canvas-svg`, the element that exists
too early; at `:122`, before the *second* compile, it waits 1500 ms with a comment saying exactly why.
Fixing the tool makes both unnecessary.

*diagnosed 2026-08-16 by intercepting the editor's own `POST /api/plan/compile` under both navigations:
same database, `goto` → **200**, row-link → **422**. `./tools/e2e.sh all` gives `map-layers` 13/14 with
`smoke` 39/39 in the same run; `./tools/e2e.sh map-layers` alone is 18/18. `B229` was this filed a second
time — its hypothesis, that an earlier spec breaks the stored plan, is disproved by the same test.*

- [ ] **B34 — The two map-list endpoints disagree on sort order, and the dashboard gets the noisy one.**
  `MapsListEndpoint` branches on the `stage` query param onto two differently-ordered repository methods:
  `MapRepository.ListAsync` sorts `OrderBy(Slug)`, `ListByStageAsync` sorts
  `OrderByDescending(UpdatedAt).ThenBy(Slug)`. The dashboard always requests `?stage=…`, so it always gets
  recency order — and on the imported Edit corpus `updated_at` records when the *pipeline* last wrote the
  row, not when the author last worked on the map, so it carries no authoring signal. The 349 Edit rows hold
  only 29 distinct timestamps (a re-processing pass stamped them in ~22-map batches a second apart), so the
  list renders as 29 alphabetical runs concatenated — it reads as scrambled, with the three maps outside the
  supported range (`3084`, `allure`, `lost_haven`, never re-processed) parked at the bottom. Recency earns
  its keep on Sketches/Plans/Configuring, where the timestamps are real edits and the lists are short.
  Preferred fix: slug order for the Edit stage, recency for the other three (one line); alternatives are
  slug everywhere, or leave it and let recency come good once maps are edited in the studio. Cosmetic — no
  data is wrong, and both orders are deterministic.

- [ ] **B47 — The library has no search, and the sketch's theme names are its own.** Two small gaps the
  library page left open, worth doing once it has enough rows to hurt. The style browser filters by kind but
  not by name, so a library of forty styles is a scroll; the theme half has no filter at all. And a theme
  pulled into a sketch takes the library's name as its sketch-side id, which the bridge uniquifies — pull the
  same theme twice and the second is `meadow-2` with nothing saying they are the same theme. A name search box
  on both halves, and a note on the pulled theme recording where it came from (which slots into B44's
  snapshot record rather than duplicating it).

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)

- [ ] **B54 — A rebuild has no undo.** The rebuild now carries the finish and the credits across (B49, B52)
and says what it trades before it runs (S39), so what it still replaces is replaced *on purpose*: the
board, and the teams/spawns/wools/build zones the plan states. What is missing is a way back from a
deliberate press that turns out to have been wrong. The mechanism is cheap, because both authored blobs
are already rows in `map_artifact` keyed by a 64-char `kind` with no unique constraint: before each
from-plan write, copy the current blob to a `…_prior` kind, and add a restore that puts both back and
re-runs the pipeline from them (restore layout → `sketch/finish` → restore intent, the same chain the
build uses, so the world cannot end up disagreeing with the layout). The finish step wants extracting out
of `SketchFinishEndpoint` first so both callers share it. Surface it where the loss would be noticed: a
one-shot *Undo this rebuild* in the plan editor's success panel. Deliberately not built with S39 — with
the carries landed, the remaining exposure is a mis-click rather than silent data loss, and the
confirmation already covers a mis-click at a fraction of the cost. This is the belt to that pair of
braces, worth having once the studio is used by someone who did not write it.

### Refactoring and cleanup

- [ ] **TN6 — A compile that failed leaves a button promising the build it cannot do.** The compile drawer's
  draft button is `Disabled="@(compiledLayout is null || draftBusy)"` (`PlanTool.razor:669`) while its label
  is `BuildLabel`, which reads only whether the map is built. So a **422** leaves *Rebuild this map* on
  screen, greyed and silent about why — and the json pane beside it still shows the plan, because
  `compiledPlan` is set before the request. An author sees a dead button and a plan that looks fine. Say what
  the drawer is in: label the button for the state (*Fix the plan first*, or the count of blocking findings)
  and point at the findings list already rendered above it. Found by `map-layers` hanging thirty seconds on
  that button rather than failing on the compile.

- [ ] **C45 — The authors editor fetches every avatar from a third-party host at render time.**
  `AuthorsEditor.razor:39` renders `https://mc-heads.net/avatar/{uuid}/16` as an `<img src>`, so every author
  row on the Overview and the plan's Identity step is an unpinned request from the user's own browser to a
  host nobody here reviews — the runtime-CDN shape `CLAUDE.md` § *JS dependencies* rules out, in image form,
  and dead the moment egress is restricted. It is dead already: headless Chromium in the cloud container
  answers `net::ERR_CONNECTION_RESET` for it, which is two of `configure-objectives`' ten checks. **The
  username lookup behind the row has the same problem and now costs a second spec**: `/api/minecraft/player`
  proxies to Mojang server-side, the container reaches neither, and `paint` filters the 404 out of its
  cleanliness check exactly as `configure-objectives` does — two specs carrying a filter for one dependency. Decide what
  an author row shows without it — the uuid's own colour, an initial, a vendored silhouette — and whether a
  fetched avatar is worth a server-side proxy with a cache. `AvatarEmpty` beside it is already the
  no-uuid case, so there is a fallback to widen rather than one to invent.

- [ ] **CV21 — the world canvas has a `build` layer nothing paints into.** Stating the layer stack once
(`CV19`) surfaced two layers with no content. One was removed there — a `block-highlight` rect created
`visibility:hidden` whose only handle was assigned and never read. This is the other: the `build` group is
created empty, no painter ever appends to it, and its toggle `setBuildVisible` has no caller outside the
class — not the bridge, not any of the sixteen hosts. So it is an empty group with a visibility switch
nobody throws. Removing it takes `setBuildVisible`, `#showBuild`, `#paintBuildRegion` and one line of the
documented public surface with it, which is why it was left in place rather than swept during a
behaviour-preserving refactor. Check first whether a Build phase was *meant* to fill it (the name suggests
the Build-Regions work) — if so the task is to wire it, not delete it, and that is a different task in the
feature section.

- [ ] **B102 — Clear the region directory before a rebuild writes it.** `AnvilRegionWriter.Write` calls
  `Directory.CreateDirectory` and nothing else, so every `.mca` a previous build left is still there and a
  chunk the new build does not touch — because its geometry moved — is read back as part of the new map. That
  makes rebuilding into an existing `out_dir` untrustworthy, which is exactly what iterating on a spec does,
  and contradicts the README's promise that "the same spec rebuilds the same map, so two runs can be
  compared". It cost a design session real time, presenting as building counts that could not be reconciled
  until the directory was deleted by hand. Distinct from the concurrent-build race `CLAUDE.md` warns about:
  that one is two builds at once, this one is one build after another.

- [ ] **B220 — Fix the doc-comment defects, then take the four ids out of `NoWarn` so the next one fails the
  build.** Each is a sentence pointing at something that is not there, and each is silenced in all five
  `.csproj` that emit a documentation file (`Domain`, `Pgm`, `Minecraft`, `Export`, `Api`):

  | id | the defect | what to look for |
  |---|---|---|
  | **CS1573** | a method documents some parameters and not others | a `<param>` list shorter than the signature — usually a parameter added later |
  | **CS1574** | a `<see cref>` names something that does not resolve | a member that was renamed or moved to another type |
  | **CS0419** | a cref matches several overloads and silently picks one | `<see cref="Foo"/>` where `Foo` has more than one signature |
  | **CS1734** | a `<paramref>` names a parameter that was renamed | the name in the prose against the name in the signature |

  **One member of the family no warning catches, and it misleads hardest.** Five docstrings open **two
  `<summary>` blocks on one member**, the first describing something other than what follows it — a docstring
  left behind when the member under it went away. The XML is well formed, so `CS1570` says nothing; the
  compiler concatenates both into that member's entry and the tooltip leads with a sentence about something
  else. Found by scanning `src/` for comment blocks with more than one `<summary>` open, which is what to
  re-run: `UnitRequests.cs:6`, `UnitSeating.cs:6`, `Producibility.cs:124`,
  `SketchDressingInspector.razor.cs:263`, `PlacedProp.cs:251`.

  *measured 2026-08-16 by dropping the four ids from `NoWarn` and rebuilding clean: **148 distinct sites over
  55 files** — 90 CS1573, 34 CS1574, 14 CS0419, 10 CS1734. The entry's earlier count of 36 over 26 files was
  the tail of one project, not the sweep. Worst files: `TerrainTheme.cs` (9), `SpawnBoxEmitter.cs` (7),
  `FrontlineBoxEmitter.cs` (7), `MapExportComposer.cs` (7). `CS1587` stays silenced on purpose — a docstring
  on a local function, which the compiler never emits.*

- [ ] **Comment hygiene sweep — the task ids.** Code comments must describe behaviour only, and the
  attribution half is done (B43 swept every "port of X.py" / "matches Python" reference out of `src/` and
  `tests/`). What remains is **implementation-phase and task ids** (`NS`, `N00`, `B8`, `P5`, `ND2`, …) in
  about nineteen places — a comment that says *when* something was built rather than what it does, which
  reads as noise to anyone who was not there. New code already follows the rule (`CLAUDE.md`).

- [~] **B44 — Theme + style library: the map's applied theme is still an inline blob.** The tables, the HTTP
  surface, the `/library` page and the sketch's pull/push bridge all shipped (`FEATURES.md`); two slices
  remain. **(1) Apply-as-snapshot** — a map's *applied* theme is still the sketch document's own registry, so
  "the library holds the reusable copy, the map holds a frozen one" is true only by convention: pulling a
  library theme into a sketch copies its JSON and nothing links them, but there is no snapshot record saying
  *which* library theme a map's paint came from, and no way to re-pull one when the library moves on. Give the
  map's scope store a forked instance with a `parent_id` back-reference, the same doctrine the generator's plan
  persistence uses. **(2) A data migration** lifting the themes inlined in a map's own `sketch_layout_json`
  registry into styles + themes + bindings, deduping identical materials — today a map themed without pushing
  anything out keeps its blob and the library cannot see it.

- [~] **C12 — The last of the component vocabulary: the icon, the generator, the inline styles.** The
  vocabulary is built and adopted — the atoms, `Section`, the shell and the workspace shells are across every
  production surface, with two raw `action-btn`s and two raw `list-row`s left as genuine exceptions
  (`FEATURES.md`; the reference is `docs/client/ui-conventions.md`). Three slices remain, and `/design` is the
  zero-visual-diff oracle for all of them since a component emits the classes the markup did.

  **`Icon` is built and unadopted.** `Components/Primitives/Icon.razor` centralizes the lucide reconciler
  gotcha — recreate on a glyph change rather than patch a node lucide has already replaced with an `<svg>` —
  and **156 raw `<i data-lucide>` still stand**. Adopt incrementally: the icon-bearing components
  (`Button`/`DetailHeader`/`Chip`) first, then the page sites that re-render. High churn, subtle benefit, so
  parked by choice rather than blocked.

  **The `gen-*` set is the last real drift**, and the largest thing left here: `/generator`'s filter rail, card
  grid, candidate cards, badges, tray and census tables are around forty classes in `generator.css`
  re-implementing `workspace-sidebar`, `card-grid`, `badge` and `filter-chip` under their own names. The atoms
  inside them have been picked up where they fit; the layout has not.

  **Polish**: fold the one `section-heading` use into `SectionHeader`, and drop the 84 inline `style=`
  occurrences now expressible as component params (`Fill`, `Full`, a modifier `Class`).

- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.

- [ ] **CV15 — The bridge invoke wrapper is inconsistent.** `plan-bridge` and `sketch-bridge` wrap
  `dotnetRef.invokeMethodAsync` in a local `fire()` that swallows the throw when the host hasn't wired a
  callback; `world-bridge` calls it unguarded, so an unwired callback surfaces as a console error instead
  of a no-op. Settle on one helper next to `fetch-json.js`. Tiny, but it is the only thing the five bridges
  genuinely share — the rest of their apparent repetition is per-tool document semantics and should stay
  separate.

  - [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.

### Test coverage

- [ ] **G163 — `map-layers`' rebuild-confirmation step flakes about one run in three.** The step drives
  Compile on a freshly-opened plan and reads the drawer; when the plan document has not reached the client
  yet it compiles an empty plan, which is a 422 by design, so the drawer never opens and the following
  `page.click` times out at 30s. The spec guards it with a fixed `waitForTimeout(1500)` — a duration
  standing in for a condition, and the wrong guess about a third of the time. Measured 1-in-3 both with and
  without the `OB17` rule, so it is timing rather than validation. Waiting on the first piece id label
  (`.map-canvas-svg text`, the overlay's proof the document arrived) was tried and did **not** fix it, so
  the stall is later than the document load. **A caught failure now names the click, and it is not the one
  the paragraph above blames.** The step got as far as reading the drawer's button label ("the button names
  a rebuild" passed on `Rebuild this map`) and then timed out on `page.click("Rebuild this map")` — the
  *second* click, on a compile that answered 200, long before the empty-plan compile the 1500ms guard is
  aimed at. The recorded 422 is an earlier fault on the same page, not this one. A 30s timeout on a button
  whose text was just read means the element was found but never became actionable, which points at a
  drawer that keeps re-rendering rather than at a document that has not arrived — so the fix is a wait on
  the drawer settling, and the 1500ms guard may be guarding nothing. A flake in the browser gate costs more
  than the step is worth, because it makes every unrelated run ambiguous.

- [ ] **B35 — Endpoint coverage: half the API is exercised by nothing.** `PgmStudio.Api` sits at **42.8%**
  lines (`tools/coverage.sh`), and the shortfall is not spread evenly — a long tail of endpoint files is
  effectively untouched while the tested ones are fine: `PreflightEndpoint` 2.6%, `ImportEndpoints` 3.6%,
  `MonumentEndpoints` 5.3%, `LayersEndpoints` 5.5%, `ConfigureEndpoints` 6.2%, `AuthoringEndpoint` 8.2%,
  `MapPlanEndpoints` 12.3%, `AnalysisEndpoints` 13.0%, `RegionEndpoints` 15.6% (the two island files that
  were bottom of this list were deleted rather than tested — `RP18`). `ApiTestFactory` (B20) already gives schema-isolated MariaDB, so the
  marginal cost per endpoint is one happy path plus its error contract; these are cheap tests, not a
  redesign. Prioritise the ones that write: import, configure, region and map-plan.

  **What the gap costs, measured once:** `POST .../sketch/columns` had no test, and a change to what it
  returns beside the payload shipped a null spread that answered **400 for every board with nothing wrong
  with it** — the 3-D preview blank studio-wide, found by starting the app rather than by the suite. The test
  that now covers it is nine lines.

- [ ] **B36 — The region/filter authoring-and-editing path is half covered.** A coherent cluster sits
  around 40–58% while its neighbours are high: `RegionAuthoringEncoder` 43.8% (370 uncovered lines),
  `RegionParser` 52.0% (295), `RegionEditor` 57.5% (180), `FilterParser` 48.9%, `RegionGeometry2d` 39.5%,
  `RegionBuilder` 43.7%, `FilterEditor` 41.9%, `WoolEditor` 58.5%. This matters more than the endpoint tail
  because it is map-contract logic, not glue — a silent regression here changes generated `map.xml` rather
  than returning a wrong status code, and `--authoring` is a manual harness, not a gate. Note the
  neighbours prove the standard is reachable: `MapParser` 92.9%, `XmlWriter` 88.1%, `RegionCategorizer`
  91.4%. Cover the type-specific region/filter branches first — that is where the uncovered lines are.

- [~] **C28 — The client's remaining test layers (smoke has landed).** The **smoke layer + runner shipped**
  as `C31` (`tools/e2e.sh`, `tests/e2e/`) — every route is swept for "renders and raises nothing", seeded
  from a composed board; `icons.mjs` (C30) added the first *positive* render assertion on top of it.
  `PgmStudio.Client` is still **absent from the coverage report** (no test project
  references it), and two layers are still open:
  **(a) mount/interop** — per canvas tool, assert the bridge mounted and the surface has a real size; this
  is the C29 class of bug (a canvas at 45% of its workspace for weeks, in two tools) and it is assertable
  without knowing user intent.
  **(b) scenarios** — one flow per tool, specifically *the path that creates the artifact*, where a break is
  unrecoverable rather than cosmetic: Sketch `New → name → draw → Finish → Configure`; Plan
  `New → globals → piece → Compile`. The seed already proves that chain works headlessly.
  Deliberately **not** e2e: field-level inspector behaviour and anything asserting where geometry lands —
  those rot; extract the decidable logic instead (`CV12`). A bUnit project for the phase/step state machines
  is still worth considering, and is independent of the above.

- [ ] **CV12 — Two thirds of the JS layer is never loaded by a test, and the bridges are the reachable
  third.** Of 55 studio modules (12,694 lines), the 333 tests in `tests/js/` reach 28 (4,677 lines); the
  other **27 files / 8,017 lines are never imported**, which `--experimental-test-coverage` reports as
  *absent* rather than zero, so the report reads healthier than the tree is. The untested set is the whole
  interactive layer: every canvas (`world-canvas` 1046, `plan-canvas` 1017, `sketch-canvas` 871,
  `canvas-base`, `sideview-canvas`), every bridge, every controller, `iso-webgl` and `studio.js`.

  **Two slices, and the cheap one is not extraction.** A bridge is not DOM-bound the way a canvas is:
  `mount()` is handed its canvas and elements, and the only other thing it touches is `fetch` — both
  stubbable with what `tests/js/` already has (`_dom-stub.js`, `_painter-stub.js`). Drive
  `enterIso`/`fetchColumns` with a recording canvas and a canned `fetch`: the most stateful function in the
  untested set (an await, a race guard, a cache stamp, two failure paths, and now a decoded refusal
  envelope), in the file where a rename shipped a `ReferenceError` to the browser that neither the C# build
  nor the JS suite could see. `sketch-bridge` is 904 lines, `plan-bridge` 449, and they are near-twins.

  For the canvases and controllers the answer is unchanged: keep **extracting the decidable logic** —
  hit-testing, snapping, viewport maths, selection resolution — into pure modules the existing harness
  reaches. Pairs with the JS consolidation review.

## Future

- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and the validator/evaluator return rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. **The gate this waited on is open: G119, G117 and G125 have all landed** (with
  G126's typed boxes), so the MCP head now wraps endpoints that exist rather than duplicating their
  plumbing. Of the three genuinely-new pieces, **`emit_family` is half-built**: G144's
  `GET /api/shapes/probe` already emits a canonical family through `BoxFiller` — profile check and
  docking gate included — and answers with the shape or a directed `FillRejection`, and
  `/api/shapes/probe/schema` serves the per-family knob surface and minimum box in the dock frame, which
  is the payload a tool-description needs to constrain its parameter enum. What is left of it is the
  *stamp*: placing the emission into an existing plan's typed box instead of returning a standalone
  `symmetry:none` plan. So the remaining new work is the PNG path for `plan_render`, that stamp, and the
  resource curation. Tools: `compose` (the G117 endpoint —
  request in; plan + canonical descriptor + derived facts + score out; starting material to mutate) ·
  `plan_validate` (errors + rule lint + full evaluator readout — the response must flag empty
  `placements`, which leave the feel terms vacuously green) · **`plan_feasibility`** (the G125
  read-back: mask → derived params → emit-compare, directed verdicts citing rule/task ids — the oracle
  that makes the loop converge; the validator alone passes plans the composer cannot produce, proven by
  the funnel exemplars scoring 0) · **`emit_family`** (stamp a canonical shape through the real emitters
  into a typed G126 box — agents never hand-cut rectangles) · `plan_render` (image content — agents
  self-correct far better seeing the board) · `plan_save`/`plan_get`/`plan_list` (the G119 store, with
  an agent-authored origin marking so agent output never contaminates the human-labeled corpus) ·
  `create_draft`/`export` (existing chain; return the export **link**, never the world zip inline). MCP
  resources: `generator/rules.md` + `generator/model.md` as the design brief, **`GET /api/shapes/catalog`
  as the machine-readable half of that brief** (G144 — the same vocabulary computed from the emitters, so
  unlike the prose it cannot drift, and each entry carries the tier that says whether the composer really
  produces it), `tools/seeds/*.plan.json` (incl. the funnel exemplars) as few-shot examples, and the G118
  verdict JSONL once it exists. **G146 blocks this, as correctness rather than tidiness**: the design
  brief describes Z as a production family and no sampler draws one, so an agent that reads the prose and
  asks for a Z authors a plan `plan_feasibility` then rejects — a loop that cannot converge, caused by a
  doc/code disagreement the catalog's tiering made visible. Either land G146 first or restrict
  `emit_family` to the catalog's in-mix tier. The same reasoning applies to the verdict JSONL: few-shot
  examples inherit whatever the sampler over-produces (measured at G144: the donut is 73 of 89 wool
  cards, U/H/L one apiece), so an unstratified corpus teaches an agent to write donuts. Scope
  is the **author agent** only; the **analyst agent** (mine verdicts/reject logs for rule + envelope
  refinements — read-only `verdicts_export`/`rejects_query`) is a small follow-on once the corpus has
  data, not before.

  **What the API audit settled about the head's shape (`B214`).** Four rulings, all of them about the
  boundary rather than about the tools:

  **The loop's stop condition is a rule id, and it now exists.** A submit→lint→fix loop only converges if
  every failure is a finding — until `B214` nine endpoints answered a raw .NET stack trace and one returned
  MariaDB's own error, which an agent can neither cite nor act on. The split the head reads is `RQ1` versus
  `RQ2`: `RQ1` means the document is wrong, so fix and resubmit; `RQ2` means the studio broke, so **stop**,
  because no amount of editing the plan will clear it. An agent that cannot tell those apart spends its whole
  budget re-authoring against a server defect.

  **No MCP parameter is ever a stringified document.** The HTTP surface has three body conventions —
  twenty-five endpoints take a document unwrapped, and the rest take a record, some of which carry the
  document as a *string* in a field (`{name, kind, params}`, `{name, themeJson}`). That last shape is the one
  MCP makes strictly worse: a `string` parameter carries no schema, so the client validates nothing and the
  agent double-encodes for no gain. **The head is where that encoding is absorbed** — a tool takes a typed
  `plan` object and stringifies it on the way to `:7894` — which is also why the wire itself does not need
  changing for this task's sake.

  **The head exposes the document model only.** Where the library keeps a second model of the same thing —
  `RoomStyleSaveRequest` has 28 fields against `HouseStyle`'s 46 and shares 11 names — the composed form
  stays behind the head. That is safe rather than merely tidy: a map binds its room styles as a **snapshot**
  rather than a library id, so an author agent never needs the library at all. The tool list above already
  reflects this by carrying no library tools; this is the reason it is right.

  **Tool schemas are generated from the request records**, never hand-written, for the reason
  `GET /api/shapes/catalog` is preferred over the prose brief beside it: a hand-written schema is prose in
  another notation and drifts from the endpoint the same way.

  **And the plan layer is the agent surface for a measurable reason.** A `plan.json` is 1 500–6 000 bytes and
  an `intent.json` about 4 000; a `layout.json` is **20 000–30 000**, five to twenty times the plan and almost
  all of it geometry — polygons, Bézier controls, per-vertex heights — that an agent has no business hand
  editing. So the sketch is exposed as **operations** (`from-plan`, `finish`) and never as a document
  parameter.

  **One caveat above was not this task's, and is now closed.** `plan_validate` having to flag empty
  `placements` "which leave the feel terms vacuously green" was `B140` — a document passing every check by
  being asked nothing — and it is fixed where it lived rather than worked around in the head: `/plan/evaluate`
  and `/plan/feasibility` both answer `PL1` to a plan with no pieces, in the same sentence `compile` gives.
  The head needs no empty-placements check of its own.

## Lower priority / parked

- [ ] **A8 — [Decision, parked] Should the layout generator be its own project?** `Pgm` holds two charters:
  the `map.xml` codec (48 files) and the layout generator (`Compose`/`Evaluate`/`Shapes`/`Derive`/`Plan`, 85
  files and 11.5k lines, touching no XML). The generator references only `Domain` and `Geom`, so
  `PgmStudio.Compose` would add **no dependency edge** — the split is free in graph terms, and it would make
  `Pgm`'s charter true again while making the generator's own dependencies enforceable (today it can reach the
  codec and nothing notices). Against it: a rename across every citation, and `PlanCompiler` — the plan →
  layout + intent seam — would sit on a project boundary rather than inside one. **The blocking question is
  whether `PlanCompiler` belongs to the generator or to authoring**, and that is answerable only when the
  generator next needs a structural change; doing it as a standalone refactor buys nothing today.
  See `docs/project-structure.md` §6.1.
