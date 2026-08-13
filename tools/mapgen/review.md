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
a building and never to a board. The surfaces exist — `surface.md` beside this file is the map of them — and
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

Two entries below are what stand between the tool and that, and both belong at the top rather than buried:

- **MG29 — the spec format is a reduction of the system, not an addressing layer over it.** It invented a
  small vocabulary and hid the rest, so a shape became a footprint, a theme became four family names, and
  every board came out with a rim, one theme and the same wall — because those were the only things the
  format could say. A description-driven author needs to reach the real documents, not a simplification of
  them.
- **MG30 / MG34 — a map was composed in one shot and judged at the end.** A layout is meant to be built up
  **layer by layer** — pieces, then their shaping in the sketch tool, then heights, then paint, then relief,
  then dressing — with each step looked at before the next is laid on it. Fifteen boards were emitted whole
  and inspected once, from a single top-down, after every decision had already been made.

## Start here — what actually breaks a map

The entries below are in pipeline order, not in severity order, and they are not equal. Most are the
difference between a rough map and a good one. **Six are the difference between a map and a broken map**, and
they are what to fix first — each one ships a board that cannot be played as intended, and four of them make
one that cannot be won at all.

| | Breaks | Why it is fatal |
|---|---|---|
| **MG18** | every destroy board | The goal defaults to obsidian and the kit carries an iron pickaxe, which does not mine obsidian at all. The monument cannot be broken, so the map cannot be won. 86 of 86 corpus maps with an obsidian goal ship a diamond pickaxe. |
| **MG3** | any goal over void | An objective with no ground under it cannot be reached or mined. Unwinnable, and silent — the map builds and loads. |
| **MG14** | every generated map | The export composer is bypassed, so a map ships with no `itemkeep`, `toolrepair`, `itemremove` or hunger rule — and without the reordering that puts `not-build-area` last, which is the rule holding players out of the void. It therefore feeds MG3. Fixing it is one call, and it closes MG15, MG16 and MG17 with it. |
| **MG5** | spawn and wool rooms | The relief is solved after the rooms are sited and moves the ground under them. Carving *below* a room leaves its floor cut or hanging. |
| **MG4** | spawns on a piece edge | The yaw decides which wall the door is cut through, so a spawn facing outward opens its only exit over the drop. |
| **MG7** | structures everywhere | Buildings stamped inside buildings and trees grown through both. Not merely ugly — a structure through a room's wall is a hole in it, and a tree through a doorway is a blocked route. |

Two more are wrong on every board and cheap to fix, without making a map unplayable: **MG26** (a mirrored
board whose dressing differs side to side, because each orbit image is decided separately) and **MG20**
(wool-room chests turned into the wall). Everything else is design, and the section above is what it is
measured against.

## How a map is designed

The rest of this document is faults. This section is the frame they are faults against, and it is not a
checklist — it is the reason the checklist exists.

**A layout is a control on player flow.** That is what the plan layer and the composer are for: the voids,
the gaps between pieces and the placement of pieces decide where a player can go, how long it takes and what
they can see on the way. Every later decision inherits it. A board is therefore not a container that scenery
is sprinkled into — the ground *is* the design, and the scenery is a second layer of the same argument.

**The plan's rectangles are a starting point, not the shape.** The composer emits rectilinear pieces to keep
the first pass simple and legible. That is precisely why the pipeline walks into the **sketch tool** next:
the shapes are there to be manipulated — dragged into a swirl instead of a straight run, given Bézier edges,
cut with a subtract, stepped in `base_height`. A capture layout can be as organic as a destroy one. Taking
the compiled rectangles as final (MG34) is taking the scaffolding for the building.

**On a capture map, flow is controlled primarily by void — and the void is the design.** The gaps between
pieces are not what is left over after the ground is drawn; they are the instrument. A destroy map is much
freer, because it does not need a lane structure to carry a wool run, but freer is not the same as
unshaped: it still has to control movement, and a board that controls none is a field.

