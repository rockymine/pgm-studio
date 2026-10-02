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

## Parked from the programme

- [ ] **TS126 — A note's checkable claim, kept as a check.** *Parked (author): not yet.* Some notes state
  what a board must keep being — a path reaches the bridge (note 58), a bedrock wall has void on both sides
  (note 48), a wall does not float (note 54). A reply could carry the claim as a measured check over the note's
  anchor, kept when the author resolves the note, and raised as a finding naming the note when a later pass
  breaks it. `docs/tools/sketch.md` § In game.

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

- [ ] **WE143 — A band stack that cycles.** Strata are a short run of beds meant to recur, and a stack
  can only hold its last band or hand over, so a `height` strata is written out longhand to the tallest face:
  `specs/whitstone-weald/build-spec.py` `strata()` states its ten beds four times over to cover 76 courses. A
  stack following the ground (TP26) has to span the lowest ground to the highest, which makes that longer still.
  A third `BandEnding`, `cycle`, reads `BandStack.At(step % span)` past the last band, with `BandEndings` in
  `PgmStudio.Vocabulary` and the editor's ending toggle offering it. Lands with or after `WE60`, since both
  change the words the ending is spelled in.

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

- [~] **WE41 — A pattern is a family shown off rather than a ground.** *Parked (author): not yet — the
  predicate below is named, and nothing judging a pattern's look is built until the author says so.* The predicate the
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

