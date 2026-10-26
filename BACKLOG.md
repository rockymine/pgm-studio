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

- [ ] **TC23 — A world uploaded with its `map.xml`.** *Parked (author): skipped for the time being.* `POST /map/upload` takes a zip of a world's `region/` and
  its `map.xml` from any member, refused over a size cap, scanned as `import-url` scans; the stamps run over it
  on export. The `map.xml` is read into an intent, on the author's call: teams, spawns, wools, monuments, cores,
  control points and build regions become intent pieces, configured in Configure as a world read from a scan is
  (D13), and what the studio cannot state is refused by name (`docs/tools/configure.md`, `docs/tools/flow.md`).

- [ ] **TS190 — A place states the numbers it is held to.** *Parked (author): agent-facing, and the rule measures already band a distance.* A place carries targets — a value and the band it
  must fall in — and each is measured off the sketch's ground and shown beside its band, a miss as a complaint,
  as pgmvox's checker prints one line per number (`freeform/lib/AGENT-GUIDE.md` § the plan). The walks the
  sketch already measures (`BoardWalks`, `PlanNav.Of`) carry the first kinds: a team's walk from its spawns to a
  place, and which team arrives first. Lands on `PlacesDocument`'s `Place` (`Contracts/PlacesDtos.cs`) and
  `PlacesCheck`. **Open, the author's:** which kinds of target a place may state beyond walks and arrivals —
  a width or a height reads the place's shape against the ground. `docs/tools/sketch.md`.

- [ ] **TS191 — `sight`: who can see whom on the sketch's ground.** *Parked (author): agent-facing; a target is likely an objective.* A line between an eye 1.62 blocks over the
  feet and a target's chest 0.9 over its feet is clear when every block it passes, sampled each quarter block,
  is above the ground the sketch builds and outside its houses; answered as each target's share of eyes that
  see it, and the targets none sees, over HTTP with a `?format=text` twin (pgmvox `read/sight.py`). **Open, the
  author's:** what the eyes and targets are — fixed points (spawns, goals, places) or every cell a team stands
  on. `docs/tools/sketch.md`.

- [ ] **TS201 — The canvas keeps its width across Draw's steps.** *Parked (author): no layout decided yet.* The
  Places step's inspector is wider than Ground's and Layout's, so stepping into it narrows the canvas and moves
  the board under the pointer. `docs/tools/sketch.md`.

- [ ] **TS202 — Ground and Layout as one step.** *Parked (author): needs a redesigned dock first.* The two were
  split so the dock would not hold every tool at once; one step means the dock carries the shape tools and the
  piece families together. `docs/tools/sketch.md`.

- [ ] **WE168 — A room's interior is placed relative to its house.** *Parked (author): later; derived for now.*
  The chests (`WoolChests`, `RoomFrames.InteriorCorners`) and the monuments (`RoomFrames.MonumentSlots`) are
  stated on the room's `house` as positions relative to its first wing, movable in Layout, and a migration
  carries each derived position into the stored one. `docs/world-export/structures.md`.

- [ ] **TS172 — The look is stated before the review.** *Parked (author): too much for now; the programme goes on
  without it.* A palette, a house style per area, tree and rock zones and
  the biome as sketch data, drawn on the review sheet and applied by the build, so the author judges what a board
  is made of before it is built (D12, phase 5). `docs/tools/sketch.md`.

  *Evidence: four of the seven points of Abbeymoor's first review were paint, materials or dressing, and no picture
  before its build showed any of them (`experiment/REVIEW-1.md`).*

- [ ] **TS130 — Point edits and bends reach every outline.** *Parked (author): tangled with how land is made,
  which the pgmvox port's Phase 2 redesigns; picked up when that work needs it.* `editShapes` and
  `bendShapes` address a shape's `vertices` alone (`SketchGeometryEdit.Outline`), where `outlines` already
  writes a stroke's, fluid's or flora's `points`
  (`Refinement.Outlined`). Both reach those, each refused where an edit folds the ring.
  `docs/tools/flow.md`.

- [ ] **C104 — Where a sketch's layers live.** *Parked (author): deciding the direction themselves.* The
  floating `SketchLayerStrip` is taller than the dock, scrolls sideways and clips its add button on canvases
  under about 820px, and the overlay chips (Shapes, Mirror, Chunks, Blocks, Contours) crowd the canvas beside it.
  Three directions and a View menu for the overlays are mocked at https://claude.ai/artifact/4gDNRBfyhMfjkAMMFdDSaz;
  none is chosen. `docs/tools/sketch.md` § Draw.

