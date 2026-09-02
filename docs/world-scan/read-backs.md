# Reading a built world back

Everything a caller does with the studio runs through the API, and the API describes itself — except the one
thing done *after* building, which is looking at what was built. Nine renderers sit in
`PgmStudio.Minecraft/Render/` and `PgmStudio.Export/`, and until `WS6` they reached a caller only through
`PgmStudio.RoundTrip`'s flags: a capability no schema named, so a brief had to carry a table of them and an
agent had to know a .NET binary existed at all.

They answer over HTTP now, one route each, and what each one draws is written once — in
`WorldReadCatalog` — and served twice: as the endpoint summary the schema publishes, and as
`PgmStudio.RoundTrip --help`.

## What it reads, and where the world comes from

**The world is built for the request.** A map that ships its own region files has one on disk; a
sketch-authored map's exists only as the layout and the intent it derives from, which is the same position
`GET /map/{slug}/export` is in, and it builds one too. A map with no stored sketch layout is a **404** — not a
fault, but a statement that there is no world here to build.

**The build runs no gate, deliberately.** A board that fails one is exactly the board somebody needs to look
at, and a read-back that refuses the broken case is never there when it is wanted. `OB17`, `EX1`, `OB24` and
the rest belong at the door a map ships through, not at the window somebody looks in through.

**The map document is projected from the resolved intent** rather than composed through the export. The
overlays need one — the spawns, goals and apply rules a picture draws on top of the terrain — and the
projection is the version that agrees with the world the build just made: spawns snapped to the structures it
placed, goal locations filled in from the cubes it cast. Going through the export would lose the world to the
first gate that fired. A document that will not project costs the overlays and not the picture.

## The reads

Every route is `GET /api/map/{slug}/…`, every picture is `image/png`, and every one takes `scale` — pixels a
block, 1 to 16, default 4, clamped rather than refused.

| Route | Also | Answers |
|---|---|---|
| `render/topdown` | `--topdown --subject …` | the board from above, one question per image. `subject` = `ground` · `structure` · `made` · `foliage` · `objectives` · `combined`; `material` colours by the real palette rather than by category; `ymax` looks under a roof or a canopy; `layer` draws one storey of a stacked board |
| `render/section` | `--section` | a vertical cut with a Y scale. `axis` = `x`\|`z`, `from`/`to` its extent, `at` the other coordinate, `ymin`/`ymax` the courses drawn, `depth` how far behind the plane to project; `?format=text` answers the same cut as characters, `every` blocks a char |
| `render/heightmap` | `--heightmap` | elevation as tone, contour lines every `contour` blocks (default 4); `grey` drops the tone where a board's own palette fights the height reading; `layer` draws one storey. `?format=text` answers the same reading as a height-banded grid, `every` blocks a character, with the spawns, goals, houses and water overprinted |
| `render/surface` | `--surface` | the paint, as the tone families `TerrainPalette.Families` names; `layer` draws one storey |
| `render/traversability` | `--traversability-map` | the navigable components, with the spawns and goals on them |
| `slopes` | — | the worst step to a neighbour per sampled cell, as JSON digit rows or, on `?format=text`, `.`/`:`/`#` — the tiers a walk is priced in. `faces` names the barrier runs worth checking, largest first |
| `editability` | — | which columns a player may edit and **what makes each one editable**, as JSON: digit rows over a bounding box, the four `EditZone` words, a colour each, the counts, and `findings`. The zones are `build_zone` · `ground` · `filtered` · `sealed`, read by following PGM's own resolution — the first region-filter application that does not abstain settles the column, and place and break are the separate scopes PGM makes them |
| `render/structures` | `--structures` | the building census by block material, `minarea` the smallest counted (default 16); `layer` draws one storey |
| `render/mirror` | `--mirror` | the board against its own symmetry; `mode` overrides the one the map states |
| `render/walk` | — | what reaching each cell costs from `from`, with the route to `to` over the top. `field` = `blocks` · `distance` · `drops`, `aim` = `travel`\|`reach`\|`comfort`, `team` whose walk it is |
| `walk` | — | the same journey as numbers rather than as a picture, as JSON: `{reachable, distance, blocks, drops, worstDrop, aim, cells, places, steps, rises, falls, worstStep, beside}`. `?from=x,z&to=x,z`, `aim` and `team` as above; `?beside=N` (0–6) adds every distinct thing recorded within `N` cells of the route |
| `column` | `--column` | one or more columns bedrock-to-sky, every block named, as `text/plain`. `?at=x,z`, repeated |
| `transect` | — | a polyline walked block by block, as JSON: `{stations, rises, falls, worstStep, barriers, scrambles, drops, events, beside}`. `?points=x,z;x,z[;x,z…]`, `every` thins the stations, `beside` lists every claim within that many cells of the line; `?format=text` answers the same walk as a table |
| `themes/census` | — | every ground cell counted by the theme that paints it: cells and share per theme, its distinct surface materials, which theme borders which, and the board's whole palette count |

