# Geometry consolidation (TODO A4)

> The plan + full audit for collapsing the scattered 2-D geometry code. Captured while the symmetry
> context was fresh (the N03 / orbit-unification work). The actual refactor is a **clean-session** job —
> it's cross-cutting and has design calls; don't bolt it onto feature work.

## The two families (do **not** force them into one type)

### (1) Affine symmetry transforms — reflect/rotate a point or AABB about a centre

Canonical: **`PgmStudio.Geom.Symmetry`** (`Order` · `Point` · `Rect` · `Apply` · `Normal` · `OrbitAxes` ·
`ReflectPoint` · `RotatePoint`) — a dependency-free leaf (`PgmStudio.Geom`, moved out of `Contracts`), so
client (WASM), server, and `Analysis` all reach it.

- **Status: COMPLETE (family 1).** Every C# affine site routes through `Geom.Symmetry`: `Pgm/Geometry2d`,
  `SymmetryAuthoring`, `SymmetryExpander` (its `Step` removed), `MonumentEndpoints`, `Analysis/SymmetryDetector`,
  `RegionParser`/`RegionBoundsDeriver` (`MirrorBounds`), `SketchRasterizer`, and client `OrbitAssignment`.
  The two `ModeNormals` dicts collapsed onto `Symmetry.Normal`; `SketchRasterizer.MirrorAxes` onto
  `Symmetry.OrbitAxes`. See **Deep audit › Revised order** below for the verified breakdown.

### (2) Polygon set-ops + point-in-polygon + IoU / centroid

The Analysis project uses **NetTopologySuite (NTS)** — 7 files: `RegionGeometry2d`, `SymmetryDetector`,
`IslandDetector`, `Buildability`, `ResourceSources`, `WoolSources`, `RegionAuthoringEncoder`.

- **Hand-rolled point-in-polygon dup'd outside NTS:** `Pgm/SketchRasterizer` (ray cast) + client
  `Client/Pages/Configure/SpawnPhase.PointInRing` + JS `geometry/polygon.js` `pointInRing`.
- **Consolidation:** a thin shared `contains` / IoU helper (wrap NTS server-side) and drop the hand-rolled
  ray-casts where NTS is already available.

### Bounds rebound (cross-cutting)

Scattered AABB re-bound after a transform: `Geometry2d.CornersToBounds`, `Symmetry.Rect`, `RegionParser`,
`RegionBuilder`, `AuthoringEndpoint`, `WorldFeatureWriter`. Converge the **affine** ones on `Symmetry.Rect`.

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
for **islands** — which are polygons, ray-cast in C# by `SpawnPhase.PointInRing` (dup'ing shape.js's
polygon contains across the runtime split). If intent zones ever go beyond rectangles (circle/polygon
protection or build areas), three things generalize together: the intent `Rect` → a shape type;
`OrbitAssignment`'s rect mirror → a ring mirror; and `BuildGenerator`/`WoolGenerator` (which emit
`rectangle` regions). That needs a **shared C# shape model** mirroring `shape.js`, folding in
`SketchRasterizer`'s and `SpawnPhase`'s hand-rolled point-in-poly. Until then, **rectangles are the
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
and the two C# `PointInRing` ray-casts (`SketchRasterizer` + `SpawnPhase` → `Geom.Polygon.PointInRing`).
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
  PUT. `SpawnPhase`/`ProtectionPhase` orbit-fill in WASM C# via `Contracts.Symmetry` + `OrbitAssignment`;
  the server's `SymmetryExpander.FillSpawns` then **no-ops** (teams already filled). So spawns/protection
  are *client*-authored, build areas are *server*-authored (client sends one side). Two orbit paths that
  agree **only because both call `Contracts.Symmetry`** — keep that the single source.

### Shapes (`shape.js`) — two models, not one
- JS `shape.js` is the rich vocabulary (`rectangle|circle|polygon|lasso`, Bézier `toRing`, `containsPoint`).
  C# has **no general shape model** — the intent is **rectangle/point only** and `SketchRasterizer`'s shape
  model is **sketch-private**. Rectangles are the contract; `OrbitAssignment` rect-only is correct, not a gap.
