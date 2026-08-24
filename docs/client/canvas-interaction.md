# The client JS layer — map and contracts

Status: **current** (describes what is in `src/PgmStudio.Client/wwwroot/js` today, verified against the
tree). This is the orientation document for the browser-side half of the studio: what the layers are, what
each module is for, which rules keep them from tangling, and where the known rough edges are. Blazor owns
chrome, forms, state and persistence; this layer owns everything that draws or responds to a pointer.

Read alongside `../tools/sketch.md` and `../tools/plan.md` (what the two drawing tools mean), and
`../tools/flow.md` (which tool works at which level, and how a map moves between them).

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
`SideviewCanvas` (a Canvas2D depth cross-section).

All three drawing surfaces — world, plan and sketch — are **hybrid**: the world layers are painted each
frame to a 2-D `<canvas>` pinned under the `<svg>` (via `render/canvas-painter.js` — the fix for Firefox
holding a stale rasterization across a zoom), while the screen-space chrome (labels, selection box, resize
handles, the scale bar) stays in the svg, which is also the single pointer target. Two consequences run
through everything below. Nothing world-side is retained, so a canvas's own state *is* the picture and
every setter ends in a repaint rather than an element edit — which is also why a controller's in-progress
preview is state with a `paint(painter)` method rather than elements it mutates. And nothing drawn is
addressable as an element: hit-testing is data-driven off the document, and the world half reports its
paint order through `painter.layers` rather than through `data-layer`. The fixed-fit previews
(`ConfigureRenderer`) are retained SVG on purpose: with no viewport transform, they hold no rasterization
that can go stale.

