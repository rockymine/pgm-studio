# Decoration — flora, paths, scatter, canopy (G34, slice G161)

A third pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
terrain painting of `terrain-painting.md`. Where the painter dresses the terrain's *material* — grass over
stone, quartz on the rim — this pass adds the terrain's *life*: tall grass and flowers scattered on the
soil, a worn path dragged across it, boulders and trees seated on top. It is the second half of the
theming work parked as G34: G157 carved out the terrain slice ("no new geometry, only materials"); this is
the **prop-stamps** slice, and it is the opposite by construction — it exists to add geometry.

**Four of the five tools ship (G161): flora, paths, boulders and trees.** Water (§7) is the exception and is
still a proposal — its carved bed is an elevation change, which is the one thing the rest of the stage does
not do; it is filed as `G169`. The model was worked out against a live prototype —
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
- `docs/contracts/sketch-authoring.md` §2 — the sketch model a path shape (§4) extends.
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
  plan's protected regions (spawns, objectives, and the paths of §4) as exclusion zones. Nothing lands
  where it would break play or read wrong.
- **Scatter.** Even, non-touching placement for rocks and trees — blue-noise sites taken as local maxima
  of a hash field over a disc, the same `dx²+dz² ≤ r²` scan `Skeleton.Anchored` already runs. Gate the
  sites by a density field and the even scatter becomes clumped groves.
- **Stamp.** A 3-D volume seated on `SurfaceTop` and written cell-by-cell with `SetBlock` — the
  shape-mask-in-a-box `ObjectiveStamper` already uses for destroyables and cores. A prop seats on the
  *lowest* column of its own footprint, so it sits into a slope rather than floating over the low side, and
  it refuses ground that is missing or protected rather than half-placing itself.
- **The fan (G162).** Every prop declares a `PropClass`. **Cosmetic** props — one-block plants — scatter
  freely: a flower one team has and the other does not changes nothing. **Gameplay** props — anything that
  blocks a step or breaks a sight line — are generated **once**, on the orbit's canonical representative
  (`OrbitScatter.Canonical`), in the prop's own local frame, and then stamped at every image of that orbit
  with each offset **turned** by that image's transform.

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

`DR-FL` iterates `SurfaceTop`, and for each cell whose top block is soil and whose density field clears the
threshold, sets a plant into the air cell at `SurfaceTop`. It adds one block per cell and never touches the
ground — the lightest of the four passes, and the one that reuses the most (`PatternNoise` verbatim, the
surface grid verbatim, only the placement loop new). `FloraSpec` carries the knobs: `Coverage` against the
density field, `Scale`/`Octaves` shaping it, and `FernShare`/`FlowerShare`/`FlowerScale`/`TallShare` mixing
the species. `DressingPalette.SoilShare` is the eligibility read — sand takes a fraction of what grass does,
quartz none.

The split that matters here is not a species one. Grass, fern and flowers are one block tall and walked
straight through, so they are **cosmetic** and scatter freely; the two-block `TallGrass` and `LargeFern`
break a sight line and are marked **gameplay**, which routes them through the fan of §2. A plant's
`PropClass` is declared on the palette row, not inferred at placement.

## 4. Paths — drag a line, replace the finish (`DR-PA`)

The sketch lasso already captures a freeform drag as a polyline (`#startLasso`/`#addLassoPoint` in
`sketch-draw-controller.js`) — it just closes it into a filled ring on release. A path is the **open**
version: keep the centerline, buffer it. Every cell within a radius of the nearest centerline segment
becomes path material, replacing the surface finish beneath it. That radius test — `dist ≤ R` to the
nearest segment, a swept-disc (capsule) union — is the "Voronoi radius" a WorldEdit user would draw by
hand, and the disc-scan primitive it needs already exists in `Skeleton.Anchored`. The centerline reuses
`SketchShape.Vertices`; the half-width reuses the `Radius` field the circle shape already carries.

A path is a `SketchShape` of type `"path"`: `Vertices` is the open route the author clicked and `Radius` its
half-width, and `Geom.PathBand` derives the closed band from those two — smoothing the drawn points into a
dense centripetal Catmull-Rom curve (which cannot cusp on a tight bend), then offsetting that curve to both
sides with `Ribbon`. The order matters: offsetting the drawn points would corner the band at every click.
`SketchRasterizer.RingOf` gets one `"path"` branch and `geometry/shape.js` `toRing` its twin, kept in
lock-step by a parity constant the way `CirclePoints`/`BezierSamples` are. Because the result is a band
*ring*, every downstream system — island detection, mirror/orbit, per-anchor height, world export — works
with zero new code, the `sketch-tool-improvements.md` §8a reuse principle. Its ends are cut square: a path
in a map arrives somewhere, and a flat end meets a plaza or a bridge mouth cleanly where a round cap bulges
past it.

The single solid band is the boring case; the imperfect paths are the point. Three of them are a width that
varies along the stroke, so they are the same ring with a different `Widths` function and nothing else:

- **Solid** — one width the whole way, a clean utility road.
- **Rough edge** — the width wandered by a `PatternNoise` sample, so the outline is organic, not ruled. The
  two sides read the field far apart, or the band would merely breathe in and out symmetrically instead of
  eroding. A seed on the shape fixes the wander, so a map exports identically. Same operator family as the
  G142 roughen pass's edge displacement.
- **Tapered** — the width varied along the arc: fat in the middle, thin at the ends.

Two more variants are **not** a ring at all, and are filed rather than faked: **worn** (a per-cell dice
below a coverage threshold — gravel scattered thin) and **stepping stones** (discs at intervals along the
arc, with gaps between) both gate *cells*, not the outline, so they need a footprint the shape model cannot
carry as one closed ring. They are `S37`. The **voronoi cobble** read is not a path variant at all: a path
is a shape, so it takes a terrain theme, and `VoronoiMaterial` on that theme is the cobbling.

What a path grows is decided by the same shape scope planting always uses, with one rule of its own: a path
claims itself as **bare** even when no dressing is assigned to it, because a meadow growing over the road
drawn through it is the one thing an author who drew a road did not ask for. Dressing it explicitly still
wins — a verge of long grass down a track is a real intent.

## 5. Boulders — half-buried, scattered (`DR-SC`)

A boulder is the first decoration that is genuinely 3-D, and it is the same shape-mask-in-a-box the
objective stampers already build. Seat a `BlockBox` on `SurfaceTop` (via `SurfaceYOver`), then fill the
cells that pass an ellipsoid test — `((x−cx)/rx)² + (y/ry)² ≤ 1`, the squared-distance mask `StampCore`'s
`Inset` and `StampDestroyable`'s `InPlusSection` are the precedent for. Half the ellipsoid sits below the
surface, so the rock reads as emerging from the ground rather than dropped on it. Perturb the radius with a
noise sample for an angular, weathered read; stack two or three lobes for a cairn; flatten `ry` for a wide
outcrop. The finish is a material and a micro-mask: stone, andesite, mossy cobble, blackstone — and moss
creeping onto the top-lit faces, itself a tiny `Unit` mask, so the finish carries its own micro-flora.

Placement is the shared scatter: `BlueNoise` sites for even spacing, a size field to vary them, and the
protected set of §2 as an exclusion mask so no rock lands where it blocks play. `BoulderSpec` carries
`Density`, `SizeSpread`, a `BoulderForm` (round, angular, outcrop, cairn), the finish block and a moss flag;
`BoulderShapes.Of(form, size)` answers with the lobes, and `Geom.Blob` fills them — a quadric eroded by a
noise field sampled in the lobe's own frame, which is what makes an angular rock angular rather than a
dented sphere. Spacing is the one place a spec's own size overrides its density: a boulder field of large
rocks at a low spacing merges into one mass, so `BoulderSpec.Spacing` takes the larger of the density
spacing and the prop's own diameter. A boulder is a solid volume standing on the ground, so it is
**gameplay**, not cosmetic.

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

Both trees are the same stamper as the boulder: a trunk-and-limbs volume plus a leaf mask over a box.
`TreeSkeleton.Grow` answers the limbs and tips from a `TreeShape`, `SweptVolume` fills each limb as a
capsule along its spline, and `TreeCrown` places the clusters and answers which one owns a cell. Placement
is the same scatter, with one addition — a low-frequency density field gathers trees into groves with
clearings between, so a forest reads as clumps rather than an even orchard (`TreeSpec.GroveScale`/
`GroveThreshold`). Species are data rows (`TreeSpecies`: log and leaf blocks plus the shape knobs), so a
stand of mixed oak and birch is a list, not a branch. A tree is the largest thing the stage places, so it is
**gameplay**.

## 7. Water — ponds and channels (`DR-WA`, unbuilt: `G169`)

Nothing in this section exists yet. It is here because water is the fifth tool the stage is aimed at and its
shape is already decided; it waits on the elevation pass, since it is the one decoration that changes the
ground rather than standing on it.

A channel begins exactly where the §4 path does — a dragged centerline and a radius, the same swept-disc
band. What makes water its own tool is that it **cannot drape on the surface** the way gravel can: laid on
a slope it reads as blue paint. Water has to sit in a **carved bed** and fill to a **level plane**. So a
channel lowers `SurfaceTop` in its footprint to a bed (a shallow U-profile — deepest at the centerline,
rising to the banks), and fills water from the bed up to one water height across the whole run; a pond is
the closed version — an organic **basin**, the §5 boulder blob read concave, its outline wandered by the
same FBM the rough path uses. This carve-and-level step is the one piece of decoration that changes
elevation, so it belongs with the **G32-C** elevation pass as much as with this stage: the bed is negative
terrain, and the water surface is a fixed-Y plane, not a draped follow of the ground.

