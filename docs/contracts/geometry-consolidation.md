# Geometry consolidation (A4 — DONE)

> **Status: complete.** Both families consolidated, the editor hit-test decision made (keep AABB), parity
> unchanged. This doc is now the rationale record + the earmark for the one *future* resident (a
> `Rect ∪ Cylinder` C# shape model, only if intent ever goes beyond rectangles). The plan + full audit for
> collapsing the scattered 2-D geometry code, captured while the symmetry context was fresh (the N03 /
> orbit-unification work).

## The two families (do **not** force them into one type)

### (1) Affine symmetry transforms — reflect/rotate a point or AABB about a centre

Canonical: **`PgmStudio.Geom.Symmetry`** (`Order` · `Point` · `Rect` · `Apply` · `Normal` · `OrbitAxes` ·
`ReflectPoint` · `RotatePoint`) — a dependency-free leaf (`PgmStudio.Geom`, moved out of `Contracts`), so
client (WASM), server, and `Analysis` all reach it.

- **Status: COMPLETE (family 1).** Every C# affine site routes through `Geom.Symmetry`:
  `SymmetryAuthoring` (the dict-`bounds_2d` baker, via `Symmetry.Rect`), `SymmetryExpander`,
  `MonumentEndpoints`, `Analysis/SymmetryDetector`,
  `RegionParser`/`RegionBoundsDeriver` (`MirrorBounds`), `SketchRasterizer`, and client `OrbitAssignment`.
  The two `ModeNormals` dicts collapsed onto `Symmetry.Normal`; `SketchRasterizer.MirrorAxes` onto
  `Symmetry.OrbitAxes`. See **Deep audit › Revised order** below for the verified breakdown.

### (2) Polygon set-ops + point-in-polygon + IoU / centroid

The Analysis project uses **NetTopologySuite (NTS)** — 7 files: `RegionGeometry2d`, `SymmetryDetector`,
`IslandDetector`, `Buildability`, `ResourceSources`, `WoolSources`, `RegionAuthoringEncoder`.

- **Status: COMPLETE (family 2 helper).** `PgmStudio.Analysis.Region.Geometry2dOps` is the single
  NTS footprint helper: `CoversCell` (block-cell point-in-polygon, the `(x+0.5, z+0.5)` sampling
  convention — `Geometry` + prepared-geometry overloads) and `IoU`. The repeated
  `geom.Contains(new Point(x+0.5, z+0.5))` ray-casts (`Buildability`, `ResourceSources` ×2,
  `WoolSources`) and the inline IoU (`SymmetryDetector`) now route through it; parity unchanged
  (buildability/wool/traversability 10/10). `WoolSources`' `Centroid` (banker's-rounded representative
  point) is **not** folded in — it's a single site with its own rounding, not a duplicate.
- **Hand-rolled point-in-polygon outside NTS (intentional twins, not folded):** `Pgm/SketchRasterizer`
  and client `SpawnStep` use `Geom.Polygon.PointInRing` (scalar leaf — those projects have no NTS);
  JS `geometry/polygon.js` `pointInRing` is the documented preview twin.

### Bounds rebound (cross-cutting)

Scattered AABB re-bound after a transform: `Symmetry.Rect` (canonical), `RegionParser`, `RegionBuilder`,
`AuthoringEndpoint`, `WorldFeatureWriter`. Converge the **affine** ones on `Symmetry.Rect`.

## Shape model (`shape.js`) — and why `OrbitAssignment` is rectangle-only

A third axis, orthogonal to the two families above. `geometry/shape.js` (JS) is the **unified primitive
vocabulary** — rectangle / circle / polygon / lasso — with `toRing` (incl. Bézier sampling), `toBounds`,
`ringCentroid`, and a general **`containsPoint(shape, x, z)`** (rect = bounds, circle = radius, polygon =
ray-cast). Its C# parity twin is `Pgm/Editing/SketchRasterizer` (shape → ring → raster, with its own
point-in-poly). `symmetry.js` is **orthogonal** to it: it transforms a shape's *ring vertices* —
`boolean.js` mirrors a shape by `toRing` + `applySymmetry` per vertex. So: **shape.js = the shape model +
containment; symmetry.js = the transforms; they compose.**

