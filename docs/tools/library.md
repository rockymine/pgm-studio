# The Library tool

## What it is

The library is where a material is authored once and reused. It is the only tool in the studio that knows
nothing about maps: no slug, no stage, no map row anywhere in it. What it holds is recipes, and the tools that
build worlds reach into it to pick one.

Nine kinds, in three families. Six **compose upward**: **Styles** (*Patterns* on screen) — a style is one
pattern, a material mixing blocks; **Themes** (*Palettes* on screen) — a terrain finish made of styles; **Roofs**, **Storeys** and
**Porches** — the parts a building binds, each made of styles; **Houses** — a whole building made of parts and
styles. Two are **recipes a click puts down**: **Trees** and **Boulders**, which compose nothing and are what a
placement names. One places nothing at all: **Biomes**, the byte each column carries, which tints the ground
without adding a block. A style is browsed by what it looks like, everything above it by what it composes to, a
recipe by what it builds, and a biome by the ground it colours. A house's row is a `room_style` and composes to a
`HouseStyle`; the surface calls it what the thing is.

Three routes, and the rail carries the nine kinds. `/library` is the chooser — one card per kind, grouped as
*Terrain* (styles, themes, biomes), *Buildings* (houses, roofs, storeys, porches) and *Nature* (trees,
boulders), each over the pictures of the newest three entries it holds and its count. `/library/{kind}` browses that kind: a strip carrying a name search,
whatever else the kind filters by, and **New**, over a grid of cards. `/library/{kind}/{id}`, or
`/library/{kind}/new`, opens one entry on a page of its own.

Two tools consume the library. The Sketch tool's Theme phase pulls a theme in and pushes one back out, binds a
room style as the shell every wool cage and spawn cube is stamped with, and picks the biome the board's columns
carry; the Dressing phase names a room style, a tree recipe or a boulder recipe on each placement, pulling the
row it picked into the map's own registry. Nothing else reads it.

## What it writes

Eleven tables, one group per level: `style`; `theme` with `theme_bucket`; `roof_style` with
`roof_style_course`, `storey_style` with `storey_style_course`, and `porch_style`; and `room_style` with
`room_style_course` and `room_style_storey`.

**Two directions, and the difference between them is the whole design.** *Inside* the library, everything is
bound **by id**: a theme names the pattern that fills each of its buckets, a room style names the courses and
parts it is built from. So editing a style reaches every theme and every room style binding it — the editor
says so when it saves — and deleting one is refused rather than allowed to break them.

*Out of* the library, everything is **copied**. A theme applied to a sketch is stored as the painter's own JSON
in that sketch's registry; the room shells bound in the Theme phase are snapshots; a placed building carries its
style rather than a library id. So a library edit can never rebuild a map that already shipped, and there is no
mechanism by which it could — which is the guarantee, not an omission.

**A map's source may name a row rather than copy it, and what lands is still a copy.** A refinement states
`{"library": "dunes"}` wherever it states a material, a theme, a room style, a prop style or a biome, and the
studio copies the row in when the source is applied, so a library edit still never rebuilds a stored board. The
refinement the map keeps records the row each name resolved to and a hash of what was copied, and
`GET /map/{slug}/state` names the ones whose row has moved on since; the next apply takes the row as it is then.
*Driving it without the UI* has the shape.

**A slot holds one block or one pattern.** Every theme bucket and every course of a house, a roof and a storey
is filled either by a block written in place — `block_id`, `block_data`, and `block_laid` for a log lying along
its run — or by a `style` row bound by id, never both. A single block is not a pattern: a block is named by its
id and variant, which the block catalogue (`GET /terrain/blocks`) already answers, so it takes no row and no name.
What the library lists as patterns is therefore only what mixes blocks, or tints one by team.

**A name is letters, digits, spaces, dashes and underscores, and names one row of its kind.** A source names a
row by it, so it is one a person types — no space at either end, none doubled — and two rows of a kind never
carry it, compared without case; every table holds a unique index over its names. A save naming a row otherwise
is refused, `LB4` for the characters and `LB5` for a name taken, and the seed counts a seeded name on, `-2`,
where a row of the author's already carries it. A seeded biome takes the game's name made one:
`Mesa (Bryce)` is `Mesa Bryce`, `Extreme hills+` is `Extreme hills plus`.

**The library holds each pattern, roof, storey and porch once.** Two rows holding the same material are one
pattern, so a save of a material the library already holds is refused, naming the row that holds it (`LB3`),
and a theme import binds that row rather than adding a copy. A roof, a storey or a porch many seeded houses
share is one row, which every house stacking it binds.

## The four levels

### A style is a pattern, and a block is not one

`style` rows carry a name, a kind and `params` — the serialized `TerrainMaterial` the painter itself reads,
any of the kinds below but the two that lay one block: a `solid` and a `laidLog` are what a slot holds in place,
and saving either as a pattern is refused (`LB1`). There is no second model of a material anywhere: the same editor authors a library style and a theme bucket in
the Sketch tool, and the kind is read back off the JSON node the editor rewrote, so the row's kind and its
params cannot disagree.

**A style may be filled with another style, because a style is a material and materials nest.** Every slot the
material editor draws — a layer of a stack, a band of a voronoi, a patch of a cell field, a tint's neutral
fallback, a frame's panel — offers *Fill from a pattern…* beside the control naming its kind, and the two answer
the same question: a kind gives an empty recipe of that shape, a style gives one already written. It is how a
palette of small styles becomes a large one, which is the reason the level exists.

What lands is a **copy**. The material tree is the wire format the painter deserializes and the form a map
snapshots, so a slot holding a style's *name* would have to be resolved by everything that reads one, and a
map's paint would change under it whenever the library did. Once filled, the material is ordinary JSON edited
like any other — the style is where it came from, not what it is — which is why the control goes back to
reading as an offer rather than staying on the name it was given. Editing the source style afterwards does not
reach what was filled from it.

**A pattern is named for what it contains.** A seeded pattern, and one a theme import lifts in, takes the name
`PatternNames` describes it by: the blocks it lays in the order it first lays them, then the kind laying them —
`cobblestone-andesite-noise`, `stone-bricks-andesite-polished-andesite-cells`, `team-stained-clay-gray-neutral`
— with the words for what tells it apart where two would read alike, `cobblestone-andesite-columnar-noise`
beside it. The author's own ground patterns keep the names their author gave them. A seed rearranges which block
lands where and leaves what a wall looks like, so two seeded patterns never differ by their seed alone.

**The house styles boards are built with are in the seed folder.** Fifty-seven styles are kept as the stamper's
own JSON, a file of each name under `Minecraft/Library/houses` (*The seed*, under *Driving it without the UI*),
and seeded into the room library under those names. A board names one as
`{"library": "brick-roofed-stone-and-dark-oak-house"}` wherever it states a room style or a house prop's style,
and gets the building the file describes: each composes back out of the store to exactly the style the file
states, which `LibrarySeedTests` asserts style by style, and each passes the house gate and the name rule
(`HouseStyleValidationTests`, `HouseNamesTests`). They stand on no row of the showcase, since a style carries no
footprint.

**A house style is named for what it is** (`HS19`): describing words — its materials, its roof, how it is put
together — then the kind of building, `brick-roofed-stone-cottage`, `oak-stilt-house`, `hay-gambrel-barn`.
Every word comes from one of two closed lists in `HouseNames`, which `GET /api/room-styles/name-words` answers,
and neither holds a board's name, a role a room plays on a map, an occupation or a place: those say where a
style was first used rather than what it is. The name is checked when a room style is saved, so the seeded
names, the presets' included, are held to the rule a person saving a row is.

A style's card picture travels with the row rather than costing a request per card, because a library is
browsed by what its entries look like. The editor previews two views of one material: a **plan**, one course
seen from above, which is where a voronoi and the three noise fields vary, and a **section**, one row of
columns cut open downward, which is the axis a layer stack varies along and the elevation a wall material is
seen as. A stored style's `params` is exactly one of the nodes below, and a saved row is that node plus a name:

```json POST /api/styles
{ "name": "quartz and diorite rim", "kind": "noise",
  "params": "{\"kind\":\"noise\",\"seed\":4021,\"scale\":2,\"octaves\":1,\"stops\":[{\"kind\":\"solid\",\"id\":155,\"data\":0},{\"kind\":\"solid\",\"id\":1,\"data\":3}]}" }
```

### The fourteen kinds

Every kind resolves one block per cell, and every one of them **nests**: wherever a material is asked for
below, any of the fourteen may stand — so a voronoi band can be a team tint, a layer of a stack can be a
noise field, and a wall stripe can be a checkerboard. `id` and `data` are the block and its variant.

`kind` is what the reader dispatches on, and it is read **wherever it sits in the object**: a material, a
style or a theme reordered by a formatter or a re-serializer says exactly what it said before, because key
order carries no meaning in JSON. A `kind` that is absent, or names none of the fourteen, is refused at the
read with `GET /api/terrain/patterns` named — which is the endpoint that answers every kind's own field list,
and the one to read rather than guessing a field name off a kind's.

