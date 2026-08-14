# What a map is made of, and where each part is made

The reference `tools/mapgen` should have been written against. A map is not one document — it is a short
stack of them, each owned by a different project, each with its own JSON shape, its own endpoint and its own
generator. Reading the stack is what stops a tool from inventing a flatter format of its own and losing the
system's reach in the process (`mapgen-review.md` MG29).

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
spawns, wools, iron, destroyables and cores as piece-relative half-cell offsets — a destroyable or a core alone
may drop the piece and read its offset as an absolute board position instead (`B128`), so a goal can ride an
authored sketch shape with no plan piece manufactured to carry it — authored for **team 0 only** and fanned by
symmetry at compile. Beside them it carries `walls` — a defence wall named by the two pieces
whose interface it stands on — and `globals` with the cell size, the symmetry and the player count. It is the
right surface for an agent because it is small and because the validator and evaluator answer it with
rule-ids.

The **layout** is the ground: `shapes` (rectangle, circle, polygon, path) with set-algebra operations,
grouped into `islands` that decide what mirrors. A shape carries far more than a footprint — its own `theme`,
its `floor` and `base_height`, per-vertex `anchor_heights`, a `height_mode` of `level`/`raise`/`sink` with a
`skirt`, and a `relief_scope` of `hold`/`exclude` deciding whether its ground joins the island's solved
relief. The **relief** rides beside the shapes rather than inside them, keyed by island id, because a plan
recompile replaces every shape it produced and a relief is hand work a plan cannot express.

The set algebra is where **void** comes from, and void is the instrument `mapgen-review.md` names as the primary
control on flow for a capture board — so it is worth stating outright rather than leaving inside the phrase
"set-algebra operations". A shape's `operation` is `add` unless it says otherwise; a shape carrying
`subtract` takes its footprint's columns out of the ground entirely, which is a hole to the void rather than
a dip in the surface. That is the difference from relief, and it is the whole of it: **relief moves a
surface, a subtract removes it**, so no mark of any kind cuts a channel and no channel is ever a relief
question. A channel twenty blocks across in front of an objective is one subtract rectangle or polygon, drawn
at the width the gap wants — and its **height is not read**. The rasterizer resolves a subtract in plan only,
removing the whole column at every cell the outline covers, so a one-block-tall subtract carves exactly as
deep as a two-hundred-block one. `ruediger.layout.json` states `base_height` 100 over `floor` 0 on its cut,
which is a statement of intent rather than a load-bearing number. `override` decides the order the algebra resolves
in: the ordinary pass is (adds − subtracts), then override-adds overwrite whatever column they land on, then
override-subtracts remove theirs last, so an override-add is how ground is put back inside a hole and an
override-subtract is how a hole is cut through ground that was itself an override.

The **intent** is what the map is *for*: teams, spawns with yaw and protection, wools with rooms and
monuments, destroyables, cores, the build region and its holes, water lanes, and `structures` — room floors,
entrance redstone, iron cubes and approach walls. It is the only layer that knows the objective.

## A worked example

`tools/seeds/ruediger.plan.json`, `ruediger.layout.json` and `ruediger.intent.json` are a hand-drawn capture
map at all three layers, kept because it is the authored example in the repository that shows the stack being
used as **design** rather than as configuration. It is worth being clear about what that means, since the
temptation is to read it as a feature showcase and it is not one: the map was drawn **before relief and
before house themes existed**, so none of what it does is reaching for a new toy. It is an author deciding
where ground goes and what it is made of, with the tools that were there. Its dressing was going to come next
and never did.

It is also **one** way to build a map, not the pattern to copy. What transfers is the method, not the
shapes.

**Elevation is built from shapes, and the small shapes are the elevation — and those shapes came out of the
plan.** The layout's tiers are not a layout-only technique: `ruediger.plan.json` states them as
`PlanPiece.Surface` on cell-grid rectangles, thirty-one pieces standing at ten heights from 7 to 16 over a
base of 9, and `PlanCompiler` turns each distinct surface within a component into its own shape. The proof is
the file itself — the compile emits the twenty-one polygons `s0`–`s20` and the four structural rectangles
`spawn-red`, `spawn-blue`, `wool-red-red` and `wool-blue-blue`, which is twenty-five of the layout's
twenty-six shapes, and its bbox −70..70 × −130..130 is the one the layout carries. So the mechanism transfers
exactly, and an author writing a `*.plan.json` can state a stepped board directly: the map's western
staircase is six pieces, one at surface 7 and five single cells climbing 8, 9, 10, 11, 12, one step per cell,
of which the one at 9 states no surface at all and inherits the base.

