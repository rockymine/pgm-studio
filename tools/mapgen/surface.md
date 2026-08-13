# What a map is made of, and where each part is made

The reference `tools/mapgen` should have been written against. A map is not one document — it is a short
stack of them, each owned by a different project, each with its own JSON shape, its own endpoint and its own
generator. Reading the stack is what stops a tool from inventing a flatter format of its own and losing the
system's reach in the process (`review.md` MG29).

Four layers, in the order a map moves through them: a **plan** says where things go in cells, a **layout**
says what ground exists and what it is made of, an **intent** says what the map is played for, and the
**world plus `map.xml`** is what a server loads. Nothing skips a layer, and each is stored separately, so a
map can stand at any of `plan` / `sketch` / `configure` / `edit` (`MapStage`) and carry the layers below it.

## The documents

Each is real JSON with a C# type that owns its shape. The type is the specification; the endpoint is how the
editor reaches it.

| Document | Type | Lives in | Read / written at |
|---|---|---|---|
| plan | `PlanModel` | `Pgm/Plan/PlanModel.cs` | `GET /map/{slug}/plan`, `POST /plan/compile`, `/plans` store |
| sketch layout | `SketchLayout` | `Pgm/Sketch/SketchLayout.cs` | `GET·PUT /map/{slug}/sketch` |
| relief | `SketchReliefJson` | `Pgm/Sketch/SketchRelief.cs` | inside the layout under `relief`, keyed by island id |
| themes | `TerrainTheme` | `Minecraft/TerrainTheme.cs` | inside the layout under `themes`; library at `/themes` |
| dressing | `DressingDoc` | `Minecraft/Dressing/DressingJson.cs` | inside the layout under `dressing` |
| room styles | `HouseStyle` | `Minecraft/HouseStyle.cs` | inside the layout under `roomStyles`; library at `/room-styles` |
| intent | `MapIntent` | `Pgm/Authoring/MapIntent.cs` | `GET·PUT /map/{slug}/intent` |
| map.xml | `MapXml` | `Domain/MapModel.cs` | written by `XmlWriter`, parsed by `MapParser` |

The **plan** is the smallest and the most semantic: pieces with rects and roles, and `placements` holding
spawns, wools, iron, destroyables and cores as piece-relative half-cell offsets, authored for **team 0 only**
and fanned by symmetry at compile. Beside them it carries `walls` — a defence wall named by the two pieces
whose interface it stands on — and `globals` with the cell size, the symmetry and the player count. It is the
right surface for an agent because it is small and because the validator and evaluator answer it with
rule-ids.

The **layout** is the ground: `shapes` (rectangle, circle, polygon, path) with set-algebra operations,
grouped into `islands` that decide what mirrors. A shape carries far more than a footprint — its own `theme`,
its `floor` and `base_height`, per-vertex `anchor_heights`, a `height_mode` of `level`/`raise`/`sink` with a
`skirt`, and a `relief_scope` of `hold`/`exclude` deciding whether its ground joins the island's solved
relief. The **relief** rides beside the shapes rather than inside them, keyed by island id, because a plan
recompile replaces every shape it produced and a relief is hand work a plan cannot express.

The set algebra is where **void** comes from, and void is the instrument `review.md` names as the primary
control on flow for a capture board — so it is worth stating outright rather than leaving inside the phrase
"set-algebra operations". A shape's `operation` is `add` unless it says otherwise; a shape carrying
`subtract` takes its footprint's columns out of the ground entirely, which is a hole to the void rather than
a dip in the surface. That is the difference from relief, and it is the whole of it: **relief moves a
surface, a subtract removes it**, so no mark of any kind cuts a channel and no channel is ever a relief
question. A channel twenty blocks across in front of an objective is one subtract rectangle or polygon, drawn
at the width the gap wants and standing tall enough to take the whole column — `ruediger.layout.json` cuts
one with a rectangle at `base_height` 100 over `floor` 0. `override` decides the order the algebra resolves
in: the ordinary pass is (adds − subtracts), then override-adds overwrite whatever column they land on, then
override-subtracts remove theirs last, so an override-add is how ground is put back inside a hole and an
override-subtract is how a hole is cut through ground that was itself an override.

