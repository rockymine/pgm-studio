# The Library tool

## What it is

The library is where a material is authored once and reused. It is the only tool in the studio that knows
nothing about maps: no slug, no stage, no map row anywhere in it. What it holds is recipes, and the tools that
build worlds reach into it to pick one.

Six kinds, in the order the things compose: **Styles** — a style is one material; **Themes** — a terrain
finish made of styles; **Roofs**, **Storeys** and **Porches** — the parts a building binds, each made of
styles; **Houses** — a whole building made of parts and styles. A style is browsed by what it looks like;
everything above it by what it composes to. A house's row is a `room_style` and composes to a `HouseStyle`;
the surface calls it what the thing is.

Three routes, and the rail carries the six kinds. `/library` is the chooser — one card per kind over its own
count and a picture of what it holds. `/library/{kind}` browses that kind: a strip carrying a name search,
whatever else the kind filters by, and **New**, over a grid of cards. `/library/{kind}/{id}`, or
`/library/{kind}/new`, opens one entry on a page of its own.

Two tools consume the library. The Sketch tool's Theme phase pulls a theme in and pushes one back out, and its
Rooms step binds a room style as the shell every wool cage and spawn cube is stamped with; the Dressing phase's
building prop takes a room style as its own. Nothing else reads it.

## What it writes

Eleven tables, one group per level: `style`; `theme` with `theme_bucket`; `roof_style` with
`roof_style_course`, `storey_style` with `storey_style_course`, and `porch_style`; and `room_style` with
`room_style_course` and `room_style_storey`.

**Two directions, and the difference between them is the whole design.** *Inside* the library, everything is
bound **by id**: a theme names the style that fills each of its buckets, a room style names the courses and
parts it is built from. So editing a style reaches every theme and every room style binding it — the editor
says so when it saves — and deleting one is refused rather than allowed to break them.

*Out of* the library, everything is **copied**. A theme applied to a sketch is stored as the painter's own JSON
in that sketch's registry; the room shells bound in the Rooms step are snapshots; a placed building carries its
style rather than a library id. So a library edit can never rebuild a map that already shipped, and there is no
mechanism by which it could — which is the guarantee, not an omission.

## The four levels

### A style is one material

`style` rows carry a name, a kind and `params` — the serialized `TerrainMaterial` the painter itself reads.
There is no second model of a material anywhere: the same editor authors a library style and a theme bucket in
the Sketch tool, and the kind is read back off the JSON node the editor rewrote, so the row's kind and its
params cannot disagree.

A style's card picture travels with the row rather than costing a request per card, because a library is
browsed by what its entries look like. The editor previews two views of one material: a **plan**, one course
seen from above, which is where a voronoi and the three noise fields vary, and a **section**, one row of
columns cut open downward, which is the axis a layer stack varies along and the elevation a wall material is
seen as. A stored style's `params` is exactly one of the nodes below, and a saved row is that node plus a name:

```json
{ "name": "quartz rim", "kind": "solid", "params": "{\"kind\":\"solid\",\"id\":155,\"data\":0}" }
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

```json
{ "kind": "solid", "id": 1, "data": 0 }
```

**`layered` — a band stack read as depth from the top of the bucket.** Grass over two dirt; a wall's banded
riser. Each band states its thickness in courses, and the stack states what it does where they run out: the
bucket is the stack's whole space, so it `repeat`s and a band deeper than declared never falls through to
nothing. The other ending, `handOver`, claims nothing past the last band and leaves whatever is under the
stack showing — which is what a band *inside* a larger space wants, and is why the ending is stated rather
than assumed.

```json
{ "kind": "layered", "stack": { "ending": "repeat", "bands": [
  { "material": { "kind": "solid", "id": 2 }, "thickness": 1 },
  { "material": { "kind": "solid", "id": 3 }, "thickness": 2 } ] } }
```

**`teamTint` — the block tinted by the team that owns the cell**, on the same 0–15 damage scale wool uses, so
clay, wool or stained glass takes the team's colour. A cell with no team — a neutral mid — falls back to
`neutral`. It works on any bucket, not just the wall, and nests inside a stack or a pattern.

```json
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

