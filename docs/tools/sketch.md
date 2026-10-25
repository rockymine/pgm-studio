# The Sketch tool

## What it is

Sketch authors the ground itself: the outline of every landmass, the height of every part of it, what it is
made of, and what stands on it. It is the largest of the studio's tools and the only one that draws real
geometry — a plan states rectangles on a coarse grid, and everything finer than that happens here.

It is also one of the two tools a map can be started in. Opened empty it is a drawing surface and nothing
more: a sketch alone produces geometry and states no intent, so a map begun here has no teams, no spawns and
no objective until Configure gives it one. Opened on a map stored from a compiled plan, it receives the compiled
layout — the plan's abutting same-height pieces already fused into single polygons — and refines it.

The route is `/maps/{slug}/sketch`. Six phases sit on the rail in the order the work is done: **Info**,
**Draw**, **Terraform**, **Palette**, **Decoration** and **Review** — and one after them that is not a step in
making the board: **History**, its changes. This
document names them as the app does; the code calls Terraform the Relief phase, Palette the Theme phase, and
Decoration the Dressing phase. Info states what the
board is and is its own body; Review is the board as a player sees it, with the author's notes on it, in two
steps: Cameras, on the canvas, and Notes, a body of its own. The
other phases share the one live canvas, which stays mounted while Info or Notes is up so the
drawing state and the zoom survive the trip. Draw has three steps, Places, Ground and Layout, on that one canvas, and Places is an admin's only. Terraform,
Palette, Decoration and History have no steps: each swaps what the columns hold and which overlays the layer bar offers, and the canvas is reused as it stands.

**The address says where the author is.** It carries the phase by the rail's word — `info`, `draw`, `terraform`,
`palette`, `decoration`, `review`, `history` — and its step by the bar's (`?phase=draw&step=layout`), and is
rewritten in place as the author moves, so a reload or a shared link opens there; a step the caller does not have,
Places for anyone but an admin, opens on the phase's first step they do.

What a drive reads back about the board is not a phase: it is `GET /map/{slug}/report`, an agent's
read (*The report*, under the API).

**Each phase owns its state, in one of two shapes.** A phase whose truth is on the canvas — Terraform,
Decoration and Palette — takes one snapshot the bridge announces (`OnRelief`, `OnDressing`, `OnThemes`) and hands
it, with the bridge handle, to its list and its inspector, which write back through the handle. The Palette
snapshot is one value, `SketchThemes`: the registry and each theme's document, the map default, which shape
carries which theme, where each copy came from, the room shells and the biome, announced on every change of any
of them and on a load and a step taken back alike. A phase whose truth is the server's loads it itself: Info
is a component that reads its own document, and a phase with two bodies — History, and the views
Review's two steps show — has a plain state object (`SketchHistory`, `SketchViews`) that the host holds once and
passes to both. The map's notes are one more, `SketchNotes`, read in one place for Review and for the threads
History names. The host keeps the phase switch, the save before a page phase opens, the canvas mode, overlays and tool,
and the bridge's callbacks; the state objects reach the canvas and the save through delegates it hands them.

The editor bar (`EditorBar`, `docs/client/routing-and-ia.md`) is the same in every phase. Its crumb reads
*Maps › the map's name* — the name Info states, and the slug until one is saved — followed by the stage switcher,
whose menu opens the map's Plan and Configure where it holds them; the phase and its steps are in the middle. At
the right it carries the save state, the *View only* tag and the Problems button, then one group of icon buttons
— **Undo**, **Redo**, **Download** — a divider, and Back and Next, which is the only filled button. The number of
groups on the board is not in the bar: the Groups list in the sidebar is that count. The tool saves
continuously — every change schedules a debounced write 800 ms later, and Ctrl+S flushes it at
once. The finished map leaves by **Download** (an icon-only button titled *Download map: its world and map.xml,
ready for a server*, which pulses while it builds) on every phase: it flushes the layout, builds
the world the first time (the finish below, `POST …/sketch/finish`, which rasterizes the layout and moves the
map to `stage=configure`), and saves the export — the world ZIP with its `map.xml`. A board built from a plan
carries the plan's game settings, so its export needs nothing from Configure. What stops the map, and what the
build left out of it, sits behind **one button** in the bar — *Can't download* or *Can't save* with the number
of rules beside it, or *Left out* and *Warnings* where nothing stops it — which opens the findings as a list of
rules (`ProblemList`, `docs/client/ui-conventions.md`): the download's refusal, the save's refusal or the
warnings a landed save carried, and the declines the 3-D build raised. A refused download opens it. Where the
export refused, each of its rules carries **Open Configure**, which is where a hand-drawn board's teams, spawns
and objectives are stated. In the 3-D preview the declines are a *N left out* button in the layer bar, opening
the same list on that kind. Building the export costs the server a world, so **Download** needs an account on
the whitelist and is greyed for anyone else (`docs/access.md`). The phases run in order, Draw's *Next* going on
to Terraform. A draft that was never drawn on is discarded on the way out. A caller who may not change the map
sees every phase read-only: the canvas pans, selects and measures and changes nothing, the fields that write
are greyed, and nothing is saved or discarded (`docs/client/ui-conventions.md`). The bridge enforces it below the
greying: every handle verb that edits the document is refused while the page is read-only (`docs/client/canvas-interaction.md` §6).

**The geometry follows the drawing after the first build, too.** The scan the finish writes records the layout revision it
was rasterized from, so a layout written later (a vertex moved, a coast bent, a shape redrawn) is rasterized
again the first time anything reads the scan (`SketchFinish.RefreshAsync`). Every read of it goes through one
accessor, `FeatureData` — the segment rows behind `segments`, `column-floor` and `block-seat`, the surface
layer behind `top-surface`, the islands behind `islands` and `symmetry`, the configuration and the bounds
behind `regions/tree` and Configure — and an Api test fails on a route that loads the scan around it. So
every route answers for the board as it is drawn now, never for the one the first build saw. A scan written again
keeps what the author set on the configuration (the islands excluded), and drops a symmetry detected off the
old islands unless the author confirmed it. The connectivity reads walk the built world instead
(`docs/tools/configure.md`), and `scan-summary` counts wool, chests and resources a sketch scan never writes.