- `shape.js`'s header ("shared by editor + sketch") is **drift**: only the sketch path imports it. The
  **editor hit-tests regions by AABB** (`editor-canvas.js #hitTest` over `node.bounds`), islands by
  `pointInRing`. A consolidation must decide: editor adopts `containsPoint`, or correct the header.
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
any consolidation/relocation must keep its behaviour and `Contracts.Symmetry`-backed API intact; when
cylinders land, its rect `Covers` test generalises to a cylinder containment, not a rewrite.

### Ranked findings
**Correctness / latent:**
1. ~~**`SketchRasterizer.MirrorPoint` silently drops `mirror_d1`/`mirror_d2`**~~ **FIXED:** `MirrorPoint`
   now delegates to `Contracts.Symmetry.Point` (rot_270 = k=3 of rot_90), so every axis — incl. the
   diagonals — is covered and consistent with the generator + JS, and the hand-rolled switch is gone
   (one duplicate removed). Regression: `SketchRasterizerTests.Mirror_d1_adds_a_diagonally_reflected_copy`.
2. **`Traversability.RegionCentre` (`:87`)** uses the AABB midpoint, not the polygon centroid → nav-point
   can land in void for circle/half/compound spawn footprints. Use NTS `Centroid`.
3. **Rounding split:** `OrbitAssignment` rounds orbit corners to **integers**; `SymmetryExpander.TransformRect`
   to **1 dp**. No bug now (server no-ops), but lands the day the **Wools wizard** client-orbits rooms.
   Decide the convention and write it into `new-map-authoring.md`.

**Duplication:**
4. ~~`PointInRing` ×3~~ **DONE:** the two C# copies (`SpawnPhase`, `SketchRasterizer`) now call
   `Geom.Polygon.PointInRing`; `polygon.js` stays as the documented JS twin.
5. ~~`ReflectPoint`/`RotatePoint` in `SymmetryDetector`~~ **DONE:** routed through `Geom.Symmetry.Apply`.
   (`RegionGeometry2d.Reflect` uses an NTS `AffineTransformation` matrix on a whole `Geometry`, not the
   scalar form — left as-is; it's family-2 NTS, not a scalar-math dup.)
6. `MirrorBounds`/`UnionBounds`/`TranslateBounds` duplicated verbatim between `RegionParser` and
   `RegionBoundsDeriver`, plus a third dict-form in `Geometry2d.ReflectBounds2d`.
7. **No canonical map-bbox.** `WoolSources.MapBbox` (pad 8) and `Buildability.RegionBbox` (margin 16) both
   feed `RegionGeometry2d.ToGeometry`'s `bounds` (used to clip `half`/`negative`) → a `half` region can
   clip differently per analysis pass. Define one canonical extent.

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

Remaining:
1. Fix the orbit rounding convention (#3) + define one canonical map-bbox (#7).
2. Family (2): shared NTS contains/IoU; drop hand-rolled server ray-casts. Fix `RegionCentre` (#2).
3. Decide the editor-AABB-vs-`containsPoint` story (and correct `shape.js`'s header either way).
4. Leave the JS `geometry/*` layer as the documented preview twin (not merged).
5. Future residents of the leaf: a C# shape model (`Rect ∪ Cylinder`) when intent goes beyond rectangles;
   the generative layout algorithms (TSP/annealing, random-point polygon seeding).

> Note: `Geometry2d.ReflectBounds2d`/`RotateBounds2d` (dict-`bounds_2d` rebound) already delegate their
> *points* to `Geom.Symmetry`; the rebound loop is dict-shaped and stays in `Pgm` (the leaf is dict-free).
> The `UnionBounds`/`TranslateBounds` copies shared between `RegionParser`/`RegionBoundsDeriver` are
> non-affine (min/max merge, offset) — a separate Pgm-internal dedup, not a leaf fold.

> Constraint through all of the above: **keep `OrbitAssignment` intact** (the point-aware orbit→team
> keystone); and any new shape support stays within **`Rect ∪ Cylinder`** (PGM-expressible) — see the
> shape-model bound note above.