**Did `OrbitAssignment` (C#) miss `shape.js` as an entry point?** Two reasons it doesn't use it — only one
is a real gap:
- **Runtime** — `OrbitAssignment` is C# (intent model, WASM); `shape.js` is JS (canvas). The C# side has
  *no* general shape model (only `SketchRasterizer`'s sketch-private one), so it can't call `containsPoint`.
- **Scope (the real reason)** — the **intent model is rectangle-only**: `BuildIntent.Areas`/`Holes`,
  `SpawnIntent.Protection`, `WoolIntent.Room` are all `Rect`. So `OrbitAssignment`'s point-in-rect
  (`Covers`) + `Symmetry.Rect` (4-corner mirror) is the correct, complete primitive *today*; a general
  `containsPoint` would be over-engineering. It's "different data layers" **and** a deliberate scope cut —
  not a missed reuse.

**The latent gap to track.** "All primitives are shapes" holds at the canvas/sketch layer (shape.js) and
for **islands** — which are polygons, ray-cast in C# by `SpawnStep.PointInRing` (dup'ing shape.js's
polygon contains across the runtime split). If intent zones ever go beyond rectangles (circle/polygon
protection or build areas), three things generalize together: the intent `Rect` → a shape type;
`OrbitAssignment`'s rect mirror → a ring mirror; and `BuildGenerator`/`WoolGenerator` (which emit
`rectangle` regions). That needs a **shared C# shape model** mirroring `shape.js`, folding in
`SketchRasterizer`'s and `SpawnStep`'s hand-rolled point-in-poly. Until then, **rectangles are the
contract** and the current code is right.

> Reality check: shape.js's header says it's "shared by the editor (regions) and the sketch tool," but in
> practice **only the sketch path imports it** — the editor canvas hit-tests regions by **AABB**
> (`#hitTest` over `node.bounds`, via `region-convert.js`), not `containsPoint`. So even within JS the
> shape/contains story isn't fully unified; a consolidation should decide whether the editor adopts
> `shape.js` containment or stays AABB.

## Boundaries that constrain "one module"

- **Pgm ↔ Analysis:** Pgm hand-rolls affine transforms; Analysis is NTS. Don't force an NTS dependency on
  Pgm it doesn't want — family (1) and family (2) can be separate shared pieces.
- **C# ↔ JS:** the JS `geometry/*.js` layer (symmetry, polygon, boolean, transform) is a **deliberate**
  parallel impl for live canvas previews ("hot path stays in JS"). It is *not* a consolidation target;
  keep it mirroring the canonical C# behaviour, not merged into it.

## Home for the shared module (open decision)

`Contracts` works for family (1) (dep-free; client + server reach it) but is conceptually the DTO leaf — a
**dedicated geometry leaf project** (`PgmStudio.Geometry`, referenced by Client/Pgm/Analysis) may be
cleaner and is what the original A4 note envisioned ("one module … mind the Pgm↔Analysis boundary").
Family (2) likely stays in Analysis on NTS regardless.

## Suggested order

