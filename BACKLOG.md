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

- [ ] **PG3 — An authored monument location is discarded, and the author has ruled that it should not be.**
  `WorldBuilder` fills every wool's `Monuments` from `monLoc` — the air cell the capturing team's spawn
  structure stamped — so a stated location is replaced unconditionally (`OB25` now says so). The ruling is in
  `docs/gameplay/approaches.md`: a monument is a **standalone stamp**, tied to the spawn for simplicity, and
  an author may put it anywhere **near that team's spawn** so long as it is **discoverable**. So: honour a
  stated location, derive only where none is stated, and have the spawn stamper leave no monument block for a
  wool whose monument stands elsewhere. Two gates follow, both on the built world beside `OB17`: a monument
  further from its capturing team's spawn than the author's distance allows, and one with no line onto it —
  a player carrying a wool must not search. `OB25` retires when the honouring lands; the distance is a number
  the author still owes, and the corpus is where to read what real maps do.

  *Reproduced on `probe-mootgate`: `POST /wools/red/monuments` at `(-8, 16, -48)` reads back from the map
  document and the intent, and `GET /xml` writes `<block id="red-blue-monument">-20,16,-80</block>` — inside
  blue's spawn room.*

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
  only the point tool re-seats. Pairs with `N08` (monument Y editing); the side-view's own clamp has
  landed (`FEATURES.md`).

- [~] **N12 — Configure has no destroyable phase.** Wools and Cores each have one and the objective phases
  are a group sharing one gate (`FEATURES.md`), so this is now the third phase slotting into machinery that
  already exists: add `destroyables` to `ConfigurePhases` + `IsObjective`, a `DestroyableAuthoring` slice
  beside `CoreAuthoring`, and the steps. A destroyable is the core's shape with a different structure — one
  region per defending team, no per-capturing-team monuments — but its knobs are style/materials/float
  rather than a casing. A DTM map authored in the plan tool can already be configured (the slice rides
  through untouched); what it cannot be is *seen* or edited there. Detection is a separate question and is
  `B58`: unlike a core, a destroyable has no signature of its own, so the phase should offer manual
  placement first and adopt candidates when that ranker lands.

- [ ] **TE3 — Retire the Edit tool.** The author's ruling: it is not being kept. The intent model authors a
  map now, and nobody has driven `/maps/{slug}/edit` — so its three unwired inspectors were never work, they
  were work on a surface with no future. `Features/Edit/` is 16 files and 2,155 lines behind one route.
  **`WorldCanvas` and `world-bridge` stay**: the Configure tool's build-layer, core-casing and core-objective
  steps mount the same canvas, so what goes is the tool, not the surface it draws on. Take the route out of
  the smoke sweep's list and the nav rail with it, and grep `docs/` for the tool's own name in the same
  commit — `capabilities.md` and `routing-and-ia.md` both describe it as a surface an author can open, and
  `docs/tools/edit.md` is the document that goes. `TE2` went with it: the tool's wool picker spelling the
  sixteen dyes a second way is a defect in a surface with no future, and `WoolColors` is already the one list
  every other reader takes.

## The sketch tool: shapes, islands, and the ground they become

The depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection highlight);
what is gathered here is the parked and dormant slices of the same surface.

#### Markers

- [ ] **TS75 — A destroy goal has no sketch presence, and no plan raster draws it.** A destroyable and a core
  carry **no rect in the plan** — `Anchor` is a bare point, and that is correct: neither has a footprint. So
  their sketch presence is a **movable point with a stated height**, not a rect, and the height is the
  interesting half — the one thing the plan cannot know before the relief runs. `GET /plans/{id}/png` has the
  same gap: `B128`'s empty-`piece` marker is how a landform carries an objective without a tier manufactured
  to hold it, and the one picture the plan layer offers cannot show it.

  *`tallow-mirefast`: the raster draws five pieces, both spawns and the legend, and nothing at `(0, −50)`
  where the wardstone stands.*

### Painting terrain

- [ ] **TS51 — Scoping the paint repaint, and the preview it would pay for.** A full board paint is ~2.0s and
  the column read ~2.9s / 2.6MB on a real agent board, tracking board *area* rather than shape count — a
  112-shape board and a 534-shape board of the same size cost the same. So the Blocks overlay refreshes on
  entering a phase and not per edit. A bbox or shape filter on `POST /map/{slug}/sketch/paint` would make a
  one-shape repaint proportional (tens of milliseconds for a shape covering a fortieth of the board), which is
  what the brush actually edits, and would also buy a scoped isometric preview of the selection in the
  inspector — the affordable half of a live preview of the built world.