The **intent** is what the map is *for*: teams, spawns with yaw and protection, wools with rooms and
monuments, destroyables, cores, the build region and its holes, water lanes, and `structures` — room floors,
entrance redstone, iron cubes and approach walls. It is the only layer that knows the objective.

## A worked example

`tools/seeds/ruediger.layout.json` and `ruediger.intent.json` are a hand-drawn capture map, kept because it
is the only authored document in the repository that shows the layout being used as **design** rather than
as configuration. It is worth being clear about what that means, since the temptation is to read it as a
feature showcase and it is not one: the map was drawn **before relief and before house themes existed**, so
none of what it does is reaching for a new toy. It is an author deciding where ground goes and what it is
made of, with the tools that were there. Its dressing was going to come next and never did.

It is also **one** way to build a map, not the pattern to copy. What transfers is the method, not the
shapes.

**Elevation is built from shapes, and the small shapes are the elevation.** The layout carries no `relief`
block — it predates one. Its ground steps because twenty-six shapes sit at `base_height` 7, 8, 9, 10, 11, 12,
13, 14, 15 and 16, stacked as set algebra with one `subtract` cutting through and one face standing at 100.
The little shapes are not detail added to a big shape; they *are* how the author states a change in height.
That is the second way to make terrain, beside the relief solver, and it is the one that gives deliberate
steps rather than a solved surface — the two are meant to sit on one board, a built stepped quarter against a
naturally solved one.

**Three themes over one map, chosen per shape.** `ruediger` (sand and sandstone over stone) paints the body,
`ruediger-steps` (grass and stone brick) picks out the treads and the border, and `theme` (stone brick and
stained clay) carries the rest; seventeen of the twenty-six shapes name one. The stepped area therefore reads
as built and the ground around it reads as ground — the distinction MG2 says a single blanket theme throws
away.

**Curves and a hole.** Five shapes carry Bézier `controls` on their polygon edges, so their outlines are
drawn rather than rectilinear, and one shape is `subtract`.

**Two islands that mirror into four.** Both are `mirrors: true` under `rot_180`, so the authored half becomes
the whole 140×260 board.

**Hand-authored defence walls.** The intent carries two `structures.walls` entries — 21×3 at `topY` 13 and
its orbit image — the bedrock approach walls MG21 says nothing generated has ever asked for. It also carries
the two room floors and the two entrance redstone lines.

What it has none of is houses, dressing or iron, which is exactly the gap the review's dressing entries
describe. One clarification worth keeping, because the map is easy to misread: it holds a **single** sketch
layer named "Ground". The layering that reads as depth is the shapes' own `base_height` tiers, not
`layers[]`.

## The capability surface

`MapSpec` — the JSON `tools/mapgen` actually reads — is a reduction of the four documents above, not an
addressing layer over them (`review.md` MG29): it names a handful of knobs and hides the rest, so every one
of the first fifteen boards came out with a rim, one theme, one relief style and the same wall wherever it
met a drop. What follows is the surface underneath the reduction, by pipeline stage, so an author reaching
for a description knows what it can become before reaching for the spec's shorthand instead. Everything
below is built and shipping; where it is not reachable from `MapSpec`, that is a gap in the spec's
vocabulary, not in the system.

### A shape is not only ground — it is also an obstacle, and the cap is what makes it one

The layout reads as a document about where land is, and most of what it does is that. It is also the coarsest
control the system has over where a player may *go*, and two fields together are what turn a shape from
terrain into architecture.

The first is height without a way over it. `BuildIntent.MaxHeight` becomes the map's `max_build_height`
(`BuildGenerator`), and PGM will not let a block be placed above that line — so a bridge cannot be built over
anything standing higher. Ground whose top clears the cap is therefore not merely tall: it cannot be climbed,
cannot be bridged past, and cannot be built on top of. A shape given a `height_mode` of `level` at an
absolute height above the cap, or a `base_height` that reaches past it, is a wall made of terrain, and it
holds whatever line its outline is drawn along. The same fact is what makes a marker above the cap
ungriefable, which is the whole of why B89 puts one there.