`column` answers characters rather than JSON for the reason the plan grid and the flow account do: it is read
by a person or an agent rather than parsed, and it is the one read a caller with no image reader can act on.

`walk` is the one read here that answers numbers, because what it says is a quantity a rule is stated in
rather than a shape an eye judges. Both ends are given as `x,z` and snapped onto the nearest ground within 24
blocks, since a marker's own coordinates are a block in a room rather than a cell of terrain.

## What a walk costs, and in which unit

The walk is `PgmStudio.Geom.Walk`, and it is the one traversal every distance is measured with. It is
**eight-connected**, counting a diagonal as the 1.41 blocks a player crosses rather than as one step, and it
answers four things at once, each in its own unit and **none of them weighed against the others**:

| answer | unit | what it is for |
|---|---|---|
| `reachable` | yes / no | whether there is a way at all |
| `distance` | **blocks** | how far it is, and so how long it takes |
| `blocks` | **blocks placed** | the climb and the bridging — the number a kit budget is compared against |
| `drops` · `worstDrop` | **falls, and blocks** | a fall is free, and still a delay |

**A column offers a place for every surface in it carrying two clear blocks above, and the walk's node is a
place rather than a cell.** A cell is `(x, z)`, a column seen from above; a place is `(x, z, y)`, a cell and
the storey of it. A gallery under a deck is one cell and two places, and they are different somewhere to be —
which is the whole reason the walk cannot be keyed on the cell. A column offering no such surface anywhere is
not walkable at all, and a column with nothing stacked over it offers exactly one place, so a board drawn on
flat ground is a stack of one the whole way through.

The headroom test is what makes a building a building: the terrain under a wall has no headroom, so the wall's
own top is the surface, and crossing it costs the climb. Without it a walled cell reads at the floor the wall
stands beside, which is how a route comes to walk through a house for nothing.

**A step between two places has to fit under the lower one's clearance** — how many blocks are open over it
before the next solid one. A player builds up through open air and falls down through it, so a gallery roofed
sixteen blocks up is not a step from a deck twenty-six blocks over it, while the same gallery where the roof
is cut away is. A place with open sky over it has no ceiling to fit under, so on a board with nothing stacked
the rule never refuses a step.

**A cell with no storey stated means its lowest place**, which is where a player walking in at terrain level
ends up. `from` and `to` take `x,z,y` to say otherwise, and the storey nearest the stated `y` is the one
walked from. A marker the document places says its own: a spawn box states its floor, a wool its `y`, a goal
region its underside, and `NavPoint.Seat` resolves that against a board so the readers cannot disagree about
which storey a spawn is on.

**A climb of Δ costs Δ−1 blocks.** One block up is a step; anything higher is ground the player first has to
make. **A drop is free to 3 and counted beyond it**, because 4 is where fall damage starts — every kit carries
a water bucket, so a fall walls off no route, but stopping to place and drink one is time the distance does
not show, which is why it is reported as a count of falls rather than as blocks. **Void inside a build zone
costs one block a cell**, and a bridged cell is given the height of the ground nearest it, so a crossing is
level and the climb is charged where the player steps up onto the far side. **Water costs no block and blocks
no route**; it is slower, so it doubles the distance of the cells it covers.

`aim` picks which of those the route minimises. `travel` takes the shortest way and reports what it costs to
build; `reach` takes the way asking for the fewest placed blocks and reports how far round it goes. **They
disagree on any board worth the question** — on a 60×140 test board with a relief ridge across it, travel
walks 121 blocks placing 41, and reach walks 144 placing 24.

**`comfort` is the third, and it exists because standoff costs distance.** Both aims above break a tie by
preferring the route whose worst moment is furthest from an edge, but a tie-break sits after the aim's own
quantity and can therefore only buy standoff that is free. Standoff is rarely free: on the flat test board
the route out of spawn crosses a ten-cell neck at a clearance of **1**, and crossing it at its widest **5**
costs **2 blocks on a 121-block walk** — a price no tie-break can ever pay. So `comfort` is a separate
question: among the routes no more than `Walk.Detour` (**10 blocks**) longer than the shortest — the same
ribbon a corridor is claimed with — the one whose worst exposure is least. On that board it answers 123
blocks at a worst clearance of 5.