The layout carries no `relief` block — it predates one. Its ground steps because twenty-six shapes sit at
`base_height` 7, 8, 9, 10, 11, 12, 13, 14, 15 and 16, stacked as set algebra with one `subtract` cutting
through and one face standing at 100. The little shapes are not detail added to a big shape; they *are* how a
change in height is stated. That is the second way to make terrain, beside the relief solver, and it is the
one that gives deliberate steps rather than a solved surface — the two are meant to sit on one board, a built
stepped quarter against a naturally solved one.

**Three themes over one map, chosen per shape.** `ruediger` (sand and sandstone over stone) paints the body,
`ruediger-steps` (grass and stone brick) picks out the treads and the border, and `theme` (stone brick and
stained clay) carries the rest; seventeen of the twenty-six shapes name one. The stepped area therefore reads
as built and the ground around it reads as ground — the distinction MG2 says a single blanket theme throws
away.

**Curves and a hole — and these are the sketch's, not the plan's.** Five shapes carry Bézier `controls` on
their polygon edges, so their outlines are drawn rather than rectilinear, and one shape is `subtract`. That is
where the boundary between the two layers actually falls on this map: the curves sit on `s2`, `s6`, `s9`,
`s15` and `s16` — compiled polygons whose outlines were bent afterwards — and the subtract is
`s1785789842694_2`, a sketch-minted id and the one shape in the file the plan did not produce. The themes
above are the sketch's for the same reason. A plan states rectangles on a cell grid and a height per
rectangle; everything drawn, carved or painted on top of that is the tool downstream.

**Two islands that mirror into four.** Both are `mirrors: true` under `rot_180`, so the authored half becomes
the whole 140×260 board.

**Hand-authored defence walls.** The intent carries two `structures.walls` entries — 21×3 at `topY` 13 and
its orbit image — the bedrock approach walls MG21 says nothing generated has ever asked for. They are a plan
mark rather than hand-written intent: `ruediger.plan.json` carries one entry in `walls`, naming the piece pair
`wool-a-t1`/`wool-a-t3`, and the compiler stamps it onto the seam those two share and fans it. The intent also
carries the two room floors and the two entrance redstone lines, from the same compile.

What it has none of is houses, dressing or iron, which is exactly the gap the review's dressing entries
describe. One clarification worth keeping, because the map is easy to misread: it holds a **single** sketch
layer named "Ground". The layering that reads as depth is the shapes' own `base_height` tiers, not
`layers[]`.

## Forty-eight worked plans, and a traced corpus

`ruediger` is the worked example this document reached for first, and it is a whole stack — plan, layout and
intent, one map. The plan layer has further worked examples and there are forty-eight of them, in
`tools/seeds/`, which is where an author looking for "how is a board actually stated" should start. Nothing
pointed at them, which is the likeliest reason every generated board so far began from `compose`: the only
example on offer was one layer down from the question being asked.

**Sixteen of them are real published maps traced into plan space** (`tools/seeds/traced/` — `acapulco`,
`aether`, `ad-astra`, `after-hours`, `3084` and the rest). That makes them the ground truth for what a
real board's structure looks like as pieces: how many, how large, how they connect, and — the part no
generated board has ever reproduced — **how their heights step**. `traced/bridgid-ii.plan.json` carries 36
pieces across sixteen height tiers from 11 to 41.

**Height is stated per piece, and the global stays where it is.** Every seed leaves `globals.surface` at 9
and varies `PlanPiece.Surface` instead, which is what makes a step a step: `mirror-big-board` runs 39 pieces
over tiers 11·13·15·17·19, `big-board-wool-two-sided-plaza-parallel-mid` runs 16 pieces over
5·7·11·13·15·17, and `mirror-tiny-map-cliff` puts its pieces at 3·5·7·11 — at and **below** the global,
which is how ground goes down rather than up. A relief dropped onto a board whose pieces all sit at the
default has nowhere to cut to; the seeds solve that by stating the tiers first.