**`solid` — one block everywhere.** The leaf every other kind bottoms out in.

```json POST /api/terrain/material-preview
{ "kind": "solid", "id": 1, "data": 0 }
```

**`layered` — a band stack read along a stated `axis`.** Grass over two dirt; a wall's banded riser. Each band
states its thickness, and the stack states what it does where they run out: where the bucket is the stack's
whole space it `repeat`s and a band deeper than declared never falls through to nothing. The other ending,
`handOver`, claims nothing past the last band and leaves whatever is under the stack showing — which is what a
band *inside* a larger space wants, and is why the ending is stated rather than assumed; `beyond` says what
shows there.

The axis is what the thickness is measured in. `depth` (the default) is courses down from the top of the
bucket; `inward` is steps in from the landmass's void-facing edge, so the bands are concentric rings; `height`
is courses up from the stack's own `from` in world Y, so the banding is pinned to the world rather than to the
column — unless it states `follow` (0–100%), which carries the bands with the ground averaged `reach` cells either
side, so strata rise and fall with the land (TP26); `slope` is **degrees of inclination**, which makes the stack an angle mask — one band for the flat,
another for the shoulder, another for the face of the same hill.

```json POST /api/terrain/material-preview
{ "kind": "layered", "stack": { "ending": "repeat", "bands": [
  { "material": { "kind": "solid", "id": 2 }, "thickness": 1 },
  { "material": { "kind": "solid", "id": 3 }, "thickness": 2 } ] } }
```

```json POST /api/terrain/material-preview
{ "kind": "layered", "axis": "slope", "stack": { "ending": "repeat", "bands": [
  { "material": { "kind": "solid", "id": 2 }, "thickness": 20 },
  { "material": { "kind": "solid", "id": 3, "data": 1 }, "thickness": 15 },
  { "material": { "kind": "solid", "id": 4 }, "thickness": 55 } ] } }
```

**`teamTint` — the block tinted by the team that owns the cell**, on the same 0–15 damage scale wool uses, so
clay, wool or stained glass takes the team's colour. A cell with no team — a neutral mid — falls back to
`neutral`. It works on any bucket, not just the wall, and nests inside a stack or a pattern.

```json POST /api/terrain/material-preview
{ "kind": "teamTint", "blockId": 159, "neutral": { "kind": "solid", "id": 159, "data": 8 } }
```

#### The five area patterns

These five vary across the *ground*, and they share one field: **`rise`**, the vertical period of the pattern
in blocks, `0` for none. A pattern of the plane gives every block in a column the same answer, which decides
the surface and leaves a wall face as vertical stripes; a positive `rise` samples the field over the volume
instead, so a wall carries the same fabric its surface does.

**`voronoi` — straight-edged cells with bands running inward from each boundary.** The footprint is tiled by a
jittered grid of period `cellSize`, one seed point per grid cell, and every block belongs to the nearest seed.
Each band states how many blocks inward from the cell boundary it runs; the last band's depth is ignored and
it takes whatever is left of the cell. Reads as a diagram — a grid of lines with cells off it.

```json POST /api/terrain/material-preview
{ "kind": "voronoi", "seed": 1, "cellSize": 10, "rise": 0, "bands": [
  { "material": { "kind": "solid", "id": 155 }, "depth": 1 },
  { "material": { "kind": "solid", "id": 3 },   "depth": 2 },
  { "material": { "kind": "solid", "id": 1 },   "depth": 1 } ] }
```

**`cell` — the same regions, one flat colour each, with the lookup warped.** Where a voronoi draws a diagram,
this draws a **fabric**: flat patches, any two of which may meet. `jitter` (0–100) is how far a site may sit
from the middle of its grid cell — 0 gives the grid squares, 100 gives shards — and `warp` is how many blocks
the boundary wanders, which is what turns a straight-edged diagram into organic patches.

```json POST /api/terrain/material-preview
{ "kind": "cell", "seed": 1, "cellSize": 10, "jitter": 50, "warp": 4, "rise": 0,
  "palette": [ { "kind": "solid", "id": 1 }, { "kind": "solid", "id": 24 },
               { "kind": "solid", "id": 3, "data": 1 } ] }
```

**`noise`, `turbulence`, `electric` — one fractal field read three ways**, each taking `scale` (the feature
size), `octaves` (how many levels of detail) and `stops` (the materials the field ramps through, first to
last). `noise` is the plain field: soft cloud-like ramps. `turbulence` folds it at every zero crossing so it
creases instead of fading — billowed, marbled bands laid out like smoke. `electric` inverts and sharpens that
fold, so the crossings become thin branching filaments with everything else falling away — veins through a
body rather than bands across one.

```json POST /api/terrain/material-preview
{ "kind": "noise", "seed": 1, "scale": 16, "octaves": 3, "rise": 0,
  "stops": [ { "kind": "solid", "id": 1 }, { "kind": "solid", "id": 2 },
             { "kind": "solid", "id": 3 }, { "kind": "solid", "id": 24 } ] }
```

`turbulence` and `electric` take exactly the same fields; only `kind` changes.

#### The six wall patterns

These read the wall's own geometry rather than the ground's, so they draw in section and on a riser and look
flat from above. Three of them read where a cell sits **around** the building's outline — the arc the boundary
walk assigned it — which is what lets a stripe carry round a corner instead of restarting on each face.

**`wallRun` — stripes travelling along the wall face**, wrapping the whole void-facing perimeter. The runs
repeat in order around the loop, each as many arc cells wide as it says, so any number of materials with any
widths cycle continuously around every corner. A cell off the outer perimeter — an internal riser — reads as
arc 0 and takes the first run.

```json POST /api/terrain/material-preview
{ "kind": "wallRun", "runs": [
  { "material": { "kind": "solid", "id": 155 },           "width": 3 },
  { "material": { "kind": "solid", "id": 159, "data": 8 }, "width": 2 } ] }
```

**`wallDiagonal` — the same stripes sheared by height.** `slope` is how many arc cells the pattern shifts per
course up: 1 is 45° on a square-blocked face, larger lays it flatter, negative leans it the other way, 0 is the
vertical run again. The height is read from the cell's own Y rather than from the foot of the wall, so two
walls of different heights standing side by side meet with their diagonals in line.

```json POST /api/terrain/material-preview
{ "kind": "wallDiagonal", "slope": 1, "runs": [
  { "material": { "kind": "solid", "id": 155 },           "width": 2 },
  { "material": { "kind": "solid", "id": 159, "data": 8 }, "width": 2 } ] }
```

**`wallFrame` — an edge material inked around the wall's borders and corners, with a fill inside.** `angle` is
the turn threshold in degrees that counts as a corner, and because the measured turn ramps to a vertex rather
than switching on at it, the same number sets how far the ink wraps round each corner — a low threshold inks a
broad return, a high one only the vertex. `thickness` is the courses taken at the top and bottom, and a wall
too short to hold two of them is all edge.

```json POST /api/terrain/material-preview
{ "kind": "wallFrame", "angle": 45, "thickness": 1,
  "edge": { "kind": "solid", "id": 159, "data": 15 },
  "fill": { "kind": "solid", "id": 155 } }
```

**`checker` — two materials on a board of `size`-block squares**, laid in the face the cell belongs to, so a
wall gets squares rather than the vertical stripes a plane pattern would give it.

```json POST /api/terrain/material-preview
{ "kind": "checker", "size": 1,
  "even": { "kind": "solid", "id": 155 },
  "odd":  { "kind": "solid", "id": 159, "data": 15 } }
```

**`logChecker` — one log alternating upright and laid**, which is the timbering the corpus houses use. On a
`surface` or a `rim` the flat squares read as bark against sawn end, which is what a log floor is. On a face
with no wall run to follow — a freestanding pillar — both squares stand, because a laid log there would show a
cut end on every side a player walks round.

```json POST /api/terrain/material-preview
{ "kind": "logChecker", "size": 1, "id": 162, "data": 0 }
```

**`laidLog` — one log lying along the wall, never across it.** The axis a log is laid on decides which two of
its six faces are the sawn ends, and a log laid across a wall puts one straight out at the viewer; this takes
the axis the wall is going. At a corner, where the wall has faces on both axes, the log stands upright — which
is what a corner post is, and so does a log on a face with no run at all.

```json POST /api/terrain/material-preview
{ "kind": "laidLog", "id": 17, "data": 0 }
```

### A theme is a terrain finish made of styles

A `theme` binds a style to each of the four buckets and carries the geometry that is not a material.

**What a bucket is.** The painter reads a column of stone top-down and hands each block to exactly one bucket,
and the four are read in that order:

- **`rim`** — the cap on the top course of every **edge** column: what the ground reads as from across the
  void. It claims a stated `depth` of top courses.
- **`surface`** — the stack finishing the top of **interior** columns, claimed downward, also to a stated
  `depth`. Grass over two dirt is a surface three deep.
- **`wall`** — the exposed **riser** under the rim, down as far as the shallowest drop beside it. A team tint
  here is what makes a team's ground read as theirs. Its depth is not a knob: the riser it finds is its depth.