**`team` decides whose walk it is, and a board with protections has no single answer.** Ground an `enter`
rule bars a team from is taken out of what that team may stand on and what it may bridge onto, so a route
through an enemy protection is not offered. Naming no team walks the ground everybody shares, which is the
right question about a board's shape and the wrong one about whether a player can get somewhere: widen a
spawn's protection until it swallows the approach to its own wool and the shared walk still answers
**reachable in 121 blocks**, while `?team=red-team` answers **unreachable** — which is what the export gate
refuses under `EX1` where the goal is one that team must take. A goal the team *defends* is asked a weaker
question there, since its own wool room bars it by design: the walk only has to reach the barred ground's
border. Both ends are snapped on the shared ground before the team's is walked, so a barred
objective answers unreachable rather than sliding sideways to the nearest cell the team may stand on.

The bound is what keeps it honest in both directions. Unbounded, a standoff route wanders; ordered after
distance, it never moves. And the exposure term is the route's **worst** shortfall rather than its total,
because a sum charges a longer route for its own length and would rank a safe detour below the edge it
avoids. `comfort` has no field of its own — the bound is the journey's own length, so it is answered between
two cells; `render/walk?aim=comfort` shades the travel field and draws the comfort route on it, which is the
pairing that shows what the standoff bought.

**`walk` also says which storey it stood on at every place, which steps it left a walk for, and what stands
beside it.** `places` carries the route's own `y` at each cell — the storey the walk chose, not the ground a
deck or a gallery roofs it with — and `steps` names every consecutive pair whose rise is not a plain walk: a
scramble, a barrier or a drop, classed the way `PgmStudio.Geom.Walk.StepWord` classes any signed step, with
the totals `rises`, `falls` and `worstStep` over the whole route. `?beside=N` (0 to 6 cells, Chebyshev) adds
every distinct thing the provenance record names within `N` cells of any cell the route passes through — a
tree, a boulder, a house, water, a spawn, a goal, wool or an iron cube, the first cell it is met at and its
distance; flora and paint are left out, since neither is a thing a player runs into. `?format=text` answers
the same reading as characters: the route's own numbers, a station at every place it stood with the word and
the signed step where it left a walk, the totals, and what stands beside it — the same table the driver's
route profile printed from a client-side copy that could not know which storey the walk chose.

## A stacked board is drawn one storey at a time

Four reads project a column to one cell — `topdown`, `heightmap`, `surface`, `structures` — so on a stacked
board they draw the topmost storey and whatever shows past it. **`layer` names one instead**: the sketch layer
by its id, drawn as its own ground and everything standing on it, up to whatever the next layer starts at.
A layer the board does not carry is a **422** naming the ones it does, rather than an empty picture; a board
drawn in no layers at all says that instead.

**A storey is the span plus what stands on it**, because keeping only the span would drop the houses, the
trees and the goal markers, which is most of what a picture of a storey is for. The window runs from the
layer's floor to the block below the next layer's floor in that same column, and to the world ceiling for the
topmost. A column the layer never drew contributes nothing, which is what makes a gallery under a deck read as
its own footprint rather than as the whole board. A span is **half-open** — `[YFloor, YTop)` — so the layer
above begins *at* this one's top: a storey whose rock meets the landmass over it with no gap between them
ends at its own last course and not at the world's.

**The provenance record is narrowed with the world.** A claim is recorded per column and carries no course, so
it describes that column's *topmost* block; left whole under a storey read it paints the storey over the one
being drawn — a house onto a cellar floor, a tree into a tunnel. What replaces it is what the spans already
say about the course the storey shows: at or below the layer's own top the block is the rasterizer's terrain
and reads `Ground`, above it something is standing on the storey and the recorded claim is kept only where
this storey shows the column's own top. A course that is neither carries no claim, and the picture's own
legend says which reading it used — `RECORDED PROVENANCE` where any column took its category from a claim,
`MATERIAL ESTIMATE` where none did.

`ymax` keeps the whole record, because a cut taken to look under a roof is still inside the claimed building
and the room under it belongs to the same claim.

