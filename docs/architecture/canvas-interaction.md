# Canvas rendering & interaction infrastructure

Status: **mixed** — the `CV` task series in `TODO.md`. CV1 (resize/move wiring) and CV9
(primitive render styles) are **landed**; CV2/CV3/CV4/CV8 are design/in-progress. This is the
technical spec for the shared JS/canvas layer underneath the **Edit** editor (`/maps/{id}/edit`)
and the **Configure** wizard (`/maps/{id}/configure`) — de-duplication, region resize, arrow-key
move, the controller pattern, pruning/realignment, and (§10) how drawable primitives are rendered
and styled. It lives in `docs/architecture/` rather than `docs/tools/` because none of this is a
single tool's doc: Edit and Configure both mount the same canvas engine, and Sketch/Plan are
pulled in as consumers of the shared primitive-style vocabulary in §10.

Read alongside `docs/architecture/routing.md` (the two map surfaces) and `docs/tools/configure.md`
(the wizard phases). Coordinate/transform math lives in `transform.js`; C# geometry consolidation
is its own track (`A4`). The sketch tool's canvas port (design, not yet built) is **not** covered
here — see `docs/tools/sketch.md`.

---

## 0. The one fact that frames everything

The Configure wizard does **not** have its own canvas. Every built phase mounts the **same
`EditorCanvas`** the Edit page uses, via `studio.mountCanvas` → `studio-canvas.js`:

| Phase | Mount | Mode |
|---|---|---|
| WorldScan | `<EditorCanvas/>` (`WorldScanStep.razor:39`) | read-only + Blocks toggle |
| WorldIslands | `IslandSelect="true"` (`WorldIslandsStep.razor:53`) | island pick |
| WorldSymmetry | `SymmetryMode="true"` (`WorldSymmetryStep.razor:56`) | axis overlay |
| Teams | `IslandSelect="true"` (`TeamAssignStep.razor:97`) | island pick + team tint |
| Spawn | `PointPick="true"` (`SpawnStep.razor:39`) | point-drop + marker pick |

Consequence: **wiring a capability into `EditorCanvas` + its bridge makes it available to both
surfaces at once.** Resize and arrow-key move (below) must reach the draw steps `N02` (spawn
protection), `N03` (build), `N04` (wool rooms) — those phases will mount `<EditorCanvas
DrawCategory="…">`, so they inherit the shared mechanism for free.

The `if`/mode branches that configure each canvas + toolbar per activity (e.g. Blocks toggle hidden
when `IslandSelect || SymmetryMode || PointPick`, `EditorCanvas.razor:46`) are **intentional
configuration, not duplication** — leave them.

---

## 1. Current architecture (what exists, verified)