**A tab writes only what was drawn in it, and only over the board it read.** It holds the `ETag` the layout
was read at and states it as `If-Match` on every save, so where the stored board has moved on since — an agent
driving the API, a second tab — the save is refused `RQ13` at 409 rather than writing the older board back over
the newer (the held revision, the serialised saves and the refusal's sentence are `DocumentSave`). The Problems button then says so, no further save is sent until the page is reloaded, and **Download**
stops rather than build a board that is not the one on screen. A flush with no edit behind it — entering Review,
leaving the tool — sends nothing at all. What a layer states and the canvas has no control for — `kind`,
`part_of`, `seat` — is held as it was read and written back with the layer, so a made thing an API caller
seated stays seated through a save from the browser.

**What the later phases state, `docs/world-export/` executes**, and that folder is where the depth is. This
document is the tool: what each phase authors, what it writes, and what refuses. Beside it sit four that each
take one of those statements through to the blocks it becomes — `terrain-painting.md` (what the painter makes of a theme, cell by
cell), `structures.md` (the shells the Palette phase binds and the house the Decoration phase stamps),
`decoration.md` (the dressing pass itself) and `tree-corpus.md` (the hand-built ground truth any generated
foliage is scored against). `docs/world-export/sketch-world-export.md` is the world folder the export writes. Each is cited
below from the phase that feeds it.

## What it writes

The drawing is one artifact: the `sketch_layout_json` blob on the map row, read and written through `MapArtifactStore`
like every other artifact. It is stored **verbatim** as the browser produced it —
authoring source rather than a canonical document — which is why the editor's own save replaces the whole blob
and a deletion therefore sticks. No write merges.

The identity of the map — its display name and authors — lives on the map row and is saved through
`PATCH /api/map/{slug}/metadata`, not in the layout.

**The views an author keeps in the Review phase are a second artifact beside it**, `map_views_json`: a list of
`{id, name, lookX, lookZ, fromX?, fromZ?, y?, pitch?, yaw?, picture}`, where a view is only where the eye stands and
what it looks at, or which way it turns, and `picture` marks the one the map's picture is drawn from. It sits beside the layout rather than in it, so keeping a picture is not an edit to the board and does
not change the world the board builds — and a board loaded again over its own slug keeps its views.

**The map's places are a third artifact, `map_places_json`, and a kept one**: every write is a change in the
map's history, diffed and restored beside the layout and the intent. A place is a name over its own shape — a
`point`, a `box` between two corners, a `ring` of three points or more, or a `line` of two or more, in blocks —
and, at most, the id of the play piece it holds. Places make no terrain and link to nothing else: a shape of the
layout is redrawn as the ground is worked, so a place states its own geometry rather than naming one. What an
author or an agent has to say about a place is the thread of a note on it. `area` is the ground the board is
meant to fill, `[minX, minZ, maxX, maxZ]`, for an agent drawing a board that does not exist yet; a person's
board is the size of what is drawn. `verdict` is where the places stand with the author — `proposed`, `changes`
or `approved`, absent being `proposed` — and an agent builds on places the author approved; nothing the studio
stores is refused for it.

```json PUT /api/map/{slug}/places
{"places": [
  {"id": "abbey-hill", "name": "Abbey Hill", "shape": "ring", "points": [[-58, -88], [-26, -88], [-26, -56], [-58, -56]]},
  {"id": "hill-track", "name": "Hill track", "shape": "line", "points": [[0, -112], [-24, -94], [-40, -80]]}
]}
```

**The notes an admin leaves in the Review phase are rows, not an artifact.** `map_note` holds one per note —
its map by slug, what it is pinned to and its status — and `map_note_message` its thread, each message
with who wrote it, the token it was written with where it was one, the change to the map's documents it was
written at, the picture it carries by hash, and a reply's own mark on a picture. A note names its map by slug rather than by row because a driver replaces
the map row on every build, and a thread outlives every rebuild of the board it is about; deleting the map is
what lets its notes go. The pictures themselves are files, named by the SHA-256 of their bytes under
`Notes:Pictures` (*Notes*, under Review).

### The layout document

`SketchLayout` (`src/PgmStudio.Pgm/Sketch/SketchLayout.cs`) is the typed model of the blob, and it is the same
shape whether a layout was hand-drawn or compiled from a plan.

| Key | Holds |
|---|---|
| `setup` | `mirror_mode` — `none` for a board with no symmetry, and `rot_180` when unstated — the symmetry `center`, and the `bbox` the canvas frames on open |
| `layers[]` | the stacked slabs — each `{id, name, base_y, layout:{shapes, groups}}`, plus `kind`, `part_of` and `seat` where the layer holds a made thing, and `underside` where it hangs rock under itself (*A layer's underside*). Always at least one; a flat board is a stack of one, called `ground` |
| `themes` · `themeSources` · `mapTheme` | the terrain-paint registry, which library row each of its themes was copied from, and the map-wide default |
| `roomStyles` | the two bound room shells — `wool` and `spawn` |
| `dressing` | every placed prop |
| `biome` · `biomeSource` | the biome every column carries, and the library row it was copied from |
| `paint[]` | the paint rules: a material laid over a column's top courses where its slope, height, noise or nearness to a modifier say so, the first rule a column meets winning over its theme (`docs/world-export/terrain-painting.md`, TP28) |
| `modifiers[]` | the terraform stack: operations run over the built ground in list order, each inside its mask (*Terraform*, below) |

Eight of those are the map's **finish** rather than its shape — `themes`, `themeSources`, `mapTheme`,
`roomStyles`, `dressing`, `biome`, `biomeSource`, `paint` — and that grouping is load-bearing: none of them
decides where ground stands, so a compiled layout carries
none and a refinement states them onto it. `modifiers` is *not* in that set, because a modifier is geometry: it
decides what the rasterizer emits.

The geometry half, with one shape of each flavour — a plain rectangle, a polygon carrying per-vertex heights
and a Bézier edge, a carve, and an erected shape standing out of the ground:

```json PUT /api/map/{slug}/sketch
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
          "height_mode": "raise", "skirt": 2, "relief_scope": "hold" }
      ],
      "groups": [ { "id": "i1", "name": "Team island", "mirrors": true,
                     "shapeIds": ["s0", "s1", "s2", "s3"] } ] } }
  ]
}
```

Read against the fields above: `s2`'s `base_height` of 100 is the statement of intent a subtract's height
always is — it carves the whole column whatever the number — and `s3` sits *inside* the hole `s2` cut, which
is what `override` is for, since an override-add is laid after the ordinary subtracts. `controls` is keyed by
the index of the vertex whose edges the handles bend. The whole document adds the finish keys and `modifiers`
beside `layers`; the stack is shown under *Terraform*.

### Shapes

A shape is one entry in a layer's `shapes` array. Its geometry is a rectangle (`min_x`/`min_z`/`max_x`/`max_z`),
a circle (`center_x`/`center_z`/`radius`), or a vertex list (`vertices`, with optional Bézier `controls` per
edge). Every shape carries an `operation` — `add` builds ground, `subtract` removes it — and an `override`
flag that decides the order the set algebra resolves in: the ordinary pass is adds minus subtracts, then
override-adds overwrite whatever column they land on, then override-subtracts remove theirs last.

**`keepClear` says the shape is not ground to dress.** A shape drawn to *be* something — a town wall, a crop
bed, a well's rim, a flight of stairs — is terrain by construction: nothing about its material, its layer or
its provenance separates it from the ground beside it, so a road repaints its top course and a channel cuts it
down to its line. Marking it puts its columns in the dressing pass's keep-out (`KeepOut.Structure`), and
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
difference from a terraform modifier, and it is the whole of it: a modifier moves a surface, a subtract removes it.

**A subtract with a lid over it is a roofed void, and it is the shortest way to state underground space.**
A subtract has no `y` at all — it is a set of `(x, z)` cells, which is why its own height is not read — so it
cannot hollow a column out from the middle. What it can do is state that a column is empty and let something
else stand over it: an **override add above the subtract's own floor** takes the layer's one span with it and
records nothing beneath, so the void the subtract stated is still void, with a deck on top. Measured on a
landmass built `[0..20)` with a subtract through it and an override add stated at `floor 14`: the column comes
back holding `[14..17)` and nothing else, and the board raises **no finding at all**. The same lid stated at
`floor 0` — at the subtract's own floor — is the fill `SK13` names.

That is the whole rule, and the two halves of it are one sentence: an override add **above** the subtract's
floor is a lid and is silent; **at or below** it is a fill and is named. So a chamber under a hill can be
stated as the ground it is not rather than built as walls and a ceiling around a gap — the negative space
first, then what roofs it.

**A hollow ring is one polygon and not two shapes.** A subtract is named wherever an add sits over or
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

**An edit that adds or removes points carries the heights with it.** A point that was a vertex keeps its
height; a point the edit adds — an insert, a pull, a bend's resampled coast — takes the height the old outline
had where it stands, interpolated along its nearest edge and rounded to a whole block; a removed point takes its
own height with it. So an insert on an edge leaves the ground exactly where it was, and a removal changes the
slope only through the point that went. The canvas and the server's vertex, pull and bend edits keep this one
rule. Heights that did not already line up with the outline are left as they stand, for `SK22` to name.

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
the one an author states to mean forty-five degrees.

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

Four further fields decide how a shape meets the ground under it and the modifiers over it. `height_mode` —
`level`, `raise`, `sink` or `drape` — makes a shape stand at its own statement over the ground rather than be
part of it: a mesa cut flat at an absolute height, a
plinth held a fixed amount above the middle of the ground it sits on, a quarry the same downward, and a field
wall or a hedge held that amount above the ground at every cell, so it climbs the hillside it is laid over.
The Draw inspector names `base_height` by what it is under the mode: **Top at y** for `level`, edited as the
absolute y of the flat top (`floor + base_height`); **Height above ground** for `raise`, measured from the middle
height of the ground under the shape; **Depth below ground** for `sink`; **Height above ground** *at every
cell* for `drape`; and **Height above floor** for ordinary ground, its hint stating the top's y. `skirt` is how far in
from its own outline an erected shape eases back into the ground it meets, in blocks; zero is a sheer face,
which is right for a built thing and wrong for a hillside. `relief_scope` is `hold`, `ride` or absent and
says how the shape meets the modifier stack (see *Groups and layers*): `hold` keeps its columns out of every
modifier, `ride` keeps its form and moves it with the ground, and absent lets the modifiers reshape it. Play
rooms always hold. The canvas
bridge writes only the words `ReliefScopes` and `HeightModes` name, validated against one JS list
(`shared/relief-words.js`) that a test holds equal to the C# sets. And a polyline carries `stroke_edge`
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
which is what a bend fits and what the *Reshaping ground a compile drew* section below writes. The two are
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
hit-tested, never edited, laying no ground of its own, and merged back into the saved document unchanged.

**It places no terrain and it does scope paint**, which are not the same claim. The rasterizer takes no ground
from a role shape — the island under it already holds that — but it does read one as a **paint scope** over the
cells it covers, the way it reads a shape stating a `height_mode`: a candidate at whatever height it carries,
never the surface another shape is measured against. That is how a room's own ground is stated, and without it
there is no shape to state it on; a role shape naming no theme says nothing and the board's own ground stands.
The plinth the build levels under a room's footprint is finished by whatever theme resolves there
(`docs/world-export/terrain-painting.md` § the scope, § the runtime seam).

**A room is drawn from the intent, and the layout stores only what it says about the ground under it.** Every
spawn and wool whose `protection` is one rectangle is drawn into the first layer as it is read
(`RoomShapes.Project`) — by `GET /sketch`, by every partial write, by the world build — and no stored layout
holds one. What the layout states about a room lives in its **`rooms`** record, keyed by the id the room is
drawn under: `floor`, `base_height`, `anchor_heights`, `relief_scope`, `height_authored`, `doors`, `theme` and
`material`, each meaning what it means on a shape. A body `PUT` back with its rooms drawn is stored folded
(`RoomShapes.Fold`): the rectangles go, and what each room shape stated about its ground lands in its record.
A room with no record holds against the modifiers all the same, since a play room always holds.

**A room is drawn as its region and its house's wings.** `spawn` and `woolRoom` are the *region* — the
ground the room stands on and the protection around it — drawn under the piece's id, with the `color` their
labelled box is filled with, and the height the room's ground is held at. `building` is one *wing* of the
house raised inside it (`docs/world-export/structures.md` §1), drawn in the **building ink** — the same one a
house dressed onto the ground takes, because a room's building is that kind of house — as an outline over the
box rather than a second filled one. It carries no height of its own: the region is what the room's ground is
held at and what an author corrects a height on. The room's wing takes the region's id plus `-building`,
and every wing after it `-building-2`, `-building-3` on. **A room stating no house is drawn with the one it is
raised as** — `WX1`'s default, the region inset a block and further on a spawn's door sides, a walled room's
(`RoomHouse.Default`) — so a composed or newly placed room shows its building, and a drag or a resize of it
states the house where it was let go. A region stated as several rectangles is not drawn at all.

**Its footprint stays inside the fused polygon, and has to.** A room places no terrain of its own, so the
ground it stands on is the group's. Selecting the polygon in front of a room therefore selects one that
reaches under the room as well — which is the shape of the thing, not a fault in it. What keeps the room level
is that a play room holds against the modifiers.

### Groups and layers

A **group** drawn in the tool is computed, not authored. Every time the shapes change, the tool unions them
and reports the connected pieces that result, so two rectangles pushed together become one group and pulling
them apart splits it again. What an author owns is the group's `name` and its `mirrors` flag — whether the
group is copied onto its symmetry orbit — and those survive a recompute by matching the group back to its
previous self. A single-member group shows the shape inspector rather than the group one, so a lone rectangle
needs no drilling.

**A group the board was loaded with is kept as stated until an edit touches it** (`settleGroups` in
`boolean.js`). The build reads a group's `shapeIds` for its fan and its keep-clear, and an API
caller may state a group the geometry would not have made — one group over pieces that do not touch, or a
post held unfanned in a group of its own over the wall it stands on. So a loaded group keeps its id, name,
`mirrors` and `shapeIds` while none of its shapes changes outline or is deleted and no new or reshaped shape
overlaps one of them; a theme, a height or a name set on a shape is not an outline change. A touched group,
and any shape no group lists, is grouped from the geometry as a drawn one is, and takes its identity from
the loaded group it overlaps most. A group of several pieces is drawn as one outline per piece.

The group matters beyond naming: **it is the unit the symmetry fan and the keep-clear fan are read off**, because
the build copies a mirroring group's `shapeIds` onto their images. It is not the unit a terraform is stated over.
The modifier stack runs over the whole board's ground, each modifier inside its own mask, so two parts of one
landmass are reshaped separately by two masks.

**A shape can stand out of the modifiers, and this is where the fusion stops being a cage.** `relief_scope` on a
shape says how it meets the stack (*Terraform*). `hold` keeps the shape's columns out of every
modifier: it keeps its own column — its stated floor, its thickness and any per-vertex tilt — and the ground
around it is reshaped up to its edge. `ride` keeps the shape one thing standing proud: each of its columns
moves by the median change the stack made to the ground under the shape, as a `raise` does, so a building on a
hill the stack raises rises with it and is not itself reshaped. Absent lets the modifiers reshape the shape like
any other ground. A play room always holds.

That is what makes a mixed board possible. A flat rectangle with a raised step attached to it is one group;
marking the step `hold` leaves it standing at exactly the height it was drawn at while a Hill beside it reshapes
the ground around it.

A shape declaring a `height_mode` meets the modifiers without a scope. A `level` shape holds: its flat top is an
absolute height and no modifier moves it. A `raise`, `sink` or `drape` shape rides: the stack moves it with the
change it made to the ground under it, a `drape` cell by cell, so a wall climbs the hill raised under it, and a
`raise` or `sink` by the median change under it, so a plinth stays one flat thing standing proud. A shape stating
`relief_scope: ride` moves by that median whatever its height mode.

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
soffit at y 24 whatever the modifiers do above it. `anchor_heights` interpolates thickness per vertex across a
polygon, which is how a ramp climbs from one layer's floor to another's.

**The modifiers run in world y.** The stack runs over the top of each column among the layers that are not made
things (*Terraform*), so a stacked board answers one built surface and a caller reading a height off `column`
never adds `base_y` a second time.

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
against its own surface, so a cell standing on two layers is painted twice. Within a layer the paint follows
the shape that forms the surface: among the shapes covering a column only those reaching its visible top may
own it, and among those the smallest-area themed shape wins. Across layers there is no contest, because each
surface shows its own. A shape id is unique within its layer and not across the stack — two made things compiled by one tool
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
isometric preview, and a new layer defaults to ten blocks above the highest existing one. A placed prop follows
the same rule in the Decoration phase: one resting on another layer is drawn dimmed, and a click reaches only the
props of the layer being drawn on.

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
strict about the ground they were written for. The painter reads `kind` too: it
paints `made` layers before the ground, so a made thing is its own material in every course it spans and the
ground under a ladder or a cactus keeps its surface (`docs/world-export/terrain-painting.md` TP25). Otherwise a
`made` layer rasterizes, themes and mirrors identically.

**A made thing fanned onto its images turns its blocks with it.** A block whose data is a direction — a ladder,
a stair, a log's axis, a torch, a fence gate, a chest — is stated once, on the authored half, and on every
other image of a mirroring group it is written turned by that image's transform. So a ladder with its back to a
wall has its back to the image of that wall on a `mirror_x`, `mirror_z` or `rot_180` board and on all four
images of a `rot_90` one, and a stair climbing into a wall keeps climbing into it. A reflection swaps the
facings across its own axis and keeps the rest; a quarter-turn swaps the axes.

**The turn is read from the fan, never from the fold.** What decides it is which image of a shape's orbit
claimed the cell, so a thing the author drew a second time on the far half of a group that does not mirror is
image 0 there and is written exactly as drawn. `docs/world-export/terrain-painting.md` §5 carries the mechanism.

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

### A layer's underside

**A floating layer hangs rock under itself when it states an `underside`.** Without one a layer is the slab its
shapes state, so ground standing over void ends in a flat bottom. With one, each column's floor drops to a rock
that is `edge` blocks thick at the rim of the layer's footprint and `taper` blocks thicker for every block inward,
which narrows an island to a keel and leaves the rim thin. The underside belongs to the **layer** and not to a
shape, because one layer is one landmass: two shapes of a layer that touch hang as a single island, and a
per-shape underside would cut a seam into the rock wherever they meet. A group is no owner either, since a group
only decides what is copied onto the symmetry orbit.

| Field | Takes | Says |
|---|---|---|
| `edge` | blocks, default 4 | the rock's thickness under a rim column, counted from the column's top |
| `taper` | blocks per block, default 0.9 | the thickness added for each block inward, measured as the straight-line distance to the nearest cell the layer does not hold, so a corner narrows faster than a straight rim and a lake in the island has a rim of its own |
| `rough` | blocks, default 0 | the amplitude of noise added to the thickness, in features about 12 blocks wide |
| `seed` | integer, default 0 | the noise row; the same seed hangs the same underside on every build |
| `sheer` | shape ids, default none | shapes of the layer whose outline beside open air is a rift side |

**The thickness is measured from each column's own top, after the modifiers and the seats have settled it**, so
terraform that raises a hill raises the rock under it with it. A column's floor is the lower of the flat floor its
shapes state and the underside, which makes an underside only ever add rock: a slab already thicker than the rock
under its rim keeps its own floor there and hangs only where the taper has gone deeper than the slab is thick.
The floor never rises above the course under the top and never falls below course 1.

**With `rough`, the noise is read at each cell's canonical member under the board's symmetry**
(`PgmStudio.Geom.Symmetry.Canonical`), so a mirrored layer hangs the same on every image and the two halves of a
rot_180 board cannot be told apart from underneath.

**A `sheer` side drops far before it narrows.** The open air touching the outline of a named shape makes the
columns beside it hang 26 blocks at the rim and 1.2 more per block inward, in place of `edge` and `taper`
(`Underside.SheerEdge`, `Underside.SheerTaper`). Where a column is near both kinds of rim the shallower reading
applies, so a sheer face on one side of an island does not deepen the others. A shape id that names no shape of
the layer is ignored.

**An underside rests on the terrain below where it would leave under four blocks clear.** When another terrain
layer stands under the column, and the floor would sit fewer than `Underside.Clear` (4) blocks over that layer's
top, the floor is that top instead and the two join, so an island hung low over a hill is one mass with it and
no sliver of air is left to fall into. A column whose floor stays four blocks or more clear hangs free, so a rim
can hang over a valley while the keel meets the ridge under it. Only terrain is rested on: a `made` layer below is
not ground. Every reader sees the joined floor, because they all read `ColumnSegment` — the walk, the painter's
bands (a column whose lowest stone is above course 1 is a floating one and takes a base), the export and
`sketch/columns`.

An island of one shape with a rough, sheer-edged underside, set 40 blocks over void:

```json PUT /api/map/{slug}/sketch/layers/{layerId}
{
  "name": "Island",
  "base_y": 40,
  "underside": { "edge": 4, "taper": 0.9, "rough": 1.5, "seed": 1, "sheer": ["cliff"] },
  "layout": {
    "shapes": [
      { "id": "cliff", "type": "rectangle", "operation": "add",
        "min_x": 0, "min_z": 0, "max_x": 6, "max_z": 40, "floor": 0, "base_height": 1 },
      { "id": "body", "type": "rectangle", "operation": "add",
        "min_x": 6, "min_z": 0, "max_x": 40, "max_z": 40, "floor": 0, "base_height": 1 }
    ],
    "groups": []
  }
}
```

Read against the fields above: both shapes are one island of one layer, `base_height` 1 leaves the rock to the
underside, and the west edge, which `cliff` draws, hangs 26 blocks at its rim where the other three hang 4. In the
Draw phase the active layer's panel carries an *Underside* box with the four numbers; `sheer` is stated over the
API and rides through a save from the browser untouched.

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

The modifiers run inside that build (`SketchTerraform.Run`), so the contour overlay and every read of the
columns answer the same ground (*Terraform*). A layer's underside lowers its floors after the modifiers and the
seats have settled the tops (`SketchRasterizer.Hang`).

What becomes of those columns once the map is exported — the layer scheme the world folder is written in, its
`level.dat`, the coordinate anchoring, the wool-cage chests and the observer platform — is
`docs/world-export/sketch-world-export.md`.

## Phases

**Selection is a ladder of three rungs, and exactly one of them is on screen.** A plain click picks the unit
the phase states: a group in Draw, because a landmass is what moves; a modifier in Terraform, through its mask;
a shape in Palette, because a theme is assigned to one shape or to everything a group holds. A
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

`Escape` walks the whole way out in the order a press means it: an in-progress draw, then the tool in hand,
then the points, then a picked wing to its room and a picked piece or place to nothing, then the entered group,
then the selection itself — one step a press. Nothing on the rail deselects: `Escape` and a click on empty canvas
are the two ways out. With a theme in hand
it does none of that and puts the theme down instead. Finishing a drawn outline is no part of the ladder: a polygon closes on `Enter` or on a click landing back at
its own first vertex, and a polyline ends on `Enter`, open where its last point was placed.

**The Shapes chip draws every primitive on the board; without it, the selected or entered group draws its own
members instead** — faintly where the group is merely selected, plainly where it is entered, so a member
becomes reachable without hunting for the toggle first.

**Which layer is drawn on is canvas chrome, not any one phase's own state.** The layer strip floats at the
foot of the canvas beside the dock and is present in every phase that draws the canvas. It is where the set
of layers is *changed* as well as switched: only Draw offers the `+` that adds one and the `×` that removes
one, because both are drawing work, and the last layer has no `×` at all — a board always stands on one.
Whatever is placed lands on the active layer — a placement takes it unless it already names one — so an
author is never asked twice which layer something belongs to. The Palette phase's swatch strip is chrome for
the same reason and floats above the dock beside it.

**A phase offers the overlays it can use, and switches on the ones it works with.** The layer bar is not a
fixed six: a phase shows the layer it works on and the layer it works against, and an overlay that would draw
a fact another shown layer already carries is not offered at all. Draw and Terraform keep the shapes, the mirror,
the chunk grid, the blocks and the contours; Draw offers the contours off, for the heights a shape builds while
it is being placed, and Terraform leads with them, because the paint has not run yet and the contours are the only
view of what is being stated. Palette and Decoration open with **Blocks and
Shapes on** — the paint is what they act on and the outlines say what carries it — and offer no contour chip,
because the painted ground already carries the height. Snap is in none of them: it changes what a drag does
rather than what is drawn, so it sits in the dock beside the shape tools, in Draw, the one phase that drags a
shape edge onto another. Entering a phase switches on what that phase works with and never switches anything
off, so an overlay asked for by hand is not taken away by walking through the phases. Placing a view in In
game is the one exception, and it takes nothing away either: the canvas draws the Board layer and the views
alone whatever the other chips say, offers no chip at all, and gives every other layer back as the author had
it on the way out.

**How big the selection is reads as a pill under it** — the same pill Configure draws under a region, from the
one function both call. A picked play piece is also named, by its id, small and left-aligned above its box, as
Configure names a picked region; no piece carries a name on the board otherwise, its colour saying what it is.
The corner readout is for what is being drawn or measured right now; what the selection measures is under the
selection.

**The map's destroyables and cores show as markers**, in the colours the plan places them in. The sketch draws
the ground and owns no objective, so they arrive from the map's intent as positions and nothing else — no
name, no handle, nothing to select. An author refining the ground can see what it has to carry; Configure is
where one is edited.

**Undo is one stack for the whole document, sixty steps deep.** `Ctrl`/`⌘`+`Z` steps back and
`Ctrl`/`⌘`+`Shift`+`Z` (or `Ctrl`+`Y`) steps forward. A step is the whole layout — the value the canvas can be
loaded back into — rather than a record of which edit happened, so a press that changes nothing costs no step
and a drag that fires on every frame between the press and the release costs exactly one. A step is the
document and not the view: the camera and the layer being drawn on are where the author is, so both survive
it. The symmetry setup is part of the document, so a step back restores it too: the canvas announces
`OnSetup(mirrorMode, cx, cz)` on every load, edit and step, and the Setup controls and Info read nothing else.

**A play piece is a step too, though it is not in the layout.** A piece is the intent's, written on the server
through `…/intent/pieces`, so placing, moving, resizing, turning, deleting or setting one keeps the intent's text
beside the layout as one step, and stepping over it writes that text back with `PUT /api/map/{slug}/intent` before
the layout is restored. The room ground a placement brings is part of the placement's step rather than one of its
own. A step the studio refuses is not taken, and the note beside the rail says why.

**Every chord below is also live in the sheet and the palette.** `?` opens the keyboard sheet grouped like the
table below, dimming whatever cannot run on the current selection; `Ctrl`/`⌘`+`K` runs any of them by name.
`docs/client/canvas-interaction.md` describes the mechanism both draw from.

| Chord | Does | Group |
|---|---|---|
| `1`–`7` | Go to Info · Draw · Terraform · Palette · Decoration · Review · History | Phases |
| `V` | Select | Tools |
| `H` | Pan | Tools |
| `R` | Rectangle | Tools |
| `P` | Polygon | Tools |
| `W` | Polyline | Tools |
| `L` | Lasso | Tools |
| `M` | Measure | Tools |
| `X` | Split | Tools |
| `B` | Flip build ⇄ carve | Tools |
| `F` | Fit the working bounds | Canvas |
| Double-click | Go one level deeper — into a group's member, then into that shape's points | Canvas |
| `Ctrl`/`⌘`+click | Reach the shape under the cursor and enter its group as the scope | Canvas |
| `Alt`+click | Pick the parent group and leave any scope | Canvas |
| `Enter` | Go one level deeper, or close the polygon/polyline in progress | Canvas |
| `Escape` | Cancel the draw, else put the brush or the tool down, else step back up a level, else clear the selection | Canvas |
| `Delete` / `Backspace` | Delete the selected point, where one is picked on the points rung | Canvas |
| `Delete` / `Backspace` | Delete the selected shape, where no point is picked | Canvas |
| Arrow keys | Nudge the selection one block (`Shift` for sixteen) | Canvas |
| `Shift`+`P` | Promote the shape to its own group | Sketch |
| `Ctrl`/`⌘`+`D` | Duplicate the selected shape | Canvas |
| `Alt`+`1`…`5` | Toggle Shapes · Mirror · Chunks · Blocks · Contours, where the phase offers it | Overlays |
| `Alt`+`6` | Snap while dragging | Tools |
| `[` / `]` | Take the previous / next theme in hand — answers only in Palette | Palette |
| `Shift`+click | Paint every shape the group holds, with a theme in hand | Palette |
| `Alt`+click | Lift a shape's theme into the hand, with a theme in hand | Palette |
| `Ctrl`/`⌘`+`Z` | Undo | Everywhere |
| `Ctrl`/`⌘`+`Shift`+`Z` (or `Ctrl`+`Y`) | Redo | Everywhere |
| `Ctrl`/`⌘`+`S` | Save the sketch | Everywhere |

### Info

Two steps. **Identity** is the map's display name and its authors, loaded from `GET /api/map/{slug}` and saved
with `PATCH /api/map/{slug}/metadata` as it is typed, the way Configure's Identity saves: once typing pauses for
800 ms, and on leaving the phase. The editor bar says where the save stands — *Unsaved*, *Saving…*, *Saved*, or
why it was not saved; a name left empty is not saved. **An author named here is named in the map's intent too**, where the map
holds one: the rows are what the map document is written from and `meta.authors` is what the *export* reads,
since the observer platform's board is stamped from the intent. A panel writing one of the two would credit the
map on its row and leave `EX6` saying it names nobody, with nothing in the interface able to settle it. The
traffic already ran the other way — an intent write resolves its names into the rows — so this is the return
leg, and the intent is patched as JSON rather than round-tripped through `MapIntent`, since a rename is not the
moment to rewrite an intent through a model. **Settings** is the symmetry the whole board is built against: the mode
— `mirror_x`, `mirror_z`, `rot_180`, `rot_90` or `none` — and the centre X and Z, which the panel hides under `none`
since nothing turns about it. Under `none` every shape is drawn once: the canvas draws no axis and no mirror
preview, and a group's *mirrors* has nothing to fan. The words are `SymmetryModes`, and the panel offers the
five `SymmetryModes.Sketched` names; a layout may also state the two diagonals. There is no size to set; the map area
grows to fit whatever is drawn. A freshly created sketch opens here (`?phase=info`); an existing one opens on
Draw.

### Draw

The canvas, and where the geometry is made. **It has three steps, Places, Ground and Layout, one click apart in the
bar**: Places draws the places, Ground draws the shapes, and Layout places the play pieces. Ground and Layout draw
every shape and every piece, and each works on its own: Ground picks, moves and draws the shapes and passes over the
pieces, and Layout picks, moves, resizes, turns, adds and deletes the pieces and passes over the shapes. Draw opens on Ground, and Info's last step goes on to Draw's first
step, where a new map starts.

**Each step's panel lists what that step works on.** Places lists the places, Ground the layer and its group→shape
tree, and Layout the play pieces: one section per kind, in the order the toolbars offer them, each piece under its
id, with a room's point after its room and a room's building reached through the room. Choosing a row picks the
piece as a click on it would, and the picked piece's row is lit. An orbit's image is listed beside the piece it
copies, tagged *image*, and takes no click, as on the canvas.

**Places is an admin's.** The places are the plan an agent and an admin talk over, so they are
written by the same people the notes are: an admin in a browser, or a token carrying the notes permission
(`docs/access.md`). Anyone else's Draw has two steps, Ground and Layout, and Review's Cameras step draws no
places for them.

**Places draws the places over whatever the board holds**, top-down, with the notes step's marks — a point, a box,
a lasso — and a polygon and a polyline beside them; each draws a place rather than ground. A drawn place is
stored at once under the next free `place-{n}`, named *Place n*, and picked. The column lists the places, a click
on one on the canvas or in the list picks it, and its panel names it, sets the play piece it holds and deletes
it; every change stores the whole document through `PUT /map/{slug}/places`, so each is a change in the map's
history. Above the list, **Verdict** says where the places stand with the author.

**A place holds one thread, and the list says which places have one.** A thread is about its place and nothing
else, so there is nothing to choose: to talk about another place, pick it. A place with a thread carries a speech
bubble at the end of its row: filled, with the count inside, while it holds messages that are new to the caller,
and an outline once they have all been read. Under the picked place's fields the thread fills the panel to the
bottom over one text box, with no filters and no New note step; the first message written there starts it, and
opening the place reads what was new in it. The places are drawn in this step and in Review's Cameras step, and
a click here picks a place and nothing under it.

**Layout's dock has three families.** *Pieces* places a spawn piece or a wool room, *Zones* a
build area or a water lane — each dragged out as a rectangle, the size it is drawn at — and *Markers* puts down
an iron marker, a destroyable, a core or a defence chest with a click, centred on the block clicked, or drags out a
wall along the line from the block the drag starts at to the one it ends at, two blocks thick at whatever angle
that runs. A chest placed beside a wall fronts away from it, and its rail turns it a quarter at a time. A
destroyable and a core are offered at two teams only.
Each writes through `POST /map/{slug}/intent/pieces`, which places the piece and every image the symmetry draws
of it (`PlayPieceAdd`): a spawn goes to the first team that has none and faces the middle of the board, a team's
first wool takes its colour and every later one the next dye no wool on the board has, and a goal takes the
default structure. A map stating no teams takes the ones its symmetry gives — red and blue at two — with the first
piece placed, so every piece names a team the map has. **Only an orbit's authored image is picked**: the images
the symmetry draws of it are drawn dashed and fainter, a click on one picks nothing, and each moves, turns and goes
with its authored image. **A spawn or wool room placed where no ground lies also draws a rectangle of ground the size
of the room**, as an ordinary shape of the active layer, which Ground then reshapes like any other. A picked
piece's rail offers **Delete** in Layout, which takes the piece and every image of it out of the intent; the
ground a room brought stays, as the shape it is. Placing ends the tool unless Shift is held, as drawing does.

**A picked piece's rail carries its settings**:
a spawn's **Facing** with a turn, a destroyable's or core's **Name**, a destroyable's **Design**, **Material** and
**Float**, a core's **Lava footprint**, **Lava height**, **Casing**, **Float** and **Leak** with the casing and
the dig they imply, and a wool's **Colour** (`ObjectiveFields`, the one body both tools show). Each is read from
the intent by the piece's id and written through `PATCH /map/{slug}/intent/pieces/{id}/settings` one setting at a
time (`PlayPieceSettings`). What a unit shares every image takes — a goal's structure is the same goal for every
team — while a name and a colour are set on the named image alone, and a spawn's facing turns its images the way
a turn does; a field put back to its default writes null. A destroyable made of gold for every team:

```json PATCH /api/map/{slug}/intent/pieces/{id}/settings
{ "key": "materials", "text": "gold block" }
```

**A room is picked whole before what stands on it.** A press on a room's building or its marker picks the room's
region the first time, as a click on a group picks the group before its member, and the wing or the marker under
the pointer once the room or something else standing on it is picked (`structuralAt`). A picked region, build area or water lane wears the transform box
every rectangle wears, and dragging a corner or an edge resizes it by whole blocks, the opposite side held
(`PieceResizeController`); the release asks the piece route, which keeps what stands on a region at the same
fraction across it (below).

**A room's house is shaped where the room is placed.** Picking a wing of a room's building in Layout shows the
transform box on that wing and the round insert marker on its free walls, the gestures a dressed house takes in
Decoration (`RoomHouseController`): a corner or an edge drags to resize the wing, held to 4 blocks across and to
the room's region, and a press on the marker adds a wing along that wall, 5 blocks deep and shrunk to
fit the region, or says there is no room for one. The rail lists the wings with the room's own first, which stays;
deletes any other the rest still meet through; sets the picked wing's **Storeys**, **Roof**, **Ridge** and whether its
roof crosses the wing it meets; and dresses the house in a **Style** — the map's for its kind, a library shell copied
onto it, or **Open ground**, the pad with no building over it (`SketchRoomHouse`).

Every change writes the house whole through `PUT /map/{slug}/intent/pieces/{id}/house`, the room's wing first and
every other image of the room carried through the symmetry (`PlayPieceHouse`). A style left out wears the map's, and
a stated `null` is open ground. A spawn's house of two wings:

```json PUT /api/map/{slug}/intent/pieces/{id}/house
{ "house": { "wings": [ { "corners": [[-16, -16], [-5, -5]] }, { "corners": [[-4, -14], [-1, -7]] } ] } }
```

**A room's doors are drawn on its house and placed by a shift-press.** While a wing is picked, each door the room
is built with is a bar on the outline where the stamper cuts it — on the free wall facing its way that holds its
place, nearest the line it was stated on — so a wool stating none shows the one its ground gives it. A shift-press
on a free wall adds a door there, centred on the block pressed, or takes away the one cut within 2 blocks of it,
and a room keeps at least one (`RoomHouseController`). The doors are read from `GET
/map/{slug}/intent/pieces/{id}/doors`, which answers them with `stated` false where the room states none, and every
change writes them whole through `PUT …/doors`, carried to every image of the room (`PlayPieceDoors`). A door on
the room's east wall, and one centred on its south:

```json PUT /api/map/{slug}/intent/pieces/{id}/doors
{ "doors": [ { "edge": "+x", "x": 0, "z": -10 }, { "edge": "+z" } ] }
```

**A picked wall's ends drag apart.** In Layout a picked approach wall wears a square grip on each end of its line,
and dragging one moves that end alone, by block corners, the other standing where it was
(`WallEndController`). The end snaps to the nearest point on a drawn shape's outline within 3 blocks, so a
wall run from shore to shore meets both. The release asks `PATCH /map/{slug}/intent/pieces/{id}` with `part`
`start` or `end` and the corner the end lands on as all four bounds, and every image of the wall moves its
matching end the mirrored or turned way; a refusal puts the wall back. The second end of a wall moved to
(-6, 20):

```json PATCH /api/map/{slug}/intent/pieces/{id}
{ "part": "end", "minX": -6, "minZ": 20, "maxX": -6, "maxZ": 20 }
```

A build area placed by hand, which answers the id it was given:

```json POST /api/map/{slug}/intent/pieces
{ "kind": "buildArea", "minX": -10, "minZ": -40, "maxX": 10, "maxZ": -20 }
```

**Four tools draw, and one word decides what they draw.** Rectangle drags a box; polygon places vertices and
closes; polyline places points along a line and ends on `Enter`, leaving it open — it commits as a `polyline` of
radius 3 with a `solid` edge, a centreline the band is grown around, which the inspector then narrows to a wall
or widens to a lane; lasso traces freehand and closes itself, simplifying the trace at a four-block tolerance so
a big round blob arrives as about ten anchors rather than one per block — it commits as a polygon. Beside them sits the
operation, **Build** or **Carve**, wearing the colour the finished shape will take, so an armed carve cannot be
mistaken for an armed build. Two more tools read and cut rather than make: **measure** drags a ruler between
two points, the usual question being how wide a void gap is, and **split** slices the topmost shape it crosses
into two independent shapes. Every draw and every split drops the
tool back to select when it completes, with the new shape selected — unless **Shift** is held as it completes,
which keeps the tool in hand and selects nothing, so the next press draws again. `Escape` or `V` puts it down.

**The top two rungs wear the same box, and the third wears none.** A group and a shape are each drawn with
the **transform box** every surface in the studio uses (`docs/client/canvas-interaction.md` §5): four corner
anchors on the selection's own bounds, a grab band along each edge, drawn as a bar while hovered, that stretches or squashes that
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

**A point is taken out with the key that takes a shape out.** Clicking a vertex handle picks it, and while a
point is picked `Delete` or `Backspace` removes that point and joins its two neighbours, where with none picked
the same keys delete the shape. A picked point belongs to the points rung: stepping up a rung, or picking
another shape, lets it go. The removal is refused, with a sentence in the layer bar for a few seconds, where it
would leave a ring under three points or a path under two, or would fold a ring across itself — the refusals
`DELETE …/vertices/{index}` gives for an outline, with the same test for the fold. A removal renumbers the Bézier
`controls` and `anchor_heights` above the removed index, drops the removed point's handles and those of the two
points that now share an edge, and is one undo step.

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
aimed plane from three — rounding to blocks, so a slope reads as the neat straight steps of a staircase. Inserting a point on an edge renumbers the marked controls with the points, so each stays on the point it was placed on.

Six overlays sit above the canvas: **Shapes** (the draw primitives over the fused groups), **Mirror** (the
symmetry copies), **Chunks** (the 16-block grid), **Blocks** (the rasterized footprint — the exact cells an
export would fill), **Contours** (the height contours of the ground every group builds) and **Snap**. A
read-only isometric preview draws **the world the export builds**: entering it posts the live layout to
`sketch/columns`, which runs the real build and answers every column's solid runs, and the browser meshes
those into triangles. So the picture carries the terrain's own materials, the ground the modifiers left,
the structures stamped on them and the goal markers hanging over the build ceiling — none of which the
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

**Height is why it works this way.** A column's top is settled by the rasterized ground, by what an
erected shape says about levelling, raising or sinking, and by the modifier stack run over both — and only
the first is knowable client-side. A preview that extruded each shape to its own thickness drew stage one
and called it the answer, so a mesa read at its own thickness and a hillside read as a plate. Asking the
server is what makes the one view that exists to show height show the height model.

The build is the cost, not the payload: on a full board it is around a second against forty milliseconds to
read the columns back out. Nothing is drawn in 3-D, so the fetch happens on **entering** the preview rather
than on every edit — the view swaps at once and fills in when the columns land. Rotating redraws the mesh
already in hand, and re-entering an untouched board draws it again rather than rebuilding. A play piece written
— placed, moved, turned, resized, a wall's end dragged, a house or its doors changed — touches the board too: it
changes the intent rather than the layout, so the mesh is let go of on every piece write and the next entry
builds again.

In Ground the sidebar carries the active layer's settings — its name and its base Y — and the group→shape tree; the
inspector on the right edits whatever is selected. Which layer is active, and which layers there are, is
the strip's alone: a layer is switched where it is drawn on, so the sidebar states what the layer **is**
rather than offering a second list of the same rows.

### Terraform

One step, and the phase that turns the flat ground Draw made into terrain. It edits one thing, the layout's
**modifier stack**: terraform operations run over the built ground in list order, each inside its mask, so a
modifier changes the ground as the ones before it left it. The left panel lists the drawn shapes and the stack,
the dock holds one tool per operation, the picked modifier takes the inspector, and a strip at the canvas foot
steps the ground through the stack.

**The dock offers the operations in five families.** After Select and Move come four flyout groups, each showing
the operation last picked from it and a chevron that lists the rest: *Ground* (Hill, Hollow, Level, Butte,
Spire), *Lines* (Ridge, Scarp, Canyon, Profile, Road), *Water* (River, Lake, Coast), *Texture* (Roughness,
Lumps, Shatter, Gullies, Warp) and, in a group of its own, *Carve* (Tunnel, Chamber, Shaft).
Picking an operation arms its tool, `modifier:<op>`. Each operation has its own glyph, drawn as a profile or a plan
of the ground (*Icons*, below).

**A modifier is added by tracing its mask.** Pick an operation in the dock, press on the board, drag round the
ground it should shape and let go: the trace is simplified to the points at its real bends and becomes the new
modifier's mask, and the new modifier is picked. Releasing with Shift held keeps the tool in hand for the next
one. *Add an outline* in the inspector (*Add a line* on a modifier whose mask is lines) arms the picked
modifier's own tool, and the next trace joins its mask.

**The line operations trace an open run.** Ridge, Scarp, Canyon, Road and River are dragged as a line rather than
an outline, and the release keeps the run simplified with Douglas–Peucker at the same two-block tolerance. The
mask area is `{ "kind": "line", "points": [[x, z], …], "width": 6 }`: the band the operation works in is `width`
blocks across, half to either side of the run. A drag that travels under three blocks is a misfire and adds
nothing. Every other operation traces an outline, as above.

**With Select in hand the board edits masks the way Draw edits shapes.** Every mask is outlined; a press inside
an outline picks its modifier, the smallest outline winning where they nest, and a press within half a line's
width of it picks that, and a drag moves the whole mask. The picked mask carries a grip on each point, and
hovering an edge offers a new point there, which the press that adds it then drags. A line has no edge from its
last point back to its first. `Delete` removes the picked modifier. The picked mask is drawn bold and its images
under the map's symmetry dashed, a line with its band faintly beside it, and a modifier switched off is drawn faint.

**The left panel lists the shapes above the stack.** *Shapes* has a row for each drawn shape that is ground (a
carve has no ground of its own) with its kind and size, and the relation the modifiers have to it, set from the
row: *Follow* (the modifiers reshape it with the land around it, the default), *Hold* (`relief_scope` `hold`: it
keeps its own height and the modifiers leave it out) or *Ride* (`relief_scope` `ride`: it keeps its form and
moves by the median change the stack made to the ground under it). A shape that states a `height_mode` shows
that mode in place of the choice, because it already meets the stack by its own statement. Setting a relation is
the bridge's `setReliefScope`, so it is one undo step and the contours answer it.

**Below it, the stack is in the order it runs.** A row picks its modifier; its own buttons move it earlier or
later and switch it off, which keeps its numbers and leaves it out of the build. The inspector says what the
operation does and edits its name, its numbers, its mode and soft rim, its seed, and whether the map's symmetry
fans its mask. Every edit, on the board or in a panel, is one undo step.

**The inspector carries every operation's numbers.** Each operation has the fields the table under *The modifier
stack* gives it; Scarp adds a *Raised side* choice (the side of the run, walking along it as it was traced), and
a mask with lines has a *Line width* field for each line; River, Lake and Coast add a *Bed* row that picks the
block or pattern under their water (`bed_material`). *Leaves out* lists the drawn shapes as checkboxes:
each ticked shape's columns are left out of that one modifier (`mask.leaves`), which is how one Hill spares a
plinth that the rest of the stack still shapes. *Changed N columns* under the operation's description is how many
columns the modifier moved, read from `POST …/sketch/modifiers/changes` with the live layout after the same pause
the contours wait for; where the studio cannot answer, the line is not shown.

**The step strip reads the ground part-way through the stack.** At the canvas foot, in 2-D and in the 3-D
picture alike, a range runs from 0 to the number of modifiers that are on, with a label naming where it stands:
*the shapes* at 0, then the name of each modifier in the order it runs. Releasing the thumb sets the step, and the
bridge adds `?step=N` to the contour overlay's read (`sketch/relief?heights=true`) and to the 3-D read
(`sketch/columns`), so both show the ground after the first N modifiers that are on. The last step is the whole
stack and sends no `step`. The step is a view, not an edit: it is not saved and not an undo step, a switched-off
modifier or a deleted one that leaves it past the end returns it to the whole stack, and leaving Terraform does the
same.

**What the stack built is on the board as a shaded height map under contours**, on by default in this phase
and offered as the *Contours* chip in every other. `POST …/sketch/relief?heights=true` returns each group's
built surface (`SketchRasterizer.BuiltSurfaces`): the highest top of each column it forms, read off the build's
own columns with every layer stacked and every modifier run, as one block height per cell of the group's box
with the lines traced half a block above each top. The ramp runs dark-low to light-high over each group's own
range, and the group fill stands down underneath it.

**Icons.** The dock, the list and the inspector draw each operation with a `terrain-*` glyph (`terrain-hill`,
`terrain-ridge`, `terrain-river`, …) from `wwwroot/js/studio/icons/terrain-icons.js`, a hand-written script that
registers them into the vendored lucide set after it loads. `tools/vendor-icons.mjs` generates the vendored file
and leaves that script out of its scan.

#### The modifier stack

**The stack runs inside the build every read takes.** `SketchTerraform.Run`, called from
`SketchRasterizer.RasterizeColumns`, runs every modifier not switched off over the top of each column among the
layers that are not made things, as a world y, so a Level's height and a Hollow's floor are heights a player would
read off `column`. The contours, the paint, the 3-D preview, the export and `column`, `transect`, `slopes` and
`walk` all answer it. A column holds, and no modifier moves it, under a play room, under a shape whose
`relief_scope` is `hold` and under a `level` shape. A `raise`, `sink` or `drape` shape rides, and so does one
whose `relief_scope` is `ride`: it moves with the change the stack made to the ground under it (*Groups and
layers*).

Eighteen operations run. Hill, Hollow, Level and Roughness are ported from pgmvox's ground layer
(`pgmvox/document/layers.py`, `_ground_layer`), Shatter from its broken-ground primitive (`pgmvox/geom/shapes.py`,
`shatter`), and the other thirteen from its landform library (`pgmvox/ground/landform.py`), where each takes a
centre and a radius that the mask supplies as its centroid and `scale`:

| `op` | The screen calls it | What it does to each column inside its mask | Its numbers |
|---|---|---|---|
| `mound` | Hill | raises it by `rise × (1 − e^power)`, `e` 0 at the mask's deepest point and 1 at its rim | `rise` 8, `power` 1.6 |
| `crater` | Hollow | lowers it to a bowl `floor + slope × max(0, e × scale − flat)`, the seam rounded over `melt` blocks | `floor`, `flat` 0, `slope` 1, `melt` 0 |
| `level` | Level | holds it at `top` out to `inner` and eases it back to the ground by `outer`, both in the mask's units | `top` (absent: the median of the ground it holds), `inner` 1, `outer` 1.5 |
| `noise` | Roughness | adds each of its `sizes[]`, `{cell, octaves, rise}`, as value noise from `seed` | `sizes`, `seed` |
| `ridge` | Ridge | raises it by `rise × (1 − e²)` along a `line` or the spine of an area, the foot wandering in from the rim by up to `wander` blocks of half width, by noise | `rise` 8, `wander` 2 |
| `butte` | Butte | holds it at `top` out to the mask's rim, then a cliff `cliff` blocks wide and a talus apron `talus` blocks wide from `apron` of the way up the cliff down to the ground; only lifts, and the cliff and apron lie outside the mask | `top` (absent: the median ground inside + 12), `cliff` 1.5, `talus` 6, `apron` 0.3, `jag` 0.12 |
| `spire` | Spire | raises it to `base + (top − base) × (1 − e)^(1 / taper)`, `base` the ground at the mask's centroid, `e` roughened by `jag`; only lifts | `top` (absent: the median ground inside + 20), `taper` 1.6, `jag` 0.15 |
| `scarp` | Scarp | on a `line`: lifts the ground on the `side` hand (1 right, −1 left, walking along the line) by `height`, rising over `cliff` blocks on the line, and eases the other hand up by `apron` of `height` falling to nothing over `talus` blocks; the lift ends at the mask's edge | `height` 6, `side` 1, `cliff` 2, `talus` 5, `apron` 0.3 |
| `canyon` | Canyon | on a `line`: cuts a floor `bed` of the half width across, `depth` under the ground the line follows, its walls climbing to the rim as the distance across to the power `wall`, in steps of `ledges` blocks when it states any; only cuts | `depth` 12, `bed` 0.35, `wall` 2.5, `ledges` 0 |
| `river` | River | on a `line`: cuts a bed `depth` under the ground the line follows that never climbs, held level in reaches and stepping down in falls (two blocks or more, after ten of reach), banks easing back to the ground over `bank` blocks, the ring beside the water raised to its surface; **makes water** | `depth` 2, `water` 1, `bank` 4, `bed_material` (absent: the default bank) |
| `lake` | Lake | in an area: cuts a bed `depth` under the surface in the middle and a block under it at the rim, holds the ground within `shore` blocks outside at the surface or above, easing back to the ground; **makes water** | `level` (absent: one under the ground a tenth of the rim stands at or below, so one cleft in the rim does not drain it), `depth` 3, `shore` 3, `bed_material` (absent: the default bank) |
| `road` | Road | on a `line`: grades the ground along it to climb or fall no more than `grade` blocks a block, its ends holding the ground they stand on, level across the mask's width with shoulders easing into the ground over `shoulder` blocks | `grade` 0.25, `shoulder` 4 |
| `gullies` | Gullies | in an area: cuts `count` grooves running outward from its centroid between a quarter of `scale` and `scale`, each up to `depth` deep, bent by `wander` and narrowed by `sharpness`; only cuts | `count` 8, `depth` 3, `wander` 0.9, `sharpness` 3 |
| `warp` | Warp | resamples the ground inside at positions shifted by noise, up to `amount` blocks, in swirls `cell` blocks across | `amount` 6, `cell` 24 |
| `profile` | Profile | on a `line`: sets the ground by its distance from the line. Out to the first step the floor is the ground, or `floor` where stated; each of `steps[]`, `{start, end, rise}` in blocks out, eases from the level before it to `rise` over the floor and holds there beyond, so the first cliff rises out of the floor and never cuts it. The nominal floor a `rise` counts from is `floor`, or the median ground along the line. `talus` lifts the floor over that many blocks before the first step by `apron` of its rise, `jag` moves each step's edges in and out by noise, and `side` (1 or −1) keeps one hand of the line. The last step's plateau holds to the band's edge and stops there, so a Profile wants ground at that height behind it — land drawn that high, or a Level, Butte or Hill under the band's far side — or a `cut` Profile carved into higher ground; drawn over low ground with nothing behind, each edge is a free-standing wall | `steps` (absent: two, from a quarter to two fifths and from three fifths to three quarters of the half width, rising 6 and 12), `jag` 1.5, `talus` 0, `apron` 0.3 |
| `coast` | Coast | in an area, the sea: the floor falls from the shore `slope` blocks a block to `depth` under the surface; outside, the land within `shelf` blocks eases down to a beach one over the sea, low land beside it lifted to the beach; **makes water** | `level` (absent: one under the ground a tenth of the rim stands at or below), `shelf` 10, `depth` 6, `slope` 0.35, `bed_material` (absent: the default bank) |
| `lumps` | Lumps | in an area: strews up to `count` lumps whose middles fall inside it, each a hill on an ellipse `across` blocks long, its short axis `stretch` of that, turned at random or within 25° of `angle`, its foot roughened by `jag`, `heights` blocks high and falling as `1 − e^power`; overlapping lumps add; only lifts | `count` 12, `across` [6, 16], `heights` [2, 5], `stretch` [0.5, 1], `jag` 0.25, `power` 1.8 |
| `shatter` | Shatter | breaks the ground inside into pieces about `cell` blocks across, their seams bent up to `wander` blocks; each piece is lifted or dropped up to `amount` and tilted up to `tilt` blocks a block, and within `gap` blocks of a seam the ground is cut up to `depth` into a cleft | `cell` 10, `amount` 3, `tilt` 0.25, `gap` 1, `depth` 4, `wander` 3 |
| `tunnel` | Tunnel | on a `line`: **carves** a passage, a tube the line's `width` across whose axis runs 0.45 of its radius over a floor that lies level between the ends — each end's floor the ground there, a point's own third number `[x, z, y]` where it states one, a straight run between — dipped `sag` at the middle and breathing `wander` of its width; never nearer the surface than `cover` blocks, except within `mouth` blocks of an end, where it opens | `cover` 3, `mouth` 4, `sag` 0, `wander` 0.2, `dress` true |
| `chamber` | Chamber | in an area: **carves** a dome `height` high over its `floor`, falling to the rim, its wall roughened by `jag`, never nearer the surface than `cover` | `floor` (absent: `height` and `cover` under the lowest ground inside), `height` 6, `cover` 3, `jag` 0.15, `dress` true |
| `shaft` | Shaft | in an area: **carves** a well from its `floor` up to the sky, a `ladder` up the wall at the south of its middle | `floor` (absent: 16 under the lowest ground inside), `ladder` true, `dress` true |

**Which masks an operation reads.** Scarp, Canyon, Profile, River and Road read a `line` and nothing else; Butte,
Spire, Lake, Coast, Lumps and Gullies read an ellipse or a polygon; Shatter reads an ellipse, a polygon or the
board; Ridge reads a line, an ellipse or a polygon; Warp reads any. An operation
handed an outline it does not read changes nothing and raises no refusal. Hill, Hollow, Level and Roughness read
every kind, a line included: their distance `e` is measured from it. Canyon, River and Road follow a line as a
Catmull-Rom curve through its points rather than as straight segments. Heights an operation states (`top`, `floor`,
`level`) are world y in the unit of a column's top: a Level at `top` 12 and a Lake at `level` 12 meet flush. A
range (`across`, `heights`, `stretch`) is `[least, most]`, one number standing for both.

**Every modifier states the same five things around its numbers.** `id` is what the list and every read name
it by. `name` is what the author calls it. `off: true` switches it off and keeps its numbers. `mode` keeps half
of the change: `lift` only what raises, `cut` only what lowers, and absent both; a Hill with a negative `rise`
and `cut` is a dish of its own shape. `soft` fades the whole change in over that share of the mask, measured
in from its rim.

**A mask is one or more areas, and the map's symmetry fans them.** An area is an `ellipse` (`at`, `rx`, `rz`,
`angle` in degrees), a `polygon` (`points`), a `line` (`points`, `width`) or the whole `board`. Its distance `e` is
0 at its middle or deepest point and 1 on its rim, and `scale` is how many blocks one unit of `e` spans: the larger
radius of an ellipse, the deepest point of a polygon. A line is an open polyline: its `e` is the distance to it over
half its `width`, so `e` is 1 at `width / 2` and `scale` is `width / 2`, and the operations that vary along a run
(River's fall, Road's grade) read the arc length of the nearest point on it, and Scarp the hand a column lies on.
With `mask.mirror` (the default) each area also stands at its images under the map's symmetry, through
`PgmStudio.Geom.Symmetry`; an image that falls on an area the mask already holds, as a centred ellipse does under a
half turn, counts once. A Scarp's image lifts the mirror image of the ground its original lifts, so a line lying
on a mirror axis is two areas, one lifting each hand. **An image carries the same noise and the same pieces as the
area it is an image of**: every operation that reads noise or scatters by seed reads it at the point of the drawn
area the column is the image of (`MaskArea.ToDrawn`), and a `board` under a symmetry reads it at the member of
each column's orbit that stands for all of it, so a Roughness, a Lumps or a Shatter is as fair as the map. Where
a Hill's or a Hollow's areas overlap, `join` adds them: 0 keeps the larger change, and above 0 they sum as a
p-norm, so the saddle between two hills fills into a ridge. Any other operation keeps the larger change of its
areas column by column.

**A carve cuts the built world, after the paint, and keeps the ground over it.** Tunnel, Chamber and Shaft move no
column's top, so the stack passes over them — they read no step and count no changed columns — and the world build
cuts them in list order once the painter has finished the ground (`SketchRasterizer.CarvesOf`, `CaveCarver`): a
cave's walls are the theme's fill, and the relief over it stays. A carve never cuts water, bedrock or a column a
stamp claimed, so a wool room or a monument is never undermined, and its areas fan by the symmetry like any mask's.
With `dress` (the default) what it cut is dressed as pgmvox's `dress_cave` dresses a cave: floors of gravel,
andesite, clay and cobble, a mushroom here and there, stalactites from the roof and coal and iron in the faces,
every draw hashed at the folded cell so the two halves of a board match. The sketch's 3-D view draws the ground's
top and so does not show a carve; `render/eye`, `column` and `transect` read the built world and do.

**River, Lake and Coast make water, and the world build fills it.** Each wets the columns it cut and states the
height its water surface stands at: a River's channel stands at its bed plus `water`, a Lake's cut at its `level`,
a Coast's sea at its `level`. A wet column is dry again where a later modifier raises its ground to the surface
or above, so the stack answers only the cells that hold water at its end (`SketchRasterizer.WaterOf`), cached with the columns so a read of the water and of the ground
cannot disagree. Each cell answers its surface and the id of the modifier whose water it is (`WetColumn`). The world
build hands that map to the dressing pass, which fills it before any prop is placed and
through the routine a `basin` fluid uses (*Decoration*): each wet column takes water from its ground top up to the
block under its surface, writing only into air, and a beach follows the water's own edge. Water is therefore part of the world every read sees — `sketch/columns`, `column`,
`transect`, the export and the walk's swim set — and a later River over the same columns replaces the water, and
the ownership, of an earlier one. The cells carry the fan of the map's symmetry already, so nothing is mirrored a
second time.

**A water's own bed is the modifier's `bed_material`.** The floor under the water and the beach at its edge are laid
with the material the modifier states, a terrain material in the form a theme's buckets take (`{"kind": "solid",
"id": 13}`, a pattern, a band stack); the inspector's *Bed* row picks a block or one of the library's patterns. The
word is not `bed`, which is a Canyon's flat floor share. A modifier that states none lies on the default bank, a
patchwork of gravel, coarse dirt and sand. The dressing pass floods the cells one bed at a time, so a river and a
lake of two beds each keep their own and their beaches are measured from their own water. A `bed_material` that
does not read as a material is dropped and the water lies on the default bank; it is not refused.

**Water that runs off the ground falls.** A wet column with a side that has no ground — a cell no layer built
terrain on, the board's edge or a hole in it — pours a column of flowing water (id 8, data 8) into the cell past
it, from the block under its surface down through air to the lowest floor of the ground the board built, and stops
on whatever stands in its way. A River drawn out to the edge therefore ends in a fall rather than a wall of
still water. Only the stack's water does this; a placed `basin` or channel prop meets the void as a wall
(`DR-DRY`), and the falling columns are in the void, so no read-back counts them as the water's cells.

**Water that touches is one body, and the stack settles it before it answers.** Where a River runs into a Lake,
or any two waters meet at different surfaces, the lower takes the higher as far as its own wet columns reach, so
the junction stands at one level rather than a step of water hanging over a channel, and each column that takes a
surface takes the modifier it came from with it, and with that modifier's bed. Then every dry column beside
water is raised to that water's surface, so no water stands over its bank where a later modifier — a Roughness,
a second body's cut — left the bank lower. Both run after the whole stack, whatever order the modifiers take.

**A mask can leave shapes out.** `mask.leaves` is a list of shape ids: the columns of those shapes, and of their
images under the map's symmetry as a held shape's are counted, are out of that one modifier's run, so it neither
moves them nor reads them (a Level's median ignores them). Every other modifier still shapes them, which is what
separates `leaves` from `relief_scope: hold`. An id the document does not hold leaves nothing out.

**The stack can be read a step at a time.** `?step=N` on `POST …/sketch/relief` and `POST …/sketch/columns`
reads the ground after the first N modifiers that are not switched off: 0 is the shapes alone and a number past
the end is the whole stack. `POST …/sketch/modifiers/changes` takes the layout and answers
`{ "modifiers": [ { "id", "columns" } ] }`, one entry per modifier in list order: how many columns its own run
moved, a column counting when its top, rounded to a block, differs from what the modifiers before it left. A
modifier switched off, or a word this build does not know, answers 0. The stack runs once for all the counts.

```json PUT /api/map/{slug}/sketch
{
  "setup": { "mirror_mode": "rot_180", "center": { "cx": 0, "cz": 0 } },
  "layers": [ { "id": "ground", "name": "Ground", "base_y": 0, "layout": { "shapes": [
    { "id": "s0", "type": "rectangle", "operation": "add",
      "min_x": -60, "min_z": -30, "max_x": 60, "max_z": 30, "floor": 0, "base_height": 10 } ], "groups": [] } } ],
  "modifiers": [
    { "id": "west-hills", "op": "mound", "mask": { "areas": [
        { "kind": "ellipse", "at": [-40, 8], "rx": 14, "rz": 10 },
        { "kind": "ellipse", "at": [-26, 14], "rx": 9, "rz": 8 } ], "mirror": true },
      "rise": 9, "join": 2, "soft": 0.3, "seed": 1 },
    { "id": "brook", "op": "river", "depth": 2, "water": 1, "bank": 4, "mask": { "areas": [
        { "kind": "line", "points": [[-58, -20], [-20, -12], [20, -18], [58, -26]], "width": 6 } ], "mirror": true } },
    { "id": "plaza", "op": "level", "mask": { "areas": [ { "kind": "ellipse", "at": [0, 0], "rx": 10, "rz": 7 } ],
        "mirror": false }, "top": 12, "seed": 1 },
    { "id": "rough", "op": "noise", "mask": { "areas": [ { "kind": "board" } ] },
      "sizes": [ { "cell": 12, "octaves": 2, "rise": 1 } ], "seed": 7 }
  ]
}
```

### Palette

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
and the snapshot records the row it came from (`themeSources`, keyed by the board's theme id). That note is
what a second copy of the same row matches by, so copying it in again refreshes the copy it already made
rather than defining a second theme beside it — and it holds whichever of the two has since been renamed,
where a match by name would have made a renamed pair into two themes with nothing saying they are one. A row
the board has a copy of is badged with the name that copy carries, and its action reads **Update** rather
than **Add**.

**With a theme in hand the canvas is a brush**, and the modifiers are read against what is held rather than
against the grouping. A click paints the shape under it and puts the brush down, leaving that shape selected;
`Shift` held keeps the brush in hand for the next stroke; `Ctrl`+click widens the stroke to every shape the
group holds; `Alt`+click lifts that shape's own theme back into the hand. A click on no shape puts the brush
down too. There is no apply button and no
scope control — with an empty hand the usual selection rule applies unchanged, and the tree reaches a group
either way. A shape carries the assignment (`shape.theme`), a group stroke writes it to every member, and a
cell that carries none falls to the map default, so the resolution is shape, then map. `Escape` puts the brush
down — a thing in hand is the first thing it lets go of — and so does leaving the phase.

**The inspector says what is in hand, what the selection carries, and — with neither — what the board falls
back to.** Palette, badged **in hand**: where the theme was copied from and whether it still says what that
row says — the row is read and the two documents compared, so a snapshot that has been edited on either side
says so instead of reading as current — the sample plateau the theme finishes, and **Remove**, which takes it
off the board. A theme is edited in the library, never here. Selection: what that shape or group is painted
with, `mixed` where a group's shapes disagree, and **Unpaint**. Map defaults, when nothing is selected and
nothing is in hand: the default theme, the board's biome and the two room shells, in one section rather than
two. **Every row there is one library row bound to
the whole board** — the same statement four times over — so each row carries its own button through to the
library that holds it and each says what it is bound to by name. The pictures are the library's, and a section
describing what the board falls back to is a list of bindings rather than a gallery — no row of it draws what
it is bound to. The order is the order a column is built: the paint under everything, the tint over it, then
the finish stamped where a room stands.

**The paint rules follow the bindings, because they are the board's own and no library row's.** *Paint rules*
lists the board's `paint[]` in the order a column meets them: each with its on toggle, its name, the arrows that
move it earlier or later and its removal; what it lays (a block or a library pattern); *Steeper than* and *At
most* in degrees; *From y* and *To y*; *Near* one of the board's modifiers *Within* so many blocks of its mask;
how many *Courses* it lays; and *In patches*, a noise *Patch size* wide that leaves a *Bare share* of the ground.
Every edit replaces the list in one undo step and the Blocks overlay repaints. A rule naming a modifier is how a
River's bank becomes sand: `docs/world-export/terrain-painting.md`, TP28, is the rule itself.

**The map default is the board's; the built-in is stone.** Every bucket of `TerrainTheme.Default` is stone —
what unpainted ground already is — so a board that names no theme exports as a board that names no theme, and
a bucket a theme leaves unbound resolves to stone rather than borrowing a finish it never asked for. The
finishes worth starting from are named themes: the library's seed folder holds eight — `meadow`, `dunes`,
`ashfall`, `firnline`, `claybed`, `oldstone`, `clay grassland` and `clay mycelium` — and the seed puts them in the
library, where they are picked like any other (`docs/tools/library.md`, *The seed*).

**The map's biome sits with the default theme**, because it is the same kind of statement: what every column
falls back to. A biome places no block — it is the byte a client reads to tint grass, leaves and water — so it
is neither a prop nor a per-shape choice, and what the phase offers is a **select over the biome library**.
The kinds, the scale and the palette are the library editor's; picking one here copies its field onto the
board and records the row it came from (`biome`, `biomeSource`), so editing the library afterwards retints
nothing. The select resolves what is held by that note first and by the field's own content after it, so a
field written straight to `PUT …/sketch/biome` reads as the preset it is; one the library does not hold says
so rather than reading as none. `(none — plains everywhere)` is what a board that never opened the question
exports as. `docs/world-export/terrain-painting.md` §5b is the field itself.

**Room shells** are two more of those bindings, and they are a fallback in the same sense the default theme
is: one shell for every wool cage and one for every spawn cube. What separates them from the theme and the
biome is reach, not kind — a shell finishes only where a room stands — which is why they are rows in the same
section rather than a section of their own, and why a bound shell says its name and draws nothing: the plan
and the section of a shell are the house library's pictures, and `POST /room-styles/preview-snapshot` is what
draws one from a snapshot outside the UI. Two bindings and no more — rooms are fanned
across the symmetry orbit so both sides face the same building, and a per-room shell would be a sightline that
differed between teams. What is stored is the composed style's **JSON snapshot**, not the library id it came
from, so editing the library afterwards cannot rebuild a shipped map's rooms. Each binding has three states,
and they are genuinely distinct: absent stamps that kind's built-in shell, an object stamps the bound style,
and an explicit null means no building at all — a pad on open ground. The finish records the shell but not
the row it was snapshotted from, so the select says which row is held by matching the snapshot against each
library row's **document**, the way the biome select does: a board read back after a reload, and one written
straight to the endpoint, resolve the same way one bound in the phase does. A snapshot no row matches reads
as `(a shell the library does not hold)` rather than as the built-in one.

```json SketchLayout
{ "roomStyles": { "wool": { "roof": { "form": "gable", "pitch": 1 } }, "spawn": null } }
```

That map stamps its wool cages with the bound style and gives its spawns no building at all. Leaving `spawn`
out entirely — rather than writing `null` — is the third state, and stamps the built-in spawn shell. The built-in shell is the placeholder a board is drawn with, so every build of a board whose rooms of a
kind still stand in it answers `WX14` on `warnings`, once per kind, naming the rooms and the `roomStyles`
field to bind (`docs/world-export/structures.md` §9).

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
which is what the Decoration phase's building prop stamps too.

### Decoration

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
a boulder is bedded into the ground it stands on, how a tree is built or copied, and how a channel is dug.
Two of those reach further. A tree's foliage is scored against `tree-corpus.md`, the 75 hand-built trees that
are the measured ground truth for what a tree looks like; and the building prop stamps `structures.md`'s
house, which is why what it can be made of runs past what this phase can state.

Seven things can be placed, in three placement geometries.

| Tool | Kind | Placed by | Starts as |
|---|---|---|---|
| Stroke | `stroke` | tracing a line | gravel, radius 3, `solid`, coverage 0.7, paint rather than a route |
| Fluid | `fluid` | tracing a line | water, a `canal` radius 3, cut 2 deep, a 2-block shore over a Voronoi bank; `fluid: lava` fills the bed with lava, `shape: pool` fills a drawn ring instead, `shape: basin` fills only the ground in a ring lower than its `level` and cuts nothing, and `level` states the world Y the fluid stands at |
| Ground cover | `flora` | tracing a ring | coverage 0.45 at scale 12, with fern and flower shares |
| Building | `house` | dragging a rectangle | no style of its own until one is picked from the room-style library |
| Tree | `tree` | a click | the built-in recipe, an oak 12 tall, until a tree is picked |
| Boulder | `boulder` | a click | the built-in recipe, a mossy erratic of size 4, until it is tuned |
| Chest | `chest` | a click | on the ground, fronting `negZ`, holding nothing until its stacks are listed (`docs/world-export/decoration.md` §8a) |

**A building grows a wing the way an outline grows a point.** A picked building shows the outline editor's round
insert marker at the middle of the free wall nearest the pointer — a run of wall no other wing stands against, at
least 4 blocks long — and a press on it adds a wing along that wall, the wall's length and 5 blocks deep, shrunk
short of any building it would run into (`shared/building.js`). The new wing is picked, and the transform box on it —
a grip on each corner, and a handle along each edge that shows while the pointer is on it — resizes it; a drag
never adds one, and a press inside another wing picks that one. The inspector lists the wings, deletes any but the last or one the others meet
through, and sets the picked wing's **Storeys**, **Roof**, **Ridge** and whether its roof crosses the wing it meets
(`SketchHouseWings`). A room's house in Layout is the same building and takes the same gestures (Layout, below).

**Every one of them takes a style, and for the two that lay ground the style and the material are separate
questions.** A **stroke** replaces the surface it crosses rather than adding to it — it is a finish, not
terrain, which is why it carries a material and no height. Its `style` shapes the *band*: `solid` holds one width the whole way for a clean utility road, `worn` thins it by
a per-cell dice (that is what `coverage` spends), `rough` wanders the width by a noise field so the outline is
organic rather than ruled, `stones` lays discs at intervals with gaps between them — stepping stones across a
void — and `tapered` runs it fat in the middle and thin at the ends. What fills the band is `pave`, a **full
terrain material**: a solid block, a layer stack, a Voronoi patchwork, a noise ramp, any pattern the painter
offers. The two are independent, so a worn cobble and a solid cobble are both sayable. **`wander`** is the
line's own question rather than the band's: it bends the path up to that many blocks either side of the curve
through its points, swinging side to side every `wanderLength` blocks, with both ends left where they were drawn
— the inspector's **Wander** and **Bend length** sliders, and the canvas draws the bent line
(`docs/world-export/decoration.md` §4).

**`claimsGround` is the third question, and neither of the first two answers it.** A style is a brush; it
says nothing about whether the paving is a thing on the board or a finish on the ground, and the same brush at
the same radius draws a road between two spawns and a grass tongue over a crag. `claimsGround` is off unless
stated: a claiming stroke holds the cells it covers, so a tree keeps three blocks off it and a boulder two
(`DR-ROAD`) and a building may end one but not stand across it (`DR-CROSS`), while paint claims nothing and is
planted over. It is not a claim that players walk here — a protected verge and a road are the same
declaration. Marking every stroke a claiming one is how a board ends up with nowhere left to plant.

**Fluid** is the one prop that changes the ground rather than the surface: it cuts a bed and fills it to a
level line with water or lava, because water laid flat on a surface reads as blue paint. Its `shape` says what its points
mean — a `channel` strokes them as a centerline and takes its width from `radius`, a `pool` closes them into a
ring and fills it, which is the only way to make a harbour or a lake with square corners; on a pool `radius`
is the shelf the bed takes to reach full depth. A `basin` closes them into a ring and cuts nothing: every column
inside it whose ground stands lower than its `level` fills to that level and every other is left as it stood,
so the ground draws the shore and a ring drawn loose round a dug hollow floods exactly the hollow. A basin
states its `level`, and one that does not is refused `DR-DOC`; its beach follows the water rather than the
ring. It fills round whatever stands in it and never cuts under it,
so a ship moored in a harbour floats rather than sitting in a dry hole. Absent a `level` the line is
derived — the lowest surface the channel crosses — and the prop only ever carves existing terrain: the cut
stops at the surface it crosses and never fills what was already air. **`level` states the world Y instead**,
and then the fill reaches it whatever the column beneath is doing, which is what fills a basin dug out in the
shapes: a lake, a harbour, the water a ship floats on has no surface up at the line for a derived one to find.
**The basin is a low floor and not a hole** — the pass skips any column the surface map does not carry, so a
subtract leaves nothing to fill and a harbour is an override add laying a floor at the depth the fluid reaches
down to. The footprint bounds it either way — the pass never floods outward, so the rim is the author's. Its `form` is `canal`
(a clean uniform width, deepest on the centreline), `natural` (the width wandered by noise) or `stream` (the
width pinching and swelling on a beat down the arc, running shallower throughout, so it reads as riffles
rather than one even channel). Around that: `radius` and `depth`, `edge` for how far a natural or stream bank
wobbles, `shore` for how wide a beach the fluid meets the land through with `shoreWander` for whether that
beach opens and closes along the run, and `bank` — again a full terrain material, defaulting to a Voronoi of
gravel edges, coarse dirt inside them and sand in the middle, which shows through the shallows and continues
as the beach.

**A tree and a boulder are recipes in the document, tuned in the sidebar.** What is placed is a point plus a
`style` key; what stands there is a recipe in the document's own registry. The sidebar writes that recipe: picking
a tree's card pulls the library row in under a key read off its name (`docs/tools/library.md`), and moving a
generated tree's **Height** or a boulder's **Shape**, **Size**, **Rock** or **Moss** pulls the tuned recipe and
names it on the placement. A pull of a recipe the registry already holds answers that key, so forty oaks of one
height are forty positions and one recipe.

**A tree is two different things rather than one thing with a switch.** A `template` tree is vanilla: its
`species` — oak, birch, spruce, jungle, acacia, dark oak or willow — names its wood, its canopy profile and its
proportions together, since a notched cone is a spruce, a flat umbrella on a leaning trunk is an acacia and a
low dome hung with curtains is a willow,
and neither is a knob setting of the other; `height` scales the lot. A `copied` tree is one an author built by
hand and cut out of a world, carried as its own `body` of `[x, y, z, id, data]` offsets: it states no species
and no height, because what it looks like is what was built. Each form reads only its own fields, so the
others are inert rather than wrong. The sidebar lists the library's trees in two groups, **Generated** (one
card per species) and **Hand-built**, the hand-built folded after the first nine. A hand-built tree has no
knobs: its card names who built it, and it shows no Height and no Seed.

**A boulder** is a glacial erratic: a mass standing on the ground, bedded a third of its height into it. It
takes a `form` — `round` (a weathered erratic), `angular` (the same rock, its surface broken), `outcrop` (wide
flat lobes with their middle at the surface, a low shelf rather than a rock) or `cairn` (three shrinking lobes
stacked) — a `size` from 2 to 10 blocks of reach, default 4, a `mossy` flag for whether moss specks a tenth of
the sky-lit faces, and `rock`, a full terrain material like a stroke's paving. All four are the recipe's. A rock's material resolves in the
**boulder's own frame** rather than the map's, so a mottled stone carries the same mottling to every image of
its orbit instead of sampling whatever the world pattern says where each image happened to land.

**Ground cover** is a drawn ring of which `coverage` is the share that grows, cell by cell, so a low
coverage is tufts across the whole ring rather than one shrinking patch; `scale` and `octaves` shape the field
that gathers it into thicker and thinner patches (**Patch size**), and the cover is split by `fernShare`,
`flowerShare` (with its own `flowerScale`) and `tallShare` (`docs/world-export/decoration.md` §3).

**A slider moves the picture and waits to move the board.** While a knob is dragged the readout and the
preview follow it — one `prop-preview` in flight at a time, the newest value drawn when it lands — and the
canvas takes the value once, on release, as one patch. A push re-renders the whole tool and marks the board
for saving, which is why it waits for the release rather than following every step.

The pickers show **your** prop rather than a stock one. `GET /terrain/stroke-styles?pave=…` draws the five band
styles in the material already chosen, `/terrain/boulder-forms?rock=…` the four rock shapes in the author's
stone, and `/terrain/fluid-forms` the three channels as actual dug beds — so the question answered is
"what would mine look like", not "what does the catalogue contain". A tree and a boulder are picked from their
library instead, each row drawn through the pass that builds it; a boulder's four shapes are labelled with
the names the library's seeded boulders carry: Erratic, Shattered, Outcrop and Cairn. A stroke's **Paving**, a
channel's **Shore** and a boulder's **Rock** are each picked as a single block or a library pattern and copied
onto the prop; the library's `shore` pattern is the channel's default bank, and a material that is neither
reads as **Custom**. **Seed** is offered only on the kinds that read one: a hand-built tree, a chest and a
building build the same whatever it is. `POST /terrain/prop-preview` renders one
on a flat sample patch with one ground, so a placed prop is drawn there whatever storey it names — and a building whose wings make no building is refused there with the same `HJ*`/`HP*`
findings the build acts on, rather than drawn as though it would stand.

Two reaches are bounded rather than free, and it is load-bearing: a tree's height is held to 5–40 and a
boulder's size to 2–10. Cost is superlinear in reach — the sample patch a preview cuts is quadratic in it — so
a height of 999 would not draw a tall tree, it would ask for a patch hundreds of blocks across and never
return.

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
shape and `HJ1`–`HJ5` for how its wings meet. The inspector shows the refusal as an open row of rules, each
with its own sentence, so an author who joins two ranges lying side by side is told they meet in a gutter and
to turn one across the other. A join is
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
to the four-block floor a single rectangle always needed, and the whole plan to `MaxFootprint` (192 blocks)
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
| `ridge` | `alongX` or `alongZ`, where the wing's own proportions should not decide |
| `projects` | whether the wing carries its roof across the hall instead of marching into it |

```json POST /api/map/{slug}/sketch/props
{ "kind": "house", "id": "h1", "seed": 1, "wings": [
  { "corners": [[0, 6], [9, 10]] },
  { "corners": [[0, 0], [4, 5]], "spec": { "ridge": "alongZ", "projects": true } } ] }
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

