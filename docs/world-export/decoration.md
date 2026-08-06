# Decoration — flora, paths, scatter, canopy (G34, slice G161)

A third pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
terrain painting of `terrain-painting.md`. Where the painter dresses the terrain's *material* — grass over
stone, quartz on the rim — this pass adds the terrain's *life*: tall grass and flowers scattered on the
soil, a worn path dragged across it, boulders and trees seated on top. It is the second half of the
theming work parked as G34: G157 carved out the terrain slice ("no new geometry, only materials"); this is
the **prop-stamps** slice, and it is the opposite by construction — it exists to add geometry.

**All five tools ship: paths, ground cover, boulders and trees (G161), and water channels (G169, §7).** Water
is the one that changes the ground rather than standing on it — its carved bed is an elevation change the rest
of the stage never makes — so only its richer reads and the closed pond form remain open under `G169`.

**Dressing is placed, not sprinkled.** This is the stage's central decision and it took a rewrite to reach:
the first cut authored *recipes* — named density fields assigned to shapes, the way a terrain theme is — and
that is wrong for a prop. A tree is cover and a boulder is a wall, so where each one stands decides how the
map plays, and a decision about how a map plays belongs to the person making it rather than to a noise
field. So every part of this stage is a thing put somewhere: a route is dragged, an area of cover is traced,
a tree and a rock are clicked into place, and each carries its own knobs. The fields that remain are the ones
*inside* a drawn area — which blade of grass, which cobble — where placing them one at a time would be data
entry rather than authoring. The model was worked out against a live prototype —
`tools/decorate/prototype.html`, one self-contained page whose every figure (the noise fields, the path
variants, the boulder elevations, the grown tree, the forest scatter) is emitted by the real algorithm,
not hand-drawn. Read it alongside this doc the way `showcase.cs` reads alongside `model.md`: when prose and
prototype disagree, suspect the prose. Rule ids here are `DR*` (dressing), local to this file the way
`structures.md` owns `WX*` and `terrain-painting.md` owns `TP*` — `rules.md` is compose-scoped and frozen,
so decoration law lives in the world-export docs, not there.

A note on the name. In this codebase **"decorative"** already means two settled things — the non-playable
floating masses the island detector prunes (dragons, birds), and the non-objective wool a map places for
colour. To keep those apart from *this* stage the code family is **dressing** (`DR*`, a `Dressing` pass, a
`DecorationStamper` is the one concession to the human word); the doc keeps "decoration" only as the title,
where the collision cannot bite.

Read alongside:

- `docs/world-export/terrain-painting.md` — the pass this one runs immediately after; the surface it reads.
- `docs/world-export/structures.md` §6.4 — the preset seam (style-as-data). A dressing style attaches here.
- `docs/contracts/sketch-authoring.md` §2 — the sketch document this stage stores its props beside.
- `docs/generator/ideas.md` — G34 (the umbrella), G32-C (structures & elevation, the sibling pass), G142
  (the roughen pass, whose noise operators the path edge borrows).
- `docs/world-export/ideas.md` — the dressing-stage gap pool: what turns these tools into one coherent
  stage. The fairness rule (G162) is answered here in §2; what remains is the guardrail budget,
  vertical-surface dressing, the arbitration contract, a biome theme, context-aware placement, and
  border/POI framing.

---

## 1. The pass's place

A generated layout compiles into a `SketchLayout` (geometry) and a `MapIntent` (objectives). The rasterizer
turns the geometry into a stone `VoxelWorld`; the stampers seat rooms, cubes and objectives on it; the
painter rewrites the stone surface into grass, dirt, quartz, clay. Every one is a pass in *realize*, and
they run in that order for one reason: each reads what the last produced. The dressing pass is the next
link — it reads the finished, **painted** world and the plan's protected regions, and it runs **last**,
after `TerrainPainter.Paint` in `SketchWorldBuilder.Build`.

Running after the painter is what makes the whole stage tractable. The painter has already decided, per
cell, what the surface *is* — and the single fact the dressing pass needs is exactly that: soil accepts
flora, quartz does not; grass can be replaced by a path, a monument's wool cannot. So the pass reads the
top block of each column and the plan's protected regions, and never has to re-derive either. The one
elevation model it needs is the same `SketchTerrain.SurfaceTop` (`Dictionary<(int X,int Z),int>`, the
first air Y above each column) the painter and every stamper already read.