**A void works on a rectangle too.** Even a large plain rectangular board becomes a designed one by cutting a
hole in front of the objective — a gap of roughly twenty blocks, far enough that it cannot simply be jumped,
though the number is illustrative rather than a law. It need not be a straight edge; an organic polygon reads
as terrain rather than as a wall. What it does is force every attacker to **pass around it**, which is a
decision, a delay and a place a defender can watch.

**Each element creates a specific tactic, and the tactics differ in dimension and in timing.** This is the
part worth reading slowly, because "put a forest there" is not the point — what the forest *does* is:

- **a void hole** in front of the goal makes players go **around**, and turns the two ways round it into two
  approaches a defender must split attention across
- **a hill** is not merely height and sightline: attackers climb to its ledge and **bridge from the ledge
  toward the objective**, arriving from **above**. A defender on the ground has to watch the sky as well as
  the approaches, and the bridge is a visible commitment that takes time to build
- **a forest** gives cover to within a few blocks of the objective, which makes it most valuable **early**,
  when someone can move through unseen and be on the goal quickly. It also gives height a second way —
  a tree can be **climbed** for the same advantage a hill gives
- **a small depression** near the objective is an entrance **below**: a player drops in, tunnels, and comes
  up under the monument, where nobody is looking
- **a river or a drop** forces a **bridge**, which is a chokepoint that has to be built before it can be used
- **a village** gives cover the whole way in and is fought through room by room
- **open ground** exposes, which is what an objective itself wants around it

Read together, those are approaches from **around**, **above**, **below** and **through** — not four
flavours of the same walk. That is the difference between a composed objective and a decorated one.

**So an objective sits exposed, and the ground around it is composed.** A monument or a core in the open, a
forest on one side, a hill on the other, a village behind. That is the whole method in one sentence: the
approach is legible, the defender has somewhere to hold, the attacker has a way to arrive unseen and a price
for using it. None of that survives being scattered.

**And the point of composing it that way is that the approaches differ.** Ringing an objective with
different ground is not scenery variety — it is how a goal comes to have several ways in that are not the
same way twice, and that arrive from different directions in three dimensions. A defender must then choose
what to watch, and an attacker must choose what to pay, which is a decision on both sides rather than one
lane repeated.

The capture side already says this and the destroy side does not. **WL8** records that a wool's default is a
**single chokepoint route** and that real maps add **alternative routes** — and, usefully for a river or a
drop, that an approach crossing a sealed zone counts as an approach even when it has to be *bridged rather
than walked*. There is no equivalent rule for a destroyable or a core, which is exactly where one is needed:
a goal a team defends wants more than one angle onto it, or the defence is a single doorway and the attack is
a queue. Flat ground inside a nice environment is a real style and a legitimate answer; it is just rarely the
better one, and it should be a choice rather than what happens when nobody decides.

**Think what to place where, and why.** If there is no answer to "why here", the thing does not go there
yet. Randomness is the wrong approach for nearly all of it.

## The board

**MG1 — A destroy map needs a board drawn for it, not a capture board with its wool taken out.**
`objective_mode` composes a capture-the-wool board and rewrites its markers: the wool placement the generator
budgeted and sited becomes a monument, and for `dtcm` a core two cells along. The board underneath is
unchanged — the same lanes, the same hub, the same two rooms at the same distance, sized by a budget that was
solving for a wool run. That is not what a destroy map is. The corpus is the evidence and it was read for the
wrong thing: `--island-study` was run over all 368 destroy worlds to count islands and never to look at their
*shape*. The 320 readable ones are sitting there with their footprints, their monument sitings, their
approach geometry and their build regions, and the way to a destroy board is to read them and compose for
them. The retarget is a shortcut that produced fifteen capture boards wearing three different hats.

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

**MG32 — A destroy board is not a capture board with a different goal; the topology is inverted.** In
capture the thing a team wants is deep in *enemy* ground, so the board is built around a long run out and a
longer run back. In destroy the thing a team defends is its **own** monument: the spawn sits remote at the
back, the monument is a short walk forward of it, and the contested space is everything beyond. That single
difference resizes the whole board — the run is shorter, the defended ground is smaller and closer, and the
space between the two teams is correspondingly larger and emptier. Which is also why destroy maps have room
for scenery that capture maps do not, and why retargeting a capture board (MG1) produces something that
plays wrong even when every element is present.

