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
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit.

**A monument sits inside its capturing team's spawn, and that settles which map each entry is about**
(author; `docs/design-decisions.md`). The studio's own maps derive it — `WorldBuilder` fills the location
from the air cell the spawn structure stamped, `OB25` says so, and `ConfigureTool.LoadOriginAsync` drops the
Monuments step for a sketch-origin map because there is nothing there to author. So what is left in this
group is the **imported** map: the one the scan reads a monument off, or fails to, and the colour an author
never gets to choose on either kind.



- [ ] **TC6 — Per-side focus: framing one team's quadrant while its unit is being worked.** *Parked on a
  ruling: what the framing should be.* The want was filed against `FocusSection`, a mockup on the `/concepts`
  page — both were deleted on 2026-07-22 (`7fac0f69`, superseded by Configure), so there is no design left to
  wire up, only the question it stood for. The canvas half that exists is `WorldCanvas.FitIsland` →
  `world-canvas.js:330 fitIsland(id, fillFrac)`, which frames one **island**; a team's quadrant on a
  two-island board is not an island, and on a four-team board the two do not coincide either. **The
  question:** is the frame an island, the team's spawn plus its objectives, the symmetry quadrant the orbit
  cuts, or the author dragging it themselves — and does it follow the selected team, or is it a control.

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

- [ ] **WE28 — A relief is keyed by group id, and a stacked board's storeys share one namespace.**
  `SketchReliefJson` rides top-level on the layout keyed by group, and a group id is unique across the stack
  only by the author keeping it so: two storeys of one board are the same footprint one layer up, told apart
  by a string nothing in the geometry distinguishes. A collision is a complaint (`SK12`) rather than a
  refusal, and the two halves of the studio then disagree about what it means — the rasterizer solves the
  stated relief onto **every** group answering to the id, each over its own footprint, while
  `POST …/sketch/relief/read` and the contour overlay report the first alone.

  **Settled (author): the key is the layer plus the group.** The pairing becomes structural rather than a
  string equality two documents have to keep agreeing on, and it addresses a relief the way
  `PUT …/sketch/layers/{layerId}/groups/{groupId}` already addresses the group it belongs to. Amend the model
  in `docs/world-export/relief.md` first, then the key and the four `/sketch/relief/{groupId}` routes, then a
  read-forward for a relief keyed by group alone — its layer is the one that carries that group. `SK12`
  narrows to within a layer in the same commit.

  *A stack keyed `team` on both `under` (`base_y` 0) and `ground` (`base_y` 40), one point mark at h 24:
  both storeys build the solved surface — `under` y1..23, `ground` y40..63 — and the read answers one group.
  Every key in the 24 stored relief-bearing layouts and the 76 in `pgm-studio-mapgen` resolves to exactly one
  layer, so the read-forward is unambiguous.*

- [ ] **S42 — Relief: the carve and the graded road fold too.** The solve folds, and so now does the stair cut
  (`FEATURES.md`) — the first later pass to land, and the one that showed the rule is real rather than
  theoretical. The other two are still open: a carve and a graded road each decide things by **walking** the
  map, and a walk has a direction the half-turn does not preserve, so each folds again or it undoes what the
  solve established (`world-export/relief.md` §8). Measured on the designed map, a carve that did not re-fold left
  the two halves **9 blocks** apart. Belongs with S46, which lands both passes; the fold itself needs no new
  machinery — `ReliefSolver.FoldBlocks` is the shape of it.