The document is the list of what was placed, in placement order, each entry carrying its own knobs, and beside
it the recipes a tree, a boulder or a building names by key. One of each:

```json SketchLayout
{ "dressing": { "props": [
  { "id": "d1", "kind": "stroke", "seed": 1, "points": [[-36, 0], [-20, 4], [-4, 0]],
    "radius": 3, "style": "worn", "coverage": 0.7, "claimsGround": true,
    "pave": { "kind": "solid", "id": 13, "data": 0 } },
  { "id": "d2", "kind": "fluid", "seed": 2, "points": [[-30, -16], [-16, -12]],
    "radius": 3, "depth": 2, "form": "stream", "edge": 0.8, "shore": 2, "shoreWander": true,
    "bank": { "kind": "solid", "id": 12, "data": 0 } },
  { "id": "d2b", "kind": "fluid", "seed": 6, "shape": "pool", "layer": "ground", "level": 12,
    "points": [[-30, 16], [-6, 16], [-6, 34], [-30, 34]], "radius": 6, "depth": 3, "shore": 2,
    "bank": { "kind": "solid", "id": 12, "data": 0 } },
  { "id": "d2c", "kind": "fluid", "seed": 7, "shape": "basin", "level": 9,
    "points": [[4, 16], [30, 16], [30, 34], [4, 34]], "shore": 2 },
  { "id": "d3", "kind": "flora", "seed": 3,
    "points": [[-38, 8], [-26, 8], [-26, 18], [-38, 18]],
    "spec": { "coverage": 0.45, "scale": 12, "octaves": 3,
              "fernShare": 0.25, "flowerShare": 0.18, "flowerScale": 18, "tallShare": 0 } },
  { "id": "d4", "kind": "house", "seed": 4, "wings": [ { "corners": [[-22, -16], [-14, -8]] } ],
    "front": "negZ", "style": "croft" },
  { "id": "d5", "kind": "tree", "seed": 5, "x": -32, "z": 2, "layer": "ground", "style": "birch-12" },
  { "id": "d6", "kind": "boulder", "seed": 6, "x": -8, "z": 14, "style": "cairn-3" }
  ],
  "styles": {
    "croft": { "kind": "house", "shell": { "roof": { "form": "gable", "pitch": 1 } } },
    "birch-12": { "kind": "tree", "form": "template", "species": "birch", "height": 12 },
    "cairn-3": { "kind": "boulder", "form": "cairn", "size": 3, "mossy": true,
                 "rock": { "kind": "solid", "id": 1, "data": 0 } }
  } } }
```