- [ ] **S34 — Reuse a sketch paint's column classification across the edits of one drag.** `TerrainProfile`
  construction is what a paint now costs — ~60 ms of the ~164 ms a 40k-cell board takes (S33, `FEATURES.md`),
  and roughly 35 ms of that is its two `GridComponents.Label` passes: one flood fill for plateaus, a second for
  landmasses, each sorting its seeds and hashing a coordinate pair per neighbour edge. They re-run from
  scratch on every step of a drag, though only the moved shape's neighbourhood changed and the plateau
  components are already a refinement of the landmass ones (equal-top cells are 4-connected), so the second
  pass could be merged out of the first. Whether the rest is worth an incremental cache depends on a number
  nobody has: a typical board is ~93 ms end to end now, so this is the 200×200 case, not the common one.

- [ ] **WE46 — A building wears the ground it stands on.** A house has to read as something somebody built,
  from across the map, which means its walls are not in the tone family under its feet. Complain where a
  building's wall material and the terrain ringing its footprint resolve to one `TerrainPalette` family, and
  refuse an ore block as a building material outright. `docs/world-export/decoration.md`.

  *9 of 50 buildings on the spec boards are walled in the ground's own family — `opus5-siderite-bowl` puts
  three grey-stone houses on grey stone, `sonnet-gantry` two brick houses on brick.*

- [ ] **WE47 — A board wears a theme per piece.** A theme is a *place* and a board has two or three; giving
  every piece of the plan its own is the plan leaking into the paint. Complain where a layout's `themes`
  registry carries more than three, and where a flight of steps compiled from a plan does not share one theme
  with itself. `docs/tools/sketch.md`.

  *51 boards carry a registry: 16 hold three, but 11 hold five, 7 hold six, and five hold between sixteen and
  twenty-four — `opus5-interchange` has 24.*

- [ ] **WE48 — A pattern's brush is smaller than the thing it dresses.** A field whose features are smaller
  than what they are laid over reads as static however good its palette is. Complain under a floor on
  `CellMaterial.CellSize` and the field patterns' `Scale`. **The floor is 2 (author)** — a guard against a
  brush finer than the blocks it paints rather than a style rule, so it flags a pathological value and leaves
  every board on the shelf alone. `docs/world-export/terrain-painting.md`.

  *Medians over the committed themes: `cellSize` 6 for a cell pattern (down to 2), `scale` 8 for a noise field
  (down to 4), `cellSize` 6 for a voronoi. Those are the numbers that produced the boards under review.*

- [~] **WE41 — A pattern is a family shown off rather than a ground.** *Parked on a ruling: no candidate
  predicate reproduces the author's judgement, and nothing is built until one is chosen.* The predicate the
  author has since named is not colour distance but **how much of a family a pattern takes**: two blocks is a
  texture, three a mottle, five a family on display. Complain where a pattern's entry list carries more than
  two members of one `TerrainPalette` family. Beside it, two placements the author states absolutely: a
  **voronoi** belongs in the **fill** and is made of stone — never the surface — and a **field** pattern's two
  blocks must be near shades of one ground, so it carries a texture and never a border between two grounds.
  `docs/world-export/terrain-painting.md`.

  *Measured over the 51 boards in `pgm-studio-mapgen/specs` that carry a theme registry: of **277 patterns**,
  85% carry three entries or more — 51 carry five, 8 carry six or seven — and only 15% carry two. Of **50
  voronois**, 44 are on the surface and none is in the fill. The earlier candidates (one family per pattern:
  157/201; a neutral family mixed with a warm one: 54) are superseded.*

### Relief

- [ ] **WE76 — `step` is keyed once per group, so a bench and a road cannot share a landmass.** A relief's
  `step` is the block quantum the whole solved surface snaps to, and the schema has no per-mark or per-shape
  quantum beside it — so ground that wants worked terraces and ground that wants a walkable ramp are one
  setting on a group and a group is a landmass. Give a mark its own `step`, falling back to the group's, in
  `SketchReliefJson`'s mark model and in `ReliefSolver`. `docs/world-export/relief.md` §6 and
  `docs/tools/sketch.md` § the relief document both state the group's step as the only one and change with it.

  *`opus5-scarrow-delph`, one board, the same seventeen marks: `step 1` → 559 scrambles / 603 barriers ·
  `step 2` → 1881 / 482, and `RL2` goes silent while no row is crossable on foot in either direction ·
  `step 3` → 12 / 1785 · `step 6` → 2 / 1086 and 31 cliffs. What shipped is four nested `area` rings written
  outward-in, which terrace themselves at `step 1` and leave the roads at a one-block quantum — a workaround
  that is better than the knob.*

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

  **Settled (author): the key is the layer plus the island.** That is what an author means — "the ground of
  the ground storey" — and it is the reading that lets a relief be solved on a storey *under* the board, which
  is the case a bare island id cannot express at all. The pairing becomes structural rather than a string
  equality two documents have to keep agreeing on. Amend the model in `docs/world-export/relief.md` first,
  then the key, then a read-forward for a stored relief keyed by island alone — its layer is the one that
  carries the island, which is recoverable.

