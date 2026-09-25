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

**And a drained programme's last entry lands here.** The hill and the shop are both built and both
answerable over the API; what neither has is a step, so `TC7` and `TC9` sit with the surface that owes
them one rather than with the contract work that shipped them.



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
  commit — `routing-and-ia.md` describes it as a surface an author can open, and
  `docs/tools/edit.md` is the document that goes. `TE2` went with it: the tool's wool picker spelling the
  sixteen dyes a second way is a defect in a surface with no future, and `WoolColors` is already the one list
  every other reader takes.

- [ ] **TC7 — The configure tool cannot place a hill.** The API is the way in — an agent adds
  `controlPoints` to the intent it already posts (`docs/pgm/control-points.md` §9) — and the wizard has no
  step for it. What the step states is a count and the anchors; the tuning is one shared block and already
  has its answer (`capture-time="5s"`, `points="1"`, `<score><limit>750</limit></score>`), so the step fills
  it rather than asking. The anchors are the plan's to derive and it does (`FEATURES.md`), so the step asks
  for a count on a board with no plan behind it and shows what the plan already worked out on one with.

- [ ] **TC9 — The configure tool cannot place a shop.** The API is the way in — an agent adds `shops` to the
  intent it already posts (`docs/pgm/shops.md` §9) — and the wizard has no step for it. What the step states
  is the menu: a name, a currency, and a list of material/amount/price rows. Where the keepers stand is
  derived unless one names a place of its own (`docs/pgm/shops.md` §9), so the step offers the place and
  fills nothing in where the board says nothing.

## The sketch tool: shapes, islands, and the ground they become

The depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection highlight);
what is gathered here is the parked and dormant slices of the same surface.

### Painting terrain


- [ ] **WE60 — `repeat` names a cycle and holds a course.** `BandEnding.Repeat` makes the last band claim
  everything past the stack, which is what `BandStack.At` does and what the enum's own docstring says. The
  word says the opposite: a reader who has met a repeating texture anywhere else expects the bands to cycle.
  A painting agent authoring a `height` strata for a 9-block pillar wrote three courses of red sandstone and
  six of smooth and reported it as a defect, then wrote its courses out longhand to work around behaviour
  that was never wrong — evidence at `(-4,-49)` on `specs/probe-badlands-2` in `pgm-studio-mapgen`. Nothing
  to fix in the painter; the fix is the name. `hold`, `carry` or `extend` each say what it does, and
  `BandEndings.Repeat` in `ThemeVocabulary` plus the `"repeat"` on the wire and `TerrainThemeJson:139` move
  with it. A stored theme carrying the old word has no second reading, so the callers change in one commit.

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

### Placing something on a storey that is not the top one

All six placement kinds carry an optional `Layer`, a prop carries one, and `BuiltTerrain.SurfaceFor(layer)`
answers that storey's own surface — so what a document states, the world builds. Each entry below states what
cannot state it.

*Measured on a two-storey board (`under` y0..7 under `ground` y24..31), two wools alike but for the field: the
one naming `layer: "under"` builds at **y7** with its cage around it, the one naming nothing at **y31**.
`PUT /map/{slug}/intent` is what places an underground objective.*

- [ ] **B263 — A prop's layer cannot be seen or changed, and every storey's props draw alike.** Placing one
  already records the storey (`dressing-doc.js` `add`, `TS45`), and `DressingContext.GroundFor` resolves it,
  declining `DR-LAYER` where that layer has no ground. Two reads are missing. `SketchDressingInspector` has no
  field for `PlacedProp.Layer`, so moving a prop between storeys means editing the layout by hand. And
  `dressing-render.js` draws a gallery-floor prop exactly like the roof one over it, so a stacked board's
  dressing reads as one plane. The Sketch tool already carries the layer strip, so this adds no chrome.

  **Needs a ruling first:** should a prop on an inactive storey be dimmed, hidden, or drawn as it is with a
  badge? The field is an afternoon; how the canvas says which floor something is on is the actual decision.