## The objectives

**MG3 — A monument or a core may never stand over void.** Some do. An objective in void cannot be broken:
without a build region under it there is nothing for a player to stand on or mine through, so the goal is
unreachable and the map is unwinnable. This is a hard invariant, not a quality note, and it belongs in the
tool as a refusal — a spec that sites a goal off the ground should fail to build rather than write a board
that cannot be played. The check is cheap: the rasterized ground is already computed before dressing, and the
goal's anchor either has a column under it or does not.

**MG18 — An obsidian goal needs a kit that can mine obsidian, and every destroy map in this batch fails
it.** The plan compiler defaults a destroyable's material to obsidian, which over half the corpus also uses,
and the generated spawn kit carries an **iron** pickaxe. An iron pickaxe does not mine obsidian slowly — it
does not mine it at all, breaking nothing and dropping nothing. So every monument and every core in the
seven destroy boards is indestructible and every one of those maps is unwinnable, in the same way and for the
same reason as a goal standing over void (MG3).

The corpus does not merely prefer the pairing, it never breaks it. Of 312 destroy maps, **86 name obsidian in
a goal's materials and all 86 carry a diamond pickaxe — 100%, no exceptions.** Among the 226 whose goal is
some softer material, 65% carry one anyway, so a diamond pickaxe is common everywhere and *mandatory* where
the goal is obsidian. Only 30 maps carry both a diamond and an iron pickaxe, so the usual shape is a
substitution rather than an addition: the destroy kit is the capture kit with its pickaxe upgraded, which is
what makes it a kit **variant** rather than a second kit.

Two ways to be right, and the tool should do both: pair the kit to the goal material when it builds a destroy
map, and refuse to write a map whose goal material no tool in its kit can break. The second is the one that
survives someone later choosing a different material.

**MG19 — The observer spawn takes no kit.** `ObserverIntent` carries a point and a yaw and nothing else, so
observers arrive with whatever the server hands them. An observer wants its own kit variant — the flight and
the tools for watching a match rather than playing one — and the intent has nowhere to put it.

**MG20 — Wool-room chests face into the wall.** The chests stamped in a wool room are not turned to the room
they open into, so some present their back to the player and can only be opened from inside the wall. A
chest's facing is a block data value and has to be resolved against which wall it sits on and which way the
room is entered — the same question the room's door already answers, which is where the answer should come
from.

**MG4 — A spawn must not face its exit into the void.** The spawn's yaw decides which wall its door is cut
through, and a spawn on the edge of a piece with its yaw pointing outward puts the only way out over the
drop. The direction is settable and is currently inherited from whatever the compiler fanned. It should be
chosen against the ground: face the spawn at the board, not off it.

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

**MG23 — A destroyable wants a bedrock platform under it.** A 5×5 bedrock platform one block beneath the
ground under each destroyable, so the monument cannot be undermined from below and the ground it stands on
cannot be mined out from under the goal. Nothing stamps one today.

**MG24 — Every goal wants a marker in the sky.** A wool room, a destroyable and a core each want a mark high
above them, above build height so no one can reach or grief it — something as simple as a small cube or a
letter picked out in blocks. It is how a player crossing open ground knows where the goal is without a map,
and it is the cheapest legibility a board can carry. Above build height matters: `BuildIntent.MaxHeight`
already caps building, so the marker sits out of reach by construction.

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

**MG5 — A relief must not move ground an objective is standing on, and must never carve below it.** On the
capture maps the solved surface overrides the spawn and wool placements, so a room ends up cut into a slope
the relief invented after the room was sited. Two rules, in strength order: the ground a spawn or a wool room
stands on should be **left alone** by the solve, and failing that it must **never be carved below** the
room's floor — a surface solved downward under a stamped room leaves the floor cut through or hanging over a
hole, which is the version that breaks the map rather than merely spoiling it. The machinery to prevent both
is already there and unused: a shape states `relief_scope` —
`hold` pins it at its own stated top so the surrounding surface is solved *knowing where it has to arrive*,
and `exclude` keeps it out of the solve altogether — and `height_mode` with `skirt` sits a stated platform
into the terrain rather than on it. Every structural shape the plan compiler projects (`spawn-*`, `wool-*`)
is exactly the case those words were written for, and none of them sets either.