- [ ] **TS126 — A note's checkable claim, kept as a check.** *Parked (author): not yet.* Some notes state
  what a board must keep being — a path reaches the bridge (note 58), a bedrock wall has void on both sides
  (note 48), a wall does not float (note 54). A reply could carry the claim as a measured check over the note's
  anchor, kept when the author resolves the note, and raised as a finding naming the note when a later pass
  breaks it. `docs/tools/sketch.md` § Review.

## The configure wizard: a map built from what an author states it is

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/pgm/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`).

- [ ] **RP121 — A wool room the world builds is held to the room cap too.** `WX13` asks only a spawn room
  (`WorldBuilder.OversizedRoom`, one call at `WorldBuilder.cs:232`), so a wool room whose protection Configure
  redrew past 20 by 20 blocks builds with no finding, while `ST9` asks only of a building the intent states
  (`IntentCheck`). The wool room loop calls the same check, and `WX13`'s text names wool rooms beside spawn
  rooms. `docs/refusals.md`.

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
  it rather than asking. The anchors are derived when a plan is compiled (`FEATURES.md`), so the step asks for
  a count on a board whose intent states none and shows the anchors the compile already worked out on one that
  does.

- [ ] **TC9 — The configure tool cannot place a shop.** The API is the way in — an agent adds `shops` to the
  intent it already posts (`docs/pgm/shops.md` §9) — and the wizard has no step for it. What the step states
  is the menu: a name, a currency, and a list of material/amount/price rows. Where the keepers stand is
  derived unless one names a place of its own (`docs/pgm/shops.md` §9), so the step offers the place and
  fills nothing in where the board says nothing.

- [ ] **TC14 — The wizard drops the `warnings` its intent save answers.** `ConfigureTool.SaveIfDirtyAsync`
  reads only the status of `PUT /map/{slug}/intent`, so an `RQ3` naming a field the server kept nowhere reads
  as Saved. Show the complaint beside the editor bar's save state, naming the field path. Evidence: the Room
  step wrote `wools[].room` while the intent reads `protection`, and every room drawn there was dropped
  under a green Saved.

- [ ] **TC16 — The Build phase offers the build mode.** `BuildIntent.Mode` (`void`, `never`, `only`, from
  `BuildModes`) and `Materials` are stated on the intent and nowhere in the wizard: a select beside Build
  height showing the default `BuildIntent.ModeFor` takes, and a material list where the mode is `only`.
  `docs/tools/configure.md` §*Build*.
- [ ] **PG29 — A kit loses nothing it states, or the map is refused.** `MapParser.ParseKits` reads items,
  armour, effects, `max-health`, `force` and `clear`, and drops every other kit element in silence, so an exported map plays
  differently from the one read: `game-mode` in 325 corpus maps, `action` in 112, `double-jump` in 68, `health`
  in 56, `knockback-reduction` in 48, `walk-speed` in 40. Each is read and written
  (`Kit`, `XmlWriter`, `Serializer`, the `kit` row), or its presence refuses the map the way an unread objective
  module does. `docs/pgm/supported-maps.md`.
- [ ] **PG24 — Jump pads.** `MapIntent.JumpPads`: a box, a velocity turned with the symmetry, a pad block and
  `safe`. `JumpPadGenerator` writes `<apply region velocity>`, which already round-trips
  (`XmlWriter.cs:879`); a safe pad adds pgmvox's fall variable and fall-damage deny (`play/rules.py:477`). The
  launch is checked against its landing with pgmvox's `solve_launch` (`core/move.py`), ported beside
  `PgmStudio.Geom.Jump`. 215 corpus maps write `velocity=`. `docs/pgm/new-map-authoring.md`.
- [ ] **G292 — A plan states a zone of rules.** `PlanZone` states build regions and water lanes; a third kind,
  with `ZoneIntent`'s rules (`enter`, `place`, `break`, `harm`, `kit`), compiles into `MapIntent.Zones`
  (`PlanCompiler.cs:413`). Blocked on the layout law: BZ5 forbids every zone touching a spawn piece and the BZ
  rules read every zone a plan states (`PlanValidator.cs:686`), so a rule zone needs its own place in
  `docs/generator/rules.md`, amended by its correction protocol. `docs/pgm/zones.md` §3.
- [ ] **PG31 — A spawn keeps every region it states.** `Spawn.Region` holds one region, so a spawn written
  with several `<region>` children keeps the first and loses the rest on read — 83 spawns in 34 corpus maps
  (`skill_issue`'s defenders spawn at either end). A portal naming a lost one exports a reference to nothing.
  `Spawn.Regions`, each with its own yaw, read and written by `MapParser`/`XmlWriter`, stored and served.
  `docs/pgm/portals.md`.

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

### The finished sketch's scan

- [ ] **TS178 — A finished sketch's scan follows the intent's rooms.** The scan is redrawn when the layout's
  revision moves past the one it was drawn from (`SketchFinish.RefreshAsync`, `WorldFeatureWriter.SketchScanRevision`),
  but a room holds the ground under it and lives in the intent, so a move through the piece route alone leaves the
  scan describing the room where it stood. Key the scan on both revisions. `docs/tools/sketch.md`.

### Water

- [ ] **S46 — Water reads the ground; a river on the axis is a canal.** A dressing path draping over whatever
  it crosses is **settled as correct** — it repaints the top block of each column and adds no cell, which is
  what lets a road cross a slope without becoming a ramp, and routing or grading it would be the tool
  deciding where the author's road goes. Terrain that a route *emits* is the draw phase's path primitive
  (`FEATURES.md`) and the erected-shape modes, not this. Water is the half that is genuinely wrong on
  ground with relief, because it has to obey the ground rather than sit on it. It needs three things the flat model never
  did: routing on a **depression-filled** copy, because steepest descent stops at the first grain-made pit after 2 cells
  where the filled run covers 65; a bed floor forced non-increasing downstream; and **per-pool** water levels
  replacing `decoration.md` §7's single lowest-surface line — the measured run holds 14 distinct levels, and a
  basin is an outlet alongside the map edge, which is what a pond is. The exception is the case that matters
  most: **a river on the mirror axis cannot both fall and be fair**, because a half-turn reverses the flow, so
  on the axis it is a canal at one level and falling water belongs to the flanks
  (`world-export/decoration.md` §7.1).

### A made thing is a third kind, and it is drawn out of layers

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
  `sequoia-1` alike, and so every entry of the seed folder's `trees.json`. A copied tree
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
  iron cube the room seats beside the door (`ResolvedRoom.Iron`, `WX8`). The field is `NOT NULL DEFAULT 2`
  (`M0027`), and 11 of the deployed 13 room styles hold 2, so read as stated every room door narrows: it has to
  become nullable with a migration, and whether a stored 2 is a statement is the author's. *Evidence: Sootcombe
  note 10 asks for a 2-wide wool-room door.* `docs/world-export/structures.md` §7.

### Shapes

- [ ] **TS156 — An outline can be bent into a coast from the canvas.** `POST /map/{slug}/sketch/shapes/{id}/bend`
  resamples long edges, wanders the cut points and fits Béziers, optionally only on named shore edges, keeping a
  plan's corners fixed; the sketch canvas has no control for it, so a compiled plan's rectangles are roughened by
  hand point by point. Add a *Bend* action on a selected polygon (edges and wander as two sliders, a preview, one
  undo step) in `SketchInspector`, calling the route. `docs/tools/sketch.md`. *Evidence: the route is used by
  every agent build-spec that roughens a coast; no `.razor` under `Features/Sketch` names bend or wander.*

- [ ] **TS141 — A room style stated as a library fork is honoured, or refused.** In `dressing.styles` a
  house stated as `{"library": <name>, "kind": "house", "shell": <parts>}` builds the fork (`DressingJson`
  resolves the shell over the row), but the same object under `roomStyles.spawn` stores 200, raises nothing and
  builds the bare library row. Resolve `roomStyles.<part>` through the same reader the dressing registry uses,
  or answer `RQ1` at the path; a second accepted shape that is silently dropped is the failure.
  `docs/tools/sketch.md`. *Evidence: `pgm-studio-mapgen/reports/sonnet55-halcyon-cays.md`, the kit section — the
  spawn halls came out cyan clay over pink-white clay.*

- [ ] **S59 — Per-vertex height is the headline feature and is found by accident.** The path is: select a
  polygon, read the one conditional sentence in the inspector, click a vertex on the canvas without moving it,
  then type into a field that appears in the panel. On the canvas a vertex handle looks exactly like a drag
  handle and its height is a bare text label, so nothing says a click-without-drag does something a drag does
  not. Make the height labels read as interactive (a pill or a hover state), and ideally let the label itself
  be edited or scrolled in place rather than round-tripping to the inspector. The shift-click 2–3 vertices
  slope-fit has the same problem and the same fix. The 3-D preview is where a height edit is actually legible
  and it now draws the built world (`FEATURES.md`), but it is a modal swap rather than a companion view, so it
  confirms an edit after the fact rather than while it is being made.

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
  where one ring's hole would run long. Lands in `TeamUnitAllocator.ChooseHubForm`/`ChooseHubWalls`;
  `docs/generator/model.md` and `rules.md` (`CT8` is the rule that states the envelope and measures nothing).

  *Evidence: 12 seeds a band, cell 4 — the hole run's median is 16 · 16 · 18 · 24 blocks up the ladder and
  **14 of 38 centi boards exceed 40**, none below it. The corpus of 359 CTW maps puts the run's median at 20
  and its p90 at 43 (836 voids), so the bar is the corpus p90. `opus5-threapland` is the worked case: one
  76×36 hole at cell (-10,-19), where a double-hole hub would have given two.*

- [~] **G264 — A composed wool room can stand 12 blocks across void from a piece fronting the crossing.** The
  inside corner of a bent wool is its lane's width, 12 blocks at nano, which `WL20` allows against the team's
  own ground and `WL12` refuses against the front (16). The room's setback along its lane takes a block basis of its own
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
  wall.** A new dock for `CompoundForm.Donut` in `UnitSeating.SeatOverhang`/`WoolBoxEmitter`, beside the
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

## Measuring a board the way a player plays it
The author ruled on how each distance a rule checks is read (*Rule text audit*, *Declarations and geometry*
tab, 2026-10-04). Each ruling moves which findings fire, so each lands with its `--goldens` re-recorded and
the skerry layout as the board that shows the over-warning is gone.

- [ ] **G285 — A crossing is a straight bridge from the nearest land.** A player walks the land the shortest
  way and bridges straight across the void; nobody routes around inside it. `G5` (rectangle gap, `ContactGraph.cs:312`),
  `CT12` (4-connected void path, `PieceInterfaces.cs:157`) and `WL12`, `WL20` and `LN6` (straight runs) read the same void three
  ways and over-warn: on the skerry layout a small front piece makes the check ask a diagonal route through
  the void to a wool room. One measure: walk the land, then the straightest bridge from land edge to land
  edge. `docs/generator/`.
- [ ] **G286 — A step a player cannot walk is 3 blocks.** `EL1`, `SP8`, `WL11` and `WX11` fire at 2. A 2-block step is fine where a block can be placed; 3 or more is the fault, on plan seams and above all
  out of a spawn. Lower is always better, so a soft term prefers it. Read the thresholds from
  `Walk.FreeRise`/`Walk.ScrambleStep` rather than literals. `docs/generator/`.
- [ ] **G287 — A corridor is its size band's width.** `G2` is three numbers today: the size band's text, 10
  blocks in the plan check (`ContactGraph.CorridorMin`) and 2 cells in producibility. It is the composer's band
  width, a multiple of 4: 8, 12, 16, 20, for generated wool layouts. What `G2` asks of a build region is open
  too: `LintG2` measures every entry in `zones` on its shorter side, while a region is cut against the front
  lines it lies between, so widening one moves it off them, and a mid region too thin says the front lines
  are too short (author). `MD7` already holds the mid region's width along the front lines per band, so
  settle which question `G2` asks of a region before it carries an edit. Read it beside `EZ2`
  (`Analysis/Playability/BuildZoneGap.cs`), which asks the same relation of the built ground: a coast pulled
  back from its build region leaving void under 10 blocks where the plan put ground. `docs/generator/`.
- [ ] **G288 — A front line's width is its front edge.** `FR6` counts cells and `FR9` blocks, off different
  structures. The width is the edge that faces the enemy into the build zone, not every edge touching it: a
  2 by 20 piece poking into the zone is not a 42-block front. `docs/generator/`.
- [ ] **G289 — One measure per question, in `PgmStudio.Geom`.** Rectangle overlap, touch and clearance are
  written six or seven times. One rectangle relation (apart, corner, edge, overlap, with the gap), one
  "nearer than N" over a set of cells with the edge stated (a thing exactly at the limit stands), replacing
  `GroundClaims.NearerThan`, `NearRoute` and `DressingScope`'s rect lambdas. Same numbers first, then
  G285–G288 on top. `docs/generator/`.
- [ ] **G291 — Settle the raised rules whose check is not their argument.** *Parked on the author.*
  `docs/generator/audit.md` §8 lists thirteen rules where `rules.md` argues one thing and the check does
  another; for each, the author says which is right, and the losing half changes in the same commit.
  `docs/generator/`.
- [ ] **TS167 — A path reaches a house's door.** The clearances around a building make a road or path to its
  door impossible. A path may run up to the door, paving never paints inside the house (its floor does), and
  `/sketch/seats` agrees with the pass that places buildings: today it refuses a building on paving the pass
  allows (`ClaimRaster.cs:210` against `Decorator.cs:1287`). `docs/tools/sketch.md`.

## A first-time reader: the words, the sizes, and the help a tool owes them
A reviewer new to mapmaking read the studio cold and reported what stopped them. The copy pass and the
text-size setting have shipped; what remains is what a sentence cannot fix — a term with nowhere to be looked
up, controls that behave unlike every other tool, and the look itself. `docs/client/writing-for-the-ui.md`
is the standard the copy is held to.

- [~] **RP99 — The terms defined where they appear.** `/glossary` lists `GET /api/glossary`; what remains is a
  `Term` component that underlines a word, shows its one-line definition on hover and links to its
  `/glossary#slug` entry, then the tool pages losing the explanatory paragraphs that stand in for help today. The
  guide entries in `docs/client/writing-for-the-ui.md` join the glossary as a longer field when a page needs
  them. `docs/client/`.