`SideviewCanvas` is the fourth surface and the odd one: it is painted throughout, with no svg half at all.
It has no viewport matrix either — its viewport is a fitted integer scale with centring offsets, and it
neither pans nor zooms, which is why it does not extend `CanvasBase`. It shares `CanvasPainter` for the
backing store (the CSS box × device pixel ratio) and the colour-token cache, and it draws through the
primitives whose call sites take plain numbers: `text`, `dot`, `line`. The box-shaped primitives are named
for the plan's x/z axes while this surface's second axis is elevation, so `min_z` would name a Y — those
draws are raw context calls inside a `layer()` phase, which brackets them in save/restore.

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
parts under unit test (§7). `render/` is stateless emit in two dialects. The **painted** dialect is the
default and takes a `CanvasPainter` plus data: `shape-render`, `sketch-render`, `symmetry-render`,
`canvas-chrome`'s working area, and `primitive-style`'s whole vocabulary speak it. The **retained** dialect
(`svg.js`) covers what stays in the DOM — the screen-space overlays and the fixed-fit previews. Where both
are wanted the *geometry* is factored out rather than written twice: `symmetryAxes`
is the type→lines rule for both, and the path builders (`ringToPath`/`polyToPath`) serve both because a
canvas `Path2D` takes SVG path data. Neither dialect holds document state. `canvas/` is where state lives.
`controllers/` are interaction strategies a canvas plugs in. `bridge/` is the only layer that talks to C#.

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
| `geometry/islands.js`, `region-convert.js` | GeoJSON coercion; PGM `+1`-rule bounds conversions |
| `plan/plan-doc.js` | the plan document model + its geometry (pure; the wire format lives here) |
| `plan/plan-inspect.js` | derived-structure overlay helpers for the plan inspect layer |
| `render/svg.js` | the retained dialect: the element factory for the screen-space overlays and the fixed-fit previews, plus the path builders both dialects share |
| `render/layer-stack.js` | the z-ordered **screen-space** layer groups, declared once per canvas (key order = paint order, bottom first); each group carries `data-layer="<name>"`, plus `showLayers`/`clearLayer` |
| `render/canvas-painter.js` | the painted dialect, and the whole of it: a DPR-aware 2-D surface — `begin(scale, pan)` applies the viewport so draws are in world units, `layer(name, paint)` brackets each phase in save/restore and records its name (`painter.layers` is the queryable paint order), `screenPx` holds a constant screen stroke width, `token`/`color` resolve and cache CSS custom properties (demoting any value the context won't parse), `toSurface` carries the world→surface fit, and the primitives (`rect`/`line`/`segments`/`circle`/`dot`/`ellipse`/`path`/`ring`/`poly`/`text`/`image`) take world coordinates and one style vocabulary |
| `render/canvas-chrome.js` | viewport-derived chrome shared by the drawing canvases: the visible-world rect, the grid-step ladder, the painted working area, the scale bar |
| `render/shape-render.js`, `sketch-render.js`, `symmetry-render.js`, `block-render.js` | shared stateless painting for primitives, sketch overlays, symmetry axes, block PNGs |
| `render/primitive-style.js` | the one place a primitive's fill/stroke style is decided, across all four editors |
| `render/iso-webgl.js` | the depth-buffered 3-D preview, on raw WebGL, lazily imported |
| `render/column-mesh.js` | the server's per-column runs → the triangles that preview draws; decides which faces are seen, and drops the runs of any sketch layer the viewer has hidden before deciding |
| `canvas/canvas-base.js` | the shared pan/zoom/drag machinery (§3) |
| `canvas/world-canvas.js` | the shared engine behind Edit + Configure (§1) |
| `canvas/plan-canvas.js`, `sketch-canvas.js`, `sideview-canvas.js` | the plan grid, the sketch surface, the depth cross-section (all painted; the first two hybrid, the third painted throughout) |
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
closures** in its constructor — getters for the screen-space layer or transform it needs — plus a
callbacks object. It exposes `onMouseDown`/`onMouseMove`/`onMouseUp`, `cancel()`, and mode-specific extras
such as `onResizeMove`/`onResizeUp`, `onDblClick` or `refresh`. It never reaches into canvas internals
beyond its accessors. The canvas forwards its `CanvasBase` hooks into whichever controller is active.

A **draw** controller holds its in-progress primitive as numbers and draws it through `paint(painter)`
from the canvas's `draw` phase, reporting each change through `onPreviewChanged` so the canvas repaints.
It does not own a layer, because nothing world-side persists between frames. An **edit** controller is the
opposite case and emits elements: its handles are screen-space, where a fixed pixel size, a cursor and a
`mousedown` target are exactly what is wanted.

There are two pairs — `world-draw`/`sketch-draw` and `world-edit`/`sketch-edit` — and **they are
deliberately not merged.** They share a protocol, not an implementation: the world canvas draws region
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

`world-bridge` mounts `WorldCanvas`, `plan-bridge` `PlanCanvas` (and owns the plan document and id minting;
it persists nothing — saving is the host's, on the author's word), `sketch-bridge` `SketchCanvas`, `sideview-bridge` `SideviewCanvas`, and
`scan-bridge` the non-interactive `ConfigureRenderer`. `fetch-json.js` is the shared no-store fetch. The
bridges look repetitive but are not: each owns different document semantics. What they genuinely share is
only the invoke wrapper, which is inconsistent today (**CV15**).

**A preview that cannot run says which of the two reasons it was.** `enterIso` fails for two unrelated
causes — the browser has no WebGL, or the server would not build the board — and for a long time both crossed
to C# as one bare `OnIsoUnavailable()`, so the canvas answered *no WebGL* on browsers that plainly had it and
the reader went looking in the wrong place entirely. The bridges now carry a reason: an empty string is WebGL
itself, and anything else is the sentence the build answered with, read out of the refusal envelope
(`message`, then `error`, then the bare status). The host shows *no WebGL* for the first and the build's own
words for the second. A failure with a sentence available should never be reported as a different failure.

**Three interop details cost an afternoon each the first time.** `InvokeVoidAsync(name, params object?[])`
**spreads** an array argument, so passing one whole array means boxing it — `(object)ids.ToArray()` — or the
JS side receives the elements as separate parameters. A Razor markup lambda cannot contain a `"` literal, so
an inline handler that needs an empty string uses `string.Empty` or a method group instead. And a
`.control-input--hidden` checkbox is `display: none`, so a test or a script that clicks it hits nothing —
the wrapping label is the clickable thing.

## 7. What is tested, and what is not

`npm test` (or `tools/js-test.sh`) runs Node's built-in runner over `tests/js/` — no `node_modules`, so it
works from the shared folder. 333 tests over 19 files pass.

Coverage splits cleanly along the DOM line. The modules the tests import average around 92% lines, several
at 100% (`transform`, `symmetry`, `islands`, `polygon`, `plan-inspect`, `decompose-cut`, `shape-render`);
`canvas-painter` is the one DOM-adjacent module under test, via a small context stub. Of the 55 studio
modules (12,694 lines), the tests reach 28 — the other **27 files, 8,017 lines, are never imported by a test
at all**: every canvas, every bridge (`sketch-bridge` 904 lines, `plan-bridge` 449), every controller,
`iso-webgl` and `studio.js`. Note that `node --test --experimental-test-coverage` reports
such files as *absent*, not as zero, so the report reads healthier than the tree is.

This is a coherent split rather than neglect: pure logic is tested, DOM-bound code is not. The painted
render layer sits on the tested side of it because a stateless painter takes a stand-in — `_painter-stub.js`
records what was drawn, so `shape-render`, `symmetry-render` and the style vocabulary are asserted on
without a canvas. For the canvases and controllers the way in is still to keep extracting decidable logic —
hit-testing, snapping, viewport maths, selection resolution — down into the pure layers the existing harness
already reaches (**CV12**).

**The bridges are the exception, and the split has hidden it.** A bridge is not DOM-bound in the way a canvas
is: `mount()` is handed its canvas and its elements, and the only other thing it touches is `fetch`. Both are
already stubbable with what `tests/js/` has — `_dom-stub.js` and `_painter-stub.js` are precedent — so
`enterIso`/`fetchColumns` can be driven directly, with the canvas a recorder and `fetch` answering a canned
payload or a refusal. That matters because `enterIso` is the most stateful function in the untested set: an
await, a race guard, a cache stamp and two failure paths, and a rename inside it shipped a
`ReferenceError` to the browser that neither the C# build nor the 333 JS tests could see.

Above the unit line, `tests/e2e/paint.mjs` is the one check that a painted surface actually paints. A blank
canvas raises no error and leaves no elements behind, so it is exactly as "clean" as a working one to the
smoke sweep; `paint.mjs` asserts on pixels instead, for each of the three hybrid surfaces: painted
coverage, buffer = CSS box × DPR, the screen chrome present in the svg, and that a wheel burst *changes*
the pixel signature — which a stretched raster would not. It reaches the world canvas through the Edit
tool's nav rail, since that route opens on a phase with no canvas mounted.

## 8. Known duplication and open work

The layer is about 6,600 lines of first-party code (the raw ~11,000 figure includes a 2,130-line vendored
`polygon-clipping`, plus comments and blanks). It is not bloated, and the remaining duplication is small in
line terms — a few hundred at most. It is filed because it costs *consistency*, not size:

- **CV15** — the bridge invoke wrapper: `plan-bridge` and `sketch-bridge` guard `invokeMethodAsync` in a
  `fire()` helper, `world-bridge` calls it unguarded.
- **CV9** — a point draws as a 1×1 block on Edit and as a fixed-radius dot on Configure. Tracked with the
  shared helper is `render/primitive-style.js`; §10 below is the four-editor account.

One stale reference remains in a module header: `static-renderer.js` cites an `OverviewRenderer` that no
longer exists.

## 9. Where to look for what

Changing how something *looks* → `render/`, and check `primitive-style.js` first, since it may already own
the decision. Changing how something *behaves under the pointer* → the relevant `controllers/` module, or
`canvas-base.js` if it affects every surface. Adding maths → `geometry/`, never a canvas class. Changing
what crosses to C# → the surface's bridge, and remember hot paths stay in JS. Touching symmetry → read the
twin-implementation warning in `CLAUDE.md` first; it has a canonical C# counterpart that must stay in step.
