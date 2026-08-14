# What the first fifteen maps got wrong

A review of the boards under `CommunityMaps/ctw` and `CommunityMaps/dtcm` built by `tools/mapgen` from the
specs in `specs/`, read against what the corpus actually ships. It is a **pool, not a board**: the entries
carry doc-local `MG` ids so one can be referred to, and an entry moves onto `TODO.md` with a real id when it
becomes the focus. The order is the pipeline's — board, objectives, ground, paint, dressing, then the reading
back — rather than the order the faults were noticed.

The common thread is worth stating once, because most of the entries are instances of it. The tool reaches
for a **random** answer wherever an author would reach for a **deliberate** one: where a tree goes, which
wood it is cut from, where a building stands, what the ground does, which of the nineteen palette families
paints a wall. Randomness is the right tool for the grain on a surface and the wrong one for everything a
player navigates by. A map is a designed thing, and every one of these is somewhere the design was left to a
seed.

## What this should be able to do

The target is worth stating before the faults, because most of them are it being missed. **A map should be
describable in prose and built from that description** — an agent reads "a destroy board, one connected
island, the monument in the open with a forest closing the west flank, a hill east that attackers can bridge
from, a village behind, a void channel twenty blocks in front" and authors the documents that say it: the
shapes,
their heights, the themes on each, the relief, the dressing, the objective and its kit.

**That is already possible, and it has already been done.** The house presets in `HousePresets` were authored
exactly this way — described in prose and built from the description, down to "seven courses between spruce
log posts that stand the full height, the bottom two cobble and andesite mixed" — and they work. Their
docstrings are the briefs they came from. So the method is not a proposal; the gap is that it was applied to
a building and never to a board. The surfaces exist — `capabilities.md` beside this file is the map of them — and
nothing above needs a capability that has not been built. What it needs is an author that knows the surfaces
it is touching. An MCP head (`B21`) would make it a first-class loop with the validator and evaluator
answering in rule ids, and some work remains there, but the shortfall today is not the machinery.

**It is a reviewed loop, not a single shot.** Iterations are expected: a pass is produced, a human reads it
and says what is wrong with it — this relief is too harsh here, that plan puts the wool behind the spawn —
and the next pass answers that. Which is why building in layers (MG30) matters beyond tidiness: a pass is
only reviewable if it is small enough to have an opinion about, and only correctable if the passes after it
have not already been laid on top.

Where this goes, stated as direction rather than as something that exists: the critique becomes **spatially
anchored** — an area marked with a rectangle and a sentence about what is wrong inside it. Text carries that
today (a rect and a note is a rect and a note), so nothing blocks it; it is a question of the loop being
built around it.

One entry below is what stands between the tool and that, and it belongs at the top rather than buried
(the sibling entry, MG29, named the same gap in the spec format and closed with `B118`, which made the spec a
thin addressing layer over the real documents instead of a reduction of them):

- **MG30 / MG34 — a map was composed in one shot and judged at the end.** A layout is meant to be built up
  **layer by layer** — pieces, then their shaping in the sketch tool, then heights, then paint, then relief,
  then dressing — with each step looked at before the next is laid on it. Fifteen boards were emitted whole
  and inspected once, from a single top-down, after every decision had already been made.

## What broke a map, and what closed it

Eight entries were the difference between a map and a broken map rather than between a rough map and a good
one, and they are gone from the pool because they are fixed. They are recorded here as a list rather than as
entries, because the shape of that set is the useful part: six of the eight shipped a board that could not be
played as intended, four shipped one that could not be won at all, and every one of them was silent — the map
built, loaded, and looked correct from above.

| was | closed by | what it shipped |
|---|---|---|
| MG18 | `B81` | every generated destroy board shipped the fixed iron-pickaxe kit regardless of goal material, believed at the time to make an obsidian monument unbreakable |
| MG3 | `B82` | a goal standing over void, unreachable and unminable |
| MG14, MG15, MG16, MG17 | `B80` | no `itemkeep`, `toolrepair`, `itemremove` or hunger rule, and `not-build-area` not last |
| MG5 | `B83` | a relief solved under a sited room, cutting its floor |
| MG4 | `B84` | a spawn whose only exit opened over the drop |
| MG7 | `B85` | buildings inside buildings and trees through both |
| MG26 | `B86` | a mirrored board whose dressing differed side to side |
| MG20 | `B87` | wool-room chests turned into the wall |