- [ ] **C83 — Clicking the zoom readout resets the view.** The zoom percentage in `CanvasReadout` is
  pointer-transparent; a reader expects clicking it to return to 100% or fit. Make the zoom item a button
  (the rest of the readout stays transparent) that calls each canvas's existing fit command, in the sketch
  and configure canvases. `docs/client/canvas-interaction.md`.

- [ ] **TS150 — Zoom in the sketch's 3-D view.** Reported: in a map's Draw phase with 3-D on, the wheel moves
  the zoom readout and the scale bar but the picture stays still. Reproduce in the iso view
  (`sketch-canvas.js`, the lazily loaded `iso-webgl`) first; the readout and the picture must answer the same
  zoom. `docs/tools/sketch.md`.

- [~] **TS151 — The notes overview and the change history, laid out for the work.** Review's notes overview heads
  its lists with four filter chips plus three grouped lists; put the filters into tabs (*This view · Whole map ·
  All*) with a status select. History: up to 60 edit lines sit above *Restore*, so pin it in the inspector's footer
  and show the three column counts as one row. `docs/tools/sketch.md` § Review, § History.
- [ ] **TS164 — Decoration's inspector widens only for its picker.** *Parked (author): judging from the
  screenshots in the layout mockups' section 6.* `.workspace-inspector--wide` holds the
  Decoration inspector at 420px (`editor.css:295`) against 280px everywhere else, so the canvas jumps 140px on
  entering the phase while the wide column shows an empty state. Widen it only while a block or pattern picker
  is open. `docs/tools/sketch.md`.