> **`wwwroot/js/studio/` is now organised into five layers** (reorg landed; one folder per
> archetype, strict downward dependency `bridge → canvas → controllers → render → geometry`).
> The line/path references later in this doc predate the reorg — treat them as historical CV-task
> notes; the layout below is the current map. Pure layers are unit-tested (`tools/js-test.sh`,
> `tests/js/`, Node's built-in runner — no node_modules).

```
geometry/      pure math, NO DOM — point arrays & numbers only
  transform.js     buildTransform / buildInverseTransform (world↔svg)
  polygon.js       pointInRing, rasterisePolygon, clipHalfPlane
  symmetry.js      applySymmetry, applySymmetryToBounds
  region-convert.js  region/shape → 2D bounds (the +1-rule conversions)
  islands.js       geojsonToSimplified, normalizeIslands

render/        stateless SVG emit — imports geometry + a toSvg, nothing else
  svg.js           svgEl, handleRectAttrs, ringToPath/polyToPath/boundsToRingPath, anchorBlockEl
  shape-render.js  renderShape(type, boundsOrPoly, toSvg, attrs)
  primitive-style.js  primitiveStyle({ colour, treatment, selected, primary }) — §10
  symmetry-render.js  renderSymmetryOverlay   block-render.js  blockDataToDataUrl + renderBlockImage
  palette.js       game colours (chat / dye / team)

canvas/        stateful engines
  canvas-base.js       interactive pan/zoom/drag FSM (base)  → extended by editor-canvas.js
  static-renderer.js   fixed-fit preview base (svg sizing + transform + viewport + resize)
                       → extended by configure-renderer.js, overview-renderer.js
  sideview-canvas.js   standalone Canvas2D depth cross-section

controllers/   interaction strategies plugged into a canvas (onMouseDown→bool, onMouseMove, …)
  editor-draw-controller.js   rect/cuboid drag + cyl/circle 2-click
  editor-edit-controller.js   8-handle resize + arrow-key nudge
  select-controller.js        generic click-mode registry (region / island)

bridge/        C#-interop: mount() → a `handle` object Blazor calls; one *-bridge.js per surface
  editor-bridge.js  configure-bridge.js  overview-bridge.js  scan-bridge.js  sideview-bridge.js
  fetch-json.js     (no-store fetch helper, bridge-only)

EditorCanvas.razor(.cs)   the Blazor host: parameters, [JSInvokable] callbacks, toolbar UI
# (the sketch canvas [extends canvas-base] + its sketch-*-controllers are a design, not yet
#  ported — see docs/tools/sketch.md)
```

`CanvasBase` provides `_scale/_panX/_panY/_viewportG/_activeTool`, wheel zoom, middle/left-drag pan,
a 4px click-vs-drag dead-zone, `_clientToSvg`, and the hook surface subclasses override
(`_onToolMousedown`, `_onPointerMove`, `_onToolMouseup`, `_onCanvasClick`, `_onViewportChanged`,
`_onZoom`, `_onMouseleave`, and the consume hooks `_onResizeMove`/`_onResizeUp` which return `true`
to intercept an event before pan logic runs).

---

## 2. Hit-testing — two pickers (spawn folded in)

`EditorCanvas` now has **two** pickers, one per genuine select mode (`EditorSelectController`, §5):

| Picker | Reads | Returns | Geometry | Used by |
|---|---|---|---|---|
| `#hitTest` | `#nodeMap` (regions) | node | smallest-area **AABB** containment, else **nearest within a 2-block margin** | region select (Edit + all Configure draw/spawn steps) |
| `#hitTestIsland` | `#ctx.islands` | island id | true **point-in-polygon** (`#pointInRing`) | WorldIslands / Teams |

**Spawn-pick was unified away.** Spawns used to be markers in a separate `#authorSpawns` array, picked
by a proximity-only `#hitTestSpawn` returning a *team*. They are now **point dummy regions** in
`#nodeMap` (`{team}-spawn`), picked by the normal `#hitTest`; the 2-block margin (added to `#hitTest`)
gives the same forgiving click a 1-block point needs. So `#hitTestSpawn`, the `#authorSpawns` marker
layer, and the `spawn` select mode are gone — the canvas has one representation of intent geometry
(dummy regions) and one select rule. Islands stay separate (world polygons, not bounds primitives).

Precision notes: region picking is AABB + margin (a click inside a circle's bbox but outside its
radius still selects it; a click within 2 blocks of a small region's bounds selects it when nothing
contains the point); island picking is exact point-in-polygon.

---

## 3. Region resize (8-handle) — built, but never wired

### 3.1 What already exists in `EditorCanvas`
- **8 handles** (`HANDLE_DEFS`, `editor-canvas.js:33-42`): corners `nw/ne/sw/se` + edge midpoints
  `n/e/s/w`, each with its own resize cursor.
- `#renderHandles(node)` (`:1021`) draws them into the overlay layer; each handle's `mousedown`
  sets `#resizeState = { node, xField, zField, cursor }` (`:1031-1037`).
- `#handleFields` (`:1013`) maps a handle key → which bounds field(s) it edits, accounting for
  flipped axes.
- `#doResize(clientX, clientY)` (`:1042`) converts pointer → world, rounds to block, enforces a
  **1-block minimum** extent, and calls `onBoundsChange(node, newBounds)`.
- The `CanvasBase` consume hooks are already overridden: `_onResizeMove` → `#doResize` (`:189`);
  `_onResizeUp` → `onBoundsSave(node, bounds)` then clears `#resizeState` (`:195`).
- `#updateOverlay` (`:857`) renders the selection chrome (name label + dimension pill) and calls
  `#renderHandles` **only if `RESIZABLE_TYPES.has(node.type)`** — i.e. `rectangle`/`cuboid`
  (`:45`, `:907`). It early-returns when `#selectedNode` is null or `is_negative`.
- `showAnchors(node)` / `clearAnchors()` (`:227`, `:236`) set/clear `#selectedNode` and repaint the
  overlay. `updateRegionBounds` / `refreshRegionBounds` (`:397`, `:488`) live-update a shape's SVG.

### 3.2 The wiring gap (why it's dead today)
Verified: `studio-canvas.js` does **not** pass `onBoundsChange`/`onBoundsSave` in the callbacks
object, and its `handle` exposes none of `showAnchors`/`clearAnchors`/`updateRegionBounds`. So:
1. `#selectedNode` is never set → `#updateOverlay` early-returns → **handles never render**.
2. Even if they did, `onBoundsChange`/`onBoundsSave` are `undefined` → **drag would do nothing**.

### 3.3 Persistence already exists
`PATCH /api/map/{slug}/regions/{regionId}` accepts a `bounds` key and writes it
(`RegionEditor.PatchRegion`, `RegionEditor.cs:153-193`). Precedent: `BuildRegionsActivity` patches
`regions/{id}` with `{coords:{y}}` and `{id:newId}` today (`BuildRegionsActivity.razor.cs:128-139`).
Resize/move = the same call with `{bounds:{min_x,min_z,max_x,max_z}}`.

### 3.4 What landed in CV1
The live drag stays **entirely in JS** (the hot path; "only selection calls C#"), so only the final
footprint round-trips on mouse-up. Region geometry is now **editable from two surfaces that share one
persistence path** — the canvas handles and the inspector fields — so neither goes stale.

1. **Selection drives the overlay, in JS.** `setSelectedRegions(ids)` (already called on every
   selection, canvas-click *and* sidebar) resolves the selection: a **single resizable** region calls
   `showAnchors(node)` (dimension pill + 8 handles); otherwise `clearAnchors()`. No new bridge method,
   no new C# selection wiring — it piggybacks on the existing `setSelection` round-trip.
2. **Live update in JS.** `#doResize` calls `this.updateRegionBounds(node, nb)` (shape + anchors +
   overlay follow the cursor) instead of round-tripping per mouse-move. `HANDLE_SIZE` is `14`
   (screen px) for an easy grab target.
3. **Persist via the host, through one event.** `_onResizeUp` fires `onBoundsSave`; the bridge
   forwards it and `EditorCanvas` raises the **`OnGeometrySaved(id, min/max x/z)`** parameter — it does
   **not** persist itself (so the Configure wizard can route the same event to its intent slice in CV2).
   The Edit activity persists via `RegionEdits` and reloads only if the server **rejects** the edit.
4. **Editable inspector.** `RegionInspector` coord fields become editable when the host wires
   **`OnSetCoord(key, value)`**; footprint keys (`min_x/min_z/max_x/max_z`) route through the `bounds`
   PATCH, all other keys (cuboid `min_y/max_y`, point `x/y/z`, cylinder `base/radius/height`, …) through
   the `coords` PATCH (`ApplyCoordUpdate`). After persisting, the host pushes the new footprint back to
   the canvas via **`RefreshRegionBoundsAsync`** so the shape follows a typed edit.
5. **Shared helper.** `Models/RegionEdits` owns the bounds-vs-coords routing, the PATCH, and the
   in-place node update (Coords + Bounds) so the inspector and canvas agree without a full reload. Both
   the drag (`OnGeometrySaved`) and the typed edit (`OnSetCoord`) funnel through it. Wired in all four
   Edit activities that pair the canvas with the inspector (Regions, Build, Objective, Teams).
   `onBoundsChange` is intentionally **not** wired (no per-move interop).

### 3.5 Availability requirement
The resize mechanism must be reachable on **Edit** (the proven page) **and** in the Configure draw
steps **N02 / N03 / N04**, where the author resizes the spawn-protection / build / wool-room rects
they draw. Because all of these mount `EditorCanvas`, the wiring above covers them — each phase host
only needs to honour `OnBoundsSave` against its own intent slice (the wizard's `Intent` +
`MarkDirty`, not the raw region PATCH, for intent-backed phases).

### 3.6 Constraints
- Handles only on `rectangle`/`cuboid` (`RESIZABLE_TYPES`). Circular/polygon types are out of scope
  for handle-resize.
- Bounds round to whole blocks; minimum extent 1 block per axis (already enforced in `#doResize`).

---

## 4. Move — arrow-key nudge (and body-drag, optional)

### 4.1 Reference behaviour (the design source)
In the reference sketch flow the **activity** owns the keyboard, not the canvas
(`sketch-layout-activity.js:82-96`): a `document` keydown handler, guarded by *panel hidden* and
*focus not in INPUT/TEXTAREA* and *something selected*, computes `step = shiftKey ? 16 : 1`, maps
Arrow keys → `dx/dz`, `preventDefault()`, and calls a model-level `moveShape(dx, dz)` that translates
the selected shape and triggers a debounced save. Keeping keyboard in the host (not the canvas) is
deliberate — the canvas stays a dumb renderer.

### 4.2 Port shape
- **Host owns keydown.** For Edit, the owning activity; for Configure, the phase component. Same
  guards: skip when the canvas isn't visible, when focus is in a text input, and when nothing is
  selected. `step = Shift ? 16 : 1`; `ArrowLeft/Right → dx ∓`, `ArrowUp/Down → dz ∓`;
  `preventDefault`.
- **Translate the selected region's bounds** by `(dx, dz)` and route through the **same persistence
  as resize** (§3.3): live echo via `updateRegionBounds`, save via the `OnBoundsSave` path.
- Expose a small canvas/bridge helper `moveSelected(dx, dz)` (translate `#selectedNode.bounds` +
  `updateRegionBounds` + return the new bounds) so the host doesn't recompute geometry.
- Applies to the same types as resize, plus point/block (translating a single-cell region is valid);
  define per-type translation in one place.

### 4.3 Body-drag (optional, lower priority)
"Freely drag the region around" by grabbing its body (not a handle) is a natural companion but is
**not** in the current `EditorCanvas` (only handle-resize exists). If added, it belongs in the same
edit controller (§5) as a body `mousedown` → translate, reusing the resize persistence. Treat as a
follow-up, not a blocker.

---

## 5. Controller pattern

### 5.1 The contract
A **controller** encapsulates one interaction mode. It is a plain class that:
- takes **state accessors** in its constructor — getter closures for the layer(s)/transform it
  needs (because layers are rebuilt on `#repaint`) — plus a **callbacks** object;
- exposes `onMouseDown(...) → bool` (return `true` to consume), `onMouseMove`, `onMouseUp`,
  `cancel()`, and mode-specific extras (`onResizeMove/Up`, `onDblClick`, `refresh`);
- never reaches into canvas internals beyond its accessors.

The canvas forwards its `CanvasBase` hooks into the **active** controller. `EditorDrawController`
(`editor-draw-controller.js`) already follows this exactly — constructed with `() => drawLayer`,
`() => toSvg`, `{ onRegionDraw }`, dispatched from `_onToolMousedown/Move/Up`. It is the template.

### 5.2 New: `EditorEditController` (resize + move)
Extract the inline resize machinery (`#renderHandles`, `#handleFields`, `#screenBounds`,
`#doResize`, `#resizeState`, and the `_onResizeMove/_onResizeUp` bodies) into an
`EditorEditController`, mirroring the draw controller. Add the §4 `moveSelected` translation here so
**resize and arrow-move share one persistence + one "selected region" notion**. This is also where
optional body-drag lands. Routing: `_onResizeMove → edit.onResizeMove`, `_onResizeUp →
edit.onResizeUp`, exactly as the reference sketch routes its edit controller through the consume
hooks.

> Sequencing note: §3/§4 can be wired **inline first** (ship the UX), then refactored into this
> controller — or done controller-first to avoid rework. Recommended: wire inline to prove the UX
> on Edit (`CV1`), then extract the controller (`CV4`) so the extraction is a pure move with no
> behaviour change to verify against.

### 5.3 Mode controllers (select / island)
The `_onCanvasClick` branches were mode logic. Each is now a registered picker on
`EditorSelectController` (region-select owns `#hitTest`; island-select owns `#hitTestIsland`) so
adding a mode no longer means adding an `if`. The former spawn-pick mode (and `#hitTestSpawn`) is
gone — §2's unification turned spawns into point dummy regions picked by the one `#hitTest`. This is
the broader "controller pattern" investigation — lower urgency than
resize, but it is the abstraction the **sketch canvas port** needs anyway (see
`docs/tools/sketch.md`): `SketchDrawController` and `SketchEditController` slot into the same
contract, so the port plugs in instead of bolting on.

---

## 6. De-duplication

### 6.1 JS render duplication (collapse into shared helpers)
| Logic | Copies | Action |
|---|---|---|
| Symmetry axis + centre | `ConfigureRenderer.#renderSymmetry` (`configure-renderer.js:191`), `EditorCanvas.#renderSymmetry` (`editor-canvas.js:693`), `OverviewRenderer.#renderSymmetry` (`overview-renderer.js:95`) | Extract `renderSymmetryOverlay(group, type, cx, cz, toSvg)` next to `shape-render.js`; point all three at it. **Fixes a latent bug**: `ConfigureRenderer` omits the diagonal cases `mirror_d1`/`mirror_d2` that `EditorCanvas` handles, so the scan/legacy preview can't draw diagonal mirrors. |
| Island polygons | `ConfigureRenderer.#renderIslands` (`:160`), `EditorCanvas.#buildIslands` (`editor-canvas.js:625`) | Extract `renderIslandPaths(...)`; both call it. |
| Block → PNG | shared `blockDataToDataUrl` (`block-render.js`) used by Configure/Overview, but `EditorCanvas.#renderBlockImage` (`editor-canvas.js:595`) inlines a 4th copy | Make `EditorCanvas` use the shared helper. |

### 6.2 JS bridge boilerplate
`fetchJson` (no-store) and the `mount`/`dispose` handle factory are copied across `studio-canvas.js`,
`configure-canvas.js`, `scan-canvas.js`, `overview-canvas.js`. Extract one `fetchJson` + a small
handle helper into a shared bridge module.

### 6.3 C# geometry — DONE in A4
`SpawnStep` now routes through the canonical `PgmStudio.Geom` leaf: `Symmetry.Order`/`Symmetry.Point`
for orbit-fill and `Polygon.PointInRing` for island hit-testing — no hand-rolled transforms or ray-cast
remain. What's still open here is **not** geometry: the symmetry **label** (`SymLabel`, byte-identical in
`WorldScanStep`/`WorldSymmetryStep`) and **team-count** mapping (repeated in
`WorldSymmetryStep`/`TeamAssignStep`/`SpawnStep`) should collapse into one shared C# `SymmetryInfo`
helper — small, low-risk (= **CV8**).