The three geometries are visible in the shape of the entries: a marker carries `x`/`z`, a traced prop carries
`points` — a line for a stroke or a channel, a closed ring for ground cover — and a building carries its
wings, each the two opposite corners of a rectangle. `pave` and `bank` are full terrain materials, so any of the fourteen kinds
in `library.md` may stand there.

**What a prop is *made of* is named once, beside the placements.** A tree, a boulder and a building each carry
a `style` key into the document's own `styles` registry, and the registry states each recipe once — a board
carrying hundreds of trees over a few dozen recipes stores the recipes and not the repetition, and changing one
changes every placement wearing it. A recipe names its kind the way a placement does, so one registry holds all
three; the key is read off the recipe (`oak-10`, `copied-716`, `angular-6`), so it says what
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

**So a recipe is identified by what it is made of and named by what an author reads it as.** A library row's
name is the key it is pulled in under, and no library table makes that name unique: two rows an author called
the same thing are two recipes, and the key has to tell them apart. Pulling a recipe the registry already
holds is therefore the entry already held, whatever the row pulling it is called — a grove stays one entry —
while a *different* recipe arriving under a name already taken is numbered (`oak`, then `oak-2`), so what a
placement already on the board is made of is never rewritten by a later pick. The row id is deliberately not
the key: a map holds a snapshot and never a foreign key into the library, and a number that looks resolvable
and is not would read as one.

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
carries the pass itself.