```json
{ "kind": "voronoi", "seed": 1, "cellSize": 10, "rise": 0, "bands": [
  { "material": { "kind": "solid", "id": 155 }, "depth": 1 },
  { "material": { "kind": "solid", "id": 3 },   "depth": 2 },
  { "material": { "kind": "solid", "id": 1 },   "depth": 1 } ] }
```

**`cell` — the same regions, one flat colour each, with the lookup warped.** Where a voronoi draws a diagram,
this draws a **fabric**: flat patches, any two of which may meet. `jitter` (0–100) is how far a site may sit
from the middle of its grid cell — 0 gives the grid squares, 100 gives shards — and `warp` is how many blocks
the boundary wanders, which is what turns a straight-edged diagram into organic patches.

```json
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

```json
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

```json
{ "kind": "wallRun", "runs": [
  { "material": { "kind": "solid", "id": 155 },           "width": 3 },
  { "material": { "kind": "solid", "id": 159, "data": 8 }, "width": 2 } ] }
```

**`wallDiagonal` — the same stripes sheared by height.** `slope` is how many arc cells the pattern shifts per
course up: 1 is 45° on a square-blocked face, larger lays it flatter, negative leans it the other way, 0 is the
vertical run again. The height is read from the cell's own Y rather than from the foot of the wall, so two
walls of different heights standing side by side meet with their diagonals in line.

```json
{ "kind": "wallDiagonal", "slope": 1, "runs": [
  { "material": { "kind": "solid", "id": 155 },           "width": 2 },
  { "material": { "kind": "solid", "id": 159, "data": 8 }, "width": 2 } ] }
```

**`wallFrame` — an edge material inked around the wall's borders and corners, with a fill inside.** `angle` is
the turn threshold in degrees that counts as a corner, and because the measured turn ramps to a vertex rather
than switching on at it, the same number sets how far the ink wraps round each corner — a low threshold inks a
broad return, a high one only the vertex. `thickness` is the courses taken at the top and bottom, and a wall
too short to hold two of them is all edge.

```json
{ "kind": "wallFrame", "angle": 45, "thickness": 1,
  "edge": { "kind": "solid", "id": 159, "data": 15 },
  "fill": { "kind": "solid", "id": 155 } }
```

**`checker` — two materials on a board of `size`-block squares**, laid in the face the cell belongs to, so a
wall gets squares rather than the vertical stripes a plane pattern would give it.

```json
{ "kind": "checker", "size": 1,
  "even": { "kind": "solid", "id": 155 },
  "odd":  { "kind": "solid", "id": 159, "data": 15 } }
```

**`logChecker` — one log alternating upright and laid**, which is the timbering the corpus houses use. Off a
wall the flat squares read as bark against sawn end, which is what a log floor is.

```json
{ "kind": "logChecker", "size": 1, "id": 162, "data": 0 }
```

**`laidLog` — one log lying along the wall, never across it.** The axis a log is laid on decides which two of
its six faces are the sawn ends, and a log laid across a wall puts one straight out at the viewer; this takes
the axis the wall is going. At a corner, where the wall has faces on both axes, the log stands upright — which
is what a corner post is.

```json
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

```json
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
built-in default, near enough: a quartz rim, grass over two dirt, a team-tinted clay wall, a stone body.

In the library the same theme is a row of bindings rather than a document — each bucket naming a style id, a
depth and a toggle — and `GET /themes/{id}/json` is what assembles the row into the above.

**Unbound is a real answer, and so is switched off.** A bucket with no style (id 0) keeps the built-in finish
and is stored by being left out, which is what makes the library worth having for the case it was built for —
a rim and a fill bound once and reused, with only the surface and the wall differing between themes. A theme
needs neither a rim nor a wall, so the toggle is offered whether or not a style is bound: an unbound bucket
that is **off** keeps its binding, because it says a great deal, and only an unbound bucket that still paints
is dropped on save, because that one says nothing.

The preview is a sample plateau painted and cut open, plus a top-down swatch per bucket.
`GET /themes/{id}/json` assembles the row into the painter's own theme JSON — the form the export consumes and
a map snapshots — and `POST /themes/import` runs the other way, lifting a whole theme JSON into the library as
one style per bucket plus a theme binding them.