- [ ] **WE98 — A water prop's band stops at its radius, not at the bank.** A `water` prop fills its own
  stated band, so where the channel is narrower than the ground it runs through the top course faces air on
  both sides and the river reads as a trench with a stripe in it. The bank is a fact about the terrain and the
  radius is a fact about the document, and nothing reconciles them. Fill to the contour instead: from the
  prop's centreline outward to the first column whose surface stands above the stated level, capped by the
  radius, so a wide reach fills wide and a narrow one narrows. `WaterProp` in `Minecraft/Dressing/Decorator.cs`;
  `docs/world-export/decoration.md` § water.

  *`opus5-corbel-scar`, section at x=0: two courses of air between the water's top course and both banks.
  `DR-DRY` already counts them — 126 open columns — so the detection exists and the fill does not.*

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
  course whatever its `base_y`, and only the stone-only invariant keeps a pass off the layer below. So a
  viaduct over a street repaints the street's whole column wherever the ground theme fills in plain stone,
  and the cure is a value in a *different* theme — while the store answers 200 and the export gate answers
  OPEN. Either bound a plain layer's bands to its own span, or complain where a layer stands over one whose
  resolved `fill` is `(1, 0)`, a document-level read since the themes and the stack are both in the layout.
  `docs/world-export/terrain-painting.md` states the bedrock-course rule and changes with it. **`B144` does
  not reach this**: it settles *whose* theme owns a column, and this is how far *down* that theme's bands run
  — `TerrainPainter.Paint` paints each pass from the bedrock course upward, and `floorByLayer` is filled for a
  `prop` layer alone.

  *`opus5-tiefkreuz` build 1, `GET …/column?at=0,66`: `y 42..39 Iron Block` the viaduct rail, `y 29..27
  Stone Bricks` the street lid, and `y 26..1 Iron Block` — twenty-six courses of city painted as rail. The
  isometric drew a grey board and only `column` found it.*

### A made thing is a third kind, and it is drawn out of layers

- [ ] **WE77 — `WX11` measures a structure's plinth from the highest terrain its footprint touches, and the
  stamper seats it on the lowest.** The same maximum-over-a-footprint reading put a goal's bedrock plate above
  the goal it protects, and `WE82` settled that half by handing the stamper the ground the goal resolved on
  rather than letting it read one; this is the other half, and the check is what reads wrong now.
  `MapExportComposer.CheckStructureSites` takes
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

- [ ] **S59 — Per-vertex height is the headline feature and is found by accident.** The path is: select a
  polygon, read the one conditional sentence in the inspector, click a vertex on the canvas without moving it,
  then type into a field that appears in the panel. On the canvas a vertex handle looks exactly like a drag
  handle and its height is a bare text label, so nothing says a click-without-drag does something a drag does
  not. Make the height labels read as interactive (a pill or a hover state), and ideally let the label itself
  be edited or scrolled in place rather than round-tripping to the inspector. The shift-click 2–3 vertices
  slope-fit has the same problem and the same fix. The 3-D preview is where a height edit is actually legible
  and it now draws the built world (`FEATURES.md`), but it is a modal swap rather than a companion view, so it
  confirms an edit after the fact rather than while it is being made.

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
  a map can be re-imported — which is **`B9`**, and `docs/backlog-strategy.md` files that as roadmap:
  a capability nobody is blocked on. So this entry waits on one nobody has asked for, and doing it alone
  fixes the derivation for maps imported after it and for none of the maps that exist.


## The plan model: pieces, and the edges between them

`PieceInterfaces` turned every seam between two plan pieces into a read — its height delta, its typed wall,
each side's frontline share, the straits between bridged islands — and the lint table quantifies over it
(`SP8`/`SP9`/`ST8`/`ST9`/`BZ11`/`FR8`/`CT12`). What is left is one number nobody has stated, one read the
seams support and nothing asks for, the word the model uses for a seam — and the rules that are about a
piece's own geometry rather than about what is stamped on it: what a spawn's ray faces, what a wall seals,
and what a `subtract` takes away.

- [ ] **TN14 — Cycling a spawn's facing leaves the iron it seeded on the old hand.** The editor asks
  `POST /api/plan/room` once, when the piece is drawn, and writes the answer's `at`, `footprint` and `iron`
  onto new placements (`plan-bridge.js:128` `seedRoom`). Cycling the facing afterwards — a re-click on a
  selected spawn (`canvas/plan-canvas.js:1053`) or the rail's Cycle facing button (`plan-bridge.js:446`
  `cycleFacing`) — rewrites `facing` alone, so the cube keeps the side it was seeded on. Re-ask on a facing
  change and move the seeded cube, leaving one the author has since slid alone.

  *the door walls do not move with the facing, so the footprint does not go stale; the facing breaks the tie
  between two equally wide walls, `Doors[0]` is what the cube is seated beside, and the cube stands on the
  player's right as they walk out. On quatrefoil's two-door `spawn` piece the answer's `iron` is
  `[3.5, 16.5]` for `back`, `left`, `back-right`, `front-left` and `back-left` and `[16.5, 10.5]` for
  `front`, `right` and `front-right`, at an unmoved `footprint` of `[1, 1, 12, 12]`.*

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