- [ ] **B264 — Configure cannot address a storey at all, so no objective can be authored below the top one.**
  Not six missing fields. `SketchLayerStrip` appears in one file, `SketchTool.razor`, and every Configure
  canvas runs base-layer-only by construction — `SpawnStep`, `TeamAssignStep`, `WorldIslandsStep` and
  `WorldSymmetryStep` each say so in their own comments. So there is no active layer for a placement to take,
  and the point-pick surface cannot pick a point on a lower floor even if there were.
  Build in that order: the strip in Configure, a pick that resolves against the chosen storey's surface, then
  the six write paths (spawn, wool, monument, iron cube, destroyable, core) and a field on each inspector.
  `docs/tools/configure.md` gains the storey to its phases.

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


## The shop: buying things in the middle of a match

- [ ] **PG16 — A spawner's drop height is the author's and nothing checks it.** `SpawnerIntent.At` carries a
  `Y` and the slice writes it verbatim, so a generator stated a few blocks low drops into rock and one stated
  high rains its stack down a cliff — neither says anything. A capture point has the opposite shape and the
  right one for ground: `ControlPointIntent.Anchor` states no `Y` at all, and the pad is cut into whatever the
  world build solved under it. A spawner cannot take that wholesale, because a generator on a built plinth is
  a real shape, so what it wants is a way to say *on the ground here* — an absent `Y` resolved against
  `terrain.SurfaceTop` at export, beside where the capture point's pad is cut
  (`WorldBuilder.StampControlPoints`). Evidence: `SpawnerGenerator` runs in the document pass, before the
  terrain exists, and `docs/pgm/shops.md` §10's worked example has to state `"y": 12` from a `column` read.

- [ ] **PG15 — A spawner's rate cannot climb as the match runs.** `SpawnerIntent` states one delay, so the
  corpus's commonest generator shape after the plain one cannot be authored: the same drop on the same region,
  restated with a shorter delay behind a time filter. It wants a list of rungs on the spawner — a delay and
  the minutes it starts after — with the slice writing one `<spawner>` per rung over the one pair of regions
  and minting the `<after>` filter each names, since a filter id is a feature the intent does not author
  either. Evidence: 369 of the corpus's 1,432 spawners carry a `filter`, and
  `ctw/mame_i_shrunk_the_pvpers` states each of its two gold-nugget generators four times — `8s` plain, then
  `5s` behind `after-30m`, `after-60m` and `after-90m`.

## Composed boards the author judged: what a larger board is made of

Twelve judged donut boards at 20 and 30 players named what a larger composed board gets wrong. `MD7` now scores the thin, long crossing; the rest is below, to be taken
**one change at a time** and judged between, because changing several at once made the boards worse.

- [ ] **G278 — Parked: a wider front paid for out of the hub.** *Blocking question: which single change is
  tried first.* A combined round — the face drawn from a fixed range per band (24–36 · 32–48 · 40–56 · 48–64
  blocks), the hub capped at 44 · 52 · 68 · 76 blocks, two wools from micro up, a two-legged front only where
  each leg reaches `FR9`, 16-block stones — widened the crossing but made the hubs uniform (every milli hub a
  68-block ring; the double-hole, G and P gone) and the wool placement worse, and was rolled back. The ranges
  are the author's and stand for when this is resumed.

- [ ] **G279 — A donut hangs toward the back, and its spawn steps inward to clear it.** `SeatOverhang` draws
  either flip; prefer the one whose ring runs toward the hub's back, and let `UnitSeating.SpawnCentre`'s
  back-end seat slide inward past the ring. Also seat the donut nearer the spawn where its route to the wool
  stays long. `docs/generator/model.md` §5.6.

  *Evidence: judged boards p20 seed 71 (the ring runs toward the middle) and p20 seed 25 (the donut sits far
  from the spawn with a long way to its wool).*

- [ ] **G280 — A donut may dock turned 90°, its entry and a third of its long bar against the hub's side
  wall.** A new dock for `ShapeFamily.Donut` in `UnitSeating.SeatOverhang`/`WoolBoxEmitter`, beside the
  entry-stub dock. `docs/generator/model.md` §4.6.

  *Evidence: judged board p30 seed 32, where the author proposed it.*