- [ ] **WE148 — A mark or a push that lands nowhere says why.** `RL4` fires on a mark that pinned no cell
  and names only "off its group's ground"; two causes read alike under it. A mark stated over ground another
  group owns — a `team` mark on the mid stone — should name that group, which the rasterizer knows. A bevel of
  at least half the ring's short side leaves every pin under weight 1, and `ReliefSolver` drops such pins over
  unclaimed ground, so a small area mark with a wide bevel pins nothing; that is its own sentence. And a push
  whose ring covers no cell has no finding at all: `ReliefReadback.Check` reads pushes only where `Cells > 0`,
  so a push keyed on the wrong group is silent where a mark is `RL4`. *Evidence: a 10 × 6 area mark on Gypsum
  Reach (note 52's wash rim) raised `RL4` and nothing else; Sootcombe's `mid-rise`/`mid-lip` pushes are keyed
  `team` over the mid stone and cover 0 cells.* `docs/world-export/relief.md` §6.

- [ ] **WE149 — Pushes that overlap, and a mark under a push.** *Parked (author): whether two pushes summing
  over one cell is a fault or a tool.* `Sculpt` adds every push's lift (`ReliefSolver.cs`) and applies the sum
  over the marked field, so two pushes stack and a mark under a push moves the base it lifts from — as
  `relief.md` §5 states, on purpose. Nothing reports it: the read gives each push's gradients, not where two
  meet. If the author rules it a fault, the finding is the push twin of `RL3` — the pair, the overlap's cell
  count, and at the worst cell the summed lift against each push's own. *Evidence: a second push over Gypsum
  Reach's wash rim dug a pit to y7, and a line mark there did the same by lowering the base.*

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

### Placing something on a storey that is not the top one

All six placement kinds carry an optional `Layer`, a prop carries one, and `BuiltTerrain.SurfaceFor(layer)`
answers that storey's own surface — so what a document states, the world builds. Each entry below states what
cannot state it.

*Measured on a two-storey board (`under` y0..7 under `ground` y24..31), two wools alike but for the field: the
one naming `layer: "under"` builds at **y7** with its cage around it, the one naming nothing at **y31**.
`PUT /map/{slug}/intent` is what places an underground objective.*

- [ ] **B264 — Configure cannot address a storey at all, so no objective can be authored below the top one.**
  Not six missing fields. `SketchLayerStrip` appears in one file, `SketchTool.razor`, and every Configure
  canvas runs base-layer-only by construction — `SpawnStep`, `TeamAssignStep`, `WorldIslandsStep` and
  `WorldSymmetryStep` each say so in their own comments. So there is no active layer for a placement to take,
  and the point-pick surface cannot pick a point on a lower floor even if there were.
  Build in that order: the strip in Configure, a pick that resolves against the chosen storey's surface, then
  the six write paths (spawn, wool, monument, iron cube, destroyable, core) and a field on each inspector.
  `docs/tools/configure.md` gains the storey to its phases.

### A made thing is a third kind, and it is drawn out of layers

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

### Dressing: what the pass can place

- [ ] **TL35 — A copied tree states no species.** A tree save requires `species` (`RQ1`) and the library
  answers one for every row, so every copied recipe states `"species": "oak"` — `acacia-1`, `birch-3` and
  `sequoia-1` alike, and so every entry of `pgm-studio-mapgen/corpus/tree-showcase/trees.json`. A copied tree
  reads only its body, so the word is inert, but it says of an acacia what the showcase's `kinds.json` exists to
  stop anyone reading off a block. `TreeStyleSaveRequest.Species` is required of a template only,
  `PropStyleLibrary.TreeOf` gives a copied row none and `DressingJson` writes none for one; the snapshot is cut
  again after. `docs/tools/library.md`.

  *Evidence: all 84 entries of the snapshot read `"species":"oak"`.*

- [ ] **WE154 — A chest PGM refills.** A `chest` prop's stacks are written into the world and never
  refilled. PGM's `<lootables>` refills one: `<loot id>` states items the way a kit does, and `<fill loot=…
  filter=… refill-interval=…|refill-trigger=…>` fills every container its filter admits the first time it is
  opened and again on the interval or trigger, clearing it first unless `refill-clear="false"`
  (`core/…/loot/LootableModule.java`); each item goes into a **random free slot**
  (`LootableMatchModule.fill`), so a refilled chest cannot keep its stacks' order. Add `refill` to the prop —
  an interval or a trigger — carried through the codec as a `<lootables>` section beside `<kits>` (model,
  `MapParser`, both serializers, `XmlWriter.WriteItemSpec` for the items) with a cuboid region per image as
  the fill's filter. Item names differ by path: the world takes 1.8 ids (`minecraft:planks`) and a 1.8 PGM
  parses Bukkit's (`WOOD`), so the prop needs one table between them. `docs/world-export/decoration.md` §8a;
  `docs/pgm/`.

- [ ] **WE152 — A room's door width, stated by its style.** *Parked (author): what a stored `door_width` of 2
  means.* `WX7` cuts a room's door from its wall (`RoomFrames.DoorWidth`: 4 on an even interior of six or more,
  3 on an odd one, 2 at four across) and a style's `doorway.width` reaches only the dressing's houses. Honouring
  it is `clamp(stated, 2, interior − 2)`, dropped by one where its parity differs from the wall's so the door
  stays centred — carried on `RoomShells`, since `Domain` cannot see `HouseStyle`, and re-checked against the
  iron cube the plan seats beside the door (`PieceRoom.Iron`, `WX8`). The field is `NOT NULL DEFAULT 2`
  (`M0027`), and 11 of the deployed 13 room styles hold 2, so read as stated every room door narrows: it has to
  become nullable with a migration, and whether a stored 2 is a statement is the author's. *Evidence: Sootcombe
  note 10 asks for a 2-wide wool-room door.* `docs/world-export/structures.md` §7.

### Shapes

- [ ] **TS140 — A carve: a cave cut through ground that stays.** A subtract is a set of `(x, z)` cells
  that empties the whole column (`SketchRasterizer.cs:349`), and underground space is that hole under a flat
  override-add lid, so a chamber cannot keep the relief ground over it and every tunnel is a straight-walled
  shaft. Add a carve stated in three dimensions: a polyline whose vertices each carry a radius and a centre
  height, splined like a polyline's points so the bore swells and narrows smoothly between vertices, with a
  `rough` seed wandering the wall; a single point is the negative boulder, a lumpy ellipsoid. It removes
  `[y − r, y + r]` from the **built** column and leaves what is above it, so it runs after the raster the way a
  fluid already carves built terrain (`decoration.md` §7), and its walls show the theme's fill. Decide first
  what the plan-tier walks (`SK11`) and the export's reach say about a void they cannot see. `docs/tools/sketch.md`.
  *Evidence: `sketch.md` "A subtract with a lid over it": the lid comes back as the column's only span.*

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

## The composer states distances in cells, and a grid scale moves them

- [ ] **G273 — A fine row should key to the hub's legs where the front has none.** `MidCarver.Keyed` lines
  its stones up with the faces of the unit's **front row**, which is the right feature when there is more
  than one — but a front presents a single face on about two boards in three, so the keyed row lands on one
  or two boards in twenty-four and the rest centre. The hub always has a body, and the holed forms (ring, G,
  P, twin, double-hole) all carry legs either side of a bay: those are the same kind of feature and they are
  there on every board. Read them out of the hub box's pieces the way `TryCarve` reads the front's, and key
  to them when the front offers nothing. `docs/generator/model.md` §5.13 and `rules.md` amendment 38.

  *Evidence: `p24 rot_180 seed 3` (`opus5-stennerwath`) — `frontline-t1` is one 64-block bar, so nothing to
  key to, while `hub-t2 x[-32,0)` and `hub-t3 x[16,36)` are two legs 16 blocks apart. Its stones sit centred
  at `x[-12,16)`, in the hub's own bay rather than in front of either leg.*

- [ ] **G272 — Bound the run a player walks around a hub's hole at 40 blocks.** A hole's job is rotation
  (`CT8`): a loop round it gives an alternative route between lanes. Past a certain length it stops being a
  loop and becomes a wall — two players on opposite sides never meet and neither can change direction. The
  author's bar is **40 blocks on the hole's longest side**. `HubBoxCells` sizes the hub from its budget share
  alone and the holed bodies build every wall one corridor thick, so the hole takes the whole difference as
  the box grows. Either the ring's walls thicken with the box or the body splits the void — a `double-hole`
  where one ring's hole would run long. Lands in `TeamUnitAllocator.ChooseHubBody`/`ChooseHubWalls`;
  `docs/generator/model.md` and `rules.md` (`CT8` is the rule that states the envelope and measures nothing).

  *Evidence: 12 seeds a band, cell 4 — the hole run's median is 16 · 16 · 18 · 24 blocks up the ladder and
  **14 of 38 centi boards exceed 40**, none below it. The corpus of 359 CTW maps puts the run's median at 20
  and its p90 at 43 (836 voids), so the bar is the corpus p90. `opus5-threapland` is the worked case: one
  76×36 hole at cell (-10,-19), where a double-hole hub would have given two.*

- [~] **G264 — A composed wool room can stand 12 blocks across void from a piece fronting the crossing.** The
  inside corner of a bent wool is its lane's width, 12 blocks at nano, which `WL12` allows against the team's
  own ground and not against the front (16). The room's setback along its lane takes a block basis of its own
  where the ground across the corner fronts the band, in `WoolBoxEmitter` rather than in the seat clearance.
  `docs/generator/rules.md` and `model.md`.

  *Evidence: 4 of 400 composed boards (200 seeds each at 8 and 20 players), e.g. `p20 rot_180 seed 53`,
  `frontline-t1` ↔ `wool-b-room`, 12 blocks.*

## Composed boards the author judged: what a larger board is made of

Twelve judged donut boards at 20 and 30 players named what a larger composed board gets wrong. `MD7` now scores the thin, long crossing; the rest is below, to be taken
**one change at a time** and judged between, because changing several at once made the boards worse.

- [ ] **G278 — Parked: a wider front paid for out of the hub.** *Parked (author): not now. When resumed,
  the question is which single change is tried first.* A combined round — the face drawn from a fixed range per band (24–36 · 32–48 · 40–56 · 48–64
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
  wool stands ahead of the spawn, with a build zone across the bay.** *Parked (author): not now.* The zone gives attackers a second,
  shorter way in and moves the wool further from the spawn. Build it first as an adapted plan through the
  studio's API and play it back with the author, before any composer change. `docs/tools/plan.md`.

  *Evidence: judged board p20 seed 80, where both wools cannot sit well on an L hub.*

- [ ] **G283 — A wool stands too near its own spawn or too near the front on a two-wool board.** The
  non-donut wool sits close to its spawn on p20 seed 24, and the second wool close to the frontline on p20
  seed 50, where the author would shift both wool boxes back. `spawn-wool-ratio` and `wool-front-ratio`
  already score it; the fix is in where `UnitSeating` seats the second wool. `docs/generator/model.md` §5.6.

## The plan model: pieces, and the edges between them

- [ ] **G270 — A mid stone may grow to the build zone's border, and one spanning the band splits it in two.**
  *Parked (author): no composer change yet.* A zone never grows to overhang the ground it docks (`BZ9`
  stands). A stone is read by how many of its edges border the zone: **four** — inside it; **three** —
  against one side, not spanning it; **two adjacent** — in a corner; all three allowed. **Two opposite** —
  across the whole band: allowed, and the band is then emitted as two zones, one per side, neither holding the
  stone (PGM accepts one zone either way; two is what mapmakers write). A stone with **one** edge on the zone
  would stand outside the band; the composer never lays one, and nothing is checked for it, since only the
  composer knows a stone (`MidCarver.IsStone`) and a frontline docks on one edge. Several stones with zone
  between them keep one zone. So
  `StoneInsetCells` stops holding a stone a cell off the band's ends, and `MD4` and `CT1`'s one band zone are
  restated: `rules.md` (a new amendment), `model.md` §5.13, `MidCarver.Stones`.

  *Evidence: `rot_180`, seeds 0–59, normal crossings spend a median of 85 · 72 · 81 · 88% of their share
  from nano to centi; `p16 rot_180 seed 0`, band 8×12 cells, one 6×6 stone held a cell off each end, 72%.
  Spanning the band it is 8×6, 48 of 50 cells, and the band becomes two 8×3 zones.*

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

## A note and the changes that answer it

The In game phase's threads and the History phase's changes describe one piece of work from its two ends — what
the author asked, and what the board did about it — and nothing yet joins them.

- [ ] **TS131 — A change names the notes it answered, and History opens them.** A reply records the change it
  was written at (`NoteMessageDto.Change`, `Contracts/MapNoteDtos.cs:76`), so the threads a change answered are
  the ones holding a message written at it, and nothing reads that back: History's inspector shows a change's own
  note only (`SketchChangeInspector.razor:21`) and has no way into In game. List under the picked change every
  thread with a message written at it, each opening that thread in In game, which `SketchTool` already does by
  id (`?note=`). `docs/tools/sketch.md` § History. *Evidence: answer a note with a drive and open History on the
  change it landed as — the thread is nowhere on the screen.*

- [ ] **TS132 — A thread counts its changes from the note, not from its last message.** `ChangesSince`
  (`SketchNotesColumn.razor.cs:130`) counts the changes after the thread's *last* message and `LastChange` starts
  the diff there, so once an agent replies at the change that answered the note the count reads 0 and the link
  to those changes is gone — the span the author most wants. Count from the note's own first message, and offer
  "since the last message" as a second link only where a later change exists. `docs/tools/sketch.md` § In game.

- [ ] **TS133 — Coming back to History redraws the change it shows.** Leaving History clears the canvas diff
  (`setDiff(null)`, `SketchTool.razor.cs:586`), and `GoHistory` (`:235`) picks a change only when `spanTo` is
  null — so on return the inspector names the kept change while the canvas stays blank until one is clicked again.
  Redraw the kept span on entry (`ShowSpan(spanFrom, spanTo)`). `docs/tools/sketch.md` § History.

- [ ] **TS134 — A link to one note opens it once.** `?note=` is read from the query (`SketchTool.razor.cs:48`)
  and stays in the URL, and In game's `openedFromLink` (`SketchInGamePhase.razor.cs:102`) lives on a component
  torn down with the phase, so every return to In game reopens that thread over whatever the author was reading.
  Drop `note` from the URL once its thread is open, replacing the entry rather than pushing one.
  `docs/tools/sketch.md` § In game.

- [ ] **TS135 — Letting go of a view says when the studio refused.** `LetGoView` (`SketchTool.razor.cs:486`)
  reports only an unreachable studio and never reads the DELETE's status, so a refusal leaves the view on screen
  with no word. Show the refusal's `message` in the views error the phase already renders.
  `docs/tools/sketch.md` § In game.

- [ ] **TS136 — In game's change count follows the board while it is open.** The changes are read on entering
  the phase (`SketchTool.razor.cs:181`) and not again, so a drive that lands while In game is open leaves every
  thread's "changes since" stale until the author leaves and comes back. Read them again when the board's
  revision moves while the phase is up. `docs/tools/sketch.md` § In game.

- [ ] **TS137 — A thread can be marked won't-do and retagged in the browser.** The thread offers Resolve and
  Reopen only (`SketchInGamePhase.razor:137`), and a tag is chosen when a note is written and never after
  (`SketchNotesColumn.razor:128`). `PATCH /map/{slug}/notes/{id}` already takes `wont-do` and a tag
  (`NoteChangeRequest`, `Contracts/MapNoteDtos.cs:125`) and the client already calls it for the two statuses it
  offers (`SketchInGamePhase.razor.cs:348`); add won't-do and a tag picker to the thread. `docs/tools/sketch.md`
  § In game.

- [ ] **TS138 — A reply can ask the studio for its "after" picture.** Answering a note means fetching
  `render/eye` with the note's own camera, posting the PNG to `POST /notes/pictures` and naming the hash as the
  reply's `picture`: three calls every agent writes by hand (the `pgm-board` skill, §2). Let a reply state that
  its picture is the note's camera, and have the studio draw it at the change the reply is written at.
  `docs/tools/sketch.md` § Answering the notes an author left.

- [ ] **TS139 — A browser test for In game and its notes.** `tests/e2e` covers History (`sketch-history.mjs`)
  and Report (`sketch-report.mjs`) and nothing of In game: no spec writes a note, replies, resolves one, opens a
  thread from `?note=`, or follows "changes since" into History. Add one spec covering those five to
  `./tools/e2e.sh all`; it is the test that would have caught `TS133` and `TS134`. `docs/tools/sketch.md` § In
  game.

## User Experience

- [ ] **TS121 — A lasso over the void carries coordinates.** `EyeScene.Project` counts a ray that hits nothing
  as sky and adds no column, so a note drawn over void has no `columns` and its place is read off the
  picture's pixels. Where a ray points down, meet it with a level plane — the median ground of the columns the
  same mark did hit, else the board's — and carry those cells as their own set beside `columns`, which keeps
  meaning ground actually seen; a ray pointing up stays sky. *Evidence: Gypsum Reach note 74, a lasso over the
  void beside the frontline.* `docs/tools/sketch.md` § Notes; `docs/world-scan/read-backs.md`.

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** *Parked (author): imports
  are not a priority.* When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)

## Refactoring and cleanup

- [ ] **TL37 — The library is seeded from one place.** What a fresh studio holds is stated in four kinds of
  place: C# preset classes (`StylePresets`, `ThemePresets`, `HousePresets.All`), literals inside the seeder itself
  (the boulder recipes, `LibrarySeed.cs:69`; a tree per `DressingPalette.Species`), 49 embedded JSON files
  (`Minecraft/Houses/Kept`), and a tool run by hand at deploy that copies trees out of a world in the authoring
  repository (`tools/seed-trees.cs` over `corpus/tree-showcase`). Inspect each and move every seeded row —
  materials, themes, houses and their parts, tree and boulder recipes, the copied trees, the biome patterns — into
  one fixture folder of data read by one seeder, with one test that the seeded library equals the folder. A
  preset that is code because it is computed stays code and is named as such. `docs/tools/library.md` § the seed.

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

## Opening the studio to other people: sign-in, a server, and what a caller may ask for

The access rules, the Discord sign-in, tokens for callers without a browser and the read-only client are in
place (`docs/access.md`), and the studio runs at pgmstudio.de (`docs/deployment.md`). What remains is the
server's backups and the edits a read-only page still lets start.

- [ ] **RP81 — The edits a read-only page still lets start.** Three reach past the shells that grey a page
  (`docs/client/ui-conventions.md`): a `Sidebar`'s own inputs (the sketch layer's rename and base height), the
  select tool's drag on the canvas, which moves a shape until the save is refused, and a `FlowBar` whose next
  step writes (the sketch's Finish). Pass the cascaded `StudioReadOnly` into the canvas controllers as a flag
  that refuses a drag, and let `FlowBar` disable a writing step. The server refuses all three today.

- [ ] **RP84 — The server's backups leave the machine, and a failed deploy says so.** Every dump in
  `/var/backups/pgm-studio` is taken before a deploy and sits on the disk the database is on, and nothing takes
  one nightly. A nightly timer beside `pgm-studio-deploy.timer` dumps the database and
  `/var/lib/pgm-studio` (the data-protection keys and the texture cache) to a Hetzner Storage Box over SFTP,
  keeping a week of nightlies and a month of weeklies. The same pass gives `autodeploy.sh` somewhere to say a
  deploy failed — a GitHub issue opened with a fine-grained token scoped to issues, or mail — since today a
  failure is only in the journal and `/var/lib/pgm-studio-deploy/failed`. `docs/deployment.md` *Limits*.

## The remainder: work no concept above has claimed

- [ ] **G262 — The seed corpus states iron the placement rules no longer seat.** Measured across
  `tools/seeds`: 12 of 14 spawn-room cubes resolve unplaceable, on five seeds, because a cube and a walled
  room need `6 + 2 + 3` = 11 blocks on one axis and those spawn pieces are 10×10, 15×15 and 20×10. Nothing is
  broken by it — an unplaceable marker stamps nothing and is flagged `WX9` — so this is a data refresh, not a
  defect: re-author each spawn piece so it either has the depth for a yard or states a footprint small enough
  to open one, then re-record whatever `docs/generator/seed-stats.md` measures off them.

- [ ] **WE134 — A map's own dressing registry still takes a hand-written `copied` body.** The tree library
  refuses a `copied` save carrying no cut (`DR-COPY`), but a map's `dressing.styles` takes
  `{"kind":"tree","form":"copied","body":…}` as written and nothing asks where the body came from.
  *Blocking question (author): does "hand-built props are not wanted" reach a map's own registry — refuse an
  inline `copied` body unless it names a library tree, which carries the cut — or only the library?*
  `docs/world-export/decoration.md`.

  *Evidence: 11 build specs in `pgm-studio-mapgen/specs` (`opus5-flintwick`'s `library_tree`, the
  `opus5b`/`opus5c` commons) copy a library tree's body inline, which a cut-carrying rule would still allow;
  the hand-written bodies among the specs' 37 inline bodies are the ones it would refuse.*

- [ ] **PG18 — The region draft bucket has no client left.** `POST /regions` and its `/orbit` follow-up take
  `draft_step`, `RegionDrafts` writes it into the `region_drafts_json` artifact and `/regions/tree` echoes
  it back, so a freshly drawn region could be shown as drawn-but-unwired. The Edit tool was the only thing
  that drew one, and it is gone. Retire the field on both request DTOs (`EditRequests.cs`), `RegionNode`'s
  echo (`RegionTreeDtos.cs`), `RegionDrafts` and the artifact, and `docs/pgm/region-data-flow.md` §5 with them.

- [~] **WS74 — Traversability reads 169 of 911 corpus maps as not connected.** *Parked by the author: more
corpus reading is not worth its cost, and `--goldens` holds these 169 as the baseline any change is measured
against.* These are played maps, so most verdicts are still the reading's. By first cause: 81 separate ground, 41 a point with no ground near,
27 a team barred by an `enter` rule, 20 a sealed room. Known and left: `abstract` stacks six spawns per team
one above another behind spawn filters, `curly_wools_ix` asks defenders to pillar up to their wool,
`black_betty` has a broken void setup, the cannon maps fight across void, and `citadel` is attack/defend,
where only the attackers need the wool. Take the rest per cause, reading each against PGM.
`docs/world-scan/read-backs.md`.

  *Evidence: `ki` states `<apply block-place="deny(void)" region="bases">` "You may not bridge to the enemy
  side!"; `outcast`'s halves are joined only by water at y ≤ 1.*

- [ ] **PG19 — The `resize` region is refused.** PGM grows or shrinks a child region by a vector
(`<resize min=… max=…>`); the parser does not read it, and `MapParser.EnsureSupported` refuses the five corpus
maps using it rather than read a rule over it as covering the whole board. Read it as the child's footprint
grown by the x and z of `min`/`max`, in `RegionParser` and `RegionGeometry2d`, and take it off the refused
list. `docs/pgm/supported-maps.md`.

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
