# Decoration — flora, paths, scatter, canopy (G34, slice G161)

A third pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
terrain painting of `terrain-painting.md`. Where the painter dresses the terrain's *material* — grass over
stone, quartz on the rim — this pass adds the terrain's *life*: tall grass and flowers scattered on the
soil, a worn path dragged across it, boulders and trees seated on top. It is the second half of the
theming work parked as G34: G157 carved out the terrain slice ("no new geometry, only materials"); this is
the **prop-stamps** slice, and it is the opposite by construction — it exists to add geometry.

**Status: designed, not built.** Nothing here ships yet; the pass, the stamper and the raster branch below
are the G161 proposal, and every type named in the future tense is unbuilt until its task lands. The model
was worked out against a live prototype — `tools/decorate/prototype.html`, one self-contained page whose
every figure (the noise fields, the path variants, the boulder elevations, the grown tree, the forest
scatter) is emitted by the real algorithm the stage would run, not hand-drawn. Read it alongside this doc
the way `showcase.cs` reads alongside `model.md`: when prose and prototype disagree, suspect the prose.
Rule ids here are `DR*` (dressing), local to this file the way `structures.md` owns `WX*` and
`terrain-painting.md` owns `TP*` — `rules.md` is compose-scoped and frozen, so decoration law lives in the
world-export docs, not there.

A note on the name. In this codebase **"decorative"** already means two settled things — the non-playable
floating masses the island detector prunes (dragons, birds), and the non-objective wool a map places for
colour. To keep those apart from *this* stage the code family is **dressing** (`DR*`, a `Dressing` pass, a
`DecorationStamper` is the one concession to the human word); the doc keeps "decoration" only as the title,
where the collision cannot bite.

Read alongside:

- `docs/world-export/terrain-painting.md` — the pass this one runs immediately after; the surface it reads.
- `docs/world-export/structures.md` §6.4 — the preset seam (style-as-data). A dressing style attaches here.
- `docs/contracts/sketch-authoring.md` — the sketch model a path shape (§3) extends.
- `docs/generator/ideas.md` — G34 (the umbrella), G32-C (structures & elevation, the sibling pass), G142
  (the roughen pass, whose noise operators the path edge borrows).

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

The four concepts below read as four features but run on four primitives, which is the argument for one
stage rather than four:

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
- **Stamp.** A 3-D volume seated on `SurfaceTop` via `PositionSnap.SurfaceYOver` (which samples the whole
  footprint, so a prop sits level on uneven ground and survives the symmetry fan) and written cell-by-cell
  with `SetBlock` — the shape-mask-in-a-box `ObjectiveStamper` already uses for destroyables and cores.

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

A `DR-FL` pass would iterate `SurfaceTop`, and for each cell whose top block is soil and whose density
field clears the threshold, `SetBlock` a plant into the air cell at `SurfaceTop`. It adds one block per
cell and never touches the ground — the lightest of the four passes, and the one that reuses the most
(`PatternNoise` verbatim, the surface grid verbatim, only the placement loop new).

## 4. Paths — drag a line, replace the finish (`DR-PA`)

The sketch lasso already captures a freeform drag as a polyline (`#startLasso`/`#addLassoPoint` in
`sketch-draw-controller.js`) — it just closes it into a filled ring on release. A path is the **open**
version: keep the centerline, buffer it. Every cell within a radius of the nearest centerline segment
becomes path material, replacing the surface finish beneath it. That radius test — `dist ≤ R` to the
nearest segment, a swept-disc (capsule) union — is the "Voronoi radius" a WorldEdit user would draw by
hand, and the disc-scan primitive it needs already exists in `Skeleton.Anchored`. The centerline reuses
`SketchShape.Vertices`; the half-width reuses the `Radius` field the circle shape already carries.

The single solid band is the boring case; the imperfect paths are the point, and each is the same distance
field with one extra gate:

- **Solid** — `dist ≤ R`, a clean utility road with a coarse-dirt edge ring.
- **Worn** — and a per-cell `Unit` dice below a coverage threshold: gravel scattered thin, a trail rather
  than a road.
- **Rough edge** — `R` perturbed by an `Fbm` sample, so the band's outline is organic, not ruled. This is
  the same operator family as the G142 roughen pass's edge displacement.
- **Voronoi cobble** — the band tiled by a jittered grid of sites, each cell taking its site's shade: the
  cobbled read of `VoronoiMaterial`, applied to a footprint instead of a colour.
- **Stepping stones** — discs sampled at intervals along the centerline's **arc length**, with gaps
  between: the disconnected path, stones across a void.
- **Tapered** — `R` varied along the arc length (fat middle, thin ends, or a one-way narrowing).