**A third folder teaches structures rather than maps.** `tools/seeds/teaching/` holds seventeen plans built
to demonstrate one thing each — build interfaces, build regions, a crammed frontline, a middle void with and
without steps, mid rotations — and it carries its own shopping list of what is covered. It is the right place
to look for what a named structure is supposed to look like before authoring one.

The folder's own `README.md` documents only the three `base-*` sketch layouts and frames the directory as
test fixtures for the export, so none of the above is discoverable from it (`B108`).

## The capability surface

`MapSpec` — the JSON `tools/mapgen` actually reads — used to be a reduction of the four documents above rather
than an addressing layer over them (`mapgen-review.md` MG29): it named a handful of knobs and hid the rest, so
every one of the first fifteen boards came out with a rim, one theme, one relief style and the same wall
wherever it met a drop. `B118` closed that gap: `plan`, `layout` and `intent` are now handed through the spec
verbatim, and the convenience fields that remain (`theme`, `relief`, `room_shell`) are shorthand that expands
into a fragment of one of those three. What follows is the surface underneath the convenience fields, by
pipeline stage, so an author reaching for a description knows what it can become before reaching for the
spec's shorthand instead — or, once the shorthand runs out, states the fragment directly through `layout` or
`intent`. Everything below is built and shipping; where it is not reachable at all — not through a
convenience field and not by handing a document fragment through — that is a gap in the system, not in the
spec.

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
layout says where ground is absent; `BuildIntent`'s areas say where a player may place a block, so the same
gap is permanent or crossable-from-the-first-minute depending on a document that says nothing about geometry.
Which of the two a given channel should be is `docs/gameplay/approaches.md`. The mechanical consequence
belongs here: this is how a board's islands are joined at all, since a capture map's separate landmasses are
connected by build regions rather than by ground — which is what `ruediger` does, and what any connectivity
read has to know before it can call a board disconnected.

The **water lane** is the third setting of that same dial, and it is not water.
`docs/pgm/water-lanes.md` owns the mechanism: a gap between islands that becomes **bridgeable part-way through
a match**, built on PGM's void filter reading y=0 live, so the crossing opens on a timer rather than at the
start. Where it belongs on a board, and the misuse the mechanism invites, is `approaches.md`.

### How many goals a destroy board carries

The count is a design decision with a narrow real range, and it is worth stating in numbers because a tool
will otherwise decide it as a side effect. Measured over the 127 corpus maps carrying a destroy objective,
per team: **one destroyable in 55% of them, two in 37%, three in 5%**, four or more in four outliers. Cores
are rarer and tighter — **one in 77%, two in 19%, three in a single map in the corpus** — and of the seventeen
maps carrying both kinds, sixteen have exactly one core a team, so the ordinary combined board is one
destroyable and one core.

Those are counts. What the counts are *for* — why several goals are placed against each other rather than
scattered, and why a large board with one goal is not underfilled — is `docs/gameplay/approaches.md`.

**A word on the word.** In game the mode is *destroy the monument*, so a `<destroyable>` is colloquially a
monument — but `monument` is already taken here and in PGM for the block a wool is placed on in a capture
map. The two are different objects on different kinds of map. This document says **destroyable**.

### The goal's material is a knob, and the kit has to agree with it

A destroyable's material is authored on the plan, not fixed. `DestroyablePlacement.Materials`
(`Pgm/Plan/PlanModel.cs`) is a plain string, empty meaning obsidian; `PlanCompiler` resolves it against
`ObjectiveDefaults.Materials` (`Domain/ObjectiveDefaults.cs`, `"obsidian"`) and carries the result onto
`DestroyableIntent.Materials` (`Pgm/Authoring/MapIntent.cs`). Obsidian is the corpus's own centre — over half
of every destroyable in the corpus names it — but it is not the only word the corpus uses:
`docs/pgm/destroyables-and-cores.md` DT1 measures emerald and gold behind the `cube-3` style and end
stone behind `cube-4` and `column-plus`. What the stamper can actually build from is a short, published
vocabulary rather than free text: `DestroyableMaterials.All` (`Domain/DestroyableMaterials.cs`) names exactly
four buildable matches — `"obsidian"`, `"emerald block"`, `"gold block"`, `"ender stone"` (`"end stone"`
resolves to the same block) — the four its own docstring measures carrying 84% of declared destroyables
(`docs/world-scan/objective-suggestion.md` §3). **The two ends of the pipe are
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

