# The Sketch tool

## What it is

Sketch authors the ground itself: the outline of every landmass, the height of every part of it, what it is
made of, and what stands on it. It is the largest of the studio's tools and the only one that draws real
geometry — a plan states rectangles on a coarse grid, and everything finer than that happens here.

It is also one of the two tools a map can be started in. Opened empty it is a drawing surface and nothing
more: a sketch alone produces geometry and states no intent, so a map begun here has no teams, no spawns and
no objective until Configure gives it one. Opened on a map that came from a plan, it receives the compiled
layout — the plan's abutting same-height pieces already fused into single polygons — and refines it.

The route is `/maps/{slug}/sketch`. Five phases sit on the rail in the order the work is done: **Info**,
**Draw**, **Relief**, **Theme** and **Dressing**. Info states what the board is and is its own body; the
other four share the one live canvas, which stays mounted while Info is up so the drawing state and the zoom
survive the trip. None of the four has steps: each swaps what the columns hold and which overlays the layer
bar offers, and the canvas is reused as it stands.

The tool saves continuously — every change schedules a debounced write 800 ms later — and leaves by
**Finish**, which flushes the layout, rasterizes it server-side into world geometry, and moves the map to
`stage=configure`. A draft that was never drawn on is discarded on the way out.

**What the later phases state, `docs/world-export/` executes**, and that folder is where the depth is. This
document is the tool: what each phase authors, what it writes, and what refuses. Beside it sit five that each
take one of those statements through to the blocks it becomes — `relief.md` (the elevation solver behind the
Relief phase, with the measured terrain law), `terrain-painting.md` (what the painter makes of a theme, cell by
cell), `structures.md` (the shells the Theme phase binds and the house the Dressing phase stamps),
`decoration.md` (the dressing pass itself) and `tree-corpus.md` (the hand-built ground truth a grown tree is
scored against). `docs/world-export/sketch-world-export.md` is the world folder Finish writes into. Each is cited
below from the phase that feeds it.

## What it writes

One artifact: the `sketch_layout_json` blob on the map row, read and written through `MapArtifactStore`
like every other artifact. It is stored **verbatim** as the browser produced it —
authoring source rather than a canonical document — which is why the editor's own save replaces the whole blob
and a deletion therefore sticks. Only the plan-compile path merges (below).

The identity of the map — its display name and authors — lives on the map row and is saved through
`PATCH /api/map/{slug}/metadata`, not in the layout.

### The layout document

`SketchLayout` (`src/PgmStudio.Pgm/Sketch/SketchLayout.cs`) is the typed model of the blob, and it is the same
shape whether a layout was hand-drawn or compiled from a plan.

| Key | Holds |
|---|---|
| `setup` | `mirror_mode`, the symmetry `center`, and the `bbox` the canvas frames on open |
| `layers[]` | the stacked slabs — each `{id, name, base_y, layout:{shapes, groups}}`, plus `kind`, `part_of` and `seat` where the layer holds a made thing. Always at least one; a flat board is a stack of one, called `ground` |
| `themes` · `mapTheme` | the terrain-paint registry and the map-wide default |
| `roomStyles` | the two bound room shells — `cage` (wool) and `spawn` |
| `dressing` | every placed prop |
| `relief` | interior elevation, keyed by group id — and `landform`, the word the group states about what kind of ground it is meant to be |

Four of those are the map's **finish** rather than its shape — `themes`, `mapTheme`, `roomStyles`,
`dressing` — and that grouping is load-bearing: a plan cannot express any of them, so when a plan is recompiled
onto a map the compiled layout's geometry replaces what was there while the finish is carried across
(`CarryFinish`). Relief is *not* in that set, because a relief is geometry: it decides what the rasterizer
emits, and it is carried by its own rule with a refusal attached (below).

The geometry half, with one shape of each flavour — a plain rectangle, a polygon carrying per-vertex heights
and a Bézier edge, a carve, and an erected shape standing out of the relief:

```json
{
  "setup": { "mirror_mode": "rot_180", "center": { "cx": 0, "cz": 0 },
             "bbox": { "min_x": -60, "max_x": 60, "min_z": -40, "max_z": 40 } },
  "layers": [
    { "id": "ground", "name": "Ground", "base_y": 0, "layout": {
      "shapes": [
        { "id": "s0", "type": "rectangle", "operation": "add",
          "min_x": -40, "min_z": -20, "max_x": 0, "max_z": 20, "floor": 0, "base_height": 9 },
        { "id": "s1", "type": "polygon", "operation": "add",
          "vertices": [[0, -20], [30, -20], [30, 20], [0, 20]],
          "anchor_heights": [9, 13, 13, 9], "floor": 0, "base_height": 9,
          "controls": { "1": { "out": [38, -12], "in": [38, -18] } } },
        { "id": "s2", "type": "rectangle", "operation": "subtract",
          "min_x": -14, "min_z": -6, "max_x": -6, "max_z": 6, "floor": 0, "base_height": 100 },
        { "id": "s3", "type": "rectangle", "operation": "add", "override": true,
          "min_x": -12, "min_z": -2, "max_x": -8, "max_z": 2, "floor": 0, "base_height": 9,
          "height_mode": "raise", "skirt": 2, "relief_scope": "exclude" }
      ],
      "groups": [ { "id": "i1", "name": "Team island", "mirrors": true,
                     "shapeIds": ["s0", "s1", "s2", "s3"] } ] } }
  ]
}
```

Read against the fields above: `s2`'s `base_height` of 100 is the statement of intent a subtract's height
always is — it carves the whole column whatever the number — and `s3` sits *inside* the hole `s2` cut, which
is what `override` is for, since an override-add is laid after the ordinary subtracts. `controls` is keyed by
the index of the vertex whose edges the handles bend. The whole document adds the finish keys and `relief`
beside `layers`, both shown below.

### Shapes

A shape is one entry in a layer's `shapes` array. Its geometry is a rectangle (`min_x`/`min_z`/`max_x`/`max_z`),
a circle (`center_x`/`center_z`/`radius`), or a vertex list (`vertices`, with optional Bézier `controls` per
edge). Every shape carries an `operation` — `add` builds ground, `subtract` removes it — and an `override`
flag that decides the order the set algebra resolves in: the ordinary pass is adds minus subtracts, then
override-adds overwrite whatever column they land on, then override-subtracts remove theirs last.

**`keepClear` says the shape is not ground to dress.** A shape drawn to *be* something — a town wall, a crop
bed, a well's rim, a flight of stairs — is terrain by construction: nothing about its material, its layer or
its provenance separates it from the ground beside it, so a road repaints its top course and a channel cuts it
down to the water line. Marking it puts its columns in the dressing pass's keep-out (`KeepOut.Structure`), and
a prop that lands there is declined as `DR-KEEP` naming the cell. The mark is **exact — no margin** — because
the wall a road runs through a gate of has to keep its own columns and not a verge either side of them; it is
the marked shape's own footprint rather than what survives the layer's set algebra, and it travels through the
symmetry fan with its group, so a marked shape on a mirrored group keeps its images clear too.

**A shape says what paints it at one of two grains, and never both.** `theme` names the layout's registry
and is ground: five buckets whose choice is made per column by whether that column is an edge — a rim on the
lip, a wall down the riser, a surface over the middle. `material` states one `TerrainMaterial` and is what
the shape is **made of**: painted over its whole span, no rim, no wall, no surface depth. Which to reach for
follows from the shape rather than from taste. Ground with a middle to it takes a theme; a thing with no
middle — a two-block stilt, a one-block kerb, a stair tread, a rail — takes a material, because every one of
its columns is an edge and a theme scoped to it can only ever paint its rim over its wall (`SK23`). A shape
stating both is refused (`SK24`). `docs/world-export/terrain-painting.md` §3.2 carries the mechanism.

**`material` is about paint and says nothing about walking.** The word that takes a thing out of the walks is
`kind: "made"`, and it is a **layer's**, not a shape's: it takes the whole layer out of the ground everything
rests on, out of the reachability walk and out of the stacking rules. That is right for a train, a statue or a
balloon and wrong for a stair, which is the way onto a storey and has to stay ground a player is measured
over. The two are independent — a stair tread states a material and no kind, a train's slice states both.

**A subtract is a hole, not a dip.** It takes the whole column out at every cell its outline covers, so its
own height is not read — a one-block-tall subtract carves exactly as deep as a hundred-block one. That is the
difference from relief, and it is the whole of it: relief moves a surface, a subtract removes it.

**A subtract with a lid over it is a roofed void, and it is the shortest way to state underground space.**
A subtract has no `y` at all — it is a set of `(x, z)` cells, which is why its own height is not read — so it
cannot hollow a column out from the middle. What it can do is state that a column is empty and let something
else stand over it: an **override add above the subtract's own floor** takes the layer's one span with it and
records nothing beneath, so the void the subtract stated is still void, with a deck on top. Measured on a
landmass built `[0..20)` with a subtract through it and an override add stated at `floor 14`: the column comes
back holding `[14..17)` and nothing else, and the board raises **no finding at all**. The same lid stated at
`floor 0` — at the subtract's own floor — is the fill `SK13` refuses.

That is the whole rule, and the two halves of it are one sentence: an override add **above** the subtract's
floor is a lid and is silent; **at or below** it is a fill and is refused. So a chamber under a hill can be
stated as the ground it is not rather than built as walls and a ceiling around a gap — the negative space
first, then what roofs it.

**A hollow ring is one polygon and not two shapes.** A subtract is refused wherever an add sits over or
under it (`SK13`, below), so a ring wall cannot be a disc with a smaller disc taken out of it on any layer
that also carries a floor or a roof. What it is instead is one polygon that traces its outer ring, slits
inward along a radius, traces its inner ring the other way round and returns along the slit: the fill rule is
**even-odd**, so the doubly-wound interior is outside the shape and the annulus stands. Nothing special reads
it — it is a vertex list like any other, draggable in Draw, and the same construction makes an elliptical
wall, a drum, a crenellated parapet and every hollow tower in `pgm-studio-mapgen/sculpture/forms`.

**An override add lays a floor inside a wall whatever their heights are.** A layer keeps one span per column
and the taller add wins it floor and all, so a low deck drawn inside a tall ring on one layer is simply not in
the world. Marked `override` it is laid after the ordinary pass and overwrites the columns it covers outright,
which is what puts a floor inside a tower, a lid on a shaft and a walkway across a court without a second
layer.

Height is two numbers. `floor` is where the shape's base sits and `base_height` is its thickness, so the
column spans `[floor, floor + base_height]`. A polygon or lasso whose `anchor_heights` line up with its
vertices varies that thickness per vertex, interpolated across the footprint as a TIN. A shape is never
thinner than one block and never floors below zero; a freshly drawn one starts at height 9.

**`anchor_heights` is read by the three kinds that state points, and each reads it in its own frame.** A
rectangle and a circle state bounds and have no points to align to, so a height per vertex on one is `SK22`.
A **polygon** and a **lasso** enclose their own footprint, so the heights interpolate over a TIN of it. A
**polyline** encloses nothing — its points are a centreline — so every cell of the band around it is somewhere
*along* that line, and the heights interpolate over the **arc** between the two drawn points bracketing it.
That is what makes a causeway the ramp it is: `"vertices": [[-60,0],[0,0],[60,0]]` with
`"anchor_heights": [4, 20, 4]` builds a bank rising 16 blocks to its middle and falling back, symmetrically.
Before the first drawn point and past the last it is that end's own value, a band being cut square at its
ends. An array that is not the length of its own point list is `SK22` too, for the same reason under either
frame: the reading is one height to one point, so a mismatch cannot be built and the shape falls back to its
`base_height`.

**That is what makes a tilted quad a stair.** The surface is sampled at each cell's **centre** and the
thickness is `Math.Round` of what it reads, ties away from zero — so a quad rising one course a cell builds a
stair of single courses, and a shallower one climbs a course at a time too. `anchor_heights [8, 20]` over
twelve cells builds `8 9 10 11 12 13 14 15 16 17 18 18`; a 9-wide shape stated 1 on one side and 9 on the
other builds nine treads of one. A flight is one shape at any gradient.

**Ties away from zero rather than to even, and gradient 1 is why.** A quad rising exactly one course a cell
reads every cell centre on a half — the default round-half-to-even would send those alternately down and up
and build `7 9 9 11 11 13`, a flight with a two-block rise in every other tread that no player walks. It is
the only whole ramp that ties on every cell, measured over every rise from 3 to 16 cells of run, and it is
the one an author states to mean forty-five degrees. The relief's held-shape read has always rounded this way
(`StatedTop`) and says so; the two must not differ by a course over one sample.

**What a voxel column cannot do is a step smaller than one block.** The gradient decides how often a
one-block step falls, never how big it is: a 1:2 ramp is the same sequence of one-block steps as a 1:1 stair
with a flat cell between each pair. `slopes` reads a one-block rise as `. walked` and a two-block rise as
`: scramble`, so the whole design space of a climb is where its steps fall.

**And a flight has to arrive somewhere.** Nothing in the document says a shape is a flight, so nothing used to
say where one ends: a quad that tops out level with the ground beside it for a single cell and then falls
twenty-four is walkable at every tread and is not a way up anything. `SK26` reads the tilt and walks off both
ends in the shape's own direction — a stroke's by its centreline's tangent, a polygon's by the steepest climb
into each lip tread — and complains where the first cell off is not within one course. Give the end a landing:
a few cells of ground at the last tread's own height, **in front of** the flight rather than beside it, since
ground reachable only by turning at the lip is the plateau the flight was cut into.

Four further fields matter once a group carries a relief. `height_mode` — `level`, `raise` or `sink` — makes
a shape stand out of the solved field rather than be part of it: a mesa cut flat at an absolute height, a
plinth held a fixed amount above whatever ground it sits on, a quarry the same downward. `skirt` is how far in
from its own outline an erected shape eases back into the ground it meets, in blocks; zero is a sheer face,
which is right for a built thing and wrong for a landform. `relief_scope` is `hold` or `exclude` and decides
whether the shape's ground takes part in its group's relief at all (see *Groups and layers*); absent means
it is simply part of the group's ground. And a polyline carries `stroke_edge`
(`solid`, `rough`, `tapered`) with a `stroke_seed`, since a polyline is stored as the open centreline it was
drawn as and the band around it is derived.

**A polyline is the layout's other curve, and it is the one that does not have to be authored.** The drawn
points are run through a centripetal Catmull-Rom spline at eight samples a segment *before* the band is offset
to either side, so four clicked points become a twenty-five-point smooth centreline and the band around it
reads as a flowing wall rather than a chain of chords. Nothing has to be stated for that: it is what the
rasterizer does with every polyline. `stroke_edge` then decides what the band's two long edges do along it —
`solid` holds one width the whole way, `rough` lets the width wander up to 45% either side (the two sides
reading noise rows far apart, so the band does not merely breathe), `tapered` runs fat in the middle and thin
at the ends. The ends are cut square, because a stroke in a map arrives somewhere. `opus5-millrace`'s canal
walls are the worked example: `wall-s` is four points, `radius 1`, `stroke_edge: solid`, and it draws as a
curve.

**A band that laps itself loses the lap, and `SK25` says so.** The two offset edges are one ring and a ring is
filled even-odd, so a centreline that winds back beside itself — a spiral, a hairpin tighter than the band is
wide — cancels wherever the two windings cross, and the overlap comes back as void rather than as ground.
What a coil is instead is one polyline per part-turn: two shapes contesting a column is the ordinary case, the
taller add wins it, and the coil is continuous. `pgm-studio-mapgen`'s `geometry.spiral_arcs` is the worked
example, and `maps/geometry-showcase` carries the spiral ramp it builds.

The other curve is `controls` on a polygon or lasso ring — per-vertex Bézier handles, absolute coordinates,
which is what a bend fits and what the *Reshaping ground the plan compiled* section below writes. The two are
not alternatives: a ring is a closed outline of ground and takes handles, a polyline is an open line and takes
a spline it did not ask for.

**A `polyline` shape and a `stroke` prop are not the same thing, and only one of them is terrain.** The shape
adds columns — `floor` to `floor + base_height`, the ground a player stands on. The prop repaints the top
course of whatever it crosses and adds no cell at all. They share one leaf, `Centerline`, so both flow the
same way; everything else differs, including the band words: a shape's `stroke_edge` is one of three
(`solid`, `rough`, `tapered`) because an outline can express nothing else, while a prop's `style` is one of
five (`worn` and `stones` besides), because a fill can leave gaps and an outline cannot.

A shape tagged with a `role` is not terrain at all. It is something the plan placed, projected in so it stays
visible instead of dissolving into the fused group, and loaded as a locked render-only overlay: never
hit-tested, never edited, skipped by the rasterizer, and merged back into the saved document unchanged.