- **`fill`** — every block no other bucket claimed, the body of the terrain under the surface and behind the
  wall. It takes what is left, so it has no depth either.

**Ground something rests on has no rim and no surface.** Where another layer's stone stands on a column — a
tunnel wall on the ground it rises from — or a stamp is set down on it, that column's top course is not open
ground: the wall takes it where its face shows and the fill where it does not, so a cliff under a wall reads as
rock rather than as a buried stripe of turf. Its open neighbours keep their surface, and a `boundary` rim lips
them along it the way it lips a room (TP25).

They **fall through** in that order: an unpainted rim falls to the surface, an unpainted surface or wall to the
fill, and the fill to nothing — which is why the fill alone cannot be switched off. Only the rim and the
surface carry a `depth`. Both, and the wall, may be disabled outright, and disabled is not the same as unbound:
a theme that binds no rim keeps the built-in one, while a theme whose rim is *off* paints no rim at all.

**The rim and the surface take a band, and the wall and the fill take a material.** A band is
`{"material": …, "depth": N}`, because those two are the buckets with a depth to state; the other two are a
material written directly. A bucket key left out entirely keeps its default, and a bucket stated with the
wrong one of those two shapes is refused at the read, naming the field — a bare material at `surface` leaves
the band holding no material at all, which the painter would otherwise meet a whole raster later.

Three knobs sit beside the buckets rather than in them. **`bedrock`** is the floor course, either an absolute
thickness or a terrain-relative depth, and the band resolver always clamps a bucket's depth to the stone above
it, so no bucket ever recolours bedrock. **`rimEdges`** decides which edges the rim caps at all: `void` caps
only where the ground borders the void, so a staircase of stacked plateaus takes one rim around its outside
rather than a lip on every tread; `drop` caps wherever the ground falls away; `boundary` caps every plateau
boundary, a face against a structure included. **`wallOnTerrainFaces`** decides whether risers inside the
terrain are painted as wall or left to the fill.

That is the whole of a theme, and this is one written out — the form `GET /themes/{id}/json` returns, the form
a sketch stores in its `themes` registry, and the form the export consumes:

```json POST /api/terrain/theme-preview
{
  "bedrock": { "relative": false, "value": 1 },
  "rimEdges": "drop",
  "wallOnTerrainFaces": true,
  "rim":     { "material": { "kind": "solid", "id": 155 }, "depth": 1, "enabled": true },
  "surface": { "material": { "kind": "layered", "stack": { "ending": "repeat", "bands": [
                 { "material": { "kind": "solid", "id": 2 }, "thickness": 1 },
                 { "material": { "kind": "solid", "id": 3 }, "thickness": 2 } ] } },
               "depth": 3, "enabled": true },
  "wall":    { "kind": "teamTint", "blockId": 159,
               "neutral": { "kind": "solid", "id": 159, "data": 8 } },
  "wallEnabled": true,
  "fill":    { "kind": "solid", "id": 1 }
}
```

Note the shape: the rim and the surface are **band objects** — a material plus a depth plus a toggle — while
the wall and the fill are **bare materials**, the wall's toggle riding beside it as `wallEnabled`. That is the
seeded `meadow` finish, near enough: a quartz rim, grass over two dirt, a team-tinted clay wall, a stone
body.

In the library the same theme is a row of bindings rather than a document — each bucket holding a block or naming a
pattern id, with a depth and a toggle — and `GET /themes/{id}/json` is what assembles the row into the above. The
theme above is two blocks and two patterns: the quartz rim and the stone fill are not rows of their own, and the
grass-over-dirt stack and the team-tinted clay are.

**Unbound is a real answer, and so is switched off.** A bucket holding neither a block nor a pattern resolves to stone — what
`TerrainTheme.Default` states for every bucket, and what unpainted ground already is — and is stored by being
left out, which is what makes the library worth having for the case it was built for —
a rim and a fill bound once and reused, with only the surface and the wall differing between themes. A theme
needs neither a rim nor a wall, so the toggle is offered whether or not a style is bound: an unbound bucket
that is **off** keeps its binding, because it says a great deal, and only an unbound bucket that still paints
is dropped on save, because that one says nothing.

The preview is a sample plateau painted and cut open, plus a top-down swatch per bucket.
`GET /themes/{id}/json` assembles the row into the painter's own theme JSON — the form the export consumes and
a map snapshots — and `POST /themes/import` runs the other way, lifting a whole theme JSON into the library: a bucket laid in one block
holds the block, and a bucket laid in a pattern binds the library's own copy of it, or a new pattern named for
what it contains where the library holds none. That import is also what the editor's **Start from JSON**
offers while a theme is being started: a whole painter theme pasted in — what an agent writes over the API,
what a board saves out — lands as an editable row rather than a stored blob. It creates a row, so it is
offered on a new theme and not on one that already exists.

### A part is a roof, a storey or a porch

One composer serves all three, because they are the same act — pick a kind, fill that kind's parts with blocks
and patterns, turn that kind's knobs — and what differs between them is data rather than a third editor.

A **roof** is everything above the eave: its form, pitch and overhang, whether it carries a hole and a ridge
cap, the `roofSlab` a half-course rise steps on every odd course or the `roofStair` a whole-course rise steps
in, its `roofWear`, and a material for each of its `roof`, `verge` and `gable` parts. It has no thickness: a course stack counts upward from its part's own base, which a
wall has and a roof does not, since a slope's depth at a cell is however many courses close the step down to
its neighbour. The slab is the roof's own rather than the house's, which is what lets the slab/pitch pairing be
checked here as well as on a whole shell — and what makes a house binding a roof take that roof's answer, the
way it takes its form and its pitch.

A **storey** is one room: the `clear` a player stands in — never under three, because a room has to be stood up
in — the floor's border width and inlay inset, its windows, and courses for the `wall` (which does stack) plus
`post`, `ceiling`, `field`, `border` and `inlay`. A house stacks storeys in order, so a shop under two flats is
three bindings of two presets.

A **porch** is the strip of footprint the walls give up and what stands on it: depth, inset, edge, roof and a
rail block. It carries no courses at all — its deck is the house's floor, and its canopy is the house roof's
material unless the house binds its `canopy` part — so what is left to it is its shape. Its roof is a gable
unless it names another, and a shed is complained of on a porch saved here as on one bound to a house (`HS14`).

Every part's picture stands it on a plain sample building, so what differs between two cards is the part and
never the house around it.

**A seeded part is named for what it is laid in.** The roofs, storeys and porches the seed cuts out of its houses
are named by `PartNames`: a roof by its body's blocks and its form, `spruce-planks-gable-roof`; a storey by its
wall's blocks, `cobblestone-andesite-spruce-log-storey`; a porch by its rail and edge, `jungle-fence-front-porch`.
Where two would read alike, each takes the fewest words for what tells it apart — its pitch, its verge, its
windows, how high it stands — so `dark-oak-planks-gable-roof-pitch-2-oak-log-verge` sits beside
`dark-oak-planks-gable-roof-pitch-1-oak-log-verge`.

### A biome is the colour a column carries

A `biome_pattern` row carries a name, a kind and `params` — the serialized `BiomeField` the export itself
reads. It is shaped like a style and stored beside one because it is the same kind of thing: one recipe, named
once and reused. What it states is not a block but the byte a client reads to tint grass, leaves and water, so
a board wearing one changes colour without a single extra block being placed.

The three kinds are the three a field takes. **`solid`** is one biome over the whole area — the plainest thing
to say, and what the seeded presets are: one per biome, so a board that is simply desert is a
pick rather than a document to write. **`cell`** is jittered regions each taking one biome from a palette,
which is the shape a biome map actually has, and states a `seed`, a `cellSize` in blocks and a `jitter` from 0
(a grid) to 100 (a fully wandering boundary). **`noise`** is a fractal field cut into bands, one biome per
band, so regions wander into one another rather than meeting on a cell wall; it states a `seed`, a `scale` in
blocks and `octaves`.

```json POST /api/biome-patterns/preview
{ "kind": "cell", "seed": 91, "cellSize": 45, "jitter": 85, "palette": [1, 4, 5] }
```

**The card is a patch of grass seen from above, under the field.** Grass because it is the block a biome moves
most and the one a board is mostly made of, so the picture shows the difference an author is actually choosing
between — a `solid` row reads as a flat green and a `cell` row draws the regions it will lay down. It is
measured in blocks at the size an area pattern's style card uses, since a biome field is read in blocks
exactly as one is, and it is drawn through the export's own palette and biome tints, so a card cannot promise
a colour the game will not show.

**A map takes a copy, never a key.** Picking a pattern in the sketch's Theme phase copies the field onto the
board (`biome`) and records the row it came from (`biomeSource`), the same doctrine a theme and a room shell
follow: editing a library pattern must never silently retint a shipped map. Nothing binds a row, so forgetting
one asks no question and changes no board.

### A tree and a boulder are recipes a click puts down

