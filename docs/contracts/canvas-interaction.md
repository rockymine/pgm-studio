# The client JS layer — map and contracts

Status: **current** (describes what is in `src/PgmStudio.Client/wwwroot/js` today, verified against the
tree). This is the orientation document for the browser-side half of the studio: what the layers are, what
each module is for, which rules keep them from tangling, and where the known rough edges are. Blazor owns
chrome, forms, state and persistence; this layer owns everything that draws or responds to a pointer.

Read alongside `primitive-styles.md` (how a primitive is *styled* across the four editors),
`sketch-authoring.md` and `plan-editor.md` (what the two drawing tools mean), and `tool-consistency.md`
(the phase/step model the hosts follow).

## 1. The one fact that frames everything

The Configure wizard has no canvas of its own. Every phase that draws mounts **the same `WorldCanvas`**
the Edit page uses, differing only by parameters. Sixteen Blazor components mount it today — eleven
Configure steps (`WorldScan`, `WorldIslands`, `WorldSymmetry`, `TeamAssign`, `Spawn`, `Protection`,
`BuildLayer`, `WoolRoom`, `WoolSpawn`, `WoolMonuments`, `WoolObjectives`) and five Edit phases (`Setup`,
`Regions`, `BuildRegions`, `Objective`, `Teams`).

The consequence is the single most useful thing to know before changing anything here: **wiring a
capability into `WorldCanvas` and its bridge makes it available to sixteen surfaces at once**, and
breaking one breaks all of them. The `if`/mode branches that configure the canvas per phase
(`IslandSelect`, `SymmetryMode`, `PointPick`, `DrawCategory`) are intentional configuration, not
duplication — leave them alone.

The other three canvases are genuinely separate engines because they draw different things: `PlanCanvas`
(the coarse cell grid of the plan editor), `SketchCanvas` (freeform shapes and boolean islands), and
`SideviewCanvas` (a Canvas2D depth cross-section, the only non-SVG surface).

## 2. Five layers, one direction

Modules are grouped by archetype, and imports run strictly **downward** — verified, with no cycles:

```
bridge      → canvas, plan, geometry, vendor
canvas      → controllers, plan, render, geometry
controllers → render, geometry
plan        → render, geometry
render      → geometry
geometry    → (vendor only)
```

`geometry/` and `plan/` are pure: point arrays and numbers, **no DOM**, which is exactly why they are the
parts under unit test (§7). `render/` is stateless SVG emit — it takes geometry plus a `toSvg` and returns
elements, holding no state of its own. `canvas/` is where state lives. `controllers/` are interaction
strategies a canvas plugs in. `bridge/` is the only layer that talks to C#.

Keeping a new module in the lowest layer that can hold it is the rule that has kept this tree navigable;
the corollary is that **pure logic must not be written inside a canvas class**, because nothing below the
canvas layer can then reuse or test it.