- [ ] **G187 — The funnel capacity, and a flow term the evaluator can fire.** Plan-tier flow is read
  already: `PlanFlow.Read(plan)` takes a `PlanModel` and `GET /map/{slug}/plan/flow` serves it, `PlanRoutes`
  reads one journey's corridor and the holes on it, and `Cells.WaysRound` is built and called from
  `PlanRoutes.cs:169`. Two things are left. **`MinVertexCut`** — unit-capacity vertex max-flow, the funnel
  capacity `match-flow.md` §2 asks for — is the one `Geom.Cells` primitive still missing. And no flow
  reading reaches the evaluator: its 29 terms walk the surface for distances (`SurfaceNav`) and `Evaluate/`
  cites neither `PlanRoutes` nor `PlanFlow`, so a dead-share term wants writing over the answer `PlanFlow`
  already gives — at `POST /plan/evaluate`, the first call in the loop, before a map row exists. That
  second half is what makes `G164` a short consumer rather than a project.

  **`WaysRound` cuts with a ray, and `MinVertexCut` is a capacity rather than a second way count.** Counting
  the cut's components answers the opposite question on the same corpus — "rotation never splits on any ring
  board" against "splits on nearly all of them" — because an uncuttable door cell inside one barrier splits
  it into two fragments with no second route.

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

- [ ] **B54 — A rebuild drops a hand-drawn shape and says nothing.** `PUT /map/{slug}/sketch/from-plan`
  carries the finish, the relief and an author-corrected structural height, and refuses **409** rather than
  orphan a relief. Geometry drawn in the sketch is carried by nothing — right, since the plan owns the board —
  but the reply does not say so: `200`, `{"orphaned": []}`, no warning, which tells a caller the opposite of
  what happened. The endpoint already asks this of the relief and answers it as `SketchFromPlanDto(orphans)`;
  a stored shape no compile output and no `intentRef` accounts for is the same question of a different field,
  and belongs beside it in the reply and as a complaint. `SketchEndpoints`, `docs/tools/sketch.md`.

  Two wordings go with it, in `PlanTool.razor`'s rebuild confirmation: **Keeps** omits the relief, which is
  the most expensive thing on the board and is kept, and **Replaces** says "everything the plan states",
  which a hand-drawn shape is not. The client also never sends `?force=true`, so a rebuild that would orphan
  a relief dies as `save layout failed (HTTP 409)` with nothing offered.

  *Built from `opus5-corbel-scar`'s plan, sketched on, then rebuilt after growing one piece by two cells:
  relief, theme and prop carried; the circle gone from the shapes and from `team`'s `shapeIds`; `200`,
  `{"orphaned":[]}`, no `Pgm-Warnings`.*

## Refactoring and cleanup

- [ ] **C64 — What is left of the CSS that styles markup nobody renders.** The dashboard run is gone
  (`FEATURES.md`); **54 of 598** selectors across the studio stylesheets are still matched by no `.razor`,
  `.cs` or `.js` and are not a modifier a component composes at runtime — `components.css` **23**
  (`panel-accordion`, the seven `choice-*`, `map-row-action`, `list-row-btn`), `editor.css` **18**
  (`topbar-actions`, `topbar-changes-badge`, `map-svg`, `layer-item`, `page-placeholder`, `geo-label-input`)
  and `design.css` **13** (the `gen-*` family). These are scattered rather than one surface, so each wants
  its own look: a name here may be the last of a component that half-shipped rather than the leftover of one
  that went. **Two traps.** A modifier whose stem is composed in C# — `action-btn--<variant>` — reads as dead
  and is not, which is why the count excludes them. And a compound naming a live class inside a dead ancestor
  reads as *live* and is not; `sidebar-import-row .field-input` was one, and a grep will never find the next.