**MG18's row is corrected, not merely historical.** The entry judged an unpaired kit unwinnable; that
judgment was wrong. An iron pickaxe breaks obsidian, it just does not drop it, and a destroy goal only
requires the block gone — a mismatched kit makes a raid slow, not impossible. `B81` still closed a real fault
(every generated destroy board shipped the same fixed iron pickaxe, obsidian goal or not), and its own kit
derivation is still in place; what `B81` also shipped on top of the derivation — a hard refusal on a
mismatch, later carried into the export gate as `OB18` (`B116`) — was built on the false premise and was
removed by `B134`.

Two more that were not faults but absences closed with them: a destroyable now stands on a one-block bedrock
platform (MG23, `B88`) and every goal carries a marker above the build cap (MG24, `B89`).

What remains below is design. It is the difference between a rough map and a good one, which is a real
difference, and the section that follows is what it is measured against.

## How a map is designed

The rest of this document is faults. The frame they are faults against — what a void, a hill, a forest, a
depression, a village and open ground each do to a match, and why an objective sits exposed with composed
ground around it — is `docs/gameplay/approaches.md`. It was moved there because every claim in it is a claim
about **play**, which neither the corpus nor the code can settle, and mixing that kind of statement into a
list of defects is how an unreviewed opinion becomes a rule. Each claim there carries whether the author has
confirmed it, and every claim it currently carries is confirmed.

Two of its sentences are the ones the entries below are measured against, and they are worth having here:
**a layout is a control on player flow**, so the ground is the design rather than a container for scenery;
and **think what to place where, and why** — if there is no answer to "why here", the thing does not go there
yet. Randomness is the right tool for the grain on a surface and the wrong one for everything a player
navigates by.

## The board

**MG1 — closed by `B118`.** `objective_mode` used to compose a capture-the-wool board and rewrite its
markers — the wool placement the generator budgeted and sited became a monument, and for `dtcm` a core two
cells along, with the board underneath unchanged: the same lanes, the same hub, the same two rooms at the
same distance, sized by a budget that was solving for a wool run. `Retarget` is gone, and `tools/mapgen` has
no mechanism left that turns a wool marker into anything else — a destroy board is authored as its own
`plan`, a destroyable or a core placed on whichever piece the design wants rather than the piece a wool
budget sited.

What MG1 also named and `B118` does not deliver is the other half: composing a board **for** a destroy
objective's own topology (`MG32`) rather than requiring the whole plan to be drawn by hand. The corpus
reading behind that stands as MG1 left it — `--island-study` was run over all 368 destroy worlds to count
islands and never to look at their *shape*; the 320 readable ones are sitting there with their footprints,
their monument sitings, their approach geometry and their build regions, unread for shape. That reading is
`B106`'s starting point, and any future destroy-native composer's.

**MG31 — Where a spawn and a goal sit relative to each other is already law, and a hand-built board must
carry it too.** The composer never places a wool or a spawn freely, and the reasons are written down in
`docs/generator/rules.md` rather than buried in it: the frontline→wool path never passes **through** a spawn
(SP1); a spawn sits near the **back** of its lane, because the space behind a spawn is dead space (SP2); iron
goes **beside or ahead** of a spawn, since players face forward (SP7); a wool sits at the far end of a
dead-end lane inset about 5 (WL1), on a **different lane** from the spawn and at least **20** away — all 17
corpus pairs (WL2); a team's wools are 1–3, each on a distinct lane (WL6), with a measured separation between
them (WL7). Those relations are what make a board readable: a goal you can see the way to, that is not behind
you when you leave your spawn, and that no one reaches by walking through a protection region. **A custom
destroy board gets none of this for free**, so the rules have to be carried across by hand — sightline to
the objective, the objective not behind the spawn, the approach not through the spawn — and MG1's
corpus reading is where the destroy-side numbers come from.