A **placement is a position; what stands there is a recipe.** A path and a fluid channel are *traced* on the
canvas, so pre-authoring one is authoring a shape without its place and their knobs stay in the Dressing
phase. A tree and a boulder are a *click* — there is no geometry to draw — so what is placed is a point plus a
name, and the name is a row here (author).

A `tree_style` is one of **two trees**, which its form picks. A `template` tree names a **species**, whose row
carries the wood, the canopy profile and the proportions, and scales it by height. A `copied` tree is one an
author built in a world and the cutter took out of it, and it carries its own **body**: every block as
`[x, y, z, id, data]` from its foot, the lowest log, which stands at the origin and rests on the ground. Its
height is read off the body rather than stated, and it has no knob — it is retuned by cutting it again. Beside
the body it carries its **cut**, `{world, x, y, z, at}`: the world directory it was cut from, the foot's world
coordinates there and the time of the cut (UTC), which a template never carries. Each form reads only its own fields, so the
ones it does not read are inert rather than wrong, and switching form keeps every field: a body survives a
look at what the same recipe would be as a template, and the species chosen for it is still chosen on the way
back. A `boulder_style` is four statements — form, size,
whether moss specks a tenth of its sky-lit faces, and the material it is cut from, which is a full terrain material and
so may be any of the fourteen kinds.

**A copied tree is cut out of a world, not typed in.** `dotnet run tools/seed-trees.cs <worldDir>`
reads a world where every tree stands clear of every other, takes each connected body of logs, leaves and
carpentry — wooden slabs and stairs, fences, vines — that rests on something, and writes it into the seed
folder's `trees.json`, which the seed files into the library. A body
hanging in the air is a fragment of a tree that broke and is reported rather than filed.

**A copied tree is named for what it is.** The trees sort into rows by the z they stand at, placed along x,
and what each row is — a willow, a large pine, a tiny oak — is the author's statement, in `kinds.json` beside
the world's `region/`: `{"rows": {"17": "willow"}, "trees": {"7-4": "sequoia"}}`, the second for a tree its
row does not describe. A tree is filed as `<kind>-<n>`, counted through the world in row order and along x,
so two rows of one kind share one count — `oak-1` to `oak-10` over three rows. A world with a filed row the
file names no kind for is refused, since nothing in the blocks says what a tree is: the showcase's willows are
dark-oak log under oak leaves. A library row is matched by its **cut** — the world it came from and the foot it stood on — so a re-cut updates
the same rows at the next start and a relabelled row renames them. A wool tree opens a row of
its own whether or not `--wool` files it, so one flag does not move every row behind it. The 94 trees of `pgm-studio-mapgen/corpus/tree-showcase` are the corpus it was
written for, and
cutting them is the only way a `copied` row comes to exist: the seed files the folder's cut beside the seven
template species and the four erratics, and reads no world itself.

**A cut can name who built the tree.** `--builder=<name>` records a Minecraft name on every cut the run files,
answered as `cut.builder`; a pull carries it into the map's recipe as `builder`, and a map the tree stands on
credits them as the original builder of the copied trees (`docs/world-export/sketch-world-export.md` §4a). Every tree of
the showcase was built by rockymine, so a showcase row names them and a cut from another world names nobody
until it is filed with the flag. The editor shows the name under the cut.

```
dotnet run tools/seed-trees.cs ../pgm-studio-mapgen/corpus/tree-showcase --builder=rockymine
```

**The cut is a file, and a re-cut of an unchanged world writes the same bytes.** Every tree the run cuts is
written with its name, the foot it stands on in the world, and the recipe this library answers for it at
`GET /api/tree-styles/{id}/json` — one body row to a line, so a re-cut diffs by the block — to
`src/PgmStudio.Minecraft/Library/trees.json`, or to `--json=<file>`. The authoring repository keeps the same cut
at `pgm-studio-mapgen/corpus/tree-showcase/trees.json`, and a board copies its trees from that file rather than
from any studio's library, so its trees do not move when a library is re-seeded or a row renamed.

```
dotnet run tools/seed-trees.cs ../pgm-studio-mapgen/corpus/tree-showcase --builder=rockymine \
    --json=../pgm-studio-mapgen/corpus/tree-showcase/trees.json
```

**A `copied` save without a cut is refused.** The cutter is what writes a cut and nothing else does, so a
row claiming the form states one or is turned away with `DR-COPY` at **400** — a block list typed into a
request, a dead-bush cluster, a log pile or a crate is not a tree cut from a world, and the library files no
such thing as one. There is no form word for a hand-built body: a prop that is not a tree cut from a world is
not filed here at all. A re-save carries the `cut` the recipe's `GET` answered, which is what the editor sends.
A row filed before the cut was recorded loads, browses and places as it did and answers no `cut`, so it cannot
be saved again as `copied` until the cutter files it again — the next start files every tree of the seed folder again with the cut recorded.

**The card is the whole picture, and that is the point.** Seven woods differ in colour and seven species differ in
*shape* — a notched cone is a spruce, a flat umbrella on a leaning trunk is an acacia, a dome hung with curtains
is a willow — and neither reads off a
number; a copied tree has no number at all. So both kinds browse as one card each, drawn through the pass that
builds them, and the editor's own stage draws the draft larger for the same reason: a recipe is tuned by
watching one knob move the picture.

**A pull copies the row into a map's own registry.** `GET /tree-styles/{id}/json` answers the recipe as a
dressing document states it, and picking a card in the Dressing phase files it under the row's name in the
sketch's `dressing.styles` and names that key on the placement. The registry is the **document's**: the export
reads a stored layout and has no database to resolve a row against, and a shipped map must build the same way
next year as today — so retuning the library row changes the next pull, not a map already written. Retuning
the *registry entry* changes every placement in that map wearing it, which is what naming a recipe is for.
Nothing asks before a recipe is deleted, because nothing binds it.

### A room is a building made of parts and styles

A `room_style` carries the extents and knobs of a whole shell — floor depth, wall height, roof form, pitch,
overhang, border and inlay, the doorway's fill, width, height and head, beams, windows and gable windows, an
optional porch — plus three ways of composing: per-part **course stacks**, optional bound **roof and porch style ids**,
and a **storey stack** whose position in the list is the position in the building, ground first, so there is no
ordinal on the wire. Reordering the list reorders the house.

**Every field the row stores has a control, and each sits at the part it belongs to.** Beams are the wall's
timber frame, so they are a wall knob; the slab a roof steps in and the openings cut into the gable are roof
knobs; the door's width and the lintel over it are the doorway's. The one shape written once is the window:
a wall's opening, a storey style's and a gable's are the same seven knobs, so they are one `WindowFields`
component rather than a copy per surface — which is also where an opening's **host block**, the band it is set
into, became reachable. What a control offers first is a state that passes: a beam starts as a log (`HS1`), a
head as stairs over a slab of the same material (`HS4`), a gable's opening as the wall's own.

**An editor loads a row through `RoomStyleDetail.AsSaveRequest()` and never through a field list of its own.**
A house is loaded into a draft and the draft is PUT back, so what the load leaves out the save writes away —
and a field with no control is exactly the one a hand-written list forgets. The mapping lives in one place so
a field added later is added to it rather than around it.