The second is that the top need not be flat. `anchor_heights` states a height per vertex on a polygon or
lasso, TIN-interpolated across the footprint, so a shape's surface tilts rather than stepping. A rectangle
given four differing anchors is a slanted block; slanted and standing above the cap, it is an obstacle that
reads as a leaning slab rather than as a fence, and the direction of its lean is a decision about which side
a player is funnelled to. Below the cap the same tilt is a ramp, which is the other thing it is for: a way up
that no stair cut had to invent.

Read with the set algebra above, that is the layout's whole vocabulary of movement control, and it is
deliberate at every step. A `subtract` is a hole. A shape above the cap is a wall nobody passes. A tilted
shape below it is a way up. Gaps between pieces are the routes that remain. None of that is scenery, and none
of it needs a relief, a theme or a prop to work.

**Whether a hole can be crossed is a separate decision from cutting it, and it is made in the intent.** The
layout says where ground is absent; `BuildIntent`'s areas say where a player may place a block. A void gap
with no build region over it is permanent — nobody bridges it, and the approach it forces is around. The same
gap with a build region covering it is crossable from the first minute, at the price of the time and the
material a bridge costs and the visibility of building one. Both are legitimate and they play differently, so
a channel cut without deciding which it is has had half of it decided by accident. This is also how a board's
islands are joined at all: a capture map's separate landmasses are connected by build regions rather than by
ground, which is what `ruediger` does and what any connectivity read has to know before it can call a board
disconnected.

The **water lane** is the third setting of that same dial, and it is not water.
`docs/contracts/water-lanes.md` owns it: a gap between islands that becomes **bridgeable part-way through a
match**, built on PGM's void filter reading y=0 live, so the crossing opens on a timer rather than at the
start. Its use is narrow and worth stating, because the mechanism invites misuse — a lane in the middle of a
board means waiting three quarters of an hour for the map to begin. It belongs where a goal is tucked away
and wants a **second** approach opening late, changing the shape of the endgame rather than the opening.

### How many goals a destroy board carries, and where they stand

The count is a design decision with a narrow real range, and it is worth stating in numbers because the tool
will otherwise decide it as a side effect. Measured over the 127 corpus maps carrying a destroy objective,
per team: **one destroyable in 55% of them, two in 37%, three in 5%**, four or more in four outliers. Cores
are rarer and tighter — **one in 77%, two in 19%, three in a single map in the corpus** — because leaking a
core is a harder and longer job than breaking a monument, so a board wants fewer of them. Of the seventeen
maps carrying both kinds, sixteen have exactly one core a team, and the ordinary combined board is one
destroyable and one core. A large board with a single goal on it is not an underfilled board; it is the most
common destroy map there is.

Where a board carries more than one, they are **placed against each other rather than scattered**: a west and
an east, or two forward with one back near the spawn, or two back with one forward. That arrangement is the
board's shape, because each goal is a place a team has to hold, and their spacing is what decides whether the
defence is one line or three. The ground around each is themed apart in the corpus, which is the same
per-shape paint the layout already offers — the approach to a west goal reading differently from the approach
to an east one is what makes several goals read as several places rather than as one objective duplicated.

**A word on the word.** In game the mode is *destroy the monument*, so a `<destroyable>` is colloquially a
monument — but `monument` is already taken here and in PGM for the block a wool is placed on in a capture
map. The two are different objects on different kinds of map. This document says **destroyable**.

### The goal's material is a knob, and the kit has to agree with it