**In the Decoration phase a prop takes the layer being drawn on, and the inspector moves it.** A placement
records the canvas layer strip's active layer (`DressingDoc.add`); a prop that already names one keeps it.
With a prop selected on a board of more than one layer, the inspector's **Storey** field offers the strip's
own layers and writes the pick as `layer` through the same `updateProp` patch every other knob takes. Unlike
the other knobs it is not carried into the next placement's starting values, because the next prop takes the
active layer. The field offers every layer and filters none: one with no ground under the prop is declined
`DR-LAYER` by the pass, which is what `POST …/sketch/dressing` and the export's warnings report.

**A prop on another layer is drawn dimmed.** `dressing-render.js` draws a prop whose `layer` is not the active
one at `OFF_LAYER_ALPHA` of its strength, fill, outline and route alike, so a gallery-floor tree and the roof
tree over it read as two floors. Like another layer's shapes it is context rather than a target: a click on
the canvas picks only props on the active layer, and the placed-props list still reaches any of them. A prop
naming no layer, and every prop of a board with no active layer, draws at full strength.

**Every word above is written in camelCase, and that is the canonical form** — what `POST .../sketch/finish`
and the export always write back, and the form every example in this document is in. The reader is more
forgiving than the writer: a prop's own enum fields (`style`, `form`) are matched case-insensitively, so
`"Worn"` or `"WORN"` reads the same as `"worn"`, and `kind` — the discriminator that says which prop or which
material an object is — no longer has to be an object's first key (`DR-DOC`). What `kind` cannot be is a word the
reader does not know: `"boulderr"` or a missing `kind` both refuse the document by name rather than being
silently misread — in a placement and in a recipe alike, down to a material inside a house recipe's shell, whose
refusal names the recipe and the field (`shell.storeys[1].deck`). A document that fails to parse anywhere — one bad field, one unrecognized `kind` — refuses
the whole export rather than exporting with fewer props than it was asked for; see *What it refuses* in
`configure.md`, since the refusal fires at export, not while the sketch is merely saved.

Decoration does not repaint the Blocks overlay, which shows the painter's surface colours — a prop adds blocks
*above* the surface. Its **Next** leads on to Review, where what was placed is seen standing.

### Review

**Review is the board as a player sees it**, drawn by `render/eye` with Minecraft 1.8.9's own block sprites
(`docs/world-scan/read-backs.md`, *Seen from a player's eye*). It is the one phase that asks whether the finish
works rather than where it is: two noisy blocks of one colour are static on the ground and one calm grey from
above, and only a picture in the game's own sprites shows which. The server needs the sprites to draw it; one
without them answers the list with the reason, and the Notes step says so instead of showing a gallery.

**Review is two steps, Cameras and then Notes, and it opens on Cameras.** Cameras is the canvas, where the views
are placed, kept, removed and the map's picture is chosen; Notes is the gallery of what those cameras see, with
the author's notes beside it. On Cameras, **Back** goes to Decoration and **Next** to Notes; on Notes, **Back** goes
to Cameras and **Next** to History, and the step bar reaches either step directly. A link to a note — `?note={id}`
on the route, or a thread opened from History — lands on Notes, on that thread.

**Entering it saves the board's unsaved edits first**, because every picture is of the board as stored. A board that has
changed builds a new world, and every picture is drawn again the first time it is asked for; a board that has
not answers from the pictures already drawn, which the studio keeps on disk across restarts. The gallery's
thumbnails are drawn at 480 × 270 and the enlarged picture at 1280 × 720, so the enlarged one is a drawing of its
own: until it arrives, the thumbnail of the same view stands in for it, scaled up, with *Drawing…* in its corner.
A picture the studio is too busy to draw (`429`) is asked for again after the `Retry-After` it names, up to four
times, before the gallery says why.

**Every board keeps one view of its own: the whole board straight down.** The eye stands over the board's
middle, north at the top, high enough that the board's width fits across the frame and its depth down it, so
almost nothing is hidden and a mark on it reads like a mark on a map while still showing the board as it is
built. It is the first picture in the gallery and the first camera on the canvas, and it is framed from the
built board every time the views are listed, so it follows the board as the board grows. It can be changed like
any kept view, and the change is kept in its place; it is never let go, and deleting a changed one puts the
framed one back.

**Then come what the studio suggests, and then what the author kept.** The suggestions are read
off the built board (`WorldViews`): the whole board from above the middle of its long side; every team's spawn seen
from its front, and the view from it — the spawn as the map's `<spawn>` states it, the eye at its point a
player's height up, facing its `yaw` and level, since the studio writes no `pitch` and PGM's is then 0; every
wool room, destroyable, core and control point; the first four buildings
the dressing placed; and the first two boulders. Spawns and objectives are shown for every team, since each is
a different team's first sight of the board; a prop is shown once, at the first image of its orbit.

**A building is framed from its footprint rather than by the eye.** The eye finds its own place for a small
thing — a boulder — but what it reads as a thing's ground and top is the columns around it, and inside a
building those are its roof, with a wool room's marker floating over that. So a spawn, a wool room or a house is
looked at from the terrain outside it, on the side facing the board's middle (a spawn's front), far enough back
that its width and height fit the frame, and tipped to the middle of its body. A side whose sight line meets a
hill, a rock or a wall is passed over for a nearer stand and then for the next side round, and so is a stand
so far below the thing that the eye would look up at it more steeply than 20°.

**A goal is framed from the box it was built in.** A destroyable or a core is seen from its box and a control
point from its pad, the ground under it to the box's top, so the sky marker hanging over the goal is never what
the eye aims at. Where no stand on the ground sees a building or a goal — a monument down in a crater — the eye
rises into the air over the same stands and looks down at 25°, 45° and then 65°; where none of those is clear
either, it is left to find its own place.

**Cameras is the canvas with the camera tool armed, and every view on it.** Each
view is drawn as a camera where its eye stands, and nothing more — the views kept in full, the studio's
suggestions faint. A suggestion that leaves the eye to find its own place is drawn where the eye resolved to on
the board as built, which `GET …/views` answers as each view's `eye`. The same cameras are listed beside the
canvas, kept first and then suggested, each with the block it looks at.

**A press on empty ground places a new view.** It stands the eye and a drag turns it toward what it looks at; a
click without a drag names only what to look at, and the eye finds its own place. The inspector then states the
camera whole, in the terms and ranges PGM's spawn `yaw`, `pitch` and `angle` are written in, with every field
filled from where the studio resolved the eye to. **Position** is X, Y and Z, the Y being the eye's own
height. **Facing** is a yaw from −180 to 180 (south 0, west 90, north 180, east −90, as the game's `F3` screen
shows it) and a pitch from −90 straight up to 90 straight down; a yaw typed outside the range is wrapped into it.
**Angle** is the block the camera looks at: it is read off the middle of the picture, and stating one turns the
camera onto the middle of that block from where it stands, the way PGM's `angle` overrides a spawn's yaw and
pitch.

A line under the stand says what its height is: at a player's eye over the ground, so many blocks over it, or
over the void, and **Move to eye height** puts a raised eye back at a player's height. A line under the angle
says where the middle of the picture lands when that is not the angle stated — the block is open, or something
stands before it. Changing any field draws the
picture again, and **Keep** stores the camera as stated and lists it, with the tool still armed for the next.

**A press on a camera picks it up.** Pressing it and dragging stands it elsewhere; a click, or choosing it in
the list, opens it as it is. Only the camera in hand shows the line it looks along and the target at its end,
and dragging that target turns it. The inspector opens with
the view's own name and camera — for a suggestion that found its own place, the camera it resolved to — and the
canvas follows what the fields state. A kept view is changed in place with **Save** (`PUT …/views/{viewId}`), while a suggestion is kept as a
new view of its own, since a suggestion is not stored and cannot be changed.

**One view is the map's picture: the `map.png` a server lists the map by**, and it is chosen here. An export
draws it at 290 × 246 through the same eye, from the view marked as the picture or, where none is, from the
whole board seen above its long side (`docs/world-export/sketch-world-export.md`, *Delivery*). A row's picture
button marks its view — a kept one in place, a suggestion by keeping it — and so does the camera in hand's
**The map's picture (map.png)** box when it is kept or saved; unticking it on the view the picture is drawn from
hands the picture back to the overview. The view the picture is drawn from wears a *map picture* badge in the
list, on the line under its name beside the block it looks at, and over its thumbnail in Notes.

**A kept view other than the board's own is removed here**, from its row in the list or with **Remove** on the
camera in hand. Where the studio refuses that, or refuses drawing the map's picture from a view, the refusal is
said under the list.

**Cameras draws the board as it is built, and nothing over it.** The canvas shows the **Board layer** —
the full build from straight above, one pixel a column in the colour of the block on top of it, so the trees, the
houses and the fluids are in it — shaded the way the game's own map item shades, each column against its
northern neighbour, with a light height term over that. It is drawn from the same columns the 3-D preview
meshes (`sketch/columns`), asked for once when the step opens. The shapes, the blocks, the mirror, the chunk
grid, the work bounds, the axis, the group outlines and the objective labels are all left off whatever their
chips say, and so is the symmetry centre's cross; the cameras and the one in hand are what the canvas adds. No chip is offered over it: the shading already reads the lie of the ground, and a
contour traced over the built columns would run round every tree and house as readily as round a hill. Cameras
is 2-D only; the 2-D / 3-D switch is not offered.