- [ ] **C69 — A proportional UI font.** *Parked (author): waits on the design direction.* Every page is set
  in `ui-monospace` (`.editor-page`, `editor.css`), which reads as a terminal and is wider per word at the same
  size. The alternative is a system sans for interface text with monospace kept for data: slugs, coordinates,
  JSON, block counts. `docs/client/ui-conventions.md`.

- [ ] **C70 — Which visual direction the studio takes.** *Parked (author): pick a mock-up.* Six directions
  were mocked as artifacts (a cleaned-up version of today's look, a game-flavoured one, a dense pro-tool one,
  *Paper*, the whitepaper's figure style: white ground, one ink, one accent, the four plan inks; *PGM*,
  pgm.dev's look: its orange-red, the orange navbar bar, documentation layouts, light and dark; and
  *Overcast*, oc.tc's look: maroon and cream, Lexend, slanted pictures, ribbon panels, and a map page laid
  out like a player profile), each covering the landing page with map pictures and the map list with
  authors, beside the sketch's In-game and change-history panels. The choice decides `tokens.css` and the
  landing and list layouts; the components stay. `docs/client/ui-conventions.md`.

## User Experience

**What the UX review of a recorded build found open.** Each entry was read off the recording and checked against
the client source.

- [ ] **TS218 — A paint rule says how much ground it takes.** Palette's rule cards show no result: nothing tells one
  rule's columns from the ground before it, or says an earlier rule shadows it. Answer a count per rule from the
  paint read (`sketch/paint`) and show it in each card's header — *claims 1,240 columns*, or *claims none: an earlier
  rule wins* in the warning colour — and tint that rule's columns while its card is hovered. `docs/tools/sketch.md`.