A destroyable's material is authored on the plan, not fixed. `DestroyablePlacement.Materials`
(`Pgm/Plan/PlanModel.cs`) is a plain string, empty meaning obsidian; `PlanCompiler` resolves it against
`ObjectiveDefaults.Materials` (`Domain/ObjectiveDefaults.cs`, `"obsidian"`) and carries the result onto
`DestroyableIntent.Materials` (`Pgm/Authoring/MapIntent.cs`). Obsidian is the corpus's own centre — over half
of every destroyable in the corpus names it — but it is not the only word the corpus uses:
`docs/contracts/destroyables-and-cores.md` DT1 measures emerald and gold behind the `cube-3` style and end
stone behind `cube-4` and `column-plus`. What the stamper can actually build from is a short, published
vocabulary rather than free text: `DestroyableMaterials.All` (`Domain/DestroyableMaterials.cs`) names exactly
four buildable matches — `"obsidian"`, `"emerald block"`, `"gold block"`, `"ender stone"` (`"end stone"`
resolves to the same block) — the four its own docstring measures carrying 84% of declared destroyables
(`docs/contracts/objective-suggestion.md` §3). **The two ends of the pipe are
not automatically kept in step.** `DestroyableGenerator` writes `Materials` verbatim into the emitted
`<destroyable materials="…">`, while `SketchWorldBuilder` stamps the structure from
`DestroyableMaterials.BlockId(Materials)`, which silently falls back to obsidian for anything outside the
four. Authoring one of the four words is exactly OB12's contract kept whole — the XML's filter and the
stamped blocks name the same material — while authoring anything else ships a `<destroyable>` whose declared
`materials` matches nothing in its own region: the exact silent zero-health goal OB3 warns PGM will not catch.
The knob is set the same way any plan field is — write `materials` on the plan's destroyable marker
(`PUT /map/{slug}/plan`) and compile it (`POST /plan/compile`) — and it is one of the four words, not any
material PGM itself would accept.

The core has no such knob. `CorePlacement` and `CoreIntent` carry no material field at all, and the doc
records why: DC1 calls obsidian "effectively universal" in the corpus and the generator emits no `material`
attribute, relying on PGM's own default. Asking a core for a different casing is therefore not a gap in the
spec — it is not expressible anywhere in the pipeline today.

Turning the destroyable's knob is not free of consequence, because PGM enforces a hard invariant on the other
side of it: **a tool has to be able to mine what it is sent to break.** An iron pickaxe does not mine
obsidian at all — not slowly, not at all — so a destroyable left at its obsidian default needs a diamond
pickaxe in the spawn kit, and the corpus never breaks that pairing (86 of 86 obsidian-goal maps carry one).
Nothing in the generator ties the two together yet: `TeamsGenerator.GenerateKits` writes one fixed "Standard"
kit for every map with spawns, an iron pickaxe among its tools, with no branch for a destroy objective or its
material (`Pgm/Authoring/TeamsGenerator.cs`). So today, choosing anything other than a hand-edited kit leaves
the default obsidian goal unbreakable, and choosing a softer material such as end stone does not fix that on
its own — it only changes which pairing has to be checked by hand. `review.md` MG18 and `TODO.md` B81 record
this as the fault it is; until B81 lands, pairing the kit to the goal is the author's job, not the
generator's.

### What the composer never asks for: a defence wall and a resource cube

Two structure kinds are authored end to end and reachable only by opening a plan in the editor, because the
composer that writes a plan from a `ComposeRequest` never touches either list.

A **defence wall** is `PlanWall` (`Pgm/Plan/PlanModel.cs`): the two piece ids, `A` and `B`, whose shared
interface it stands on, plus a `Side` naming which of the two faces carries the wall's defence chests. It is
two blocks thick by construction — `ContactGraph.WallInterfaces` turns the marked interface into a footprint
that `PlanCompiler` fans through the board's full symmetry order, and the chest face is carried as
`Side`'s piece id rather than a coordinate so a reflection, which swaps which face has the smaller
coordinate, still opens the same wall from the same side of the map on every orbit image. The compiled
`WallStructure` lands in `MapIntent.StructureIntent.Walls`, and `StructureStamper` builds it. An **iron
cube** is `IronPlacement`, a `{ piece, at }` half-cell marker inside `PlanPlacements.Iron`; `PlanCompiler`
resolves it to a world column, fans it through the same orbit, and emits an `IronCube` that
`StructureStamper.StampIronCube` raises as a 4×4×4 block. Both are authored the same way a wool or a spawn
is — write `walls`/`placements.iron` into the plan document (`PUT /map/{slug}/plan`) and compile it
(`POST /plan/compile`) — and both round-trip through the editor's own tools (the plan editor's brick tool for
a wall, the marker palette for iron). `Composer.cs` writes neither list, and `MapSpec` has no field for
either, so a generated board carries no defence wall and no resource to fight over unless a person adds one
by hand afterward.