**Notes shows one picture large, with the gallery under it.** The enlarged picture is drawn at 1280×720 under
its name; choosing a card in the gallery shows it instead, `←` and `→` step through the gallery, and **Full
size** opens the 1920×1080 picture in a tab of its own. The camera it is drawn with is printed over its top-left
corner the way the game's debug screen prints one, as two lines of values: `XYZ: 0.5 246.6 1.5` and `Facing:
180.0 90.0`, yaw then pitch. Where an open thread compares its pictures, the camera printed is the note's own.

**The phase follows the board while it is open.** It reads the board's changes again every 30 seconds and
whenever the tab comes back into view, and the notes with them. Where a change has landed that the pictures do
not show, a line over the picture names both changes and offers **Draw the pictures again**, before anything is
marked on a picture of a board that is gone.

#### Notes

**An admin leaves a note pinned to exactly the place it is about; an agent answers it on the note; the author
closes it.** Nobody types a coordinate: the anchor carries them. The notes column sits beside the picture for a
caller who may read and answer notes — an admin, or a token an admin issued with the notes permission
(`docs/access.md`) — and nobody else sees it. Notes exist in Review's Notes step, Draw's Places step, where a picked
place's panel carries that place's one thread, and History, which lists the threads written in a
span; no other phase, page or rail item shows them, and the sketch surface never draws them.

**A note is one piece of text with one anchor**, and the anchor is what makes it directed. A note written in Review
is written on the picture in view, so every anchor there but `map` is a picture, and each of those keeps the
exact camera the picture was drawn with and the picture's size; a note written in Places is pinned to a place:

| Anchor | Made with | What it keeps |
|---|---|---|
| `point` | the **Point** tool: a click on the picture | the pixel, the block its ray hits and the ground under that block — the leaves of a floating tree, and the grass it should stand on |
| `box` | the **Box** tool: a drag | the two corners, every ground column the rays through the rectangle's pixels hit, and the void they cross |
| `lasso` | the **Lasso** tool: an outline | the outline, every ground column the rays through the pixels inside it hit, and the void they cross |
| `view` | a new note with no mark | the camera and the picture |
| `map` | **Pin to the whole map instead** | nothing spatial |
| `place` | the first message written in a picked place's panel, in Places | the place's id, under `place` |

**A note on ground is tagged with the place the ground falls in.** A `point` takes the place its block stands in,
and a `box` or a `lasso` the place holding most of the columns it hit — inside a box or a ring, or within two
blocks of a point or a line, the last place listed first (`PlaceLookup`) — so the studio stores that place's id
under the anchor's `place`. Such a note stays a thread of its own in Review; the place's thread is only the
`place` one. Review's
Cameras step draws the places over the board, so a camera is stood with them in sight.

**The area is what was seen.** The studio does the projection, not the browser: the column sends the mark in
the picture's pixels, and `render/eye/pick` re-casts the ray through each pixel against the world the picture
was drawn from, answering the ground columns hit as `[x, y, z]`. Ground hidden behind a hill is not selected,
because no ray reached it — which is the honest reading of "I looked at this", and why the straight-down view
and an aerial shot matter.

**A mark over the void still says where it is.** A pixel whose ray hits nothing and points down is met with a
level plane at the median height of the ground the same mark hit, or of the board's ground where it hit none,
and the column it crosses that plane in is kept under `overVoid` as `[x, y, z]` at that level, where the column
holds no block. So "an island here" drawn beside the board names the cells it means, at the height the ground
beside them stands, and `columns` keeps meaning ground that was seen. A ray pointing up selects nothing. The camera is answered exactly, because a view
that leaves the eye to find its own place may find another once the board changes; `render/eye?eye=x,y,z&yaw=&pitch=`
draws it again.

**A picture note keeps the picture.** The board changes after the note, and a view draws the board as it is now,
so the note stores the image it was written on: the browser encodes the picture on screen as WebP at quality 85
and posts it to `POST /api/notes/pictures`, which keeps it once under its hash. A 1280×720 picture is about
100 KB that way, a sixth of the PNG. An agent's reply carries the same camera drawn after its change, and the
two are the before and after. A note whose picture cannot be kept is not sent: the box keeps its text and says
so, since a note on a picture it does not carry points at a board nobody can see again.

**A note is written at the change its picture shows.** `GET …/views` answers the change the board stood at when
the pictures were listed, and every pick answers the change it read. A mark whose pick read a later one — a
drive landed while the phase was open — names no ground on the picture in view, so it is refused with both
numbers and **Draw the pictures again**, which lists the views afresh, draws every picture again, keeps the
note's text and arms the tool the mark was drawn with. The note states that change as its `change`, so a
thread's count of the changes since starts where the author's picture did.

**The tools sit in the dock at the bottom centre of the picture**: Point, Box and Lasso, and a switch that hides
the notes' pins and marks. Arming a tool opens a new note; the column arms none. A mark is projected as soon as it
is drawn, and the new note says what it landed on — the block and its ground, or how many columns and how much
sky — before it can be sent.

**Send is off until there is something to send, and says what it is doing.** It reads *Sending…* from the press
on, and *Note sent* or *Reply sent* beside it once the message lands; a press is one note however many clicks
follow it, and Ctrl+Enter (⌘+Enter) in either box sends it.

**The column works in two steps.** The overview lists the notes on the picture in view under four filters — All,
Waiting (on you), With agent, Resolved, on one line — then the notes on the whole map, then any on a picture the gallery no
longer offers. Choosing a note, by its row or by its pin on the picture, opens its thread, and **‹ All notes**
goes back. A row reads the way a chat app previews a thread: what the note is pinned to, then its status as a
muted word and as much of the last message as fits, then at its foot the note's pin and how many messages the
thread holds. A pin is an empty speech bubble, one colour in the column and on the picture, carrying no number;
the status is a word, never a hue. A
thread is a head — the pin, what the note is pinned to in at most two lines, and its status under that — then its
messages top to bottom with their pictures, and the reply box at the foot.

**A thread reads as a chat.** It opens on its newest message and follows each one that lands. A message is a
bubble beside its writer's mark, under their name, when it was written and the change it was written at; a
message following one by the same writer within ten minutes, with no change landed between them, continues it
as a bare bubble under the first. A
person is their head and their name, and the reader's own messages sit on the right in the accent's tint. An
agent is named by its token's label beside the agent mark — a robot on a violet disc, the same mark the
maps page draws for an agent's credit — in a violet bubble on the left, and the hover says
whose token it wrote with: a token acts as the person who issued it, and naming the agent by that person would
show one writer answering itself. A new note shows
what it is pinned to over its text box. The gallery counts each view's notes that are not
resolved, and choosing another view switches the overview with it. `?note={id}` on the route opens Notes on
that note's thread, which is the link a ruling written into `docs/gameplay/approaches.md` carries back to where it
was decided. The link is opened once and then leaves the address, so coming back to Review shows what the
author was reading.

**The reply box is the field, with a footer under it.** The footer holds what the thread can be closed as —
**Resolve**, **Won't do** or **Reopen** — on the left and **Send** on the right, so neither scrolls away with the
thread. A line naming the mark sits over the field only while a reply carries one, and *Reply sent* shows there
once the message lands.

**An open thread compares its pictures on the big picture.** A note written on a picture shows that picture in
place of the gallery's, with **Before**, **After**, **Now** and **Wipe** over it: the picture the note was written
on, the after the latest reply carries, and the note's own camera over the board as it stands. Wipe lays the
after over the before up to a seam dragged across the picture, and is what a thread with an after opens on; one
with none opens on Now. All of them are one camera at one size, so the note's mark lands on each where it landed
on the before.

**A reply can point as well as say.** With a thread open, Point, Box and Lasso mark the reply rather than start a
new note: the mark is read on the picture in view — the note's own camera over the board as it stands where the
thread compares its pictures, the gallery's view otherwise — and the reply box says what it landed on, with
**Clear mark** beside it. The reply keeps it as `mark`, in the shape a note's anchor is kept in, so "no, *this*
one" arrives as the block or the columns rather than as a sentence. Every reply's mark on the picture in view is
drawn in violet while its thread is open, and its bubble names the mark under the text.

**A thread has one status, and only the author closes it.**

| Status | Set by | Means |
|---|---|---|
| `open` | a new note, the author's reply, or the author reopening | waiting for an agent |
| `answered` | an agent's reply | a change landed, waiting for the author |
| `needs-info` | an agent's reply, or a note an agent opens | the agent could not act without more, and asks what |
| `wont-do` | an agent's reply with its reason, or the author | stays visible, and the author can reopen it |
| `resolved` | the author | done |

The reply box's footer offers **Resolve**, **Won't do** and **Reopen** where each applies. A reply leaves the thread where its `status` says; absent, a reply written with a token is
`answered` and one written in a browser is `open`. An agent never resolves, declines or reopens by `PATCH` — it answers, asks or
declines in a reply — so a fix that looks right in a number but wrong in the game is caught by the person who
asked. An agent may open a note too, as a question in the place it is about, and it waits for the author the
same way.

**A thread marks where the board changed between its messages.** A message records the latest change to the
board's documents when it was written, or the one its writer stated. Where changes landed between two messages —
an agent's pass, the author's own edit, a restore — a rule across the thread between them counts them, so the
count sits right above the reply that came after them; where changes landed after the last message, one more
rule closes the thread. Each is a link that opens History on that span.

**A note carries no tag.** One message often speaks to the ground, the look and the play at once, so what a note
is about is the agent's to read off its text and its mark: work on this map, a fault in the studio that becomes a
backlog task, or a gameplay ruling that holds on every map, which once resolved is written into the gameplay law
with a link to its thread. The agent's reply says how it read the note, so a wrong reading is caught on the
thread.

### History

**History is the board's changes, what each one did, and the way back to any of them.** The column lists every
change to the board's documents newest first — its number, what its writer said it is or which documents it
wrote, and when; hovering a row says who wrote it and with which token. The phase opens on the latest change,
and picking one draws what it did over the board with the canvas as a selection surface and Shapes on: a shape
added in green, taken away in red, reshaped in amber with its old outline dashed beside the new one, and changed
in any other field — a theme, a floor — in blue; a prop placed, removed or moved, a hollow dot where it stood and
a filled one where it stands; and the runs of columns whose ground, surface block or structure the build moved,
boxed in the colours `docs/world-scan/read-backs.md` names.

**The inspector says the rest.** Who wrote the change, when, what they said, and where its documents were built,
linked to the commit and folder where the writer stated an origin, and the earlier changes it dropped where it
was a source that named some; then a legend naming the four shape colours, and every edit it made to the four
documents, one a line, the thing it is about beside the change in words; then how many columns it moved of
each kind. The edits come first and the columns when both boards are built, which on a large board is seconds.
For a caller who may read notes, the threads with a message written in the span are listed under it — the
replies a change answered with, and the notes written on its board — and each opens its thread in Review's Notes.
Leaving History and coming back draws the span it showed again.

**Putting the board back writes the documents as they stood at the start of what is shown, as one new change.**
For a single change that is the change before it, so the button takes the change back — and every change after
it, which the inspector counts before it is pressed. The tab saves what was drawn in it first, then takes up the
board the restore wrote, so the canvas and the store agree afterwards; a tab whose board is behind the stored one
is refused until it is reloaded. The four routes underneath are `docs/tools/flow.md`'s, *Every change a map
keeps*.

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
And they answer for a brush finer than the blocks it paints (`PT3`): a `cellSize` or a `scale` under two is a
pattern varying faster than the ground can show it, which reads as noise at any distance whatever the palette
holds. Every pattern in the tree is asked, so a fine field nested in a coarse voronoi's band is named where it
sits; a `checker`'s square and a wall run's stripe are drawn rather than sampled and are not asked at all.
And they answer for a field on a **face** with no vertical period (`PT4`): a pattern of the plane gives every
block in a column the same answer, which is a fabric from above and vertical stripes edge-on, so a `rise` is
what gives a face its grain. Both tall buckets are asked — the wall where it paints, and the fill always,
since the fill is what every cut through a board leaves and is where a shape's own `material` lands. Every
band of a stack is asked whatever its thickness: a rise is never nought on a face.

**A dressing document that will not parse is refused here too**, rather than at the export that reads it
again: the gate cannot judge a style it cannot read, but *that* is the finding, and it carries the JSON path
the binder gave up at — a polymorphic material stated in the wrong shape names the field it is in
(`$.shell.storeys[1].deck`) instead of arriving as an unlabelled failure after the ground is laid.

**A part of the layout is a resource, and the whole layout is not the unit of an edit.**
`sketch/layers`, `sketch/groups`, `sketch/shapes`, `sketch/props`, `sketch/themes`,
`sketch/room-styles` and `sketch/biome` each read and write one part at a time — the shape the objectives
already answer in (`POST /map/{slug}/wools`, `PATCH /map/{slug}/wools/{woolId}`) — so a caller moving one
shape does not send back every other shape, every theme and every prop. Two things follow from the routes
rather than from the edit.

**The address is the id the document already carries.** A shape id is unique across the whole stack, so a
shape answers to it wherever it is drawn; a group answers under the layer that carries it, because the fan
is read off the shapes of one slab; and a layer that named itself keeps its id while
one that did not answers under its position, which is the name the rasterizer gives it anyway. Nothing new is
minted to make a part addressable, which is what lets a caller read a board with `GET /sketch` and edit it
without a second vocabulary.

**A partial write answers for the whole document.** Each one runs the gate and the check `PUT /sketch` runs,
in the same two registers: a style or theme its own materials cannot honour **refuses** at 400, and
everything the document says that the build cannot is a **complaint** riding back on the 200. Anything less
would make the small route the way to get past the big route's gate.

**And a typed body is what publishes the model.** `PlacedProp` on the wire puts the seven prop kinds, their
knobs and their recipes into `/api/openapi/v1.json` — where the dressing document had no field named at all
— and `TerrainTheme`, `HouseStyle`, `BiomeField`, `SketchLayer`, `SketchGroup` and
`SketchShape` reach it from the sketch rather than only from a preview route or, in the biome's case, from
nowhere at all: every field of a stored layout now has a route that says what it holds.
That is where an agent looks before asking for something the studio cannot do, so a part with no address is
a part it has to learn from prose instead.

**The house half** reads `roomStyles.wool`, `roomStyles.spawn` and the shell of every building in
`dressing.props` off the document that is about to be written, and runs each through the same house-style gate
(`docs/tools/library.md`'s Refusals) — a block named for a geometric role that is not that kind of block, a
doorway that does not clear 2.5 blocks once its head is written in, or a roof whose own materials are wrong for
its pitch or its family, `HS1`–`HS6` in all. The author's verdicts on how a house looks, `HS7`–`HS19`, ride
back on the answer as complaints and refuse nothing. The cage and the spawn are checked identically: a stair
lattice or a slab band window is allowed on either, as it is on any house, so long as its block is the kind the
form needs. A placed building is checked identically for the same reason: its shell is a snapshot on the prop
rather than a reference to a library row, so the style stored here is the style the export stamps, and
`HouseProp.Check` reads only its wings and joints. Its findings name the prop —
`field` is `dressing.props[3].style.verge` and `subjects` carries the prop's own id.

**It is asked on both roads a layout is stored through**, not only on this one: `PUT /map/{slug}/source` runs
it over the layout its refinement produced.

Answers **400** `{error: "invalid style or theme", message, findings[]}` (`docs/refusals.md`), one finding
per fault, and writes nothing. A layout with no `roomStyles`, no buildings and no themes — or one whose
snapshot does not parse as a house style or a theme at all — is not this gate's business and saves as it
always did: only a well-formed style or theme that is wrong is refused. A snapshot holding a part the record
cannot — stated as `null` where it has none, or written as a material rather than as its courses, the shape a
style had when its floor, ceiling and wall were each one block — is refused by its path, `RQ1` on a bound
room style (`roomStyles.wool.wall`) and `DR-DOC` on a building's recipe (`shell.wall`), since the export would
otherwise stamp the built-in shell in its place.

**And a bound shell taller than the build ceiling is refused there too** (`WX10`,
`docs/world-export/structures.md`). A room's shell is authored geometry subject to no cap of its own, while
the goal marker over it hangs five blocks above a ceiling twenty over the ground — so a tall layer stack
swallows the very sign that says where the goal is. The height is measured on the smallest footprint a shell
can stand on, 6×6, since every sloped roof only climbs further on a bigger one: a style refused here has no
footprint it could have been stamped on. It rides in the same **400** envelope, `field` naming `roomStyles.wool` or
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

**It complains of a shape that fills ground a subtract takes away, and of one that draws nothing there.**
A subtract is how a board states its **negative space** — the void a plan's buffer pieces compile to, the hole
a composed footprint leaves — and **a hole is never scenery**: what a body encircles is ground players go
round, and a board's walls are drawn to guard it. So the negative space may be *redrawn* — the buffer rounded
off, narrowed, moved — and papering it over is named. An override add, or any add on **another layer** (a
subtract reaches only the layer it is on), puts the ground back, `SK13`. A complaint rather than a refusal, because
the way under the ground is a carve (*The modifier stack*) and a lid over a hole is no longer the only one. On the
same layer
a plain add draws **nothing at all** instead, because the algebra is
`((adds − subs) ∪ override-adds) − override-subs` and a subtract beats every plain add whatever order the two
are written in — the shape is on the canvas and not in the world, which is the rule's other half. Either way `SK13` names both shapes, which of the two happened, how many columns they contest and
the northmost of them.

**A subtract states void over its own courses, so what contests it is a span rather than a footprint.** An add
whose top stops at or below the hole's floor is the ground *under* the void, and one whose floor starts at or
above the hole's top is a deck *over* it; neither holds a course the hole holds, and neither is what the rule
is about. That is what makes a **room** drawable: a mass, a subtract stating the courses the void is meant to
have, a floor whose top meets its floor and a ceiling whose floor meets its top. The comparison is in absolute
courses with each layer's own `base_y` already in them, so it holds across a stack as readily as within one
layer, and the count and coordinate the finding carries are the contesting columns rather than every shared
one.

**Where a refusal lands, and why nowhere earlier.** Every sketch-stage route — the write, the merge, and the
paint, dressing, contour and 3-D reads — takes a board whatever its geometry says and rides the findings back
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

**It complains where one hill is painted a theme per step.** A plan component spanning several surfaces
compiles to one shape per surface — a stepped island becomes stacked plateaus, each `{component}-{surface}`
and each addressable — so a theme scoped per plateau paints one hillside as two or three grounds with a hard
line at every riser. A theme is a **place**: the ground a board changes character at is where a player crosses
from one part of the map to another, not where the plan happened to step. `SK27` names the component, the
surfaces it climbs through and what each plateau paints, so the riser can be found on the canvas; the fix is
one theme over the flight with the change stated inside it — a `slope` stack tells a riser from a tread by its
angle, a `height` stack cuts at the surfaces the steps already sit at — or two components in the plan where
the steps really are two places.

A plateau is told from a shape somebody drew by the compiler's own naming: the number the id ends in is the
shape's own thickness, and a drawn shape sits on a stated `floor` where a compiled one never does. So a
sculpture's numbered parts are outside it, and a component whose own name ends in a number keeps it. Nine
authored boards raise it, one component each.

**Two things can take an override add's statement away.** An override add is what a made thing is drawn
as — a wall, a flight of stairs, a crop bed, a stepped mound — and it states two things at once: the column is
its own, and this is its top. Each can be taken away by something that raises no other finding.

A **modifier** moves the top of the columns inside its mask, so an override add inside one builds to whatever
the stack leaves unless it says otherwise: a `relief_scope` of `hold` or a `level` height keeps its top where it
was stated, and a `raise`, `sink`, `drape` or a scope of `ride` carries it on the ground the stack left. Nothing complains about an
override add that says neither, so a wall drawn under a Hill's mask comes out raised with the ground. An override
add carrying no `base_height`, `floor` or `anchor_heights` is a footprint holding a theme, and the ground the
stack leaves under it is the ground it was drawn for — a scree apron over a swell is written exactly that way.

A **theme** follows the shape that forms the surface, so where two override adds share a column the taller
wins both the ground and the paint. Where the smaller of the two is also the shorter, its theme is therefore on
*none* of the ground they share: a mound's outer ring crossing a wall leaves the wall standing to its own
courses in its own stone, and the mound's turf absent from every column of it — a shape drawn, stored and
painting nothing. That is `SK15`, a complaint naming both shapes, both themes, the columns they share and the
northmost. Two shapes at *one* height are a theme scoped to a patch, the smaller one is the scope, and that is
what scoping is for and is not this. **The images count**: a shape in a mirroring group stands on
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
error to catch: where their spans meet the blocks interleave, the later pass winning each cell it writes, and
what stands there is one inside the other. `SK18` names the made thing, what it is standing in, how many
columns they share courses in and the first of them. **A shared column is not the fault** — a beacon frame
forty courses over a monument stands in every one of its columns and in none of its blocks — so the read is
the made thing's own span against the solid run rising off the terrain, which at a column a stamp claimed is
the stamp. It is read off the finished world's **provenance** rather than off
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
shape and its layer. Nothing else says so, and the surfaces that could are looking elsewhere: the shape
rasterizes where the author put it, so the board is not missing it; and the canvas, which groups every shape
no group lists from the geometry, draws it as a group of its own — mirrored by default — until the next
browser save writes that group into the document. The same list carries the group's
keep-clear fan, so an unlisted shape takes no part in that either. A layer stating no groups at
all is outside this — the whole of that layer mirrors — and so is a role-tagged room piece, which is never
listed by design.

**And it complains where a group lists its shapes and declines to be fanned.** `mirrors: false` is a legitimate
statement — a landmark seated on the symmetry centre is already every one of its images, and fanning it would
stamp it onto itself — so the flag alone is not the fault. What decides is where the group stands. `SK28` reads
the group's own bounds against each of its orbit images: bounds that meet an image straddle the centre and may
well be that image, and are left alone; bounds disjoint from every image cannot be, and that group is built
once, on one team's ground and nowhere else. On a half-turn board one side has the curtain wall and the other
does not; on a quarter-turn board three of the four teams have nothing there.

Nothing else reports it, which is what makes it worth a rule. The store answers 200, the export gate opens,
and pre-flight's mirror check reads spawns, wool rooms and build zones rather than made geometry, so a board
with one castle and three bare quadrants passes every gate the pipeline has. `GET /map/{slug}/column` is what
confirms either way, and the image coordinate has to be exact: the reflection of block `z` is `−z−1`, so
probing `−z` lands one block off the image and reports a difference on a board that is exactly symmetric.
Where a group really is its own image, its own shapes have to be symmetric too — eight causeway trestles whose
offsets have no partner under the half-turn prop a neutral crossing at different spacings for the two teams,
and the deck above them, being one centred rectangle, says nothing about it.

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

**And it complains about a board with no finish at all.** `SK8` rides on the success when the stored layout
carries **no theme registry, no terraform and no props** — the finding names which of the three are absent. Ground
alone is a legitimate board, so this stops nothing; what it stops is the board shipping unremarked. Every other
gate here needs something stated to disagree with — `SK3` a shape citing a theme the layout does not carry,
`SK4` a shape drawing nothing, `SK7` a layout rasterizing to no ground — so a board stating none of it slips
between all three and exports as raw stone with every stage answering 200. It is asked at the finish rather
than at the `PUT`, for the reason `SK6` and `SK7` are: a board mid-draw has every right to be bare, and only
finishing declares the drawing done.

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
halves stands on one), a group listing a shape id the layout does not carry, and a **theme** the registry does not carry, on a shape or as the map default (which
paints those cells unthemed stone and is otherwise the quietest fault a finish has — one reported per name
rather than one per shape) — `SK4` for a shape that
draws no ground (a polygon under three vertices *or of no area, every point on one line*, a circle or polyline of
no width, a rectangle of no area), and
`SK5` for a column the world cannot hold. Each carries the document path that named nothing in its `field`
and the shape's id as its subject.

**A part of the document nothing read is said the same way.** `SK3` covers a name that resolves to nothing;
the layer under it is a *field* that resolves to nothing — a rectangle keyed `x`/`z`/`w`/`h` instead of
`min_x`/`min_z`/`max_x`/`max_z`, a `modifiers` list written one level too deep — which the deserializer drops before
any gate can see it, leaving a board that covers no ground under a success. Both writes answer it as
`RQ3` complaints naming the dotted path (`layers[0].layout.shapes[0].x`), over the body **as posted** rather
than the merged one, since that is the document the caller can correct. The reading and its four exemptions
are `docs/refusals.md`, *`RQ3`*.

**Heights are clamped rather than refused**: a shape is never thinner than one block and its floor never dips
below zero, whatever is asked for — and since the clamp means what stands is not what was asked for, `SK5`
says so on the way past.

And a prop whose kind the client does not know is silently **dropped on load** rather than carried. A shape
nothing can edit is worse than an absence.

**A write reads the document; a read walks the ground.** Nine of the rules above — `SK9`, `SK10`, `SK11`,
`SK13`, `SK15`, `SK16`, `SK23` and `DR-TONE` — are answered off the **rasterized spans** rather than
off the JSON: what stacks over what, what a layer's slab drives into, what is standable and unreached, which
theme paints a given cell. Answering them walks every column of the board's extent, which on a played-size
board is seconds. So a **partial write** — a shape, a vertex, a layer, a group, a prop, a theme — takes the
document reading and leaves those nine for a read that asks: on `opus5-millrace` (274×268 columns, 312 shapes) that is **48 ms a moved vertex against 1,291 ms**, and
the nine calls that reshape a compiled rectangle drop from twelve seconds to under one.

What a write leaves out it **names**, on its own header.
`Pgm-Unwalked: SK9 SK10 SK11 SK13 SK15 SK16 SK23 DR-TONE` is on every partial
write, and it is deliberately not folded into `Pgm-Warnings`: that key means *these were found* and its
absence means *nothing was*, which is the one rule that makes it readable, so a rule that was never asked
cannot ride there. `GET /map/{slug}/findings` walks the ground and answers all of them, and so
does the **finish**, which is where a board carrying one is stopped. `SK2` is outside the split and answers
under either reading — a board too large to realize is measured off the shapes' own boxes, and must refuse
before anything walks a column of it.

**The play pieces are asked of the intent.** `GET /map/{slug}/findings` asks a map holding an intent the rules
about its pieces (`IntentCheck`): a map with no spawn (`PL2`) or no
objective (`PL3`, a complaint), a wool colour that is no dye (`PL14`), a monument form that is not one
(`PL10`), a goal floated over its cap (`OB22`), a core's lava outside its range (`DC1`), monuments or cores on
a map whose teams are not two (`OB14`), and four complaints about where pieces stand — an iron cube outside
every spawn region (`ST2`), a spawn in the half of its region nearer the symmetry centre (`SP2`), a room's
building over 20 by 20 blocks across all its wings (`ST9`) and a room's region over 20 by 30 (`ST10`). A unit
is asked once, by its authored image, and the board's symmetry is the intent's or the sketch's.

**So is every room the export stamps** (`RoomCheck`), resolved by the resolver the build itself calls, under the
shell the map binds for its kind. A room that will not frame on its region is refused — a house too small
(`WX2`) or reaching off its region (`WX12`), a marker half on a grid line (`WX3`), a pad that does not fit
between the walls (`WX4`), a wool room whose stated entries reach no wall (`WX6`) — and the build stamps the
default room around its marker in its place. A pad moved inward to clear the walls (`WX4`) and iron with no
place for its cube (`WX8`) are complaints, and a spawn room seating fewer monuments than the wools its team
captures is refused (`PL8`).

**And the board's structure is read off the ground the sketch builds** (`SketchBoardCheck`), one block to a
cell: every column the layout raises, owned by the group that draws it, and every spawn and wool region, owned
by its room, a cell's team being the image of the spawn nearest it and building allowed over the intent's build
areas (`SketchGround`). The board deriver reads it, and the rules answer under their ids: a face's front line under a third of its void side or under 15 blocks (`FR8`, `FR9`), a gap beside a
goal under 16 blocks, beside a team's own ground under 12, and a plain hole under 12 (`WL12`, `WL20`, `LN6`),
and on a two-team wool board the strait outside 15 to 40 blocks (`CT12`), each read over the authored build
areas where one is asked of an area. A build area running past the last ground it docks is `BZ9`, its id
named, and a board with no build area at all is `SP11`. The walks a match needs to a wool are asked over the
ground and the build cells together: a wool no spawn of a team that captures it can walk to is `PL9`, one per
team, and one the front line — the build cells and the ground they meet — reaches only through a spawn's room
is `SP1`, the wool's own room counting as open where a spawn's overlaps it. And a hop
outside 10 to 20 blocks is `G5`: the straight span between the nearest cells of two landmasses one build
region touches, where it runs inside that region and through no third one, asked within the authored team's
ground — a gap to another team's ground is the strait, and one inside an image repeats the unit's. Ground no
group mirrors carries the nearest spawn's team, so a hop from the unit to neutral ground on the far side is
not asked.

**And where the intent's pieces meet that ground** (`SketchSeamCheck`), the same rules answer under their
ids, each piece asked once by its authored image. A build area or water lane under 10 blocks across its
shorter side is `G2`, one touching a spawn's region `BZ5`, a lane over ground `BZ12`, and build areas that
touch and together fill one rectangle `BZ11`. An approach wall beside or over a wool's region is refused
(`PL13`); otherwise ground one block past either end, on either face, is `PL17`, and the mouth it bars — the
blocks of its line with ground on both faces, so a line drawn on past the shore bars only the ground it
crosses — outside 10 to 20 blocks is `ST8`. A spawn's first door with under 15 blocks of ground or build
area ahead of it, stepped out of its region along the door, is `SP9`.

**A step is asked only where a match crosses it**: at a spawn's door and at a wool room's entry. A wool's
entries are the sides of its region its doors open through; a wool stating none takes one, on the side of
its region meeting the most ground, the side facing the middle on a tie (`WoolDoors`), and a wool door whose
doorstep — a block either side of its centre, just outside the region — is neither ground nor a build area is
`WL21`. Where the easiest crossing
from a region to one owner's ground steps two blocks or more, the step is `SP8` ahead of a spawn's first door —
its own side, and the half of each flank side toward it — and `WL11` at a wool's entry, each with its number.
A step anywhere else is a cliff the author drew, so `EL1` is not asked of a sketch. An approach wall under 10 blocks from the entrance it stands before — the nearest entry parallel to
it that it overlaps along its line, joined to it by unbroken ground — is `ST11`. A wall farther out is not
asked: a wool takes a wall per approach, and one standing out on an approach around a hole guards its own,
which ground alone cannot tell from a wall seated too far back.

**The board's measures are read off the same ground**, at the 5-block cell the corpus's bands were measured at
(`SketchMeasures`): a cell stands where more than half its blocks do. The evaluator's own terms answer them.
Off the board's structure: front faces per team and the widest (`FR4`, `FR6`), crossings, neutral and team
stepping stones, cuts, enclosed holes and uncrossed middle holes (`CT1`, `CT4`, `CT13`, `CT5`, `CT8`, `CT9`),
and the narrowest lane to a wool room (`LN1`). Off the walks, in blocks: wool to wool, spawn to wool and its
spread and ratio, wool to the crossing and its balance, ratio and remoteness (`WL7`, `WL13`, `WL9`, `WL15`,
`WL10`, `WL16`, `WL17`, `WL18`), each over the authored team's ground and its own build areas — an area that
stands where an earlier one's symmetry image does is that one's copy — and on a two-team board a goal's walks
over the whole closure (`GO1` to `GO4`). And the band between the fronts — the rectangle round the board's
front-to-front build cells, which a composed plan names its mid band — measured across the fronts against its
floor for the board's size and front to front against twice its width (`MD7`, `MD8`). And the flow of a match
over the board, walked as the plan's flow is (`PlanNav` stated from the ground, every cell at one height): the
share of ground no journey reaches and how far the attack's and the defence's routes lie over each other
(`LN5`, `CT14`). A value outside its band is a complaint carrying the number and the
band, and a term with no band yet says nothing. Nothing is summed or ranked: a measure says how the board will
play and the author weighs it. Compiled from a seed plan, the sketch measures what the plan measures — 984 of
989 values over the 49 seeds — and the rest are where a centre island straddles both images of a rot_180 board,
whose team is the deriver's tie to break. A plan a map still holds as history is asked nothing.

## The API

Every endpoint is rooted at `/api`; a read is open to anyone and a write needs someone who may edit the map
([`docs/access.md`](../access.md)), which is what the 401 and 403 no row repeats are. A route that builds a world waits its turn in the build queue and answers 429 when it cannot, which no row repeats either.

**The map's layout**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /sketch` | `{name?, width?, depth?, mode?, centerX?, centerZ?}` | `{slug}` — a `map` row at `stage=sketch`, whose layout is a **ground layer** at `base_y` 0 under the id `ground`. A frame seeds the `setup` beside it, and `mode` is any `SymmetryModes` word, `none` included, with anything else read as `rot_180`; without a frame the editor uses its 120×80 `rot_180` default. A board is a stack and a flat one is a stack of one, so the ground is written here rather than invented by whichever surface draws on the board first | — |
| `GET /map/{slug}/sketch` | — | the stored layout with the map's rooms drawn in from the intent, or `{}`. The `ETag` is the revision to state on the next write | 404 |
| `GET /map/{slug}/intent/pieces` | — | `[{id, kind, color, minX, minZ, maxX, maxZ, carried, line?}]` — every play piece where the sketch draws it, at the rectangle `PATCH …/intent/pieces/{id}` reads it as standing on; `carried` marks an image the intent does not store and an iron cube seated in a spawn room; a room's point is `{room}-marker`; a wall's rectangle spans its columns and `line` is its two ends `[x1, z1, x2, z2]` | 404 |
| `POST /map/{slug}/intent/pieces` | `{kind, minX, minZ, maxX, maxZ, team?}` — a room's or an area's rectangle, or the block a goal, a cube, a chest or the observer stands on; a wall states `{kind, ends: [x1, z1, x2, z2]}`, its two ends on block corners, in place of the rectangle | `{id}` — the authored image's id; the piece is stored with every image the symmetry draws of it | **400 `RQ1`** an unknown kind, a rectangle with no area, a wall stating a rectangle or two ends at one point, a team the map lacks or that has a spawn, a spawn where every team has one, a second observer · 404 · **409 `RQ5`** a stale `If-Match` or a region the map already names · 422 |
| `PATCH /map/{slug}/intent/pieces/{id}/settings` | `{key, text?, number?, flag?, alone?}` — `key` one of `name`, `color`, `style`, `materials`, `float`, `lava`, `lavaHeight`, `openTop`, `leak`, `facing`; `text` for a name, colour, design or material, `number` for the rest and for a facing's quarter turns clockwise, `flag` for `openTop`; none of the three puts it back to its default | `{}` — the setting stored, on every image of the unit for what a unit shares | **400 `RQ1`** a setting the piece's kind lacks, or a value it cannot take · **404 `RQ4`** the id names no piece · 409 · 422 |
| `DELETE /map/{slug}/intent/pieces/{id}` | `?alone=true` to keep the other images | `{}` — the piece and every image of its unit taken out of the intent | **400 `RQ1`** a room's point, which goes with its room · **404 `RQ4`** the id names no piece · 409 · 422 |
| `GET /map/{slug}/places` | — | the places as stored, or `{}`; the `ETag` is the revision to state on the next write | 404 |
| `PUT /map/{slug}/places` | `{places: [{id, name, shape, points, holds?}], area?, verdict?}`; `?note=` kept on the change | `{}` — stored verbatim as one change in the map's history | **400 `RQ1`** a place with no id, a shape that is not `point`, `box`, `ring` or `line` or carries the wrong number of points, a point that is not `[x, z]`, an `area` whose max is not past its min, a `verdict` that is not `proposed`, `changes` or `approved` · **400 `RQ5`** two places stating one id · **400 `RQ4`** a place holding a play piece the map has none of · **403 `RQ8`** a caller who is neither an admin nor a token carrying the notes permission · **409 `RQ13`** a stale `If-Match` · 404 |
| `PUT /map/{slug}/sketch` | the layout | `{}` — a **verbatim replace**, which is what makes a deletion stick, but for the rooms: a room shape is stored as its `rooms` record and its rectangle is dropped; `warnings` rides beside it where the document names something it does not have (`SK3`/`SK4`/`SK5`) or carries a field the reader has nowhere to keep (`RQ3`). **The board's own geometry never refuses this write**: a drawing in progress is stored whatever it says, and every finding it raises rides back on `warnings`, `SK13` included. The `ETag` is the revision it landed at | 400 non-JSON, or 400 `{findings}` on a bound room style the house-style gate refuses · **409 `RQ5`** an `If-Match` naming a revision the layout is no longer at · 404 |
| `POST /map/{slug}/sketch/finish` | — | `{slug, configureUrl}` — rasterizes to world geometry, moves the map to `stage=configure`. It runs the document gate over the stored layout, so the stage that declares the drawing done is also the last one to say what will not be built | 422 `SK6` nothing stored · 422 `SK7` nothing drawn · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 404 |
| `DELETE /map/{slug}/discard-if-empty` | — | `{discarded}` — drops a draft still at its default name, credited to nobody but its originator, with nothing drawn | — |

