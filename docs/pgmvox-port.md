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
| Vocabulary | anything can be built; a missing thing costs 5–60 lines, not a schema change | the layer document and its operation families (Phases 2–7), and the made thing as the escape hatch (Phase 8) |
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
browser, so a ground layer's preview in the client is the same code as the build. A Python service beside the
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
`HouseStyle` and `PlacedProp`; a paint layer is a band stack with an area; the relief solver is a layer kind.
Where neither side has a name, the eight shape decisions below settle it, once, before the first operation
lands.

**A move is done when its document says so.** The commit that lands an operation updates the tool document
that describes it, adds its `FEATURES.md` line, and is followed in `pgm-studio-mapgen` by one board rebuilt
through the studio with it, as the evidence the capability arrived whole.

## The shape it lands in

**A board's ground is to become a layer document: an ordered list of layers, each one operation over the areas
it is drawn in.** The order is the meaning; a later layer acts on what the earlier ones made. A layer will be
switched off, moved, and edited in the browser by dragging its area, and an agent will write exactly the same
list as JSON. `pgmvox.document.layers` builds it today, and `LAYER-DOCUMENT.md` (draft 3) is its specification.
Nothing in this section exists in the studio yet; Phase 2 builds it.

**Every area will answer one thing, a distance, which is what lets any area carry any operation.** It is 0 at
the area's centre, 1 on its rim and over 1 outside, so a crater drawn with the lasso is the same crater as one
stated as a circle. The studio's drawing tools stay; a rectangle, a polygon, a lasso or a polyline stops being
ground and becomes the area or the line of a layer.

**Every layer will be kept as it ran.** A layer's output is keyed by a hash of its parameters and everything before
it, so an edit to layer *k* re-runs from *k*, and every intermediate state can be rendered and read. A diff has
two halves: the layers whose parameters changed, and the layers whose *effect* changed because the ground under
them moved — on the Vale, moving one butte re-routed roads over 3,261 columns nobody touched.

**The layers will build in fixed stages.** pgmvox's thirteen — made blueprint, fields, ground, paint, lay, water,
faces, made, volume, structures, build, dress, symmetry — and after them the studio's two: play pieces, which
write the intent, and stamps, which the studio derives from them and lays last.

**The relief solver becomes one ground layer, not the thing replaced.** Exact heights are what the game needs at a
spawn floor, a wool room and a monument's plinth, and marks give them where noise does not. What goes is the
obligation to say everything with marks, and the rules that only check marks (`RL1`–`RL6`) with it.

**Ground stays in columns; space becomes a second family that runs after it in the block grid.** The column model is
fast, runs in the browser and folds cleanly under symmetry, so the ground keeps it. Tunnels, chambers, shafts,
bodies and floating undersides run afterwards over `VoxelWorld`, so `SK13`'s cut-and-lid stops being the only
way down.

**The layer document makes the layer word unnecessary.** `B264` (Configure cannot address a storey), `WE28` (a
relief keyed by layer and group) and `TS64` (a made thing as one row in the layer strip) were the planned way to
make terrain addressable. In a layer document every layer is addressable by construction, so the three are not
built: they retire, on the author's call, in the commit that lands Phase 2's document.

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
it, since a shape is stretched and redrawn as the ground is worked. A ground layer may take a place as its area,
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
- The generator emits a sketch. Its piece model stays its own (`docs/generator/model.md`) and ends in a layout and
  an intent rather than a plan.
- The rules move onto the sketch: the measures read off the sketch's rasterized ground, which never builds a
  world, and the refusals asked between play pieces. `sight` is the one new read; in-game notes are tagged with
  the place their ground falls in.
- The plan retires: the Plan tool and `from-plan` go, every stored plan is kept read-only as history, and
  `drive.py` and the skill move to sketch sources; the specs stay as they are (D11).

**What the author notices:** a monument dragged where the landscape wants it, places with reasons reviewed
before anything is built, and boards that stop looking alike because none of them starts as rectangles.

### Phase 2 — the layer document and the ground

**This is the largest phase and the one every later phase stands on.**

- The document: layers, stages, areas and their distance, numbers as fields, the cache, per-layer delta and diff,
  and the existing reads (`column`, `transect`, `slopes`, `incline`, `walk`) taken after any layer with `?step=`.
- The relief solver and the sketch's shapes as layer kinds, and a migration that carries every stored sketch
  into layers without loss. `WE28`, `B264` and `TS64` retire in the same commit.
- A Layers tab in Sketch, as the terrain-layer prototype draws it: reorder, toggle, drag an area, see what a
  layer changed.
- The ground operations, in the order `pgmvox-cleanup/INVENTORY.md` §4.3 ranks them by how much board code they
  delete: the three primitives (a 1-D wandering line, a field made symmetric, nearest-on and scatter), field
  terms, the island outline, level, mound and crater with `join` and `melt`, profile, ridge, butte and spire,
  watercourse and lake, grade, then warp, gullies, lumps and shatter.
- Noise as a layer with its own area, which is the lever `STUDIO-2-DESIGN.md` §11 found: grain on a marked board
  moves 19% of columns and changes nothing a player sees.

**What the author notices:** ground made of hills, canyons and rivers where they are drawn, editable one at a
time. Shipped behind the author's choice of beta (decision D3).

### Phase 3 — paint, lay and water

- Paint layers: a block chosen by slope, area, height band and noise, first match winning, an area that may name
  another layer's line (a river's sandy bank).
- Lay: soil depth by slope, rock in beds that dip and fold, merged with `TP26` so a bed follows the broad tilt.
  A band stack that cycles (`WE143`) lands here.