The cheapest place for this is a new `"path"` branch in `SketchRasterizer.RingOf`/`RasterShape` (expand the
centerline+radius into a closed band ring, or fill by the capsule test directly) with its twin in
`geometry/shape.js` `toRing`, kept in lock-step by a parity constant the way `CirclePoints`/`BezierSamples`
are. Emit a band ring and every downstream system — island detection, mirror/orbit, per-anchor height,
world export — works with zero new code, the `sketch-tool-improvements.md` §8a reuse principle. A path is
then a paint operation on a stroke: it changes the surface finish, so it can run in the sketch layer at
rasterize time or as a `DR-PA` recolour in the dressing pass; running it in the pass lets its cells become
the flora exclusion mask of §3 for free.

## 5. Boulders — half-buried, scattered (`DR-SC`)

A boulder is the first decoration that is genuinely 3-D, and it is the same shape-mask-in-a-box the
objective stampers already build. Seat a `BlockBox` on `SurfaceTop` (via `SurfaceYOver`), then fill the
cells that pass an ellipsoid test — `((x−cx)/rx)² + (y/ry)² ≤ 1`, the squared-distance mask `StampCore`'s
`Inset` and `StampDestroyable`'s `InPlusSection` are the precedent for. Half the ellipsoid sits below the
surface, so the rock reads as emerging from the ground rather than dropped on it. Perturb the radius with a
noise sample for an angular, weathered read; stack two or three lobes for a cairn; flatten `ry` for a wide
outcrop. The finish is a material and a micro-mask: stone, andesite, mossy cobble, blackstone — and moss
creeping onto the top-lit faces, itself a tiny `Unit` mask, so the finish carries its own micro-flora.

Placement is the shared scatter: blue-noise sites for even spacing, a size field to vary them, and the
plan's protected regions — spawns, objectives, the paths of §4 — as exclusion masks so no rock lands where
it blocks play. A `DR-SC` pass is a new `DecorationStamper` mirroring `ObjectiveStamper`: a
`DecorationStyle` enum, a `Dimensions(style)` table (style-as-data, the `structures.md` §6.4 seam), a
`DecorationBox(surfaceTop, anchorX, anchorZ, style)`, and a `Stamp` that loops the box under the mask
calling `SetBlock`. The volume primitive is `BlockBox`; the write primitive is `SetBlock`;
`AnvilRegionWriter.Write` serializes the result — all already in hand.

## 6. Trees — copied and grown (`DR-TR`)

The trivial tree copies vanilla: a trunk of a known height and a canopy of a known profile, parameterised
per species — oak's blob, birch's tall slim crown, spruce's layered cone, acacia's leaning flat top,
jungle's broad canopy, dark oak's two-wide trunk. Each is a parametric template (trunk height, canopy
radius, canopy profile), not a copied schematic, and an oak grove is genuinely all most maps need. The
interesting tree is **grown** — a recursive brancher: from the trunk, split into limbs at an angle,
shorten by a taper and repeat to a depth; leaves blob at the terminal tips. Four knobs (branch angle,
depth, taper, leaf density) span a design space no vanilla generator makes, and the jitter is hash-keyed
per branch so a seed always grows the same tree.

Both are the same stamper as the boulder: a tree is a trunk column plus a leaf mask over a box. Placement
is the same scatter, with one addition — a low-frequency density field gathers trees into groves with
clearings between, so a forest reads as clumps rather than an even orchard. A `DR-TR` pass is a
`DecorationStyle` variant of the boulder's stamper: the species templates as data rows, the grower as a
generator with its knobs, the grove clumping as the density-gated scatter of §5.

## 7. What it reuses, and what it adds

The stage leans hard on machinery G157 and the sketch tools already shipped; the net-new surface is small
and lands in the same realize seam.

| Concept | Reuses | Net-new | Rule family |
|---|---|---|---|
| Flora overlay | `PatternNoise`; `SurfaceTop`; the `TerrainProfile` column read | the overlay pass — one `SetBlock` above the surface | `DR-FL` |
| Paths | the lasso capture; `SketchShape.Vertices`/`Radius`; the `Skeleton` disc-scan | a `"path"` branch in `RingOf`/`RasterShape` + its `toRing` twin; the gates | `DR-PA` |
| Boulders | `ObjectiveStamper`'s mask-in-a-box; `BlockBox`; `SurfaceYOver` | a `DecorationStamper` + `DecorationStyle`/`Dimensions`; the ellipsoid/cairn masks | `DR-SC` |
| Trees | the boulder stamper and scatter | the species templates; the recursive grower | `DR-TR` |

Two neighbours bound the stage. G32-C (structures & elevation, the "second generator") is the sibling pass
that gives a flat layout its heights; a boulder or tree seats on whatever surface that pass leaves, so the
two compose but do not depend on each other. G142 (the roughen pass) shares this stage's architecture —
last in realize, over the authored unit, symmetry re-fanned — and its edge-displacement operator is the
path's rough edge; if both land, they share the noise operators rather than duplicating them.

The build order follows the reuse gradient: flora first (it adds the least and reuses the most), then the
path raster branch, then the `DecorationStamper` for boulders, then trees as a style on top of it. Each is
a `DR*` slice of G161, carved off G34 the way G157 was — its own rules in this file, its own pass wired
last into `SketchWorldBuilder.Build`, after the painter.