- [ ] **G281 — An approach may raise its arm: an `I` or `L` seated at the hub's far edge, facing the way the
  spawn faces.** Today every approach leaves the hub sideways. Seat it at the end of a lateral edge nearest
  the back, turned so its run heads toward the back, as a variety draw in `UnitRequests.WoolRequest` and
  `UnitSeating`. `docs/generator/model.md` §5.5–5.6.

- [ ] **G282 — On an L-shaped hub with the spawn at the back, the short arm takes a long `I` turned so its
  wool stands ahead of the spawn, with a build zone across the bay.** The zone gives attackers a second,
  shorter way in and moves the wool further from the spawn. Build it first as an adapted plan through the
  studio's API and play it back with the author, before any composer change. `docs/tools/plan.md`.

  *Evidence: judged board p20 seed 80, where both wools cannot sit well on an L hub.*

- [ ] **G283 — A wool stands too near its own spawn or too near the front on a two-wool board.** The
  non-donut wool sits close to its spawn on p20 seed 24, and the second wool close to the frontline on p20
  seed 50, where the author would shift both wool boxes back. `spawn-wool-ratio` and `wool-front-ratio`
  already score it; the fix is in where `UnitSeating` seats the second wool. `docs/generator/model.md` §5.6.

## The plan model: pieces, and the edges between them

- [ ] **TN21 — a plan's `meta.authors` does not survive the compile.** `POST /api/plan/compile` answers an
  intent whose `meta` is `{name, created: "", authors: [], contributors: []}` however the plan's own `meta`
  was filled in, so a board driven plan-first exports with `EX6` — the observer platform's authors board
  gets a heading and nothing under it — and the names have to be written onto the intent by hand afterwards.
  `PlanCompiler` is where the intent's `meta` is built; carry `name`, `authors` and `contributors` across
  from `PlanModel.Meta`. Evidence: `pgm-studio-mapgen`'s `techniques/walls-and-iron` plan states
  `meta.authors: ["the technique cards"]` and the compiled intent comes back with `authors: []`; its
  `compiled.txt` prints both.

- [ ] **PG17 — `DC3`'s own text describes a verbatim write the export does not do.** The rule says a
  material naming nothing the studio builds "writes into the map.xml verbatim while the blocks come out
  obsidian, and a declared material matching nothing in its own region is a goal at zero health (`OB3`)".
  Measured, both ends move together: a destroyable authored `materials: "diamond block"` builds three
  obsidian and the export writes `materials="obsidian"`, so the `OB3` case the sentence warns of cannot
  arise this way. Correct the rule's `means` to say the word is resolved rather than passed through, and
  check `docs/pgm/destroyables-and-cores.md` for the same claim. Evidence:
  `pgm-studio-mapgen/techniques/destroy-goals/mismatch.txt`, which posts it and reads the world and the
  document back.

- [ ] **G271 — A split band that is refused still carries no stone, so the crossing has neither an island
  nor a bay.** `MidCarver.TryCarve` returns `[]` whenever `design.SplitBand` is set, on the reading that the
  bay between the split's two legs is the island. But `SplitRun` grants the split only where the face admits
  one, and the gap was already fixed at `EmptyHalfGapCells` when the request was made — so a refused split
  spends the wide empty crossing and puts nothing in it. Carry the realised split out of the carve and lay
  the row when it was refused. `docs/generator/model.md` §5.13.

  *Evidence: `p8 rot_180 seed 2` (nano, `pgm-studio-mapgen/specs/opus5-cleftmoor`) — the band is
  `x[-3,3) z[-4,4)`, exactly the 6-cell frontline hull and its own rot_180 image, so there are no two legs
  and no bay; `mid 0/28` cells of the crossing's share, and 32 blocks of plain void from front to front.*