**Previews over a live layout.** All four take the working document as the body rather than reading the stored
blob, so they track unsaved edits, and all four answer 400 rather than 500 on a layout they cannot process.
All six run the document gate, so all six answer `SK3`/`SK4`/`SK5` under `warnings` beside whatever else they
carry — the board an author is looking at is the one place those complaints are worth reading.

| Endpoint | Answers |
|---|---|
| `POST /map/{slug}/sketch/paint` | the painted surface as palette-indexed block pixels — the real painter's output, with team tints resolved from the stored intent |
| `POST /map/{slug}/sketch/relief[?interval=][&heights=true][&step=]` | `{interval, groups[]}` — per group with ground its height range, its bounds and its traced contour lines — the contour overlay, over the highest built top of each column the group forms with every modifier run, the lines traced half a block above each top. `heights=true` adds `heights`, that surface itself: one block height per cell of that box, row-major from the north-west corner, `null` where the footprint holds no land |
| `POST /map/{slug}/sketch/modifiers/changes` | `{modifiers: [{id, columns}]}` — per modifier of the posted layout, in run order, how many columns its own run moved (the rounded top changed); one switched off answers 0. One pass over the stack | 400 `RQ1` a body that is not a layout · 404 |
| `POST /map/{slug}/sketch/columns[?step=]` | `{palette, cols, layers, min_x, min_z, max_x, max_z}` — the whole built world as per-column runs, which the 3-D preview meshes. `cols` is one flat array walked as `[x, z, runCount, (yTop, yBottom, paletteIndex, layerIndex) × runCount, …]`, and `layerIndex` is into `layers` or `-1` for a run no layer accounts for; its `warnings` carries every prop the dressing pass declined (`DR-*`) as well, at severity `decline`: the world built and those things are not in it | 400 `RQ1` a body that is not a layout · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/dressing` | `{props[], declines[], claimedCells, claims}` — what the dressing pass would place, run and stopped before anything is written: per prop the columns it covers, where it rests and the height it resolved to, and every prop that did not land as its `DR-*` finding. `claims` is `{bounds, width, height, classes[], rows[]}`, digit rows over the board's own ground the way `coverage`'s own classes are, classing every cell as a prop's own claim, a goal's clearance, a keep-out, or free — so a candidate site is looked up on the raster rather than tried and read back as a decline. `?format=text` answers the same reading as characters, with the classes' key, a column-index line, the declines and a `placed n, declined n` line under it | 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/seats[?kind=&width=&depth=&style=]` | `{bounds, width, height, kind, standoff, footprintWidth, footprintDepth, rows[], seats, refused[], unasked[], slopeLimit}` — where a prop of that kind and footprint **may** stand, which the `DR-*` declines only ever answer backwards: `1` where a box of `width`×`depth` blocks seats with its minimum corner on that cell, `0` where it does not, a space off the board, and `refused` the tally of which rule turned the rest away. `kind` is one of the document's own prop kinds (absent: `tree`) and decides the route standoff; one number asks about a square. A `tree` is asked `DR-ROOT` as well — every cell whose surface is not grass or dirt is refused. For a `house`, `style` names the recipe whose height `DR-SLOPE` is asked against (absent: the default building), `slopeLimit` is that height and `unasked` names `DR-CROSS` and `DR-WAY`, the declines the query does not run; every other kind answers `unasked` empty. `?format=text` answers the same mask with its key and the tally under it | 422 `no such prop kind` `RQ4` · 422 `no such house recipe` `RQ4` · 422 `the board cannot be built as drawn` `SK2` or `SK13` · 422 `dressing document invalid` `DR-DOC` · 404 |
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
| `DELETE /map/{slug}/sketch/layers/{layerId}` | `{id}` — take one layer, everything drawn on it, off the board | 409 · 404 the id names no layer |
| `GET /map/{slug}/sketch/layers/{layerId}/shapes` | the shapes drawn on one layer, in draw order | 404 the id names no layer |
| `POST /map/{slug}/sketch/layers/{layerId}/shapes[?group=]` | `{id}` — draw one shape on a layer. `?group=` names the ground it joins, and a name the layer does not carry yet opens a group; **a layer that already groups its shapes takes no shape that names none**, since the orbit fan is read off a group's list and an ungrouped shape on a grouped layer is built once, where it was drawn (`SK17`). A body stating a free id keeps it; one stating none, or one already drawn, is minted `{type}-{n}` | 400 `the edit cannot be made` `RQ1` naming no group · 400 `malformed body` `RQ1` · 409 · 404 the id names no layer |
| `GET /map/{slug}/sketch/groups` | every group the board carries, across all its layers — `{layer, id, name, mirrors, shapeIds}` each. The list a caller reads before naming a group to draw into | 404 |
| `PUT /map/{slug}/sketch/layers/{layerId}/groups/{groupId}` | `{id}` — state one group whole: what it is called, whether it is fanned onto the symmetry orbit, and which shapes share its ground. Creates it where the layer carries none under that id | 400 `malformed body` `RQ1` · 409 · 404 the id names no layer |
| `DELETE /map/{slug}/sketch/layers/{layerId}/groups/{groupId}` | `{id}` — ungroup. The shapes stay on the layer and are drawn where they were drawn; what goes with the group is the orbit fan | 409 · 404 the id names no group |
| `GET /map/{slug}/sketch/shapes/{shapeId}[?format=text]` | one `SketchShape`, wherever on the stack it is drawn — a shape id is unique across the whole document, so it is the address wherever it lives. `?format=text` answers its outline as the edges a point edit names instead: each edge by the vertex it leaves, its two corners, its length, `~` where a handle curves it, and what the shape's layer covers half a block outside it, run by run along the edge — another shape, `id (image)` where the board's symmetry draws a fanned shape's image, or `void`. It is the read a pull or a bend's `edges` is written from, so a coast is found by reading rather than by copying the ring and counting; a rectangle or a circle answers that it has no outline to edit | 404 the id names no shape |
| `PATCH /map/{slug}/sketch/shapes/{shapeId}` | `{id}` — change one shape without restating the board. A stated field replaces what the shape carried and **a stated `null` takes the field off**, so the one call both writes a height and clears a relief scope. `id` is the address and is kept whatever the body says | 400 `the edit cannot be made` `RQ1` on `role`, `intentRef` or `height_authored` · 400 `malformed body` `RQ1` · 409 · 404 the id names no shape |
| `POST /map/{slug}/sketch/shapes/{shapeId}/bend` | `{id, vertices, held}` — redraw one outline as a **coast**: resampled along its long edges every `step` blocks, each inserted point pulled off its edge by up to `wander`, and Bézier handles fitted over the result. Body `{wander, step, seed, tension?, side?, edges?, fan?}`. **The outline's own vertices never move**, so a corner stays where the plan put it and the neck a spur hangs off keeps its width. `side` is `out` (the default — the slight bloat that reads as land), `in` (keeps the plan's footprint, for a board whose shapes abut on a measured strait) or `both` (wanders across the line the plan drew). The side is decided by asking the ring rather than by reading a winding, so it is right for either winding and for a concave stretch. `edges` names the edges to draw, each by the vertex it leaves; every edge not named keeps no cut and no handle. An outline the board's symmetry carries onto itself is drawn as a coast that is its own image too — its wander read at each point's canonical image, a named edge's images drawn with it — unless `fan` is false. `held` counts the points that had no room on the side asked for and stayed where they were cut, which rides back as `SK21` | 400 `the edit cannot be made` `RQ1` — a `role` shape (a room's rectangle is not a coast), no `vertices` to resample, an `edges` entry the outline does not have, a wander that folds the outline across its own far side, or a `wander`/`step` of nought · 409 · 404 the id names no shape |
| `PATCH /map/{slug}/sketch/shapes/{shapeId}/vertices/{index}` | `{id, index, vertices}` — move one point of one outline. Body `{x, z}`. **Every other vertex stays exactly where it was drawn**, which is the whole of the call: a board's shapes abut, and an edit that drags a ring's other points opens ground between two that were flush | 400 `the edit cannot be made` `RQ1` — a `role` shape, no `vertices` to address, an index the outline does not carry (the message states the range), or a move that folds the ring · 409 · 404 the id names no shape |
| `POST /map/{slug}/sketch/shapes/{shapeId}/vertices` | `{id, index, vertices}` — add one point after the vertex `after` names, and answer where it landed. Body `{after, x?, z?}`; stating no point puts it at the **midpoint of that edge**, which is a new corner half way along a wall with nothing else moved. The last vertex's edge closes the ring | 400 as above · 409 · 404 the id names no shape |
| `DELETE /map/{slug}/sketch/shapes/{shapeId}/vertices/{index}` | `{id, index, vertices}` — take one point out, leaving every other where it was drawn | 400 `the edit cannot be made` `RQ1` — as above, plus an outline down to its last three, since two points draw no ground · 409 · 404 the id names no shape |
| `DELETE /map/{slug}/sketch/shapes/{shapeId}` | `{id}` — rub one shape out, and take it out of every group that listed it | 409 · 404 the id names no shape |
| `GET /map/{slug}/sketch/props` | `{props[], styles{}}` — every placement the map carries and the recipes they name, typed as `PlacedProp` so the seven kinds, their knobs and their styles are in the published schema. The recipes ride with the placements because a placement naming a key nobody can resolve is not readable on its own | 400 `unreadable dressing` `DR-DOC` · 404 |
| `POST /map/{slug}/sketch/props` | `{id}` — place one prop, without sending the board it stands on. A body stating a free id keeps it; one stating none, or one already taken, is minted `{kind}-{n}`. The placement goes on the end, since the pass runs in placement order and an addition has not been placed before anything. Its `style` is read the way the stored document's are: a key names the recipe the map's registry holds under it, and a recipe stated inline — a house's shell as an object, a tree's or a boulder's fields spread on the placement — is lifted into the registry under a key read off it | 400 `malformed prop` `RQ1` (the message names every kind) · **400 `malformed prop` `DR-DOC` a `style` key the registry holds no recipe under** · 400 `invalid style or theme` `HS*`/`PT*` · 409 stale `If-Match` · 404 |
| `PATCH /map/{slug}/sketch/props/{propId}` | `{id}` — replace one placement, keeping its position in the pass's order and the id it is addressed by. Editing a prop must not move it past what the pass places after it | 400 as above · 409 · **404 the id names no placement** |
| `DELETE /map/{slug}/sketch/props/{propId}` | `{id}` — take one placement off the board. The recipe it named stays in the registry, since a key is shared by every placement wearing it | 409 · **404 the id names no placement** |
| `GET /map/{slug}/sketch/themes` | `{themes{}, mapTheme}` — the registry by the id an author registered each theme under, and which of it covers every cell no shape scope claims. A registry entry the painter cannot read as a theme is left out, the same way the painter drops it | 404 |
| `GET /map/{slug}/sketch/themes/{themeId}` | one `TerrainTheme`, as the painter reads it | 404 the registry carries no such id |
| `PUT /map/{slug}/sketch/themes/{themeId}` | `{id}` — register a theme under an id, replacing whatever that id carried. The one write in the sketch that creates and replaces through the same verb, because a registry entry is addressed by the name an author gave it. A field no theme has — a misspelled bucket — is stored as posted and named on `warnings` as `RQ3` | 400 `malformed theme` `RQ1` · 400 `invalid style or theme` `PT*` · 409 stale `If-Match` · 404 |
| `DELETE /map/{slug}/sketch/themes/{themeId}` | `{id}` — take a theme out of the registry. **It does not refuse over what still names the id**: a shape painting with a theme the registry stopped carrying takes the map default, and the map default naming one takes unthemed stone, both already `SK3` complaints on the stored document. They ride back on this write | 409 · 404 the registry carries no such id |
| `PUT /map/{slug}/sketch/map-theme` | `{id}` — which registered theme covers every cell no shape's own scope claims. Body `{"theme": "<id>"}`; a null or absent theme clears it, which paints unthemed stone. Naming a theme the registry does not carry is stored and complained about (`SK3`) rather than refused | 400 `malformed request` `RQ1` · 409 · 404 |
| `GET /map/{slug}/sketch/room-styles` | `{wool, spawn}` — both shells **resolved**, which is what the stampers will read: a part that is absent answers its built-in shell and a part bound to open ground answers null. Raw snapshots would not say which of the three states a caller is in | 404 |
| `PUT /map/{slug}/sketch/room-styles/{part}` | `{id}` — bind the shell one kind of room is stamped in; `part` is `wool` or `spawn`. **A body of literal `null` is a statement, not an omission**: it asks for open ground, a pad rather than a building over it, which is what a spawn on a plateau the plan already shaped often wants to be. A field no house style has is named on `warnings` as `RQ3` | 400 `unknown room part` / `malformed room style` `RQ1` · 400 `invalid style or theme` `HS*` · 409 · 404 |
| `DELETE /map/{slug}/sketch/room-styles/{part}` | `{id}` — unbind, which puts that kind of room back to its **built-in** shell. Not the same as binding null | 409 · 404 nothing is bound |
| `GET /map/{slug}/sketch/biome` | one `BiomeField` — `solid`, `cell` or `noise`. The library's own rows are `GET /api/biome-patterns`, and every biome with the colours it tints with and the biomes of the same colour is `GET /api/terrain/biomes` | 400 `unreadable biome` `RQ1` · 404 the board states none, which is plains everywhere |
| `PUT /map/{slug}/sketch/biome` | `{id}` — which biome each column of the exported world carries. Map-wide and answered per chunk, because a biome's tint is blended across a radius and a region drawn to a finer edge never reaches its own colour there. A field the field's kind has no place for — `biome` where a `solid` field states `id` — is named on `warnings` as `RQ3` rather than read as plains in silence | 400 `malformed biome` `RQ1` · 409 · 404 |
| `DELETE /map/{slug}/sketch/biome` | `{id}` — take the field off the board, which is plains everywhere | 409 · 404 |

**The report** — everything a drive reads back, for an agent; the tool has no page for it
(`docs/world-scan/read-backs.md`, *One read answers everything a drive reads back*).

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/report[?pictures=true]` | `{slug, change, headline, reads[], pictures[]}` — `headline` is `{walked, scrambled, barrier, placed, declined, worstStep, worstRoute, says[]}`, the three numbers and the three lines they are said in; each reading `{name, route, text, missing}`, the route answering the same reading alone; each picture `{name, route, png, missing}`, `png` drawn only on `?pictures=true`. `?format=text` answers the whole as one document | 404 `SK6` no stored layout |

**The pictures from a player's eye** — what the Review phase lists and draws. Each view answers the
`render/eye` query words that draw it, so a caller adds only a size.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/views` | `{views[], undrawable, change}` — every view of the board: its own straight-down view first, then the studio's suggestions from the built board, then the others kept, each `{id, name, kept, own, lookX, lookZ, fromX, fromZ, y, pitch, yaw, query, eye, picture}`. `own` marks the straight-down view (`above`), kept by every board; `picture` the one the map's picture is drawn from — the kept view marked as it, else the `overview` suggestion; `eye` is the camera the view resolves to on the board as built — where an eye left to find its own place ends up — or null on a server with no block textures. A map with no sketch layout has no world to frame or suggest from and answers only what it kept. `undrawable` is why no picture can be drawn on this server, or null; `change` is the map's latest change when they were listed, the board the pictures are of | 404 |
| `POST /map/{slug}/views` | the view kept, minted `view-{n}`. Body `{name?, lookX, lookZ, fromX?, fromZ?, y?, pitch?, yaw?, picture?}`; a blank name is `View {n}`, and `picture: true` draws the map's picture from this view and from no other. `y` and `pitch` together are an aerial shot: an eye raised to `y` and tipped `pitch` degrees down, 90 straight down. `yaw` states the camera whole — it turns from `fromX`, `fromZ` at `y`, and `look` is then only where the canvas draws its target; it is wrapped into the game's −180 to 180 | 400 `not a view` `RQ1` — a stand point stating one coordinate without the other, a coordinate off any board, an eye height below the world's floor or over its top, a pitch past straight up or down, or a `yaw` without its stand and `y` · 404 |
| `PUT /map/{slug}/views/{viewId}` | the view changed in place, keeping its id. Body as for keeping one; a blank name keeps the view's own, `picture` absent keeps whether it is the map's picture and `false` stops it being. The board's own `above` is changed the same way, and the change is then kept in place of the framed one | 400 `not a view` `RQ1` · 404 no kept view has that id — a suggestion is not stored, so it cannot be changed |
| `DELETE /map/{slug}/views/{viewId}` | the view let go. A suggestion is not kept, so it cannot be deleted; deleting a changed `above` puts the framed one back | 404 no kept view has that id · 409 `RQ15` the framed `above`, which is never let go |

```json POST /api/map/{slug}/views
{"name": "Across the middle", "lookX": 25, "lookZ": -22, "fromX": -26, "fromZ": 21}
```