- [ ] **TS219 — A paint rule card folds to one line.** Five rules take about 1,700 px; unset conditions weigh as much
  as set ones, an empty field looks like 0, and nothing says every condition must hold. Fold a card to a sentence
  built from what it sets (*Snow Block · slope ≤ 34° · y 26–28 · in patches*), offer *+ Add condition* for the rest,
  say "all conditions must hold" once, and state Bare share in percent like Paved share. `SketchPaintRules`.
  `docs/tools/sketch.md`.

- [ ] **TS220 — Every modifier row says its type, and the list scrolls to its pick.** Terraform's list names a type
  only on renamed rows, and lumps, roughness and warp share one wavy glyph. From about seventeen rows the picked one
  is cut off. Show a muted type word on every row, give the list its own scroll region, and scroll the pick into
  view as the Placed list does. `SketchModifierList`. `docs/tools/sketch.md`.

- [ ] **TS221 — Deleting a modifier is not beside Close, and says how to undo.** The inspector header puts Delete
  32 px from Close with no confirmation; undo exists but only as the top bar's arrows and Ctrl+Z. Move Delete to the
  inspector's foot and show *ridge-2 deleted · Undo* after it. `SketchModifierInspector`. `docs/tools/sketch.md`.

- [ ] **TS222 — The tree picker searches.** About 94 cards in library order with no search, height or family;
  selected and hover share one border (`design.css` `.prop-card--active`). Add a search box and Species and Size chips
  over the grid, a height under each generated card, and a check mark on the selected one.
  `SketchDressingInspector`. `docs/tools/sketch.md`.