`ymax` is the older cut and stays, but it is a single height and separates two storeys only where the upper one
happens to be flat. On `opus5-mineshaft` the deck roofs all 6,400 cells and `ymax=19` does reach the gallery —
because that deck is level, which is a property of that board and not of stacking.

`section` and `column` take no `layer` word: they keep Y and show every storey already. Neither do
`traversability` and `walk`, which run over the ground rather than draw it and answer per storey without being
asked.

## What a theme census counts

`themes/census` counts every ground cell of the built board by the theme that paints it, resolved the way
`TerrainThemeScope` resolves it for the painter: a shape's own override where one claims the cell, the map
default everywhere else. `render/surface` already reads a board against the tone families it was authored
from, but only as a legend baked into a picture; this is the number a board that mashes its themes has no
gate for.

Per theme it answers `cells` and `share` — that count over the board's whole ground — and `materials`, the
distinct surface blocks its cells carry in the finished world, as `id:data name`, most frequent first and cut
at twelve, with `materialCount` holding the true count whether or not the list was cut. `adjacency` is every
pair of themes that shares a border: for every 4-neighbour pair of cells painted in different themes, one
count per unordered pair, largest first — the number that says a hillside painted in three unrelated themes
is three themes mashed together rather than one graded slope. `palette` is the board's own distinct
`id:data` count, over every theme, the same reading `render/surface`'s legend gives a single board without
naming which theme spent which block.

`?format=text` answers `THEMES  n themes over c ground cells, p distinct surface blocks`, one row per theme
with its cells, share and materials, then `borders:` and one row per bordering pair with the cells that cross
it.

## One of them misleads, and it has cost a reader a conclusion

A caveat met *after* a conclusion has already cost the conclusion, so each rides in its own route's summary
rather than in a document somebody may not have open.

**`structures` cannot see a town this studio built** (`B149`). It finds roofs by material, and its terrain
list swallows stone, cobble, sandstone, stone brick, quartz and stained clay — so a cottage roofed in any of
them reads as ground. On a studio-built world take `render/topdown?subject=structure`, which reads the
provenance sidecar and draws what the build recorded itself placing.

**A made thing is neither ground nor structure, and `subject=made` is its picture.** A ship, a balloon, a
crane and a car are laid on layers of their own, so a render keyed on the pass would draw a balloon flying
thirty blocks over a field as that field's surface and a house beside it as a house standing on one.
`ProvenancePass.Made` is claimed for those columns after the dressing pass — the passes between work on the
terrain round a made thing rather than on the thing, and the harbour that fills round a hull claims every
column it filled — so `subject=structure` carries buildings only and `subject=made` carries the made things
over the terrain they stand on or fly above, with nothing the dressing placed in the way.

And one that is not a fault: **`surface`'s magenta is not a material.** It is the honest answer for a block no
tone family claims, and the legend says how many there were.

## What each read is for

`answer-shapes.md`, beside this document, measures which of these reads a model can subtract from and
which it can only gauge, against two runs where the difference cost a board; the reads it asks for are on
the board as `WS19`–`WS22`.

`column` is the workhorse and the only honest answer: every picture beside it is a projection, and this is
what is actually at a coordinate. It is the read to reach for when a picture and a document disagree.

**A claim about a shape — a bank, a wall, a stair, a basin — is read off a transect and not off a column.** A
column answers what is at one coordinate, and a shape is a claim about the *step* between two of them, which
neither a column nor a picture states. A basin whose wall is a sheer eight-block face reads, column by column,
as a set of true numbers that say nothing about the wall; the same two cells as neighbouring stations on a
transect answer `BARRIER +8 at (-52, 0)`, which is unmissable where the numbers alone were not. Every station
carries the ground, the storey a walker stands on, the water and the highest block in its column, and the step
from the one before it, classed the way `PgmStudio.Geom.Walk.StepWord` classes every step in the studio —
walked, scrambled, a barrier, or a drop.

`topdown` keeps no Y at all — a riser, a ramp's step heights, a stamped room's floor and a goal's clearance
are none of them in it. `section` and `column` are the two that keep it, which is why every shipped roof fault
was visible in a section and invisible from above.

**`traversability` joins two cells only where the ground between them is ground rather than a wall.** A roof
carries two clear blocks of headroom like any other surface, so an unbounded flood climbs a house and runs a
route over it — a road blocked by a building reads as one whole component. The bound is `Walk.WallRise`:
more than five blocks of rise is a face a player goes round, not up, so a house, a cliff and a wall each stop
the flood at their own foot while a bank, a ramp or a flight of steps still joins what it climbs between. A
bridged cell carries no height of its own and joins whatever it touches, which is what a build zone means.
The export's reachability gate is deliberately not bounded this way: there the question is whether anyone
*can* get somewhere, and a player carrying blocks pays for the climb.