- [ ] **S42 — Relief: the carve and the graded road fold too.** The solve folds, and so now does the stair cut
  (`FEATURES.md`) — the first later pass to land, and the one that showed the rule is real rather than
  theoretical. The other two are still open: a carve and a graded road each decide things by **walking** the
  map, and a walk has a direction the half-turn does not preserve, so each folds again or it undoes what the
  solve established (`world-export/relief.md` §8). Measured on the designed map, a carve that did not re-fold left
  the two halves **9 blocks** apart. Belongs with S46, which lands both passes; the fold itself needs no new
  machinery — `ReliefSolver.FoldBlocks` is the shape of it.

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

### Water

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

- [ ] **WE73 — Nothing stands in the water.** A `pool` or `channel` claims every column of its bed
  (`ClaimKind.Water`) and a quay wall drawn as a `keepClear` path keeps its band, so a boulder stated in the
  race is refused before it is seated — `DR-CLAIM` by the channel at `(−110, 55)`, `DR-KEEP` by the wall at
  `(−70, 52)` on `maps/fable-millrace-revamp` — and a rock's cells above the bed would meet water rather
  than air in `Decorator.Fan` if it were. The author brushed about twenty rocks into Millrace's bed and the
  studio plants none. Let a boulder seat on a bed the water claims and write through the water, keeping the
  claim for everything else; `docs/world-export/decoration.md` §5 and §7.

### Path

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

### Biomes

- [ ] **WE52 — A drawn patch takes its own biome field.** The map states one field and every column answers
  to it. What an author wants beside that is a shape drawn in the Dressing phase — the way an area of cover is
  drawn — carrying a field of its own, so a corner of the board reads as desert against a map that is otherwise
  forest and river. The map's field becomes the default the patches sit on. `BiomeScope.Paint` already walks
  every column and would resolve patch-first, map-second; what is missing is a drawn area on the dressing
  document and the pass that reads it. Per **shape** selection is deliberately not wanted (author) — a patch is
  drawn for this, not inherited from the geometry.

- [ ] **WE53 — A painted biome is invisible in the studio.** Every static render multiplies grass, leaves,
  vines and water by a fixed temperate tint, because a render has no biome to sample
  (`Minecraft/Palette/BlockPaletteData.cs`). So a board's biome field shows in game and nowhere in the studio —
  an author paints it blind. The fix is a biome-aware tint on the paint overlay and the iso preview: the tint
  is a per-biome multiplier over the same texture mean the palette already holds. Evidence:
  `maps/biome-test-pattern` in the mapgen repo carries three biomes over 16,384 columns and looks identical in
  every studio picture. Swampland wants its own path even then: vanilla paints it two-tone from a noise of its
  own (see `terrain-painting.md` §5b), so a single multiplier cannot reproduce it.

- [ ] **WE54 — A biome field has no surface to author it on.** `GET`/`PUT`/`DELETE
  /map/{slug}/sketch/biome` states one and the published schema names `solid`, `cell` and `noise`
  (`FEATURES.md`); nothing in the browser does. It belongs in the Dressing phase (author), which today
  places props — a prop is a point or a point-list with a radius, and a field is neither — so the phase gains
  its first map-wide control rather than a seventh prop kind. Wants the biome ids as a named list, which
  `Minecraft/Palette/Biome.cs` already holds.

### Layers

`WE24` gave every placement an optional layer and two resolvers that agree about where a floor is. The export
has read it since the stack landed; nothing in the browser writes it, so a stacked board can only be dressed
and populated on its top surface. The frame is settled — the storey being drawn on is canvas chrome and a
placement takes it — so what is left is each surface reading and writing the layer it is handed.

- [ ] **B263 — A prop's layer can be neither seen nor overridden, and the canvas draws every prop alike.**
  `DressingDoc.add` stamps the storey being drawn on, so a prop placed on an upper layer records it and
  `DressingContext.GroundFor` resolves it (declining `DR-LAYER` where that layer has no ground). What is
  left is the two reads: `SketchDressingInspector` has no field for `PlacedProp.Layer`, so a prop cannot be
  moved to another storey without editing the layout by hand; and `dressing-render.js` draws a
  gallery-floor prop exactly like a roof one, so a stacked board's dressing reads as one plane.

- [ ] **B264 — No intent placement takes the active layer either.** The same optional `Layer` is on all six —
  monument, spawn, wool, iron cube, destroyable, core — and `MapIntent` carries it at six sites, set by no
  Configure step; `SpawnStep` states outright that its canvas is base-layer only. So on a stacked board an
  objective stands on a lower floor only by writing the intent by hand. Under `TS45` a placement takes the
  active layer, and what is left is the six write paths and the field on each inspector.