The break from the painter is the geometry. `TerrainMaterial.Resolve` only ever answers *which block* a
stone cell becomes — it cannot add a cell. Flora, boulders and trees **are** added cells: a tall-grass
block in the air at `SurfaceTop`, a rock volume, a trunk and canopy. So the dressing pass is not a new
material — it is a sibling pass that calls `VoxelWorld.SetBlock` (and `AddTileEntity` for a potted or
NBT-bearing prop) above the surface. That it runs after the stone-only painter also means it can place
non-stone freely without the painter overwriting it.

## 2. What every part shares

The parts below read as four features but run on the same handful of primitives, which is the argument for
one stage rather than four:

- **Noise.** The same deterministic `PatternNoise` (`Hash`/`Unit`/`Value`/`Fbm`) the terrain patterns are
  built from. A density field decides where flora goes; a low-frequency field gathers flowers into meadows
  and trees into groves; a per-cell `Unit` is the dice a worn path rolls. Deterministic hash-from-cell,
  **never RNG** — the discipline `terrain-painting.md` §5 already holds, so a map re-exports identically.
- **Mask.** Eligibility from the painted surface (soil vs. quartz, read from the top block) and from the
  plan's protected regions (spawns, objectives, structures) as exclusion zones. Nothing lands where it would
  break play or read wrong. A path's own cells join the mask as it is laid, so nothing grows through a road.
- **Placement.** A **point** for the props that stand somewhere (a tree, a boulder) and a **drawn outline**
  for the ones that cover a stretch (a route along a line, cover inside a ring). Two interactions, and the
  split is the model's: a marker is a click because a spot is a click, and an area is a drag because tracing
  is how a stretch of ground is described.
- **Stamp.** A 3-D volume seated on `SurfaceTop` and written cell-by-cell with `SetBlock` — the
  shape-mask-in-a-box `ObjectiveStamper` already uses for destroyables and cores. A prop seats on the
  *lowest* column of its own footprint, so it sits into a slope rather than floating over the low side, and
  it refuses ground that is missing or protected rather than half-placing itself.
- **The fan (G162).** Every prop is placed once and stamped at **every image of its orbit**, in the prop's own
  local frame, with each offset **turned** by that image's transform. An author draws one half of a map and
  gets a fair one, which is the contract the layout itself has had all along — and the canvas draws the
  images as ghosts, so half a map is never authored blind. Within a drawn area the one gameplay-affecting
  species, two-block grass, is decided on the orbit representative (`OrbitScatter.Canonical`) so the same
  ground is tall or bare for every team, while the flowers beside it stay free: two identical flower beds
  read as a glitch and decide nothing.

  Fanning the *site* alone is not enough and this is the mistake worth naming: mirroring only the anchor
  leaves both teams with the same unmirrored prop shape, so a boulder with a lobe to its east has a lobe to
  its east on both sides of the map. The prop has to be built as offsets from an anchor and the offsets
  turned, which is why every generator here answers in a local frame. An offset is a delta between cells,
  and the delta between two mirrored cells is just the mirrored delta — so the turn is a plain rotation with
  no half-cell correction, which the anchor's own mirroring has already applied. A stamp is all-or-nothing
  per image: if one image's ground is missing or protected, that image is skipped rather than clipped.

## 3. Flora overlay — the paint-aware overlay (`DR-FL`)

The plainest decoration is WorldEdit's `//overlay`: put 70% air and 30% grass-and-fern on top of
everything. The studio can do better than *everything* because it just painted the surface. Grass, fern
and flowers belong over grass-block, dirt, coarse dirt and podzol, sparsely over sand, and nowhere over the
quartz of a plaza or the wool of a monument. The overlay is masked by the paint beneath it — the eligible
set is a property of the top block, read the way `TerrainProfile` reads a column.