`walk` and `render/walk` are the two that answer *at what price*, which is the half `traversability` cannot
reach: that picture says whether a board joins up, and every distance under it was a flat step count. The
picture is the one to take first — the field shades every cell at once, so a pad four blocks over its own
approach reads as a colour step exactly on the room's footprint, and a pond reads as the blue it is.

Its key is two-dimensional, because the picture is: **reading across a ramp is how dear a cell is, reading
down the three ramps is what a player is standing on to be there** — ground for nothing, void at a block a
cell, water at twice the walk. One number line serves all three, since the value axis is the same for every
footing. A flat list of swatches cannot say that, and a reader given one reads the hue as a class and the
class as a hue.

`heightmap` answers whether a relief solved into the shape it was drawn as, and shows a flat pad butted
against a hill as the ruled edge it is. Its text twin answers the same question as characters rather than
tone: a neighbour's height is a subtraction rather than an estimate, and the houses, halls, water, spawns
and goals overprinted on it say what the relief carries rather than leaving a reader to guess from shape
alone. `surface` answers whether a board's paint is the palette it was authored from — a whole tone family
taken where two members were meant reads as the noise it is. `mirror` answers whether a board somebody
believes is symmetric actually is.

`slopes` answers where a relief is too steep to be crossed for free, the way `heightmap` answers its overall
shape: a cliff reads as a line of `#`, a ramp as a band of `.` running through it, and ground graded past
what a player can scramble up reads as a page of `#` rather than one count with nowhere to check it. Its
faces name the barrier runs worth walking to in-game, largest first.

It asks that as **two** questions, and the picture separates them. The first is shape: a column is solid where
its image is solid, at the same heights, taken through the build's own transform about the map's stated
centre, and a mode of order four asks all three images so a column counts as paired only when the whole orbit
closes on it. That is what a player can be disadvantaged by, and a column failing it draws red. The second is
material — whether the two halves are made of the same thing — and it is asked only of the columns that
already pair by shape, since a column standing where its image does not has no material to compare; one that
stands right and is finished wrong draws amber, and the verdict line carries both counts. A block's **data is
skipped where it carries a team's colour** (wool, stained clay, stained glass and pane, carpet), so a red wall
facing a blue one is the same wall on both sides, which is what a team tint is for. Everything else is
compared as id and data both, because a pattern samples the cell folded into the board's primary image
(`docs/world-export/terrain-painting.md` TP21) and therefore answers alike wherever the orbit lands.

## Limits

The build is paid per request; nothing is cached. A large board is the same cost as an export, which is what
it is.

**`at` names the other axis, and one outside the world is refused.** A cut along x is taken at a z and a cut
along z at an x, which is the easiest thing about this route to have backwards. An `at` outside the world's
own span answers **422** naming the axis and the range rather than a blank picture, because a coordinate
outside the world is a fault and a blank image is the slowest possible way to be told so.

**`section` reads a plane or a slab, and `depth` picks which.** Without it the cut is one block thick, which
is the right read for a `layered` material — the whole reason that material kind exists is to vary a colour
down a riser — and the wrong one for looking at a map, since a cut through a house that misses its walls
reads as floor, air, roof. With it each column takes the nearest block up to that many behind the plane, so
the walls are in the picture. **Hue stays the material and the depth is carried in the value**: what stands on
the cut reads exactly as the slice draws it and what is behind reads as the same material further away, which
is what makes a building behind the cut read as a building rather than as a lighter smudge. The far end keeps
45% of its brightness rather than fading out, because a ramp reaching the background makes "deep" and "nothing
there" the same pixel. Sixteen blocks is the cap — a chunk, and deeper than any room this studio builds.

`column` is still the only read that keeps Y over a single cell, and `sideview` the only one that reduces a
whole map to a depth map per direction; it reads `segment` rows and so answers only for a scanned map,
which is why the two coexist rather than one replacing the other.

The walk runs over **a place per standable surface**, so an overhang, a tunnel and a deck over a yard are each
somewhere of their own. Every distance the studio reports is this walk: the evaluator's spawn, wool and frontline
terms, the plan tier's route and coverage reads and the destroy-goal ratio all solve over a `WalkGround`,
and the bands they are judged against were measured in its unit.