- [ ] **WE78 — A plain layer's paint runs to the bedrock course, and eats the layer under it in silence.**
  `TerrainPainter.Paint` gives a `prop` layer its own floor from `BuiltTerrain.FloorByLayer` (`WE56`), so a
  made thing is painted over its own span. A **plain** stacked layer is not: its bands run from the bedrock
  course whatever its `base_y`, and the only thing keeping a pass off the layer below is the stone-only
  invariant — it writes over `(1, 0)` and nothing else. So a viaduct over a street repaints the street's
  whole column wherever the ground theme fills in plain stone, and the cure is a value in a *different*
  theme. Nothing says so: the store answers 200, the export gate answers OPEN, and `themes/census` counts
  the surface and is right. Either bound a plain layer's bands to its own span, or complain where a layer
  stands over one whose resolved `fill` is `(1, 0)` — a document-level read, since the themes and the stack
  are both in the layout. `docs/world-export/terrain-painting.md` states the bedrock-course rule and changes
  with it.

  *`opus5-tiefkreuz` build 1, `GET …/column?at=0,66`: `y 42..39 Iron Block` the viaduct rail, `y 29..27
  Stone Bricks` the street lid, and `y 26..1 Iron Block` — twenty-six courses of city painted as rail. The
  isometric drew a grey board and only `column` found it.*

- [ ] **WE79 — A `stroke` ignores the layer every other prop kind honours.** `PlacedProp.Layer` is on the
  abstract prop and `DR-LAYER` refuses one naming a layer with no ground, and both reach a `stroke` in the
  schema; neither reaches its seating, which takes the column's top surface. So a street laid under a
  viaduct paves the viaduct's ballast. Route the stroke's seating through `DressingContext.GroundFor` the
  way the other five kinds are. `docs/world-export/decoration.md` § what a stroke rests on changes with it.

  *`opus5-tiefkreuz`: one avenue from the head house to the station had to be drawn as two strokes stopping
  at the viaduct's faces, `(0,104)`–`(0,78)` and `(0,60)`–`(0,42)`. `opus5-interchange` measured the same
  thing on lane markings — `"layer": "under"` came back at y25.*