**A room projects as two shapes, because it is two rectangles.** `spawn` and `woolRoom` are the *region* — the
ground the room stands on and the protection around it — and carry the `intentRef` back to the entity they
belong to, the `color` their labelled box is filled with, and the height the group's relief is held against.
`building` is the *footprint* raised inside one of them, the rectangle the shell is stamped on
(`docs/world-export/structures.md` WX1), drawn in the **building ink** — the same one a house dressed onto the
ground takes, because a room's footprint is the single-wing case of that building — as an outline over the box
rather than a second filled one. It carries no `intentRef` and no height of its own: the region shape is what a group's relief is held
against and what an author corrects a height on, so a second shape claiming that identity would be a second
answer to one question. Its id is the region's plus `-building`, so a recompile writes the same shape over the
same one. **Only a stated footprint projects** — a placement that leaves it to `WX1`'s default shows its
region alone, since drawing a rectangle nobody stated as though they had is the opacity a stated footprint
exists against.

**Its footprint stays inside the fused polygon, and has to.** A room places no terrain of its own, so the
ground it stands on is the group's; that overlap is what binds the room to its group's relief, since an
annotation is never listed in a group's own `shapeIds` (`docs/world-export/relief.md` §11). Selecting the
polygon in front of a room therefore selects one that reaches under the room as well — which is the shape of
the thing, not a fault in it. What keeps the room level is the pin, and nothing after the pin may tilt it
(§2.1).

### Groups and layers

A **group** is not authored; it is computed. Every time the shapes change, the tool unions them and reports
the connected pieces that result, so two rectangles pushed together become one group and pulling them
apart splits it again. What an author owns is the group's `name` and its `mirrors` flag — whether the group is
copied onto its symmetry orbit — and those survive a recompute by matching the group back to its previous
self. A single-member group shows the shape inspector rather than the group one, so a lone rectangle needs
no drilling.

The group matters beyond naming: **it is the unit a relief is stated against**, because a relief solved per
shape would leave a seam wherever two shapes met and disagreed about the height they share. One group holds
one relief, keyed by its id, and there is no way to give two parts of a landmass a relief each — two shapes
that touch are one group and share whatever it states.

**A shape can leave that solve, though, and this is where the fusion stops being a cage.** `relief_scope` on a
shape says how its ground takes part: `exclude` removes its cells from the field entirely, so the shape keeps
its own column — its stated floor, its thickness and any per-vertex tilt — while the relaxation treats the
footprint as a hole and bends around it exactly as it bends around the void; `hold` leaves the cells in the
field but pins them at one level, read at the shape's ring centre, and the surrounding land is then solved
knowing where it has to arrive. Held shapes are applied last, so one wins its cells outright rather than being
averaged against.

That is what makes a mixed board possible without a second relief. A flat rectangle with a raised step
attached to it is one group; marking the step `exclude` leaves it standing at exactly the height it was drawn
at while a relief shapes only the ground around it, and marking it `hold` flattens it to its own top and lets
the surrounding surface rise to meet it.

**`reach` bounds the statement; the word bounds the ground.** The fill is a screened-Poisson relaxation and
`reach` is the screening: the field decays back to `base` over a characteristic length of that many blocks, so
a finite reach makes each mark a local landform with plain ground between. The default is the trap —
**`reach` of zero means unlimited**, which is what a room-sized group wants and exactly what lets one mark
decide a whole fused board. But reach only says how far the *mark* travels. Every cell the relief covers takes
its height from the solved field, so a shape inside it has no height of its own left however local the marks
are; keeping a drawn height beside a relief is what `exclude` and `hold` are for.

The four outcomes are worth stating as measurements, over one plan — a 30×20 field at surface 9 with a 10×20
step at 19 attached to it, compiled to one group, carrying one bench five blocks below the base inside the
field. The readings are the built column top at the pit, out on the field, and on the step.

| The group's relief | pit | field | step | The board |
|---|---|---|---|---|
| `reach` 0, step inherits | 4 | 4 | 4 | one mark flattens everything, step included |
| `reach` 8, step inherits | 4 | 7 | 8 | the pit is local; the step's rise is gone all the same |
| `reach` 8, step `exclude` | 4 | 6 | 19 | the step keeps its own column — a clean face at the seam |
| `reach` 8, step `hold` | 4 | 12 | 19 | the step is pinned and the ground ramps up to meet it |

A push needs none of this care: it is applied to the solved surface with its own `falloff`, so it is bounded by
construction. And the relaxation puts no extreme where no mark asked for one — what travels is reach, never
invention.

One exception: the word is not read on a shape that declares a `height_mode`. Such a shape already stands out
of the field by construction, and `raise`/`sink` need to read the ground under their own footprint to know
where to stand, which an excluded footprint would not have.

### Layers, and what a stack of them is

**A layout is composed of layers, and the ground is one of them.** The document holds `layers[]` and nothing
beside it, so a flat board is a stack of one — the layer a compiled plan emits, id `ground`, at `base_y` 0.
Every reader takes the stack through one entry point, `SketchLayout.Stack`, which also names a layer that
named nothing by its position (`layer0`); the gate, the rasterizer and the theme scope therefore cannot
disagree about which shapes a document holds, nor about what to call the layer holding them.

**A layer is a slab, and that is the whole of what makes stacking work.** One layer holds exactly one span per
column — a `(Top, Floor)` pair — and where two adds contest a cell the taller replaces the shorter *outright,
floor included*; **at one height the deeper column wins**, so a deck drawn over the ground it crosses leaves
that ground reaching whatever it reached before. So two shapes drawn over one another on a single layer do not
stack: the lower one is simply not in the world, and the gate says so by name (`SK9`, below). A stack is a
stack **of layers**, and `base_y` is what puts one span above another.

Reading the floor on a tie is what makes the merge **commutative**, and that matters because a mirrored board
does not merge its two halves in the same order: the authored half contests two authored shapes, the image
half contests an image against ground already merged. Left to the list order the two faces of one seam came
out differently — a viaduct and the island it lands on, both topping out at the same course, gave solid
ground to bedrock on one face and a four-course deck over void on the other, cell by cell.

The consequence is a drawing rule. **A roofed gallery is walls clamped around a tucked-in floor, on two
layers** — four wall shapes leaving a channel, the floor shape claiming that channel, and the roof on the
layer above. Drawing a low floor inside a tall wall shape on one layer builds the wall alone, because the wall
is the taller add. `opus5-mineshaft` is that board built both ways: the two-layer version and the two one-layer
variants that do not work, all three committed.

**The three height words, and where each is measured from.** `floor` is the underside of a shape, measured
*inside* its layer; `base_height` is the thickness above that floor; `base_y` shifts the whole layer. A cell's
column is that layer's `[floor, floor + height)` shifted by `base_y`, so `base_y 20` with `floor 4` puts a
soffit at y 24 whatever the relief does above it. `anchor_heights` interpolates thickness per vertex across a
polygon, which is how a ramp climbs from one layer's floor to another's.

**Relief solves per layer** and comes back already shifted into world Y, so a stacked board carries one solved
surface per relief-bearing group and a caller never adds `base_y` a second time. **A depression or a river in
an upper layer does not cut through the layer beneath it** — the layers are solved independently, which is
what the author's ruling asks for.

**The set algebra runs by category, not in document order**: `((adds − subtracts) ∪ override-adds) −
override-subtracts`, per layer. An override-add is therefore laid after every ordinary subtract, which is what
lets a shape sit inside a hole another shape cut. **An override add standing in ground keeps the ground under
its floor.** It overwrites the column it lands on, and where that column's ordinary span reaches the
override's floor the result runs from the ground's own floor to the override's top — a wall traced along a
lip with its floor a few courses under the bed is the wall from its floor up and the ground below that, not a
wall over a shaft to bedrock. A deck drawn above the ground's top keeps the air beneath it, because that gap
was drawn; only a column the override actually stands in is filled.

**One line lets an air gap survive between two slabs.** `TerrainPainter.Paint` writes only where the world
already holds *stone*, and the gap between a gallery and the deck over it is air. Without that invariant the
band resolver — which runs bedrock at the bottom and surface at the top of a column — would fill the layer
out of existence.

**A placement names the layer it rests on.** An objective, a spawn, a room and a prop each carry an optional
`layer`; naming none takes the top surface, and naming one the board has no ground on is declined rather than
seated on the top. `docs/world-export/decoration.md` §1 carries the prop half.

**A column carries one theme per layer, not one theme.** The board is painted one pass per layer, each layer
against its own surface, so a cell standing on two layers is painted twice. Within a layer the smallest-area
themed shape still wins a contested cell; across layers there is no contest, because each surface shows its
own. A shape id is unique within its layer and not across the stack — two made things compiled by one tool
number their shapes alike — and the theme a cell paints with is its own layer's shape's. `docs/world-export/terrain-painting.md` §3 carries the mechanism.

**A stacked board is walked layer by layer.** The walk's node is a place — a cell and the layer of it — so
a deck twenty blocks over a yard is two somewheres rather than one, and a step between them is a step only
where the lower one's clearance admits it. A yard roofed by the deck over it is a separate board from the deck;
cut a hole in the roof and the two join there. `docs/world-scan/read-backs.md` §"What a walk costs" carries the
rule.

**What a stacked board says about itself, and what it does not.** `SK10` names two layers cutting into one
another and `SK11` names a mass no route reaches, both as complaints. What neither can say is which layer a
scanned world's ground belongs to: a scan's segments carry no layer at all, because **a scanned cave and a
stacked sketch are one geometry seen twice**, differing only in provenance.

In the editor, the active layer is the one being drawn on; the others ghost underneath in 2-D and stack in the
isometric preview, and a new layer defaults to ten blocks above the highest existing one.

### A made thing, and the three words that say a layer is one

Everything above describes **ground**: slabs stacked into decks, galleries and terraces, each one terrain that
a player stands on. A layer can hold something else. A ship, a balloon, a statue, a gatehouse — a **made
thing** — is drawn out of layers because layers are what can hold an arbitrary solid, and it is neither
terrain nor a dressing prop: a prop is a catalogue entry a stamper places, and a made thing is drawn, shape by
shape, in the same editor and by the same set algebra as the ground.

Three optional fields on a layer say so, and nothing else changes about how it is drawn.

| Field | Takes | Says |
|---|---|---|
| `kind` | `ground` · `made` | what the layer holds. Absent is `ground` |
| `part_of` | any name | the made thing this layer is one slice of, where a thing spans several. Absent, the layer is a whole by itself and answers to its own `id` |
| `seat` | `ground` | that the layer's floors are taken from the ground beneath it rather than stated absolutely |

**`kind` is what keeps the stacking rules off it.** `SK10` reads two layers whose spans meet as a gap that is
not in the world, and `SK11` reads a mass under open sky that nothing reaches as a way onto a deck somebody
forgot to draw. Both are right about terrain and wrong about a sculpture: a solid form sinking into a hill has
no gap to lose, and a raised arm, a dome on columns, an antenna is not standable ground. A `made` layer is
therefore out of `SK10`'s pair walk and out of `SK11`'s detached-mass walk, and the rules stay exactly as
strict about the ground they were written for. Nothing else reads `kind`: a `made` layer rasterizes, paints,
themes and mirrors identically.

**`seat` is where the house model is borrowed from.** A house prop seats on the lowest column of its own
footprint one course down, carves the terrain standing over that floor out of every footprint column, and
declines rather than half-lands (`docs/world-export/structures.md` §6). A seated layer does the same thing
with the same three moves, over the whole made thing rather than over one rectangle: the lowest solid column
under the columns the thing **rests on** decides one drop, every layer of the thing is shifted by it together,
and the ground standing above the settled floor is cut out of the whole footprint so a bank the thing digs
into stops at its own hull. **The drop is one number for the whole thing, not one per layer** — a keel that
settled independently of the deck above it would not be a ship — which is exactly what `part_of` is for:
layers naming one `part_of` seat as a unit, and a layer naming none seats on its own.