### A part is a roof, a storey or a porch

One composer serves all three, because they are the same act — pick a kind, bind styles to that kind's parts,
turn that kind's knobs — and what differs between them is data rather than a third editor.

A **roof** is everything above the eave: its form, pitch and overhang, whether it carries a hole and a ridge
cap, the `roofSlab` a half-course rise steps on every odd course, and a material for each of its `roof`,
`verge` and `gable` parts. It has no thickness: a course stack counts upward from its part's own base, which a
wall has and a roof does not, since a slope's depth at a cell is however many courses close the step down to
its neighbour. The slab is the roof's own rather than the house's, which is what lets the slab/pitch pairing be
checked here as well as on a whole shell — and what makes a house binding a roof take that roof's answer, the
way it takes its form and its pitch.

A **storey** is one room: the `clear` a player stands in — never under three, because a room has to be stood up
in — the floor's border width and inlay inset, its windows, and courses for the `wall` (which does stack) plus
`post`, `ceiling`, `field`, `border` and `inlay`. A house stacks storeys in order, so a shop under two flats is
three bindings of two presets.

A **porch** is the strip of footprint the walls give up and what stands on it: depth, inset, edge, roof and a
rail block. It carries no courses at all — its deck is the house's floor and its canopy the roof's material, so
what is left to it is its shape.

Every part's picture stands it on a plain sample building, so what differs between two cards is the part and
never the house around it.

### A room is a building made of parts and styles

A `room_style` carries the extents and knobs of a whole shell — floor depth, wall height, roof form, pitch,
overhang, border and inlay, door and door head, beams, windows and gable windows, an optional porch — plus three ways of composing: per-part **course stacks**, optional bound **roof and porch style ids**,
and a **storey stack** whose position in the list is the position in the building, ground first, so there is no
ordinal on the wire. Reordering the list reorders the house.