**Notes** — what the notes column reads and writes (*Notes*, under Review). Every route here, the reads
included, takes the `notes` policy: an admin in a browser, or a token an admin issued with the notes
permission (`docs/access.md`). A member, and every other token, is refused `RQ8`.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /notes?status=&since=` | every note on every map, newest change first, each a `MapNoteDto` with its map's slug and name. `status` takes one status or several between commas — `open` is what an agent starts on; `since`, an ISO 8601 instant, keeps the threads whose last message or status change is at or after it | 400 `no such status` `RQ1` · 400 `no such instant` `RQ1` |
| `GET /map/{slug}/notes` | the map's notes, newest change first: `[{id, map, mapName, anchor, status, createdAt, updatedAt, messages[], unread}]`, each message `{id, author, authorUuid, token, body, change, picture, at, mark}` — `token` is the label of the token an agent wrote it with, null for a browser. `unread` counts the messages new to the caller: written by someone else after the newest one they have seen | 404 |
| `POST /map/{slug}/notes` | the note written. Body `{body, anchor, picture?, change?}`; `change` is the map's change it was written at, and absent takes the latest. A note an author writes is `open`, one written with a token is a question and `needs-info` | 400 `not a note` `RQ1` naming the field — an empty body, an anchor of no known kind, a `place` anchor naming no place the map has, a picture anchor without its camera or size, a mark of the wrong number of pixels or outside the picture, a picture no upload answered, a `change` that has not landed · 404 · 409 `place has its thread` `RQ5` — a `place` anchor naming a place that already has its thread; the finding names the note to reply to |
| `POST /map/{slug}/notes/{id}/replies` | the thread with the reply on it. Body `{body, status?, picture?, change?, mark?}`; `mark` is a `point`, `box` or `lasso` anchor as `render/eye/pick` answered it, held to everything a note's anchor of that kind is; `status` is where the reply leaves the thread — `answered`, `needs-info`, `wont-do` or `open` — and absent is `answered` for a token and `open` for a browser. A token's `answered` reply on a picture note, at the latest change and with no `picture`, carries the note's camera drawn over the board as stored | 400 `not a reply` `RQ1` — `resolved` is the author's `PATCH`, a `change` that has not landed, or a `mark` that is not one (`field` names it) · 404 no such map, or no note by that id on it |
| `POST /map/{slug}/notes/{id}/read` | the note, with `unread` 0: the caller has seen the thread to its newest message. A reply of the caller's own reads the thread up to it too. A reader is the person, a token by its label, or an open studio's local admin | 404 no such map, or no note by that id on it |
| `PATCH /map/{slug}/notes/{id}` | the note changed. Body `{status}`: `resolved`, `wont-do` or `open` to reopen | 400 `not a change` `RQ1` · 403 `RQ8` to a token — an agent answers in a reply, and only the author closes a thread · 404 |
| `GET /notes/handoff` | `{ready, waiting, fresh, handedAt, session}` — whether this studio names an agent, how many notes on every map are open, how many of those were written or answered since the last hand-off, and when that was and the session it started | — |
| `POST /notes/handoff` | the same, after the Routine is fired with the maps and their counts as its text. Body `{again?}`: `true` repeats a hand-off nothing was written since | 403 `RQ8` to a token — the author hands notes over · 409 `RQ16` no note waits, or none was written since the last hand-off · 503 `RQ12` no agent named · 503 `RQ19` the Routine refused |
| `POST /notes/pictures` | `{hash, bytes}` — the picture kept under the SHA-256 of its bytes. The body is the picture itself, a WebP or a PNG sent as `image/webp`, `image/png` or `application/octet-stream`, up to 8 MB; the same bytes answer the same hash | 400 `not a picture` `RQ1` |
| `GET /notes/pictures/{hash}` | the picture, `image/webp` or `image/png`, with a year's private cache — it never changes under its hash | 404 |
| `GET /map/{slug}/render/eye/pick` | `{camera, query, hit, ground, columns[], sky, standing, change}` — what a mark on a `render/eye` picture is on the ground. It takes the picture's own query words, so it resolves the same camera, and `at=x,y` (one pixel: the block `hit` and the `ground` under it), `box=x,y,x,y` or `lasso=x,y;x,y;…` (every ground column the rays hit, each `[x, y, z]`); none of the three answers the camera alone. `query` is the camera exactly, as `eye=x,y,z&yaw=&pitch=&fov=&width=&height=`, which draws the same picture again. `standing` is the top of the ground under the eye, null over the void. `change` is the map's latest change when the pick was cast: the board it read, which a picture listed at an earlier change does not show. Open to anyone, like `render/eye` | 404 · 422 no place sees what `look` names · 503 `RQ10` no block textures |

```json POST /api/map/{slug}/notes
{"body": "The monuments sit too close to the spawns.", "anchor": {"kind": "map"}}
```

**A placement is looked up rather than tried, and `seats` is the half that says where.** The `claims` raster
answers what holds every cell; `seats` runs the pass's own five seat rules forwards over the whole board for
one kind and one footprint, so finding a spot for a tree is one call rather than a preview pass per guess.
Two things keep the answer the pass's own. The standoff is the kind's — a tree keeps three blocks off a
route, a boulder two — and a claim only refuses a kind that places at or before the claimant's own turn, so
a bed of flora does not stop a tree while a tree stops the flora (`docs/world-export/decoration.md` § 8). And
a building gets a seat rather than a verdict. `DR-PASS` is asked here too: a building's `width`/`depth` are
its **walls**, the passage is measured from the roof over them, and a candidate joins the group of any
building standing within a passage of it — the standing ones are read off the raster's own structure cells,
one building to a run of them, and grouped once for the board rather than once per anchor. Its site is asked
to be level too (`DR-SLOPE`), by the pass's own arithmetic: the rise across the footprint against the height
of the style `?style=` names — a house recipe key in the posted layout's `dressing.styles`, absent the default
building — and `slopeLimit` answers the rise it refused from. `DR-CROSS` and `DR-WAY` walk the board's routes
and waypoints with the footprint taken out, stay the pass's to raise, and are named in the answer's `unasked`,
so a building seat marked `1` says which declines it has not been asked about. `DR-DIG` (a carve of more than three blocks at a column) is the stamp's own and stays
the pass's to raise.

**A tree is asked what it would be rooted in.** The raster is built over a world, so the surface block at a
cell is readable, and `DR-ROOT` refuses every cell whose surface is not soil — grass or one of the three
dirts. A board painted rock all the way up therefore answers a mask of zeros for `kind=tree`, which is the
honest answer: the wood wants a soil band under it before it wants a position.

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
the terrain-paint library a theme is copied from or saved to; `/room-styles` is the shell library the Palette
phase binds from, with `/room-styles/{id}/json` for the stamper's own form; `/roof-styles`, `/storey-styles` and
`/porch-styles` are the parts a room style is composed from. `/terrain/blocks` is the
block palette, and `/terrain/material-preview`, `/terrain/theme-preview` and
`/terrain/prop-preview` render what an edit will look like; `/terrain/stroke-styles`, `/terrain/fluid-forms`,
`/terrain/boulder-forms` and `/terrain/species` are the dressing vocabularies.

## Driving it without the UI

A sketch is one document, so an agent writes it and puts it. The loop is three calls.

```
POST   /api/sketch                    {"name": "Voidwatch", "width": 160, "depth": 100}
PUT    /api/map/voidwatch/sketch      <the layout document>
POST   /api/map/voidwatch/sketch/finish
```

A map that already has a layout needs only the last two: `GET /api/map/{slug}/sketch` returns it to be edited
and put back. The `PUT` replaces the blob, so a document that omits `themes` deletes them.

### Answering the notes an author left

**An agent starts a revision by reading the open notes, and it is not done while one is still open without a
reply.** It holds a token an admin issued with the notes permission (`docs/access.md`), and every step after the
read is an instrument the studio already has; the note only says where to point it. An agent is started by the
author's hand-off (below) or by the author in a chat; `GET /api/notes?status=open&since=<instant>` answers the
threads that moved since a given moment, for a caller that keeps one.

```
GET   /api/notes?status=open[&since=<instant>]                 the open notes, on every map
GET   /api/map/{slug}/render/eye?<anchor.camera as eye=…>      stand where the author stood
GET   /api/map/{slug}/column?at=<anchor.hit x,z>               what is at a point
POST  /api/map/{slug}/notes/{id}/replies   {"body": …, "status": "answered"}
```

**The studio draws the after picture.** An agent's `answered` reply on a note written on a picture, written at
the board's latest change and naming no `picture` of its own, carries the note's camera drawn over the board as
stored, which is the after the Review phase compares with the note's before. Drawing it takes a turn in the
build queue, so a reply refused `429` is asked again after its `Retry-After`; a studio with no block textures
keeps the reply without one, and a reply stating an earlier `change` is not drawn, since the board it names is
no longer the one stored.

A note's `anchor.camera` is drawn again exactly by `render/eye?eye={x},{y},{z}&yaw={yaw}&pitch={pitch}&fov={fov}&width={width}&height={height}`;
an area's `anchor.columns` are the ground it names, each `[x, y, z]`. The reply says what changed, the change
it landed as — change numbers count on across every rebuild, and the reply records the latest by itself — and
the number that moved; where the remark
could mean two places or two things, the reply is the question, with `"status": "needs-info"`, and nothing is
built on a guess. A declined note is `"status": "wont-do"` with the reason in the body.

```json POST /api/map/{slug}/notes/{id}/replies
{"body": "Read as look, not gameplay: swapped the roof to spruce slabs under a dark-oak ridge.", "status": "answered"}
```

### Handing the notes to an agent

**Nothing listens between hand-offs; the author starts the agent when the notes are written.** A studio naming a
Claude Code Routine's API trigger — `Notes:Agent:Fire` and `Notes:Agent:Token`, `docs/deployment.md` — offers
**Hand to the agent** at the head of the notes column, counting the open notes on every map and how many were
written or answered since the last hand-off. Pressing it calls the Routine's `/fire`, which starts one session
that reads the open notes and answers them; the column then says when they were handed over and links the
session. With nothing written since, the button is **Again**, for a session that stopped short.

**The hand-off carries no instruction.** Its text names the maps and how many notes each holds, and the
Routine's saved prompt is what says to answer them: the Routine receives the text as untrusted context, and it
reads the notes themselves from `GET /api/notes?status=open`. The last hand-off is held for as long as the
studio runs, so a restart makes every open note new again. The Routine's own limit is 30 fires an hour.

```
GET   /api/notes/handoff               {ready, waiting, fresh, handedAt, session}
POST  /api/notes/handoff               the same, after the Routine is fired
```

```json POST /api/notes/handoff
{"again": true}
```

### Reshaping ground a compile drew

A plan compiles to a staircase of rectangles. That is the board's *arrangement* — which ground is where, at
what height, next to what — and it is not the board's *shape*. Turning the one into the other is the work this
tool exists for, and there are two ways to do it that do not involve redrawing the ring by hand.

**One point at a time is the primary one, and it is what a hand does.** `PATCH
.../sketch/shapes/{id}/vertices/{index}` moves the vertex it names and nothing else; `POST
.../sketch/shapes/{id}/vertices` adds one after the vertex `after` names, at the midpoint of that edge when
the body states no `x`/`z`, and answers the index it landed at; `DELETE .../vertices/{index}` takes one out.
Every other point of the outline is exactly where it was drawn after each of the three, and `anchor_heights`
stays in step with the points (above). That is the whole
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
from. A `role` shape is a room's rectangle, which the intent places and a stamper seats a
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

**A coast is a bend named to the edges that are shore.** `edges` lists them by the vertex each leaves —
edge 0 runs from vertex 0 to vertex 1 — counted on the outline as the point edits leave it. An edge not named
keeps no cut point and no Bézier handle, so it stays the straight line it was even where a named neighbour
rounds into its end, and a shape flush against another along it stays flush. `{"side": "in", "edges": [2]}`
is the statement a board makes for ground that meets water along one face and its neighbours along the rest:
the bank wanders, inward and tested against the ring's own inside, and every seam keeps the line the plan cut.
An `edges` entry the outline does not have is refused with the count it does.

**A shape on the axis bends into a coast that is still its own image.** An outline the board's symmetry
carries onto itself — a mid island no group's fan copies — would otherwise come out lopsided, since the wander
is a field over the board and reads differently at a point and at its image. So the wander is read at each cut
point's canonical image instead, a named edge's images are drawn with it, and both teams meet the same coast;
`fan: false` draws the outline from its own coordinates.

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

**Three read the sketch itself.** `POST .../sketch/paint` runs the real painter and answers the surface as
palette-indexed runs — the exact colour of every footprint cell, which is how a Voronoi reads as its cells
rather than as an average. `POST .../sketch/relief[?interval=][&step=]` answers the traced contour lines per group
over the built columns, with every modifier run, as flat `[x, z, x, z, …]` runs. The terrain in **numbers** is
read off the stored board: `slopes`, `transect` and `column` (`docs/world-scan/read-backs.md`).
`POST .../sketch/columns` answers the whole built world as per-column runs; it is the heaviest of the three (it builds the map, and the answer is kept with the built
world, so the same layout and intent asked again is read rather than rebuilt) and the only one that reports
what stands *above* the surface, so it is the read for asking what a structure or a marker actually occupies.

**The finish previews draw, in SVG — or as PNG on request.** `POST /terrain/material-preview` and
`/terrain/theme-preview` answer a material and a theme as they will paint — the theme as a cut-open sample
plateau plus a top-down swatch per bucket. `POST /terrain/prop-preview` and the five card sets
(`/terrain/stroke-styles`, `/fluid-forms`, `/boulder-forms`, `/species`, `/woods`) answer a prop as it will be
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

**After the first build**, the map holds rasterized world geometry and three more reads open up:
`GET /map/{slug}/top-surface` for the per-column surface colours, `GET /map/{slug}/segments` and
`GET /map/{slug}/column-floor`. Data again, not pictures.

**The pictures of a stored sketch are the world reads.** `GET /map/{slug}/render/…` builds the world the
stored layout and intent describe and draws it — from above, in the round, in section, walked, or from a
player's eye in the game's own sprites — each with a `?format=text` twin; `docs/world-scan/read-backs.md` is the
catalogue, and `GET /map/{slug}/views` lists the eye's views the Review phase shows.

**`GET /map/{slug}/report` is every one of those reads at once**, off one build: the three numbers first, then
each reading beside the route that answers it alone, then the pictures by route — drawn too on
`?pictures=true`, and the whole as one document on `?format=text`. It is the read an agent takes after a store.

`tools/seeds/ruediger.layout.json` is the readable example for the geometry, the carve and the theme
assignment. It states no modifiers, no dressing and no room styles, so it is not a reference for those three.

## Limits

Sketch draws the ground and everything on it, and it does not know what any of it is *for*. It has no teams,
no spawns, no objectives, no build regions and no protection: a map begun here reaches Configure with geometry
and nothing else, and a map that came from a plan carries the plan's intent unchanged through this tool.

**A mask is a traced outline.** The canvas draws a mask by tracing it and edits it by its points; an ellipse area
is written in the document and has no drawing tool, nor has a rectangle.

**The stack shapes one height per column, and its water stands only over drawn ground.** The carves cut space
under that height in the built world, which the walk and every world read see; the column reads and the 3-D view
read the column's top and do not, and no shape stands in the block grid yet (`TS212`).
A Coast's sea floods the columns its mask covers that some shape built, so a sea meant to reach the board's edge is
a shape drawn out to that edge with the Coast over it; past the last drawn column is void.

**Only Draw edits geometry.** Palette and Decoration run the canvas as a selection surface: a group or a shape
can be picked, and the view panned and zoomed, but nothing can be moved, resized, rotated, reshaped or given a
vertex. Terraform draws and edits the masks of the modifier stack and no shape. Palette and Decoration reach the
ground the Draw phase owns by *picking* it — one to assign paint, one to place things on it — so an edit offered
there makes the gesture that selects a group the gesture that reshapes it, from a rail with no snapping and no
height controls to make sense of it. Decoration was the one that did not, and the way it showed was a plinth
dragged out from under a spawn: the piece the plan projected stayed where it was, the ground under it did not,
and the ground it left behind fused into an island of its own.

**Notes are an admin's alone.** A member reads and writes none, and a token carries the notes permission only
where its person is an admin, so the author's feedback is not a channel anyone on the whitelist can post to.
Nothing tells an agent there are new notes; the author does. A lasso drawn in the column sends at most 300
points of its outline, which keeps the pick's query inside a request line — the outline is thinned evenly, not
cut short. The pictures sit in a folder beside the database rather than in it, so a backup of the database
alone is not a backup of the notes (`docs/deployment.md`).

The rooms are drawn in from the intent, and **no shape edit reshapes one**. A spawn or wool room and the
building inside it are rendered so they stay visible while the ground around them is refined; a vertex, bend
or shape patch on one is refused, and a room is moved and resized as a play piece instead.

**Every other play piece is drawn too, from `GET /map/{slug}/intent/pieces`**: each destroyable and core at its
structure's footprint, each build area, water lane and iron cube at its rectangle, each approach wall as the
band two blocks thick along its line, each defence chest at its block, and the
observer at the block it stands in, and each room's point — where a spawn's team arrives (`spawnPoint`) and where
a wool is dispensed (`woolSpawn`) — at the block-wide square centred on it, its id the room's with `-marker` after
it, in its kind's colour (`PlayPieces`). Unlike a
room none of these is ground, so none is a shape of the layout: the canvas holds them beside the rooms and
writes none of them into a save. An image the symmetry draws that the intent does not store, and an iron cube
seated in a spawn room, is drawn dashed and cannot be picked, since it moves only with the piece it is an image
of or the room it stands in.

**Any of them can be picked and moved, and a room corrected.** In Layout a click lands on the piece under the
cursor and never on the ground under it, a double-click reaches no shape, and a click on empty canvas or Escape lets
the pick go; in Ground a click lands on the ground and never on a piece. Changing step lets go of what the other
step picked. The picked piece is ringed on the canvas and opens a rail of its own: what kind it
is, whose it is, its extent, and — for a **region** — its height. A room
stating no height stands on the ground under it and the field is empty; one the layout states a height for settles
on the ground from that height. Correcting it writes the number, the author's-height flag and `hold` together on the
room's record, so the room stays at the number whatever the modifiers do around it. A **building**
footprint carries no height of its own — the region it stands in is what the room's ground is held at —
so its rail states that and offers no field, and a piece that is not a room has a rail of its kind and extent
alone. Picking a piece drops whatever terrain selection was standing,
chrome included: one click selects one thing, and a room and the ground under it are two.

**A picked piece drags, and where a room is belongs to the intent.** The drag is block-granular and snaps to
nothing — a region's edges answer to the plan's grid rather than to the shapes drawn beside it — and the
release **asks** rather than tells: it writes through `PATCH /map/{slug}/intent/pieces/{id}`, addressed by
the play piece's id — a region's shape id is its piece's, a wing's is that id with `-building` after it and
`wing` naming which —
and a refusal puts the rectangle back where it stood and says why on the rail. That is the whole of what keeps the
two from diverging, since the sketch draws these rectangles out of the intent in the first place. Moving the
**region carries the room seated on it** — the marker players or a wool arrive at, the building raised on it,
the iron beside it, the entry interfaces its doors are cut on — because the region *is* the ground rather
than a zone drawn beside it, and leaving any of them behind puts it off the ground it belongs to. Moving the
**building** — any wing of it — moves the whole house alone, and one carried outside its region is refused
under `WX12`. Moving a
room's **point** moves the point alone, by whole blocks, and one carried outside the room's building — or its
region, where the room states no building — is refused; its rail shows the point's `X` and `Z`, and it turns
and is deleted only with its room. A wall's **end**, dragged by its grip in Layout, moves alone and is the one
drag that snaps, to the outline of a drawn shape (Layout, above). **A room's region, a build area and a water lane also resize**: a placed rectangle of another span is the
piece's new size. A room keeps its marker, its building, its iron and its doors at the same fraction across the
region — a point on the half-block it stood on, a wing on whole blocks — and is refused under 4 blocks across
(`WX2`), over 20×30 (`ST10`), or where a wing would come out under 4 blocks or its marker outside its wing. A
goal is centred on its anchor and a building is reshaped whole through its house, so either refuses a span other
than its own. **The whole orbit
moves**: every image of the piece's authored unit — the pieces of one kind sharing its stamp — moves by the
drag's displacement turned through the symmetry, so dragging red's spawn five blocks east across a mirror in x
moves blue's five blocks west, and a building is held inside its own region on every image it carries. A piece
stating no stamp is its own unit and moves alone, and the route's `alone` moves one image without the others.
The rest move the same way, block by block for the pieces that stand on whole blocks, and a build area or a
water lane, which carries no stamp, moves the areas standing where the symmetry carries it. Where the intent
states no symmetry — a compiled intent states none — the route takes the board's from the sketch's
setup. The same route **turns a room** a quarter at a time about the middle of its region, its doors and the way
its players face turning with it, and a mirror image turns the other way (`docs/tools/configure.md`); the rail of
a picked room, building, build area or water lane turns it with its two turn buttons.

The layout model carries one shape type the **Draw** dock cannot draw: a `circle`, which rasterizes as a 64-gon
and arrives only in a document written outside the editor.

A placed building's `wings` can state an L, a T or a U, and `Decorator` composes them into one house under one
style the way the stamper always could (`G177`). On the canvas each wing is drawn as its own rectangle and the
rectangles are joined with `mod+g`; the joined building is filled as one silhouette with each wing outlined
dashed inside it, the picked wing in the selection ink and wearing the transform box.

Symmetry here is a **preview and a mirror flag**, not a constraint. Shapes are drawn on one side and copied at
export for every group that opted in; nothing stops an author drawing across the axis, and nothing checks that
the two halves agree.

Almost nothing refuses a sketch. The only questions that stop one are whether anything was drawn at all and
whether a bound room style is one its own geometry can be built from; the board rules above are complaints a
map's findings carry, and there is no score over the drawing itself. What a board plays like is Configure's pre-flight and, past that, a human's.

**Only a stored board is pictured.** The world reads and the Review phase draw the world the *stored* layout
and intent build, which is why the phase saves before it draws. A working layout posted as a body — what
`Gauging the result` checks before saving — answers only in data: palette runs, contour polylines, the built
columns, and SVG for the finish previews.

**The pictures that do exist are now built to be read.** They used to imitate what a map looks like — grass
green, leaves green, stone grey — and a green tree on green ground was invisible in a picture meant for
reasoning; the top-down world read-backs and their per-layer isolations (ground, structures, foliage,
objectives) now paint deliberate false colour instead, one high-contrast hue per category, and the plan render
carries a key naming each ink and draws a build zone as a dashed outline, so a build zone is never read as water
(`B90`'s pictures, `B98`/`B95`'s legibility and key). The rule to work by is unchanged either way: **a render answers "did what was authored come out", not
"what is this"**: it is a check against the document, never a source of meaning on its own.