### The paint a theme can hold

`MapSpec`'s `theme` block reduces `TerrainTheme` (`Minecraft/TerrainTheme.cs`) to four material words and
one pattern name. The type underneath is five buckets, three of which carry their own geometry: `Rim` and
`Surface` are each a `TopBand` — a material plus its own depth and on/off toggle, so a one-block quartz edge
and a three-block grass-over-dirt interior are independent knobs rather than one shared number — `Wall` and
`Fill` are bare materials, and `Bedrock` and `RimEdges` (`Void`/`Drop`/`Boundary`, deciding which edges the
rim caps at all) sit alongside them as theme-wide geometry. A theme also scopes: `SketchShape.Theme` names
which registry entry a given shape paints with, so one board carries as many themes as it has shapes for, the
way `ruediger.layout.json` carries three.

`pattern` in the spec reaches six of `TerrainMaterial`'s fourteen derived kinds — `solid`, `voronoi`, `cell`,
`noise`, `turbulence`, `electric` (`Minecraft/TerrainPatterns.cs`) — leaving `layered`, `teamTint`,
`wallRun`, `wallDiagonal`, `checker`, `logChecker`, `laidLog` and `wallFrame` reachable only by writing the
theme JSON directly, the way `ruediger`'s own `wall` bucket does with a `wallRun` over a `teamTint`. Each
area pattern (voronoi, cell, noise, turbulence, electric) fills from a **palette**, and `TerrainPalette`
(`Minecraft/TerrainPalette.cs`) names nineteen tone families — `verdant`, `spring`, `turquoise`, `loam`,
`dirt`, `brick`, `rust`, `sand`, `gold`, `pale stone`, `ash`, `grey stone`, `cobble`, `mauve`, `azure`,
`slate`, `dark`, `ice`, `bright` — each a curated set of blocks that read as one ground with a texture,
grouped by what an author would reach for rather than by block taxonomy: gravel sits with cobble because
both are laid where water edges the ground, not because both read grey. `GET /terrain/blocks` answers the
whole vocabulary a picker offers; `POST /terrain/theme-preview` and `/terrain/theme-map-preview` show a
theme as it will paint before a world is built. Nineteen families crossed with six spec-reachable patterns —
and eight more kinds only reachable by hand — is a far larger surface than the one wall every generated
board wore.

### The relief mark vocabulary is constraints and pushes, not one shape of number

`Mark` (`Geom/Relief/Marks.cs`) is the type `MapSpec`'s `marks` array and `scatter` convenience both resolve
to, and `scatter` is the smallest corner of it: an ordinary run of `PointMark`s at random positions, nothing
a `marks` array stated by hand could not equally say. The five kinds pin a footprint's cells to a stated
height — `PointMark` a summit or hollow with a radius, `LineMark` a ridge or valley whose height can vary
along its length, `AreaMark` a flat bench or floor bounded by a ring, `RimMark` the footprint's own outer
edge held at one level, and `ScarpMark` a face rather than a height: two bands pinned either side of a drawn
line, so what is actually authored is a grade, and the grade decides whether the face is walked, climbed with
one block, or not crossed on foot at all. `PushMark` is a different thing wearing a similar shape: every
`Mark` is a constraint the solver has to satisfy, which is what makes two of them **conflict** where they
overlap, while a push lifts or lowers the surface *after* the constraints are solved, which is what lets two
pushes over the same ground simply **add**. A hand-drawn hill with a shaped plan — not a circle, because a
push's falloff runs from the drawn ring across the land rather than from a centre — is a push; a stated
summit at a stated height is a mark. Both are already in `MapSpec`'s vocabulary; what is rare in the corpus
of generated boards is reaching for either instead of `scatter`.