**What it rests on is the columns whose own span starts at its lowest course, not its whole shadow.** A crane
standing on a quay with its jib reaching out over the harbour covers water it never touches, and reading the
lowest ground anywhere under that shadow would find the seabed and take the crane down to it. The feet are
what the ground has to carry, so the feet are what the seat is measured from; the cut still runs over the
whole footprint, so a hull driven into a bank clears the bank. This is the seat a placed prop already takes
(`Decorator.Seats` reads a prop's resting course), and the two must not disagree about what resting means.

**What the seat measures against is what the thing is not.** Terrain, and any made thing that states its own
absolute height. So a balloon over a ship does not settle onto the ship's deck unless it says it should — it
either states no `seat` and hangs where it was drawn, or seats and finds the water beneath.

A thing whose footprint covers no ground at all has nothing to measure against and stays at the height it was
drawn; `SK16` says so as a complaint, because a sculpture in open sky is a legitimate board and the word for
it is simply to state no seat.

**A seated layer is painted over its own span.** Terrain's bands run from the bedrock course to the surface,
which is nonsense for a hull flying at y24 — its fill band would claim the whole column beneath it. A `made`
layer's bands run over `[its own floor, its own top]` instead. `docs/world-export/terrain-painting.md` §5
carries that half.

### What the rasterizer makes of it

`SketchRasterizer.RasterizeColumns` turns the document into the solid runs of the finished world, one
`ColumnSegment` each: `(x, z)`, the span `[YFloor, YTop)`, and **the id of the layer that drew it**. A cell
standing on two layers answers twice, once per layer, which is what lets a read tell a gallery from the deck
over it. A layer that named itself keeps its id; one that did not is named by its position (`layer0`), so a
segment can never come back belonging to no layer. Group mirror copies follow the saved `shapeIds` and the setup's
mode, so a group that opted out of mirroring is rasterized once.

**Two rasterizers have to agree, and five constants are what make them.** The live group preview runs the
boolean in the browser (`boolean.js`, over the vendored `polygon-clipping`) because it is the hot path; the
server rasterizes the same document authoritatively and re-detects groups. Neither is derived from the other,
so the drawn outline and the built one stay identical only as long as both sides read the same numbers:

| Constant | Value | Where |
|---|---|---|
| circle resolution | **64** points | `SketchRasterizer.CirclePoints` ⇄ `shape.js CIRCLE_POINTS` |
| Bézier sampling | **16** samples per curved edge, endpoint excluded | `BezierSamples` ⇄ `BEZIER_SAMPLES` |
| set-algebra order | adds − subtracts, then override-adds, then override-subtracts | both |
| `keepClear` footprint | the marked shapes rasterized alone, fanned by group | `SketchRasterizer.KeepClearCells` |
| `controls` keying | the vertex index as a **string** | `Dictionary<string, SketchControl>` |
| `rot_270` | `(Δx, Δz) → (Δz, −Δx)` | the internal third image of a `rot_90` orbit — never an authored mode |

Changing one without the other does not fail loudly; it produces a world whose edges disagree with the picture
by a fraction of a block, which is why the constants carry cross-referencing comments on both sides.

Where a group carries a relief, its surface is solved first (`ReliefFields`) and the same solve is what the
contour preview draws — which is the only reason a preview is worth drawing at all.

What becomes of those columns once Finish runs — the layer scheme the world folder is written in, its
`level.dat`, the coordinate anchoring, the wool-cage chests and the observer platform — is
`docs/world-export/sketch-world-export.md`.

## Phases

**Selection is a ladder of three rungs, and exactly one of them is on screen.** A plain click picks the unit
the phase states: a group in Draw and Relief, because a landmass is what moves and one relief is solved per
group; a shape in Theme, because a theme is assigned to one shape or to everything a group holds. A
**double-click goes one rung down** — from a group to the member under the cursor, from a shape to that
shape's points — and `Escape` comes back up one, so the two are the same gesture reversed. `Enter` is the
keyboard's way down, where the double-click is the pointer's.

**A single-member group is one rung and not two**, because its box and its lone member's box are the same
box and there is nothing for the shape rung to say; drilling it opens the points directly. Reaching a rung
across groups stays a modifier: `Ctrl`/`⌘`+click reaches the shape under the cursor whichever group holds
it, entering that group as the scope in the same motion, and `Alt`+click leaves any scope and picks the
parent group of whatever is under the cursor. Once a group is entered, a plain click reaches a member
shape and a click landing outside its footprint leaves the scope before landing normally — and a click on the
shape whose points are already open leaves them open, so working point by point is not interrupted by
touching what is being worked on.

`Escape` walks the whole way out in the order a press means it: an in-progress draw, then the points, then
the entered group, then the selection itself. With a theme in hand it does none of that and puts the theme
down instead. Closing a drawn polygon is no part of the ladder: a polygon or a polyline closes on `Enter`, or on
a click landing back at its own first vertex.

**The Shapes chip draws every primitive on the board; without it, the selected or entered group draws its own
members instead** — faintly where the group is merely selected, plainly where it is entered, so a member
becomes reachable without hunting for the toggle first.

**Which layer is drawn on is canvas chrome, not any one phase's own state.** The layer strip floats at the
foot of the canvas beside the dock and is present in every phase that draws the canvas. It is where the set
of layers is *changed* as well as switched: only Draw offers the `+` that adds one and the `×` that removes
one, because both are drawing work, and the last layer has no `×` at all — a board always stands on one.
Whatever is placed lands on the active layer — a placement takes it unless it already names one — so an
author is never asked twice which layer something belongs to. The Theme phase's swatch strip is chrome for
the same reason and floats above the dock beside it.

**A phase offers the overlays it can use, and switches on the ones it works with.** The layer bar is not a
fixed six: a phase shows the layer it works on and the layer it works against, and an overlay that would draw
a fact another shown layer already carries is not offered at all. Draw and Relief keep the shapes, the mirror,
the chunk grid and the blocks; Relief adds the contours and leads with them, because the paint has not run
yet and the contours are the only view of what is being stated. Theme and Dressing open with **Blocks and
Shapes on** — the paint is what they act on and the outlines say what carries it — and offer no contour chip,
because the painted ground already carries the height. Snap is in none of them: it changes what a drag does
rather than what is drawn, so it sits in the dock beside the shape tools, in Draw, the one phase that drags a
shape edge onto another. Entering a phase switches on what that phase works with and never switches anything
off, so an overlay asked for by hand is not taken away by walking through the phases.

**How big the selection is reads as a pill under it** — the same pill Configure draws under a region and the
plan draws under a piece, from the one function all three call. The corner readout is for what is being drawn
or measured right now; what the selection measures is under the selection.

**The map's destroyables and cores show as markers**, in the colours the plan places them in. The sketch draws
the ground and owns no objective, so they arrive from the map's intent as positions and nothing else — no
name, no handle, nothing to select. An author refining the ground can see what it has to carry; Configure is
where one is edited.

**Undo is one stack for the whole document, sixty steps deep.** `Ctrl`/`⌘`+`Z` steps back and
`Ctrl`/`⌘`+`Shift`+`Z` (or `Ctrl`+`Y`) steps forward. A step is the whole layout — the value the canvas can be
loaded back into — rather than a record of which edit happened, so a press that changes nothing costs no step
and a drag that fires on every frame between the press and the release costs exactly one. A step is the
document and not the view: the camera and the layer being drawn on are where the author is, so both survive
it.

**Every chord below is also live in the sheet and the palette.** `?` opens the keyboard sheet grouped like the
table below, dimming whatever cannot run on the current selection; `Ctrl`/`⌘`+`K` runs any of them by name.
`docs/client/canvas-interaction.md` describes the mechanism both draw from.

| Chord | Does | Group |
|---|---|---|
| `1`–`5` | Go to Info · Draw · Relief · Theme · Dressing | Phases |
| `V` | Select | Tools |
| `H` | Pan | Tools |
| `R` | Rectangle | Tools |
| `P` | Polygon | Tools |
| `L` | Lasso | Tools |
| `M` | Measure | Tools |
| `X` | Split | Tools |
| `B` | Flip build ⇄ carve | Tools |
| `F` | Fit the working bounds | Canvas |
| Double-click | Go one level deeper — into a group's member, then into that shape's points | Canvas |
| `Ctrl`/`⌘`+click | Reach the shape under the cursor and enter its group as the scope | Canvas |
| `Alt`+click | Pick the parent group and leave any scope | Canvas |
| `Enter` | Go one level deeper, or close the polygon/polyline in progress | Canvas |
| `Escape` | Put the brush down, else cancel the draw, else step back up a level, else clear the selection | Canvas |
| `Delete` / `Backspace` | Delete the selected shape | Canvas |
| Arrow keys | Nudge the selection one block (`Shift` for sixteen) | Canvas |
| `Shift`+`P` | Promote the shape to its own group | Sketch |
| `Ctrl`/`⌘`+`D` | Duplicate the selected shape | Canvas |
| `Alt`+`1`…`5` | Toggle Shapes · Mirror · Chunks · Blocks · Relief, where the phase offers it | Overlays |
| `Alt`+`6` | Snap while dragging | Tools |
| `[` / `]` | Take the previous / next theme in hand — answers only in Theme | Theme |
| `Shift`+click | Paint every shape the group holds, with a theme in hand | Theme |
| `Alt`+click | Lift a shape's theme into the hand, with a theme in hand | Theme |
| `Ctrl`/`⌘`+`Z` | Undo | Everywhere |
| `Ctrl`/`⌘`+`Shift`+`Z` (or `Ctrl`+`Y`) | Redo | Everywhere |
| `Ctrl`/`⌘`+`S` | Save the sketch | Everywhere |

### Info

Two steps. **Identity** is the map's display name and its authors, loaded from `GET /api/map/{slug}` and saved
with `PATCH /api/map/{slug}/metadata`. **Settings** is the symmetry the whole board is built against: the mode
— `mirror_x`, `mirror_z`, `rot_180` or `rot_90` — and the centre X and Z. There is no size to set; the map area
grows to fit whatever is drawn. A freshly created sketch opens here (`?phase=info`); an existing one opens on
Draw.

### Draw

The canvas, and where the geometry is made.

**Three tools draw, and one word decides what they draw.** Rectangle drags a box; polygon places vertices and
closes; lasso traces freehand and closes itself, simplifying the trace at a four-block tolerance so a big round
blob arrives as about ten anchors rather than one per block — it commits as a polygon. Beside them sits the
operation, **Build** or **Carve**, wearing the colour the finished shape will take, so an armed carve cannot be
mistaken for an armed build. Two more tools read and cut rather than make: **measure** drags a ruler between
two points, the usual question being how wide a void gap is, and **split** slices the topmost shape it crosses
into two independent shapes. Every draw and every split drops the
tool back to select when it completes.

**The top two rungs wear the same box, and the third wears none.** A group and a shape are each drawn with
the **transform box** every surface in the studio uses (`docs/client/canvas-interaction.md` §5): four corner
anchors on the selection's own bounds, an invisible grab band along each edge that stretches or squashes that
one axis, and four rotate zones outside the corners. What a grip *does* differs with what it holds — an
group scales all its members proportionally, a rectangle moves the bound the grip names and snaps it to the
other shapes' edges, an outline moves every vertex and every Bézier handle proportionally, because "the same
shape but bigger" is not something point-by-point editing can say — but where the grips sit does not, so an
author learns one box.

**The points rung draws the points and nothing else**: a handle per vertex, a midpoint ghost on edge hover
that inserts a vertex where it is clicked, and a pair of Bézier tangent handles per vertex that round the
edges leaving and arriving — which is how an outline stops being rectilinear. No box, no anchors, no rotate.
That separation is what lets every box sit ON its bounds rather than offset outside it: an edge midpoint
carries no anchor, so the insert ghost has the spot to itself, and a corner carries no vertex handle, because
the rung that draws vertex handles draws no corners.

**A grip's shape says which rung it belongs to, and its colour says what it is.** A square scales a whole
outline; a **disc is one point of it**, the size of the midpoint-insert ghost, because what that ghost offers
is another point of the same kind — the only difference between them is that the ghost is drawn in the
lighter accent, being a point proposed rather than a point placed. Both wear the accent, so nothing about
being drilled in is read off the colour: colour is left to say what a point IS — plain, picked for its own
height, or shift-marked in `--warning` as a surface-slope control.

A rectangle can be **promoted** to a polygon in place, keeping its id and therefore its group membership.
Selection can be rotated by a stated number of degrees about its own bounding-box centre, nudged a block at a
time with the arrow keys (sixteen with Shift), moved with snapping to other shapes' edges (Alt bypasses),
have its operation or its override flipped, or be deleted.

**Height is edited three ways.** The whole shape takes a floor and a thickness. A single selected vertex takes
its own height, which materialises the per-vertex array on first use. And two or three vertices shift-marked as
controls fit a plane through their stated heights and fill every remaining vertex from it — a ramp from two, an
aimed plane from three — rounding to blocks, so a slope reads as the neat straight steps of a staircase.

Six overlays sit above the canvas: **Shapes** (the draw primitives over the fused groups), **Mirror** (the
symmetry copies), **Chunks** (the 16-block grid), **Blocks** (the rasterized footprint — the exact cells an
export would fill), **Relief** (the height contours of whatever relief the groups carry) and **Snap**. A
read-only isometric preview draws **the world the export builds**: entering it posts the live layout to
`sketch/columns`, which runs the real build and answers every column's solid runs, and the browser meshes
those into triangles. So the picture carries the terrain's own materials, the relief the groups were solved
to, the structures stamped on them and the goal markers hanging over the build ceiling — none of which the
browser can derive. It rotates in 90° steps and disables itself where WebGL is unavailable.

**In the preview the overlay chips become the board's own layers**, one per sketch layer, because what an
author wants of a picture of a stack is to take the deck off and look under it. Every run in the payload says
which layer drew it, so hiding one is a filter over what the browser already holds rather than a second build
— and the faces are re-meshed against the board actually shown, so a gallery under a hidden deck gets its top
face rather than reading as roofed by something no longer there. **A run belongs to a layer by where it
starts**: the painter writes several runs inside one span, a stone core and the bands over it, and every one
of them begins inside the span that made the ground. A run beginning outside every span is a structure
standing on the terrain rather than being it — a house, a tree — and belongs to no layer, so hiding the
ground a house stands on does not take the house with it. A board of one layer shows no chips: there is
nothing to take off. A hidden layer **stays hidden across an edit**, and one the author deletes comes back
neither listed nor hidden.

**Height is why it works this way.** A column's top is settled in three stages — the rasterized ground, then
the per-group relief solve, then whatever an erected shape says about levelling, raising or sinking — and
only the first is knowable client-side. A preview that extruded each shape to its own thickness drew stage one
and called it the answer, so a mesa read at its own thickness and a hillside read as a plate. Asking the
server is what makes the one view that exists to show height show the height model.

The build is the cost, not the payload: on a full board it is around a second against forty milliseconds to
read the columns back out. Nothing is drawn in 3-D, so the fetch happens on **entering** the preview rather
than on every edit — the view swaps at once and fills in when the columns land. Rotating redraws the mesh
already in hand, and re-entering an untouched board draws it again rather than rebuilding.

The sidebar carries the active layer's settings — its name and its base Y — and the group→shape tree; the
inspector on the right edits whatever is selected. Which layer is active, and which layers there are, is
the strip's alone: a layer is switched where it is drawn on, so the sidebar states what the layer **is**
rather than offering a second list of the same rows.

### Relief

One step, and the phase that turns flat plateaus into terrain. Everything here is stated **inside a group**,
so the group tree is half the sidebar and the list of what has been stated is the other half. Which of the
group's shapes the solve actually covers is not stated here but on the shapes themselves, through
`relief_scope` in the Draw inspector — a shape can hold its own level or leave the field altogether.

The group's own settings are what every mark is measured against: `base` (the level the field falls back to
where nothing is stated), `reach` (how far a mark's influence travels before the field returns to `base` —
zero is unlimited, and a finite value is what keeps a landform local on a large group), `step` (the
block quantum the finished surface snaps to — a step of two is the one setting here that can break a map, and
there is deliberately nothing that repairs it), `landform` (what kind of
ground this is meant to be — one of `plain`, `rolling`, `hills`, `mountain` — which the readback measures
the solved surface against) and `grain` (a wobble applied after the solve: amplitude, feature scale, seed).
A word outside those four is a `SK3` complaint on the stored document: `RL1` judges a group only against a
landform it recognises, so an unknown word or the wrong case turns that gate off rather than failing it.

**`base` is the whole group's height, and a fresh relief takes the group's own.** A relief replaces the top
of every column of its group, so where the marks say nothing the ground is at `base` — a base that differs
from the level the shapes were drawn at moves the whole landmass the moment the first mark lands. A relief
created in the editor therefore starts at the group's own top: the most common `floor + base_height` among
its add shapes, ties to the tallest, which for a plan-derived group is one number. The panel states that
level beside the field and says which way the ground moves where the two differ, because a base an author
cannot compare is a set of heights they cannot read.

Five things are placed, and they divide into two kinds.

| Tool | Kind | States |
|---|---|---|
| Spot height | `point` | a height at a place, over a radius |
| Ridgeline | `line` | a traced line at one height, or one height per vertex, over a band either side |
| Bench | `area` | a closed ring held at one height |
| Scarp | `scarp` | a traced line with a shelf above and ground below — `high`, `low`, the `face` it drops over and the `band` either side for the land to arrive through. The `high` side is the +z hand of the drawn direction: south of a line traced west to east, north of one traced east to west |
| Push | `push` | a closed ring lifted by an `amount`, with `falloff`, `roughness`, `crown` and a seed |

The first four are **marks**, and a mark is a constraint: the ground here *is* twelve. Two marks over the same
ground argue, and the solver settles it. A **push** is different in kind — it is applied to the solved surface
afterwards as a relative lift, so two pushes over the same ground simply add, which is what makes a spur on the
flank of a hill one operation rather than a restatement of the hill. A push's lift can vary per ring vertex,
which is what makes a drawn ridge fall along its length; setting every vertex to the same number collapses the
array back to the single amount.

A sixth mark, the **rim**, is not placed at all: it holds the group's whole outline, so it rides as a property
of the group's relief — one height and a depth.

The document is keyed by group id and carries the group's own settings beside the two lists. This one states
all six, and solves to a surface running 7 to 16:

```json
{ "relief": {
  "i1": {
    "base": 9, "reach": 14, "step": 1, "landform": "rolling",
    "grain": { "amplitude": 1.2, "scale": 12, "seed": 1 },
    "marks": [
      { "id": "r1", "kind": "point", "at": [-30, -10], "h": 15, "r": 5 },
      { "id": "r2", "kind": "line",  "points": [[-36, 6], [-20, 10], [-6, 6]],
        "h": [12, 14, 11], "r": 1.5 },
      { "id": "r3", "kind": "area",  "ring": [[-38, -18], [-24, -18], [-24, -6], [-38, -6]], "h": 7 },
      { "id": "r4", "kind": "scarp", "points": [[2, -16], [2, 16]],
        "high": 14, "low": 8, "face": 2, "band": 5 },
      { "id": "r5", "kind": "rim",   "h": 9, "depth": 1 }
    ],
    "pushes": [
      { "id": "r6", "ring": [[10, -8], [24, -8], [24, 8], [10, 8]],
        "amount": 5, "amounts": [5, 5, 2, 2],
        "falloff": 10, "roughness": 0.3, "crown": 2, "seed": 1 }
    ]
  } } }
```

Two shapes of `h` are both correct: a point, an area and a rim state one number, while a line may state one
per vertex — `"h": 9` and `"h": [12, 14, 11]` are each what an author would write, and the format reads
either. A push carries no `kind` on the wire, because the array it sits in says what it is; `amounts` states a
lift per ring vertex and collapses back to the single `amount` when they all agree. And a mark that does not
carry what its kind needs is **dropped rather than defaulted** — a point without `at`, a line or scarp under
two points, an area or push under three ring vertices never reaches the solver.

Two server-side reads support the phase. The **contour overlay** posts the live layout and gets back traced
lines per group, at a stated interval, from the build's own solver, so what is drawn cannot differ from what
will be built. The **readback** answers what the stated terrain *charges* a player: reachability at each of the
three thresholds a player has (a jump, a placed block, building in earnest), places separated from ledges and
each piece named with its cell count, its middle and its box, faces qualified as cliffs, crossings measured in
both directions because a drop is free the way it falls, and the symmetry error. It is asked for rather than pushed, since it is a second solve's worth of measurement.

`docs/world-export/relief.md` is this phase written out in full: the relaxation between the marks and why it
is that rather than a weighting, what each knob costs measured on a room and on a whole map, how steepness
decides where players can go, the symmetry fold that makes two halves identical rather than close, and the
corpus reading the whole model is calibrated against.

### Theme

One step: the map's paint, picked and placed. A theme is not authored here — that is the library's, and
`library.md` is where a bucket, a material and the fourteen material kinds are written out. This phase takes
one of the board's themes in hand and puts it on the ground.

**The board's themes are a strip at the foot of the canvas**, above the dock and beside the layer strip,
because a theme belongs to the board rather than to the phase and every click in the phase is addressed to
whichever one is in hand. Clicking a swatch takes it in hand; clicking it again puts it down; `[` and `]` step
through the registry with the empty hand as one of the stops, so one pair of keys reaches every theme however
many there are. The map
default is badged on its own swatch. The trailing `+` opens the inspector's **Add from the library** panel,
which takes the column while it is up: a library theme copied in lands as a snapshot under its library name,
and copying one in again under the same name replaces it, which is how a theme edited in the library is
brought up to date.