### 6.4 Style/colour duplication (CV9 scope — see §10.5)
Three more copies fall under the primitive-style unification landed in CV9: the add/sub colour
constants, the Plan role-colour palette, and the near-duplicate `#regionAttrs`/`shapeAttrs` style
functions. Detailed in §10.5.

---

## 7. Pruning / realignment

`EditorCanvas` exposes a large public surface the bridge never forwards — but per the resize/move
work above, much of it is **essential-UX-not-yet-wired**, not dead. Triage (status as landed):

- **Wired (the resize/move/selection chain) — done:** `updateRegionBounds` is the edit
  controller's live-drag `applyBounds`; `showAnchors`/`clearAnchors` fire from `setSelectedRegions`
  when exactly one resizable region is selected; `refreshRegionBounds` is forwarded by the bridge
  for inspector/move edits; `moveSelected` lives on `EditorEditController` and backs arrow-move.
- **Keep, evaluate per feature as its UI lands:** `addRegion`, `removeRegion`, `renameNode`,
  `setRegionVisible`, `setBuildVisible`, `setResolvedMode`, `setPoisVisible`, `focusRegion`,
  `refreshRegions` — these back inspector edit / visibility / focus features that are still on the
  board (e.g. `C11`). Don't delete; wire when their feature is built.