### A building is described, then built — and its plan can turn a corner

`HousePresets` (`Minecraft/HousePresets.cs`) is the proof this method already works: each preset's docstring
is the prose brief its `HouseStyle` was built from — "its walls run seven courses between spruce log posts
that stand the full height… the bottom two courses are cobble and andesite mixed" for `Alpine`, read directly
off `alpine_mining_ii` block by block — and the style that follows says exactly that in code. Nothing about
turning a house from a paragraph into a `HouseStyle` needed inventing; it has been done ten times over, once
per preset in `HousePresets.All`.

The surface a description can reach is most of the building. A `RoomPart` wall is stacked from `RoomCourse`
bands, each its own material and height counted up from the floor, so a stripe pinned at the fourth course
stays there as the wall grows. Around it: a `Roof` body and `Verge` border with their own `Form` (gable,
flat, hip, gambrel, shed or saltbox), `Pitch`, and `Overhang`; a `Post` at the corners and a `Sill` the walls
stand proud of; `Windows` with their own `Form`, `Width`, `Height`, `Sill` and `Spacing`, and a separate
`GableWindows` style for the triangle a sloped roof leaves standing, since a gable is not a wall and centres
its one window rather than spacing a row; a `DoorHead` that arches the top course of a doorway or leaves it
plain; `Beams` — the log ends that run out past a corner where two storeys meet; and a `Storeys` stack, each
with its own wall, windows, floor and post, so a building can change what it is built of a level up. None of
that is used twice the same way across the fifteen boards `review.md` MG33 measures, which is why they read
as one house repeated: the presets cluster at 7–13 wide by 7–11 deep and every board draws from a handful of
them at the size they were designed at.

`Footprint` (`Minecraft/Footprint.cs`) carries `Wings` — more than one touching rectangle walked as one
landmass, its outline traced as a single ring so an L or a T stands under one roof with a cross-gable built
where the wings meet, rather than as two buildings that happen to touch. `HouseStamper` already builds this;
what is missing is a way to ask for it from a placed prop. `HouseProp.Points`
(`Minecraft/Dressing/PlacedProp.cs`) is exactly two opposite corners, and every `new Footprint(...)` call in
the tree — outside `Footprint.At`'s own per-storey slicing — is the single-rectangle constructor. So a
wing-carrying `Footprint` is reachable by hand-building one in code and unreachable from dressing, `village`,
or `houses` in `MapSpec`. Library previews exist for the pieces once a style is composed —
`/room-styles/preview` and its `-snapshot`, `/roof-styles/preview`, `/porch-styles/preview`,
`/storey-styles/preview` — so a building is checkable from four sides before it stands on a map.

### Circulation is decided before dressing, not after it

Scenery is placed last in the pipeline and decided first in the design, and reversing those is what makes a
board read as cluttered rather than as furnished. A dressing pass that samples wherever the ground will take
a prop produces exactly what it asks for — trees and buildings standing in the routes, so reaching a build
region means walking round a house and then round a tree, neither of which anybody put there.

The order that works states the **movement** first. A `path` shape is a drawn centreline with a half-width,
so a road network can be traced before anything is planted: where a player walks from spawn to goal, where
the flanking approach runs, where a village's street is. Those runs and a margin either side are then the
ground foliage does not get, and everything else is where a wood or a settlement may stand. That is the same
reasoning `clearance` already applies to objectives, applied to routes as well — and it turns density from a
number into a consequence, because the space left over after the circulation is drawn is the space a forest
is allowed to fill.

It also settles what a prop may do to a building it stands beside. A prop writes only into air, so a tree can
never replace a wall, a roof or a post, and leaves resting **against** a house are correct — an author pastes
a tree beside a building masked against the building's own blocks and expects exactly that. What the author
then does by hand, and what nothing does here yet, is clear the leaves that landed **inside** the building
through its roof (`B97`). A tree rooted inside a structure is a fault; a canopy leaning on one is not.

### What these combine into, which is where the width actually is

Every section above names one type. The reach of the system is not in any of them individually — it is in
what they compose into, and the compositions have no field named after them, so nothing discovers them by
reading a schema. Three worth stating, because each is built today out of parts already described.

