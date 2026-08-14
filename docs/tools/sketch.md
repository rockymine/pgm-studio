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
**Draw**, **Relief**, **Theme** and **Dressing**. Only Draw and Theme's Apply step share the live canvas; the
rest are their own bodies, and Draw stays mounted while another is up so the drawing state and the zoom
survive the trip.

The tool saves continuously — every change schedules a debounced write 800 ms later — and leaves by
**Finish**, which flushes the layout, rasterizes it server-side into world geometry, and moves the map to
`stage=configure`. A draft that was never drawn on is discarded on the way out.

**What the later phases state, `docs/world-export/` executes**, and that folder is where the depth is. This
document is the tool: what each phase authors, what it writes, and what refuses. Beside it sit five that each
take one of those statements through to the blocks it becomes — `relief.md` (the elevation solver behind the
Relief phase, with the measured terrain law), `terrain-painting.md` (what the painter makes of a theme, cell by
cell), `structures.md` (the shells the Rooms step binds and the house the Dressing phase stamps),
`decoration.md` (the dressing pass itself) and `tree-corpus.md` (the hand-built ground truth a grown tree is
scored against). `docs/world-export/sketch-world-export.md` is the world folder Finish writes into. Each is cited
below from the phase that feeds it.

## What it writes

One artifact: the `sketch_layout_json` blob on the map row (`SketchStore`,
`src/PgmStudio.Api/Endpoints/SketchEndpoints.cs`). It is stored **verbatim** as the browser produced it —
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
| `layers[]` | the stacked slabs — each `{id, name, base_y, layout:{shapes, islands}}` |
| `layout` | a legacy single-layer document, read as one layer at `base_y` 0 |
| `themes` · `mapTheme` | the terrain-paint registry and the map-wide default |
| `roomStyles` | the two bound room shells — `cage` (wool) and `spawn` |
| `dressing` | every placed prop |
| `relief` | interior elevation, keyed by island id |

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
      "islands": [ { "id": "i1", "name": "Team island", "mirrors": true,
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

**A subtract is a hole, not a dip.** It takes the whole column out at every cell its outline covers, so its
own height is not read — a one-block-tall subtract carves exactly as deep as a hundred-block one. That is the
difference from relief, and it is the whole of it: relief moves a surface, a subtract removes it.

Height is two numbers. `floor` is where the shape's base sits and `base_height` is its thickness, so the
column spans `[floor, floor + base_height]`. A polygon whose `anchor_heights` line up with its vertices varies
that thickness per vertex, interpolated across the footprint as a TIN. A shape is never thinner than one block
and never floors below zero; a freshly drawn one starts at height 9.

Four further fields matter once an island carries a relief. `height_mode` — `level`, `raise` or `sink` — makes
a shape stand out of the solved field rather than be part of it: a mesa cut flat at an absolute height, a
plinth held a fixed amount above whatever ground it sits on, a quarry the same downward. `skirt` is how far in
from its own outline an erected shape eases back into the ground it meets, in blocks; zero is a sheer face,
which is right for a built thing and wrong for a landform. `relief_scope` is `hold` or `exclude` and decides
whether the shape's ground takes part in its island's relief at all (see *Islands and layers*); absent means
it is simply part of the island's ground. And a path carries `path_edge`
(`solid`, `rough`, `tapered`) with a `path_seed`, since a path is stored as the open centreline it was drawn
as and its band is derived.

A shape tagged with a `role` is not terrain at all. It is a spawn or wool-room piece the plan placed,
projected in so the room stays visible instead of dissolving into the fused island, carrying an `intentRef`
back to the entity it belongs to and a `color`. Role-tagged shapes are loaded as a locked render-only overlay:
never hit-tested, never edited, skipped by the rasterizer, and merged back into the saved document unchanged.

### Islands and layers

An **island** is not authored; it is computed. Every time the shapes change, the tool unions them and reports
the connected landmasses that result, so two rectangles pushed together become one island and pulling them
apart splits it again. What an author owns is the island's `name` and its `mirrors` flag — whether the group is
copied onto its symmetry orbit — and those survive a recompute by matching the island back to its previous
self. A single-member island shows the shape inspector rather than the island one, so a lone rectangle needs
no drilling.

The island matters beyond naming: **it is the unit a relief is stated against**, because a relief solved per
shape would leave a seam wherever two shapes met and disagreed about the height they share. One island holds
one relief, keyed by its id, and there is no way to give two parts of a landmass a relief each — two shapes
that touch are one island and share whatever it states.

**A shape can leave that solve, though, and this is where the fusion stops being a cage.** `relief_scope` on a
shape says how its ground takes part: `exclude` removes its cells from the field entirely, so the shape keeps
its own column — its stated floor, its thickness and any per-vertex tilt — while the relaxation treats the
footprint as a hole and bends around it exactly as it bends around the void; `hold` leaves the cells in the
field but pins them at one level, read at the shape's ring centre, and the surrounding land is then solved
knowing where it has to arrive. Held shapes are applied last, so one wins its cells outright rather than being
averaged against.

That is what makes a mixed board possible without a second relief. A flat rectangle with a raised step
attached to it is one island; marking the step `exclude` leaves it standing at exactly the height it was drawn
at while a relief shapes only the ground around it, and marking it `hold` flattens it to its own top and lets
the surrounding surface rise to meet it.

**`reach` bounds the statement; the word bounds the ground.** The fill is a screened-Poisson relaxation and
`reach` is the screening: the field decays back to `base` over a characteristic length of that many blocks, so
a finite reach makes each mark a local landform with plain ground between. The default is the trap —
**`reach` of zero means unlimited**, which is what a room-sized island wants and exactly what lets one mark
decide a whole fused board. But reach only says how far the *mark* travels. Every cell the relief covers takes
its height from the solved field, so a shape inside it has no height of its own left however local the marks
are; keeping a drawn height beside a relief is what `exclude` and `hold` are for.

The four outcomes are worth stating as measurements, over one plan — a 30×20 field at surface 9 with a 10×20
step at 19 attached to it, compiled to one island, carrying one bench five blocks below the base inside the
field. The readings are the built column top at the pit, out on the field, and on the step.

| The island's relief | pit | field | step | The board |
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

A **layer** is a stacked slab with its own `base_y`. The editor always edits the active layer; the others ghost
underneath in 2-D and stack in the isometric preview. A new layer defaults to ten blocks above the highest
existing one, and there is always at least one — the first is called `Ground` and sits at 0. A cell's column is
that layer's `[floor, top]` shifted by `base_y`, and the same `(x, z)` may appear on several layers, which is
how a bridge over a gap keeps both segments.

**An agent should author the ground layer only.** Stacking is for a person drawing an overhang by eye; a
generated board wants one slab.

### What the rasterizer makes of it

`SketchRasterizer.RasterizeColumns` turns the document into the solid cells of the finished world: one entry
per `(x, z)` with its span `[YFloor, YTop]`. Island mirror copies follow the saved `shapeIds` and the setup's
mode, so an island that opted out of mirroring is rasterized once.

**Two rasterizers have to agree, and five constants are what make them.** The live island preview runs the
boolean in the browser (`boolean.js`, over the vendored `polygon-clipping`) because it is the hot path; the
server rasterizes the same document authoritatively and re-detects islands. Neither is derived from the other,
so the drawn outline and the built one stay identical only as long as both sides read the same numbers:

| Constant | Value | Where |
|---|---|---|
| circle resolution | **64** points | `SketchRasterizer.CirclePoints` ⇄ `shape.js CIRCLE_POINTS` |
| Bézier sampling | **16** samples per curved edge, endpoint excluded | `BezierSamples` ⇄ `BEZIER_SAMPLES` |
| set-algebra order | adds − subtracts, then override-adds, then override-subtracts | both |
| `controls` keying | the vertex index as a **string** | `Dictionary<string, SketchControl>` |
| `rot_270` | `(Δx, Δz) → (Δz, −Δx)` | the internal third image of a `rot_90` orbit — never an authored mode |

Changing one without the other does not fail loudly; it produces a world whose edges disagree with the picture
by a fraction of a block, which is why the constants carry cross-referencing comments on both sides.

Where an island carries a relief, its surface is solved first (`ReliefFields`) and the same solve is what the
contour preview draws — which is the only reason a preview is worth drawing at all.

What becomes of those columns once Finish runs — the layer scheme the world folder is written in, its
`level.dat`, the coordinate anchoring, the wool-cage chests and the observer platform — is
`docs/world-export/sketch-world-export.md`.

## Phases

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

**Editing is per point.** A rectangle shows eight resize handles. A polygon (and a path's centreline) shows a
handle per vertex, a midpoint ghost on edge hover that inserts a vertex where it is clicked, and a pair of
Bézier tangent handles per vertex that round the edges leaving and arriving — which is how an outline stops
being rectilinear. A rectangle can be **promoted** to a polygon in place, keeping its id and therefore its
island membership. Selection can be rotated by a stated number of degrees about its own bounding-box centre,
nudged a block at a time with the arrow keys (sixteen with Shift), moved with snapping to other shapes' edges
(Alt bypasses), have its operation or its override flipped, or be deleted.

**Height is edited three ways.** The whole shape takes a floor and a thickness. A single selected vertex takes
its own height, which materialises the per-vertex array on first use. And two or three vertices shift-marked as
controls fit a plane through their stated heights and fill every remaining vertex from it — a ramp from two, an
aimed plane from three — rounding to blocks, so a slope reads as the neat straight steps of a staircase.

Six overlays sit above the canvas: **Shapes** (the draw primitives over the fused islands), **Mirror** (the
symmetry copies), **Chunks** (the 16-block grid), **Blocks** (the rasterized footprint — the exact cells an
export would fill), **Relief** (the height contours of whatever relief the islands carry) and **Snap**. A
read-only isometric preview extrudes every shape to its own height, carves it with the subtracts that apply,
mirrors it per orbit axis and depth-buffers the result, so where footprints overlap the taller column occludes;
it rotates in 90° steps and disables itself where WebGL is unavailable.

The sidebar carries the layer list and the island→shape tree; the inspector on the right edits whatever is
selected.

### Relief

One step, and the phase that turns flat plateaus into terrain. Everything here is stated **inside an island**,
so the island tree is half the sidebar and the list of what has been stated is the other half. Which of the
island's shapes the solve actually covers is not stated here but on the shapes themselves, through
`relief_scope` in the Draw inspector — a shape can hold its own level or leave the field altogether.

The island's own settings are what every mark is measured against: `base` (the level the field falls back to
where nothing is stated), `reach` (how far a mark's influence travels before the field returns to `base` —
zero is unlimited, and a finite value is what keeps a landform local on a large island), `step` (the
block quantum the finished surface snaps to), `stairs` (cut a way up out of ground the step stranded — worth
asking for whenever the step is more than one, since that is what turns a riser into a wall) and `grain` (a
wobble applied after the solve: amplitude, feature scale, seed).

Five things are placed, and they divide into two kinds.

| Tool | Kind | States |
|---|---|---|
| Spot height | `point` | a height at a place, over a radius |
| Ridgeline | `line` | a traced line at one height, or one height per vertex, over a width |
| Bench | `area` | a closed ring held at one height |
| Scarp | `scarp` | a traced line with a shelf above and ground below — `high`, `low`, the `face` it drops over and the `band` either side for the land to arrive through |
| Push | `push` | a closed ring lifted by an `amount`, with `falloff`, `roughness`, `crown` and a seed |

The first four are **marks**, and a mark is a constraint: the ground here *is* twelve. Two marks over the same
ground argue, and the solver settles it. A **push** is different in kind — it is applied to the solved surface
afterwards as a relative lift, so two pushes over the same ground simply add, which is what makes a spur on the
flank of a hill one operation rather than a restatement of the hill. A push's lift can vary per ring vertex,
which is what makes a drawn ridge fall along its length; setting every vertex to the same number collapses the
array back to the single amount.

A sixth mark, the **rim**, is not placed at all: it holds the island's whole outline, so it rides as a property
of the island's relief — one height and a depth.

The document is keyed by island id and carries the island's own settings beside the two lists. This one states
all six, and solves to a surface running 7 to 16:

```json
{ "relief": {
  "i1": {
    "base": 9, "reach": 14, "step": 1, "stairs": true,
    "grain": { "amplitude": 1.2, "scale": 12, "seed": 1 },
    "marks": [
      { "id": "r1", "kind": "point", "at": [-30, -10], "h": 15, "r": 5 },
      { "id": "r2", "kind": "line",  "points": [[-36, 6], [-20, 10], [-6, 6]],
        "h": [12, 14, 11], "width": 3 },
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
lines per island, at a stated interval, from the build's own solver, so what is drawn cannot differ from what
will be built. The **readback** answers what the stated terrain *charges* a player: reachability at each of the
three thresholds a player has (a jump, a placed block, building in earnest), places separated from ledges,
faces qualified as cliffs, crossings measured in both directions because a drop is free the way it falls, and
the symmetry error. It is asked for rather than pushed, since it is a second solve's worth of measurement.

`docs/world-export/relief.md` is this phase written out in full: the relaxation between the marks and why it
is that rather than a weighting, what each knob costs measured on a room and on a whole map, how steepness
decides where players can go, the symmetry fold that makes two halves identical rather than close, and the
corpus reading the whole model is calibrated against.

### Theme

Three steps, and together they are the map's paint.

**Create** is the theme editor. A theme is a recipe read top-down, with one section per paintable bucket —
**rim**, **surface**, **wall**, **fill** — each carrying a material and, for the rim and the surface, a depth,
plus the theme-wide knobs beside them: the bedrock course, `rimEdges`, and whether walls are painted on terrain
faces. What each bucket claims, how they fall through to one another, and the fourteen material kinds a bucket
can be filled with are `library.md`'s subject, written out there with a JSON example each; this step is where
they are edited for one map.

A new theme is a clone of the built-in default — a quartz rim, a team-tinted clay wall, grass over dirt — and
the thing being edited is the painter's own wire JSON, so there is no second model of a theme to fall out of
step. A theme can be pulled in from the shared library and pushed back out to it.

What the painter then does with it is `docs/world-export/terrain-painting.md`: how a column is classified into
one of the buckets from its neighbours alone, what each bucket claims when two could claim the same cell, how
a theme resolves per cell through the shape/map scope, and the `TP*` rules the whole pass is written to.

**Apply** turns the canvas into a selection surface: nothing can be drawn or moved, and picking an island or a
shape assigns a theme to it. A shape carries the assignment (`shape.theme`), an island assignment writes it to
every member, and a cell that carries none falls to the map default — so the resolution is shape, then map.
With the Blocks overlay on, this step shows the **real paint**: the live layout is posted to the server, the
actual painter runs over it, and one colour per footprint cell comes back as a bitmap, so a Voronoi reads as
its cells and a noise field as its patches rather than as one representative colour.

`tools/seeds/ruediger.layout.json` is the worked example of this step. It carries three themes and names
`ruediger` as the map default; four shapes take `ruediger-steps`, thirteen take `theme`, and the remaining nine
inherit the default. That is the pattern the step exists for — the stepped area reads as built and the ground
around it reads as ground, which a single blanket theme cannot do. The same file is the reference for two Draw
features: five of its outlines carry Bézier `controls`, and its one negative shape is a `subtract` rectangle
standing a hundred blocks tall over a floor of zero, which cuts a channel clean through the board and
demonstrates that a subtract's height is a statement of intent rather than a depth.

**Rooms** binds the shells the map's stamped structures take: one for every wool cage and one for every spawn
cube. Two bindings and no more — rooms are fanned across the symmetry orbit so both sides face the same
building, and a per-room shell would be a sightline that differed between teams. What is stored is the composed
style's **JSON snapshot**, not the library id it came from, so editing the library afterwards cannot rebuild a
shipped map's rooms. Each binding has three states, and they are genuinely distinct: absent stamps that kind's
built-in shell, an object stamps the bound style, and an explicit null means no building at all — a pad on open
ground. What a room style is made of — its parts, its course stacks and its storey stack — is `library.md`'s,
with a seeded house written out there in full.

```json
{ "roomStyles": { "cage": { "form": "gable", "pitch": 1 }, "spawn": null } }
```

That map stamps its wool cages with the bound style and gives its spawns no building at all. Leaving `spawn`
out entirely — rather than writing `null` — is the third state, and stamps the built-in spawn shell.

The stamp itself is `docs/world-export/structures.md`: how one `RoomFrame` is resolved from the piece rect,
the marker and the entry interfaces so the drawn box and the built shell cannot disagree, what the pad and the
doors are for, and the `WX*` rules that size everything. Its §7 is also where a room style's own anatomy is
written out — the roof as a height field, the storey stack, the porch taken out of the footprint — which is
what the Dressing phase's building prop stamps too.

### Dressing

One step, because dressing has nothing to define up front: every part of it is a thing put somewhere. The
sidebar is the list of what is placed, and the inspector edits either the selection or — with nothing selected
— the settings the next prop of that kind will start from.

**Dressing is authored, not sprinkled, and that is the whole design.** A tree is cover and a boulder is a
wall, so *where* each one stands is a decision about how the map plays and belongs to the person making the
map: the pass places exactly what was placed and nothing else. There is no scatter, no density pass over the
board, no "fill this island with forest". Within a drawn area the individual blades are a noise field, because
nobody places nine hundred of them by hand — but the area itself was drawn.

**One piece of ground answers back.** A destroyable and a core keep the ground they cover, grown by four
blocks, clear of anything that hides them: ground cover grows across it and under a floating monument, and
tall grass does not. `decoration.md` §3.1 is the rule and the reason. Every prop is then fanned across
the symmetry orbit, so one half of a map is dressed and both halves match. Each carries a `seed`, so two props
of the same kind and knobs differ from each other while any one prop re-exports identically.

`docs/world-export/decoration.md` is the pass this phase feeds, one section per tool and each carrying its
`DR*` rules: how a flora field reads the paint under it, how a path's band is derived from its centreline, how
a boulder is seated half-buried, how a tree is copied or grown, and how a channel is dug. Two of those reach
further. A grown tree is scored against `tree-corpus.md`, the 75 hand-built trees that are the measured ground
truth for what a tree looks like; and the building prop stamps `structures.md`'s house, which is why what it
can be made of runs past what this phase can state.

Six things can be placed, in three placement geometries.

| Tool | Kind | Placed by | Starts as |
|---|---|---|---|
| Path | `path` | tracing a line | gravel, radius 3, `solid`, coverage 0.7 |
| Water | `water` | tracing a line | a `canal` radius 3, cut 2 deep, a 2-block shore over a Voronoi bank |
| Ground cover | `flora` | tracing a ring | coverage 0.45 at scale 12, with fern and flower shares |
| Building | `house` | dragging a rectangle | no style of its own until one is picked from the room-style library |
| Tree | `tree` | a click | a `template` oak, height 12 |
| Boulder | `boulder` | a click | a `round` mossy stone, size 2.5 |

**Every one of them takes a style, and for the two that pave the ground the style and the material are
separate questions.** A **path** replaces the surface it crosses rather than adding to it — it is a finish,
not terrain, which is why it carries a material and no height, and why nothing grows on what it covers. Its
`style` shapes the *band*: `solid` holds one width the whole way for a clean utility road, `worn` thins it by
a per-cell dice (that is what `coverage` spends), `rough` wanders the width by a noise field so the outline is
organic rather than ruled, `stones` lays discs at intervals with gaps between them — stepping stones across a
void — and `tapered` runs it fat in the middle and thin at the ends. What fills the band is `pave`, a **full
terrain material**: a solid block, a layer stack, a Voronoi patchwork, a noise ramp, any pattern the painter
offers. The two are independent, so a worn cobble and a solid cobble are both sayable.

**Water** is the one prop that changes the ground rather than the surface: it cuts a bed and fills it to a
level water line, because water laid flat on a surface reads as blue paint. It only ever carves existing
terrain — the cut stops at the surface it crosses and never fills what was already air. Its `form` is `canal`
(a clean uniform width, deepest on the centreline), `natural` (the width wandered by noise) or `stream` (the
width pinching and swelling on a beat down the arc, running shallower throughout, so it reads as riffles
rather than one even channel). Around that: `radius` and `depth`, `edge` for how far a natural or stream bank
wobbles, `shore` for how wide a beach the water meets the land through with `shoreWander` for whether that
beach opens and closes along the run, and `bank` — again a full terrain material, defaulting to a Voronoi of
gravel edges, coarse dirt inside them and sand in the middle, which shows through the shallows and continues
as the beach.

**A tree is two different things rather than one thing with a switch.** A `template` tree is vanilla: its
`species` — oak, birch, spruce, jungle, acacia or dark oak — names its wood, its canopy profile and its
proportions together, since a notched cone is a spruce and a flat umbrella on a leaning trunk is an acacia,
and neither is a knob setting of the other; `height` scales the lot. A `grown` tree is the recursive skeleton,
where the shape is the author's and `wood` (the same six) is all that is left to name: `stems` one to three,
`leader` for how far the central axis climbs, `flow` for how much the trunk wanders, `branchAngle`, `levels`
two or three, `whorled` for the ring-every-few-courses conifer against the broadleaf, and `leafSize`. Each
form reads only its own fields, so the others are inert rather than wrong.

**A boulder** takes a `form` — `round` (one lobe, half buried), `angular` (one heavily eroded lobe),
`outcrop` (one wide flat lobe, a low shelf rather than a rock) or `cairn` (three shrinking lobes stacked) — a
`size`, a `mossy` flag for whether moss creeps onto the sky-lit faces, and `rock`, a full terrain material
like a path's paving. A rock's material resolves in the **boulder's own frame** rather than the map's, so a
mottled stone carries the same mottling to every image of its orbit instead of sampling whatever the world
pattern says where each image happened to land.

**Ground cover** is the one place a density field is the point: a drawn ring filled by `coverage` at a feature
`scale` over some `octaves`, split by `fernShare`, `flowerShare` (with its own `flowerScale`) and `tallShare`.

The pickers show **your** prop rather than a stock one. `GET /terrain/path-styles?pave=…` draws the five band
styles in the material already chosen, `/terrain/boulder-forms?rock=…` the four rock shapes in the author's
stone, `/terrain/water-forms` the three channels as actual dug beds, `/terrain/species` every vanilla tree
built, and `/terrain/woods` the six woods on the tree currently being edited — so the question answered is
"what would mine look like", not "what does the catalogue contain". `POST /terrain/prop-preview` renders one
before it is placed.

Two knobs are bounded rather than free, and it is load-bearing: a tree's height is held to 5–40 and its leaf
cluster to 0.2–1, and a boulder's size to 1–7. Cost is superlinear in reach — a grown crown is filled by
testing every cell of its bounding box — so a `leader` of 55 rather than 0.55 would not draw a strange tree,
it would ask for a volume hundreds of blocks across and never return.

The two clicked kinds are **markers**, and a marker seats on the ground: it can only be dropped where there is
terrain, and dragging one across the void simply does not follow, so it stays on the last real cell it was
over. The cursor shows in advance whether a spot will take it. A **building** is the only prop placed as a
rectangle, because a stamper takes a box: it must be at least three by three to hold two walls and an inside,
and no larger than 192 blocks of footprint, and a drag outside that range places nothing.

**A building prop states one or more touching rectangles.** Its `wings` field is a list of them, each the two
opposite corners a drag always stored, and `HouseProp.Footprint()` composes them into a `Footprint`
(`src/PgmStudio.Minecraft/Footprint.cs`) — **one or more touching rectangles**, the same shape `HouseStamper`
has always taken. An L, a T or a U is therefore one house under one style rather than two standing beside each
other: the outline is walked as a single landmass, so an L answers six runs of wall and a T eight, a wall ends
wherever the building turns, and the cell where two wings meet is an inner corner carrying a post of its own.
Each wing may stop short of the building's full height and may override the roof form, pitch and slab, and a
storey is then its own plan over the wings still standing — which is how a one-storey hall with a two-storey
cross wing gets the wall it needs against the hall's roof with no rule written for it. Each wing is still held
to the three-block floor a single rectangle always needed, and the whole plan to `MaxFootprint` (192 blocks)
measured over the ground the wings actually cover rather than the box drawn round them, so an L takes no more
of the cap for reading larger on the corner it never stood on (`G177`).

**The roof over a junction is built and has two behaviours.** A building's roof is the union of its wings'
roofs, and each wing is extruded as the whole building it would be alone; where their plans overlap, **only the
highest surface over a cell is written**, so the lower one does not stand inside the higher as an obstruction in
the attic. Where a wing's gable end runs up against another wing rather than into the open, its roof
**marches** — carried across the wing's own width with no overhang, since an overhang is what a roof has
outside a wall and inside another wing there is no outside — and bounded by the marching course's own distance
from its own eave, so a course whose crown never meets a shallower or flatter neighbour's still stops rather
than running the neighbour's whole length (`G172`).

Whether two wings make a junction at all is whether their **ridges cross**, and a wing's proportions cannot
know that: a roof pitches across the shorter side, so a 10 × 5 hall and a 7 × 6 wing both ridge along x and
meet in a gutter, and a **square** wing ties toward x and can never cross anything. `Wing.Ridge` states the
axis where the proportions should not decide it. Note it is a **model** field rather than an authored one —
none of a wing's overrides (its ridge, its roof form, its pitch, its slab, its storey count) reaches the
dressing document yet, where a wing is two corners and nothing else (`G184`).

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
one `Footprint` before anything is placed. The canvas still only ever drags one rectangle at a time: a second
wing is something a hand-authored or agent-authored document can state today, and the drawing tool to add one
on the canvas is not built (`S60`).

The document is a flat list of what was placed, in placement order, each entry carrying its own knobs. One of
each:

```json
{ "dressing": { "props": [
  { "id": "d1", "kind": "path", "seed": 1, "points": [[-36, 0], [-20, 4], [-4, 0]],
    "radius": 3, "style": "worn", "coverage": 0.7,
    "pave": { "kind": "solid", "id": 13, "data": 0 } },
  { "id": "d2", "kind": "water", "seed": 2, "points": [[-30, -16], [-16, -12]],
    "radius": 3, "depth": 2, "form": "stream", "edge": 0.8, "shore": 2, "shoreWander": true,
    "bank": { "kind": "solid", "id": 12, "data": 0 } },
  { "id": "d3", "kind": "flora", "seed": 3,
    "points": [[-38, 8], [-26, 8], [-26, 18], [-38, 18]],
    "spec": { "coverage": 0.45, "scale": 12, "octaves": 3,
              "fernShare": 0.25, "flowerShare": 0.18, "flowerScale": 18, "tallShare": 0 } },
  { "id": "d4", "kind": "house", "seed": 4, "points": [[-22, -16], [-14, -8]],
    "front": "negZ", "style": {} },
  { "id": "d5", "kind": "tree", "seed": 5, "x": -32, "z": 2,
    "form": "template", "species": "birch", "height": 12 },
  { "id": "d6", "kind": "boulder", "seed": 6, "x": -8, "z": 14,
    "form": "cairn", "size": 3, "mossy": true,
    "rock": { "kind": "solid", "id": 1, "data": 0 } }
] } }
```

The three geometries are visible in the shape of the entries: a marker carries `x`/`z`, a traced prop carries
`points` — a line for a path or a channel, a closed ring for ground cover — and a building carries the two
opposite corners of its rectangle. `pave`, `bank` and `rock` are full terrain materials, so any of the
fourteen kinds in `library.md` may stand there; a building's `style` is a `HouseStyle` snapshot, and `{}`
means the built-in shell.

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
tool saves it. Six things nonetheless refuse.

**A bound room style is checked before the layout is stored.** `PUT .../sketch` reads `roomStyles.cage` and
`roomStyles.spawn` off the posted layout and runs each through the same house-style gate
(`docs/tools/library.md`'s Refusals, rule ids `HS1`–`HS3`) — a block named for a geometric role that is not
that kind of block, a doorway that does not clear 2.5 blocks once its head is written in, or a roof whose own
materials are wrong for its pitch or its family. The cage and the spawn are checked identically: a stair
lattice or a slab band window is allowed on either, as it is on any house, so long as its block is the kind the
form needs. Answers **400** `{error, findings}`, one finding per fault, and writes nothing. A layout with no
`roomStyles`, or one whose snapshot does not parse as a house style at all, is not this gate's business and
saves as it always did — only a well-formed style that is wrong is refused.

**Finish refuses an empty board.** `POST .../sketch/finish` answers 422 when there is no stored layout at all,
and again when the layout rasterizes to no ground. It does *not* ask for two islands: an island is a connected
landmass rather than a side, and one continent both teams stand on is a common and correct shape. Symmetry
decides whether a board has two sides, and it is stated in the setup rather than counted in the ground.

**A recompile refuses to orphan a relief.** `PUT .../sketch/from-plan` answers **409**, listing the islands
whose relief the new geometry has no home for, and writes nothing. Island identity is derived from the
geometry, so a recompile that re-fuses the board does not merely move an island — it produces a different one,
and terrain authored against the old fusion has nowhere correct to land. `?force=true` accepts the loss and
proceeds, which is the author's call and not the server's.

**A building refuses a footprint it cannot stamp** (under 3×3, over 192 blocks), and **a marker refuses the
void**, as above.

**Heights are clamped rather than refused**: a shape is never thinner than one block and its floor never dips
below zero, whatever is asked for.

And two things are silently **dropped on load** rather than carried: a prop whose kind the client does not
know, and a relief mark whose kind it cannot draw. A shape nothing can edit is worse than an absence.

## The API

Every endpoint is anonymous and rooted at `/api`.

**The map's layout**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /sketch` | `{name?, width?, depth?, mode?, centerX?, centerZ?}` | `{slug}` — a `map` row at `stage=sketch`. A frame seeds the `setup`; without one the layout is `{}` and the editor uses its 120×80 `rot_180` default | — |
| `GET /map/{slug}/sketch` | — | the stored layout, or `{}` | 404 |
| `PUT /map/{slug}/sketch` | the layout | `{ok: true}` — a **verbatim replace**, which is what makes a deletion stick | 400 non-JSON, or 400 `{findings}` on a bound room style the house-style gate refuses · 404 |
| `PUT /map/{slug}/sketch/from-plan` | a compiled layout | `{ok, orphaned}` — merges the finish, the relief and any author-corrected structural height onto fresh geometry | 409 `{islands}` orphaned relief (`?force=true`) · 400 · 404 |
| `POST /map/{slug}/sketch/finish` | — | `{slug, configureUrl}` — rasterizes to world geometry, moves the map to `stage=configure` | 422 no layout, or no ground |
| `DELETE /map/{slug}/sketch/discard-if-empty` | — | `{discarded}` — drops a draft still at its default name with no authors and nothing drawn | — |

**Previews over a live layout.** All three take the working document as the body rather than reading the stored
blob, so they track unsaved edits, and all three answer 400 rather than 500 on a layout they cannot process.

| Endpoint | Answers |
|---|---|
| `POST /map/{slug}/sketch/paint` | the painted surface as palette-indexed block pixels — the real painter's output, with team tints resolved from the stored intent |
| `POST /map/{slug}/sketch/relief[?interval=]` | `{interval, islands[]}` — per island its height range, its bounds and its traced contour lines, from the build's own solver |
| `POST /map/{slug}/sketch/relief/read` | `{islands[]}` — per island the cell count, low/high/relief, steps, tiers, the first twelve faces and the total, cliffs, crossings in X and Z, and the symmetry error |

**The finish libraries**, all map-independent and all `library.md`'s to describe. `/styles` and `/themes` are
the terrain-paint library a theme is pulled from or pushed to; `/room-styles` is the shell library the Rooms
step binds from, with `/room-styles/{id}/json` for the stamper's own form; `/roof-styles`, `/storey-styles` and
`/porch-styles` are the parts a room style is composed from. `/terrain/blocks` is the
block palette, and `/terrain/material-preview`, `/terrain/theme-preview`, `/terrain/theme-map-preview` and
`/terrain/prop-preview` render what an edit will look like; `/terrain/path-styles`, `/terrain/water-forms`,
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

### Gauging the result

Everything below runs on the **working layout posted as the body**, so a sketch can be checked before it is
saved and without a browser. What matters for an agent is which of them answer in numbers, which answer in a
drawing, and which answer in a raster it can actually open.

**Three read the sketch itself.** `POST .../sketch/paint` runs the real painter and answers the surface as
palette-indexed runs — the exact colour of every footprint cell, which is how a Voronoi reads as its cells
rather than as an average. `POST .../sketch/relief[?interval=]` answers the traced contour lines per island
from the build's own solver, as flat `[x, z, x, z, …]` runs. `POST .../sketch/relief/read` answers the terrain
in **numbers**: per island the cell count, low, high and relief, the step count and tiers, the faces with
cliffs qualified, crossings measured in both directions, and the symmetry error. That last one is the one to
reach for first, because it is the only preview that says whether terrain is any *good* without an eye — it is
what makes a relief correctable by a generator.

**The finish previews draw, in SVG.** `POST /terrain/material-preview` and `/terrain/theme-preview` answer a
material and a theme as they will paint — the theme as a cut-open sample plateau plus a top-down swatch per
bucket. `POST /terrain/prop-preview` and the five card sets (`/terrain/path-styles`, `/water-forms`,
`/boulder-forms`, `/species`, `/woods`) answer a prop as it will be built. `POST /room-styles/preview`, its
`-snapshot` twin, and `/roof-styles/preview`, `/storey-styles/preview`, `/porch-styles/preview` answer a
building in plan, section, isometric and cutaway. All of them return **SVG text inside JSON**, so an agent
gets markup it must render to look at.

**After Finish**, the map holds rasterized world geometry and three more reads open up:
`GET /map/{slug}/layers/top-surface` for the per-column surface colours, `GET /map/{slug}/segments` and
`GET /map/{slug}/column-floor`. Data again, not pictures.

**No picture of a sketch exists as an endpoint.** The API's only raster is the plan board —
`GET /plans/{id}/png` beside its `/svg`, both off one shared scene so the two encodings cannot disagree
(`B90`). The named stage images an agent can genuinely look at — **plan, heightmap, contour, surface,
dressing, topdown, foliage, objectives, traversability, structures** — are written by `tools/mapgen --stages`
off the `VoxelWorld` the build just produced, and the same read-backs are CLI flags on
`tools/PgmStudio.RoundTrip` (`--topdown`, `--heightmap`, `--contour`, `--surface`, `--structures`,
`--traversability-map`) over a built world; `--topdown` also takes `--layer ground|structure|foliage|objectives`
to isolate one of those questions instead of drawing the combined view. So an agent wanting to *see* a sketch
has two honest options: render the data it already gets from the three reads above, or build the world and
take the stage set.

Two things are worth knowing before hand-writing a document. **Editor defaults and wire defaults are not the
same numbers.** A mark placed in the editor is seeded from the client's own starting values; a hand-written
mark that omits a field takes the C# record's default, and several differ — a relief omitting `base` reads 4
where the editor would have seeded 8, a spot height omitting `r` reads 2 where the editor seeds 4, a ridgeline
omitting `width` reads 1.5 where the editor seeds 3, and a push omitting `amount`, `roughness` or `crown` reads
zero for all three. State the fields rather than relying on either. And **a mark that does not carry what its
kind needs is dropped, not defaulted**: a point without `at`, a line or scarp with fewer than two points, an
area or push with fewer than three ring vertices simply does not reach the solver.

`tools/seeds/ruediger.layout.json` is the readable example for the geometry, the carve and the theme
assignment. It states no relief, no dressing and no room styles, so it is not a reference for those three.

## Limits

Sketch draws the ground and everything on it, and it does not know what any of it is *for*. It has no teams,
no spawns, no objectives, no build regions and no protection: a map begun here reaches Configure with geometry
and nothing else, and a map that came from a plan carries the plan's intent unchanged through this tool.

The structural pieces a plan projects in are **read-only here**. A spawn or wool room is rendered so it stays
visible while the ground around it is refined, but it cannot be selected, moved or reshaped, and a destroy
objective has no sketch presence at all. The backend half of correcting a structural piece's height across a
recompile is in place; the canvas half is not (`B107`).

The layout model carries two shape types the **Draw** dock cannot draw. A `circle` rasterizes as a 64-gon, and
a `path` shape is a centreline with a band whose width, edge style and seed the inspector edits — but nothing
in the Draw dock creates either, so both arrive only in a document written outside the editor. The path shape
is not the Dressing phase's path tool and the two do different things: a `path` **shape** is terrain, drawn
into the ground and rasterized as a footprint, while a `path` **prop** repaints the surface it crosses and
adds no cell. The tool exists for the prop; the shape has none.

A placed building's `wings` can state an L, a T or a U, and `Decorator` composes them into one house under one
style the way the stamper always could (`G177`) — but the canvas itself still only ever drags one rectangle, so
reaching a second wing today means writing the document by hand or generating it. The drawing tool to add one
on the canvas is `S60`'s open half.

Symmetry here is a **preview and a mirror flag**, not a constraint. Shapes are drawn on one side and copied at
export for every island that opted in; nothing stops an author drawing across the axis, and nothing checks that
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