**With a theme in hand the canvas is a brush**, and the modifiers are read against what is held rather than
against the grouping. A click paints the shape under it; `Shift`+click widens the stroke to every shape the
group holds; `Alt`+click lifts that shape's own theme back into the hand. There is no apply button and no
scope control — with an empty hand the usual selection rule applies unchanged, and the tree reaches a group
either way. A shape carries the assignment (`shape.theme`), a group stroke writes it to every member, and a
cell that carries none falls to the map default, so the resolution is shape, then map. `Escape` puts the brush
down — a thing in hand is the first thing it lets go of — and so does leaving the phase.

**The inspector says what is in hand, what the selection carries, and — with nothing selected — what the board
falls back to.** In hand: the sample plateau the theme finishes, a swatch per bucket, and the two acts that
change the registry rather than the board — **Save to library**, which decomposes the theme into one style per
bucket so it can be edited there, and **Remove**, which takes it off the board. Selection: what that shape or
group is painted with, `mixed` where a group's shapes disagree, and **Unpaint**. Board defaults, when
nothing is selected: the map default, how many shapes are still falling through to it, and the room shells.

**The map default is the board's; the built-in is stone.** Every bucket of `TerrainTheme.Default` is stone —
what unpainted ground already is — so a board that names no theme exports as a board that names no theme, and
a bucket a theme leaves unbound resolves to stone rather than borrowing a finish it never asked for. The
finishes worth starting from are named themes: `ThemePresets` holds six — `meadow`, `dunes`, `ashfall`,
`firnline`, `claybed`, `oldstone` — and `LibrarySeed` puts them in the library, where they are picked like any
other.

**Room shells** sit under the board defaults, because they are a fallback in the same sense the map default
is: one shell for every wool cage and one for every spawn cube. Two bindings and no more — rooms are fanned
across the symmetry orbit so both sides face the same building, and a per-room shell would be a sightline that
differed between teams. What is stored is the composed style's **JSON snapshot**, not the library id it came
from, so editing the library afterwards cannot rebuild a shipped map's rooms. Each binding has three states,
and they are genuinely distinct: absent stamps that kind's built-in shell, an object stamps the bound style,
and an explicit null means no building at all — a pad on open ground.

```json
{ "roomStyles": { "cage": { "form": "gable", "pitch": 1 }, "spawn": null } }
```

That map stamps its wool cages with the bound style and gives its spawns no building at all. Leaving `spawn`
out entirely — rather than writing `null` — is the third state, and stamps the built-in spawn shell.

**All three are one select per kind**: `(the built-in shell)`, `(no building)`, then every room style the
library holds. The row's ✕ appears once a kind is off the built-in and returns it there, which is the state
with no snapshot to store. A binding is a wish rather than a guarantee — a footprint too small to carry walls
raises nothing whatever is bound (`WX2`, `docs/world-export/structures.md`), and the room's pad, chests and
monuments are stamped either way.

**The Blocks overlay is on when the phase opens**, so the phase shows the real paint: the live layout is
posted to the server, the actual painter runs over it, and one colour per footprint cell comes back as a
bitmap, so a Voronoi reads as its cells and a noise field as its patches rather than as one representative
colour. The contour overlay is not offered here — the painted ground already carries the height, and contours
over it draw the same fact twice.

`tools/seeds/ruediger.layout.json` is the worked example. It carries three themes and names `ruediger` as the
map default; four shapes take `ruediger-steps`, thirteen take `theme`, and the remaining nine inherit the
default. That is the pattern the phase exists for — the stepped area reads as built and the ground around it
reads as ground, which a single blanket theme cannot do. The same file is the reference for two Draw features:
five of its outlines carry Bézier `controls`, and its one negative shape is a `subtract` rectangle standing a
hundred blocks tall over a floor of zero, which cuts a channel clean through the board and demonstrates that a
subtract's height is a statement of intent rather than a depth.

What the painter does with a theme is `docs/world-export/terrain-painting.md`: how a column is classified into
one of the buckets from its neighbours alone, what each bucket claims when two could claim the same cell, how
a theme resolves per cell through the shape/map scope, and the `TP*` rules the whole pass is written to. The
stamp a room shell produces is `docs/world-export/structures.md`, whose §7 is also where a room style's own
anatomy is written out — the roof as a height field, the storey stack, the porch taken out of the footprint —
which is what the Dressing phase's building prop stamps too.

### Dressing

One step, because dressing has nothing to define up front: every part of it is a thing put somewhere. The
sidebar is the list of what is placed, and the inspector edits either the selection or — with nothing selected
— the settings the next prop of that kind will start from.

**Dressing is authored, not sprinkled, and that is the whole design.** A tree is cover and a boulder is a
wall, so *where* each one stands is a decision about how the map plays and belongs to the person making the
map: the pass places exactly what was placed and nothing else. There is no scatter, no density pass over the
board, no "fill this group with forest". Within a drawn area the individual blades are a noise field, because
nobody places nine hundred of them by hand — but the area itself was drawn.

**One piece of ground answers back.** A destroyable and a core keep the ground they cover, grown by four
blocks, clear of anything that hides them: ground cover grows across it and under a floating monument, and
tall grass does not. `decoration.md` §3.1 is the rule and the reason. Every prop is then fanned across
the symmetry orbit, so one half of a map is dressed and both halves match. Each carries a `seed`, so two props
of the same kind and knobs differ from each other while any one prop re-exports identically.

`docs/world-export/decoration.md` is the pass this phase feeds, one section per tool and each carrying its
`DR*` rules: how a flora field reads the paint under it, how a stroke's band is derived from its centreline, how
a boulder is bedded into the ground it stands on, how a tree is copied or grown, and how a channel is dug.
Two of those reach further. A grown tree is scored against `tree-corpus.md`, the 75 hand-built trees that are the measured ground
truth for what a tree looks like; and the building prop stamps `structures.md`'s house, which is why what it
can be made of runs past what this phase can state.

Six things can be placed, in three placement geometries.

| Tool | Kind | Placed by | Starts as |
|---|---|---|---|
| Stroke | `stroke` | tracing a line | gravel, radius 3, `solid`, coverage 0.7, paint rather than a route |
| Water | `water` | tracing a line | a `canal` radius 3, cut 2 deep, a 2-block shore over a Voronoi bank; `shape: pool` fills a drawn ring instead, and `level` states a world Y where a basin has to hold water |
| Ground cover | `flora` | tracing a ring | coverage 0.45 at scale 12, with fern and flower shares |
| Building | `house` | dragging a rectangle | no style of its own until one is picked from the room-style library |
| Tree | `tree` | a click | no recipe of its own until one is picked from the tree library |
| Boulder | `boulder` | a click | no recipe of its own until one is picked from the boulder library |

**Every one of them takes a style, and for the two that lay ground the style and the material are separate
questions.** A **stroke** replaces the surface it crosses rather than adding to it — it is a finish, not
terrain, which is why it carries a material and no height. Its `style` shapes the *band*: `solid` holds one width the whole way for a clean utility road, `worn` thins it by
a per-cell dice (that is what `coverage` spends), `rough` wanders the width by a noise field so the outline is
organic rather than ruled, `stones` lays discs at intervals with gaps between them — stepping stones across a
void — and `tapered` runs it fat in the middle and thin at the ends. What fills the band is `pave`, a **full
terrain material**: a solid block, a layer stack, a Voronoi patchwork, a noise ramp, any pattern the painter
offers. The two are independent, so a worn cobble and a solid cobble are both sayable.

**`claimsGround` is the third question, and neither of the first two answers it.** A style is a brush; it
says nothing about whether the paving is a thing on the board or a finish on the ground, and the same brush at
the same radius draws a road between two spawns and a grass tongue over a crag. `claimsGround` is off unless
stated: a claiming stroke holds the cells it covers, so a tree keeps three blocks off it and a boulder two
(`DR-ROAD`) and a building may end one but not stand across it (`DR-CROSS`), while paint claims nothing and is
planted over. It is not a claim that players walk here — a protected verge and a road are the same
declaration. Marking every stroke a claiming one is how a board ends up with nowhere left to plant.

**Water** is the one prop that changes the ground rather than the surface: it cuts a bed and fills it to a
level water line, because water laid flat on a surface reads as blue paint. Its `shape` says what its points
mean — a `channel` strokes them as a centerline and takes its width from `radius`, a `pool` closes them into a
ring and fills it, which is the only way to make a harbour or a lake with square corners; on a pool `radius`
is the shelf the bed takes to reach full depth. It fills round whatever stands in it and never cuts under it,
so a ship moored in a harbour floats rather than sitting in a dry hole. Absent a `level` the line is
derived — the lowest surface the channel crosses — and the prop only ever carves existing terrain: the cut
stops at the surface it crosses and never fills what was already air. **`level` states the world Y instead**,
and then the fill reaches it whatever the column beneath is doing, which is what fills a basin dug out in the
shapes: a lake, a harbour, the water a ship floats on has no surface up at the line for a derived one to find.
**The basin is a low floor and not a hole** — the pass skips any column the surface map does not carry, so a
subtract leaves nothing to fill and a harbour is an override add laying a floor at the depth the water reaches
down to. The footprint bounds it either way — the pass never floods outward, so the rim is the author's. Its `form` is `canal`
(a clean uniform width, deepest on the centreline), `natural` (the width wandered by noise) or `stream` (the
width pinching and swelling on a beat down the arc, running shallower throughout, so it reads as riffles
rather than one even channel). Around that: `radius` and `depth`, `edge` for how far a natural or stream bank
wobbles, `shore` for how wide a beach the water meets the land through with `shoreWander` for whether that
beach opens and closes along the run, and `bank` — again a full terrain material, defaulting to a Voronoi of
gravel edges, coarse dirt inside them and sand in the middle, which shows through the shallows and continues
as the beach.

**A tree and a boulder are recipes rather than knobs, because a click has no geometry to draw.** What is
placed is a point plus a `style` key; what stands there is a row from the library, pulled into the document's
own registry under its name (`docs/tools/library.md`). A stroke and a channel are *traced*, so their knobs stay
here — pre-authoring a river form is authoring a shape without its place — and the split is exactly that line
(author).

**A tree is two different things rather than one thing with a switch.** A `template` tree is vanilla: its
`species` — oak, birch, spruce, jungle, acacia or dark oak — names its wood, its canopy profile and its
proportions together, since a notched cone is a spruce and a flat umbrella on a leaning trunk is an acacia,
and neither is a knob setting of the other; `height` scales the lot. A `grown` tree is the recursive skeleton,
where the shape is the author's and `wood` (the same six) is all that is left to name: `stems` one to three,
`leader` for how far the central axis climbs, `flow` for how much the trunk wanders, `branchAngle`, `levels`
two or three, `whorled` for the ring-every-few-courses conifer against the broadleaf, and `leafSize`. Each
form reads only its own fields, so the others are inert rather than wrong. Both live on the **recipe**, so a
grove of forty oaks is forty positions and one row, and retuning that row retunes the grove.

**A boulder** is a glacial erratic: a mass standing on the ground, bedded a third of its height into it. It
takes a `form` — `round` (a weathered erratic), `angular` (the same rock, its surface broken), `outcrop` (wide
flat lobes with their middle at the surface, a low shelf rather than a rock) or `cairn` (three shrinking lobes
stacked) — a `size` from 2 to 10 blocks of reach, default 4, a `mossy` flag for whether moss creeps onto the
sky-lit faces, and `rock`, a full terrain material like a stroke's paving. All four are the recipe's. A rock's material resolves in the
**boulder's own frame** rather than the map's, so a mottled stone carries the same mottling to every image of
its orbit instead of sampling whatever the world pattern says where each image happened to land.

**Ground cover** is the one place a density field is the point: a drawn ring filled by `coverage` at a feature
`scale` over some `octaves`, split by `fernShare`, `flowerShare` (with its own `flowerScale`) and `tallShare`.

The pickers show **your** prop rather than a stock one. `GET /terrain/stroke-styles?pave=…` draws the five band
styles in the material already chosen, `/terrain/boulder-forms?rock=…` the four rock shapes in the author's
stone, and `/terrain/water-forms` the three channels as actual dug beds — so the question answered is
"what would mine look like", not "what does the catalogue contain". A tree and a boulder are picked from their
own libraries instead, each row drawn through the pass that builds it. `POST /terrain/prop-preview` renders one
before it is placed — and a building whose wings make no building is refused there with the same `HJ*`/`HP*`
findings the build acts on, rather than drawn as though it would stand.

Two knobs are bounded rather than free, and it is load-bearing: a tree's height is held to 5–40 and its leaf
cluster to 0.2–1, and a boulder's size to 2–10. Cost is superlinear in reach — a grown crown is filled by
testing every cell of its bounding box — so a `leader` of 55 rather than 0.55 would not draw a strange tree,
it would ask for a volume hundreds of blocks across and never return.

The two clicked kinds are **markers**, and a marker seats on the ground: it can only be dropped where there is
terrain, and dragging one across the void simply does not follow, so it stays on the last real cell it was
over. The cursor shows in advance whether a spot will take it. A **building** is the only prop placed as a
rectangle, because a stamper takes a box: it must be at least three by three to hold two walls and an inside,
and no larger than 192 blocks of footprint, and a drag outside that range places nothing.

**Two buildings are joined into one with `mod+g`, and the same chord takes a joined one apart.** Shift-click
picks a second building beside the first; joining keeps the earliest-placed one — its id, its style, its door
edge, its seed and its layer — and gives it the others' wings, and taking apart is the exact inverse, one
building per wing, each keeping what the whole one stated. A joined building draws as the **union** of its
wings rather than as a box each, because the union is what the stamp takes and a box each reads as two
buildings with a seam down the middle; wings that do not touch still draw an outline each, because that is
what they are. Every wing wears its own grips, so a joined building is reshaped a rectangle at a time. The
inspector's Wings row does the same thing for an author who would rather press a button.

**A building is not dragged onto another building's ground.** A plan states its ground once, so the drag stops
against a standing building the way a marker's stops at the void, leaving the prop on the last legal cell. It
is the necessary half of what `DR-CLAIM` asks at export, applied in the pointer because a drag cannot wait for
an answer; the server stays the authority, and claims what it stamps grown a block outward, which is wider.

**Whether the wings make a building is the joint model's to say, and it is asked rather than copied.** The
`mod+g` gesture refuses only the two cases no reading is needed for — two buildings standing on the same
ground, and two that do not touch at all, since a building is one shell under one roof and a corner is not an
edge to build a joint on — and every
other verdict comes back from `POST /api/terrain/prop-preview`, which answers `HP1`–`HP3` for the prop's own
shape and `HJ1`–`HJ5` for how its wings meet. The inspector renders the gate's own sentence, so an author who
joins two ranges lying side by side is told they meet in a gutter and to turn one across the other. A join is
one undo step, so a refused one is taken back with `mod+z` or by pressing the chord again.

**A building prop states one or more touching rectangles, and what each one is.** Its `wings` field is a list
of entries — `corners`, the two opposite corners a drag always stored, and an optional `spec` holding
everything the wing states about itself — and `HouseProp.Plan()` composes them into a `BuildingPlan`
(`src/PgmStudio.Minecraft/Houses/BuildingPlan.cs`) — **one or more touching rectangles**, the same shape `HouseStamper`
has always taken. An L, a T or a U is therefore one house under one style rather than two standing beside each
other: the outline is walked as a single landmass, so an L answers six runs of wall and a T eight, a wall ends
wherever the building turns, and the cell where two wings meet is an inner corner carrying a post of its own.
Each wing may stop short of the building's full height and may override the roof form, pitch, slab and ridge
axis, and a storey is then its own plan over the wings still standing — which is how a one-storey hall with a two-storey
cross wing gets the wall it needs against the hall's roof with no rule written for it. Each wing is still held
to the three-block floor a single rectangle always needed, and the whole plan to `MaxFootprint` (192 blocks)
measured over the ground the wings actually cover rather than the box drawn round them, so an L takes no more
of the cap for reading larger on the corner it never stood on (`G177`).

**Wings touch and never overlap**, and where they touch they share the edge whole — the shorter edge lying
within the longer. A pair standing clear is two buildings; a pair sharing blocks, or meeting over part of an
edge only, is neither, and the plan is refused. Which of the two is the **hall** follows from the ridges: the
hall's runs along the shared edge and the wing's runs into it, both along it is two ranges side by side and
both into it is one longer range. A wing also reaches no further along that edge than the hall reaches across
it, since a gable's height follows the span its slopes cross. `HouseProp.Fault()` names the rule a refused plan
broke — `HJ1` overlapping, `HJ2` a partial touch, `HJ3` a gutter, `HJ4` one longer range, `HJ5` a wing standing
taller than its hall — and `Plan()` answers null for the same plans, so a build gets the plan or nothing.