**A stair of themed steps, at whatever granularity the ground wants.** A composed plan lands at a single
height, because the rules deciding how height should fall across a board are the hard part and the composer
does not try. Relief answers half of that — a solved surface without anyone stating a piece — and stacked
shapes answer the other half, deliberately rather than by solve. Where a plan is cut into pieces the
granularity is the cell, five or nine or ten blocks at a time; the layout underneath is drawn in **blocks**,
so a step there can be one block deep. Twenty-six shapes at ten `base_height` tiers is what `ruediger` does,
and because each shape carries its own `theme`, alternate treads can take alternate paint — every even step
one theme and every odd step another — so the flight reads as built masonry rather than as a slope that
happens to be quantised. The two ways of making ground are meant to share a board: a stepped quarter that is
plainly a platform against a solved quarter that is plainly a hillside.

**An erected cube as a blocker, themed as its own thing.** A shape with a `height_mode` and a top above
`max_build_height` is an obstacle rather than terrain, and because paint scopes to the shape, that obstacle
takes its own material — so a line of them is a colonnade, a wall or a set of pillars, and nothing about it
is scenery. Tilted with `anchor_heights` it leans; below the cap the same tilt is a ramp.

**A building used as a boundary rather than as a place.** A `HouseStyle` takes `RoofForm.Flat` — the lid form
— and its courses take any material, bedrock included, so a house can be authored as a sealed slab-topped
block rather than as somewhere to walk into. Stood tall enough to clear the build cap it is a tower that
divides the map, and a run of them along an edge seals a board with scenery instead of with a wall — which is
the only way an edge gets sealed at all in a mode where nothing may be placed.

**What is missing is the inside, and the reason it matters is the facade.** `HouseStamper` builds a shell and
leaves the volume it encloses as air, so such a building is enterable and hollow rather than a mass. Filling
it is `B92`. The part worth stating now, because it is what makes the technique work rather than an
implementation detail: a filled building **keeps its windows and its door**, and the fill sits behind them. A
dark fill — black wool is the idiom — reads through a window as an unlit interior, so the building is a house
with its lights off rather than a lump wearing a house's outline. The facade is the whole point of using a
building for this instead of a shape.

## Where the generation lives

| Stage | Lives in | Takes → gives |
|---|---|---|
| compose a board | `Pgm/Compose/Composer.cs` | `ComposeRequest` → `PlanModel` |
| validate / score | `Pgm/Plan/PlanValidator.cs`, `Evaluate/` | `PlanModel` → findings with rule ids |
| compile | `Pgm/Plan/PlanCompiler.cs` | `PlanModel` → `(SketchLayout, MapIntent)` |
| rasterize | `Pgm/Sketch/SketchRasterizer.cs` | layout JSON → columns `(x, z, yFloor, yTop)` |
| solve relief | `Geom/Relief/` | `ReliefSpec` → a surface per island |
| build the world | `Api/Services/SketchWorldBuilder.cs` | layout + intent → `VoxelWorld` + resolved intent |
| paint | `Minecraft/TerrainPainter.cs` | raw stone → rim, wall, surface, fill |
| dress | `Minecraft/Dressing/Decorator.cs` | props → trees, houses, boulders, paths, water |
| stamp buildings | `Minecraft/HouseStamper.cs` | `Footprint` + `HouseStyle` → walls, roof, openings |
| stamp furniture | `Minecraft/StructureStamper.cs` | `StructureIntent` → floors, redstone, iron, walls |
| write the goal | `Pgm/Authoring/IntentGenerator.cs` | resolved intent → the map document |
| write the XML | `Api/Services/MapXmlComposer.cs` → `Pgm/XmlWriter.cs` | document → `map.xml` |

`SketchWorldBuilder` is the one to read before changing anything downstream, because **order is the
contract**: floors, then wool cages, then spawn cubes and monuments, then plan-derived structures, then the
build-region outline, then destroyables and cores, then the terrain finish, then the dressing, then the
observer platform. Painting happens *after* every stamp so it can skip a column whose top is not terrain, and
dressing happens after painting so it can read the finished surface rather than re-derive it. A pass inserted
in the wrong place is not a small mistake — it is a house painted as ground, or a tree planted through a
monument.

