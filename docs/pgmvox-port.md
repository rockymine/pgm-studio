# Bringing pgmvox into the studio

**This is the programme that moves pgmvox's capabilities into the studio, one at a time, until an agent driving
pgmstudio.de builds boards of the quality pgmvox builds.** pgmvox is the Python library for freeform boards in
`pgm-studio-mapgen` (`freeform/lib/pgmvox`, 0.30.0 on 15 October 2026). It stays where it is, as the laboratory:
an operation is tried there on real boards, and moves here once it has earned its place. This document says what
moves, in which order, into which part of the studio, how a move is proven, and which decisions are the author's.
It expires when the last phase drains.

The evidence behind it lives in `pgm-studio-mapgen/analysis/freeform-vs-studio/`, and four of its pages were
published: the report (*The Freeform Leap*), the *Studio 2.0 Fahrplan*, the *Recipe Inspector* and the *Grammar
Swatch Book*. Where this document and those disagree, this one is later and wins.

## The goal, and how it is measured

**The goal is a board built through the deployed studio that the author cannot tell apart, by quality, from one
built with pgmvox.** Not the same board — the same ceiling. A person in the browser keeps what the studio gives
today: every level stored, rendered, editable, diffable and handed back to the next agent run.

**The test is the experiment of 10 October, run again at pgmvox's depth.** That experiment held the model, the
brief, the lessons and twenty minutes equal, and the two tools made boards of comparable quality
(`experiment/RESULTS.md`). The gap Riftwater showed came from the open substrate *and* from what was spent on
it: Opus, hours per board, the author's review rounds and bespoke code. So the measure is the expensive run: a
brief built twice at that depth, once on pgmstudio.de and once with pgmvox, and judged by the author.

**A second measure is the playtest's thirty faults.** Of the faults the author found on 10 October, the studio
would have caught nine, partly caught four, and not caught or could not have built seventeen
(`playtest-feedback-2026-10-10.md`). They are a measure, not a work list: **no phase of this programme adds a
playtest check.** Each check is a rule to maintain and a refusal to explain, and the author's review catches
these faults. A check found to be needed later is filed and built on its own, outside the programme.

**Four things made the freeform boards good, and the port has to bring all four.** Operations are only the first.

| | What it is | Where it lands here |
|---|---|---|
| Vocabulary | anything can be built; a missing thing costs 5–60 lines, not a schema change | the ground document, its shapes, modifiers and placements (Phases 2–7), and the made thing as the escape hatch (Phase 8) |
| The plan of places | places with a reason and a way in, numbers against targets, reviewed before a block is placed | the sketch as the one document, its places layer and its review sheet (Phase 1) |
| Looking up close | one picture per place, and sections through it | framed renders (Phase 0) and reads after any step (Phase 2) |
| Accumulation | every board's "what I wanted, how hard" table grew the library | the port protocol below, and the made-thing rule (Phase 8) |

## Where the two stand on 15 October

**The studio's walk and game modes closed most of the gap in `d9abee1` (PR #59).** One reading of what a player
walks through serves every walk (`WS84`); it climbs ladders and vines and swims a water column (`WS85`), takes
the 1.8 running jump tick by tick in `PgmStudio.Geom.Jump` (`WS86`), and reads flag posts and score boxes as
goals (`WS87`, `WS88`). A board states Capture the Flag, King of the Flag, conquest with hold or touch points,
team deathmatch, its match's end, kill rewards, zones, portals and damage (`PG20`–`PG41`). Jump pads are the
one movement piece still open (`PG24`); payload, free-for-all and the arcade courses pgmvox plays in order (a
water drop, a parkour run) are the modes still pgmvox's alone.

**What the studio already does better stays, and pgmvox takes it.** Strata that follow the land (`TP26`), the
slope axis on every band stack (`TP24`), the relief solver for exact heights, the textured eye view with a text
twin, houses with wings, porches and the window/door rule, the stamps (goal markers, build-zone outline,
defence wall, wool-room loot, kit pairing), the gates and the `map.xml` codec. pgmvox already writes its blocks
through the studio's Anvil writer and draws through its renderer.

**What pgmvox has and the studio cannot say yet:**