- **Doc header realigned — done:** the `editor-canvas.js` header now lists the full grouped surface
  and the `CanvasBase` + three-controller delegation.
- **`#hitTestSpawn`:** removed entirely by the §2 unification (spawns became point dummy regions);
  the select controller now registers only `region` + `island`. No spawn-pick mode remains.

Pruning here means *fixing the wiring + the doc to tell the truth*, not removing capability.

---

## 8. Data flow (resize + move, end to end)

```
select region  → setSelection(ids)  (JS, via the existing select round-trip)
               → setSelectedRegions → showAnchors (single resizable) → handles render
drag handle    → #doResize → updateRegionBounds   (JS, live — no interop per move)
arrow key      → host keydown → moveSelected → updateRegionBounds   (JS live; CV3)
release / save → onBoundsSave(id, bounds)   [JS→C#]   (mouse-up; nudge = debounced)
               → host: PATCH /api/map/{slug}/regions/{id} {bounds}   (Edit)
                 or:   patch Intent slice + MarkDirty                (Configure intent phases; CV2)
               → reload only on server reject (success keeps live geometry + zoom)
```

---

## 9. Verification

- After §3/§4: on Edit, select a rectangle region → 8 handles appear → drag resizes and persists
  across reload; arrow keys nudge 1 block (Shift = 16) and persist. Repeat in `N02`/`N03`/`N04`
  draw steps. Restart `./tools/dev.sh` and verify in-browser.