- [ ] **G270 — A mid stone's depth is fixed before the hull that bounds its width is known, so a
  narrow-fronted board under-spends the crossing.** `MidCarver.Crossing` sets the half-gap from
  `StoneDeepCells` before allocation, because the allocator takes it as its axis margin; the width then comes
  from the frontline hull the carve is handed. Where that hull is narrow the stone shrinks but the depth
  cannot grow to compensate, so the mid spends **66–85%** of its share and 17–33% of boards carry no stone at
  all — about half of those split bands (`G271`) and half hulls too narrow for one stone at the
  wider-than-deep rule. Either the crossing is designed twice (a provisional gap, then a re-carve once the
  hull is known) or the depth reads a hull the envelope can predict. `docs/generator/model.md` §5.13 and
  `rules.md` amendment 35.

  *Evidence: at centi the mid's share is 109 cells and the row spends 72 — `p32 rot_180 seed 0`, hull 21
  cells, two stones of 7×6 where the share would buy 9×6.*

- [ ] **G268 — A frontline spine docked flush on a hub wall makes one slab twice the corridor deep.**
  The frontline's spine is one corridor deep and the hub's wall behind it is another, and the spine docks
  edge to edge across its whole width, so the two read as a single solid run of `2 × corridor`. Measured
  over 48 composed boards a band (`docs/world-scan/map-size-ladder.md`): the fault appears on **22–32 of
  them at every band**, and its widest run grows with the corridor — 48×24 blocks at nano, 52×32 at micro,
  100×32 at milli and 116×32 at centi — against a corpus whose own modal ground width is
  10 · 14 · 16 · 16. The grid does not reach it: a board's modal width is 30 at micro on cell 5 and 29
  on cell 4. `TeamUnitFiller` already prefers a strand frontline over the solid bar on a holed hub; what is
  missing is the same preference read off the **edge the spine actually docks** rather than off the hub's
  form. `docs/generator/model.md` §5 and `rules.md` `FR6`.

  *Evidence: `p16 mirror_z seed 1` — `frontline-t1 z[5,9)` docked on `hub-t2 z[9,13)` across `x[-6,7)`:
  52 × 32 blocks of unbroken ground.*


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

- [ ] **G263 — `WL2`'s "different lane" clause has no term, so a wool room may abut its spawn.** The rule
  reads *"on a different lane than the spawn; wool↔spawn ≥ 20"* and only the distance half is built:
  `SpawnWoolFloor` (hard, `MinBlocks = 20`) and `SpawnWoolDistance` (soft, `[27, 170]`) in
  `src/PgmStudio.Pgm/Evaluate/Terms/SpawnTerms.cs`, both measuring the walk from the spawn **point** to the
  wool **block**. `WL6` — one wool to a lane — has no term at all. Add a hard term beside them indicting a
  wool-room piece that shares an edge with, or lies within a few cells of, a spawn piece; `PieceInterfaces`
  already answers that seam. The composer cannot emit the shape — every composed wool unit is `boxes: 2`,
  the room plus its lane, against a spawn's 1 — so only a hand-authored plan reaches it, which
  `AUTHORING-BRIEF.md` asks authors for. `docs/generator/rules.md`.

  *`opus5-redmarl` places `dye-w [-13,-26,5,4]`, `yard` (spawn) `[-8,-26,7,4]` and `dye-e [-1,-26,5,4]` in
  one row with their edges touching, 8 blocks apart in the built world. `POST /plan/evaluate` answers
  `score 0, valid true`, because the walk it measures is 33. `opus5-mirkholt` and `opus5-flintwick` are the
  same shape; `opus5-coinfall` is the counter-example, with a 15-cell `run` piece between the two.*

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

- [ ] **WE131 — A house style stating `"beams": null` answers 500, not a finding.** `HouseStyle.Beams` is
  a non-null `BeamStyle` with a default, and an explicit JSON `null` binds past the default: the store's
  `SketchMaterialGate.Check` throws at `HouseStyleValidation.CheckBeams` (line 196), and every build of the
  board then throws at `HouseStamper.LayBeams` (line 496). Refuse the null where the style is bound, with
  the rule naming `{"block": -1}` as the way to say "no beams"; `docs/world-export/structures.md` §7 says
  which is the shape.

  *Evidence: fork `bothy` with `shell.beams` set to `null` under `dressing.styles` and `PUT …/sketch` —
  `RQ2`, 500, stack in the server log. The same style with `{"block": -1}` stores 200 and stamps.*