**Measured, and nothing carries them across.** A hand-authored board broke both of the spawn rules named
here: Haiku CTW Rush's spawn point sits 11 blocks from its piece's back edge and 19 from its front, against
SP2, and its iron cube stands five blocks *behind* the spawn point — and inside the map's own `red-spawn`
protection rectangle, so it is a contested resource nobody may contest — against SP7. The rules are written,
they are cited here, and no gate applies them to an authored plan, which is the same shape as `B109`.
`B177` carries this, and it supplies the rule id `B169`'s dead-ground entry was missing: SP2.

**MG32 — A destroy board is not a capture board with a different goal; the topology is inverted.** In
capture the thing a team wants is deep in *enemy* ground, so the board is built around a long run out and a
longer run back. In destroy the thing a team defends is its **own** monument: the spawn sits remote at the
back, the monument is a short walk forward of it, and the contested space is everything beyond. That single
difference resizes the whole board — the run is shorter, the defended ground is smaller and closer, and the
space between the two teams is correspondingly larger and emptier. Which is also why destroy maps have room
for scenery that capture maps do not, and why the retarget shortcut `MG1` closed played wrong even with every
element present: it kept a capture board's topology under a destroy goal.

## The objectives

**MG19 — The observer spawn takes no kit.** `ObserverIntent` carries a point and a yaw and nothing else, so
observers arrive with whatever the server hands them. An observer wants its own kit variant — the flight and
the tools for watching a match rather than playing one — and the intent has nowhere to put it.

## The structures

`StructureIntent` is the stamped furniture a map is played through — room floors, entrance redstone, iron
cubes and approach walls. Two of its four lists are complete end to end and reachable only by hand: the
**layout generator never authors them**, so they exist on maps someone drew and on no map it composed.

**MG21 — The defence wall is fully authorable and the composer never asks for one.** The chain is whole. A
plan carries `walls` as a list of `PlanWall`, each naming the two pieces (`a`, `b`) whose interface it
stands on; the plan editor authors one by clicking that interface with the brick tool; `PlanValidator` checks
it; `PlanCompiler` computes the footprint from the two pieces' contact, fans it through the orbit and emits a
`WallStructure`; the stamper builds it and `DressingScope` protects the ground under it. What no code does is
put a `PlanWall` into a plan the **composer** produced, so a generated board has no wall unless a person
opens it in the editor and marks the seam. The work is in `Composer` — deciding which contacts deserve a wall
— and not in building a wall system, which is done.

Worth reading before touching it, because it settles a question MG26 raises elsewhere: the wall is two blocks
thick so one face can be opened while the other stays solid, and which face carries its defence chests is
authored as a **piece id** rather than a coordinate, precisely so it survives the orbit — a reflection swaps
which face has the smaller coordinate, and only the piece it looks out at is invariant. That is the pattern
every fanned decision should follow.

**MG22 — Iron is authorable the same way and equally unasked-for.** `PlanPlacements.Iron` carries the
markers, `PlanCompiler` turns each into an `IronCube`, and `StructureStamper.StampIronCube` builds the 4×4×4
cube on the surface (ST2/ST3). The composer emits no iron markers, so no generated board carries a resource
to fight over. Iron wants placing regularly through the map rather than once near a spawn — it is a reason to
leave cover, which is the same argument as MG9 about trees.

## The paint

**MG2 — A single blanket theme wastes the one thing the layout already gives away.** Every spec paints one
`theme` over the whole map, so a board reads as one material from edge to edge. A shape carries its own
`theme` and its own height, and the two together are how a board says *this part is built and that part is
grown*: a grid-like quarter laid in coursed stone against a quarter of natural ground, a plateau that is
plainly a platform against a slope that is plainly a hillside. Deliberately differing the paint per shape,
keyed to what that shape is for, is available now and unused. `tools/seeds/ruediger.layout.json` is the
worked example: three themes across twenty-six shapes, so its stepped area reads as built and the ground
around it reads as ground.