A course names its part, its ordinal (0 being the course nearest that part's own base), what it is laid in — a
`block`, or a pattern's `styleId` — and how many courses it runs. `post`, `verge` and `canopy` take one material rather than a stack — a post
is a post all the way up, and a canopy is one block rim and all — so only their first course is read. A part
with no courses keeps the built-in finish, exactly as an unbound theme bucket does, which is what makes a room
style that only changes its roof worth storing.

**No house carries a footing** (author). A style's `foundation` is what it stands on: a `plate` claiming downward
from the course players walk on, that plate's `surface` zoning, and a `footing` ringing it one block proud. The
footing is complained of on every style saved (`HS7`), so a house's walls meet the ground flush; it is a state
rather than a block that happens to be air, and absent is the one the author asks for. The `sill` part is what binds
one, and the editor offers its slot only to a row that still carries it, so the binding can be taken off.

**Windows and rails are picked as a block, not as a style**, and the reason is worth keeping: their metadata is
*geometry* — which way a stair climbs, which half a slab fills — while a material resolves its own data from
where the cell sits, which would turn every stair in a wall the same way. A window's `hostBlock` names the
block it may be cut into, so a seat chosen by spacing on a banded wall does not land half in one band.

**Six fields name a block for that geometry rather than for what it is made of, and `GET
/room-styles/block-kinds` is the catalogue of them.** It answers, per field, the kind of block it takes
(`stair`, `slab`, `log`), the other statement that puts it in play — a door head's `fill`, a window's `form` —
and the sentence saying what the geometry does with it; then, per kind, every id that carries it with the
material it is cut from. Both halves are the table `HS1` refuses from, so a block it offers is one the gate
accepts, the `means` a field carries is the sentence the refusal names it with, and the `material` shown is
the one `HS4` pairs a head's stair and its slab fill by. The kinds are membership by id alone: `stair` is the
thirteen stair ids, `slab` the three **single** slabs (a double slab is a full cube wearing the name), `log`
the two id pairs the six woods split across.

A room style previews in four views — the building standing up, and a plan, a section and a cutaway of it —
and the editor shows them as the building over a row of the three cuts, because what a building looks like and
how it is made are two questions. Any one can be asked for alone, which is the only way to read a cut at the
size the stage can give it; the chips over the picture say which. A library card carries the section alone,
since a grid of rows can afford one raster each and not a world each.

All four are drawn on the shell the `footprint` word names, asked in the dock at the foot of the stage rather
than among the views: which view and at what proportion are two questions, and a row of nine capsules in one
corner reads as one long list of neither. Neither is a field of the style — both change what the picture is
taken over and nothing about what a save would store.

### A seeded house, written out

`brick-roofed-sandstone-house` is one of the presets the seed puts in: end stone and sandstone under a brick
roof, between pillars of smooth sandstone. Two courses of end stone run under five of sandstone, and at each
corner a smooth sandstone pillar stands the wall's height, the same stone dressed. The roof and its verge are
one material, which is what a roof laid in a single thing looks like, and the gable face comes back down to the
end stone the base is in so the two ends of the building answer each other. Its doorway wears a door head:
birch stairs in the two corners of the opening's top course, so the doorway loses its square top.

This is what `GET /api/room-styles/{id}/json` answers for it, unwrapped from its `styleJson` string — the form
the stamper takes, a sketch's Theme phase stores, and a placed building carries:

```json POST /api/room-styles/preview-snapshot
{
  "foundation": {
    "plate": { "extent": 1, "stack": { "ending": "repeat", "bands": [
        { "material": { "kind": "solid", "id": 24, "data": 0 }, "thickness": 1 } ] } },
    "surface": { "field": null, "border": null, "borderWidth": 1,
                 "inlay": null, "inlayInset": 2, "isPlain": true },
    "footing": null },
  "roof": {
    "form": "gable", "pitch": 1, "overhang": 1,
    "slab": -1, "slabData": 0,
    "ridgeCap": false, "hole": false,
    "body":   { "kind": "solid", "id": 45,  "data": 0 },
    "verge":  { "kind": "solid", "id": 45,  "data": 0 },
    "gable":  { "kind": "solid", "id": 121, "data": 0 },
    "gableWindows": { "form": "none", "block": 102, "data": 0,
                      "hostBlock": -1, "hostData": 0,
                      "sill": 2, "width": 2, "height": 2, "spacing": 3 } },
  "wall": { "extent": 7, "stack": { "ending": "repeat", "bands": [
      { "material": { "kind": "solid", "id": 121, "data": 0 }, "thickness": 2 },
      { "material": { "kind": "solid", "id": 24,  "data": 0 }, "thickness": 5 } ] } },
  "post": { "kind": "solid", "id": 24, "data": 2 },
  "windows": { "form": "stairLattice", "block": 135, "data": 0,
               "hostBlock": -1, "hostData": 0,
               "sill": 4, "width": 2, "height": 2, "spacing": 3 },
  "doorway": {
    "door": "air", "width": 2, "height": 3,
    "head": { "form": "arched", "block": 135,
              "fill": "upperSlab", "fillBlock": 126, "fillData": 2 } },
  "storeys": [], "porch": null, "front": null,
  "beams": { "block": -1, "data": 0, "reach": 1, "any": false }
}
```

Read it against the levels above and the whole model is visible in one object. **A part the building has more
than one statement about is an object of its own**: `foundation` is what the building stands on — its plate,
the footing round it and how the plate's top course is zoned; `roof` is everything above the eave, its three
materials and the seven numbers that shape them; and `doorway` is the way in, its size, what fills it and the
beam over it. None of the three appears as a field beside the rest. Which wall the doorway is cut through is
**not** one of them: that is `front`, the wall the whole building fronts on and the one a shed roof falls
toward, which is why it is the style's own. `wall` and the foundation's `plate` are **band stacks** — a
material and how many courses it runs, counted from the part's own base, with an `ending` saying what happens
past the last band — while `post` and the roof's `body`, `verge` and `gable` are **single materials**, which
is why the two end-stone-and-sandstone courses are a list and the brick roof is not. `footing: null` is the
absence of a part, not an empty one — the course a building would otherwise stand proud of the ground on, which
no style is saved with. `storeys` is empty because the shell is one room rather than a stack, and `porch` is
null for the same reason.

## The editor page

Every kind opens an entry in the same three-column layout. The **outline** sits on the left, the document's
name above it. The **fields** of whichever piece the outline has picked sit beside it — the widest column,
since it is the one being authored — with the save, copy and delete bar against its foot, out of the scroll,
so a document long enough to scroll is exactly the one whose save should not have to be hunted for. The
**preview** is a fixed-width companion on the right (`.lib-preview`, `420px`), resizable by the same handle
every other panel in the studio carries. There is no inspector on the route: the fields column sits directly
off the outline instead of across a canvas from it.

**An entry that has not arrived says it is being read, and a refusal is the header's.** The two states look
alike from the fields column — there is nothing to draw either way — and they are not alike at all, so the
column says only the one the header cannot: that the document is still coming. A row that genuinely will not
read sets the header's note and the fields column then says nothing, rather than repeating it. Worth stating
because the client boots cold: the first entry opened after a page load waits on the whole WASM app, which is
seconds, and a failure worded into that gap accuses the library of losing a document it is in the middle of
handing over.

**The outline is the document, not a menu.** Each row carries what its piece states without being opened — a
part names its block or pattern or keeps the *default* finish, a stack says how many courses it runs, a theme
bucket names its block or pattern or says it is *off*. Every slot is filled through one control, `SlotSelect`:
one list offering nothing, *A single block* or a saved pattern by kind, with the block picker under it once a
block is chosen and, for a log, whether it is laid along the run. A material's outline is its own nest: a voronoi's bands, a
stack's layers and a field's stops are each a row, indented by how deep they sit, so a five-entry pattern is
five rows rather than five boxes inside one another.

**`LibraryEditor` takes one parameter that decides how the outline and the fields meet, `Nests`.** A
**nesting** document — a style's recursive material tree, a house's parts, a house part — draws the node the
outline has picked, under a header naming it: its kind, its scalars, its own entries as the rows the outline
is already carrying. A **flat** document draws every section at once in a responsive grid instead, and the
outline becomes a way to reach a section rather than a way to choose which one exists. A theme is the one flat
kind: its whole document is fourteen controls across four buckets plus the geometry that places them, and
hiding eleven of those behind a click would buy nothing a document that already fits a screen needs. Picking a
theme's outline row scrolls the fields column to that bucket's section and marks it with a ring, so a scroll
that lands mid-column still says which row was asked for; each bucket carries the swatch of what it alone
paints, from the same preview call the composed picture answers.

**What the outline shows, the preview answers.** A style draws its plan and its section, either or both by a
chip; a house and a part draw the sample building four ways, and add a dock beneath the picture for the shell
size — 6×6, 8×8, 10×15 or 16×16 — the sample stands on.

**Three of those four are pictures and the fourth is the building.** The plan, the section and the cutaway are
SVG the server drew. What the building *looks like* is not drawn on the server at all: the preview answers the
stamped world's own per-column runs and the browser meshes them into the same WebGL scene a plan and a sketch
are turned in, so a house is drawn by the one renderer the studio has rather than by a second one free to
disagree with it — and it **turns**, a quarter at a press, which is the whole reason a building is worth
drawing in 3-D rather than in projection. The turn sits over the picture and appears on hover, because the
canvas fills its box and a control in the flow would push the box the scene measures. Where WebGL cannot run
the box says so and the three cuts still read. A theme's preview is the single composed picture, the
plateau its buckets and edges finish. The chips above the picture and the dock beneath it answer different
questions — which view, and what the view is taken over — so they sit in different chrome rather than one row
of capsules answering neither clearly.

The draft **is the save request** — for a style, the material's own JSON node; for the rest, the request value
itself — so the preview re-renders from the same value the save would post, and a picture cannot promise
something the save would not build. Saving a style says which way it reaches: adding one is "Added to the
library", editing one is "Saved. Every theme binding it now paints this." A save that creates a row lands on
that row's own route, so the URL always names what is open.

## Refusals

**A single block is not a pattern, and the library holds a pattern once.** `POST`/`PUT /styles` refuse params
laying one block — a `solid`, or a `laidLog`, which is one log laid along its run — with **400** `LB1`, since a slot
holds a block directly; and a material the library already holds under another row with **409** `LB3`, the
finding's `subjects` naming that row. A theme bucket or a course naming both a `block` and a pattern's `styleId`
is refused **400** `LB2` on every save that carries one — `/themes`, `/room-styles`, `/roof-styles`,
`/storey-styles` — since a slot is filled by one.

**A name is a name, and one row's.** Every `POST` and `PUT` of all nine kinds, and `POST /themes/import`, refuses a
name holding anything but letters, digits, spaces, dashes and underscores, or a space at either end or doubled,
with **400** `LB4`; and a name another row of the same kind already carries, compared without case, with **409**
`LB5`, the finding's `subjects` naming it. A row being edited keeps its own name. An import naming nothing is
"Imported theme", counted on where the library holds one.

**A bound row cannot be forgotten.** `DELETE /styles/{id}` answers **409** naming the themes, houses, roofs and
storeys still binding it, so the refusal says what would break rather than surfacing a foreign-key error. It is the
same refusal envelope every other gate answers in — `{error, message, findings}` with the names in the
finding's `subjects` — so a caller reads one shape whatever it asked to forget. The three part kinds answer
identically: a roof, storey or porch a house still wears is refused.

**A composition can be.** Deleting a theme or a room style is unguarded — a theme's bucket bindings and a room
style's courses cascade, and the styles they bound stay. That asymmetry is deliberate: the things something
else depends on are protected, and the things nothing depends on are the author's to discard.

**A house style that names the wrong kind of block is refused where it is saved.** `PgmStudio.Minecraft`'s
`HouseStyleValidation.Check` runs on every `POST`/`PUT` to `/room-styles` (over the composed shell), beside the
name rule (`HouseNames.Check`), and the two `/storey-styles` verbs (over the storey's own window); the two
`/roof-styles` verbs run `HouseStyleValidation.CheckRoof` over the composed roof, which is the whole roof gate
rather than half of it — a roof part states its own `roofSlab`, so the slab/pitch pairing has both numbers
there — and the two `/porch-styles` verbs ask the porch's roof form of `CheckRoofForm`. The same checks run
wherever else a `HouseStyle` snapshot enters the studio: a stored sketch's bound `roomStyles.wool` and
`roomStyles.spawn` and the shell of every building in its `dressing` (`docs/tools/sketch.md`'s Refusals) — the
wool cage, the spawn and a placed house checked identically, since none of the three asks for a different
rule, and there against the build ceiling as well (`WX10`, `docs/world-export/structures.md`). All three roads
to a stored layout ask it: the plain `PUT …/sketch`, `PUT …/sketch/from-plan`, and `PUT …/source`. Every style
finding names a stable rule id (`PgmStudio.Minecraft.HouseStyleRules`), so a caller can act on `rule` rather than
parsing `message`. `HS1`–`HS6` refuse. `HS7`–`HS19` are complaints — the author's verdicts on how a house looks,
and on how a porch and a stilt house stand — and ride on the success as `warnings` and the `Pgm-Warnings`
header:

- **`HS1` — a block named for a role that is not that kind of block.** `beams.block` must be a **log** — a
  beam is the end of a floor timber and docks against the posts, which is what a log is for and the only thing
  it is a house material for. `doorHead.block` must be a stair; its `fillBlock` under `upperSlab`
  must be a single slab; a `windows.block` under `stairLattice` or `arched` must be a stair, and under
  `slabBanded` a single slab; `roofStair` must be a stair; `roofSlab` itself must be a single slab when it names one at all — a **double**
  slab (43/125/181) does not count, since it ignores the half a window or a door head writes into its data and
  is a full cube regardless. Getting it wrong used to build silently — a solid lintel instead of an arch, a
  pane/air/pane stripe instead of a band — and now answers **400**
  `{error: "invalid house style", findings: [{rule, field, message}]}`, one finding per fault, naming the field
  and the block that was wrong. Nothing is substituted for the author. The forms themselves are never refused:
  a `stairLattice` window with a real stair and a `slabBanded` window with a real slab are both allowed on any
  house, a spawn included, and the author has confirmed the forms are not the fault (`B161`'s finding was the
  block, not the pattern).
- **`HS2` — a door too short to walk through.** A door head takes the doorway's top course, so a three-course
  door clears two full courses plus, if the fill is genuinely an upper slab, half of a third — 2.5 at the least
  a door may clear (author). A style whose fill only *claims* to be a slab, or is a solid beam by design, clears
  a flat 2.0 and is refused the same way.
- **`HS3` — a roof's own materials.** A roof is **one material and its verge is one material**: a pattern in
  either is refused, since a roof is read as one plane and a voronoi across it is several blocks in one
  surface, and `roofSlab` is the body's own material, since the slab is the body continuing by halves. The two
  may be the same block — a brick body with a brick verge is a whole brick roof — or they may differ, which is
  how a dark oak verge trims one. A slab named as the whole-block `roof` while `roofSlab` is unset builds a
  see-through roof at a whole block of rise; a **bare** log or a ground material named as `roof` or `verge` is
  refused outright, whichever role it is asked to fill. A **laid** log is not — a `laidLog` roof lies along the
  ridge and is one of the commonest hand-built roofs there is, and what was never a roof is the log with no
  axis rather than the log. It carries its own whole-course rise, so `roofSlab` over one is refused: no slab is
  cut from a log. `roofStair` is held the same way — the body's own material, never over a laid log — and a
  roof naming both `roofStair` and `roofSlab` is refused, since it climbs one way or the other. The **gable** is the end wall carried up and follows the wall, so it is not held to this.

- **`HS4` — a part built of two blocks, built of two materials.** A door head is a stair at each corner and a
  slab between them, and a window may be seated in a host block; each pair is one line of the building and is
  cut from one material. It is the *material* that has to match and not the shape — a stair over a slab is the
  whole point of the pair — so a sandstone stair takes a sandstone slab and a birch stair a birch one.
- **`HS5` — an ore as a building material.** An ore is stone with something in it: it belongs to the ground a
  map is dug out of, and in a wall, a post or a beam it reads as a mistake rather than as a material. Checked
  over every material a style names, patterns walked to their leaves, so it cannot be hidden inside a voronoi.
- **`HS6` — a door head with no wall to carry it.** A storey whose wall is air across the doorway's own
  courses — a house on stilts, an open undercroft — has nothing to cut, so an arch and its lintel stand in
  mid-air. The doorway itself is not refused: an opening cut in an open storey is nothing at all, which is why
  the stilt houses pass and the same houses with a head do not.

- **`HS7` — a footing.** Round a house it reads as a rim rather than as anything the building stands on,
  whatever depth of plate it rings (author); unbind the `sill` part.
- **`HS8`–`HS18` — how the building is put together.** A porch canopy past its own wall (`HS8`), beam ends
  with no laid log behind them or no log posts beside them (`HS9`, `HS11`), a stilt storey on a floor (`HS10`),
  a gable in the verge's block (`HS12`), a laid log at the foot (`HS13`), a shed roof on
  the house, a wing or a porch (`HS14`), a wall checkered in its posts' own log (`HS15`), a wall or gable of
  grass, podzol, mycelium or farmland (`HS16`), snow or ice in a wall, gable or roof (`HS17`), and a storey above
  the ground standing on air (`HS18`). Each is written out where the piece it is about is,
  `docs/world-export/structures.md` §7.
- **`HS19` — a name that says where a style was used rather than what it is.** A room style's name is
  lowercase words joined by hyphens: describing words, then one kind of building, every word from the lists
  `GET /room-styles/name-words` answers. A board's name, a map role, an occupation or a place is in neither
  list, and the finding names each word it could not place.

**A copied tree that states no cut is refused where it is saved.** `POST`/`PUT /tree-styles` run
`PropStyleLibrary.Check` over the request, and a `copied` form with no `cut` answers **400**
`{error: "invalid tree style", findings: [{rule: "DR-COPY", field: "cut", …}]}` (above, *A tree and a boulder
are recipes a click puts down*). A template is never refused there, and a `cut` sent with one is dropped.

Beyond that the library barely refuses. A save needs a name. A storey's clear floors at three. An unbound
bucket that still paints is dropped rather than rejected. Nothing yet validates a *composition* as a whole — a
roof and a storey that would look wrong stacked, a window sized bigger than the wall that holds it — only that
each geometry-carrying field names the kind of block its own form requires.

## The API

Every endpoint is rooted at `/api` and takes no map. Every `POST` and `PUT` that saves a row answers **400**
`LB4` for a name that is not one and **409** `LB5` for a name its kind already carries (*Refusals*, above). A read is open to anyone, a write needs someone on the
whitelist, and a `DELETE` needs an admin, because a library row is shared by every map that uses it
([`docs/access.md`](../access.md)). The pages grey what the caller may not do, with the reason on hover: *New*
and *Save* for anyone off the whitelist, and *Delete* for anyone but an admin.

| Endpoint | Does |
|---|---|
| `GET /styles[?kind=]` · `GET /styles/{id}` | the style library, newest first, each with its card picture |
| `POST /styles` · `PUT /styles/{id}` | save a pattern — body `{name, kind, params}`, where `params` is the material as a **string**. 400 `LB1` for one laying a single block, 409 `LB3` naming the row already holding the same material |
| `DELETE /styles/{id}` | 409 `{error, message, findings}` when something still binds it — the finding's `subjects` name the themes, houses, roofs and storeys |
| `GET /themes` · `GET /themes/{id}` | the theme library and one theme's bucket bindings |
| `POST /themes` · `PUT /themes/{id}` | compose from blocks and patterns — body `{name, rimEdges, …knobs, buckets[]}`, the knobs plus a binding per bucket, `{bucket, styleId, block, depth, enabled}` with `block` `{id, data, laid}` or a pattern's `styleId`, never both (400 `LB2`) |
| `POST /themes/preview` | what a set of bindings composes to, saving nothing — same body as `POST /themes` |
| `GET /themes/{id}/json` | the painter-ready theme JSON — the form a map snapshots — as `{themeJson: "…"}`, the document itself being the **string** in that field |
| `POST /themes/import` | lift a whole theme JSON in: a block or the library's own pattern per bucket, a new pattern only where the library holds none, plus a theme. Body `{name?, themeJson}` — the **mirror of the `GET` above**, the theme being the *stringified* document in `themeJson` rather than an object, and `name` optional (an unnamed import becomes "Imported theme"). 400, never 500, on bad JSON |
| `DELETE /themes/{id}` | forget a theme; its bindings cascade, its styles stay |
| `GET`·`POST`·`PUT`·`DELETE /roof-styles[/{id}]` · `…/storey-styles` · `…/porch-styles` | the three part libraries; each `POST …/preview` renders a draft on a sample building. `POST`/`PUT …/roof-styles` and `…/storey-styles` answer 400 `{error, message, findings[]}` (`docs/refusals.md`) when the house-style gate refuses the roof (its materials, its `roofSlab`, and the slab against its pitch) or the window; a shed, on a roof or a porch canopy, rides on the 200 as a complaint (`HS14`) — Refusals, above |
| `GET /room-styles` · `GET /room-styles/{id}` | the room library — each row `{id, name, preview, style}`, `style` being the composed shell as the stamper's own JSON, so a caller holding a snapshot can say which row it is by matching the document — and one room style's parts and courses |
| `POST /room-styles` · `PUT /room-styles/{id}` | compose a building from parts, blocks and patterns — body `{name, roofForm, front, …parts, courses[]}`, a course `{part, ordinal, styleId, block, height}` naming a block or a pattern and never both (400 `LB2`), and `front` the wall the doorway is cut through, `front` for the building's own. 400 `{error, message, findings[]}` when the composed shell fails one of the gate's refusals (`HS1`–`HS6`); its complaints, the name's (`HS19`) included, ride on the 200 as `warnings` |
| `GET /room-styles/name-words` | the two lists a room style's name is made from — `{describing[], buildings[]}`: any number of describing words, then one building word last. The lists `HS19` reads a name against |
| `GET /room-styles/doors` | the doors a room may be stamped with |
| `GET /room-styles/block-kinds` | which kind of block each style field takes, and the ids of each kind — `{fields[], kinds[]}`. A field row is `{field, kind, when, means, alsoAt[]}` and a kind row is `{kind, blocks[]}` with each block `{id, data, name, material, hex}`. It is the table `HS1` refuses from, so a block it offers is one the gate accepts and a field's `means` is the sentence the refusal names it with |
| `GET /room-styles/{id}/json` | the stamper's own JSON — what a sketch binds and a building prop snapshots — as `{styleJson: "…"}`, likewise a string to unwrap |
| `POST /room-styles/preview` · `POST /room-styles/preview-snapshot` | the shell a set of courses composes to, or the one a stored `HouseStyle` snapshot builds. The answer is `{plan, section, cutaway, columns}`: three SVG cuts, and **`columns`** — the stamped world's own per-column runs, the same shape `POST /plan/columns` answers for a map, which the browser meshes and draws in 3-D. **The two take different bodies**: `preview` takes the same record as `POST /room-styles`, `preview-snapshot` takes a **bare `HouseStyle`** — the document itself, unwrapped, exactly what `GET /room-styles/{id}/json` hands back once its string is unwrapped. A wrapper posted to it is dropped and previews the defaults |
| `DELETE /room-styles/{id}` | forget a room style; its courses cascade, its styles stay |
| `GET`·`POST`·`PUT`·`DELETE /tree-styles[/{id}]` · `…/boulder-styles` | the two recipe libraries — what a *click* puts down. A tree is `{name, form, species, height, body, cut}`, a cut `{world, x, y, z, at, builder}` with `builder` null where the cutter was not told; `POST`/`PUT /tree-styles` answer 400 `{error: "invalid tree style", findings}` with `DR-COPY` when a `copied` tree carries no `cut`. Each `POST …/preview` draws a draft as the card a browse row carries, answering `{card: "…"}`. Nothing asks before a delete, because nothing binds a recipe: a placement names a key in its **own document's** registry, which the pull copied |
| `GET /tree-styles/{id}/json` · `GET /boulder-styles/{id}/json` | the recipe as a dressing document states it, as `{styleJson: "…"}` — what a pull copies into a map's `styles` registry under a key. A copied tree whose cut names a builder carries it as `builder` |
| `GET /terrain/blocks` · `GET /terrain/patterns` | the block palette — each block's id/data, name, tone family, swatch, and what it **looks like** per face (below) — and every material kind with its fields, defaults and the cell facts it varies with |
| `GET /terrain/looks` | the construction words a face's `construction` is drawn from, each with what it means |
| `GET /terrain/biomes` | every biome 1.8 stores — `{id, name, hex, foliage, water, sharesTintWith[]}` per row: the grass tint choosing it produces, the leaf and water tints, and the biomes that are the same colour on all three (`docs/world-export/terrain-painting.md` §5b) |
| `GET`·`POST`·`PUT`·`DELETE /biome-patterns[/{id}]` | the biome library; each row `{id, name, kind, params, preview}`, the preview being a patch of grass under the field. `POST`/`PUT` answer 400 `malformed biome` when `params` does not read as a field. A delete asks nothing: a map holds a snapshot |
| `POST /biome-patterns/preview` | what a draft field draws, saving nothing — body is a **bare** `BiomeField`, unwrapped, answering `{card}` |
| `POST /terrain/material-preview` | one material drawn in plan and section — body is a **bare material**, `{kind, …}`, unwrapped. One column, not an area: a pattern cannot be judged from it |
| `POST /terrain/theme-preview` · `POST /terrain/theme-map-preview` | a whole theme as it will paint — the first over a sample plateau cut open plus one swatch per themeable bucket, the second over a compiled plan, so a theme is judged against the board it will dress rather than against a sample. Body is a **bare theme**, unwrapped |
| `POST /terrain/prop-preview` | one placed prop standing on the finish it will stand on — body `{propJson, themeJson}`, because what the paint leaves on top is what decides whether flora grows at all |
| `GET /terrain/stroke-styles` · `/terrain/fluid-forms` · `/terrain/boulder-forms` · `/terrain/species` | the dressing vocabularies — every stroke style, fluid form, boulder form and tree species a prop may name, each with the fields it carries. What a picker offers, and the closed sets a prop document is refused against |

**Every preview also draws a picture, and three query words say how to ask for one.** The default is
SVG-in-JSON, which is what the client renders inline; `?format=png` answers **one** view as `image/png` bytes
instead, which is the form an agent saves and looks at.

| Word | Takes |
|---|---|
| `format` | `png`. Absent answers the JSON |
| `view` | which view to draw, out of that route's own closed set — the first is what it draws unasked. A name outside the set is a **400** listing the ones it has. A route with one picture and nothing to choose declares no `view` at all |
| `scale` | 1 to 8, absent is 1. A magnification rather than a redraw: the same view at more pixels, because a house section is 72 × 108 unasked and a roof idiom cannot be read off that. Anything outside the range, or not a number, draws at 1 — a scale is how the answer is looked at rather than part of the question, so a bad one costs a bigger picture and never the picture |
| `part` | which part of the building the pictures are **cut to**, from `RoomParts` — a floor is its plate, a wall is the course players walk on up to the eave, a roof is everything over it and the two courses under it the overhang may fall through. Neighbouring bands therefore share courses, which is the honest picture: the eave is both the top of the wall and what the roof lands on. Absent draws the whole shell, and so does a word outside the set. What an editor sends so the picture follows the row the author has open; the three part libraries cut to their own part without being asked |
| `footprint` | the shell a **house or a part** is drawn on: `6x6`, `8x8`, `10x15` or `16x16`, absent being `8x8`. A style states nothing about the rectangle it is stamped over while a ridge follows that rectangle's own proportions, so the same style on a square and on a long shell is two different roofs. 6×6 is the least a shell may stand on (`WX2`), and the piece each is resolved out of is two blocks larger on each axis. A word outside the set draws the default, for the same reason a bad `scale` does |

The view sets, each stated once in the code and published as the `view` parameter's enum, so what the schema
names and what a refusal lists are the same list: **`material-preview`** and **`prop-preview`** draw
`plan`, `section`; **`room-styles/preview`** and **`preview-snapshot`** draw `section`, `plan` — the cutaway
is SVG only, since it draws a block as its own shape rather than as a filled cell and so has no raster to
encode, and the building itself is not a picture at all; **`theme-preview`** draws `section` plus one swatch
per bucket
(`rim`, `surface`, `wall`, `fill`); and **`GET /map/{slug}/coverage`** draws its one grid.

**The editor is driven by that schema, not by a copy of it.** `GET /terrain/patterns` is what the Styles and
Theme editors read to know which kinds exist, what each is called, what it draws, which cell facts it varies
with, and what fields it takes — and the server reads all of that off the records' own attributes and primary
constructors, so it cannot offer a kind the deserializer would reject. A kind added server-side is therefore
offered here, seeded correctly and walked in the outline without the client being taught about it twice.

What stays this side is what the schema is not: the **label** a nested material is offered under ("Light
square" against "Dark square"), the name a list's entries count under ("Grid line", "Middle"), and what a
fresh field **starts at** — a required field has no default to publish and an empty list teaches an author
nothing, so a voronoi arrives as a grid line, a thin course and a body. Those starters are keyed by field name
rather than by kind, so a new kind reusing `seed` or `stops` starts sensibly without being named at all.

**Worked bodies.** Each block below is posted verbatim by `DocumentedBodyTests`, so an example that stops
being accepted fails a test rather than misleading a reader.

```json POST /api/styles
{"name": "example-field", "kind": "noise", "params": "{\"kind\":\"noise\",\"seed\":4022,\"scale\":3,\"octaves\":1,\"stops\":[{\"kind\":\"solid\",\"id\":1,\"data\":0},{\"kind\":\"solid\",\"id\":4,\"data\":0}]}"}
```

```json POST /api/themes/import
{"themeJson": "{\"rimEdges\":\"drop\"}"}
```

```json POST /api/terrain/material-preview
{"kind": "solid", "id": 1, "data": 0}
```

```json POST /api/room-styles/preview-snapshot
{"roof": {"form": "gable"}}
```

## Driving it without the UI

Composing a theme is three calls and a hand-off: `POST /styles` for each pattern the theme needs, `POST
/themes` filling each bucket with a block or one of them, with the geometry knobs, then `GET /themes/{id}/json` for the painter-ready
form — which is what goes into a sketch's own `themes` registry, keyed under a name, with `mapTheme` or a
shape's `theme` pointing at it. `POST /themes/import` collapses the first two when a theme JSON already exists.

A building is the same shape one level up: `POST /roof-styles`, `/storey-styles` and `/porch-styles` for the
parts, `POST /room-styles` binding them with the shell's own knobs and courses, then `GET
/room-styles/{id}/json` for the stamper's form — which a sketch's Theme phase stores as its `wool` or `spawn`
snapshot, or a placed building carries as its `style`.

Both `/json` endpoints answer a **string in a field** rather than the document — `{themeJson: "…"}` and
`{styleJson: "…"}` — so what a sketch stores is the parse of that string, not the response.

**A map's source names a row by what it is called.** `{"library": "dunes"}` — or `{"library": 12}` by id — stands wherever a refinement states a thing the library holds, and what it names is
decided by where it stands. An entry of `themes` is a theme, of `roomStyles` a room style, a house prop's own
`style` a room style too, of `dressing.styles` the prop style its `kind` says, the `biome` a biome, and anything
else a material. The schema publishes a name
as `StatedName`, offered beside the type it stands for at a refinement's `themes`, `materials` and `biome`.

**The fields stated beside a name are laid over the copy**, an object member by member and anything else
whole, so a theme can be the library's with one bucket changed. A name stated inside those fields is resolved
in its turn and replaces what it stands in:

```json Refinement
{"themes": {"heath": {"library": "dunes", "rimEdges": "boundary", "wall": {"library": "sandstone"}}}}
```

**A name that names no row refuses the source**, `422` with `SR6` naming the nearest names the library holds.
A name is compared without case, as the library keeps it one row of its kind. A theme or a biome copied this way is recorded in the layout's
`themeSources` and `biomeSource` as one copied in the Sketch tool is, and the kept refinement carries each name's
`row` and `hash` (`docs/tools/flow.md`, *A map's source*).

### The seed

**What a fresh library holds is one folder, `src/PgmStudio.Minecraft/Library`, read by one seeder.**
`patterns.json` is every pattern, the author's own ground patterns first; `themes/` holds a finish per file and
`houses/` a house per file, each in its own document's form — the painter's theme, the stamper's style;
`boulders.json` is the boulder recipes and `trees.json` the trees cut out of the showcase world. `SeedFolder`
reads it, and nothing else states a seeded row. Every pattern a seeded house or theme lays is one of
`patterns.json`'s, every block it lays is written into the slot, and the folder's own test holds it to that:
no pattern is one block, no two hold the same material, none differs from another by its seed alone.

**Two seeds are computed rather than stated, and stay code for that reason.** A template tree per species at
its natural height is read off the species table, and a flat biome pattern per biome off the biome table, so
a board that is simply desert needs nothing authored and the select that picks one is never empty. The roofs,
storeys and porches the libraries list are the seeded houses' own, cut out of them and named by `PartNames`.

**`LibrarySeed` runs as the API comes up, and it is matched by what a row holds.** A pattern, roof, storey or
porch row already holding the seeded content is that entry and takes its seeded name; a pattern found by its
name instead takes the seeded content. A house and a theme are matched by name and rewritten from the folder, a
copied tree by its cut, and a template tree or a boulder is put down only where no row has its name, since one
an author has retuned is theirs. Each row keeps its id, which is what everything binding it depends on, nothing
is ever deleted, and a second start changes nothing. A seed that fails is logged rather than fatal — an empty
library is a usable studio and refusing to serve over one would be worse. `LibrarySeedTests` asserts that a
fresh library is the folder: each pattern under its name, each house and theme composing back to its file, each
copied tree to its cut.

**Renaming or retiring a seeded style is a migration's**, since the seed only adds and updates. `M0055` gave
the house styles the names the author's review gave them, each with the parts filed under its name, and took out
the twenty-four the reviews rejected together with the parts nothing else binds; the refinement each map last
stated names a renamed style by its new name, and its change history keeps the name it was written with.
`M0056` makes a gable the database's default porch form, as it is the code's. `M0058` moved every single block
out of the pattern library into the slots that lay it, merged the patterns, roofs, storeys and porches holding the
same thing into one row each, and gave each seeded pattern the name the folder states it under; a map's current
refinement naming a block's row holds the block instead, and one naming a merged pattern names the row kept.
`M0059` gives a room style the wall its doorway faces, which a house style could state and the row had nowhere
to keep. `M0060` made every stored name a library name — a `+` spelled `plus`, any other character a name may not
hold a space — gave each later row sharing a name, compared without case, the first free count after it, put a
unique index over every table's names, and made each map's current refinement follow a renamed row. Nothing stored is rewritten for the
review's rules, which complain rather than refuse: a row keeps what it states, and a map keeps the houses it
was built with, so an old board keeps the houses of its day (author).

`dotnet run tools/seed-library.cs` runs the same seeder against a database of the caller's choosing, and
finishes by composing each seeded room style back out of the library and reporting any field that came back
different — the only honest way to say whether a house survived being stored.

## Limits

The library knows nothing about maps, and that cuts both ways. There is no way to ask which maps use a row: a
map whose source names one says which it resolved to and whether it has moved on, but nothing asks the question
the other way round. And there is no way to push an edit into a map that already snapshotted it. The
snapshot is the guarantee that a library edit cannot rebuild a shipped map, so the missing "re-apply to these
maps" is the price of it rather than a gap. A style filled into another style is the same bargain one level
down: what was filled in is a copy, so editing the source afterwards does not reach it.

**The 3-D view needs WebGL, and there is no fallback drawing.** A browser that cannot give the page a WebGL
context gets a sentence in the box where the building would stand; the plan, section and cutaway are drawn on
the server and are unaffected. A second, server-drawn isometric would be exactly the copy free to disagree
with the app that the live one exists to retire.

A theme and a house's own proportions are still saved as stated — a style, a theme, and every knob that is
not one of the faults above; the previews are the only feedback on those, and they show what would be built
rather than judging it. A room, a roof, a storey and a porch style are checked for the faults the rules name
(Refusals, above) — the kind of block a form needs, a door's clearance, a roof's materials and form, how the
frame, the floors and the walls are put together, and a room style's name — not for whether the composition as
a whole reads well.

**The six shipped themes are a spread rather than a survey.** They are one green, one desert, one ashen, one
snow, one clay and one overgrown stone, written to show what a rim, a wall, a surface and a fill each do to a
plateau — not to cover what a board can be finished in. A sketch's themes still live in the sketch until
someone saves one out to the library, and the library never reaches back into a map that took a copy: bringing
a board's copy up to date is copying the library's in again over the same name.

Windows and rails are chosen as blocks rather than as styles, which means the patterns a style can hold are not
available to them. That is deliberate — their metadata is geometry, not material — but it does mean a window
frame cannot be a voronoi.

All fourteen kinds are reachable in the UI, and the editor takes their shapes from the schema `GET
/api/terrain/patterns` publishes rather than from a list of its own, so a kind added to the painter is
authorable without the client being taught it.