A course names its part, its ordinal (0 being the course nearest that part's own base), the style it resolves
through and how many courses it runs. `post`, `sill` and `verge` take one material rather than a stack — a post
is a post all the way up — so only their first course is read. A part with no courses keeps the built-in
finish, exactly as an unbound theme bucket does, which is what makes a room style that only changes its roof
worth storing.

**A building seated into terrain does not carry a footing** (author). A style's `foundation` is what it stands
on: a `plate` claiming downward from the course players walk on, that plate's `surface` zoning, and a `footing`
ringing it one block proud. The footing is the optional one — **absent** is no footing, so the walls meet the
ground flush rather than standing on a course proud of it. It is a state rather than a block that happens to be
air: naming air was how the choice used to be said, and a style stored that way reads forward as the state it
meant. Leave the `sill` part unbound to reach the same choice from the library.

**Windows and rails are picked as a block, not as a style**, and the reason is worth keeping: their metadata is
*geometry* — which way a stair climbs, which half a slab fills — while a material resolves its own data from
where the cell sits, which would turn every stair in a wall the same way. A window's `hostBlock` names the
block it may be cut into, so a seat chosen by spacing on a banded wall does not land half in one band.

A room style previews in four views — isometric, plan, section and cutaway — and the editor shows them as the
isometric over a row of the three cuts, because the isometric is what a building looks like and the cuts are
how it is made. Any one can be asked for alone, which is the only way to read a cut at the size the stage can
give it; the chips over the picture say which. A library card carries the section alone, since an isometric is
tens of kilobytes for one style and megabytes for a grid of them.

All four are drawn on the shell the `footprint` word names, asked in the dock at the foot of the stage rather
than among the views: which view and at what proportion are two questions, and a row of nine capsules in one
corner reads as one long list of neither. Neither is a field of the style — both change what the picture is
taken over and nothing about what a save would store.

### A seeded house, written out

`desert brick` is one of the presets the seed puts in: end stone and sandstone under a brick roof, with no
frame at all. Where the alpine house is a frame with panels between it, this is a wall — two courses of end
stone under five of sandstone, unbroken by posts, so the corners are wall like everything else. The roof and
its verge are one material, which is what a roof laid in a single thing looks like, and the gable face comes
back down to the end stone the base is in so the two ends of the building answer each other. It is also the
first preset to wear a door head: birch stairs in the two corners of the opening's top course, so the doorway
loses its square top.

This is what `GET /api/room-styles/3/json` answers with, unwrapped from its `styleJson` string — the form the
stamper takes, a sketch's Rooms step stores, and a placed building carries:

```json
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
  "post": null,
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
is why the two end-stone-and-sandstone courses are a list and the brick roof is not.
`post: null` is the absence of a part, not an empty one: this house has no frame, and `footing: null` says the
same about the course a building normally stands proud of the ground on. `storeys` is empty because the shell
is one room rather than a stack, and `porch` is null for the same reason.

## The editor page

Every kind edits in the same three-pane workspace the map tools are laid out in: the document as an
**outline** on the left, the **preview** across the middle, and the fields of whichever outline row is picked
in the **inspector**. The name sits above the outline, because it is the document's rather than any one
part's.

**The outline is the document, not a menu.** Each row carries what its piece states without being opened — a
part is *bound* or keeps the *built-in* finish, a stack says how many courses it runs, a theme bucket names
the style it resolves through or says it is *off*. A material's outline is its own nest: a voronoi's bands, a
stack's layers and a field's stops are each a row, indented by how deep they sit, so a five-entry pattern is
five rows rather than five boxes inside one another. The inspector then draws that node alone — its kind, its
scalars, and its own entries as rows the outline is already carrying.

**What the outline shows, the preview answers.** A style draws its plan and its section, either or both by a
chip; a house and a part draw the sample building four ways. The picture takes the width of the window and
never scrolls with the knobs that change it.

The draft **is the save request** — for a style, the material's own JSON node; for the rest, the request value
itself — so the preview re-renders from the same value the save would post, and a picture cannot promise
something the save would not build. Saving a style says which way it reaches: adding one is "Added to the
library", editing one is "Saved. Every theme binding it now paints this." A save that creates a row lands on
that row's own route, so the URL always names what is open.

## Refusals

**A bound row cannot be forgotten.** `DELETE /styles/{id}` answers **409** naming the themes and room styles
still binding it, so the refusal says what would break rather than surfacing a foreign-key error. It is the
same refusal envelope every other gate answers in — `{error, message, findings}` with the names in the
finding's `subjects` — so a caller reads one shape whatever it asked to forget. The three part kinds answer
identically: a roof, storey or porch a house still wears is refused.

**A composition can be.** Deleting a theme or a room style is unguarded — a theme's bucket bindings and a room
style's courses cascade, and the styles they bound stay. That asymmetry is deliberate: the things something
else depends on are protected, and the things nothing depends on are the author's to discard.

**A house style that names the wrong kind of block is refused where it is saved.** `PgmStudio.Minecraft`'s
`HouseStyleValidation.Check` runs on every `POST`/`PUT` to `/room-styles` (over the composed shell) and the
two `/storey-styles` verbs (over the storey's own window); the two `/roof-styles` verbs run
`HouseStyleValidation.CheckRoof` over the composed roof, which is the whole roof gate rather than half of it —
a roof part states its own `roofSlab`, so the slab/pitch pairing has both numbers there. The same checks run
wherever else a `HouseStyle` snapshot enters the studio: a stored sketch's bound `roomStyles.cage` and
`roomStyles.spawn` and the shell of every building in its `dressing` (`docs/tools/sketch.md`'s Refusals) — the
wool cage, the spawn and a placed house checked identically, since none of the three asks for a different
rule, and there against the build ceiling as well (`WX10`, `docs/world-export/structures.md`). All three roads
to a stored layout ask it: the plain `PUT …/sketch`, `PUT …/sketch/from-plan`, and `POST /map/from-documents`. Every style finding names one of three stable
rule ids (`PgmStudio.Minecraft.HouseStyleRules`), so a caller can act on `rule` rather than parsing
`message`:

- **`HS1` — a block named for a role that is not that kind of block.** `beams.block` must be a **log** — a
  beam is the end of a floor timber and docks against the posts, which is what a log is for and the only thing
  it is a house material for. `doorHead.block` must be a stair; its `fillBlock` under `upperSlab`
  must be a single slab; a `windows.block` under `stairLattice` or `arched` must be a stair, and under
  `slabBanded` a single slab; `roofSlab` itself must be a single slab when it names one at all — a **double**
  slab (43/125/181) does not count, since it ignores the half a window or a door head writes into its data and
  is a full cube regardless. Getting it wrong used to build silently — a solid lintel instead of an arch, a
  pane/air/pane stripe instead of a band — and now answers **400**
  `{error: "invalid house style", findings: [{rule, field, message}]}`, one finding per fault, naming the field
  and the block that was wrong. Nothing is substituted for the author. The forms themselves are never refused:
  a `stairLattice` window with a real stair and a `slabBanded` window with a real slab are both allowed on any
  house, a spawn included — `HousePresets.Alpine` and `Workshop` build them correctly, and the author has
  confirmed the forms are not the fault (`B161`'s finding was the block, not the pattern).
- **`HS2` — a door too short to walk through.** A door head takes the doorway's top course, so a three-course
  door clears two full courses plus, if the fill is genuinely an upper slab, half of a third — 2.5 at the least
  a door may clear (author). A style whose fill only *claims* to be a slab, or is a solid beam by design, clears
  a flat 2.0 and is refused the same way.
- **`HS3` — a roof's own materials.** A roof is **one material and its verge is one material**: a pattern in
  either is refused, since a roof is read as one plane and a voronoi across it is several blocks in one
  surface, and `roofSlab` is the body's own material, since the slab is the body continuing by halves. The two
  may be the same block — a brick body with a brick verge is a whole brick roof — or they may differ, which is
  how a dark oak verge trims one. A slab named as the whole-block `roof` while `roofSlab` is unset builds a
  see-through roof at a whole block of rise; a log or a ground material named as `roof` or `verge` is refused
  outright, whichever role it is asked to fill. The **gable** is the end wall carried up and follows the wall,
  so it is not held to this.

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
  the `Stilts` preset passes and the same house with a head does not.

- **`HS7` — a footing round a plate one course deep.** A **complaint**, not a refusal: the building stands
  either way and what the rim costs is how it reads. A footing is what a foundation stands on, so over a plate
  of a single course it is a one-block rim round a building with no foundation under it. Either drop it — no
  footing is the default — or give the plate the two or three courses that earn one.

Beyond that the library barely refuses. A save needs a name. A storey's clear floors at three. An unbound
bucket that still paints is dropped rather than rejected. Nothing yet validates a *composition* as a whole — a
roof and a storey that would look wrong stacked, a window sized bigger than the wall that holds it — only that
each geometry-carrying field names the kind of block its own form requires.

## The API

Every endpoint is anonymous, rooted at `/api`, and takes no map.

| Endpoint | Does |
|---|---|
| `GET /styles[?kind=]` · `GET /styles/{id}` | the style library, newest first, each with its card picture |
| `POST /styles` · `PUT /styles/{id}` | save a material recipe — body `{name, kind, params}`, where `params` is the material as a **string** |
| `DELETE /styles/{id}` | 409 `{error, message, findings}` when something still binds it — the finding's `subjects` name the themes and room styles |
| `GET /themes` · `GET /themes/{id}` | the theme library and one theme's bucket bindings |
| `POST /themes` · `PUT /themes/{id}` | compose from existing styles — body `{name, rimEdges, …knobs, buckets[]}`, the knobs plus bucket→style bindings |
| `POST /themes/preview` | what a set of bindings composes to, saving nothing — same body as `POST /themes` |
| `GET /themes/{id}/json` | the painter-ready theme JSON — the form a map snapshots — as `{themeJson: "…"}`, the document itself being the **string** in that field |
| `POST /themes/import` | lift a whole theme JSON in: one style per bucket plus a theme. Body `{name?, themeJson}` — the **mirror of the `GET` above**, the theme being the *stringified* document in `themeJson` rather than an object, and `name` optional (an unnamed import becomes "Imported theme"). 400, never 500, on bad JSON |
| `DELETE /themes/{id}` | forget a theme; its bindings cascade, its styles stay |
| `GET`·`POST`·`PUT`·`DELETE /roof-styles[/{id}]` · `…/storey-styles` · `…/porch-styles` | the three part libraries; each `POST …/preview` renders a draft on a sample building. `POST`/`PUT …/roof-styles` and `…/storey-styles` answer 400 `{error, message, findings[]}` (`docs/refusals.md`) when the house-style gate refuses the roof (its materials, its `roofSlab`, and the slab against its pitch) or the window (Refusals, above); porches carry nothing the gate checks |
| `GET /room-styles` · `GET /room-styles/{id}` | the room library and one room style's parts and courses |
| `POST /room-styles` · `PUT /room-styles/{id}` | compose a building from parts and styles — body `{name, roofForm, …parts, courses[]}`. 400 `{error, message, findings[]}` when the composed shell fails the house-style gate |
| `GET /room-styles/doors` | the doors a room may be stamped with |
| `GET /room-styles/{id}/json` | the stamper's own JSON — what a sketch binds and a building prop snapshots — as `{styleJson: "…"}`, likewise a string to unwrap |
| `POST /room-styles/preview` · `POST /room-styles/preview-snapshot` | the shell a set of courses composes to, or the one a stored `HouseStyle` snapshot builds. **The two take different bodies**: `preview` takes the same record as `POST /room-styles`, `preview-snapshot` takes a **bare `HouseStyle`** — the document itself, unwrapped, exactly what `GET /room-styles/{id}/json` hands back once its string is unwrapped. A wrapper posted to it is dropped and previews the defaults |
| `DELETE /room-styles/{id}` | forget a room style; its courses cascade, its styles stay |
| `GET /terrain/blocks` · `GET /terrain/patterns` | the block palette, and every material kind with its fields, defaults and the cell facts it varies with |
| `POST /terrain/material-preview` | one material drawn in plan and section — body is a **bare material**, `{kind, …}`, unwrapped. One column, not an area: a pattern cannot be judged from it |
| `POST /terrain/theme-preview` · `POST /terrain/theme-map-preview` | a whole theme as it will paint — the first over a sample plateau cut open plus one swatch per themeable bucket, the second over a compiled plan, so a theme is judged against the board it will dress rather than against a sample. Body is a **bare theme**, unwrapped |
| `POST /terrain/prop-preview` | one placed prop standing on the finish it will stand on — body `{propJson, themeJson}`, because what the paint leaves on top is what decides whether flora grows at all |
| `GET /terrain/path-styles` · `/terrain/water-forms` · `/terrain/boulder-forms` · `/terrain/species` · `/terrain/woods` | the dressing vocabularies — every path style, water form, boulder form, tree species and wood a prop may name, each with the fields it carries. What a picker offers, and the closed sets a prop document is refused against |

**Every preview also draws a picture, and three query words say how to ask for one.** The default is
SVG-in-JSON, which is what the client renders inline; `?format=png` answers **one** view as `image/png` bytes
instead, which is the form an agent saves and looks at.

| Word | Takes |
|---|---|
| `format` | `png`. Absent answers the JSON |
| `view` | which view to draw, out of that route's own closed set — the first is what it draws unasked. A name outside the set is a **400** listing the ones it has. A route with one picture and nothing to choose declares no `view` at all |
| `scale` | 1 to 8, absent is 1. A magnification rather than a redraw: the same view at more pixels, because a house section is 72 × 108 unasked and a roof idiom cannot be read off that. Anything outside the range, or not a number, draws at 1 — a scale is how the answer is looked at rather than part of the question, so a bad one costs a bigger picture and never the picture |
| `footprint` | the shell a **house or a part** is drawn on: `6x6`, `8x8`, `10x15` or `16x16`, absent being `8x8`. A style states nothing about the rectangle it is stamped over while a ridge follows that rectangle's own proportions, so the same style on a square and on a long shell is two different roofs. 6×6 is the least a room may be (`WX2`), and the piece each is resolved out of is two blocks larger on each axis. A word outside the set draws the default, for the same reason a bad `scale` does |

The view sets, each stated once in the code and published as the `view` parameter's enum, so what the schema
names and what a refusal lists are the same list: **`material-preview`** and **`prop-preview`** draw
`plan`, `section`; **`room-styles/preview`** and **`preview-snapshot`** draw `section`, `plan` — the
isometric and the cutaway are SVG only, since they draw a block as its own shape rather than as a filled cell
and so have no raster to encode; **`theme-preview`** draws `section` plus one swatch per bucket
(`rim`, `surface`, `wall`, `fill`); and **`GET /map/{slug}/coverage`** draws its one grid.

**Worked bodies.** Each block below is posted verbatim by `DocumentedBodyTests`, so an example that stops
being accepted fails a test rather than misleading a reader.

```json POST /api/styles
{"name": "example-solid", "kind": "solid", "params": "{\"kind\":\"solid\",\"id\":1,\"data\":0}"}
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

Composing a theme is three calls and a hand-off: `POST /styles` for each material the theme needs, `POST
/themes` binding them to buckets with the geometry knobs, then `GET /themes/{id}/json` for the painter-ready
form — which is what goes into a sketch's own `themes` registry, keyed under a name, with `mapTheme` or a
shape's `theme` pointing at it. `POST /themes/import` collapses the first two when a theme JSON already exists.

A building is the same shape one level up: `POST /roof-styles`, `/storey-styles` and `/porch-styles` for the
parts, `POST /room-styles` binding them with the shell's own knobs and courses, then `GET
/room-styles/{id}/json` for the stamper's form — which a sketch's Rooms step stores as its `cage` or `spawn`
snapshot, or a placed building carries as its `style`.

Both `/json` endpoints answer a **string in a field** rather than the document — `{themeJson: "…"}` and
`{styleJson: "…"}` — so what a sketch stores is the parse of that string, not the response.

**The built-in presets are put in at startup, not by a migration.** `LibrarySeed` runs as the API comes up and
writes five of the six libraries: the materials the house presets are made of, the storeys, roofs and porches
they are built from, the houses that bind those, and six terrain finishes — `meadow`, `dunes`, `ashfall`,
`firnline`, `claybed`, `oldstone` — decomposed out of `ThemePresets` into one style per bucket plus a theme
binding them. It is idempotent and keyed by name: a row already there is updated in place and keeps the id
that maps and themes depend on, and nothing is ever deleted, so a preset retired from the code stays as a row
the author now owns. A studio nobody has seeded is not a state the app can be in, and a seed that fails is
logged rather than fatal — an empty library is a usable studio and refusing to serve over one would be worse.

`dotnet run tools/seed-library.cs` runs the same seeder against a database of the caller's choosing, and
finishes by composing each seeded room style back out of the library and reporting any field that came back
different — the only honest way to say whether a preset survived being stored.

## Limits

The library knows nothing about maps, and that cuts both ways. There is no way to ask which maps use a row,
because no map references one; and there is no way to push an edit into a map that already snapshotted it. The
snapshot is the guarantee that a library edit cannot rebuild a shipped map, so the missing "re-apply to these
maps" is the price of it rather than a gap.

A theme and a house's own proportions are still saved as stated — a style, a theme, a bare porch, and every
knob that is not one of the three shapes of fault above; the previews are the only feedback on those, and they
show what would be built rather than judging it. A room, a roof and a storey style are checked for the one
thing (Refusals, above): whether each block a form needs a particular kind of is that kind, whether a door
clears the least height one may, and whether a roof's own materials fit its pitch and its family — not whether
the composition as a whole reads well.

**The six shipped themes are a spread rather than a survey.** They are one green, one desert, one ashen, one
snow, one clay and one overgrown stone, written to show what a rim, a wall, a surface and a fill each do to a
plateau — not to cover what a board can be finished in. A sketch's themes still live in the sketch until
someone pushes one out to the library, and the library never reaches back into a map that took a copy.

Windows and rails are chosen as blocks rather than as styles, which means the patterns a style can hold are not
available to them. That is deliberate — their metadata is geometry, not material — but it does mean a window
frame cannot be a voronoi.

**The editor offers a kind it cannot build.** `laidLog` is in the dropdown as "Laid log beam", but the client
has no shape for it: choosing it replaces the material wholesale with a plain stone `solid`, silently. The
painter, the wire format and every preview handle a laid log correctly — `{"kind": "laidLog", "id": 17}`
renders — so it is authorable by hand and by an agent, and only the picker is missing. Thirteen of the fourteen
kinds are reachable in the UI.