**MG6 — Relief and dressing compete, and the competition is currently settled by luck.** Steep ground is
unplantable, so the same spec at a harsher relief silently loses its forest — one scarp took a board from 785
leaves to none. The tool reports the two numbers that distinguish "no site was acceptable" from "the pass
dropped what was offered", but nothing reconciles them: the relief is solved first and the trees take
whatever is left. Deliberate placement (MG9) largely dissolves this, since a chosen grove sits where the
ground was chosen to suit it.

## The dressing

**MG7 — Nothing may be placed inside anything else.** Buildings stand inside buildings, trees grow inside
trees, and trees grow through buildings. The village pass keeps buildings a margin apart and the forest pass
keeps trees a margin from buildings *placed before it*, but the margins are small, the checks are
site-against-site rather than footprint-against-footprint, and the symmetry fan places an orbit image that
nothing tested. Overlap has to be decided against the **occupied cells** of everything already standing,
after fanning, not against the anchor a prop was requested at.

**MG8 — An L or a T house is buildable and unauthorable.** `Footprint` holds wings, `HouseStamper` walks them
as one landmass, and the roof of a building is the union of its wings' roofs with the cross-gable built where
they meet (G172). Nothing can ask for it: `HouseProp.Points` is exactly two opposite corners and
`HouseProp.Footprint()` returns one rectangle, and every `new Footprint(...)` in the tree is single-winged. So
two abutting rectangles in the same style do not merge — they are stamped as two buildings that happen to
touch, with two roofs colliding. Reaching the wing model from the dressing path is the single change that
turns a row of boxes into a village.

**MG9 — A forest is placed, not scattered.** Trees are sampled uniformly over every ground cell that passes a
filter, which is why they read as noise: no groves, no treeline, no clearing, nothing thicker where the map
wants cover and nothing bare where it wants sightlines. Trees are cover, and cover is a gameplay decision. The
same argument applies to buildings, which are currently dropped wherever the ground happens to be level.

**MG26 — The dressing is not symmetric, and the mechanism that breaks it is known.** A mirrored board whose
trees differ side to side reads as broken however good each side is. The dressing pass *does* fan every prop
through the symmetry orbit, but it decides each image independently: `Decorator.Fan` loops the orbit and
runs `Seats` per image, and a `continue` on failure drops **that image only**. So a tree whose mirror lands a
block nearer a protected column, or on ground the relief left slightly steeper, is built on one side and
missing on the other — which is exactly the asymmetry observed. A prop has to be decided **once for the whole
orbit**: seat every image or none. The same applies to any pattern placed per-cell rather than per-orbit.

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

## The XML

**MG14 — The export composer is bypassed, so a generated map carries none of the boilerplate every corpus
map carries.** `tools/mapgen` builds its document and calls `XmlWriter.ToXml(Deserializer.FromDict(doc))`
directly. The path a map is supposed to take is `MapXmlComposer.Compose(doc, isIntent: true, …)`, and
everything it does is therefore missing: `CtwStandards.Apply` (the keep / repair / remove rules derived from
the spawn kit, hunger depletion off, and the shared golden-apple kill-reward include),
`WaterLaneGenerator.EnsureInclude`, `ResourceRenewables.Apply`, `StructureRenewables.Apply`, and the
reordering that puts the `not-build-area` rule **last** — which matters because PGM stops at the first
applicator that decides, and that rule is the one keeping players out of the void (MG3). None of this is
unbuilt: `CtwStandards` derives its lists from the corpus at N=199 and `XmlWriter` already emits all four
elements. The generated maps simply never go through the composer, and one call is most of the fix.

The measure of how wrong that is, over the 365 corpus capture maps outside this batch:

| element | corpus maps carrying it | in a generated map |
|---|---|---|
| `<itemremove>` | 364 (99%) | absent |
| `<toolrepair>` | 345 (94%) | absent |
| `<itemkeep>` | 299 (81%) | absent |
| `<hunger><depletion>off` | 297 (81%) | absent |
| `<armorkeep>` | 3 (1%) | absent, and unwritable |