- [ ] **WS72 — `GET /map/{slug}/coverage` and its `?format=png` each walk the whole board.** Both run
  `GroundCoverage.Read` over the same stored documents, a field per waypoint and a walk per pair of them, and
  `drive.py` asks for both on every run: 2.5 s apiece on `opus55-scarbutte` in the Debug studio, the largest
  read a drive still waits on. The picture wants the numbers the JSON already computed, kept the way
  `BuiltWorlds` keeps a world — keyed on what the read derives from, so an edit is a new key.

- [ ] **RP72 — One unbindable field discards the whole intent, at 200, and the export gate opens on it.**
  `POST /map/from-documents` answers 200 and stores a map with **no teams, no spawns and no objectives**
  when the intent carries one field the binder cannot read. Nothing is raised: no `RQ3`, no `warnings`
  entry, no `Warning` header, and `GET /preflight` then answers `exportReady: true` on the result. The
  binder's failure to read one property is taken as the whole object being absent, so the deserialized
  intent is a default instance and every downstream reader agrees it is a valid empty one. A refusal
  belongs where the binder gives up; an intent that states teams and comes back with none is the one
  shape `RQ1` exists to catch. `docs/refusals.md` carries the gate catalogue.

  *Evidence, reproducible on the running studio: post `opus5-fallowgate`'s own plan, layout and intent
  under one slug, and the same three with `"modes": ["dtm"]` added to the intent under another. `modes`
  is a real `MapIntent` field and takes `ModeIntent` objects rather than strings. Both answer 200 with no
  warning header. `GET /map/probe-control/intent` reads `teams 2, spawns 2, destroyables 2`;
  `GET /map/probe-badmodes/intent` reads `teams null, spawns 0, destroyables 0`. Both preflights read
  `exportReady: true`.*

- [ ] **RP73 — `crown` is signed in world space, so a positive crown fills a negative push back in.**
  `ReliefSolver` adds the crown to the amount without regard to the amount's sign
  (`Relief/ReliefSolver.cs:478`, `amount += push.Crown * PushMark.Ease(...)`), so on a push of amount −12
  a crown of +12 returns the floor's centre to the surrounding level and only a negative crown dishes it.
  The field's own docstring is written from a raising push — *"how much higher the middle of the push
  stands than its edge"* (`Geom/Relief/Marks.cs:331`) — and says nothing about the other direction, which
  is the direction a pit is made in. State it in the docstring and in `docs/world-export/relief.md`. The
  editor's default of 2 against the record's 0 is the same fact with teeth: a pit knobbed up in the
  inspector starts with a two-block mound in its floor.

- [ ] **WE129 — A house excavates its footprint with no ceiling and nothing reports how much.**
  `Decorator.Ground` seats a house at `lowest - 1`, the minimum first-air-Y over `plan.Cells()`
  (`Dressing/Decorator.cs:1033`), and `Decorator.Excavate` then clears every footprint column from
  `floorY + 1` to its own surface (`Decorator.cs:1051`). Both are deliberate and right on a slope. Neither
  is bounded: a footprint whose lowest cell sits in a pit deletes that whole depth across the plan and the
  building stands in the hole it dug. `DR-SLOPE` is the only guard and it tests the same `rise` against the
  building's own height (`Decorator.cs:812`), so a shell tall enough to afford the rise excavates it in
  silence. Raise a complaint carrying the courses removed and the columns they came off — `Excavate` holds
  both numbers at the moment it removes them. Whether it should also *refuse* past some depth is the
  author's call and is not assumed here. `docs/world-export/decoration.md` §the seating rule.

  *Evidence: `opus5-whitegape`'s `works-shed` on a yard whose surface is y23–24 seated at y13, the quarry
  floor. `column (2,-50)` reads the yard face as stone brick y19–23; `column (3,-50)` one block east reads
  the shed's brickwork starting at y13. Rise 10 against a two-storey `buries` of about 13, so `DR-SLOPE`
  stayed silent. `decoration.md:767` already records the same failure on `opus5-ravensmere`.*