Two knobs do the work, and both are noise. A **density field** decides which eligible cells get anything:
white noise gives an even TV-static speckle, value noise (`Value`/`Fbm`) clumps into meadows and clearings,
blue noise spaces plants so no two touch — the look of deliberate placement. A flat threshold on the field
is the line between a sparse verge and a lush meadow. Within the vegetated cells a **species mix** decides
what: a second, low-frequency field paints flower *fields* — poppies and dandelions cluster where it peaks,
so the map gets meadows of colour instead of confetti, with grass and fern filling the rest by a share
noise. A cell that the density field rejects simply stays air; that air is the "70%".

`DR-FL` walks the cells inside a **drawn outline**, and for each one whose top block is soil and whose
density field clears the threshold, sets a plant into the air cell at `SurfaceTop`. The outline is the
authored part and the field is what fills it: nobody wants to place nine hundred blades of grass, and nobody
wants grass everywhere either. It adds one block per cell and never touches the ground — the lightest of the
four passes, and the one that reuses the most. `FloraSpec` carries the knobs: `Coverage` against the
density field, `Scale`/`Octaves` shaping it, and `FernShare`/`FlowerShare`/`FlowerScale`/`TallShare` mixing
the species. `DressingPalette.SoilShare` is the eligibility read — sand takes a fraction of what grass does,
quartz none.

The split that matters here is not a species one. Grass, fern and flowers are one block tall and walked
straight through, so they are **cosmetic** and scatter freely; the two-block `TallGrass` and `LargeFern`
break a sight line and are marked **gameplay**, which routes them through the fan of §2. A plant's
`PropClass` is declared on the palette row, not inferred at placement.

## 4. Paths — drag a line, replace the finish (`DR-PA`)

A path is drawn the way the lasso is: press, trace, release. Where the button comes up is the last point,
which is the whole interaction — there is no separate way to finish and so no way to get stuck mid-draw. The
drag is one point per block of pointer travel, so it is simplified on release to the points at real bends
(the *open* Douglas-Peucker, not the ring simplifier: a route has a direction and the ring version would
reorder it).

What is stored is that route and a half-width, and nothing else. Every cell within the radius of the nearest
centerline segment takes path material, **replacing the surface finish beneath it** — a path adds no cell. A
route is a finish laid over ground that already exists, so it can run over a slope without becoming a ramp,
and a bridge across a void stays the draw phase's job. Its cells become bare ground as they are laid, so
nothing grows through the road.

The single solid band is the boring case; the imperfect paths are the point, and all six of them are the same
distance field with one extra gate (`Geom.PathStroke`):

- **Solid** — `dist ≤ R`, a clean utility road.
- **Worn** — and a per-cell dice below a coverage threshold: gravel scattered thin, a trail rather than a road.
- **Rough edge** — `R` perturbed by an `Fbm` sample, so the band's outline is organic, not ruled. The same
  operator family as the G142 roughen pass's edge displacement.
- **Cobbled** — the band tiled by a jittered grid, each patch taking one of the path's blocks: the cobbled
  read of `VoronoiMaterial` at a footprint's scale, and the one style that spends more than the first block.
- **Stepping stones** — discs sampled at intervals along the centerline's **arc length**, with gaps between:
  the disconnected path, stones across a void.
- **Tapered** — `R` varied along the arc (fat middle, thin ends).

Being a *fill* rather than an *outline* is what makes all six possible. An earlier cut made a path a
`SketchShape` whose closed ring was rasterized as terrain, and two of these could not be expressed that way
at all — worn and stepping stones gate cells, not a boundary — so they had to be filed rather than built.
Placing the path in the pass instead costs nothing and gets them back, because the pass was already writing
cells one at a time.

The **outline** still exists, in `Geom.PathBand`, and does a different job: it is what the canvas strokes to
show where a route runs. The two deliberately differ — an outline cannot draw a gap — so the preview shows
the corridor and the fill decides what within it is paved.

## 5. Boulders — half-buried, scattered (`DR-SC`)

A boulder is the first decoration that is genuinely 3-D, and it is the same shape-mask-in-a-box the
objective stampers already build. Seat a `BlockBox` on `SurfaceTop` (via `SurfaceYOver`), then fill the
cells that pass an ellipsoid test — `((x−cx)/rx)² + (y/ry)² ≤ 1`, the squared-distance mask `StampCore`'s
`Inset` and `StampDestroyable`'s `InPlusSection` are the precedent for. Half the ellipsoid sits below the
surface, so the rock reads as emerging from the ground rather than dropped on it. Perturb the radius with a
noise sample for an angular, weathered read; stack two or three lobes for a cairn; flatten `ry` for a wide
outcrop. The finish is a material and a micro-mask: stone, andesite, mossy cobble, blackstone — and moss
creeping onto the top-lit faces, itself a tiny `Unit` mask, so the finish carries its own micro-flora.