1. Finish family (1): fold `SketchRasterizer`, `SymmetryExpander.TransformRect`, `MonumentEndpoints.ModeNormals`
   onto `Contracts.Symmetry` (+ a C# `OrbitAxes`).
2. Decide the home (keep `Contracts` vs new `PgmStudio.Geometry`); relocate if chosen.
3. Family (2): a shared NTS-backed contains/IoU; remove hand-rolled ray-casts on the server.
4. Leave the JS layer + client point-in-polygon as the preview-runtime twins (documented, not merged).

Pairs with **P7** (layer-extractor/scan consolidation).

## Deep audit — verified findings (geometry data-flow grilling)

A full read of the geometry data-flow across all four runtimes (sketch / configure / edit JS canvas, the
server write path, the Analysis NTS read path). Every claim here was confirmed against source.

### The one fact that governed the consolidation — RESOLVED
`PgmStudio.Analysis` referenced **only `Domain`**, so the NTS family could not reach `Contracts.Symmetry`,
which forced `Analysis/SymmetryDetector` to carry a byte-identical `ReflectPoint`/`RotatePoint` copy.
**Done:** a dependency-free **`PgmStudio.Geom`** leaf now holds the canonical scalar math
(`Symmetry` + `Polygon.PointInRing`); `Pgm`/`Analysis`/`Client` reference it directly, `Api` transitively.
`Contracts.Symmetry` is gone (moved to the leaf); `Pgm` no longer references `Contracts` at all. Collapsed
with the move: `SymmetryDetector`'s reflect/rotate + normal/degree tables (now `Geom.Symmetry.Apply`),
and the two C# `PointInRing` ray-casts (`SketchRasterizer` + `SpawnStep` → `Geom.Polygon.PointInRing`).
Named **`Geom`** not `Geometry` because `Analysis` uses NTS's `Geometry` type pervasively and a sibling
`PgmStudio.Geometry` namespace shadows it. The leaf is also the planned home for the shape model + the
generative layout algorithms (TSP/annealing, random-point polygon seeding).

### The true client/server border (healthy)
- **JS only reads.** Every `bridge/*.js` `fetch()` is a GET (regions/tree, islands, symmetry, layer
  pixels, segments). **No** POST/PUT/PATCH from JS — all writes go through Blazor C# `HttpClient`.
- **JS → C# carries raw events only** (`OnCanvasPointPick`, `OnRegionDraw`, `OnBoundsSave`, `OnSliceY`);
  C# validates/persists. **C# → JS pushes display previews** (`setAuthorRegions/Mirror/Symmetry`, tints).
- The client **never** receives an NTS object: islands = GeoJSON rings, regions = untyped `polygon_2d`
  dicts + AABBs (`RegionAuthoringEncoder`), analysis = scalars (`BuildabilityDto` = digit-grid + bounds).
- **Soft spot:** the Configure wizard holds **client-computed orbit geometry** in `Wizard.Intent` until
  PUT. `SpawnStep`/`ProtectionStep` orbit-fill in WASM C# via `Geom.Symmetry` + `OrbitAssignment`;
  the server's `SymmetryExpander.FillSpawns` then **no-ops** (teams already filled). So spawns/protection
  are *client*-authored, build areas are *server*-authored (client sends one side). Two orbit paths that
  agree **only because both call `Geom.Symmetry`** — keep that the single source.

### Shapes (`shape.js`) — two models, not one
- JS `shape.js` is the rich vocabulary (`rectangle|circle|polygon|lasso`, Bézier `toRing`, `containsPoint`).
  C# has **no general shape model** — the intent is **rectangle/point only** and `SketchRasterizer`'s shape
  model is **sketch-private**. Rectangles are the contract; `OrbitAssignment` rect-only is correct, not a gap.
- ~~`shape.js`'s header ("shared by editor + sketch") is **drift**~~ **RESOLVED:** header corrected to
  sketch-only. The **editor hit-tests regions by AABB** (`editor-canvas.js #hitTest` over `node.bounds`),
  islands by `pointInRing` — and that AABB choice is the **decided** behaviour (coheres with the AABB
  resize/move model; see the Remaining/`DECIDED` note above).
- ~~`containsPoint` ignores Bézier bulge → rendered curve ≠ hit shape.~~ **FIXED:** `containsPoint`
  now ray-casts over `toRing(shape)` (the same closed ring that is rendered), so the hit shape matches
  the drawn outline incl. the curve bulge (`shape.test.js` bulge case).

### Shape-model bound when intent goes beyond rectangles (future, noted)
Intent zones (spawn protection, wool rooms) are rectangle-only today and that is the contract. When they
generalise, the target is **not** an arbitrary shape model — authored zones must resolve into **PGM
regions**, so the only expressible primitives are **unions of rectangles and cylinders**. **Cylinder is
currently missing** from the intent model and the generators (and from `region-convert.js`'s
`sketchShapeToPgmRegion`, which returns null for polygon/lasso). So the eventual shape model is bounded:
`Rect ∪ Cylinder` unions, not the full `shape.js` polygon/lasso vocabulary (those stay sketch-terrain only).
**`OrbitAssignment` is a keystone** (point-aware orbit→team assignment via covered-anchor containment) —
any consolidation/relocation must keep its behaviour and `Geom.Symmetry`-backed API intact; when
cylinders land, its rect `Covers` test generalises to a cylinder containment, not a rewrite.

### Ranked findings
**Correctness / latent:**
1. ~~**`SketchRasterizer.MirrorPoint` silently drops `mirror_d1`/`mirror_d2`**~~ **FIXED:** `MirrorPoint`
   now delegates to `Geom.Symmetry.Point` (rot_270 = k=3 of rot_90), so every axis — incl. the
   diagonals — is covered and consistent with the generator + JS, and the hand-rolled switch is gone
   (one duplicate removed). Regression: `SketchRasterizerTests.Mirror_d1_adds_a_diagonally_reflected_copy`.
2. ~~**`Traversability.RegionCentre`** uses the AABB midpoint, not the polygon centroid → nav-point
   can land in void for half/compound spawn footprints.~~ **FIXED:** it resolves the region to its NTS
   footprint (`RegionGeometry2d.ToGeometry`) and uses the **area centroid when that centroid is inside
   the shape**, falling back to a guaranteed-interior `InteriorPoint` for non-convex/disjoint footprints,
   and to the AABB midpoint when no geometry resolves. (A plain `Centroid` is **not** enough — a symmetric
   disjoint union's centroid is the gap between the parts; e.g. annealing_iv's `woolrooms` centroid is the
   map centre `(0,0)`, ~95 blocks from any room.) Centroid-if-inside keeps the convex rect/disc nav-points
   on the bounds midpoint, so `--traversability` parity stays **10/10**. Regression:
   `TraversabilityTests.Spawn_navpoint_lands_inside_a_disjoint_union_not_the_bounds_gap`.
3. ~~**Rounding split** (`OrbitAssignment` int vs `SymmetryExpander.TransformRect` 1 dp)~~ **RESOLVED — by
   coordinate kind, grounded in PGM + the corpus.** Two situations: (a) **sketch shape mirroring**
   (`SketchRasterizer`) rasterizes to block cells, so leniency is fine; (b) **bounded-region mirroring**
   (Configure orbit-fill + canvas) reproduces PGM `<mirror>`, whose `transform` is an *exact* reflection
   (`MirroredRegion`; algebraically identical to our `Symmetry.ReflectPoint`). The grids, from the 350-map
   corpus: **rectangle bounds are integer** (all 4123 corpus rects on the 1×1 grid) and **points are block-
   centre `.5` or block-anchor `.0`** (1574 vs 513). So: `SymmetryExpander.TransformRect` **snaps corners to
   the integer block grid** (`Math.Round` — a drawn 20×50 box stays exactly 20×50), and `TransformPt`
   reflects **exactly** (no snap — `.5` *and* `.0` both occur, so forcing either grid would corrupt the
   other; the old `Math.Round(v,1)` happened to preserve both but was opaque). Yaw keeps its rounding
   (`atan2` float noise). The parser path (`RegionParser`/`RegionBoundsDeriver` `MirrorBounds`) was already
   exact. `OrbitAssignment` keeps integer rounding — it only orbits block-aligned protection **rects**.
   Regression: `SymmetryExpanderTests.Region_orbit_keeps_block_centre_points_and_integer_grid_rects`.

**Duplication:**
4. ~~`PointInRing` ×3~~ **DONE:** the two C# copies (`SpawnStep`, `SketchRasterizer`) now call
   `Geom.Polygon.PointInRing`; `polygon.js` stays as the documented JS twin.
5. ~~`ReflectPoint`/`RotatePoint` in `SymmetryDetector`~~ **DONE:** routed through `Geom.Symmetry.Apply`.
   (`RegionGeometry2d.Reflect` uses an NTS `AffineTransformation` matrix on a whole `Geometry`, not the
   scalar form — left as-is; it's family-2 NTS, not a scalar-math dup.)
6. `MirrorBounds`/`UnionBounds`/`TranslateBounds` duplicated verbatim between `RegionParser` and
   `RegionBoundsDeriver`. (The dict-`bounds_2d` rebound is `SymmetryAuthoring.OrbitBounds2d`, a thin
   (un)pack over `Symmetry.Rect`.)
7. ~~**No canonical map-bbox**~~ **RESOLVED — the real bbox is the surface-layer extent.**
   `RegionGeometry2d.ToGeometry`'s `bounds` (the finite box `half`/`negative` clip against) was each pass's
   own region-AABB + magic margin (`Buildability.RegionBbox` pad 16, `WoolSources.MapBbox`/`ResourceSources`
   pad 8) → a `half` region clipped differently per pass. **Now:** the canonical box is the **surface layer**
   extent — `WorldFeatureWriter` computes min/max over the scanned surface cells **once at scan** and stores
   it in `map_config.json` `bounding_box` (the `layer.parquet` surface, not the cleaned-base islands).
   `MapBounds.ResolveAsync` reads it back (islands-AABB fallback for pre-existing/xml-only maps);
   `FeatureData.MapBboxAsync` exposes it to analysis. **Wired into every clipper:** the canvas frame
   (region-tree/authoring `bounding_box` — verified it now equals the top-surface render extent, fixing
   surface-beyond-islands clipping), **`Buildability`**, **`WoolSources`** (`CheckAvailability`,
   `SuggestWools`, `RenewableGeoms`, `PgmSpawnerSources`), and **`ResourceSources.RenewableRegions`** — all
   via an optional `mapBbox` param whose `MapBbox`/`RegionBbox` is the no-scan fallback. The 16-vs-8
   per-pass margins are gone; every pass clips `half`/`negative` against the one surface box.

### Revised order
**Done:** `SketchRasterizer` diagonal-mirror gap (#1) · `containsPoint` hit-vs-render (Bézier) · the
**home decision** — the `PgmStudio.Geom` leaf holds the canonical `Symmetry` + `Polygon`, with
`SymmetryDetector` de-forced and the C# `PointInRing` copies collapsed (#4, #5). **The affine fold-in is now
complete:** `SymmetryExpander` (its `Step` is gone — `TransformPt`/`Rect`/`Yaw` call `Symmetry.Point`/`Rect`
directly), both `ModeNormals` dicts (`SymmetryAuthoring` + `MonumentEndpoints` → `Symmetry.Normal`, the
single source the `Apply`/`Point` mirror branches now also use), and the `RegionParser` /
`RegionBoundsDeriver` `MirrorBounds` reflects (→ `Symmetry.ReflectPoint`). Added `Symmetry.OrbitAxes`
(mirror of JS `orbitAxes`; `SketchRasterizer.MirrorAxes` removed). Every C# affine transform now routes
through `Geom.Symmetry`.

All consolidation work is resolved — map-bbox #7, orbit rounding #3, `RegionCentre` #2, the family-2
`Geometry2dOps` helper, and the editor hit-test decision; `shape.js`'s header is corrected to sketch-only.

1. **DECIDED — editor region hit-test stays AABB.** `editor-canvas.js #hitTest` selects by `node.bounds`
   (AABB; smallest-area tiebreak + `MARGIN=2` near-miss). Kept over polygon-precise (`pointInRing` over
   `node.polygon_2d`) because it **coheres with the AABB resize/move model** (the 8 handles operate on
   `node.bounds`, so select = what you can manipulate) and keeps "forgiving" select; the polygon route would
   make select *stricter* than manipulation and pull in hole/multipolygon handling
   (`polygon_2d.polygons[].holes`, which `#hitTestIsland` ignores), a polygon-area tiebreak, and near-miss
   re-tuning — complexity for a marginal gain. NB: a polygon route would reuse `pointInRing` over
   `polygon_2d`, **not** `shape.js`'s `containsPoint` (the sketch *shape* model) — so `shape.js` is
   sketch-only either way (header corrected).
2. Leave the JS `geometry/*` layer as the documented preview twin (not merged) — unchanged by design.

**Future (earmarked, not part of A4):** a C# `Rect ∪ Cylinder` shape model in the `Geom` leaf when intent
goes beyond rectangles (mirrors `shape.js`; needs no boolean ops — see "Home for the shared module"); the
generative layout algorithms (TSP/annealing, random-point polygon seeding). Build only when a consumer
needs it.

> Note: the dict-`bounds_2d` rebound (`SymmetryAuthoring.OrbitBounds2d`) delegates the whole transform to
> `Symmetry.Rect`; only the dict (un)pack stays in `Pgm` (the leaf is dict-free).
> The `UnionBounds`/`TranslateBounds` copies shared between `RegionParser`/`RegionBoundsDeriver` are
> non-affine (min/max merge, offset) — a separate Pgm-internal dedup, not a leaf fold.

> Constraint through all of the above: **keep `OrbitAssignment` intact** (the point-aware orbit→team
> keystone); and any new shape support stays within **`Rect ∪ Cylinder`** (PGM-expressible) — see the
> shape-model bound note above.