- After §6.1: the `/maps/new` scan preview and Configure symmetry step render **diagonal** mirrors
  (the previously-missing `mirror_d1`/`mirror_d2` case) identically to the Edit canvas.
- After §5.2: pure-refactor — behaviour identical to the inline version; no new persistence.

---

## 10. Primitive drawing styles across the editors (landed, CV9)

Status: **landed** (`CV9`). This is the cross-editor inventory of *how a drawable primitive is
rendered and styled* in **Sketch**, **Edit**, **Configure**, and **Plan**, and the design that
unified it — the successor to the narrower Edit-vs-Configure comparison this doc used to carry.
§10.6's conclusion is now implemented: the shared helper is `render/primitive-style.js`
(`primitiveStyle`), and `renderShape` has the `point` case.

The renderers audited: `render/shape-render.js` (shared), `render/sketch-render.js`,
`canvas/editor-canvas.js`, `canvas/plan-canvas.js`.

### 10.1 What "a primitive" means in each editor (the semantic frame)

The four editors draw axis-aligned/radial shapes, but a shape *means* a different thing in each —
and the visual style already encodes that meaning. This is the fact that frames the whole refactor:
**style is a function of the primitive's semantic tier, not of the editor.** The same tier looks the
same wherever it appears; different editors just populate different tiers.