A `BoulderProp` is placed at a cell and carries its own form (round, angular, outcrop, cairn), size, finish
block, moss flag and seed. `BoulderShapes.Of(form, size)` answers with the lobes and `Geom.Blob` fills them
— a quadric eroded by a noise field sampled in the lobe's own frame, which is what makes an angular rock
angular rather than a dented sphere. A boulder is a solid volume standing on the ground, so where it stands
is cover, which is why it is placed rather than scattered.

## 6. Trees — copied and grown (`DR-TR`)

The trivial tree copies vanilla: a trunk of a known height and a canopy of a known profile, parameterised
per species — oak's blob, birch's tall slim crown, spruce's layered cone, acacia's leaning flat top,
jungle's broad canopy, dark oak's two-wide trunk. Each is a parametric template (trunk height, canopy
radius, canopy profile), not a copied schematic, and an oak grove is genuinely all most maps need.

The interesting tree is **grown**, and the shape that reads as a tree is neither a fractal nor a straight
pole — a recursive brancher reads as a fractal, and one clean spline reads as a mast. The model that works
takes both halves. The skeleton is **recursive** — the ez-tree knobs of levels, children per level, and a
per-level radius and length taper, with a gentle upward *force* — but **every branch is a gnarly stepped
path smoothed into a flowing Catmull-Rom curve**. The trunk carries a slow noise sway so it wanders like a
bonsai (up, twist, straighten) rather than rising dead straight; the limbs carry a sharper per-step jitter;
both are pulled gently upright. Limbs branch to a **second degree**, and the leaf clusters gather on the
outer tips. The trunk is a **continuous central axis**, not a stub that forks and stops: a **leader** knob
sets how far it climbs — at a low leader it dissolves into a spreading fork (a decurrent oak), at a high
leader it carries on up through the crown as one dominant spine, thinning and twisting, with branches
staggered up its length and a small fan at the top (an excurrent birch or conifer). A **stems** knob gives
the base setups a real tree has — one stem, a double, or a triple. A **height** sets the tree's size in
blocks, and it is not a uniform scale: a smaller tree carries a **thinner stem at the foot** and **fewer
branches** (a branch shorter than a floor length is terminal, so recursion stops sooner), and its leaf
clusters shrink with it — a sapling is a few clusters on a thin stalk, not a shrunken big tree. The rest
(leader, trunk flow, branch angle, levels, leaf size) are hash-keyed so a seed always grows the same tree. This is the ez-tree lesson (Dan Greenheck) — a tree reads from **taper + curve + a canopy at the
tips**, not from branch count — married to the Catmull-Rom flow a Minecraft builder draws by hand in
Axiom's path tool. The stage renders it two ways: the **spine** (the centerlines and their thickness, each
limb spline in its own colour) is the view the shape is designed against, and the voxelised blocks are the
swept-disc fill of §4's path lifted one dimension (a capsule along a spline instead of a band along a
stroke), so the tree and the path share one rasterization primitive.

The crown is where a naive generator gives itself away — a spherical brush with holes punched in it reads as
one blob, and you cannot tell which branch a patch of leaves belongs to. So the crown is placed the way a
mapmaker does it by hand: **one dense disc-shaped cluster per outer tip** — a flat leaf-rock, the §5 boulder
disc reused, ~90% full — pushed up and out from the tip, never down toward the trunk, because leaves sit at
the branch ends and not on the wood. Neighbouring clusters keep a **seam of air** between them: a cell fills
only when it clearly belongs to one cluster (nearest-cluster ownership, the seam where two are equidistant),
so a viewer still reads each patch as its own branch's instead of one merged mass — which is the real reason
to keep the branch count low. A few short strands hang below each disc for a broken lower edge. The airiness
lives *between* the clusters, not as holes inside them.