**The roof over a junction is built and has two behaviours.** A building's roof is the union of its wings'
roofs, and each wing is extruded as the whole building it would be alone; where their volumes meet, **only the
highest surface over a cell is written**, so the lower one does not stand inside the higher as an obstruction
in the attic. Which of the two behaviours a joint gets is the **wing's own choice** and the one thing about a
joint the rectangles cannot say: a wing that **marches** carries each course of its roof on into the hall's
until that roof already stands as tall — no overhang, since an overhang is what a roof has outside a wall and
inside another wing there is no outside, and bounded by the marching course's own distance from its own eave,
so a course whose crown never meets a shallower or flatter neighbour's still stops rather than running the
neighbour's whole length. A wing that **projects** (`Wing.Projects`) carries its roof clean across the hall to
the far wall and shows a second gable standing on it. Marching is the default, because it is the shape that
reads as one house (`G172`, `G186`).

Whether two wings make a junction at all is whether their **ridges cross**, and a wing's proportions cannot
know that: a roof pitches across the shorter side, so a 10 × 5 hall and a 7 × 6 wing both ridge along x and
meet in a gutter, and a **square** wing ties toward x and can never cross anything. `ridge` states the axis
where the proportions should not decide it. Every image of a building carries the same statements — a
projecting wing still projects, a hipped wing is still hipped — and the ridge is the one of them a quarter turn
changes, so it is turned with the rectangle rather than dropped.

**A wing's `spec` is the six things it can say about itself**, every one optional, and a wing that says nothing
is a rectangle wearing the building's own everything — which is what every wing meant before there was
anything else to say, and why a stored document whose wings are bare corner pairs reads unchanged.

| Field | Is |
|---|---|
| `storeysHigh` | how many of the style's storeys stand on this wing; nought takes them all. Deliberately not `storeys`, which on a **style** is the list of storey styles a building is made of |
| `form` · `pitch` · `roofSlab` | the roof this wing wears where it does not wear the building's — the same three the style names, resolved as one decision rather than three |
| `ridge` | `AlongX` or `AlongZ`, where the wing's own proportions should not decide |
| `projects` | whether the wing carries its roof across the hall instead of marching into it |

```json
{ "kind": "house", "id": "h1", "seed": 1, "wings": [
  { "corners": [[0, 6], [9, 10]] },
  { "corners": [[0, 0], [4, 5]], "spec": { "ridge": "AlongZ", "projects": true } } ] }
```

Three things follow from the junction being one building rather than two roofs in one place (`G179`–`G181`).
The loft over it is **one space**: the side of a wing standing against a neighbour is a doorway rather than an
outside face, so no gable rises there and a marching T carries **three** gable faces where a projecting one
carries four. A **verge** climbs and an **eave** does not, so the cells beneath a verge overhang are air while
an eave overhang is a solid course — and where the two meet the verge crowns higher and the eave gives way,
which keeps a gable end reading as a roof hanging past its wall instead of a filled panel. And the **rim is the
building's**: a verge is the outer edge of the roof plan as a whole, never the edge of whichever rectangle
happened to lay the block.

**The overlap rule tells two buildings colliding from one building's own wings meeting.** Two props whose
plans share a cell are still refused — the second is dropped rather than raised through the first's walls — but
a plan's own wings never reach that test against each other, since the whole plan is composed and checked as
one `BuildingPlan` before anything is placed. A second wing is drawn as a building of its own and joined to the
first with `mod+g`, so what the canvas drags is always a rectangle and what it stamps is the plan they make
together.

The document is a flat list of what was placed, in placement order, each entry carrying its own knobs. One of
each:

```json
{ "dressing": { "props": [
  { "id": "d1", "kind": "stroke", "seed": 1, "points": [[-36, 0], [-20, 4], [-4, 0]],
    "radius": 3, "style": "worn", "coverage": 0.7, "claimsGround": true,
    "pave": { "kind": "solid", "id": 13, "data": 0 } },
  { "id": "d2", "kind": "water", "seed": 2, "points": [[-30, -16], [-16, -12]],
    "radius": 3, "depth": 2, "form": "stream", "edge": 0.8, "shore": 2, "shoreWander": true,
    "bank": { "kind": "solid", "id": 12, "data": 0 } },
  { "id": "d2b", "kind": "water", "seed": 6, "shape": "pool", "layer": "ground", "level": 12,
    "points": [[-30, 16], [-6, 16], [-6, 34], [-30, 34]], "radius": 6, "depth": 3, "shore": 2,
    "bank": { "kind": "solid", "id": 12, "data": 0 } },
  { "id": "d3", "kind": "flora", "seed": 3,
    "points": [[-38, 8], [-26, 8], [-26, 18], [-38, 18]],
    "spec": { "coverage": 0.45, "scale": 12, "octaves": 3,
              "fernShare": 0.25, "flowerShare": 0.18, "flowerScale": 18, "tallShare": 0 } },
  { "id": "d4", "kind": "house", "seed": 4, "points": [[-22, -16], [-14, -8]],
    "front": "negZ", "style": {} },
  { "id": "d5", "kind": "tree", "seed": 5, "x": -32, "z": 2, "layer": "ground",
    "form": "template", "species": "birch", "height": 12 },
  { "id": "d6", "kind": "boulder", "seed": 6, "x": -8, "z": 14,
    "form": "cairn", "size": 3, "mossy": true,
    "rock": { "kind": "solid", "id": 1, "data": 0 } }
] } }
```

The three geometries are visible in the shape of the entries: a marker carries `x`/`z`, a traced prop carries
`points` — a line for a stroke or a channel, a closed ring for ground cover — and a building carries the two
opposite corners of its rectangle. `pave` and `bank` are full terrain materials, so any of the fourteen kinds
in `library.md` may stand there.

**What a prop is *made of* is named once, beside the placements.** A tree, a boulder and a building each carry
a `style` key into the document's own `styles` registry, and the registry states each recipe once — a board
carrying hundreds of trees over a few dozen recipes stores the recipes and not the repetition, and changing one
changes every placement wearing it. A recipe names its kind the way a placement does, so one registry holds all
three; the key is read off the recipe (`oak-10`, `grown-birch-18`, `copied-716`, `angular-6`), so it says what
it is rather than counting, and two recipes that read the same way are numbered rather than collapsed. A
copied tree's key counts its blocks, which is the one thing about a cut body that reads at a glance.

A key naming no recipe is a **refusal**, not a fallback: a tree built as a stock oak because its recipe went
missing is a map that differs from the one the author drew, and nothing downstream could tell. A document
stored before recipes had names states them on every placement, and reading it forward is what names them —
identical recipes collapse onto one key and each placement is left carrying it.

**The registry is the document's, not the library's.** The export reads a stored layout and has no database to
resolve a library row against, and a shipped map must build the same way next year as it did today. A library
row is *pulled in* — copied into the registry under a key — and editing that row afterwards changes the next
pull rather than a map already written. That is the same rule a bound room style follows, kept by referencing
inside the document rather than by copying onto every placement.

**On a stacked board a prop says which storey it rests on.** Every entry takes an optional `layer` — the id of
the layer whose surface it sits on, as the tree above states it. Naming none takes the board's **ground** —
the tops of everything that is not a made thing — which is where a flat board's props all are and where
everything authored before the stack landed stays. It is the ground and not the highest thing standing,
because a made thing is not a storey to rest on: a cloud or a balloon over a cell would otherwise be that
cell's answer, and the goal beneath one was stamped eighty-three blocks up on it.
`DressingContext.GroundFor` answers that layer's own surfaces, and it is the same storey reading
`BuiltTerrain.SurfaceFor` gives a stamped thing, so a prop and a monument on one floor cannot disagree about
where that floor is. Naming a layer the board has no ground at is **declined** (`DR-LAYER`) rather than seated
on the top, because the top is exactly the storey the author was saying they did not mean. `decoration.md`
carries the pass itself. The Dressing phase cannot state it — the layer rail renders only in Draw, so a prop
placed in the browser always takes the top surface and a `layer` set over the API survives an edit unshown
(`B263`).

**Every word above is written in camelCase, and that is the canonical form** — what `POST .../sketch/finish`
and the export always write back, and the form every example in this document is in. The reader is more
forgiving than the writer: a prop's own enum fields (`style`, `form`) are matched case-insensitively, so
`"Worn"` or `"WORN"` reads the same as `"worn"`, and `kind` — the discriminator that says which prop or which
material an object is — no longer has to be an object's first key (`DR-DOC`). What `kind` cannot be is a word the
reader does not know: `"boulderr"` or a missing `kind` both refuse the document by name rather than being
silently misread. A document that fails to parse anywhere — one bad field, one unrecognized `kind` — refuses
the whole export rather than exporting with fewer props than it was asked for; see *What it refuses* in
`configure.md`, since the refusal fires at export, not while the sketch is merely saved.

Dressing does not repaint the Blocks overlay, which shows the painter's surface colours — a prop adds blocks
*above* the surface.

## Refusals and complaints

The sketch has almost no gate, and that is deliberate: an unfinished drawing is a legitimate state, and the
tool saves it. Eight things nonetheless refuse — and beside them sits a second list, of what the document says
that the build cannot honour, which **complains** rather than refusing because the board still builds.

**Everything the finish is made of is checked before the layout is stored** — the house styles a board binds
and the terrain themes it registers, in one gate, because they enter together and are built together. The
themes answer for what their own materials cannot do at the depth a bucket claims (`PT1`,
`docs/world-export/terrain-painting.md`): a surfacing block below the course it surfaces, which is the single
most repeated authoring mistake in this repository. They answer for what a pattern states and did not carry as
well (`PT2`): a `voronoi`'s `bands` and a `layered`'s `stack` each take a **pair** — a material and a depth —
where a `noise`'s `stops` takes bare materials, so a list of materials handed to `bands` binds one band per
entry with the material left empty. Each of those three members is a value type, so the document binds rather
than failing, and without the gate the empty material is met by the painter while the world is being built.

**A dressing document that will not parse is refused here too**, rather than at the export that reads it
again: the gate cannot judge a style it cannot read, but *that* is the finding, and it carries the JSON path
the binder gave up at — a polymorphic material stated in the wrong shape names the field it is in
(`$.shell.storeys[1].deck`) instead of arriving as an unlabelled failure after the ground is laid.

**A part of the layout is a resource, and the whole layout is not the unit of an edit.**
`sketch/layers`, `sketch/groups`, `sketch/shapes`, `sketch/props`, `sketch/themes`, `sketch/relief`,
`sketch/room-styles` and `sketch/biome` each read and write one part at a time — the shape the objectives
already answer in (`POST /map/{slug}/wools`, `PATCH /map/{slug}/wools/{woolId}`) — so a caller moving one
shape does not send back every other shape, every theme and every prop. Two things follow from the routes
rather than from the edit.

**The address is the id the document already carries.** A shape id is unique across the whole stack, so a
shape answers to it wherever it is drawn; a group answers under the layer that carries it, because the fan
and the relief are both solved over the shapes of one slab; and a layer that named itself keeps its id while
one that did not answers under its position, which is the name the rasterizer gives it anyway. Nothing new is
minted to make a part addressable, which is what lets a caller read a board with `GET /sketch` and edit it
without a second vocabulary.

**A partial write answers for the whole document.** Each one runs the gate and the check `PUT /sketch` runs,
in the same two registers: a style or theme its own materials cannot honour **refuses** at 400, and
everything the document says that the build cannot is a **complaint** riding back on the 200. Anything less
would make the small route the way to get past the big route's gate.

**And a typed body is what publishes the model.** `PlacedProp` on the wire puts the six prop kinds, their
knobs and their recipes into `/api/openapi/v1.json` — where the dressing document had no field named at all
— and `TerrainTheme`, `SketchReliefJson`, `HouseStyle`, `BiomeField`, `SketchLayer`, `SketchGroup` and
`SketchShape` reach it from the sketch rather than only from a preview route or, in the biome's case, from
nowhere at all: every field of a stored layout now has a route that says what it holds.
That is where an agent looks before asking for something the studio cannot do, so a part with no address is
a part it has to learn from prose instead.