| Capability | Studio today | pgmvox | Phase |
|---|---|---|---|
| Plan | rectangles on a 5-block grid; no reasons | places with what, why and reach; targets; sections; versions with a because | 1 |
| Ground | relief marks solved together; grain that never overrides a mark | ordered landform operations over drawn areas: terms, outline, mound, crater, level, profile, ridge, butte, spire, canyon, watercourse, lake, coast, grade, warp, gullies, lumps, shatter | 2 |
| Paint | band stacks on depth, inward, height, slope | the same, plus paint layers by place and line, beds that dip and fold, wet beds, per-column undersides | 3 |
| Underground | a whole-column subtract under a lid (`SK13` refuses the rest) | tunnels, chambers, galleries, shafts, bores in a 3D grid | 4 |
| Play pieces | a goal sits where the plan's rectangle put it | pieces of any shape, anywhere on the land | 5 |
| Houses | 61 styles, 28 with stair roofs, ten in clay; axis-aligned only | any heading, jetties, posts, chimneys, wood doors | 6 |
| Lines | a relief line plus a stroke | walls, hedges and fences along a line; paths graded, paved, stepped and half-stepped; roads found to a grade | 6 |
| Style | theme buckets: rim, wall, surface, fill, bedrock | a grammar over sections and faces, seven styles, one plan in any of them | 7 |
| Bespoke things | seven prop kinds | anything | 8 |

## How a capability moves

**A capability moves when pgmvox has proven it, never before.** Proven means two boards use it, or the author
asks for it. A capability one board used once stays in that board; pgmvox's own rule ("a thing written twice
belongs in the library") is the same test one step earlier.

**It moves to C#, and there is one implementation of it afterwards.** The studio already holds the voxel world
(`VoxelWorld`), the Anvil writer, the stamps, the rules and the codec in C#, and `PgmStudio.Geom` runs in the
browser, so a ground modifier's preview in the client is the same code as the build. A Python service beside the
studio would mean every operation twice and a second process on the 4 GB server; `CLAUDE.md`'s *one concept, one
shape* forbids that for longer than a trial. Once an operation is in the studio, pgmvox keeps its copy only as
the reference the parity test reads, and its README says where the studio's lives.

**Where it lands follows `CLAUDE.md`'s rule.** A 2.5D operation over a heightfield (a landform, a field, an
area's distance) goes in `Geom`, because the client must reach it. An operation that writes blocks goes in
`Minecraft`. What a map is played for goes in `Pgm` and writes the intent. A word three parties spell goes in
`Vocabulary`.