- [ ] **WE130 — `sketch/seats` answers a question it cannot answer for a house.** The seat query reports the
  yard beside a quarry as a legal seat, because the three rules that read the built world — `DR-CROSS`,
  `DR-WAY` and `DR-SLOPE` — are the dressing pass's to raise and not the query's
  (`Api/Endpoints/SketchEndpoints.cs:483`). An author who asks `seats` before placing a building, which is
  what the skill tells them to do, is told yes and then gets a building in a hole. Either the query runs the
  seating arithmetic it is being asked about, or its answer says in terms which questions it did not ask.
  `docs/tools/sketch.md` carries the endpoint.

- [ ] **RP71 — A map cannot be deleted.** The API carries 26 `DELETE` routes and every part of a map is
  removable through one — layers, groups, shapes, vertices, props, relief, themes, biome, room styles, teams,
  wools, spawns, regions — and none removes the map row. `DELETE /map/{slug}/sketch/discard-if-empty` drops
  only a pristine never-drawn draft, so a map that stored once is permanent short of SQL. Every `map_id`
  foreign key is already `ON DELETE CASCADE`, so the work is one endpoint over `MapRepository`, not a schema
  change. It matters for a driver rather than for the browser: a spec re-driven under a corrected slug leaves
  the old one behind, and a harness that builds a map per variant has no way to clean up after itself.
  Lands beside the other whole-map routes; `docs/architecture.md` carries the route surface.

  *Evidence: seven scratch maps (`stage-01-ground` … `stage-07-dressed`) were left in the dev database by a
  staging harness that had no route to remove them, beside the two real maps.*


- [ ] **WS68 — Every built board reads `bridgeable 0`, because the export grants building by forbidding it
  everywhere else.** `BuildGenerator` wraps the buildable rectangles in the `not-build-area` negative and
  applies `block-place=not(void)` to it — the corpus idiom `docs/pgm/template.xml` writes — so inside the
  build region *nothing* applies. `Editability.Zones` sets `granted[i]` only on an explicit `Allow`, so those
  cells come back `ground` rather than `build_zone`, and `WorldWalk`'s bridgeable set counts `build_zone` and
  `filtered` only. The reads that stand on it — `reach`, `coverage`, the walk tiers — therefore treat a
  crossing nobody is forbidden to bridge as unbridgeable. Read a void column inside a region whose only rule
  is a negative void-deny as a grant, in `Editability.Zones`; `docs/world-scan/read-backs.md`.

  *Evidence: `pgm-studio-mapgen/specs/opus5-stannerford` exports `<rectangle id="build-area-1"
  min="-16,-20" max="16,20"/>` under `not-build-area`, and its `renders/04-reach.txt` reports
  `bridgeable 0` while calling the mid stone at `x -12..11, z -8..7` (384 cells, floor y12) and the whole
  opposing half at `x -24..39, z -92..-21` (2363 cells) `no-build-zone`.
  `grep -o 'bridgeable [0-9]*' specs/*/renders/04-reach.txt` answers 0 on all 40 boards there.*

- [ ] **WS69 — A stated bedrock wall is the walk's worst step, and a transect over the same two cells
  disagrees.** A plan's `walls` entry stamps three bedrock courses and a cobweb cap across a wool-lane
  interface (`PlanCompiler.BedrockCourses`), which is the feature and not an obstacle: bedrock cannot be
  destroyed so a defender builds on it, and four courses is what an attacker bridges (author).
  `WalkProfile.Events` words every rise from `Walk.StepWord` alone (`WalkProfile.cs:38`), so the climb onto
  a wall reads `barrier` and sets `worstStep`, and a reader taking `worstStep` for the board's worst fault
  condemns a wall the map states deliberately. The profile is the site: `Of` and `Events` take the path only
  while the endpoint already holds `read.Built.Provenance` at the call (`WorldReadEndpoints.cs:904`) — pass
  it, word a step landing on a `wall` claim as the wall it is, and keep it out of `WorstStep`.
  `docs/world-scan/read-backs.md` §what a walk costs.

  *Evidence: on `technique-composed-4-taken-over`, `walk?from=-14,60&to=-14,76` crosses with 3 blocks placed
  and answers `worstStep 4` with `{"x":-14,"z":67,"rise":4,"word":"barrier"}`, while
  `transect?points=-14,60;-14,76` over the same line reads the ground under the wall as 14→15, names both
  stations `wall 0`, and answers `worst step 1: 0 barrier`.*