Turning the destroyable's knob has a consequence worth pairing the kit to, though not a legal one: **an iron
pickaxe breaks obsidian, it just does not drop it**, so a destroyable left at its obsidian default is slow to
finish against a mismatched kit, not unbreakable — the corpus itself never leaves that pairing to chance (86
of 86 obsidian-goal maps carry a diamond pickaxe). `TeamsGenerator.GenerateKits` now ties the two together:
the spawn kit's pickaxe comes from `DestroyKitPairing.RequiredPickaxe`, which upgrades the corpus-default iron
to diamond whenever the map carries an obsidian destroyable or any core, and to iron for a softer material
such as end stone. Choosing a material is therefore free of hand-editing the kit afterward — the pairing was
the fault `mapgen-review.md` MG18 named and `B81` closed. An earlier version of this pairing also refused the
export outright on a mismatch (`OB18`); `B134` found that premise false and removed the refusal, leaving the
pairing as a generation choice rather than a gate.

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
a wall, the marker palette for iron). `Composer.cs` writes neither list, so a `compose`-asked board carries
neither unless the plan it produced is hand-edited before compiling — `tools/mapgen`'s `plan` field takes a
literal plan document for exactly that — or unless `MapSpec`'s `intent` fragment adds a `structures.walls` or
`structures.ironCubes` entry directly onto the compiled intent, in already-resolved world coordinates.

### The paint a theme can hold

`MapSpec`'s `theme` convenience field reduces `TerrainTheme` (`Minecraft/TerrainTheme.cs`) to four material
words and one pattern name; the full type is reachable regardless, by adding a registry entry through the
spec's `layout` fragment — a `SketchLayout`'s `themes` map, handed through verbatim. The type underneath is
five buckets, three of which carry their own geometry: `Rim` and
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
that is used twice the same way across the fifteen boards `mapgen-review.md` MG33 measures, which is why they read
as one house repeated: the presets cluster at 7–13 wide by 7–11 deep and every board draws from a handful of
them at the size they were designed at.

`Footprint` (`Minecraft/Footprint.cs`) carries `Wings` — more than one touching rectangle walked as one
landmass, its outline traced as a single ring so an L or a T stands under one roof with a cross-gable built
where the wings meet, rather than as two buildings that happen to touch. `HouseStamper` already builds this;
what is missing is a way to ask for it from a placed prop. `HouseProp.Points`
(`Minecraft/Dressing/PlacedProp.cs`) is exactly two opposite corners, and every `new Footprint(...)` call in
the tree — outside `Footprint.At`'s own per-storey slicing — is the single-rectangle constructor. So a
wing-carrying `Footprint` is reachable by hand-building one in code and unreachable from dressing, including
through `MapSpec`'s `layout` fragment: a `house` prop is a `HouseProp` verbatim, and `HouseProp` itself has no
field to carry a second rectangle. Library previews exist for the pieces once a style is composed —
`/room-styles/preview` and its `-snapshot`, `/roof-styles/preview`, `/porch-styles/preview`,
`/storey-styles/preview` — so a building is checkable from four sides before it stands on a map.

### Circulation is decided before dressing, not after it

Why the movement is drawn before the scenery, and what a path does to where a forest may stand, is
`docs/gameplay/approaches.md`. What this document owns is the surface it is said with: a `path` prop is a
drawn centreline with a half-width, so a road network can be traced before anything is planted. (The layout
carries a `path` **shape** type as well — a centreline with its own band, edge style and seed — but no tool
in the Draw dock draws one, so it is reachable only in a document written outside the editor.)

One consequence is worth keeping here because it is about what the stamper does rather than about play. A
prop writes only into air, so a tree can never replace a wall, a roof or a post, and leaves resting
**against** a house are correct — an author pastes a tree beside a building masked against the building's own
blocks and expects exactly that. What nothing does yet is clear the leaves that landed **inside** a building
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
| build the world | `Export/SketchWorldBuilder.cs` | layout + intent → `VoxelWorld` + resolved intent |
| paint | `Minecraft/TerrainPainter.cs` | raw stone → rim, wall, surface, fill |
| dress | `Minecraft/Dressing/Decorator.cs` | props → trees, houses, boulders, paths, water, ground cover |
| stamp buildings | `Minecraft/HouseStamper.cs` | `Footprint` + `HouseStyle` → walls, roof, openings |
| stamp furniture | `Minecraft/StructureStamper.cs` | `StructureIntent` → floors, redstone, iron, walls |
| write the goal | `Pgm/Authoring/IntentGenerator.cs` | resolved intent → the map document |
| write the XML | `Export/MapXmlComposer.cs` → `Pgm/XmlWriter.cs` | document → `map.xml` |

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
centrelines, `--water`, `--flora`, `--ores` and `--underground` for the rest. These renderers live in
`PgmStudio.Minecraft.Render` and
read equally from a region directory or an in-memory `VoxelWorld` (`AnvilRegion.FromWorld`), which is what
lets `tools/mapgen` emit the same set itself, over the world it just built, with no second load off the
region files it just wrote — see `README.md` beside this file.