| Editor | What a drawn shape *is* | Background | Colour carries |
|---|---|---|---|
| **Sketch** | a **terrain shape** in a boolean vocabulary (add / subtract / override) that later rasterises into the base | none (blank authoring grid) | the **operation** (add=teal, subtract=red) |
| **Edit** | a **real `map.xml` region** (the source of truth) | immutable rasterised terrain | nothing — uniform slate; a region has no team meaning at this layer |
| **Configure** | a **region intent** (dummy node) — the same primitives as Edit but **derived/suggested** | immutable rasterised terrain | the **team / dye** it belongs to (derived) |
| **Plan** | a **rectangle piece coloured by role** — some pieces are true terrain + XML regions, some are annotation, some are technical/visualization-only | none (cell grid) | the **role** (+ surface-height tint) |

The through-line: **Sketch and Plan are where the author *decides* geometry/symmetry; Edit and
Configure are where it is *shown* (real) and *suggested* (derived).** Plan is the odd one out — it
draws rectangles only and layers three *tiers of realness* on top (terrain / annotation / technical),
which is exactly the visual vocabulary a unified primitive-style descriptor needs to express.

### 10.2 Shape-type inventory — which renderer, which SVG element

`renderShape(type, boundsOrPoly, toSvg, attrs)` (`render/shape-render.js:21-53`) is the **shared**
type→element dispatch. It is imported by `editor-canvas.js` (Edit+Configure) and `sketch-render.js`
(Sketch). **Plan does not use it** — `plan-canvas.js` draws every piece as a `<rect>` directly and
adds its own hatch patterns and objective markers.

| type | branch | SVG element | anchor |
|---|---|---|---|
| polygon (`.exterior`/`.polygons`) | polygon path | `<path fill-rule=evenodd>` | `shape-render.js:25-30` |
| `cylinder` / `circle` / `sphere` | `RADIAL_TYPES` | `<ellipse>` | `shape-render.js:9,36-44` |
| `rectangle` / `cuboid` / `block` / **`point`** / … | fallthrough | `<rect>` | `shape-render.js:46-52` |

Per-editor type coverage:

- **Sketch** (`sketch-render.js:33-56`): `rectangle`→rect, `circle`→ellipse, `polygon`/`lasso`→inline
  `<path>` (Bézier-capable, bypasses `renderShape`). Library primitives (`shape-library.js:36-52`)
  are **not** new types — `instantiate()` emits plain `rectangle`/`polygon` specs (n-gons and
  polyominoes are polygons; the `I` bar is a rectangle; `holesquare` is add-rect + subtract-rect).