- [ ] **WS70 — `walk?beside=` cannot name a wall, because its set is an allow-list documented as a
  deny-list.** `WalkProfile.StandingKinds` lists nine kinds (`WalkProfile.cs:52`) under a docstring reading
  "everything but the ambient cover (`flora`) and the paint (`stroke`)", and `StampId` documents thirteen —
  so `wall`, `roomfloor` and `redstoneline` are absent from every `beside` answer without anyone having
  decided they should be. A route that climbs a bedrock wall reports nothing beside it. Name the kinds a
  player meets, `wall` first, and state the set as what it holds rather than as what it drops.
  `docs/world-scan/read-backs.md` carries the query word.

  *Evidence: `walk?from=-14,60&to=-14,76&beside=3` on `technique-composed-4-taken-over` answers `beside: []`
  at every radius 1–3, while `transect` over the same line names `wall 0` at (−14, 67) and (−14, 68) — two
  cells the route itself passes through.*

- [ ] **WS71 — A crown over void is a standing place for the walk and a void column for every other
  read.** `WalkGround.OfSpans` offers a place for any span top with `Walk.Headroom` clear over it, so the
  top course of a canopy hanging past a piece's rim is ground the walk will route over and will seed
  `WorldWalk.Level` from, while `column`, `transect` and the census all answer that the column has no
  ground. Both answers cannot be right, and which one is wanted is the author's call — a player *can*
  stand on leaves, and a route that climbs a tree to get somewhere still makes `worstStep` an answer about
  the canopy rather than about the board. A crown over the void is not itself irregular: `template.xml`'s
  `block-break-void-filter` allows breaking leaves and logs inside the void region, so the contract already
  expects tree parts to hang past a board's edge (the author's ruling). **Blocking question: may a walk stand on a prop at all, or is a
  prop's own volume out of the walk the way a house's interior is?** The fix follows from the answer and
  not before it; `docs/world-scan/read-backs.md` §what a walk costs carries the walk's own account of a
  place.

  *Evidence: on `technique-composed-4-taken-over`, whose oaks stand on the back bar's outer rim,
  `column?at=-4,57` answers four blocks of oak leaves over void and `transect` calls that station `void`
  with `top 21, standing tree oak-path-c`. `walk?from=-2,54&to=-4,57` answers reachable, `barrier +8`,
  `worstStep 8`, standing at `(-2, 56, 21)`, `(-3, 56, 22)` and `(-4, 57, 22)` — three places in the
  canopy, none of them over ground.*

- [ ] **WE124 — A room's stamp is a block out of place on its mirror image.** The frame a room is built out
  from is measured from the piece's own minimum corner, and `rot_180` maps one piece's minimum corner onto
  its image's maximum, so everything the stamp does not centre — the bay, the ridge, the storey posts —
  lands a block off on the far team's copy. Measure the frame from its centre instead, so a room and its
  image stand on the same columns. Evidence: on `pgm-studio-mapgen/specs/opus5-coinfall`, whose two camp
  pieces are exact mirror rectangles (the mirror gate passes), the halls stamped in them are not —
  `column?at=2,-72` tops out at `y 38` against `y 40` at its image `column?at=-2,72`, and `column?at=0,-68`
  is open grass where `column?at=0,68` carries roof at `y 32`.

- [ ] **TL15 — Anything can be filed as a `copied` tree.** `copied` means cut out of a world
  (`docs/tools/library.md`, the author's ruling) and `tools/seed-trees.cs` over
  `pgm-studio-mapgen/corpus/tree-showcase` is the only thing that cuts one, but `PropStyleLibrary.Save`
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