- [ ] **TS223 — A block is picked by name, not typed as id:data.** Paint rules and stroke paving take numeric id and
  data (`3:1`, `60:7`) and truncate names in the 280 px Palette column. Reuse the searchable `BlockPicker` with id:data
  as a caption, and widen Palette's inspector to Decoration's. `docs/tools/sketch.md`.

- [ ] **TS224 — Ground cover shows its density before a field is drawn.** The inspector draws no picture of a
  ground cover, because the plan view the pass rendered read as a flat green square, and four plants (cacti, dead
  bushes, mushrooms, lily pads) offer sliders on boards with no ground they grow on. Draw a density swatch from the
  sliders that reads at a glance, and show the plants the board can grow first. `docs/tools/sketch.md`.

- [ ] **TS225 — Review and the map.xml are reached by name from where a map is finished.** Decoration's way out is
  an icon-only rail glyph and an icon-only Download; the XML is read only in Configure. Add labelled *Preview as
  player* and *View map.xml* beside Download, and a legend under the eye picture naming monuments (which float by
  design), cores and the build area. `docs/tools/sketch.md`.

- [ ] **TS228 — Each modifier parameter shows what it does, as a short animation.** A `?` on a modifier field
  explains a parameter in words; a Ridge's *Wander* or a Butte's *Apron* reads better as the ground moving while
  the number sweeps. Render one looping vector animation per parameter from the build itself — the section
  through a sample patch at a few values — and open it in the field's help. `SketchModifierInspector`,
  `docs/tools/sketch.md`.

- [ ] **C107 — Drawing the land shows its fill and its mirror while it is drawn.** A lasso trails 8 px squares with
  no fill, the mirrored half appears only once the ring closes, and the close target is a 7 px square. Draw a
  translucent fill and the symmetry copy at 40% while tracing, and a 14 px close ring that grows on hover.
  `docs/client/canvas-interaction.md`.

- [ ] **TG5 — Player bands are counted and combined.** The Generator's band chips (`SizeBands`) carry no counts
  and only one can be on, because `GET /api/compose` serves one band per request and its feed returns no
  per-band totals. Accept several bands and answer a count per band, then give the chips counts and multi-select
  like the shape facets. `docs/tools/generator.md`.

- [ ] **TL43 — Textured cards of one kind share a scale.** `StructureCard.Once` frames each tree, boulder and
  house part to its own bounding sphere, so a shrub and an oak fill their cards equally. Bound a library kind's
  set once and draw every card of it at that scale. `docs/tools/library.md`.

- [ ] **TL44 — The Dressing pickers draw with block textures.** `SpeciesCards` and `BoulderFormCards` in the
  sketch's Dressing phase still draw the flat SVG side view while the Library's cards of the same props are
  textured (`StructurePicture`). Route them through `StructureCard.Once`. `docs/tools/library.md`.