**Every move lands with a parity test.** pgmvox exports a fixture (the parameters and the heights or blocks they
produce, as pgmvox's recipe experiment did for the Vale: 2,438 bytes reproducing 40,000 of 40,000 columns), and
the C# test reproduces it column for column. Noise is the exception: the contract there is statistical
(`cell`, `octaves`, `gain`, `seed` explicit, the finest octave refused under about five blocks on walkable
ground), because a bitwise copy of numpy's generator is not worth having.

**The studio's existing concept keeps its name, and pgmvox's is renamed to it.** A house heading extends
`HouseStyle` and `PlacedProp`; a paint layer is a band stack with an area; a level is a layer. Where neither
side has a name, D16 and the eight shape decisions below settle it, once, before the first operation
lands.

**A move is done when its document says so.** The commit that lands an operation updates the tool document
that describes it, adds its `FEATURES.md` line, and is followed in `pgm-studio-mapgen` by one board rebuilt
through the studio with it, as the evidence the capability arrived whole.

## The shape it lands in

**A map's ground is built in three stages, and each does one kind of thing (D17).** Shapes on layers make the
masses, with full control over where they are and how high; modifiers, stacked in order, change what the shapes
made; placements stand presets and schematics on the result. Painting the surface is apart from all three.
Phase 2 builds it, and the concept page
(https://claude.ai/artifact/PXyATVHg1XQ12BWoBmYaes) draws Terraform over it. Of this section the modifier stack
runs in the studio: `modifiers[]` with Hill, Hollow, Level, Roughness, Ridge, Butte, Spire, Scarp, Canyon, Profile,
River, Lake, Coast, Road, Lumps, Shatter, Gullies and Warp, masked by ellipses, outlines, lines and the board and
fanned by the symmetry, listed
and edited in Terraform (`docs/tools/sketch.md`, *The modifier stack*). The rest of it is not built yet. `pgmvox.document.layers` and
`LAYER-DOCUMENT.md` (draft 4) are the laboratory it is drawn from, not its format.

**Each thing has one word (D16).** A **layer** is where shapes sit, at a base height of its own. A **storey** is a
building's floor and a **course** one row of a wall. A **band** is one of a theme's block layers, as a band
stack already states them. A **modifier** is one terraform operation, and its **mask** the area it works in. A
**placement** is a preset or a schematic put at a spot. What pgmvox calls a level is a layer here.

**A shape states a top and a bottom.** The top is a y, or `on` another shape with a thickness, so a wall laid on
the island follows the island up its slopes. The bottom is the layer's floor, down to whatever is under it, or, stated on the layer, an
underside that tapers the way a floating island's does. Beside the drawing tools' rectangle, circle, polygon,
lasso and polyline stand the primitives: box, cylinder, wedge, sphere and torus, each turned, tilted and sunk, as
Stratum sets its monolith and its fallen obelisk. Draw keeps level, a shape's base height,
and drape, which is `on`; raise and sink are modifiers.

**A modifier changes what is there, inside its mask, in the order the list gives.** Mound, crater, ridge, river,
grade and noise are modifiers, and so is every carve: a tunnel with its mouths, a chamber, rooms with the halls
between them, and a shaft. A modifier has an id, an operation, a mask (any shape kind, drawn in place or named),
a mode (set, lift or cut), a join that says how the areas of one modifier merge, a seed and its numbers. A number
may be a field expression; the modifier list in Terraform edits plain numbers and per-vertex arrays, and shows an
expression without editing it.

**How a mask's outline is bent is a setting of the mask, not a modifier (D23).** Warp moves the ground the mask is
measured on sideways, so the whole form bends, rim and crest together; rough wavers the rim; fray bites it block
by block; lobes bulge it round its middle. Each changes the distance the mask gives, so every modifier drawn with
that mask gets it, and the outline under edit keeps its straight sides and handles. The Warp modifier is another
thing: it bends ground already built.

**Every shape says what the modifiers do to it (D18).** `follow` lets them reshape it as ground. `hold` keeps its
columns out of every modifier, so its form wins. `ride` keeps its form and moves it with the change in the ground
under it. A ground shape follows and a primitive holds unless it says otherwise, so a hill raised beside a torus
rises round it and leaves it a torus. A mask may also leave out shapes by id, and a primitive is roughened only
by a modifier whose mask is that primitive.

**The stages run in one order (D18).** The shapes that follow or hold are laid; the modifiers run, over the
shapes that follow; the shapes that are `on` another are laid on the result, so `on` always means the ground
after every modifier; the shapes that ride are moved; and the placements stand on the surfaces they name, which
are finished too.

**Every floor a player stands on is a surface, and a placement names the one it stands on.** A layer's ground, a
deck and a carve's floor each publish theirs, so a house or a tree on a raised layer stands on that layer and not
on the ground beneath it. The reads (`column`, `transect`, `slopes`, `incline`, `walk`) answer per surface, and the
walk crosses from one surface to another.

**What is placed is a preset or a schematic, each its own document.** Trees, boulders and houses are library
presets. What an agent builds block by block, and what the sketch calls a made layer, is a schematic: JSON,
loadable from the library, referenced by the map and never written into its terrain. An agent's brush, a field
swept or a wall drawn, is a generator whose output is a schematic, placed like any other.

**A recipe is a group of modifiers with the paint and placements that belong to them, placed as one (D22).** A
volcano, a glacier, a caldera lake: its modifiers stand in the modifier list as one row that opens to edit them one
at a time, its paint rules stand in Palette bound to its masks, and its placements in Decoration, each tagged with
the recipe. Moving, turning or sizing it moves every part, and a few numbers on its row (size, turn, height, seed)
are the ones it exposes. A recipe carries its paint and its placements wherever it is placed. It lays no blocks of
its own and no play pieces. Recipes are library rows, as themes and patterns are, and the Fire and Ice Composer
(https://claude.ai/artifact/Tf6EbuUvyxsPAXq61781K6) is the first set of them, still baked into heights.

**The stored format is the sketch's, changed in three places (D19).** Symmetry is stated once in `setup`, and
`layers[]` keep their shapes and base heights. A shape gains `top`, `bottom`, `on`, its relation to the
modifiers and the primitive kinds, and a line's width is its full width. `relief`, keyed by group, becomes one
top-level `modifiers[]`, each with its own id and seed. `placements[]` names a preset or schematic and the
surface it stands on, and the `made` layers become schematics.

**Every modifier is kept as it ran.** Its output is keyed by a hash of its numbers and everything before it, so an
edit to modifier *k* re-runs from *k*, and every intermediate state can be rendered and read. A diff has two
halves: the modifiers whose numbers changed, and the ones whose *effect* changed because the ground under them
moved; on the Vale, moving one butte re-routed roads over 3,261 columns nobody touched.

**The relief solver does not come with it.** Every modifier is one of pgmvox's operations. Exact heights, where
the game needs them at a spawn floor, a wool room or a monument's plinth, are a shape's top, and the rules that
only check marks (`RL1`–`RL6`) go with the marks.

**Ground stays in columns; carves run after it in the block grid.** The column model is fast, runs in the browser
and folds cleanly under symmetry, so shapes and the ground modifiers keep it. Tunnels, chambers, rooms and shafts
run over `VoxelWorld`, so `SK13`'s cut-and-lid stops being the only way down. A primitive's body is cut into the runs
of blocks a column meets, a layer holding more than one span of a column where a solid stands apart from the rest, so
a primitive meets the ground, the paint, the walk and the symmetry through the machinery every shape uses.

**The modifier list makes the old addressing work unnecessary.** `B264` (Configure cannot address a storey), `WE28`
(a relief keyed by layer and group) and `TS64` (a made thing as one row in the layer strip) were the planned way
to make terrain addressable. Every modifier has an id of its own and every made thing becomes a schematic, so the
three are not built: they retired, on the author's call, with the migration `M0086`.

**A stored map is carried to its nearest new form, not held to its old heights (D20).** Each mark, push, grain and
height mode becomes the shape field or modifier it already is, and the migration writes into the map's history
what it approximated or dropped. Before it runs, every deployed map's world, `map.xml` and sketch are archived in
`pgm-studio-mapgen`, one folder a map.

**Only an isolated objective or spawn is refused (D21).** A cave, a tunnel or a layer no player can reach may be
intended, so it is at most a hint, and no measure is added for them until a built world or a playtest shows a
problem.

**The sketch will be the map's one document, and the box plan stops being a level.** The box plan began as the
way into a capture-the-wool board, and keeping it beside the sketch is what makes a monument a plan marker that
only a recompile moves, makes the relief ride beside shapes a recompile replaces, and makes `from-plan` merge
and answer 409. In the sketch the play pieces are objects placed and dragged like houses and trees, each writing
its entry of the intent, and the places are an annotation layer drawn over the ground that makes no terrain.
Rectangles stay a way of drawing: the generator emits a sketch from its own piece model, and a box drawn on a
grid is a sketch shape like any other. The stored plans are kept as history, read-only.

**The places are the design, and they link to play, not to geometry.** A place — the village, the delta, the
volcano — has a kind, what it is, why a player goes there, how it is reached, and its own rough geometry in
blocks; it may name the intent object it holds (`PLAN-REVIEW-DESIGN.md`). It never names the shapes that realise
it, since a shape is stretched and redrawn as the ground is worked. A modifier may take a place as its mask,
so a volcano layer over the volcano place is one statement.

**The intent stays the one source of play.** A play piece writes the intent and the intent writes the XML, as
today; pgmvox's own `map.xml` builder is retired for any board that passes through the studio.

## The order a map is made in

**A map is made in eight phases, and each writes one thing and reads only what the phases before it wrote.** The
order is the author's (D12). It follows what the pgmvox runs actually did, with the one step they kept in the
agent's head written down first, and with the look stated before the review rather than first seen after the
build. Traced on Abbeymoor, the layout was decided before `land.py` and its coordinates typed again in
`plan.py`, and four of the seven points of the author's first review were paint, materials or dressing that no
picture before the build showed (`pgm-studio-mapgen/analysis/freeform-vs-studio/experiment/REVIEW-1.md`).

| # | Phase | Writes | Fixes |
|---|---|---|---|
| 1 | Idea | the brief | game mode, teams and their size, map size, symmetry, mood |
| 2 | Places | the places layer, with spawns and objectives as rough points | where things go and the routes between them, each place with what it is and why a player goes there |
| 3 | Terraform | land shapes, relief, water, caves | the ground, shaped round the places: outline, heights, which basins hold water and at what level, caves and their entrances |
| 4 | Layout | exact play pieces, paths, plazas, house footprints | where each piece stands to the block |
| 5 | Look | palette, a house style per area, tree and rock zones, biome | what each area is made of, as data |
| 6 | Review | the author's notes and a verdict | the go, or the changes |
| 7 | Build | the world | the paint applied, houses built, trees and dressing placed, objectives and markers stamped |
| 8 | Play review | in-game notes, each tied to its place | the corrections |

**Places come before the ground because the ground is shaped round them.** Written down, the terraform reads
where the village and the hill are instead of restating them, and a place moves without editing three files.

**Water and caves are terraform.** A pgmvox board already fixes its water levels in the land pass, and a cave
holds resources or leads somewhere, so both are part of the ground the review judges (the author's ruling).

**The look is stated before the review and applied after it.** Palette, house styles, tree and rock zones and the
biome are data the review sheet draws, so what the author reacted to after the build is on the sheet before it.

**The review is one sheet, measured on real ground.** It carries the places, the ground with its water and caves,
the layout, the look's zones, and the walk and sight numbers read off the sketch's ground. A rough look at the
places alone, after phase 2, is cheap and optional.

**The build executes what was stated.** A correction found in play goes back to the phase that owns it.

**A map may also start from one section.** Phases 2 to 5 run for the spawn or a monument's ground alone, the
section is reviewed with its look fixed, and the rest of the map grows round it through the same phases, reusing
that look. It needs a place that carries its own house style, tree kind and floor height, which phase 5 gives.

## The phases

**Each phase ships on its own and leaves the studio usable.** A phase is pulled onto `TODO.md` as one group when
the programme ahead of it drains (`docs/backlog-strategy.md`'s one-programme rule), and its entries get their ids
then. Before any phase reaches pgmstudio.de, a migration dry run over every stored map and the browser gate run.

### Phase 0 — in the live studio, with no new words

**The quick wins need no new concept, and the author notices only better boards.**

- The framed isometric shipped as `WS90`: `render/isometric` and `render/xray` take a `box`, so each place gets
  its own picture, and the x-ray draws the rooms alone, as pgmvox's does.
- The three rule tiers shipped as `RP128`: a rule's level may be `measure`, which never refuses and is read as
  the number it measured against its range, and the families of D4's measures take it by default.

A section along a bent or diagonal line is not ported. 47 of the 49 cutaways pgmvox's boards draw are straight
cuts along x or z, which `render/section` already draws, and `transect` answers along any polyline in numbers.

Jump pads (`PG24`) are not in this phase: the studio does not have them as a capability yet, and they stay a
backlog entry of their own.

### Phase 1 — one document: the sketch, its play pieces and its places

**The sketch becomes the only document a map is drawn in, and the plan stops being a level.** A map is made in the
order above: its places, then its ground with water and caves, then its layout and its look, then the review,
then the build. Each step below leaves the studio usable, and the plan keeps working until its replacement has
landed.

- Play pieces as sketch objects: a spawn, a wool room, a monument, a core, iron, a build zone and a protection
  zone placed, dragged and turned in the sketch, each writing its entry of the intent, so moving one recompiles
  nothing. The intent is the one place a piece is stored, a drag moves its whole symmetry orbit, every piece has a
  stable id (shipped as `TS174`), and Configure's detection flow stays for maps read from a world (D13). Pieces
  are added in a Layout step of Draw, after its Ground step, and moved or turned from either step, every phase
  drawing the same board; a room placed over no ground brings a rectangle of ground of its own, and rooms and
  zones are rectangles only (D14).
- The places: a document stored with the map and changed in its one history, drawn in a Plan step of Draw
  before Ground with the notes step's tools on a layer that never rasterizes, each place naming at most the play
  piece it holds; notes anchored to places, Review drawing them over its cameras, and a verdict (D15).
- The look as data: a palette, a house style per area, tree and rock zones and the biome stated before the
  review, drawn on the sheet, and applied by the build.
- A working area an agent may state in the places document; a person's board is cropped to what it draws.
- The generator emits a sketch, shipped as `TG7`. Its piece model stays its own (`docs/generator/model.md`) and ends
  in a layout with a shape per box and an intent, and a candidate becomes a map in one step.
- The rules move onto the sketch: the measures read off the sketch's rasterized ground, which never builds a
  world, and the refusals asked between play pieces. `sight` is the one new read, parked with the target numbers a place states (`TS191`, `TS190`); in-game notes are tagged with
  the place their ground falls in.
- The plan retires: the Plan tool and `from-plan` go, every stored plan is kept read-only as history, and
  `drive.py` and the skill move to sketch sources; the specs stay as they are (D11).

**What the author notices:** a monument dragged where the landscape wants it, places with reasons reviewed
before anything is built, and boards that stop looking alike because none of them starts as rectangles.

### Phase 2 — shapes, modifiers and the ground

**This is the largest phase and the one every later phase stands on.**

- The document as D19 states it: shapes with a top, a bottom, `on` and their relation to the modifiers, the
  primitives, the `modifiers[]` list with masks, modes, joins and seeds, numbers as fields, the cache, the
  per-modifier delta and diff, and the existing reads taken after any modifier with `?step=`.
- The migration that carries every stored sketch to its nearest new form, after the archive (D20). It is `M0086`: `WE28`,
  `B264` and `TS64` retired with it.
- The modifier list in Terraform: the modifiers listed, reordered, switched off, a mask dragged, and what a modifier
  changed.
- The ground operations, in the order `pgmvox-cleanup/INVENTORY.md` §4.3 ranks them by how much board code they
  delete: the three primitives (a 1-D wandering line, a field made symmetric, nearest-on and scatter), field
  terms, the island outline, level, mound and crater with `join` and `melt`, profile, ridge, butte and spire,
  watercourse and lake, grade, then warp, gullies, lumps and shatter.
- Noise as a modifier with its own mask, which is the lever `STUDIO-2-DESIGN.md` §11 found: grain on a marked board
  moves 19% of columns and changes nothing a player sees.

**What the author notices:** ground made of hills, canyons and rivers where they are drawn, editable one at a
time. Shipped to the live studio with no beta (decision D3).

### Phase 3 — paint, lay and water

**Phase 3 has shipped: the surface is chosen by what the ground is.**

- Paint rules: a layout's `paint[]`, a material chosen by slope, height band, noise and nearness to a modifier,
  first match winning, so a River's sandy bank names the River (`docs/world-export/terrain-painting.md` §3.1b,
  TP28).
- Lay: a surface's soil thins by slope and a tread reads as a ledge (TP27); a height stack's beds dip and fold
  beside `TP26`'s follow; a band stack that cycles is the `cycle` ending (§3.1).
- Water: a River, Lake or Coast lays its own `bed_material`, and stack water beside void falls down the face
  (`docs/tools/sketch.md`, *The modifier stack*).
- A layer's underside, with a sheer side beside a rift, so a layer floats at its own height or rests on the
  ground under it (`docs/tools/sketch.md` *A layer's underside*).

### Phase 4 — space in three dimensions

- Carve modifiers: a tunnel with cover, mouths and a level floor, a chamber and a shaft with its ladder, cut in the
  built world and dressed as caves (`docs/tools/sketch.md`, *The modifier stack*); rooms joined by stepped halls,
  a gallery and a bore are not ported yet.
- The primitives in the block grid: a box, a cylinder, a wedge, a sphere and a torus, turned, tilted and sunk, and
  the natural arch as a shape whose deck is a surface (`docs/tools/sketch.md`, *Solids*). They are shapes whose
  bodies the column model reads as runs of blocks, a torus on its edge making two in a column; placing one by
  dragging it out in the dock is not built, and the other `solid.py` forms (frustum, beam, prism, revolve, sheet) are
  not ported.
- The walk across surfaces over carved space: the walk reads the built world, so a tunnel is walked, and `SK13`
  complains rather than refuses, because the cut-and-lid is no longer the only way down.

**What the author notices:** crypts and mines without the lid trick.

### Phase 5 — stamps and uploaded worlds

- A control point and a wall as play pieces of any shape, beside the ones Phase 1 places.
- Stamps as the last stage, each switchable per piece: goal marker, build-zone outline, defence wall, wool-room
  loot, kit pairing, float check.
- A world built elsewhere uploaded with its `map.xml`, read into the intent, with the stamps run over it on
  export. `POST /map/import-folder` reads a server-side folder today; an upload route is what is missing.

**What the author notices:** every pgmvox board playable with the studio's markers and outlines on it.

### Phase 6 — built things

- Houses: the three new storey fields (posts, a laid log course, jetty with setback and eave), a chimney, wood
  doors, and a heading in degrees on the placed house (`houses/HOUSES.md`). pgmvox's two house defects (door
  before windows; the cell outside a door at any heading) are fixed on the way.
- Line builds: a wall, hedge or fence along a line, with its crown following, level or graded, caps, crenels and
  gaps.
- Paths in two layers: the grade in the ground stage, the surface with stairs or half steps after the lay.
- Routes found over the ground to a grade, and networks sharing a trunk. pgmvox takes twelve seconds for four
  branches in Python, so this one is measured in C# before it is promised.
- Props pgmvox proved: stalls, lamps, braziers, crop fields, rubble.

### Phase 7 — the grammar

**The grammar is the studio's themes taken a level up: rules over sections and faces, with the blocks in a
separate style.** It lays courses down from a rim, a bay whole or not at all, fills that decline, decks on
pillars, patterns along each straight run, marks and words.

- Grammar styles as library rows, editable in the browser like themes; the rules stay code. The seven styles
  (Brittlebush, Claywork, Terra, Marble, Foundry, Segment, Drastic) are the first rows.
- A grammar layer that acts on any shape: a made section, a wall, a cliff face, a wool room. Faces on grown
  ground (`grammar.dress`) come with it.
- A sketch drawn in rectangles read as a grammar blueprint in one step, so a planned board is coloured the
  moment it is drawn.
- Facade patterns on masses and floor fields as carpets.
- The decisions the grammar takes, shown: which fill each section got, which bays it refused and why. The
  Recipe Inspector's grammar tab is the prototype.

**What the author notices:** structural boards in any of seven styles from one plan, and villages that are not
in a row.

### Phase 8 — made things, and what keeps the library growing

- `place.made`: a block volume with an anchor, footprint and turn, uploaded with the source that generated it,
  moved and turned as an object, never run on the server. An imported world is a document of one made thing.
- An agent's report ends with what it wanted and could not say. A made thing that a second board needs becomes a
  library prop or an operation, through the protocol above.

## What an agent does on pgmstudio.de, phase by phase

**The order of work stays `ORDER-OF-WORK.md`'s, and each phase of this programme takes over one step of it from
pgmvox.** Places and play pieces in the sketch, with targets (Phase 1); shapes and modifiers, then `slopes`, `incline` and `walk` read
after the last one, posted again until the numbers meet their targets (Phase 2); carves and the x-ray (Phase 4);
made ground and houses (Phases 6 and 7); stamps and gates (Phase 5). A step the studio cannot
take yet is taken in pgmvox and uploaded, and the board's report says which.

**The `pgm-board` skill and `AGENT-GUIDE.md` change with each phase.** The commit that ships a phase is followed by
the skill's lookup table naming the new reads and layers, so the next run uses them without being told.

## What stays in pgmvox

- Every operation not yet ported, and every new one tried first.
- The boards built on it, with their history; they are the evidence each move is tested against.
- The reads of a world that is not a stored map, until the upload route of Phase 5 lands.
- Its plan raster and walk graph, until the places layer carries polygons and storeys.

## The author's decisions

**All twenty-three are taken.**

| # | Decision | Blocks | Answer |
|---|---|---|---|
| D1 | Port to C#, or run pgmvox as a service beside the studio | 2 | **taken:** C#, one operation at a time, pgmvox as the laboratory |
| D2 | Does the layer document replace the sketch (with a migration), or stand beside it first | 2 | **taken:** replace. The sketch's ground becomes the document D19 states, carried by the migration D20 describes |
| D3 | Where the beta runs: a second instance on pgmstudio.de with a copy of the database, or a switch on the live one | 2 | **taken:** neither. There is no beta: the new ground document reaches the live studio when its migration has run clean in a dry run over every stored map |
| D4 | Which rules are hard, which a hint, which a measure | 0 | **taken:** hard: valid XML and pieces, nothing built inside a house, a sensible goal, float and reach; hint: spacing, ground under a house, sketch overlaps, style; measure: walks, distances, gap widths, front lines (GO, WL, CT, LN, FR, MD) |
| D5 | Phase order: the plan of places before the ground, or after it | 1 | **taken:** before; it is cheap, needs no engine, and the planning step is what made the freeform boards' places good |
| D6 | The eight shape decisions (`INVENTORY.md` §5.6, P1–P8): names of places and paths, width as full width, units and one rounding step, noise as a statistical contract, every operation declaring its `mode`, its reads and writes, a seed per operation, symmetry stated once | 2 | **taken:** the inventory's proposals as written, before the first operation |
| D7 | When the programme starts | all | **taken:** now, as the programme on `TODO.md`. `TS130` is parked in `BACKLOG.md`: it is tangled with how land is made, which Phase 2 redesigns, and is taken up when that work reaches it, beside `B264`, `WE28` and `TS64`, which retire there |
| D8 | Does the box plan stay a level beside the sketch | 1 | **taken:** no. The sketch is the one document: play pieces are sketch objects writing the intent, places an annotation layer, the generator emits a sketch, and the Plan tool retires |
| D9 | What becomes of the stored plans (48 of 64 maps on pgmstudio.de hold one) | 1 | **taken:** kept, read-only, as history. Each map's sketch and intent carry its play forward; nothing reads the plan again |
| D10 | Does `POST /plan/compile` stay, as a one-shot converter from rectangles | 1 | **taken:** yes. It is the one way a plan reaches a map: it answers a layout and an intent, and a source states those |
| D11 | Which of `pgm-studio-mapgen`'s 174 plan-and-refinement specs move to sketch sources | 1 | **taken:** none. They are experiments and stay frozen as they are; a plan that is wanted later comes across through the one-way converter (D10) |
| D12 | The order a map is made in | 1 | **taken:** idea, places, terraform with water and caves, layout, look, review, build, play review; a map may also start from one section (*The order a map is made in*) |
| D13 | Play pieces in the sketch: where a piece is stored, what a drag moves, how it is named, what Configure keeps | 1 | **taken:** the intent is the one store and the sketch draws from it; a drag moves the whole orbit; every piece has a stable id, migrated onto stored maps; imported maps keep Configure's detection flow |
| D14 | Where play pieces are placed in the sketch, and what a room can be drawn as | 1 | **taken:** Draw has two steps, Ground and then Layout, one click apart in the bar, so the two can later fold into one. Layout holds the tools that add and delete a piece, and later the analysis of the layout; every piece and every shape is drawn in every phase, and a piece is picked, moved and turned from either step, so the two are one board seen for two jobs rather than two layers switched on and off. A spawn or wool room placed where no ground lies also draws an ordinary rectangle of ground under it, the size of the room, which Ground then shapes like any other; a build zone and a water lane stand over the void and bring none, and a goal, an iron cube or the observer placed over no ground is complained of. A spawn, a wool room, a build zone and a water lane are rectangles only, sized when they are drawn and resized from an edge or a corner afterwards (`TS179`) |
| D15 | Where the places are drawn, what a place shows, and where its versions live | 1 | **taken:** Draw gains a Plan step before Ground, laid out as Review's Cameras step: the board top-down, the notes step's drawing tools, the places listed on the left and a place's thread on the right. Draw opens on Ground, and a new map on Plan. A place states what it holds and nothing more; whatever an agent has to say about it goes in its thread. Its versions are changes in the map's one history, and Review's Cameras step draws the places as an overlay, a note on ground being tagged with the place it falls in |
| D16 | What the things are called | 2 | **taken:** a layer is where shapes sit; a storey is a building's floor and a course a wall's row; a band is one of a theme's block layers; a modifier is a terraform operation and its mask its area; a recipe is a group of modifiers with its paint and placements; a placement is a preset or schematic put at a spot |
| D17 | Which stages build a map's ground, and what is kept out of the terrain document | 2 | **taken:** shapes on layers, then modifiers, then placements, with painting apart. Presets and schematics are documents of their own, referenced and never inlined; an agent's brush is a generator that outputs a schematic |
| D18 | How a shape meets the modifiers, and the order the stages run | 2 | **taken:** each shape is `follow`, `hold` or `ride`, ground shapes following and primitives holding unless stated; shapes that follow or hold, then modifiers, then shapes `on` another (on the modified ground), then shapes that ride, then placements on their surfaces |
| D19 | The stored format | 2 | **taken:** the sketch's document, with a top, a bottom, `on`, the modifier relation and the primitives on every shape, a line's full width, one top-level `modifiers[]` replacing the group-keyed relief, and `placements[]`; a modifier's number may be a field expression, which the modifier list shows and does not edit |
| D20 | What the migration keeps | 2 | **taken:** each stored statement goes to its nearest new form and the map's history says what changed; no map is held to its old heights. Every deployed map's world, `map.xml` and sketch are archived in `pgm-studio-mapgen` first |
| D21 | Which rules caves and layers bring | 4 | **taken:** an objective or spawn that is isolated is refused; everything else is a hint, an unreachable cave never a fault, and no measure is added until a world or a playtest shows one is needed |
| D22 | What a combination of modifiers is, and what it carries | 2 | **taken:** a recipe, a library row as themes and patterns are: its modifiers, the paint rules bound to its masks and its placements, each landing in its own stage tagged with the recipe and placed, moved, turned and sized as one. It carries its paint and placements; it lays no blocks and no play pieces |
| D23 | Whether bending a mask's outline is a modifier or a parameter | 2 | **taken:** a setting of the mask: warp, rough, fray and lobes change the distance it gives, so every modifier on that mask gets it |

## What this does not decide

**How much of a house style is data.** pgmvox's ten-slot archetype made its houses look right and the studio's 61
styles make its houses varied; the archetype as a library row, with the styles mapped onto archetypes, is the
obvious meeting point and has not been tried.

**Whether the places layer replaces pgmvox's raster plan.** A design with polygons, storeys and per-place
heights would; until Phase 1 has run on real boards, the raster stays the freeform planner.

**The made thing's limits.** Size, how its source is stored, and whether it may be edited in the browser are
settled when Phase 8 is pulled up.