**MG25 — The whole world is one biome, so grass and water are one colour everywhere.**
`AnvilRegionWriter` fills every chunk's biome array with a single value — `Array.Fill(biomes, (byte)1)`,
plains — so every biome-tinted block on the map takes the same tint: grass, leaves, vines, sugar cane, lily
pad, tall grass and water. Biomes are a second paint layer that costs one byte per column and is currently
spent on nothing. Painting them over the map's whole bounding box with the patterns the terrain painter
already has — voronoi for patches, noise for drift — gives grass that varies from cold-dry to swamp-dark and
water that runs from blue to green, under and independent of the block palette. Note the renderer follows
separately: `BlockPalette` multiplies biome-tinted blocks by a fixed temperate tint precisely because a
static render has no biome to read, so a top-down would need to read the array to show the result.

## The ground

**MG6 — Relief and dressing compete, and the competition is currently settled by luck.** Steep ground is
unplantable, so the same spec at a harsher relief silently loses its forest — one scarp took a board from 785
leaves to none. The tool reports the two numbers that distinguish "no site was acceptable" from "the pass
dropped what was offered", but nothing reconciles them: the relief is solved first and the trees take
whatever is left. Deliberate placement (MG9) largely dissolves this, since a chosen grove sits where the
ground was chosen to suit it.

## The dressing

**MG8 — An L or a T house is buildable and unauthorable.** `Footprint` holds wings, `HouseStamper` walks them
as one landmass, and the roof of a building is the union of its wings' roofs with the cross-gable built where
they meet (G172). Nothing can ask for it: `HouseProp.Points` is exactly two opposite corners and
`HouseProp.Footprint()` returns one rectangle, and every `new Footprint(...)` in the tree is single-winged. So
two abutting rectangles in the same style do not merge — they are stamped as two buildings that happen to
touch, with two roofs colliding. Reaching the wing model from the dressing path is the single change that
turns a row of boxes into a village.

**MG9 — A forest is placed, not scattered.** `tools/mapgen` no longer samples trees or buildings onto the
ground at all — `B118` deleted the sampler outright, along with `trees`/`village`/`houses` and the passes
behind them, because the studio's own design carries no scatter (`docs/tools/sketch.md`). What replaced it is
narrower than the fix this entry asks for: a tree or a building is now authored one at a time, an exact
coordinate written into `dressing.props` through the `layout` fragment `MapSpec` hands through verbatim. That
states *where*, deliberately, but nothing yet states *why* for a whole stand at once — no groves, no
treeline, no clearing, nothing thicker where the map wants cover and nothing bare where it wants sightlines is
composed on an author's behalf; each prop is still one line, placed by hand. Trees are cover, and cover is a
gameplay decision — the composition half of that decision, for a group rather than a single prop, remains
unbuilt.

**MG33 — Every house is the same house.** The buildings on all fifteen boards read as one shape repeated,
because they very nearly are: the presets cluster around 7–13 wide by 7–11 deep, the specs draw from a
handful of them, and each is placed at the size it was designed at. A settlement is made of buildings that
differ — a long low hall beside a tall narrow tower beside a squat outbuilding — and the difference an eye
reads first is **aspect ratio and height**, not material.

The system is modular far past what was used. A `HouseStyle` carries a roof form and pitch, an overhang and a
verge, a wall built of stacked `RoomCourse` bands, a floor, posts, a sill, window styles with their own form,
width, height, sill and spacing, separate gable windows, a door head, beams, and a storey stack — and a
`Footprint` carries wings, so a building can be an L or a T with its roofs merged (MG8). None of that varies
across the batch. Placing a village round a destroy map's monument (MG32) is the case that most wants it: a
dozen buildings of one silhouette is a barracks, and a dozen of differing silhouette is a village.