- **Edit / Configure** (`editor-canvas.js:976-1003`): `rectangle`/`cuboid`→rect, radial→ellipse,
  and **`point` with `marker:true`** is intercepted *before* `renderShape` and drawn as a fixed-size
  `<circle>` (`editor-canvas.js:986-997`). Composite/transform types (`union`/`intersect`/`negative`/
  `complement`) are filtered out before render (`editor-canvas.js:60,1064`).
- **Plan** (`plan-canvas.js:429-478`): every piece/zone is a `<rect>`; objective markers are a
  `<circle>` (spawn, with a facing line) or a rounded `<rect>` (wool/iron).

**The `point` gap (the concrete CV9 bug, now fixed).** `renderShape` had **no `point` case** — a
bare point fell through to the `<rect>` branch and rendered as a 1×1 block that shrank with zoom.
The only reason Configure spawns looked right was the `marker:true` opt-in that swapped in a
fixed-radius circle *outside* `renderShape`. So a "point" was a rect on Edit and a circle on
Configure — same type, two looks, because the fix lived in one caller instead of the shared
renderer. `renderShape` now has a real `point` case (§10.6).

### 10.3 Style inventory — the visual language

Each editor has its **own** style function; there is no shared style descriptor. But the styles fall
into a small, consistent vocabulary:

| tier / treatment | meaning | fill | stroke | where |
|---|---|---|---|---|
| **solid, opaque** | real buildable terrain / real region | role/dye/team colour, `fill-opacity 0.7–0.85` | solid, same colour | Plan generating pieces (`plan-canvas.js:442-457`) |
| **translucent, dashed** | a region / an area (not solid ground) | colour @ `0.20` (Edit/Configure) / accent @ `0.22` (Plan zone) | dashed (`4,2` region · `7 4` zone) | `editor-canvas.js:1012-1016`; `plan-canvas.js:402-421` |
| **hatched, dashed** | technical / visualization-only (teaches behaviour: intended holes, dock points) | diagonal/crossed hatch pattern | dashed, same colour (`5 4`) | Plan buffer/connector (`plan-canvas.js:429-440,303-321`) |
| **boolean-tinted** | terrain add vs subtract | teal (add) / red (sub) @ `0.28`; `6 3` dash if override | solid | Sketch (`sketch-render.js:19-27`) |
| **fixed-size marker** | a point objective (spawn / wool source) | team/dye/marker colour @ `0.85–1.0`, radius **fixed** (not zoom-scaled) | ink/`marker-stroke` | `editor-canvas.js:986-997`; `plan-canvas.js:461-478` |
| **ghost / derived** | a non-editable symmetry-orbited or cross-layer preview | colour @ `0.06–0.08`, finer dash | faint | `editor-canvas.js:1008-1011`; `plan-canvas.js:360-400`; sketch `sketch-render.js:85-109` |

The exact style knobs, per editor:

- **Edit / Configure** — `#regionAttrs(color, ghost)` (`editor-canvas.js:1007-1017`): region fill
  `0.20` + dash `4,2`; ghost fill `0.06` + dash `2,3`; selected → solid, width `2.5`
  (`editor-canvas.js:1035-1039`). Marker circle `r 6/5` by `primary` (`:986-997`).
- **Sketch** — `shapeAttrs()` (`sketch-render.js:19-27`): fill `0.28`, width `1.2`, add/sub colour,
  `override`→`6 3` dash. Islands/mirror/ghost-islands each have their own attrs
  (`sketch-render.js:71-109`).
- **Plan** — inline per-role in the renderer: generating pieces solid `0.7` + surface-height `tint()`
  toward white (`plan-canvas.js:442-457`); annotation pieces hatched `0.9` + dashed `5 4`
  (`:429-440`); build zone translucent-accent `0.22` + dashed `7 4` with cut-out holes (`:402-421`).

### 10.4 Colour source — the real Edit-vs-Configure-vs-rest divergence

| editor | colour source |
|---|---|
| **Edit** | **none** — real tree regions carry no `color`; `region.color ?? var(--canvas-region)` always falls back to slate (`editor-canvas.js:978`, `--canvas-region` `tokens.css:99,197`). Every Edit region is uniform slate. |
| **Configure** | **team / dye hex** — every dummy node is tinted `GameColors.ChatHex(team)` or `DyeHex(color)` (`ProtectionStep.razor.cs:218-234`, `SpawnStep.razor.cs:291-297`, `WoolRoomStep.razor.cs:192-209`, …). |
| **Sketch** | **operation** — add teal `--canvas-add-*`, subtract red `--canvas-sub-*` (`tokens.css:68-71`). |
| **Plan** | **role** — `ROLE_COLORS` (piece grey, spawn purple, wool-room green, buffer orange, connector teal; `plan-doc.js:21`), lightened by surface height. |