Both trees are the same stamper as the boulder: a trunk-and-limbs volume plus a leaf mask over a box, and
both end at one place that turns those cells into blocks — so the wood, and the no-decay bit every leaf
carries, are decided once. `TreeTemplate.Build` answers the vanilla tree's wood and leaves from a
`TemplateShape`, its canopy a radius per course from `CanopyProfiles` (profile-as-data, the same seam
`BoulderShapes` uses for a rock's form). `TreeSkeleton.Grow` answers the grown tree's limbs and tips from a
`TreeShape`, `SweptVolume` fills each limb as a capsule along its spline, and `TreeCrown` places the
clusters and answers which one owns a cell.

A `TreeProp` is placed at a cell and names which of the two it is. **The forms are two trees, not two
settings of one**, and collapsing them is a mistake worth naming because it was made: six presets of the
grower, one per species, offer six silhouettes and build one. The grower has no notched cone and no flat
umbrella in it — there is no knob for a canopy that steps in as it rises — so a "spruce" preset is the
grower's own crown wearing spruce blocks, which is exactly the promise a drawn picker exists to refuse.
So a template tree names a **species** (`TreeSpecies`: its wood, its canopy profile, its proportions) and
scales it by height, while a grown tree names a **wood** (`TreeWood`: the log and leaf blocks) and is shaped
by the knobs above. Wood is the one thing neither form decides for the other, which is why it is its own
row and why the grown tree's picker is six cards of the same tree in six colours — where the species
picker's six cards must differ in shape, or they are one tree wearing six palettes.

A grove is therefore a handful of trees an author placed rather than a density field, and that is the
intended trade: a forest that clumps by itself is quicker to get and impossible to aim, and a treeline
across a lane is exactly the thing worth aiming.

## 7. Water — channels (`DR-WA`)

A channel begins exactly where the §4 path does — a dragged centerline and a radius, the same swept-disc
band (`Geom.WaterBed` reuses `PathBand.Centerline` and `Polyline`'s distance field). What makes water its own
tool is that it **cannot drape on the surface** the way gravel can: laid on a slope it reads as blue paint.
Water has to sit in a **carved bed** and fill to a **level plane**, so water is the one prop that takes the
ground *out* rather than standing on it.

The carve is a shallow U — deepest on the centerline, rising to a single block at the band's edge — so the
fill sits in a bowl rather than a walled trench. `WaterBed` yields a depth per cell from that parabolic law;
the dressing pass (`Decorator.PlaceWater`) turns each depth into a cut against the surface the cell actually
crosses. The fill is one **water line** for the whole run: the lowest surface the channel crosses. That level
is what keeps the water from floating — every column's surface is at or above it, so the pass fills from just
above the bed floor up to the line with stationary water and cuts any bank *above* the line back to air,
leaving the channel open. The carve **only ever touches existing terrain**: it never rises past a column's old
surface and skips any column the surface map does not carry, so a channel dug across a hollow keeps the hollow
and one dug over a stamp leaves the stamp — the same exclusion a path respects. Like every prop, a channel is
**fanned across the symmetry orbit**, so both teams get the same water from the same side.

The water meets the land through a **beach**. The shore is its own pass — the band *outside* the water, out to
a width that wanders with a noise field and drops to nothing in places, so the water meets the grass directly
in some stretches and spreads into a flat in others (`WaterBed.ShoreCells`). Both the beach and the bed floor
are laid with the channel's **bank**, and the bank is not a block but a full **`TerrainMaterial`** — a solid, or
by default a jittered-voronoi patchwork of sand, pale gravel and coarse dirt, the same pattern the painter tiles
and edited by the same `MaterialEditor`. So the floor the shallows show through and the shore the water meets
read as one ground, drawn from one palette.

Channels take a **form**, and it drives both the water and the land. A clean-banked **canal** holds a uniform
width and a narrow, even beach; a **natural** edge wanders its width (the FBM the rough path uses) and its
shore with it; a **stream** narrows and shallows towards its ends into riffles and spreads into wide shore
flats. The carve leans toward the **G32-C** elevation pass rather than depending on it: the bed is negative
terrain laid straight into the realized world, so a channel works on the flat layouts the sketch tool builds
today and will read as a cut valley once that pass gives a layout its heights.

**Still to come (`G169`).** The reads that take a channel from "a filled cut" to "water that looks like
water," and the closed form: **depth shading** warped off-centre so one bank runs deeper than the other; an
**irregular shoreline** whose width wanders to zero in places; a **voronoi-patterned** bed and shore (sand,
pale gravel, coarse dirt) showing through the shallows; **edge life** reusing the §3 flora overlay masked to
the bank (reeds, lily pads); and **ponds** — the closed version, an organic basin (the §5 boulder blob read
concave), scattered onto low ground and joined to channels into one watershed on a single water level.

## 8. What it reuses, and what it adds

The stage leans hard on machinery G157 and the sketch tools already shipped; the net-new surface is small
and lands in the same realize seam.

| Concept | Reuses | Net-new | Rule family |
|---|---|---|---|
| Ground cover | `PatternNoise`; `SurfaceTop`; the `TerrainProfile` column read | the overlay pass, gated by a drawn outline — one `SetBlock` above the surface | `DR-FL` |
| Paths | `CatmullRom`; `Ribbon`; `Polyline`'s distance field; the lasso's own press-trace-release | `PathStroke`'s six gates; `PathBand` + its `geometry/path.js` twin for the drawn outline | `DR-PA` |
| Boulders | `SurfaceTop`; the squared-distance masks the objective stampers fill by | `Blob`; `BoulderShapes` | `DR-SC` |
| Trees | the boulder's seating; `CatmullRom` for the limb splines | `TreeSkeleton`; `TreeCrown`; `SweptVolume`; the species rows | `DR-TR` |
| Water | the §4 path stroke's band (channels); the §5 boulder blob + FBM edge (ponds); the §3 flora overlay (reeds) | `WaterBed` + `Decorator.PlaceWater` — the carve-and-level bed (shipped); depth shading, the shoreline band, ponds (G169) | `DR-WA` |

Two neighbours bound the stage. G32-C (structures & elevation, the "second generator") is the sibling pass
that gives a flat layout its heights; a boulder or tree seats on whatever surface that pass leaves, so the
two compose but do not depend on each other — and water, whose carved bed **is** an elevation change, cuts
its bed straight into the realized world, so it works on today's flat layouts and simply reads as a cut valley
once G32-C gives a layout its heights. G142 (the roughen pass) shares this stage's architecture —
last in realize, over the authored unit, symmetry re-fanned — and its edge-displacement operator is the
path's rough edge; if both land, they share the noise operators rather than duplicating them.

## 9. Where the code lives

The organizing rule is the repo's own (`CLAUDE.md`): a unit of code lives in the lowest project that has the
deps it needs and every consumer can reach — push pure algorithms down to the leaf, keep world-writing where
the world is. The dressing stage straddles two projects, and splits cleanly along that line.

**`PgmStudio.Geom/Algorithms` — the pure math, shared by every generator and its JS twins.** The folder was
always meant for geometry algorithms, and the dressing math is exactly that; three of the pieces already sit
there:
- `CatmullRom` — centripetal spline. Smooths the tree limbs and the path/channel centerline (the same
  algorithm the tree prototype reimplements in JS — reuse it, don't re-add it).
- `Ribbon` — offsets a centerline into a strip outline (`Uniform` fixed-width, `Varied` variable/tapered/
  organic). This **is** the DR-PA/DR-WA band; the code is the old orphaned `Lane` class (long-dead sketch-tool
  approaches), kept but **renamed**, because a "lane" is a layout role (a run of pieces), not a geometry
  primitive. `Varied` gives the tapered channel and the jittered shore in one call.
- `PatternNoise` — hash/value/fbm, **migrated here from `Minecraft`** (it was pure but trapped beside the
  terrain-paint materials). The pattern *materials* (`VoronoiMaterial`/`NoiseMaterial`/`WallRunMaterial`)
  stay in `Minecraft` and call it. The move added the one edge that was missing, **`Minecraft → Geom`**, which
  is what lets the dressing stamper reach any of this.
- `Blob` — the eroded quadric a boulder lobe is; `SweptVolume` — the capsule a tree limb is; `Polyline` —
  the distance field a stroke of any width is filled by.
- `TreeSkeleton` and `TreeCrown` — the grower and the crown placement, as abstract limb centerlines, radii
  and leaf-cluster centres. No block ever appears in either.
- `TreeTemplate` and `CanopyProfiles` — the other tree: a trunk under a canopy whose profile is a radius per
  course. It is a sibling of the grower rather than a mode of it, because the two build different shapes.
- `PathStroke` — which cells a stroke paves, one gate per style; `PathBand` — the outline the canvas draws
  it as, and the one C# side of the `geometry/path.js` parity pair; `WaterBed` — the same swept-disc band read
  as a carve, a bed depth per cell (deepest on the line, one at the shore) for the three channel forms.
- `OrbitScatter` — which cell of an orbit is its representative, the answer §2's fan is built on.
- `BlueNoise` — even, non-touching scatter sites. Nothing in the shipped stage places by it any more, since
  everything is placed by hand; it stays for the auto-placement ideas of `ideas.md`, which will want it.

**`PgmStudio.Minecraft/Dressing` — the world-writing pass.** `Decorator`, sibling to `ObjectiveStamper` and
`TerrainPainter`: it takes a `DressingContext` (the surface, the placed props, the protected set, the
symmetry) and writes blocks via `SetBlock`. It reaches `Geom` for the algorithms and `DressingSymmetry` for
the orbit fan. The props themselves (`PathProp`, `WaterProp`, `FloraProp`, `TreeProp`, `BoulderProp` under one
`PlacedProp` discriminator) and the block palette live here beside `Blocks`/`BlockPalette`.

**`PgmStudio.Api/Services` — reading + wiring.** `DressingScope` answers the three things the pass needs from
a map: what was placed, how the map is mirrored, and what must be left bare. Unlike `TerrainThemeScope` there
is no scope to resolve — a prop is not a recipe applied to a footprint, so reading it is reading a list.
`SketchWorldBuilder.Build` then calls `Decorator.Decorate` immediately after `TerrainPainter.Paint`.
`DressingPreview` draws a prop by placing it — a sample patch painted with a theme and run through the real
`Decorator` — and draws every picker's cards the same way, so a picker can never offer a look the export does
not produce.

The side view is a **projection**, not a cut. `Minecraft.BlockSideView` looks through every row and keeps the
nearest block, shading it by how far back it stands; a single row through a crown meets it wherever that row
happens to fall — as often through the air between leaf clusters as through them — so a cut comes out
speckled and missing pieces that are plainly there. It is the model `Analysis.SideView` already draws a whole
map with for the build-height step, with one difference: that one projects vertical *segments*, which carry
no block identity, so its picture is a stone-grey ramp; this projects a real world and keeps the block, so
the colours stay the export's own and only the shading comes from depth. Depth is measured against the
projected box rather than against the depths the view happened to reach, so a block's shade says where that
block is and nothing else — stretching the scale to fit the content would let one block moving back re-shade
every other block in the picture.

Both views look **inside** the sample's outermost ring. The painter reads a footprint's perimeter as its edge
and finishes it as one, a rim course over a wall, so that ring is the sample's own boundary rather than
ground a prop could stand on; seen from the side it is the entire front face, and a tree that stands in grass
was drawn standing behind a wall. Within one picker every card is drawn on one patch, sized to the widest
option, and cropped to the same courses — the tallest option decides the top and the ground decides the
bottom — so the cards sit in a grid with their floors on one line and their heights honestly compared, and a
two-block rock is not four pixels in a sample cut for a tree.

**`PgmStudio.Pgm/Sketch` — storage only.** `SketchLayout.Dressing` is the stored blob and nothing here reads
into it. A path was briefly a `SketchShape` and is not one: it places no terrain, so putting it in the
rasterizer meant the geometry model carried a kind that never contributed a cell.

**`PgmStudio.Client` — the placing tools.** `dressing/dressing-doc.js` is the document (the same wire format,
so there is no second model of a prop), `controllers/dressing-controller.js` the five tools, and
`render/dressing-render.js` how a placed prop and its mirror images look on the canvas. The inspector is
Blazor and owns no state: it reads the bridge's pushed document and writes patches back.

**Symmetry (G162)** is not a place but a rule, and §2 is where it binds.