**MG27 — A house is empty.** Buildings are shells with nothing inside. Chests with a small random loot table
give a player a reason to enter one, and turn scenery into a place worth crossing the map for. Loot wants to
be modest and consistent with the map's own kit, and — see MG26 — identical across the orbit, since a chest
that is richer on one side is a competitive advantage rather than decoration.

**MG28 — A conifer stands on a stump.** A grown tree drawn `whorled` puts its lowest branches near the base
but its stem stops short, so the crown sits on a stub rather than running through it: the trunk never reaches
below the topmost leaves and the tree reads as a bush balanced on a post. A conifer's whole silhouette is a
continuous stem with rings of branches getting shorter up it. This is in the grower rather than in mapgen —
`TreeSkeleton` and `TreeCrown` in `PgmStudio.Geom` — and it is measurable the same way `B78` is, through
`tools/tree-corpus/grower-gate.cs`, which already scores limb angle and reach against the 75 hand-built
trees and would score stem extent beside them.

**Reopened, and the note that closed it counted the wrong thing.** This entry was marked *does not
reproduce* on the evidence that "whorled lands 1136 leaves" over sixty sites. An absolute leaf count cannot
distinguish a leafy tree from a wooden one, which is the question. Measured over one board, one species and
one build on `tallow-mirefast`: five `grown` + `whorled` trees give **228 logs to 287 leaves — 1.26 leaves
per log, 46 logs a tree**, against three template spruces beside them at 42 logs to 222 leaves, 5.29 leaves
per log, 14 logs a tree. The whorled form builds four times the wood for the same canopy, so
"mainly logs and no leaves" is the right description and is now a number. This is `B96`'s fault exactly — a
count standing in for a ratio — recurring on a different subject and closing a real defect. `B174` carries
the evidence, and neither this entry nor `G173` closes again on a leaf count.

**MG10 — A tree's parts must agree with each other.** An oak-profiled tree is being built with spruce logs.
The `template` form takes a `Species` — which names the wood, the canopy profile and the proportions
together — and the `grown` form takes a `Wood`; the tool draws from one list of names for both and hands a
species name to a wood field. The result is a tree whose silhouette and timber come from different plants.

**MG11 — Density is a design decision and is currently a number nobody chose.** One board came out at 17,629
leaves, a closed canopy with the terrain, the buildings and the routes all buried under it; another at 173,
which is a bare map with a shrub on it. Roughly 1,000–5,000 on a board of this size reads as wooded. The
README now says so, but nothing enforces or even reports against a target.

**MG12 — The wall decoration is the same everywhere.** Every spec dresses its risers the same way, so every
board's edges read identically whatever else changes about it. The pattern vocabulary — voronoi, cell, noise,
turbulence, electric, over nineteen families — is barely sampled, and the choice is not tied to what the wall
is (a cliff face, a built retaining wall, the side of a platform).

## The tool itself

**MG29 — closed by `B118`.** `MapSpec` used to be a smaller system wearing the big one's clothes: a spec
format invented rather than derived, naming a handful of knobs and hiding everything else the documents
underneath could say — a shape reduced to a footprint though it also carries its own theme, floor, base
height, per-vertex anchor heights, `height_mode`, `skirt` and `relief_scope`; a theme reduced to four family
names and a pattern though `TerrainTheme` holds a rim band, a surface band with its own depth, a wall, a
fill, and a per-shape scope; a relief reduced to `scatter` though the mark vocabulary has five kinds. The
tell was in the output: every one of the fifteen boards had a rim, one theme, one relief style and the same
wall treatment, because those were the only shapes the format could express.