`MapXmlComposer` is the export path and not an optional wrapper: it applies `CtwStandards` (the keep, repair
and remove rules derived from the spawn kit, hunger off, the kill-reward include), the ore and structure
renewables, and the reordering that puts the `not-build-area` rule last. Calling `XmlWriter` directly skips
all of it (MG14).

## What to look at, and when

The system renders itself at every stage, and a generated map should be read at each rather than judged from
one picture at the end (MG13, MG30). Three families exist.

**The API previews** answer a document without building a world — thirteen endpoints, none of which the first
fifteen maps used. `POST /terrain/theme-preview` and `/terrain/theme-map-preview` show a theme as it will
paint, the second over a compiled plan; `/terrain/material-preview` shows one material; `/terrain/prop-preview`
shows a tree, boulder or path before it is placed; `/room-styles/preview` and its `-snapshot`,
`/roof-styles/preview`, `/porch-styles/preview` and `/storey-styles/preview` show a building from four sides;
`/themes/preview` shows a library row. `GET /plans/{id}/svg` draws the plan itself as a vector card;
`GET /plans/{id}/png` draws the same board — off the same geometry, so the two can never disagree — as the
raster an image reader can actually open (`B90`, `B21`'s `plan_render`). `GET /shapes/probe` emits a canonical
family through the real emitters and answers with the shape or a directed rejection.

**The corpus and world harnesses** in `tools/PgmStudio.RoundTrip` read a built world back: `--topdown` for
the plan view, `--heightmap` and `--contour` for the third dimension, `--surface` for what the paint did,
`--traversability-map` for whether the navigable ground actually joins spawn to every goal — ground and
headroom, plus any void column the map's own buildable-region apply rule opens to bridging from the first
tick, so a capture board that joins its islands with build regions (`ruediger`, or a composed board's own
mid band) does not read as cut apart just because the join has no ground of its own; a water lane is left
out on purpose, since it opens only after the match clock passes its timer and is not a connection yet at
the moment the picture is taken — `--buildings` and
`--structures` for what was stamped, `--island-study` and `--skeleton-study` for footprint shape and
centrelines, `--water`, `--flora`, `--ores` and `--underground` for the rest. (`--traversability` — no `-map`
— is a different thing: the Python-parity comparator over parquet features, not a picture; the name is close
on purpose; the two do not answer the same question.) These renderers live in `PgmStudio.Minecraft.Render` and
read equally from a region directory or an in-memory `VoxelWorld` (`AnvilRegion.FromWorld`), which is what
lets `tools/mapgen` emit the same set itself, over the world it just built, with no second load off the
region files it just wrote — see `README.md` beside this file.

**The prototypes** render the model rather than a map. `tools/relief` emits ten figures plus a topographic
view, a blocks-from-an-angle view, a section and a step map, and `--corpus` measures real worlds into the
same terms. `tools/compose` holds twenty-two galleries — boards, bodies, boxes, edges, mids, seeds, hubs —
each rendering the real emitters.

The rule that follows: **a stage that produced something should be looked at before the next stage consumes
it.** A theme is checkable before a world is built, a plan is checkable before it compiles, a shape is
checkable before it is drawn, and a forest is checkable in a heightmap rather than guessed from a leaf count.

The rule that follows *that* one, because looking has its own failure mode: **an image is a check, not a
source of meaning.** A render answers whether the thing that was authored came out; the document underneath
answers what it is. The plan render in particular colours by **role** rather than by material — hub violet,
spawn green, wool amber, frontline orange, and a zone in blue — so its blue is a build zone or a water lane
and never water, and the two zone kinds are separated only by shade and dash (`B95`). A board whose central
build zone was read as water off the picture, on a map carrying no water at all, is what that costs: the
observation was right, the cause was invented, and the invented cause then explained away a real
connectivity result. Read the picture for whether, read the document for what.