### 10.5 Icons — the sidebar/inspector inconsistency

`RegionNode.Icon(type)` (`Models/RegionNode.cs:85-103`) is the **canonical** type→Lucide map
(`point→dot`, `block→square`, `rectangle→rectangle-horizontal`, `cylinder→cylinder`, …). Most
Configure phases consume it dynamically, but several **hardcode** an icon that disagrees with the
node's real type:

| razor | hardcoded | node type | verdict |
|---|---|---|---|
| `SpawnStep.razor:26,36,62,108` | `cylinder` | `point` (marker) | **mismatch** — should be `dot` |
| `WoolMonumentsStep.razor:28,51,107` | `square` | `point` (marker) | **mismatch** — should be `dot` |
| `WoolSpawnStep.razor:24,47,70` | `dot` | `point` (marker) | matches canonical |
| `ProtectionStep` / `WoolRoomStep` / `BuildLayerStep` | `rectangle-horizontal` | `rectangle` | matches |

The point-markers are the hotspot: they render as circles but their sidebar icons are hardcoded to
`cylinder` / `square` instead of the canonical `point→dot`.

### 10.6 Duplication the refactor collapsed

- **Add/sub colour constants x3**: `sketch-render.js:12-15` (committed), `sketch-draw-controller.js:19-22`
  (previews), plus raw tokens in `components.css`. A recolour needs all three.
- **Plan role colours x2**: `plan-doc.js:21` (`ROLE_COLORS`) and `PlanEditor.razor.cs:100-112`
  (toolbar/inspector palette) — two hand-kept copies of the same five hexes.
- **Two style functions that are 90% the same**: `#regionAttrs` (Edit/Configure) and `shapeAttrs`
  (Sketch) differ only in fill-opacity (0.20 vs 0.28), dash pattern, and colour source.

### 10.7 Unification conclusion (landed)

"Draw a primitive" became **one data-driven thing** by separating three inputs that were previously
tangled into each editor's bespoke draw code:

1. **shape** — `{rectangle | radial | polygon | point}`. Fixes the `point` gap by giving `renderShape`
   a real `point` case (a dot/circle sized in *screen* units), so the `marker:true` workaround in
   `editor-canvas.js` collapses into the shared renderer.
2. **colour** — supplied by the caller from its own semantic source (Edit: none/slate · Configure:
   team/dye · Sketch: operation · Plan: role). The renderer never decides colour.
3. **treatment** — one enum over the §10.3 vocabulary: `region` (translucent dashed) · `terrain`
   (solid opaque) · `technical` (hatched) · `marker` (fixed-size) · `ghost` (faint derived). Each
   editor picks a treatment per primitive instead of hand-writing fill/stroke/dash.

The `primitiveStyle({ colour, treatment, selected, primary })` helper (`render/primitive-style.js`)
replaces `#regionAttrs`, `shapeAttrs`, and the inline plan role-styling; `renderShape` grows the
`point` case and stays the shared element factory. Icons route through `RegionNode.Icon`
everywhere (the hardcoded `cylinder`/`square` in `SpawnStep`/`WoolMonuments` is the remaining
cleanup — §10.5), so `point→dot` is consistent.

Scope note: Plan's hatch patterns and surface-height tint are genuinely Plan-specific and stay in
`plan-canvas.js`; the win is the *shared* pieces — the `point` render, the style vocabulary enum, the
colour-is-caller-supplied rule, and the single icon map — not forcing Plan through `renderShape`.

---

## 11. The sketch canvas port (planned, not built)

The concrete port plan — JS module mapping (`canvas/sketch-canvas.js` extending `CanvasBase`,
`controllers/sketch-draw-controller.js` + `sketch-edit-controller.js` slotting into the §5
controller contract), the unified shape model (`geometry/shape.js`, `geometry/boolean.js`), the
MariaDB persistence model, and the finish/rasterise step — lives in `docs/tools/sketch.md`. It is
not duplicated here; this doc only supplies the controller/canvas contract (§5) and primitive-style
vocabulary (§10) the port builds on.