`B118` deleted the site sampler this reduction had grown alongside (`trees`/`village`/`houses`, MG9's own
symptom) and made the spec a **thin addressing layer over the real documents** instead: `plan`, `layout` and
`intent` are handed through verbatim, and the convenience fields that survive — `theme`, `relief`,
`room_shell` — are shorthand that expands into a fragment of one of those three rather than the whole
vocabulary. A spec can now add a `SketchShape` with its own theme, floor, base height, per-vertex anchor
heights and `relief_scope`; a `TerrainTheme` with its rim band and per-shape scope; a relief mark of any of
the five kinds; a `MapIntent` fragment reaching whatever the plan it compiled from did not carry. That is
also what a prose-described map needs: a description names intent — *a stepped plateau in coursed stone,
natural ground falling away west* — and the author turns it into shapes, heights and themes through the
document that already has the words for them, not through a spec that renamed a handful.
`capabilities.md` beside this file is the reference `MapSpec` should have been written against from the
start, and `tools/seeds/ruediger.layout.json` is a hand-drawn map that uses the layout format to its width —
three themes chosen per shape, ten `base_height` tiers stepping the ground with no relief block at all,
Bézier outlines, a subtract, and the defence walls of MG21 actually authored.

**MG34 — The sketch stage was skipped as an authoring step.** mapgen composes a plan, compiles it and builds
the world, so whatever rectangles the compiler emitted are what got built. But the compiler emits
rectilinear pieces *to keep the first pass simple*, and the sketch tool is the stage where they stop being
rectangles: an edge takes Bézier `controls`, a run becomes a `path`, a shape is cut with a `subtract` or
stepped by `base_height`. Compiling straight through to the world treats the scaffolding as the building,
which is why fifteen boards look like plans rather than like places — and why a capture layout came out as
rectangular as a destroy one, when neither has to be.

**MG30 — Nothing was looked at between the stages.** Fifteen maps were built and judged from one top-down
render each, at the end, after everything had already been decided. The system renders itself at every stage
and none of it was used: thirteen preview endpoints answer a theme, a material, a prop, a building or a plan
without building a world; the round-trip harness reads a built world back as a heightmap, a contour, a
surface, a traversability map or a structure map; the relief prototype draws a section and a step map. Every
fault in this document that is about *appearance* — the rim everywhere, the identical walls, the asymmetric
trees, the closed canopy, the dark map with no silhouette — was visible in an image nobody rendered. The
working rule is that a stage which produced something gets looked at before the next stage consumes it.

The other half of the same fault is that the layout was never **built up in layers**. A board is meant to
accumulate — pieces placed, then reshaped in the sketch tool, then given heights, then paint, then relief,
then dressing — each pass laid on something already read and accepted. Emitting a whole board in one call
and rendering it once at the end means no pass was ever chosen in response to what the pass before it
actually produced, which is why nothing on these boards answers anything else on them: the trees do not know
where the hill is, the paint does not know where the steps are, and the relief does not know a room is
standing there (MG5).

## Reading it back

**MG13 — The maps were judged from one view.** Every map was checked with `--topdown` at three pixels to the
block, which shows the plan and hides everything about the third dimension: whether a drop is walkable,
whether a room's floor sits where the relief left it, whether a goal has ground under it. The other readings
exist and were not used — `--heightmap`, `--contour`, `--surface`, `--traversability-map`, `--buildings`,
`--structures` in `tools/PgmStudio.RoundTrip`, and the relief prototype's own topographic, blocks-from-an-
angle, section and step-map renders in `tools/relief`. Several of the faults above would have been visible in
the first one of those that was run.

## A second run, from the outside

The entries above were written from sixteen boards by the same hands that built them. A second run tests them
the other way round: a small model that had never seen the codebase, given the prose brief this document's own
target section describes — a destroy board, the monument in the open, a forest closing the west flank, a hill
east to bridge from, a village behind, a void channel twenty blocks in front — and told to author the spec,
build it, look at the renders, and iterate. It took six passes and reported what it could not say.

It reported five of the six as walls. **Two of the five were not walls even in the format the run was handed
— the old, reduced `MapSpec` this section predates `B118`'s fix.** A hill at a stated place was one
hand-written relief mark, handed through the spec verbatim in the stored relief's own vocabulary, documented
in the paragraph directly beneath the block the run quoted from. A village behind the monument was a list of
placed buildings, each a named preset at a stated `x`/`z` with a stated facing, documented on the same page.
Both were checked by building them: a `point` mark asked for at x −60, z −20 with radius 26 and height 14
lands its high band's centroid at x −59.5, z −20.3, its orbit image at x +59.0, z −20.4, and the surface
histogram's two dominant bands are the two heights the marks stated; four buildings placed by hand all
raised, none in void. The run reported these as impossible while quoting the documentation that describes
them.