The rest is the read that makes water look like water, and the details are where a channel stops looking
stamped. **Depth** shades the fill — shallow and light at the edge, dark toward the middle — but the falloff
is a smooth low-frequency field warped **off-centre**, so one bank runs deeper than the other and the bed is
never a clean symmetric bowl. The **shoreline** is its own **irregular pass**, not a fixed-width ring: its
width wanders with a noise field and drops to **zero** in places, so the water meets the land directly with
no border in some stretches and spreads into a wide flat in others. The shore and the bed read as a
**pattern**, not one flat block — jittered-voronoi patches of sand, pale gravel and coarse dirt (the terrain
`VoronoiMaterial` idea reused) — and the bottom pattern shows through the shallows. **Edge life** reuses the
§3 flora overlay masked to the bank and surface: reeds scattered on the shore, lily pads on the still, deep
water (blue-noise, sparse). Channels take a **form** — a clean-banked canal, a natural FBM-wandered edge, or
a stream that narrows and shallows into riffles — and ponds and channels compose into one watershed on a
single water level: basins joined by a watercourse, banked and planted throughout. Placement of standalone
ponds is the §5 scatter aimed at low ground, with the plan's protected regions as exclusions.

## 8. What it reuses, and what it adds

The stage leans hard on machinery G157 and the sketch tools already shipped; the net-new surface is small
and lands in the same realize seam.

| Concept | Reuses | Net-new | Rule family |
|---|---|---|---|
| Flora overlay | `PatternNoise`; `SurfaceTop`; the `TerrainProfile` column read | the overlay pass — one `SetBlock` above the surface | `DR-FL` |
| Paths | `CatmullRom`; `Ribbon`; `SketchShape.Vertices`/`Radius`; the ring every consumer already reads | `PathBand` + its `geometry/path.js` twin; a `"path"` branch in `RingOf`/`toRing` | `DR-PA` |
| Boulders | `SurfaceTop`; the squared-distance masks the objective stampers fill by | `Blob`; `BoulderShapes`; the scatter's size-aware spacing | `DR-SC` |
| Trees | the boulder's seating and scatter; `CatmullRom` for the limb splines | `TreeSkeleton`; `TreeCrown`; `SweptVolume`; the species rows | `DR-TR` |
| Water | the §4 path stroke (channels); the §5 boulder blob + FBM edge (ponds); the §3 flora overlay (reeds) | the carve-and-level bed (with G32-C); depth shading; the shoreline band | `DR-WA` |

Two neighbours bound the stage. G32-C (structures & elevation, the "second generator") is the sibling pass
that gives a flat layout its heights; a boulder or tree seats on whatever surface that pass leaves, so the
two compose but do not depend on each other — except water, whose carved bed **is** an elevation change and
so leans on G32-C directly. G142 (the roughen pass) shares this stage's architecture —
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
- `BlueNoise` — even, non-touching scatter sites as strict local maxima of a hash field over a disc, and the
  canonical-representative form the fan of §2 asks for.
- `Blob` — the eroded quadric a boulder lobe is; `SweptVolume` — the capsule a tree limb is; `Polyline` —
  the distance field a stroke of any width is filled by.
- `TreeSkeleton` and `TreeCrown` — the grower and the crown placement, as abstract limb centerlines, radii
  and leaf-cluster centres. No block ever appears in either.
- `PathBand` — the band a drawn line stands for, and the one C# side of the `geometry/path.js` parity pair.
- `OrbitScatter` — which cell of an orbit is its representative, the answer §2's fan is built on.

**`PgmStudio.Minecraft/Dressing` — the world-writing pass.** `Decorator`, sibling to `ObjectiveStamper` and
`TerrainPainter`: it takes a `DressingContext` (the surface, the per-cell recipe, the protected set, the
symmetry) and writes blocks via `SetBlock`. It reaches `Geom` for the algorithms and `DressingSymmetry` for
the orbit fan. The specs (`FloraSpec`, `BoulderSpec`, `TreeSpec`, and the `DressingRecipe` that holds them)
and the block palette live here beside `Blocks`/`BlockPalette`.

**`PgmStudio.Api/Services` — resolution + wiring.** `DressingScope` mirrors `TerrainThemeScope`: which
recipe governs a cell, how the map is mirrored, and what must be left bare, all resolved from the sketch
layout and the `MapIntent`. `SketchWorldBuilder.Build` then calls `Decorator.Decorate` immediately after
`TerrainPainter.Paint`. `DressingPreview` draws a recipe by growing it — a sample patch painted with a
theme and run through the real `Decorator`, so a knob that does nothing in the export does nothing in the
picture either.

**`PgmStudio.Pgm/Sketch` — the drawn tools only.** A `DR-PA` path is a `SketchShape` of type `"path"`,
ringed by `PathBand` (Pgm already references Geom). Auto-placed dressing does not pass through here; a
shape's `Dressing` annotation, which scopes it, does.

**Symmetry (G162)** is not a place but a rule, and §2 is where it binds.