- [ ] **B144 — Settle how height and paint resolve an overlap, and warn where they disagree.** Height takes the
  **taller** add-shape (`MergeCell`); paint takes the **smallest-area** shape (`ShapeScopeOwners`, "the most
  specific scope"). The documented way to give a tier an organic edge is to let the tier below run *under* it —
  and where that lower tier is the smaller shape, it keeps its own paint over ground the upper tier owns. No
  field scopes paint to the visible surface rather than to a shape, and nothing warns.

  *`opus5-run2` §5 #2 · re-probed on `marlstone-steps` against the committed region files: `(0, 58)` is
  sandstone at y21 where `(0, 70)` — the same shelf at the same height — is quartz.*

  **The stacked case is already settled** (`TS23`, `FEATURES.md`): across layers there is no contest, because
  each surface shows its own paint.

  **The nested-tier case is settled the same way: paint follows the shape that forms the surface.** Among the
  shapes covering a column, only those reaching the visible top may own its paint; among *those*, the smallest
  area still wins. That keeps patch-scoping exactly as it is — two shapes at one height are a theme scoped to
  a patch, and the smaller one is the scope — and stops a shape running *under* another from painting a
  surface it does not form, which is the whole of the defect. It is `TS23`'s rule read within a layer rather
  than across layers: each surface shows its own. `ShapeScopeOwners` gains the height test `MergeCell` already
  makes; where two shapes tie on height nothing changes.

### A made thing is a third kind, and it is drawn out of layers

- [ ] **WE77 — `WX11` measures a structure's plinth from the highest terrain its footprint touches, and the
  stamper seats it on the lowest.** `MapExportComposer.CheckStructureSites` takes
  `floor = cells.Select(surface).Max()` and reports `floor - beside` as the face a foundation fills; a house
  prop seats on the **lowest** column of its own footprint one course down and carves the terrain standing
  over that floor away (`docs/world-export/structures.md` §6). So a footprint that clips one tall authored
  shape reports a plinth the world does not build. Read the floor the way the stamper does, or off the
  provenance the stamp recorded. `docs/refusals.md`'s `WX11` sentence states the maximum reading and changes
  with it.

  *`opus5-mootgate` build 3: `WX11 house h-south-d 0 stands 7 blocks above the cell beside it at (11, 33)`,
  where `column?at=11,33` reads Grass Block at y14 and `column?at=11,34` reads the house's own plate at y14 —
  a drop of 0, and no bedrock face in the world. The house's footprint touches a stair-flight polygon whose
  top is y21–22, which is exactly 7 above y14. Moving it two blocks clear of the flight silenced the rule.*


**The author's ruling.** The shape tool draws **terrain** — shapes and a relief. The dressing pass places
**props** — houses, trees, boulders. A sculpture is neither: it is a *made thing*, its own kind beside those
two, and it happens to be written in the layer model because that is what can hold it.
`pgm-studio-mapgen/SCULPTING-WITH-LAYERS.md` is the measurement behind every entry here — nine forms in sketch
shapes, eight of them one layer, and nine compiled solids on two exported boards.

**The house is the worked precedent, and its contract is the one to copy** (`docs/world-export/decoration.md`
§8, `structures.md` §6). A house prop is a drawn rectangle handed to a stamper that neither knows nor cares
where the footprint came from; it **seats on the lowest column of its own footprint, one course down**, carves
the terrain standing over that floor out of every footprint column while the ground outside keeps its height,
**claims what it stamps grown one block outward**, and refuses rather than half-lands — `DR-SITE` on the first
column with no ground under it, `DR-SLOPE` where the rise across the footprint reaches the building's own
height. That is exactly what a made thing needs, and none of it has to be invented.

- [~] **TS64 — A made thing is one row in the strip, and one thing to drag.** The layer's `prop` field is
  written and the rasterizer seats by it; the surface is not. `opus5-automaton` carries 31 layers, 24 of them
  `colossus-L0…sentinel-L7`, so `SketchLayerStrip` shows 31 tabs and `GET …/render/topdown?layer=` prints all
  31 in its `RQ4` refusal. Render the strip, the layer list and the topdown filter **by `prop`** — one row per
  made thing with its layers folded under it. **A prop is also what moves**: dragging one has to take every
  layer of it together, since a made thing standing half a block from where it was put is not a made thing.

- [ ] **TS79 — `SK18` reads a column and not a course, so a frame over a goal is "inside" it.** The
  provenance a made thing and a stamped structure are compared on carries one claim per column and no Y, so a
  beacon frame at y72–80 over a monument at y37–40 raises one complaint per layer of the frame — fourteen on
  `maps/fable-millrace-revamp`, all wrong about a thing forty courses up. Compare the made thing's own span
  (`ColumnSegment` floor and top, which the rasterizer has) against the structure's stamped courses, and name
  only the columns where the two share a course. `SketchRasterizer` and the check that raises `SK18`;
  `docs/tools/sketch.md` Refusals and `docs/refusals.md`.

  *`beacon-front-L2` … `L6` against `destroyable-1`, first at `(−90, 18)`; the frame's lowest block is y72.*

- [ ] **WE74 — A made thing fanned across the axis cannot change colour with the side it lands on.** The
  author's statue is red clay and wool on one island and blue on the other; a `teamTint` material answers
  the team only where the column resolves one, and a made thing on a neutral island resolves none, so the
  restated board states the statue twice, unmirrored, with the blues swapped by hand
  (`specs/fable-millrace-revamp/build.py`). Let a made-thing layer state the team its images take — the
  image index is what the fan already knows — so one authored statue fans into a red one and a blue one.
  `docs/tools/sketch.md` § A made thing; `docs/world-export/terrain-painting.md` § 5.

- [ ] **TS63 — A form library: the round structures a layer already draws.** `ring_wall`, `ellipse_wall`,
  `dome`, `spire`, `ziggurat`, `arch`, `colonnade`, `tapered_tower`, `bowl`, `crenellated_wall`, `drum_tower`,
  and a `gatehouse` composing five of them — a footprint and a few numbers each, emitting circles and polygons
  with a floor and a height, so what lands stays draggable in Draw. Costs measured on
  `pgm-studio-mapgen/sculpture/forms`: a hollow dome of radius 13 is 13 circles on one layer, a hollow ellipse
  is 2 polygons, a thirty-course tapered tower is 6, the gatehouse 8 layers and 74 shapes. **Which forms earn
  a place is the author's**: the arch, the ziggurat, the ellipse wall, the tapered tower and the domed roof
  are wanted; the amphitheatre and the colonnade are not, as drawn. The two mechanisms every round form is
  built out of — an annulus as one even-odd polygon, and an override add laying a floor inside a wall — are
  written up in `docs/tools/sketch.md`, so a library emits what an author can already draw by hand.

### Shapes

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

- [ ] **TS83 — Eight props on four mapgen boards stand on coast the old bend invented.** `TS30` moved the
  bend into the studio, where a point may only be pulled **inward**; the driver's own copy read the shoelace
  sign backwards and pulled every point outward by the same amount, so each bent board grew past the plan's
  own footprint. Re-driven against the correct coast, four of the five bend boards decline exactly two props,
  each standing on the growth. The boards build, export and play; what is wanted is each prop moved inside
  the drawn coast, which `06-claims.txt` says where there is room for. The worlds under `maps/` were built
  against the old coast and are unaffected until re-driven. The work is in `pgm-studio-mapgen`, and it is
  filed here because the bend that moved them is the studio's.

  *`opus5-alderfen` placed 106, declined `steading-e`/`steading-w` · `opus5-blockrealm` 38, `tree-4`/`tree-5`
  · `opus5-quiverstone` 44, `birch-1`/`dwelling-e` · `fable-mossgill` 38, `tree-10`/`tree-3` ·
  `opus5-lodestar` 30, none. On `opus5-alderfen`'s rings: `garth-14` is 9750 compiled, 11033 under the old
  bend, 8467 under the studio's.*

- [ ] **S59 — Per-vertex height is the headline feature and is found by accident.** The path is: select a
  polygon, read the one conditional sentence in the inspector, click a vertex on the canvas without moving it,
  then type into a field that appears in the panel. On the canvas a vertex handle looks exactly like a drag
  handle and its height is a bare text label, so nothing says a click-without-drag does something a drag does
  not. Make the height labels read as interactive (a pill or a hover state), and ideally let the label itself
  be edited or scrolled in place rather than round-tripping to the inspector. The shift-click 2–3 vertices
  slope-fit has the same problem and the same fix. The 3-D preview is where a height edit is actually legible
  and it now draws the built world (`FEATURES.md`), but it is a modal swap rather than a companion view, so it
  confirms an edit after the fact rather than while it is being made.

- [ ] **TS74 — `SK13` cannot tell a fill from a floor, so a subtract cannot mark a room.** A subtract is
  what an author reaches for to say *this column is void*, and flooring and roofing that void is the use it
  is reached for — but `SK13` refuses every add over a subtracted cell alike, at any height, on any layer.
  Give it the cut it is missing: an add whose **top is at or below the subtract's stated `floor`** is the
  ground under the void and says nothing; one whose span crosses that floor is the fill the rule is for.
  `SketchRasterizer.AddsOverSubtracts` already carries both floors and decides `survives` from them, so the
  test is a comparison it can make where it stands. `docs/tools/sketch.md` and `docs/refusals.md` carry the
  rule's two halves and both change with it.

  *Measured on the running studio: `rock` a mass `[0..30)` with a subtract over a 12×12, `floor` a plain add
  `[0..4)` and `ceil` a plain add `[11..30)`, each on its own layer, builds the column `y0..3` solid, **void
  `y4..10` — seven courses** — `y11..29` solid. A room, with rock under it. It previews, and `finish`
  answers 422 `SK13` twice.*

## The library: a browse page, an editor page, and the rail between them

The shape has landed: the rail carries the six kinds, `/library` chooses between them, and an entry opens a
page laid out as an outline, its fields and a preview companion. What is left is what an author can *say* on
it, what they can *see* while saying it, and what is on the shelf to say it about.

**The dressing surface is being cleaned up, and the line is what an author *draws*.** A water channel and a
path are traced on the canvas, so pre-authoring one is authoring a shape without its place and they keep their
controls in the tool (author). A tree and a boulder are a click, so what is placed is a *recipe* and the recipe
is library material — picked in the inspector, with a way through to the library to author a new one. A
building already works this way. The inspector then holds a full editor for the two drawn kinds, a picker and a
forward for the two clicked ones, and the per-placement handful — seed, position, door edge — for all of them.

### What the author sees while authoring

- [ ] **TL14 — Nothing says what kind of block a house-style field takes.** `HS1` refuses a block named for a
  geometric role in a field that means another — a stair id under `doorHead.block`, a slab under
  `roofSlab` — and until this was corrected its fix text sent an author to `GET /api/house-parts`, which has
  never been served. What exists instead: `GET /api/terrain/blocks` is the **terrain** palette (110 rows, no
  stairs at all), `GET /api/room-styles/doors` answers door kinds, and the rule's own `means` names the kind
  per field in prose. An author's way through is to read a shipped preset's `/room-styles/{id}/json` and copy.
  Wants one catalogue answering, per style field, the kind it accepts and the ids of that kind —
  `Minecraft/Palette/BlockFamilies.cs` already holds the families the geometry reads. `docs/tools/library.md`
  § what a style states, and the `HS1` remark, both change with it.


**The card carries the section, and that is settled** (author): an author knows a house by its name, and the
one that wants looking at is a click away from the real thing, turnable, cut to the row they have open
(`FEATURES.md`). What is left here is how long the page takes to say any of it.

- [ ] **TL13 — The house editor waits ten seconds on a list it barely uses.** `HouseEditor.OnInitializedAsync`
  makes six sequential fetches before `OnParametersSetAsync` may run, so nothing renders until the last one
  lands — and one of them is `GET /api/styles`, which answers **203 rows each carrying its preview SVG**
  (843 ms on localhost) to fill a select that shows a name and a kind. The editor shows "Reading the house…"
  for about ten seconds on a cold open. Either the six run together, or the style list answers a summary
  without the pictures for callers that only pick by name; `StyleSelect` groups by kind and renders no swatch,
  so it needs neither `preview` nor `params`.

  *measured 2026-09-02 on the workshop preset: doors, blocks and styles fire, then roofs, storeys and porches,
  and `GET /room-styles/{id}` only after all six.*

### What is on the shelf

- [ ] **B47 — A theme copied onto a board loses where it came from.** Copying in matches by name, so the same
  library theme copied twice replaces its own snapshot rather than growing a `meadow-2` — but a board theme
  renamed on either side is two rows with nothing saying they are one theme, and nothing can say whether a
  snapshot is behind the row it came from. Wants a note on the copied theme recording the library row, which
  slots into `B44`'s snapshot record rather than duplicating it.

## World import: reading a map the studio did not build

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

## The plan model: pieces, and the edges between them

`PieceInterfaces` turned every seam between two plan pieces into a read — its height delta, its typed wall,
each side's frontline share, the straits between bridged islands — and the lint table quantifies over it
(`SP8`/`SP9`/`ST8`/`ST9`/`BZ11`/`FR8`/`CT12`). What is left is one number nobody has stated, one read the
seams support and nothing asks for, the word the model uses for a seam — and the rules that are about a
piece's own geometry rather than about what is stamped on it: what a spawn's ray faces, what a wall seals,
and what a `subtract` takes away.

- [ ] **TN12 — Cycling a spawn's facing leaves the room it seeded pointing the old way.** The editor asks
  `POST /api/plan/room` once, when the piece is drawn, and writes the answer's `at` and `footprint` onto a new
  placement (`plan-bridge.js:128` `seedRoom`). Cycling the facing afterwards — a re-click on a selected spawn
  (`plan-canvas.js:1053`) or the rail's Cycle facing button (`plan-bridge.js:441` `cycleFacing`) — rewrites
  `facing` alone, so the stored footprint keeps the door apron on the side the room no longer opens on. The
  endpoint reads the stated facing now, so re-asking it answers correctly; what is missing is the ask. Re-seed
  on a facing change, leaving a footprint the author has since moved alone.

  *the door gap is `RoomFrames.DefaultFootprint`'s inset in front of the door, so a `front` seed on a `back`
  spawn puts the apron at the room's `−z` edge and the door at its `+z` one.*

- [ ] **B213 — Stop fusing the two pieces a wall sits between, and lock the seam in the sketch.** A wall's
  rect is fixed at compile from the interface its two plan pieces share, and nothing afterwards holds that
  seam: resize or re-bow either shape and the wall stays where it was, spanning less than the lane it was
  drawn across, with no refusal and no warning. A wall slows an attack and gives defenders a base to build on
  **without players tunnelling around it** (author) — both halves need the bedrock line cutting its lane in
  full, so a shortened wall is a gameplay failure rather than a cosmetic one.

  **The shape (author's call).** `PlanCompiler` does not fuse an abutting pair at equal height when a wall
  sits on the seam between them — the fusion is what destroys it. The wall then has an interface in the
  sketch model too, and the four vertices bounding it are **locked together**: they move as one or not at
  all, so an organic pass can bow the coast either side and the wall's own span survives it.

  *`opus5-coldharbour-v2-authoring.md` §6: an organic pass bowed a wool lane's coasts past both ends of its
  wall, players could walk round it, every call answered 200, and the only symptom was traversability moving
  from 2 isolated markers to **0** — the direction that reads as an improvement.*

## Distance, and the walk every measure is taken with

`Geom.Walk` is the traversal now — eight-connected and octile, charging a climb in the blocks a player places,
counting a fall, slowing through water, narrowed per team where an `enter` rule bars one — and it runs over a
set that reads a surface as somewhere a player can stand rather than as any column holding a block. 

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

## User Experience

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

- [ ] **C57 — The plan canvas enters a box without showing it has.** Both authoring canvases hold the same
  two-level model, and the sketch draws the island it has entered as a dashed outline under the selection —
  the frame clicks are resolving inside. `PlanCanvas` holds `#scopeBoxId` and honours it in `#selectDown`,
  but nothing on screen says a box is entered, so the same click means two different things with no way to
  tell which. The box's own rect is already drawn by the overlay pass; it wants the entered one drawn in the
  accent, dashed, the way `SketchCanvas.#paintSelectionHighlight` draws its scope. Same file, same pass as
  the selection box it sits under.

## Refactoring and cleanup

- [ ] **RP65 — The layout DTO still says `JsonElement` where its own routes say `DressingDoc` and
  `BiomeField`.** `SketchLayout.Dressing` and `.Biome` are `JsonElement?` because their types live in
  `Minecraft` and `SketchLayout` lives in `Pgm`, which are siblings over `Domain` + `Geom` — the fields' own
  docstrings say so (`SketchLayout.cs:41-50`). The part routes publish both models and the store-time gate
  reads both (`FEATURES.md`), so what is left is the whole-layout route: `GET`/`PUT /map/{slug}/sketch` names
  the two fields with no shape under them, and a reader who starts from the document rather than from the
  parts finds two holes in it. Move the dressing and material model down to a project both reach, or publish
  the two schemas from `Minecraft` and reference them from the layout.

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

- [ ] **C62 — Three CSS rules describe a component nothing renders.** `components.css:989–1004` carries an
  `AUTHOR CHIP (avatar + resolved player name — used in map detail view)` block — `.map-author-chip`,
  `.map-author-avatar`, `.map-author-name` — and no markup in the repo uses any of the three: `grep -r
  "map-author" src/ tests/` hits only that CSS. The comment names a surface (the map detail view) that
  either never shipped or lost the rows since, so a reader looking for the author chip finds a definition
  and no component. Delete the block, or build the chip the comment names and say which page carries it.
  `src/PgmStudio.Client/wwwroot/css/studio/components.css:989`. The class list in
  `docs/client/ui-conventions.md` names no chip either, so nothing else has to move with it.

- [ ] **WE70 — Six callers guess whether a shell is bound, and the export is the only one that knows.**
  `WoolFrame`/`SpawnRoom` take `shellBound`, which sizes the default footprint (`WX1`) and the wall inset;
  `WorldBuilder:148,179` pass the map's real binding (`RoomStyleScope.StylesOf`) and six others hardcode
  `true` — `PlanStructurePreview:60,74`, `MapExportComposer:455,460`, `DressingScope:216,218` — while
  `PlanValidator:404` passes `false`. Where a placement states no `footprint`, the guess changes the
  rectangle, so the previewed box, the `map.xml` region, the frontage and the stamped world can each name a
  different room on one map. Thread the layout's bound pair to the three `Export`/`Api` callers, which all
  reach the layout already; the validator's `false` is correct and stays.

  *Swept over spawn pieces 6×6–24×24 facing −z with no stated footprint, 94 sizes resolve a different
  default: a 20×14 piece frames `(1,4)..(19,13)` bound and `(1,5)..(19,13)` open, and a 6×12 piece frames
  `(1,1)..(5,11)` against `(1,5)..(5,11)`.*

- [ ] **C51 — Twenty-eight selects outside the authoring surface are still hand-rolled.** `Select` and
  `StyleSelect` serve the library and the terrain components (`B259`, `FEATURES.md`); the plan tool carries 7
  raw `<select>`, the sketch tool 7, Edit 6, Configure 5 and the design showcase 1. Each is the same
  options-and-a-value question written as markup, so a group, a per-row note or a disabled row has to be
  re-invented wherever one is wanted. Adopt the control at those sites; `docs/client/ui-conventions.md`'s
  *Forms* tier already names it.

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
  
  The suite half is one missing wait. `map-layers.mjs:75` waits for `.map-canvas-svg`, the element that exists
  too early; at `:122`, before the *second* compile, it waits 1500 ms with a comment saying exactly why.
  Fixing the tool makes both unnecessary.

  *diagnosed 2026-08-16 by intercepting the editor's own `POST /api/plan/compile` under both navigations:
  same database, `goto` → **200**, row-link → **422**. `./tools/e2e.sh all` gives `map-layers` 13/14 with
  `smoke` 39/39 in the same run; `./tools/e2e.sh map-layers` alone is 18/18. `B229` was this filed a second
  time — its hypothesis, that an earlier spec breaks the stored plan, is disproved by the same test.*

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

## The remainder: work no concept above has claimed

- [ ] **WE13 — The catalogue map is not a map, and may not be wanted at all.** `tools/library-map.cs` emits a
  grid of 37 unconnected plots and `GET /map/{slug}/export` refuses it 409 `EX1` — *3 spawn/objective point(s)
  are not reachable from the rest*. **The ruling (author): a catalogue is not a map**, so `EX1` is asking it a
  question it was never built to answer, and moving its wools onto one plot would make it a worse catalogue to
  fix a verdict that does not apply.

  **What is open is whether to keep the catalogue at all**, and the author's lean is not to: it has not been
  opened in a while, and the library's own 3-D preview now shows a piece far better than walking to its plot
  does. So the exemption is not worth building until that is settled — an exemption for a tool nobody opens is
  the more expensive of the two answers. Retiring it takes `tools/library-map.cs` out of the seven scripts in
  `tools/` and its paragraph out of `docs/tools/library.md`.

- [ ] **G262 — The seed corpus states iron the placement rules no longer seat.** Measured across
  `tools/seeds`: 12 of 14 spawn-room cubes resolve unplaceable, on five seeds, because a cube and a walled
  room need `6 + 2 + 3` = 11 blocks on one axis and those spawn pieces are 10×10, 15×15 and 20×10. Nothing is
  broken by it — an unplaceable marker stamps nothing and is flagged `WX9` — so this is a data refresh, not a
  defect: re-author each spawn piece so it either has the depth for a yard or states a footprint small enough
  to open one, then re-record whatever `docs/generator/seed-stats.md` measures off them.

- [ ] **A8 Should the layout generator be its own project?** `Pgm` holds two charters:
  the `map.xml` codec (48 files) and the layout generator (`Compose`/`Evaluate`/`Shapes`/`Derive`/`Plan`, 85
  files and 11.5k lines, touching no XML). The generator references only `Domain` and `Geom`, so
  `PgmStudio.Compose` would add **no dependency edge** — the split is free in graph terms, and it would make
  `Pgm`'s charter true again while making the generator's own dependencies enforceable (today it can reach the
  codec and nothing notices). Against it: a rename across every citation, and `PlanCompiler` — the plan →
  layout + intent seam — would sit on a project boundary rather than inside one. **Deferred (author): not
  yet**, because which side `PlanCompiler` belongs to is not yet known — and it is the one thing the split
  turns on. The answer arrives when the generator next needs a structural change and the seam has to be
  argued about anyway; doing the rename as a standalone refactor before then buys nothing.
  See `docs/project-structure.md` §6.1.