**Three were real, and none of them was a missing capability.** No spec field weighted where trees went, so a
forest could not be leaned onto one flank — though the dressing document beneath placed every tree at an
explicit coordinate, which is exactly what a spec's addressing layer needs to reach rather than replace. No
spec field reached a shape, so the paint could not differ per shape (`MG2`, still open — the addressing layer
`B118` shipped lets a spec **add** a themed shape but not retarget the theme of one the plan or the composer
already produced) and a **void channel could not be cut** — and that last is worth reading closely, because
the run also named the wrong layer for it. A channel is not relief and no mark of any kind makes one: it is a
**negative shape**, an outline carrying `operation: subtract` standing tall enough to take the whole column,
which *removes* ground rather than lowering it. `ruediger.layout.json` cuts one with a rectangle at
`base_height` 100 over `floor` 0, and it is the instrument this document names as the primary control on flow
for a capture board. The run searched the relief block, because that is where the spec kept elevation and the
spec had no word for a shape at all — and now does: a subtract is a `layout` fragment's shape, added the same
way any other is.

So the honest reading was never that the system could not say these things. **Everything in the brief was
expressible**, and three of the six only one layer down, in documents `capabilities.md` maps and the studio's
own tools author. What the run measured is narrower and more useful, and it outlives the fix: **a surface
that exposes a reduction teaches the reduction as the boundary.** Handed a format whose vocabulary was one
theme, a scatter and a rim, an author took the vocabulary for the system — reporting a wall where a paragraph
of the README stood, and reporting the absence of a shape against the feature next to it. That was the cost
MG29 named, priced: not lost capability, but an author who cannot tell a capability that is missing from one
that is merely out of reach, and who therefore stops asking. `B118` is that fix: `plan`, `layout` and
`intent` handed through verbatim as the real documents, with the convenience fields kept only as shorthand
that expands into them.

**What it got wrong is worth more than what it got right.** Two faults were reported that a controlled rerun
does not reproduce. A pass drawn `grown` with `whorled` was reported as building trunks with no crown; over
one board at sixty sites, spruce between 8 and 14 with the village off, whorled lands 1136 leaves against
plain grown's 1102 and template's 1846, so the whorl is within noise of not being set. And template was
reported as an order of magnitude denser than grown as a property of the form; at equal height it is 1.6×.

There is a likely mechanism for the second class of error, and it is worth naming because the pictures were
added to prevent exactly this. The plan render colours by **role**, and at the time carried a zone — a build
zone or a water lane — in two shades of **blue**, separated only by shade, opacity and dash. Blue reads as
water to anyone who has the image and not the key, and the board in question carries no water whatever: its
only include is the kill reward. So a true observation (two components) acquired an invented cause (a water
lane) that happened to make the observation sound resolved. `B95` closed that gap — a legend on every plan
render and a build zone drawn in a hue no water ever wears — because the rule it argues for is that an image
answers whether something came out and never what it is.

Both tree readings come from the same place, and it is a fault this document already carries. The zero-leaf pass
asked for a **tall** grown tree, and a tall grown tree is not a tall tree but an absent one (`B78`): every
grown prop was dropped, the 136 logs left standing were the village's corner posts, and the run reported
success. So a documented, filed, understood inversion emptied a forest inside three passes, and the reading
back could not name it — after the README warns in as many words that a building's corner posts are logs too
and that the leaf count is a forest's only honest measure. A fault an author reliably misattributes is worse
than one that merely bites, because the correction it invites is to the wrong knob: the next pass changed the
whorl, which does nothing, and left the height, which is everything.

## Still to come

The author is adding to this. New entries take the next free `MG` id and slot into the pipeline section they
belong to.