The hyphenated spellings PGM also accepts — `item-keep`, `tool-repair`, `item-remove` — are used by **no**
corpus map at all, and the writer already emits the unhyphenated forms the corpus uses.

**MG15 — Spawn armour goes in `<itemremove>`, the way the corpus does it.** *(Decided by the author.)*
Leather kit armour currently drops and lies on the ground, because nothing says otherwise. `<itemremove>`
destroys the stack when it spawns as an entity (`ItemSpawnEvent`), so the armour still leaves the body on
death but never litters the field and cannot be worn by the killer; it works because the kit re-applies
team-coloured armour on respawn. That is the convention at 99% of the corpus, and it is already what
`CtwStandards` derives — `m.ItemRemove = kit.Armor.Select(a => a.Material)`. So this entry needs no new code
beyond MG14: routing through the composer produces it. The alternative, `<armorkeep>`, keeps the armour on
the body and is not being taken — three maps in 365 use it, and the writer could not emit it anyway, having
no armour list.

**MG16 — Tools that wear out are meant to be repairable, and a generated map repairs nothing.**
`<toolrepair>` lists materials whose pickup repairs the tool already in the inventory rather than stacking a
second one — picking up a sword restores the held sword's durability by the picked-up one's remaining hits
and the pickup is cancelled. Without it a kit's sword, axe and pickaxe simply break and the player is
disarmed until the next death. 94% of the corpus carries it; `CtwStandards` already derives the list as the
kit's tools and weapons, identified by the material's last word.

One thing to check while wiring it, because the two overlap: the generated spawn kit marks its tools
`unbreakable="true"`, and an unbreakable tool never wears down, so `toolrepair` has nothing to repair on it.
The corpus carries both at once, which suggests the two are belt and braces rather than alternatives — but
whether the generated kit should keep `unbreakable` once `toolrepair` is present is a real question and
should be answered against what the corpus kits do, not assumed.

**MG17 — The derivation is silent when a map has no kit.** Everything `CtwStandards` derives sits behind
`if (m.Kits.FirstOrDefault() is { } kit)`; only the kill-reward include and hunger-off happen
unconditionally. Every map in this batch carries a spawn kit, so MG14 alone fixes them — but a kitless map
would pass through the composer and come out with no keep, repair or remove rules and no warning that its
loadout rules are missing. Worth a report line rather than silence, since the elements are near-universal.

## The tool itself

**MG29 — `MapSpec` is a smaller system wearing the big one's clothes.** The spec format was invented rather
than derived: it takes a handful of knobs, names them, and hides everything else the documents underneath can
say. A shape becomes a footprint when it also carries its own theme, floor, base height, per-vertex anchor
heights, `height_mode`, `skirt` and `relief_scope`. A theme becomes four family names and a pattern when
`TerrainTheme` holds a rim band, a surface band with its own depth, a wall, a fill, and a per-shape scope. A
relief becomes `scatter` when the mark vocabulary has five kinds. The tell is in the output: every one of the
fifteen boards has a rim, one theme, one relief style and the same wall treatment, because those were the
only shapes the format could express. The fix is not more knobs — it is that a spec should be a **thin
addressing layer over the real documents**, able to hand through a `SketchShape` or a `TerrainTheme`
verbatim, with the convenience fields as shorthand that expands into them rather than as the whole surface.
That is also what a prose-described map needs: a description names intent — *a stepped plateau in coursed
stone, natural ground falling away west* — and the author turns it into shapes, heights and themes. It cannot
do that through a format whose whole vocabulary is one theme and a rim.
`surface.md` beside this file is the reference that was missing, and `tools/seeds/ruediger.layout.json` is a
hand-drawn map that uses the layout format to its width — three themes chosen per shape, ten `base_height`
tiers stepping the ground with no relief block at all, Bézier outlines, a subtract, and the defence walls of
MG21 actually authored.

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
exist and were not used — `--heightmap`, `--contour`, `--surface`, `--traversability`, `--buildings`,
`--structures` in `tools/PgmStudio.RoundTrip`, and the relief prototype's own topographic, blocks-from-an-
angle, section and step-map renders in `tools/relief`. Several of the faults above would have been visible in
the first one of those that was run.