- [ ] **TS107 — The Dressing phase calls every tree vanilla, and the wood picker behind it is unreachable.**
  `SketchDressingInspector.IsGrown` reads the placement's own `form` field, and **nothing writes it**: the
  only writer is `SetForm`, which has no caller since a tree became a recipe picked from the library. So the
  blurb under the title always ends "A vanilla tree of its species: trunk, canopy, proportions" — including
  over a placement wearing `grown conifer`, whose sentence should be "Grown from a branch skeleton you shape,
  in the wood you choose". `SetForm`, `LoadWoods`, `woods` and `woodedFor` go with it: the wood cards they
  fetch are reachable from nothing. **The ruling first:** does a placement carry the form beside its `style`
  — `PickRecipe` already holds the recipe JSON it would come from — or does the inspector resolve it from the
  registry entry `pullRecipe` just wrote under that name? The second keeps one statement of what a tree is;
  the first costs a field that has to be kept in step. Evidence: place a tree, pick `grown conifer`, read the
  paragraph under **Tree**.

- [ ] **TN16 — The structure preview calls a wool room a `wool-cage`.** `StructureBox.Kind` is one of
  `spawn-cube`, `wool-cage`, `iron`, `destroyable`, `core` and `wall` (`PlanStructurePreview:74`,
  `PlanInspectDto:117`), and the two room families are the only ones naming a thing the rest of the studio
  calls something else: the vocabulary beside them already says `woolRoom` (`StructuralRoles.WoolRoom`), and a
  board binds a shell for a **wool** room and a **spawn** room (`roomStyles.wool`, `roomStyles.spawn`). Rename
  the two to `wool-room` and `spawn-room`, and change the client that draws them. The words are this DTO's own
  constant and nothing else spells them, so the surface is three source lines, three in
  `PlanStructurePreviewTests` and four in `docs/` — one of which is a dated `rules.md` amendment and stays as
  it reads. The prose goes with it: nine docstrings across `WorldBuilder`, `MapExportComposer`, `SketchRules`,
  `SketchMaterialGate` and `PlanStructurePreview` still call the structures a wool cage and a spawn cube,
  which is the same word under a different hat and wants renaming in one pass rather than in two.

- [ ] **RP65 — The layout DTO still says `JsonElement` where its own routes say `DressingDoc` and
  `BiomeField`.** `SketchLayout.Dressing` and `.Biome` are `JsonElement?` because their types live in
  `Minecraft` and `SketchLayout` lives in `Pgm`, which are siblings over `Domain` + `Geom` — the fields' own
  docstrings say so (`SketchLayout.cs:41-50`). The part routes publish both models and the store-time gate
  reads both (`FEATURES.md`), so what is left is the whole-layout route: `GET`/`PUT /map/{slug}/sketch` names
  the two fields with no shape under them, and a reader who starts from the document rather than from the
  parts finds two holes in it. Move the dressing and material model down to a project both reach, or publish
  the two schemas from `Minecraft` and reference them from the layout.

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
  `/maps/{slug}/plan` from a single component through six `@if (MapBacked)` branches, and the two render as
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

- [ ] **C51 — Nineteen selects outside the authoring surface are still hand-rolled.** `Select` and
  `StyleSelect` serve the library and the terrain components (`B259`, `FEATURES.md`), and the sketch tool's
  three inspectors have since adopted them. What is left is 25 raw `<select>` — the plan tool 10
  (`PlanTool.razor` 9, `PlanInfoPhase` 1), Configure 5, Edit 6, the sketch tool 1, the world canvas 1 and a
  page 1, plus `Select.razor`'s own — **of which Edit's six go with `TE3`**, so the work is 18. Each is the
  same options-and-a-value question written as markup, so a group, a per-row note or a disabled row has to be
  re-invented wherever one is wanted. Adopt the control at those sites; `docs/client/ui-conventions.md`'s
  *Forms* tier already names it.

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

- [ ] **TL15 — Anything can be filed as a `copied` tree.** `copied` means cut out of a world
  (`docs/tools/library.md`, the author's ruling) and `tools/seed-trees.cs` over
  `pgm-studio-mapgen/showcase/tree-showcase` is the only thing that cuts one, but `PropStyleLibrary.Save`
  takes `form: "copied"` with the request's own `Body` array, so a board can post a block list it made up and
  the row is indistinguishable from a cut one — which is how a dead-bush cluster, a log pile and a crate came
  to be filed as trees. What is wanted is the **refusal**, not a provenance card: give `TreeStyleRow` the cut
  — the world directory, the foot's world coordinates, the date — written by the cutter and absent on
  anything else, and refuse a `copied` row that carries none. **Weigh against:** a body is the only recipe
  that can hold a block the two generative forms cannot emit, so a hand-built prop needs a form word of its
  own before `copied` can be closed to it.

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