- Water from each river's level, with its bed's own stack; falls off the island's edge into the void.
- Undersides per column, with a sheer side beside a rift, and slab islands floating at their own heights.

### Phase 4 — space in three dimensions

- Carves: tunnel with cover and a level floor, chamber, gallery, shaft, bore, cave dressing (`TS140`).
- Volume layers: a box turned, tilted and sunk; solids from `volume.solid`; the natural arch as a storey of its
  own.
- The walk across storeys over carved space. `SK13` stops refusing anything, because the cut-and-lid is no
  longer the only way down.

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
pgmvox.** Places and play pieces in the sketch, with targets (Phase 1); ground layers, then `slopes`, `incline` and `walk` read
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

**Twelve are taken and three are open; each open one blocks the phase named.**

| # | Decision | Blocks | Answer |
|---|---|---|---|
| D1 | Port to C#, or run pgmvox as a service beside the studio | 2 | **taken:** C#, one operation at a time, pgmvox as the laboratory |
| D2 | Does the layer document replace the sketch (with a migration), or stand beside it first | 2 | open; recommended: replace, after a beta in which both exist |
| D3 | Where the beta runs: a second instance on pgmstudio.de with a copy of the database, or a switch on the live one | 2 | open; recommended: a second instance, so the live studio does not change until the migration has run clean |
| D4 | Which rules are hard, which a hint, which a measure | 0 | **taken:** hard: valid XML and pieces, nothing built inside a house, a sensible goal, float and reach; hint: spacing, ground under a house, sketch overlaps, style; measure: walks, distances, gap widths, front lines (GO, WL, CT, LN, FR, MD) |
| D5 | Phase order: the plan of places before the ground, or after it | 1 | **taken:** before; it is cheap, needs no engine, and the planning step is what made the freeform boards' places good |
| D6 | The eight shape decisions (`INVENTORY.md` §5.6, P1–P8): names of places and paths, width as full width, units and one rounding step, noise as a statistical contract, every operation declaring its `mode`, its reads and writes, a seed per operation, symmetry stated once | 2 | **taken:** the inventory's proposals as written, before the first operation |
| D7 | When the programme starts | all | **taken:** now, as the programme on `TODO.md`. `TS130` is parked in `BACKLOG.md`: it is tangled with how land is made, which Phase 2 redesigns, and is taken up when that work reaches it, beside `B264`, `WE28` and `TS64`, which retire there |
| D8 | Does the box plan stay a level beside the sketch | 1 | **taken:** no. The sketch is the one document: play pieces are sketch objects writing the intent, places an annotation layer, the generator emits a sketch, and the Plan tool retires |
| D9 | What becomes of the stored plans (48 of 64 maps on pgmstudio.de hold one) | 1 | **taken:** kept, read-only, as history. Each map's sketch and intent carry its play forward; nothing reads the plan again |
| D10 | Does `POST /plan/compile` stay, as a one-shot converter from rectangles | 1 | open; recommended: yes, since the generator compiles its pieces anyway and the converter costs nothing beside it |
| D11 | Which of `pgm-studio-mapgen`'s 174 plan-and-refinement specs move to sketch sources | 1 | **taken:** none. They are experiments and stay frozen as they are; a plan that is wanted later comes across through the one-way converter (D10) |
| D12 | The order a map is made in | 1 | **taken:** idea, places, terraform with water and caves, layout, look, review, build, play review; a map may also start from one section (*The order a map is made in*) |
| D13 | Play pieces in the sketch: where a piece is stored, what a drag moves, how it is named, what Configure keeps | 1 | **taken:** the intent is the one store and the sketch draws from it; a drag moves the whole orbit; every piece has a stable id, migrated onto stored maps; imported maps keep Configure's detection flow |
| D14 | Where play pieces are placed in the sketch, and what a room can be drawn as | 1 | **taken:** Draw has two steps, Ground and then Layout, one click apart in the bar, so the two can later fold into one. Layout holds the tools that add and delete a piece, and later the analysis of the layout; every piece and every shape is drawn in every phase, and a piece is picked, moved and turned from either step, so the two are one board seen for two jobs rather than two layers switched on and off. A spawn or wool room placed where no ground lies also draws an ordinary rectangle of ground under it, the size of the room, which Ground then shapes like any other; a build zone and a water lane stand over the void and bring none, and a goal, an iron cube or the observer placed over no ground is complained of. A spawn, a wool room, a build zone and a water lane are rectangles only, sized when they are drawn; resizing one afterwards is `TS179` |
| D15 | Where the places are drawn, what a place shows, and where its versions live | 1 | **taken:** Draw gains a Plan step before Ground, laid out as Review's Cameras step: the board top-down, the notes step's drawing tools, the places listed on the left and a place's thread on the right. Draw opens on Ground, and a new map on Plan. A place states what it holds and nothing more; whatever an agent has to say about it goes in its thread. Its versions are changes in the map's one history, and Review's Cameras step draws the places as an overlay, a note on ground being tagged with the place it falls in |

## What this does not decide

**How much of a house style is data.** pgmvox's ten-slot archetype made its houses look right and the studio's 61
styles make its houses varied; the archetype as a library row, with the styles mapped onto archetypes, is the
obvious meeting point and has not been tried.

**Whether the places layer replaces pgmvox's raster plan.** A design with polygons, storeys and per-place
heights would; until Phase 1 has run on real boards, the raster stays the freeform planner.

**The made thing's limits.** Size, how its source is stored, and whether it may be edited in the browser are
settled when Phase 8 is pulled up.