**`--topdown` reads by category, not by material, and the choice is a default rather than a rule.** A column's
visible block sorts into one of five categories — void, water, foliage, structure, ground
(`PgmStudio.Minecraft.RenderCategories`) — and each paints a fixed, saturated hue chosen for maximum pairwise
separation on the wheel rather than for resemblance to the game's own colours: stone, stone brick, cobblestone
and andesite are all some shade of grey in the game and would paint one indistinguishable field, so the
category scheme reads them as one thing (ground) and gives foliage a violet no terrain wears and a built
surface an orange no material shares with it. `--material` switches back to the old per-block `BlockPalette`
reading for the caller checking a theme's actual paint rather than the map's shape. `--layer
ground|structure|foliage|objectives` isolates one question per image instead of drawing the combined view: a
category other than the one asked for reads as a flat context tone, and `objectives` carries no terrain
reading at all — only the `map.xml` overlay, on a uniformly dim backdrop — because "where do the goals sit" is
a question the finished map's own colours only get in the way of. `tools/mapgen --stages` now emits `foliage`
and `objectives` alongside the eight `B90` named, and `dressing`/`topdown` draw the category reading by
default rather than the realistic one. Every PNG this renderer (and every other world read-back below) writes
carries a legend baked into the image — swatch, name, one row per colour actually used — and a scale line
stating blocks-per-pixel, via `PgmStudio.Geom.Render.Legend`, so a reader never has to bring an outside key to
the picture (`B98`, `B95`).

**The Ground/Structure boundary is read from a recorded build, not from the block, whenever one is
available (B133).** A material test cannot separate a cottage's stone-brick wall from a plaza paved in the
same stone brick, or from a mesa an author painted to read as built, and no palette refinement fixes that — a
block does not know what placed it. `PgmStudio.Minecraft.WorldProvenance` is what a built world carries
instead: `SketchWorldBuilder` claims every rasterized column as `Ground` first, then each stamp — a room
floor, a wool cage, a spawn cube, a wall, an iron cube, a redstone line, a destroyable, a core, a
dressing-placed building — claims its own footprint as `Structure` over it, composited in placement order so
a later claim covers an earlier one. `RenderCategories.Of(blockId, provenance)` reads a recorded claim as
authoritative for the Ground/Structure pair (liquid, foliage and void stay material questions, which they
already answer without ambiguity) and falls back to the material estimate — the single-argument overload —
when no claim was ever recorded for that column. `--topdown`'s `Run(regionDir, …)` overload and
`tools/mapgen --stages` pick up the recorded record automatically; `--material` is unaffected, since a
material check is asking a different question on purpose. The picture states which reading it used: the
scale line carries `STRUCTURE READING: RECORDED PROVENANCE` or `MATERIAL ESTIMATE (NO RECORDED PROVENANCE)`,
because that is exactly the fact a colour alone cannot carry.

**The split is real and is not papered over.** A world the studio *builds* gets the recorded truth: the
record is written beside the region files a build writes (`PgmStudio.Minecraft.WorldProvenanceFile`, one
run-length-encoded JSON file per region directory — a block still carries no provenance byte of its own, so
the answer has to live somewhere the voxels do not), and travels inside a downloaded `.zip` the same way. A
world the studio only *scanned* — the corpus, an imported map, anything built before this recording
existed — carries no such file and stays on the material estimate, which is the only reading available for
it and always will be.