| Module | Role |
|---|---|
| `geometry/transform.js` | world↔SVG coordinate math — the single bridge between block space and screen space |
| `geometry/polygon.js` | **the** point-in-polygon home (`pointInRing`), rasterisation, half-plane clip |
| `geometry/shape.js` | the unified primitive model: rectangle / circle / polygon / lasso, `toRing`/`toBounds`/`containsPoint` |
| `geometry/boolean.js` | island booleans over `polygon-clipping` — the one sketch-domain geometry layer |
| `geometry/decompose-cut.js` | lane decomposition: lasso enclosure, edge markers, splitting a piece at two seams |
| `geometry/symmetry.js` | the JS twin of `PgmStudio.Geom.Symmetry` (see the warning in `CLAUDE.md`) |
| `geometry/triangulation.js` | ear clipping — the JS twin of `Geom.Triangulation.EarClip` |
| `geometry/islands.js`, `region-convert.js`, `shape-library.js` | GeoJSON coercion; PGM `+1`-rule bounds conversions; drag-on primitives |
| `plan/plan-doc.js` | the plan document model + its geometry (pure; the wire format lives here) |
| `plan/plan-inspect.js` | derived-structure overlay helpers for the plan inspect layer |
| `render/svg.js` | the element factory and path builders every other renderer uses |
| `render/layer-stack.js` | the z-ordered layer groups, declared once per canvas (key order = paint order, bottom first); each group carries `data-layer="<name>"`, plus `showLayer`/`showLayers`/`clearLayer` |
| `render/shape-render.js`, `sketch-render.js`, `symmetry-render.js`, `block-render.js` | shared stateless emit for primitives, sketch overlays, symmetry axes, block PNGs |
| `render/primitive-style.js` | the one place a primitive's fill/stroke style is decided, across all four editors |
| `render/iso-webgl.js` | the depth-buffered 3-D preview, on raw WebGL, lazily imported |
| `canvas/canvas-base.js` | the shared pan/zoom/drag machinery (§3) |
| `canvas/world-canvas.js` | the shared engine behind Edit + Configure (§1) |
| `canvas/plan-canvas.js`, `sketch-canvas.js`, `sideview-canvas.js` | the plan grid, the sketch surface, the Canvas2D cross-section |
| `canvas/static-renderer.js`, `configure-renderer.js` | fixed-fit non-interactive previews |
| `controllers/*` | one interaction mode each (§4) |
| `bridge/*` | one `mount()` per surface (§6) |
| `shared/panel-resize.js`, `studio.js` | sidebar drag bars; Lucide icons and small interop helpers |

## 3. The shared base

`CanvasBase` owns the machinery every interactive surface needs and nothing else: `_scale`/`_panX`/`_panY`,
the viewport `<g>`, wheel zoom about the cursor, middle-drag and left-drag pan, a 4px click-vs-drag dead
zone so a sloppy click still selects, body-drag of a grabbed handle, and `_clientToSvg`.

Subclasses do not override behaviour; they fill in hooks. `_onToolMousedown`, `_onPointerMove`,
`_onToolMouseup`, `_onCanvasClick`, `_onViewportChanged`, `_onZoom` and `_onMouseleave` are notifications.
`_onResizeMove` and `_onResizeUp` are *consume* hooks — returning `true` intercepts the event before pan
logic runs, which is how a controller claims a drag. Body-drag is a small protocol of its own: `_toWorld`
maps a point into world space (identity for sketch, an inverse transform for the editor), `_hitMovable`
decides whether something was grabbed, and `_moveStart`/`_moveTo`/`_moveBy`/`_commitMove` carry the drag
through to persistence, with `_moveTo` offering a snap-aware absolute path and `_moveBy` the incremental
fallback.

## 4. Controllers

A controller encapsulates exactly one interaction mode. It is a plain class that takes **accessor
closures** in its constructor — getters for the layer or transform it needs, because layers are rebuilt on
repaint — plus a callbacks object. It exposes `onMouseDown`/`onMouseMove`/`onMouseUp`, `cancel()`, and
mode-specific extras such as `onResizeMove`/`onResizeUp`, `onDblClick` or `refresh`. It never reaches into
canvas internals beyond its accessors. The canvas forwards its `CanvasBase` hooks into whichever
controller is active.

There are two pairs — `editor-draw`/`sketch-draw` and `editor-edit`/`sketch-edit` — and **they are
deliberately not merged.** They share a protocol, not an implementation: the editor draws region
primitives (rectangle drag, two-click cylinder) and resizes bounds with an 8-handle box plus keyboard
nudge, while the sketch tool draws four shape types under a boolean operation and edits individual
vertices, Bézier tangents and snapped edges. Unifying them would mean one class with two disjoint halves.
The protocol is the abstraction; the bodies are the domain.

## 5. Hit-testing — deliberately not one rule

Different surfaces need different notions of "what did I click", and the divergence is a decision, not
drift:

| Picker | Geometry | Why |
|---|---|---|
| `WorldCanvas.#hitTest` | smallest-area **AABB** containment, else nearest within a 2-block margin | forgiving region select — a 1-block point needs a clickable target, and a click inside a circle's bbox should still select it |
| `WorldCanvas.#hitTestIsland` | exact **point-in-polygon** | islands are world polygons, where "inside" must mean inside |
| `SketchCanvas` island pick | exact point-in-polygon, exterior minus holes | same, and holes must not swallow clicks |
| `PlanCanvas.#selectDown` | cell-grid containment | the plan is a coarse cell frame; a cell either contains the point or does not |

All of them route through the one predicate in `geometry/polygon.js`. That single home matters more than
it looks: two subtly different copies of a point-in-polygon test is how a hit test starts behaving
differently in one tool than another, which is precisely the bug a duplicate copy in `decompose-cut.js`
was set up to cause before it was removed.

## 6. Bridges — the interop seam

Each surface has one bridge exposing `async mount(...) → handle`. Blazor imports the module on demand
(`await JS.import`, no global and no load-order race), calls `mount` with the SVG and wrapper elements
plus a `DotNetObjectReference`, and keeps the returned handle to drive the canvas. Traffic is asymmetric
by design: **hot paths stay in JS** — cursor coordinates and zoom percentages are written straight to
label elements per mousemove — and only decisions cross to C# (`OnSelect`, `OnRegionDraw`,
`OnCanvasIslandSelect`, …).

`world-bridge` mounts `WorldCanvas`, `plan-bridge` `PlanCanvas` (and owns the plan document, id minting
and the debounced autosave), `sketch-bridge` `SketchCanvas`, `sideview-bridge` `SideviewCanvas`, and
`scan-bridge` the non-interactive `ConfigureRenderer`. `fetch-json.js` is the shared no-store fetch. The
bridges look repetitive but are not: each owns different document semantics. What they genuinely share is
only the invoke wrapper, which is inconsistent today (**CV15**).

## 7. What is tested, and what is not

`npm test` (or `tools/js-test.sh`) runs Node's built-in runner over `tests/js/` — no `node_modules`, so it
works from the shared folder. 148 tests pass.

Coverage splits cleanly along the DOM line. The fifteen modules the tests import average **82.8%** lines,
several at 100% (`transform`, `symmetry`, `islands`, `polygon`, `plan-inspect`, `decompose-cut`). The other
**26 files, roughly 6,900 lines, are never imported by a test at all** — every canvas, every bridge, every
controller, `iso-webgl` and `studio.js`. Note that `node --test --experimental-test-coverage` reports such
files as *absent*, not as zero, so the report reads healthier than the tree is.

This is a coherent split rather than neglect: pure logic is tested, DOM-bound code is not. The way to
improve it is not to mock a canvas but to keep extracting decidable logic — hit-testing, snapping,
viewport maths, selection resolution — down into the pure layers where the existing harness already
reaches (**CV12**).

## 8. Known duplication and open work

The layer is about 6,600 lines of first-party code (the raw ~11,000 figure includes a 2,130-line vendored
`polygon-clipping`, plus comments and blanks). It is not bloated, and the remaining duplication is small in
line terms — a few hundred at most. It is filed because it costs *consistency*, not size:

- **CV15** — the bridge invoke wrapper: `plan-bridge` and `sketch-bridge` guard `invokeMethodAsync` in a
  `fire()` helper, `world-bridge` calls it unguarded.
- **CV9** — a point renders as a 1×1 `<rect>` on Edit and a fixed-radius `<circle>` on Configure. Tracked
  with the full four-editor audit in `primitive-styles.md`.

One stale reference remains in a module header: `static-renderer.js` cites an `OverviewRenderer` that no
longer exists.

## 9. Where to look for what

Changing how something *looks* → `render/`, and check `primitive-style.js` first, since it may already own
the decision. Changing how something *behaves under the pointer* → the relevant `controllers/` module, or
`canvas-base.js` if it affects every surface. Adding maths → `geometry/`, never a canvas class. Changing
what crosses to C# → the surface's bridge, and remember hot paths stay in JS. Touching symmetry or
triangulation → read the twin-implementation warnings in `CLAUDE.md` first; those two have canonical C#
counterparts that must stay in step.