## A second run, from the outside

The entries above were written from sixteen boards by the same hands that built them. A second run tests them
the other way round: a small model that had never seen the codebase, given the prose brief this document's own
target section describes — a destroy board, the monument in the open, a forest closing the west flank, a hill
east to bridge from, a village behind, a void channel twenty blocks in front — and told to author the spec,
build it, look at the renders, and iterate. It took six passes and reported what it could not say.

It reported five of the six as walls. **Two of the five are not walls, and the spec reaches both.** A hill at
a stated place is one hand-written relief mark: `marks` is handed through the spec verbatim in the stored
relief's own vocabulary, and the README lists all five kinds with their parameters in the paragraph directly
beneath the block the run quoted from. A village behind the monument is the `houses` list, which places a
named preset at a stated `x`/`z` with a stated facing, documented on the same page. Both were checked by
building them: a `point` mark asked for at x −60, z −20 with radius 26 and height 14 lands its high band's
centroid at x −59.5, z −20.3, its orbit image at x +59.0, z −20.4, and the surface histogram's two dominant
bands are the two heights the marks stated; four buildings placed by hand all raised, none in void. The run
reported these as impossible while quoting the documentation that describes them.

**Three are real, and none of them is a missing capability.** No spec field weights where trees go, so a
forest cannot be leaned onto one flank — though the dressing document beneath places every tree at an
explicit coordinate, which is what the spec's population form is a shorthand for. No spec field reaches a
shape, so the paint cannot differ per shape (MG2) and a **void channel cannot be cut** — and that last is
worth reading closely, because the run also named the wrong layer for it. A channel is not relief and no mark
of any kind makes one: it is a **negative shape**, an outline carrying `operation: subtract` standing tall
enough to take the whole column, which *removes* ground rather than lowering it. `ruediger.layout.json` cuts
one with a rectangle at `base_height` 100 over `floor` 0, and it is the instrument this document names as the
primary control on flow for a capture board. The run searched the relief block, because that is where the
spec keeps elevation and the spec has no word for a shape at all.

So the honest reading is not that the system cannot say these things. **Everything in the brief is
expressible**, and three of the six only one layer down, in documents `surface.md` maps and the studio's own
tools author. What the run measured is narrower and more useful: **a surface that exposes a reduction teaches
the reduction as the boundary.** Handed a format whose vocabulary was one theme, a scatter and a rim, an
author took the vocabulary for the system — reporting a wall where a paragraph of the README stood, and
reporting the absence of a shape against the feature next to it. That is the cost MG29 names, priced: not
lost capability, but an author who cannot tell a capability that is missing from one that is merely out of
reach, and who therefore stops asking. The fix it argues for is the same either way — a spec that is a thin
addressing layer over the real documents, with the convenience fields as shorthand that expands into them —
and the case for it is stronger, not weaker, for the capabilities having been there all along.

**What it got wrong is worth more than what it got right.** Two faults were reported that a controlled rerun
does not reproduce. A pass drawn `grown` with `whorled` was reported as building trunks with no crown; over
one board at sixty sites, spruce between 8 and 14 with the village off, whorled lands 1136 leaves against
plain grown's 1102 and template's 1846, so the whorl is within noise of not being set. And template was
reported as an order of magnitude denser than grown as a property of the form; at equal height it is 1.6×.

There is a likely mechanism for the second class of error, and it is worth naming because the pictures were
added to prevent exactly this. The plan render colours by **role**, and a zone — a build zone or a water lane
— is drawn in **blue**, the two separated only by shade, opacity and dash. Blue reads as water to anyone who
has the image and not the key, and the board in question carries no water whatever: its only include is the
kill reward. So a true observation (two components) acquired an invented cause (a water lane) that happened
to make the observation sound resolved. `B95` is that gap; the rule it argues for is that an image answers
whether something came out and never what it is.

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