- [ ] **TL45 — A house card is drawn on the footprint its editor chose.** `RoomStylePreview.CardOnce` renders the
  default 8×8 sample footprint from a fixed front camera, not the footprint the house editor's dock states.
  `docs/tools/library.md`.

- [ ] **C89 — A map can be deleted from `/maps`.** `DELETE /api/map/{slug}` ends a map and everything under it
  (`docs/tools/flow.md`), and the maps list has no control for it, so abandoned drafts and variants pile up.
  Add a delete action on a row for its owner and admins, behind a confirm naming the map, greyed with the reason
  for anyone else (the `WriteGate` pattern). `docs/client/routing-and-ia.md`.

- [ ] **RP105 — An admin can issue a token that acts as another member.** `POST /api/users/{uuid}/tokens`
  exists and `Tokens.razor` only issues the caller's own (`/users/me/tokens`). Add the member picker to the
  admin's Tokens page. `docs/access.md`.

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

## The tool hosts and their bridges: one shape for save, feed, selection and the verb table

- [ ] **TS162 — Undo takes back only what the phase in view edited.** *The author's ruling:* an undo never
  removes work done in another phase. The sketch keeps one stack of whole-document snapshots across every
  phase (`history` in `sketch-bridge.js`), so the topbar Undo in Theme takes back the last shape drawn in Draw.
  Tag each step with the phase that made it and restore only that phase's slice of the document — Draw the
  shapes, Theme the registry and each shape's theme, Dressing `dressing`, Terraform `modifiers`, Info `setup` — so
  Undo in a phase walks back that phase's own steps and is greyed where it has none; `Ctrl`+`Z` follows the
  same rule. *Open (author): whether Info's mode needs an undo at all.* `docs/tools/sketch.md`.

## Refactoring and cleanup

- [ ] **RP129 — A read of a world in memory does not encode it as NBT first.** Seven renderers take
  `IEnumerable<AnvilRegion.Chunk>` so one path reads both a `.mca` file and a built world, and the built one
  arrives through `AnvilRegion.FromWorld`, which runs `AnvilRegionWriter.BuildLevel` over every chunk on every
  call: `TopDownRender:130`, `SectionRender:81`, `SectionText:64`, `SurfaceReport:80` and `:98`,
  `StructureFinder:124`, `HeightProfileRender:65`, `MirrorReport:51`, and `WorldReads.Reach`
  (`WorldReadEndpoints:123`). Give the renderers one section reader both sources answer — the file's decoded
  sections, or `VoxelWorld`'s own — so a built world is read where it stands; measure a board's `report` before
  and after. `docs/world-scan/read-backs.md`.

- [ ] **RP130 — A scale word is read once.** Outside `WorldRenderEndpoint.ScaleRange`, three routes clamp their
  own `scale` by hand: `render/walk` 1–8 default 2 (`WorldReadEndpoints:1134`), the change diff 1–16 default 4
  (`MapChangeEndpoints:144`) and the terrain preview (`TerrainPreviewEndpoints:124`). Give them the same range
  reader, so the word is declared and clamped in one place. `docs/world-scan/read-backs.md`.

- [ ] **RP107 — The edit-route count is stated from the code.** `MapEdit.cs`, `EditException.cs` and
  `docs/architecture.md` say "thirty-six edit routes"; there are 20 `MapEdit.RunAsync` call sites. State the
  number nowhere, or derive it. `docs/architecture.md`.

- [ ] **TN16 — The prose calls a wool room a wool cage and a spawn room a spawn cube.** The vocabulary already
  says `woolRoom` (`StructuralRoles.WoolRoom`), and a board binds a shell for a **wool** room and a **spawn**
  room (`roomStyles.wool`, `roomStyles.spawn`), yet docstrings in `WorldBuilder`, `SketchMaterialGate`,
  `PieceDoors`, `WorldProvenance`, `MonumentStamper` and `StructureRenewables` still call the structures a wool
  cage and a spawn cube, which is the same word under a different hat. Rename them in one pass.

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
  Check `BoxEdgeInterface`/`EdgeInterval` in the same pass: those name a genuine full edge
  and its sub-intervals, so they are correct and should stay, which is exactly why the deriver's misuse
  is worth removing rather than tolerating.

## Opening the studio to other people: sign-in, a server, and what a caller may ask for