**The house half** reads `roomStyles.cage`, `roomStyles.spawn` and the shell of every building in
`dressing.props` off the document that is about to be written, and runs each through the same house-style gate
(`docs/tools/library.md`'s Refusals, rule ids `HS1`–`HS3`) — a block named for a geometric role that is not
that kind of block, a doorway that does not clear 2.5 blocks once its head is written in, or a roof whose own
materials are wrong for its pitch or its family. The cage and the spawn are checked identically: a stair
lattice or a slab band window is allowed on either, as it is on any house, so long as its block is the kind the
form needs. A placed building is checked identically for the same reason: its shell is a snapshot on the prop
rather than a reference to a library row, so the style stored here is the style the export stamps, and
`HouseProp.Check` reads only its wings and joints. Its findings name the prop —
`field` is `dressing.props[3].style.verge` and `subjects` carries the prop's own id.

**It is asked on every road a layout is stored through**, not only on this one: `PUT .../sketch/from-plan`
runs it over the merged document, and `POST /map/from-documents` over the layout it is handed. A gate wired to
one of the three is a gate two thirds of the maps in this repository never met.

Answers **400** `{error: "invalid style or theme", message, findings[]}` (`docs/refusals.md`), one finding
per fault, and writes nothing. A layout with no `roomStyles`, no buildings and no themes — or one whose
snapshot does not parse as a house style or a theme at all — is not this gate's business and saves as it
always did: only a well-formed style or theme that is wrong is refused.

**And a bound shell taller than the build ceiling is refused there too** (`WX10`,
`docs/world-export/structures.md`). A room's shell is authored geometry subject to no cap of its own, while
the goal marker over it hangs five blocks above a ceiling twenty over the ground — so a tall layer stack
swallows the very sign that says where the goal is. The height is measured on the smallest footprint a shell
can stand on, 6×6, since every sloped roof only climbs further on a bigger one: a style refused here has no
footprint it could have been stamped on. It rides in the same **400** envelope, `field` naming `roomStyles.cage` or
`roomStyles.spawn`.

**Finish refuses an empty board.** `POST .../sketch/finish` answers 422 `SK6` when there is no stored layout
at all, and 422 `SK7` when the layout rasterizes to no ground. It does *not* ask for two islands: an island is a connected
landmass rather than a side, and one continent both teams stand on is a common and correct shape. Symmetry
decides whether a board has two sides, and it is stated in the setup rather than counted in the ground.

**It declines a shape a layer cannot hold.** Two adds stacking over the same ground on one layer — a floor
with a roof drawn over it rather than beside it — build as the upper alone, over open air, because a layer
keeps one span per column and a taller add replaces a shorter outright, floor included. `SK9` names both
shapes and says which one is not in the world. It is a **decline** rather than a complaint: the board builds
and one thing the author drew is gone from it, which nothing else on the success would say. Two adds at one
floor are ordinary ground and stay silent, and so do walls clamped *around* a tucked-in floor — the way a
roofed gallery is actually built.

**It complains where two layers are driven into each other.** A layer states a `base_y` and a height and the
pair reads perfectly; only the rasterized spans say whether the air between the layers is there. Where a
lower layer's span reaches into an upper layer's, the two build as **one solid mass** there — the gap is not
in the world and nothing under the upper slab can be stood in — and `SK10` names both layers, how deep they
meet, the deepest column and how many they contest in all. It is a **complaint**: the board builds, and a
plinth two layers thick may be what was meant.

**One shared course is the seam, not a fault.** A layer spans `[base_y, base_y + height]` *inclusive*, so
setting the upper layer's `base_y` to the lower one's top — "the deck starts where the walls end", the
obvious gesture — shares exactly one block. `opus5-mineshaft` is authored that way, its walls meeting the
deck over **5,752 of 6,400 columns**, and complaining about it would be complaining about the coordinate
system rather than about the board. Two courses or more is a slab driven through another, and that is what
fires.

**And it complains where a raised mass has no way onto it.** `SK11` walks the board's own spans and reports any
standable mass, sixteen places or more, that **stands over other ground** and that nothing joins to the rest
of the board — with the count and the lowest place's coordinates, so it can be flown to.

**Both of those walks skip a made thing.** A `made` layer is out of `SK10`'s pair walk and out of `SK11`'s, so
a hull sunk into a hill and a dome on columns raise nothing. The rules read the ground and the decks over it,
which is what they are about.

Two things are deliberately silent. **A mass beside another is a landmass**, not a fault: two landmasses across
a void are how a board is normally drawn, and the build zone bridges them at the intent tier, which a sketch
does not state — so a mass sharing no column with any other says nothing. Measured, that discriminator is the
whole difference between a finding and noise: without it `thunderstorm`, a one-layer board of ordinary
landmasses, reports **eight**. And **ground under a roof** is a room, and a room with no door is the author's to
have, so only a mass with open sky over some of it is reported. What is left is something floating above
another thing with nothing between them — an upper level whose stair was never drawn, or a deliberate perch, and
only the author knows which, so it is a complaint and never a refusal.

**Joined means walked, not reached, and the bound is two blocks.** Unbounded, the walk finds a way onto every
exposed deck — a player carrying blocks pillars up to it and the walk prices that climb rather than refusing
it, which is the right answer to *can anyone get there* and the wrong one to *is there a way up*. So the flood
is bounded to the tallest step that counts as the ground joining itself, and that is **two**: the thinnest
slab the rasterizer builds is two blocks, so a layer seamed onto the one under it raises the standing surface
by exactly two. A smaller bound calls every stack detached — at one, `opus5-undercroft` reads as **41 masses
over 7,766 places**, and at two the same board is one. The bound cuts both ways: a cliff a player can only
drop off does not join its two sides.

**It refuses a shape that fills ground a subtract takes away, and complains where one draws nothing there.**
A subtract is how a board states its **negative space** — the void a plan's buffer pieces compile to, the hole
a composed footprint leaves — and **a hole is never scenery**: what a body encircles is ground players go
round, and a board's walls are drawn to guard it. So the negative space may be *redrawn* — the buffer rounded
off, narrowed, moved — and never papered over. An override add, or any add on **another layer** (a subtract
reaches only the layer it is on), puts the ground back and is **refused**, `SK13` — at `finish`, and there
alone. On the same layer
a plain add draws **nothing at all** instead, because the algebra is
`((adds − subs) ∪ override-adds) − override-subs` and a subtract beats every plain add whatever order the two
are written in — the shape is on the canvas and not in the world, which is the rule's other half and only
complains. Either way `SK13` names both shapes, which of the two happened, how many columns they contest and
the northmost of them.

**Where a refusal lands, and why nowhere earlier.** Every sketch-stage route — the write, the merge, and the
paint, dressing, relief and 3-D reads — takes a board whatever its geometry says and rides the findings back
on `warnings`, refusals included. `POST .../sketch/finish` is where the same check becomes fatal, which is the
stage that declares the drawing done, and `MapExportComposer` is the second gate behind it.

The reason is that **a sketch is a working document and an edit is not atomic.** Drawing a floor under a hole
and then removing the hole is an ordinary order to work in; so is the reverse. A store that refuses the
intermediate state does not prevent the board — it deletes the shapes the author drew to get there, and the
tool cannot even see that it did, a refused PUT being a completed round-trip that throws nothing. The Sketch
tool now reads the status and says **Not saved** with the server's own sentence when a write does not land.

The same rule is why the 3-D preview stays true. It draws what the board builds — the subtract wins, the add
it beats draws nothing, and that absence is exactly what the author needs to see — rather than going dark on
a document the rasterizer can answer for perfectly well. The set algebra never fails; only the gate did.

**It complains where a theme cannot show itself on the shape it was scoped to.** A theme picks its bucket per
column by whether that column is an edge, and a shape with no interior column — every one of its cells having
ground missing on at least one of its eight sides — is an edge everywhere under every `rimEdges` setting. So
the rim paints its top and the wall paints the rest, and the surface, the fill and whatever pattern they carry
are nowhere on it: a two-block stilt themed like the platform it stands on is a course of the rim material
over the wall material and nothing else. `SK23` names it, **grouped per layer and theme** rather than per
shape, because the decision that answers it is one — turn that theme's rim off, or say what those shapes are
made of with `material` — and a board drawn out of small pieces raises hundreds. Only where the theme's rim
actually paints: with it off the top falls to the surface and the theme shows as written, which is the honest
way to paint thin ground. It is one of the readings taken off the **rasterized spans**, so a partial write
names it among the rules it did not walk.

**And it refuses a shape that says twice what paints it.** `theme` and `material` answer one question at two
grains; a shape carrying both builds with the material, being the narrower statement, and the theme is read by
nothing. `SK24` is a refusal — the world is fine and it is the document that says two things.

**Two silences an override add can meet, and both are named.** An override add is what a made thing is drawn
as — a wall, a flight of stairs, a crop bed, a stepped mound — and it states two things at once: the column is
its own, and this is its top. Each can be taken away by something that raises no other finding.

A **relief** replaces the top of every column of its group, so an override add on a relieved group builds to
whatever the field solves and its stated top is nowhere in the world. Only a shape naming a `height_mode`
stands out of that field, and only a `relief_scope` keeps its ground out of the solve; carrying neither is
`SK14`, a complaint naming the shape, its group and the top it asked for. The board still builds — that is
exactly the problem, and a twenty-seven-course wall coming out level with the ground beside it is what it
looks like. A top has to have been stated to be discarded, so an override add carrying no `base_height`,
`floor` or `anchor_heights` is outside this: such a shape is a footprint holding a theme, and the ground the
relief solves under it is the ground it was drawn for — a scree apron over a swell is written exactly that
way.

A **theme** is scoped by area rather than by height, so where two override adds share a column the taller wins
the ground and the *smaller* wins the paint. Where the smaller is also the shorter, the world holds one
shape's blocks in another's material: a mound's outer ring crossing a wall leaves the wall standing to its own
courses and finished in the mound's paint, sides included. That is `SK15`, a complaint naming both shapes, both
themes, the columns they contest and the northmost. Two shapes at *one* height are a theme scoped to a patch,
which is what scoping is for, and are not this. **The images count**: a shape in a mirroring group stands on
the board once per axis of the orbit, and what a patch contests is as often another patch's reflection as the
patch itself — a dais laid clear of a court on the half it is drawn on lands in the middle of it on the other.

**And it complains where a made thing seats on nothing.** A seated layer takes its floors from the lowest
solid column under its own footprint, so a thing whose footprint covers no ground has nothing to measure
against and stays at the height it was drawn. `SK16` names the made thing and how many of its columns found
nothing beneath them. A complaint and never a refusal: a balloon, a ship in the air, a statue on a spire is a
legitimate board, and the way to say so is to take the layer's `seat` off.

**And it complains where a made thing stands in something built.** The two halves of a board are laid by
different passes that do not read each other. A made thing is the rasterizer's — it is drawn at the floor it
states and is in the world before anything is stamped — while a wool cage, a spawn cube, an objective and a
dressing-placed building all seat on the **terrain's** surface, which is every column's top with the made
things taken out. So a balloon drawn where a house is going, or a house placed under a balloon, is nobody's
error to catch: the blocks interleave in the columns they share, the later pass winning each cell it writes,
and what stands there is one inside the other. `SK18` names the made thing, what it is standing in, how many
columns they share and the first of them. It is read off the finished world's **provenance** rather than off
the document, because that is the one place all four passes have registered — `SK10` skips a made-thing layer
by design, a thing drawn over ground being no lost gap, and a stamped structure is not a layer at all. A
complaint, and deliberately not a refusal: a gantry over a shed or a hull in a dry dock is a board somebody
meant, and which of the two moves is the author's call. Raising the made thing is usually the smaller change,
since it is drawn at an absolute floor and has nothing seated on it.

**And it refuses a placement naming a recipe the document does not state.** A tree, a boulder and a building
each carry a `style` key into the layout's own `dressing.styles`, and a key the registry has no entry for names
nothing at all. Every *read* of the dressing refuses one already — the preview, the paint and the export each
answer `DR-DOC` naming the placement and the key — so what `SK19` adds is **when**: without it a document
written by a driver was stored and finished with two 200s and only said no at the export, the fault sitting in
the map in between. It is a refusal, so the **finish** stops on it (422); the **save** still stores the board
and reports it on `Pgm-Warnings`, because a save that fails halfway through authoring is worse than a board
carrying a fault someone is about to fix. A placement naming *nothing* is outside it: an empty key is a prop
put down before a recipe was picked, and it builds the kind's own default the way a sketch binding no room
style stamps the built-in shell.

**And it complains where a shape belongs to no group.** A group is the unit the symmetry orbit is fanned
by — the build reads each mirroring group's `shapeIds` and copies exactly those shapes onto their images — so
a shape no group lists is built once, on the side it was drawn on, with no image anywhere. `SK17` names the
shape and its layer. Nothing else says so, and every surface that could is looking elsewhere: the shape
rasterizes where the author put it, so the board is not missing it; and the canvas draws a group's **outline**,
which is the union of the ground the group fused rather than the shapes it lists, so a shape fused into the
outline but absent from the list is drawn mirrored and built unmirrored. The same list carries the group's
relief and its keep-clear fan, so an unlisted shape takes neither of those either. A layer stating no groups at
all is outside this — the whole of that layer mirrors — and so is a role-tagged room piece, which is never
listed by design.

**What separates that from a donut is the order, and only within one layer.** A body and the hole cut out of
it are written in that order — an exterior ring then its interior rings, a compiled footprint then the buffers
stating its negative space — so a subtract *following* an add on that add's own layer is its hole and says
nothing; without that reading every simplified group with a hole in it would report a fault. Across layers
the order carries nothing of the kind: a layer's place in the stack is a **height**, and a slab written first
is written `below`. So an add on another layer is a fill wherever it sits in the document.

**What separates a fill from a lid is the floor.** A layer holds one span per column, so an override add
resting *above* the subtract's own floor moves that single span up and records nothing beneath it — the void
the subtract states is still void, with a deck over it. Only an override add standing at or below the
subtract's floor puts the negative space back as ground, and `12-underpass` is the worked example of both: a
deck at `floor: 13` bridges the cut, and the same deck with `floor` left unset refills it bedrock to grass.
The reading is per layer, since a floor is measured from its own layer's `base_y`.

**And, on a board drawn from a plan, it re-reads the strait.** `CT12` is a plan rule — the direct crossing
between the two team islands of a two-team wool board wants 15–40 blocks — and the plan measures it over
rectangles, before a shape exists. The finish measures the same pairs again over the rasterized footprint and
complains where the drawn board has moved one out of band, carrying both numbers. The pairs come from the
plan, never from the raster: which crossing is the strait is a fact about roles and build regions that a
footprint does not carry. It says nothing for a board with no plan stored beside it. `docs/tools/plan.md`
§ *What a compile produces*.

**And it complains about a board with no finish at all.** `SK8` rides on the success when the stored layout
carries **no theme registry, no relief and no props** — the finding names which of the three are absent. Ground
alone is a legitimate board, so this stops nothing; what it stops is the board shipping unremarked. Every other
gate here needs something stated to disagree with — `SK3` a shape citing a theme the layout does not carry,
`SK4` a shape drawing nothing, `SK7` a layout rasterizing to no ground — so a board stating none of it slips
between all three and exports as raw stone with every stage answering 200. It is asked at the finish rather
than at the `PUT`, for the reason `SK6` and `SK7` are: a board mid-draw has every right to be bare, and only
finishing declares the drawing done.

**A recompile refuses to orphan a relief.** `PUT .../sketch/from-plan` answers **409** in the refusal envelope
(`docs/refusals.md`) — one `SK1` finding per group the new geometry has no home for, the group id riding as
the finding's subject — and writes nothing. Group identity is derived from the geometry, so a recompile that
re-fuses the board does not merely move a group — it produces a different one, and terrain authored against
the old fusion has nowhere correct to land. `?force=true` accepts the loss and proceeds, which is the author's
call and not the server's.

**And a relief in the posted body loses to the stored one, which the same route now says out loud.** The carry
is what the route is for — a compiled layout carries no relief, because a plan cannot express one, so the
stored relief is the only one there is — but a caller that compiled, patched a relief onto the result and
posted it is on the road this route documents, and for that caller the stored terrain wins in silence. Worse
than silent: `POST .../sketch/relief/read` measures the layout in the *request body* and reports the new
numbers, while `GET .../render/heightmap` builds the *stored* document and draws the old ground, so an
iteration loop watching the readback sees its edits land and one watching the render does not. Each group
whose posted relief is not the stored one now rides back as an `SK1` complaint on the 200, naming the group
and the route that writes it: `PUT .../sketch/relief/{groupId}`.

**A building refuses a footprint it cannot stamp** and **a marker refuses the void**, as above. A footprint is
unstampable for its own size — a wing under 3 × 3, or a plan covering more than 192 blocks — or because its
wings make no building, which is the joint model's five rules: `HJ1` two rectangles sharing blocks, `HJ2` a
touch over part of an edge only, `HJ3` both ridges along the shared edge, `HJ4` both into it, `HJ5` a wing
standing taller than the hall it meets. `HouseProp.Fault()` answers the id and a sentence in the terms the
rectangles were drawn in; `Plan()` answers null for the same plans, so a stamp gets the plan or nothing.
The refusal is the prop's own and never the stamper's — a wool cage and a spawn cube go through the same
`HouseStamper` from a plan piece's geometry, which no dressing limit has any business judging.

**A board too large to realize is refused** (`SK2`, **422** `the board cannot be built as drawn`). A board costs one walk per
column of its **extent**, drawn or not, so a 4000×4000 board does not fail slowly — it takes the machine with
it. The extent is measured across the symmetry orbit, because a shape far out on one side widens the board by
twice its distance. **The ceiling itself is not published** — not in the finding, not in the rule's own
sentence, not here: a stated ceiling is a target, and an agent told what it may draw up to will draw up to it.
What the refusal carries is the span it measured, which is the half an author acts on; an authored board is a
few hundred columns a side and nowhere near it. Asked where the layout is stored, where it is previewed, and
in the export's shared sketch leg, so a headless driver meets the same measure the studio does.

**What the document names and does not have is said rather than swallowed.** The rasterizer is set algebra
over shapes, so a shape it cannot read contributes no ground instead of failing — which means a defect in the
document reads exactly like a smaller drawing. `SketchLayoutCheck` says so, as complaints riding on the
success under `warnings` — on **every** surface that runs it, the two write paths and the four reads alike,
because the complaints are carried by the pipeline rather than by the endpoint (`docs/refusals.md`, *What a
success carries*). Both writes run the gate over the merged document rather than the posted one, since the
merge is the road a headless driver takes: `SK3` for a name that matches nothing — a
shape kind nobody has, a **mirror mode** nobody has (which fans the board onto itself, so a map stating two
halves stands on one), a group listing a shape id the layout does not carry, a relief keyed to a group
that does not exist, and a **theme** the registry does not carry, on a shape or as the map default (which
paints those cells unthemed stone and is otherwise the quietest fault a finish has — one reported per name
rather than one per shape) — `SK4` for a shape that
draws no ground (a polygon under three vertices *or of no area, every point on one line*, a circle or polyline of
no width, a rectangle of no area), and
`SK5` for a column the world cannot hold. Each carries the document path that named nothing in its `field`
and the shape's id as its subject.

**A part of the document nothing read is said the same way.** `SK3` covers a name that resolves to nothing;
the layer under it is a *field* that resolves to nothing — a rectangle keyed `x`/`z`/`w`/`h` instead of
`min_x`/`min_z`/`max_x`/`max_z`, a `relief` written one level too deep — which the deserializer drops before
any gate can see it, leaving a board that covers no ground under a success. Both writes answer it as
`RQ3` complaints naming the dotted path (`layers[0].layout.shapes[0].x`), over the body **as posted** rather
than the merged one, since that is the document the caller can correct. The reading and its four exemptions
are `docs/refusals.md`, *`RQ3`*.

**Heights are clamped rather than refused**: a shape is never thinner than one block and its floor never dips
below zero, whatever is asked for — and since the clamp means what stands is not what was asked for, `SK5`
says so on the way past.

And two things are silently **dropped on load** rather than carried: a prop whose kind the client does not
know, and a relief mark whose kind it cannot draw. A shape nothing can edit is worse than an absence.

**A write reads the document; a read walks the ground.** Seven of the rules above — `SK9`, `SK10`, `SK11`,
`SK13`, `SK14`, `SK15`, `SK16` — are answered off the **rasterized spans** rather than off the JSON: what
stacks over what, what a layer's slab drives into, what is standable and unreached. Answering them walks every
column of the board's extent, which on a played-size board is seconds. So a **partial write** — a shape, a
vertex, a layer, a group, a prop, a theme — takes the document reading and leaves those seven for a read that
asks: on `opus5-millrace` (274×268 columns, 312 shapes) that is **48 ms a moved vertex against 1,291 ms**, and
the nine calls that reshape a compiled rectangle drop from twelve seconds to under one.

What a write leaves out it **names**, on its own header. `Pgm-Unwalked: SK9 SK10 SK11 SK13 SK14 SK15 SK16` is
on every partial write, and it is deliberately not folded into `Pgm-Warnings`: that key means *these were
found* and its absence means *nothing was*, which is the one rule that makes it readable, so a rule that was
never asked cannot ride there. `GET /map/{slug}/findings` walks the ground and answers all of them, and so
does the **finish**, which is where a board carrying one is stopped. `SK2` is outside the split and answers
under either reading — a board too large to realize is measured off the shapes' own boxes, and must refuse
before anything walks a column of it.

## The API

Every endpoint is anonymous and rooted at `/api`.

**The map's layout**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /sketch` | `{name?, width?, depth?, mode?, centerX?, centerZ?}` | `{slug}` — a `map` row at `stage=sketch`, whose layout is a **ground layer** at `base_y` 0 under the id `ground`. A frame seeds the `setup` beside it; without one the editor uses its 120×80 `rot_180` default. A board is a stack and a flat one is a stack of one, so the ground is written here rather than invented by whichever surface draws on the board first | — |
| `GET /map/{slug}/sketch` | — | the stored layout, or `{}` | 404 |
| `GET /map/{slug}/sketch` | — | the stored layout, or `{}`. The `ETag` is the revision to state on the next write | 404 |
| `PUT /map/{slug}/sketch` | the layout | `{}` — a **verbatim replace**, which is what makes a deletion stick; `warnings` rides beside it where the document names something it does not have (`SK3`/`SK4`/`SK5`) or carries a field the reader has nowhere to keep (`RQ3`). **The board's own geometry never refuses this write**: a drawing in progress is stored whatever it says, and every finding it raises rides back on `warnings`, `SK13` included. The `ETag` is the revision it landed at | 400 non-JSON, or 400 `{findings}` on a bound room style the house-style gate refuses · **409 `RQ5`** an `If-Match` naming a revision the layout is no longer at · 404 |
| `PUT /map/{slug}/sketch/from-plan` | a compiled layout | `{orphaned}` — merges the finish, the relief and any author-corrected structural height onto fresh geometry, and answers the same `SK3`/`SK4`/`SK5` complaints the plain write does, over the merged document. The merged board's geometry rides back on `warnings` too, rather than refusing the merge, and so does one `SK1` per group whose **posted** relief the carry replaced with the stored one | 409 `{findings}` one `SK1` per orphaned group (`?force=true`) · 400 · 404 |
| `POST /map/{slug}/sketch/finish` | — | `{slug, configureUrl}` — rasterizes to world geometry, moves the map to `stage=configure`. It runs the document gate over the stored layout, so the stage that declares the drawing done is also the last one to say what will not be built, and — where a plan is stored beside it — re-reads that plan's CTW strait over the drawn ground (`CT12`) | 422 `SK6` nothing stored · 422 `SK7` nothing drawn · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 404 |
| `DELETE /map/{slug}/sketch/discard-if-empty` | — | `{discarded}` — drops a draft still at its default name with no authors and nothing drawn | — |

**Previews over a live layout.** All four take the working document as the body rather than reading the stored
blob, so they track unsaved edits, and all four answer 400 rather than 500 on a layout they cannot process.
All six run the document gate, so all six answer `SK3`/`SK4`/`SK5` under `warnings` beside whatever else they
carry — the board an author is looking at is the one place those complaints are worth reading.

| Endpoint | Answers |
|---|---|
| `POST /map/{slug}/sketch/paint` | the painted surface as palette-indexed block pixels — the real painter's output, with team tints resolved from the stored intent |
| `POST /map/{slug}/sketch/relief[?interval=]` | `{interval, groups[]}` — per group its height range, its bounds and its traced contour lines, from the build's own solver |
| `POST /map/{slug}/sketch/relief/read` | `{groups[]}` — per group the cell count, low/high/relief, steps, tiers, the first twelve faces and the total, cliffs, crossings in X and Z, the symmetry error, the `landform` it measures as beside the `smoothing` it kept, the `seams` where two of its marks meet on a step, and the `silentMarks` that pinned nothing. Carries `RL1` where the group states a different word, `RL2` where it carries elevation it never graded (`docs/world-export/relief.md` §6.1), `RL3` where a seam is taller than a scramble and `RL4` for a mark that landed nowhere (§2.0) |
| `POST /map/{slug}/sketch/columns` | `{palette, cols, layers, min_x, min_z, max_x, max_z}` — the whole built world as per-column runs, which the 3-D preview meshes. `cols` is one flat array walked as `[x, z, runCount, (yTop, yBottom, paletteIndex, layerIndex) × runCount, …]`, and `layerIndex` is into `layers` or `-1` for a run no layer accounts for; its `warnings` carries every prop the dressing pass declined (`DR-*`) as well, at severity `decline`: the world built and those things are not in it | 400 `RQ1` a body that is not a layout · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/dressing` | `{props[], declines[], claimedCells, claims}` — what the dressing pass would place, run and stopped before anything is written: per prop the columns it covers, where it rests and the height it resolved to, and every prop that did not land as its `DR-*` finding. `claims` is `{bounds, width, height, classes[], rows[]}`, digit rows over the board's own ground the way `coverage`'s own classes are, classing every cell as a prop's own claim, a goal's clearance, a keep-out, or free — so a candidate site is looked up on the raster rather than tried and read back as a decline. `?format=text` answers the same reading as characters, with the classes' key, a column-index line, the declines and a `placed n, declined n` line under it | 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/seats[?kind=&width=&depth=]` | `{bounds, width, height, kind, standoff, footprintWidth, footprintDepth, rows[], seats, refused[]}` — where a prop of that kind and footprint **may** stand, which the `DR-*` declines only ever answer backwards: `1` where a box of `width`×`depth` blocks seats with its minimum corner on that cell, `0` where it does not, a space off the board, and `refused` the tally of which rule turned the rest away. `kind` is one of the document's own prop kinds (absent: `tree`) and decides the route standoff; one number asks about a square. `?format=text` answers the same mask with its key and the tally under it | 422 `no such prop kind` `RQ4` · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/probe-footprint` | `{cells, land, void, hole, voidCells[], holeCells[]}` — what a ring stands on, against the **rasterised** footprint rather than a model of the coast rebuilt outside the studio. The ring need not be a shape the layout carries, which is the point: it is asked before one is built on it. Body `{layout, ring}` | 422 `ring too short` · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 404 |

**The parts, one at a time.** Each of these reads and writes one part of the stored layout without the
caller holding the rest of it, and each answers that part's own type — which is what puts the model in the
published schema rather than in this document. Every write runs the gate and the check `PUT /sketch` runs,
in the same two registers.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/sketch/layers` | the stack in draw order, each layer with the shapes and groups drawn on it, typed as `SketchLayer` so a shape's fields, a layer's stacking words and a group's orbit flag are all in the published schema. A layer that named itself keeps its id; one that did not is answered under its position, which is the id every other route addresses it by | 404 |
| `GET /map/{slug}/sketch/layers/{layerId}` | one `SketchLayer` | 404 the id names no layer |
| `PUT /map/{slug}/sketch/layers/{layerId}` | `{id}` — state one layer: the height its ground starts at, whether it is terrain or a made thing, and how its floors meet the ground. Creates it at the end of the stack where the id names none. **Stating `layout` replaces the layer's shapes and groups outright; leaving it out keeps them**, because a shape has a route of its own and renaming a layer is not asking to rub its drawing out | 400 `malformed body` `RQ1` · 409 · 404 |
| `DELETE /map/{slug}/sketch/layers/{layerId}` | `{id}` — take one layer, everything drawn on it, and the relief of every group that lived only there off the board | 409 · 404 the id names no layer |
| `GET /map/{slug}/sketch/layers/{layerId}/shapes` | the shapes drawn on one layer, in draw order | 404 the id names no layer |
| `POST /map/{slug}/sketch/layers/{layerId}/shapes[?group=]` | `{id}` — draw one shape on a layer. `?group=` names the ground it joins, and a name the layer does not carry yet opens a group; **a layer that already groups its shapes takes no shape that names none**, since the orbit fan and the relief are both read off a group's list and an ungrouped shape on a grouped layer is built once, where it was drawn, on flat ground (`SK17`). A body stating a free id keeps it; one stating none, or one already drawn, is minted `{type}-{n}` | 400 `the edit cannot be made` `RQ1` naming no group · 400 `malformed body` `RQ1` · 409 · 404 the id names no layer |
| `GET /map/{slug}/sketch/groups` | every group the board carries, across all its layers — `{layer, id, name, mirrors, shapeIds, hasRelief}` each. The list a caller reads before naming a group to draw into or keying a relief over | 404 |
| `PUT /map/{slug}/sketch/layers/{layerId}/groups/{groupId}` | `{id}` — state one group whole: what it is called, whether it is fanned onto the symmetry orbit, and which shapes share its ground. Creates it where the layer carries none under that id | 400 `malformed body` `RQ1` · 409 · 404 the id names no layer |
| `DELETE /map/{slug}/sketch/layers/{layerId}/groups/{groupId}` | `{id}` — ungroup. The shapes stay on the layer and are drawn where they were drawn; what goes with the group is the orbit fan and the relief keyed under its id | 409 · 404 the id names no group |
| `GET /map/{slug}/sketch/shapes/{shapeId}` | one `SketchShape`, wherever on the stack it is drawn — a shape id is unique across the whole document, so it is the address wherever it lives | 404 the id names no shape |
| `PATCH /map/{slug}/sketch/shapes/{shapeId}` | `{id}` — change one shape without restating the board. A stated field replaces what the shape carried and **a stated `null` takes the field off**, so the one call both writes a height and clears a relief scope. `id` is the address and is kept whatever the body says | 400 `the edit cannot be made` `RQ1` on `role`, `intentRef` or `height_authored` · 400 `malformed body` `RQ1` · 409 · 404 the id names no shape |
| `POST /map/{slug}/sketch/shapes/{shapeId}/bend` | `{id, vertices, held}` — redraw one outline as a **coast**: resampled along its long edges every `step` blocks, each inserted point pulled off its edge by up to `wander`, and Bézier handles fitted over the result. Body `{wander, step, seed, tension?, side?}`. **The outline's own vertices never move**, so a corner stays where the plan put it and the neck a spur hangs off keeps its width. `side` is `out` (the default — the slight bloat that reads as land), `in` (keeps the plan's footprint, for a board whose shapes abut on a measured strait) or `both` (wanders across the line the plan drew). The side is decided by asking the ring rather than by reading a winding, so it is right for either winding and for a concave stretch. `held` counts the points that had no room on the side asked for and stayed where they were cut, which rides back as `SK21` | 400 `the edit cannot be made` `RQ1` — a `role` shape (a room's rectangle is not a coast), no `vertices` to resample, a wander that folds the outline across its own far side, or a `wander`/`step` of nought · 409 · 404 the id names no shape |
| `PATCH /map/{slug}/sketch/shapes/{shapeId}/vertices/{index}` | `{id, index, vertices}` — move one point of one outline. Body `{x, z}`. **Every other vertex stays exactly where it was drawn**, which is the whole of the call: a board's shapes abut, and an edit that drags a ring's other points opens ground between two that were flush | 400 `the edit cannot be made` `RQ1` — a `role` shape, no `vertices` to address, an index the outline does not carry (the message states the range), or a move that folds the ring · 409 · 404 the id names no shape |
| `POST /map/{slug}/sketch/shapes/{shapeId}/vertices` | `{id, index, vertices}` — add one point after the vertex `after` names, and answer where it landed. Body `{after, x?, z?}`; stating no point puts it at the **midpoint of that edge**, which is a new corner half way along a wall with nothing else moved. The last vertex's edge closes the ring | 400 as above · 409 · 404 the id names no shape |
| `DELETE /map/{slug}/sketch/shapes/{shapeId}/vertices/{index}` | `{id, index, vertices}` — take one point out, leaving every other where it was drawn | 400 `the edit cannot be made` `RQ1` — as above, plus an outline down to its last three, since two points draw no ground · 409 · 404 the id names no shape |
| `DELETE /map/{slug}/sketch/shapes/{shapeId}` | `{id}` — rub one shape out, and take it out of every group that listed it | 409 · 404 the id names no shape |
| `GET /map/{slug}/sketch/props` | `{props[], styles{}}` — every placement the map carries and the recipes they name, typed as `PlacedProp` so the six kinds, their knobs and their styles are in the published schema. The recipes ride with the placements because a placement naming a key nobody can resolve is not readable on its own | 400 `unreadable dressing` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/props` | `{id}` — place one prop, without sending the board it stands on. A body stating a free id keeps it; one stating none, or one already taken, is minted `{kind}-{n}`. The placement goes on the end, since the pass runs in placement order and an addition has not been placed before anything | 400 `malformed prop` `RQ1` (the message names every kind) · 400 `invalid style or theme` `HS*`/`PT*` · 409 stale `If-Match` · 404 |
| `PATCH /map/{slug}/sketch/props/{propId}` | `{id}` — replace one placement, keeping its position in the pass's order and the id it is addressed by. Editing a prop must not move it past what the pass places after it | 400 as above · 409 · **404 the id names no placement** |
| `DELETE /map/{slug}/sketch/props/{propId}` | `{id}` — take one placement off the board. The recipe it named stays in the registry, since a key is shared by every placement wearing it | 409 · **404 the id names no placement** |
| `GET /map/{slug}/sketch/themes` | `{themes{}, mapTheme}` — the registry by the id an author registered each theme under, and which of it covers every cell no shape scope claims. A registry entry the painter cannot read as a theme is left out, the same way the painter drops it | 404 |
| `GET /map/{slug}/sketch/themes/{themeId}` | one `TerrainTheme`, as the painter reads it | 404 the registry carries no such id |
| `PUT /map/{slug}/sketch/themes/{themeId}` | `{id}` — register a theme under an id, replacing whatever that id carried. The one write in the sketch that creates and replaces through the same verb, because a registry entry is addressed by the name an author gave it | 400 `malformed theme` `RQ1` · 400 `invalid style or theme` `PT*` · 409 stale `If-Match` · 404 |
| `DELETE /map/{slug}/sketch/themes/{themeId}` | `{id}` — take a theme out of the registry. **It does not refuse over what still names the id**: a shape painting with a theme the registry stopped carrying takes the map default, and the map default naming one takes unthemed stone, both already `SK3` complaints on the stored document. They ride back on this write | 409 · 404 the registry carries no such id |
| `PUT /map/{slug}/sketch/map-theme` | `{id}` — which registered theme covers every cell no shape's own scope claims. Body `{"theme": "<id>"}`; a null or absent theme clears it, which paints unthemed stone. Naming a theme the registry does not carry is stored and complained about (`SK3`) rather than refused | 400 `malformed request` `RQ1` · 409 · 404 |
| `GET /map/{slug}/sketch/relief` | every group's relief, by the group id it is solved over — a group rather than a shape, because a relief solved per shape leaves a seam wherever two of them meet and disagree about the height they share | 404 |
| `GET /map/{slug}/sketch/relief/{groupId}` | one `SketchReliefJson` | 404 the layout states none for that group, which is every group as flat as its shapes drew it |
| `PUT /map/{slug}/sketch/relief/{groupId}` | `{id}` — state one group's interior elevation, replacing whatever that group carried. **It does not check the group exists**: whether the id still names a fusion is `SK1`'s question on the compile path, where losing hand-authored terrain is the risk worth refusing over, and answering it here would refuse a relief written before the geometry it belongs to | 400 `malformed relief` `RQ1` · 409 · 404 |
| `DELETE /map/{slug}/sketch/relief/{groupId}` | `{id}` — take one group's relief off the board, leaving its ground as flat as the shapes drew it | 409 · 404 |
| `GET /map/{slug}/sketch/room-styles` | `{cage, spawn}` — both shells **resolved**, which is what the stampers will read: a part that is absent answers its built-in shell and a part bound to open ground answers null. Raw snapshots would not say which of the three states a caller is in | 404 |
| `PUT /map/{slug}/sketch/room-styles/{part}` | `{id}` — bind the shell one kind of room is stamped in; `part` is `cage` or `spawn`. **A body of literal `null` is a statement, not an omission**: it asks for open ground, a pad rather than a building over it, which is what a spawn on a plateau the plan already shaped often wants to be | 400 `unknown room part` / `malformed room style` `RQ1` · 400 `invalid style or theme` `HS*` · 409 · 404 |
| `DELETE /map/{slug}/sketch/room-styles/{part}` | `{id}` — unbind, which puts that kind of room back to its **built-in** shell. Not the same as binding null | 409 · 404 nothing is bound |
| `GET /map/{slug}/sketch/biome` | one `BiomeField` — `solid`, `cell` or `noise` | 400 `unreadable biome` `RQ1` · 404 the board states none, which is plains everywhere |
| `PUT /map/{slug}/sketch/biome` | `{id}` — which biome each column of the exported world carries. Map-wide and answered per chunk, because a biome's tint is blended across a radius and a region drawn to a finer edge never reaches its own colour there | 400 `malformed biome` `RQ1` · 409 · 404 |
| `DELETE /map/{slug}/sketch/biome` | `{id}` — take the field off the board, which is plains everywhere | 409 · 404 |

**A placement is looked up rather than tried, and `seats` is the half that says where.** The `claims` raster
answers what holds every cell; `seats` runs the pass's own five seat rules forwards over the whole board for
one kind and one footprint, so finding a spot for a tree is one call rather than a preview pass per guess.
Two things keep the answer the pass's own. The standoff is the kind's — a tree keeps three blocks off a
route, a boulder two — and a claim only refuses a kind that places at or before the claimant's own turn, so
a bed of flora does not stop a tree while a tree stops the flora (`docs/world-export/decoration.md` § 8). And
a building gets a seat rather than a verdict: `DR-PASS`, `DR-CROSS`, `DR-WAY` and `DR-SLOPE` read the built
world, and the pass still raises them.

```json POST /api/map/{slug}/sketch/seats?kind=tree&width=3
{"setup": {"mirror_mode": "none", "center": {"cx": 0, "cz": 0}},
 "layers": [{"id": "ground", "base_y": 0, "layout": {
    "shapes": [{"id": "a", "type": "rectangle", "operation": "add",
                "min_x": -30, "max_x": 30, "min_z": -30, "max_z": 30, "base_height": 10}],
    "groups": [{"id": "i1", "name": "Ground", "mirrors": false, "shapeIds": ["a"]}]}}],
 "dressing": {"props": [{"kind": "stroke", "id": "p1", "points": [[-20, 0], [20, 0]], "radius": 2, "seed": 5}]}}
```

**A ring can be asked about before a shape is built on it.** `probe-footprint` measures against the
**rasterised** footprint — the only coast that decides anything. A model of it rebuilt from the compiled
shapes and their own polygons disagrees by a cell or two, because a ring faceted to sixty-four points is not
the cells that ring covers, and a cell or two is the whole failure: a `raise` with no ground under it reads no
terrain, falls back to the shape's own `floor` and stands a stub of cobble in open void, which nothing
declines because a shape is terrain and terrain over void is a spur.

The third answer is the one that needs saying. A cell is **land** where the footprint has it and **void**
where it does not; a **hole** is a void cell the footprint *encloses* — a hub's two slots, a U-shaped wool
room's notch. Those are made by **arrangement**, so no region marks one, and an add-shape dropped on one fills
in the gap the layout was composed to have with nothing declined. `Cells.EnclosedVoid` is what names them,
which is the same derivation `CT8` counts enclosed voids with, so the probe and the rule cannot disagree about
what a hole is.

**The column payload** is one flat integer array walked by its own counts:
`cols = [x, z, runCount, (yTop, yBottom, paletteIndex, layerIndex) × runCount, …]`, with `palette` a list of
`#rrggbb` and `layers` the layer ids `layerIndex` points into (`-1` where no layer accounts for the run).
A run is a span that is solid throughout and one material throughout, listed top first, and `yTop`/`yBottom`
are both inclusive. Air is never sent, and a column holding nothing is absent rather than empty. One structure
answers three questions, because a run boundary is where a solid span ends *and* where the material changes:
a sky bridge over ground keeps both its segments, a wall reports its own courses instead of the surface colour
smeared down it, and water is a run like any other. The client decides what is *visible* — only it knows where
the camera is — so nothing is culled here. A board of forty thousand columns runs about a megabyte.

A map begun in Sketch has no intent, and an empty one is the right answer rather than a gap: it states no
objectives, so a preview showing none is showing what is there. Once a goal is placed, a prop standing in its
clearance is `OB19`'s — the pass declines it and this preview carries the finding under `warnings` like every
other drop, so it needs no rule of its own.

**The finish libraries**, all map-independent and all `library.md`'s to describe. `/styles` and `/themes` are
the terrain-paint library a theme is copied from or saved to; `/room-styles` is the shell library the Theme
phase binds from, with `/room-styles/{id}/json` for the stamper's own form; `/roof-styles`, `/storey-styles` and
`/porch-styles` are the parts a room style is composed from. `/terrain/blocks` is the
block palette, and `/terrain/material-preview`, `/terrain/theme-preview`, `/terrain/theme-map-preview` and
`/terrain/prop-preview` render what an edit will look like; `/terrain/stroke-styles`, `/terrain/water-forms`,
`/terrain/boulder-forms`, `/terrain/species` and `/terrain/woods` are the dressing vocabularies.

## Driving it without the UI

A sketch is one document, so an agent writes it and puts it. The loop is three calls.

```
POST   /api/sketch                    {"name": "Voidwatch", "width": 160, "depth": 100}
PUT    /api/map/voidwatch/sketch      <the layout document>
POST   /api/map/voidwatch/sketch/finish
```

A map that already came from a plan needs only the last two — the layout is already there, and `GET
/api/map/{slug}/sketch` returns it to be edited and put back. Note the difference between the two write paths:
the plain `PUT` replaces the blob, so a document that omits `themes` deletes them, while
`.../sketch/from-plan` merges the finish onto fresh geometry.

### Reshaping ground the plan compiled

A plan compiles to a staircase of rectangles. That is the board's *arrangement* — which ground is where, at
what height, next to what — and it is not the board's *shape*. Turning the one into the other is the work this
tool exists for, and there are two ways to do it that do not involve redrawing the ring by hand.

**One point at a time is the primary one, and it is what a hand does.** `PATCH
.../sketch/shapes/{id}/vertices/{index}` moves the vertex it names and nothing else; `POST
.../sketch/shapes/{id}/vertices` adds one after the vertex `after` names, at the midpoint of that edge when
the body states no `x`/`z`, and answers the index it landed at; `DELETE .../vertices/{index}` takes one out.
Every other point of the outline is exactly where it was drawn after each of the three. That is the whole
property: a board's shapes abut, and an edit that drags a ring's other points opens ground between two that
were flush — which is what happens when a whole-ring transform is used to pull one corner.

The loop an agent runs is: read the outline, pick the edge the new corner belongs to, state where it goes.
`after` and `x`/`z` in one call is the form to reach for, because it is atomic — a point that would fold the
ring leaves the outline untouched, where splitting first and moving second leaves the midpoint behind.
Omitting `x`/`z` is the other case, and it is the midpoint anchor: a corner half way along a wall, placed
before it is decided where it goes.

```
GET    /api/map/{slug}/sketch/shapes/island-12
       →  {"vertices":[[-100,-60],[100,-60],[100,60],[-100,60]], …}
POST   /api/map/{slug}/sketch/shapes/island-12/vertices   {"after": 0, "x": -40, "z": -84}
       →  {"id":"island-12","index":1,"vertices":5}
PATCH  /api/map/{slug}/sketch/shapes/island-12/vertices/1 {"x": -44, "z": -80}
       →  {"id":"island-12","index":1,"vertices":5}
```

Nine such calls take a one-piece plan's compiled rectangle — `island-12`, 4 vertices, 24,000 blocks² — to a
12-point outline of 28,084, **+17%**, with all four of the compile's own corners still exactly where the plan
put them. That is the whole workflow: a small plan for the arrangement, and the shape drawn on top of it.

Three things are refused, and all three for the same reason — the edit would leave a shape nothing can build
from. A `role` shape is the plan's own room rectangle, which a recompile redraws and a stamper seats a
building on. A rectangle or a circle states its bounds rather than an outline and has no points to address. And
a move, an insert or a delete that folds the ring across its own far side is refused rather than clamped,
because a folded outline rasterizes as ground with a hole nobody drew. An index the outline does not carry is
refused with the range it does, since a caller acting on a stale copy needs to know how long the ring is now.

**The bend is the second way, and it is a roughener rather than a resizer.** `POST .../shapes/{id}/bend`
resamples the outline's long edges every `step` blocks and pulls each inserted point off its edge by up to
`wander`, then fits Bézier handles over the result. The outline's own vertices never move, so a corner stays
where the plan put it and the neck a spur hangs off keeps its width. `side` says which way the cut points go:
`out` — the default — bloats the outline slightly, which is what makes a compiled rectangle read as land;
`in` keeps the plan's footprint, which is what a board whose shapes abut on a measured strait asks for; `both`
wanders across the line the plan drew, with the reach falling to nothing where the side turns over.

**Which of the two to reach for is not a preference.** A bend moves every cut point on the ring at once by a
formula, so it is right where the whole edge should read rougher and wrong where one place should be
different from the others. Pulling a bay, widening one flank, cutting a notch a lane runs through — those are
one point each, and doing them with a bend produces a board that is uniformly wobbly and locally unchanged.

The scale a hand actually works at is worth stating, because it is larger than a bend's. `rockymine-map-experiment`
is the reference: its four ground shapes are the plan's four rectangles reshaped by hand, from 4 vertices each
to 6, 9, 10 and 11, and **every one of them grew** — 3850 → 3920, 5500 → 6351, 3325 → 3962, 1575 → 1774, which
is +1,758 blocks² or +12.3% over the compile. Of the 36 drawn vertices, 19 sit outside the rectangle they came
from, by 2 to 20 blocks, 7 sit inside it by 4 to 8, and 10 stay on the edge. The document carries **no Bézier
handles at all**. A board reshaped that far outward, that unevenly, is not reachable by any whole-ring
transform, and it is reachable one point at a time.

### Gauging the result

Everything below runs on the **working layout posted as the body**, so a sketch can be checked before it is
saved and without a browser. What matters for an agent is which of them answer in numbers, which answer in a
drawing, and which answer in a raster it can actually open.

**Four read the sketch itself.** `POST .../sketch/paint` runs the real painter and answers the surface as
palette-indexed runs — the exact colour of every footprint cell, which is how a Voronoi reads as its cells
rather than as an average. `POST .../sketch/relief[?interval=]` answers the traced contour lines per group
from the build's own solver, as flat `[x, z, x, z, …]` runs. `POST .../sketch/relief/read` answers the terrain
in **numbers**: per group the cell count, low, high and relief, the step count and tiers, the faces with
cliffs qualified, crossings measured in both directions, and the symmetry error. That last one is the one to
reach for first, because it is the only preview that says whether terrain is any *good* without an eye — it is
what makes a relief correctable by a generator. `POST .../sketch/columns` answers the whole built world as
per-column runs; it is the heaviest of the four (it builds the map) and the only one that reports what stands
*above* the surface, so it is the read for asking what a structure or a marker actually occupies.

**The finish previews draw, in SVG — or as PNG on request.** `POST /terrain/material-preview` and
`/terrain/theme-preview` answer a material and a theme as they will paint — the theme as a cut-open sample
plateau plus a top-down swatch per bucket. `POST /terrain/prop-preview` and the five card sets
(`/terrain/stroke-styles`, `/water-forms`, `/boulder-forms`, `/species`, `/woods`) answer a prop as it will be
built. `POST /room-styles/preview`, its `-snapshot` twin, and `/roof-styles/preview`, `/storey-styles/preview`,
`/porch-styles/preview` answer a building in plan, section, isometric and cutaway. The default is **SVG text
inside JSON**, which the client renders inline — and every one of them also answers
**`?format=png&view=…`** with one named view as raw `image/png` bytes, the form an agent saves and looks at:
`view=plan|section` on `material-preview`, `prop-preview` and the **room-style previews**,
`view=section|rim|surface|wall|fill` on `theme-preview`. Both encodings come off one `CellRaster` per
picture, so they cannot disagree; a view name an endpoint does not have is a 400 naming the ones it does.

A building's **isometric and cutaway stay SVG**, and the reason is the picture rather than the plumbing: both
draw a block as its own shape rather than as a filled cell — a stair lattice's whole trick is the quarter each
of its four stairs is missing, and a renderer that fills the cell shows that window as a solid patch. There is
no raster to encode, so those two views are refused by name. The plan and the section are cell rasters and
answer either way, which is what matters: a building is looked at **in section** before it stands on a map,
and an agent that can only open a raster could not.

**After Finish**, the map holds rasterized world geometry and three more reads open up:
`GET /map/{slug}/top-surface` for the per-column surface colours, `GET /map/{slug}/segments` and
`GET /map/{slug}/column-floor`. Data again, not pictures.

**No picture of a sketch exists as an endpoint.** The API's only raster is the plan board —
`GET /plans/{id}/png` beside its `/svg`, both off one shared scene so the two encodings cannot disagree
(`B90`). The named stage images an agent can genuinely look at — **heightmap, contour, surface, topdown,
foliage, objectives, traversability, structures, mirror** — are CLI flags on `tools/PgmStudio.RoundTrip`
(`--topdown`, `--heightmap`, `--contour`, `--surface`, `--structures`, `--traversability-map`, `--mirror`)
over a built world, which is what `GET /map/{slug}/export` hands back;
`--topdown` also takes `--subject ground|structure|foliage|objectives`
to isolate one of those questions instead of drawing the combined view. So an agent wanting to *see* a sketch
has two honest options: render the data it already gets from the three reads above, or build the world and
take the stage set.

Two things are worth knowing before hand-writing a document. **Editor defaults and wire defaults are not the
same numbers.** A mark placed in the editor is seeded from the client's own starting values; a hand-written
mark that omits a field takes the C# record's default, and several differ — a spot height omitting `r` reads 2
where the editor seeds 4, a ridgeline omitting `r` reads 2 where the editor seeds 3, and a push omitting
`amount`, `roughness` or `crown` reads zero for all three. `base` is the exception and is deliberately not one:
an omitted `base` reads 4 on both sides, and the editor states the group's own top rather than any constant.
A line's reach is `r`, the one name for the one quantity; `width` is read on the way in and never written back,
so a document that is loaded and saved comes out spelling the one name. State the fields rather than relying on
any of it. And **a mark that does not carry what its kind needs is dropped, not defaulted**: a point without
`at`, a line or scarp with fewer than two points, an area or push with fewer than three ring vertices simply
does not reach the solver.

`tools/seeds/ruediger.layout.json` is the readable example for the geometry, the carve and the theme
assignment. It states no relief, no dressing and no room styles, so it is not a reference for those three.

## Limits

Sketch draws the ground and everything on it, and it does not know what any of it is *for*. It has no teams,
no spawns, no objectives, no build regions and no protection: a map begun here reaches Configure with geometry
and nothing else, and a map that came from a plan carries the plan's intent unchanged through this tool.

The structural pieces a plan projects in are **read-only here**. A spawn or wool room and the building inside
it are rendered so they stay visible while the ground around them is refined, but neither can be selected,
moved or reshaped, and a destroy objective has no sketch presence at all. The building is drawn where the plan
tool draws it, so the two tools show one rectangle rather than each showing its own. The backend half of correcting a structural piece's height across a
recompile is in place; the canvas half is not (`B107`).

The layout model carries two shape types the **Draw** dock cannot draw. A `circle` rasterizes as a 64-gon, and
a `polyline` is a centreline with a band whose width, edge and seed the inspector edits — but the dock offers
rectangle, polygon and lasso only, so both arrive only in a document written outside the editor. The canvas
controller can draw a polyline; nothing puts a button in front of it.

A placed building's `wings` can state an L, a T or a U, and `Decorator` composes them into one house under one
style the way the stamper always could (`G177`). On the canvas each wing is drawn as its own rectangle and the
rectangles are joined with `mod+g`; the joined building draws as one silhouette and each wing keeps its own
grips.

Symmetry here is a **preview and a mirror flag**, not a constraint. Shapes are drawn on one side and copied at
export for every group that opted in; nothing stops an author drawing across the axis, and nothing checks that
the two halves agree. The relief readback reports a symmetry error precisely because nothing prevents one.

Almost nothing validates a sketch. The only questions asked of it are whether anything was drawn at all,
whether a recompile would orphan hand-authored terrain, and — since the house-style gate above — whether a
bound room style is one its own geometry can be built from; there is no lint, no rule set and no score over the
drawing itself. What a board plays like is Configure's pre-flight and, past that, a human's.

**And nothing pictures one.** The sketch is the only stage between a plan and a world with no raster of its
own: `GET /plans/{id}/png` renders the plan board and the eight stage images render the built world (`B90`),
but a sketch answers only in data — palette runs, contour polylines, a numeric readback — and in SVG for the
finish previews. An agent that wants to look at what it drew must render that data itself or build the world
first. A `sketch/png` over the paint and relief it already computes would close the gap.

**The pictures that do exist are now built to be read.** They used to imitate what a map looks like — grass
green, leaves green, stone grey — and a green tree on green ground was invisible in a picture meant for
reasoning; the top-down world read-backs and their per-layer isolations (ground, structures, foliage,
objectives) now paint deliberate false colour instead, one high-contrast hue per category, and the plan render
carries a legend naming every role swatch and a build zone drawn in a hue no water ever wears rather than the
two shades of blue that once let a build zone be read as water (`B90`'s pictures, `B98`/`B95`'s legibility and
key). The rule to work by is unchanged either way: **a render answers "did what was authored come out", not
"what is this"**: it is a check against the document, never a source of meaning on its own.