Every renderer above looks straight down. `--column <regionDir> <x> <z> [x z ...]` and
`--section <regionDir> <outPng> --x <lo> <hi> --z <fixed>` (or the axes swapped) are the vertical
complement: `--column` prints one or more columns bedrock to sky, every solid block named — the cheap
textual form that verifies a `layered` material's stack, a wall's courses or a stamped room's floor; `--section`
draws the same information as an axis-aligned slice, so a riser, a ramp's step heights, a building's storeys
and a void column are all visible in one picture rather than inferred from a plan view that has already
discarded Y. Both read `PgmStudio.Minecraft.Render.ColumnReport`/`SectionRender` exactly like the renderers
above — a region directory or an in-memory `VoxelWorld`, no second load.

`--structures` finds a building by the material on top of each column when no recorded provenance is
available, because elevation alone cannot tell a hut from a boulder. That test used to be the *only* one: any
two neighbouring built columns joined into one component whether or not either was made of the same thing, so
a roof's edge touching the plaza it stands over fused into the plaza, and a themed map's own ground — painted
in the same palette it built with — fused into whatever stood on it. `B124` narrowed that: the flood also
requires the two columns' tops to be within `--max-step` of each other (default 4, the same discipline
`--buildings` already applies to a roof), which measurably separated real structures on Ashen Quarry — two
roughly 9,000-cell "town square" components shed 1,550 cells of roof that had been touching the plaza
directly. What the step could not reach was its own stated limit: a roof laid flush with the plaza's own
paving height, since nothing steps between two flat surfaces of one material.

**`B133` closes that limit rather than narrowing it further.** When a `WorldProvenance` is available,
`--structures` stops asking "built, and within a step of its neighbour" and asks "did a stamp claim this
column" instead — a candidate column is one the build itself recorded as `Structure`, whatever it is made of
and however level it sits against the paving beside it, so the step test is dropped entirely (a tall roof's
eave and ridge can differ by more than any step without being cut into two components, since the recorded
extent already says they are one building). A stone-brick cottage on a stone-brick plaza is two different
things because two different passes put them there, and a roof flush with its own paving cannot fuse with it
because the paving was never a candidate at all. Reproducing Ashen Quarry's own plan through the current
pipeline (its original build predates this recording and cannot be retrofitted) makes the gap concrete: read
by material and step alone, the whole board — town tier, mesa hull, quarry bowl — floods into three
components, one of them 80,722 cells, because the entire board was painted in one stone-brick theme; read by
recorded provenance, the same world reports seven structures matching exactly what was stamped (two spawn
cubes, two destroyable platforms, two goal markers, one iron cube), the largest 133 cells. The step test
still governs a world with no recorded provenance — a scanned map, or one built before this recording
existed — where material is the only signal there is, and it is unchanged from `B124`.

**The prototypes** render the model rather than a map. `tools/relief` emits ten figures plus a topographic
view, a blocks-from-an-angle view, a section and a step map, and `--corpus` measures real worlds into the
same terms. `tools/compose` holds twenty-two galleries — boards, bodies, boxes, edges, mids, seeds, hubs —
each rendering the real emitters.

The rule that follows: **a stage that produced something should be looked at before the next stage consumes
it.** A theme is checkable before a world is built, a plan is checkable before it compiles, a shape is
checkable before it is drawn, and a forest is checkable in a heightmap rather than guessed from a leaf count.

The rule that follows *that* one, because looking has its own failure mode: **an image is a check, not a
source of meaning.** A render answers whether the thing that was authored came out; the document underneath
answers what it is. Reading semantics off pixels is how a build zone becomes water, and both false-colour
rules downstream of that principle now hold everywhere a stage image is drawn. **Contrast is the
requirement**: every render picks its palette to separate the categories it draws rather than to depict them,
which is why `--topdown`'s default is the five-hue category scheme above rather than the game's own greys, and
why every render carries a legend rather than asking a reader to bring one. **A distinction that must survive
being looked at gets more than a shade**: the plan render colours by **role** — hub violet, spawn green, wool
amber, frontline orange — and a zone by **kind**, a build zone in pink and a water lane in blue with a
diagonal hatch on top of it, because a build zone and a water lane are the same gap with different crossing
rules and the two used to be told apart only by shade, opacity and dash (`B95`). A board whose central build
zone was once read as water off the picture, on a map carrying no water at all, is what the old two-shades-of-
blue scheme cost: the observation was right, the cause was invented, and the invented cause then explained
away a real connectivity result. Read the picture for whether, read the document for what.