The access rules, the Discord sign-in, tokens for callers without a browser and the read-only client are in
place (`docs/access.md`), and the studio runs at pgmstudio.de (`docs/deployment.md`). What remains is the
server's backups.

- [ ] **RP84 — The server's backups leave the machine, and a failed deploy says so.** Every dump in
  `/var/backups/pgm-studio` is taken before a deploy and sits on the disk the database is on, and nothing takes
  one nightly. A nightly timer beside `pgm-studio-deploy.timer` dumps the database and
  `/var/lib/pgm-studio` (the data-protection keys and the texture cache) to a Hetzner Storage Box over SFTP,
  keeping a week of nightlies and a month of weeklies. The same pass gives `autodeploy.sh` somewhere to say a
  deploy failed — a GitHub issue opened with a fine-grained token scoped to issues, or mail — since today a
  failure is only in the journal and `/var/lib/pgm-studio-deploy/failed`. `docs/deployment.md` *Limits*.

- [ ] **RP132 — A session that ends under an open page says "sign in to save".** The client asks `api/me`
  once a page load (`StudioAccess.MeAsync` caches it), so a cookie that lapses or is cleared while a tool is
  open leaves the page writable, and the next write comes back `401 RQ7`, which each tool shows as its own
  error. A client `DelegatingHandler` that sees `RQ7` on any request calls `StudioAccess.Forget()` and has the
  shell re-ask, so every `Writes` control closes with the sign-in reason and the bar offers
  `StudioAccess.SignInHref` back to the same page; the refused write says the session ended rather than
  that saving failed. `docs/access.md`.

  *Evidence: the author hit a sign-in error on pgmstudio.de while signed in, gone after a reload — a stale
  cached caller over a session the server no longer had.*

## The remainder: work no concept above has claimed

- [ ] **TE2 — Write `docs/tools/edit.md`.** The `TE` prefix obliges a document that does not exist. The region,
  filter and apply-rule editors (`RegionEditor`, `FilterEditor`, `ApplyRuleEditor` under `Pgm/Editing/`) and
  their two rules (`ED1`, `ED2`) are described nowhere, and the filter and apply-rule editors have no HTTP
  route, only the generators reach them. The document follows the seven in `docs/tools/`.

- [ ] **WE162 — Two seeded houses stamp their doorway on other columns in a mirror image.** The stamper's
  orbit tests (`A_room_and_its_rot_180_image_stand_on_the_same_columns`,
  `A_room_and_its_mirror_images_stand_on_the_same_columns`, `HouseStamperTests`) run over the eight houses they
  were written for and pass; run over every seeded house, `dark-oak-stilt-hut` (hip roof, stilts) and
  `jungle-saltbox-cottage` (saltbox) fail, their door columns landing apart between a room and its image. Find
  which of the two forms moves the doorway, fix it in `HouseStamper`, and widen `RoomStyles()` to
  `SeedFolder.Houses`. `docs/world-export/structures.md`. *Evidence: `dark-oak-stilt-hut 18x9 door -z mirror_x at
  (-9, 65, -73)`, `jungle-saltbox-cottage 14x13 door -x at (-6, 76, -78)`.*

- [ ] **RP97 — The sketch layout's words are published sets.** `GET /api/kit.py` checks a word only where
  the schema lists it, and 44 of 574 string fields do: the layout an author writes most has none, so
  `SketchShape(relief_scope="hld")` is built and sent. Mark `[WordSet]` on `SketchShape.operation`,
  `relief_scope`, `stroke_edge`, `SketchLayer.kind` and
  `PlanGlobals.symmetry`, declaring each set in `Vocabulary` where it is not already; and publish `minimum`/
  `maximum` for the 0–1 shares (`FloraSpec`, `RoofStyle.Wear`) so the kit refuses `coverage=5`.
  `docs/architecture.md`. *Evidence: a constructor sweep against schema `a095aa2c71059f2c`.*

- [ ] **RP98 — A stated null is a value the store refuses, not one the report trips on.** `kit.CellMaterial(
  rise=None)` writes `"rise": null`; the store answers 200 and `GET /map/{slug}/report` then answers `400 RQ1
  Cannot get the value of a token type 'Null' as a number @ rim.material.rise`. Refuse the null where the
  document is stored (the material reader), and have the kit treat an argument of `None` as unstated.
  `docs/architecture.md`. *Evidence: `pgm-studio-mapgen/reports/sonnet55-pippin-coomb.md`, the kit section.*

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
  See `docs/project-structure.md` §7.1.
