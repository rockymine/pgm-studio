# Canvas interaction & shared-canvas contract

Status: **design** (the `CV` task series in `TODO.md`). This is the technical spec for five
coupled pieces of canvas work — **de-duplication, region resize, arrow-key move, the controller
pattern, and pruning/realignment** — across the **Edit** editor (`/maps/{id}/edit`) and the
**Configure** wizard (`/maps/{id}/configure`). Functionality must not degrade: every item is a
like-for-like extraction or a wiring-up of code that already exists.

Read alongside `routing-and-ia.md` (the two map surfaces) and `new-map-authoring.md` (the wizard
phases). Coordinate/transform math lives in `transform.js`; C# geometry consolidation is its own
track (`A4`).

---

## 0. The one fact that frames everything

The Configure wizard does **not** have its own canvas. Every built phase mounts the **same
`EditorCanvas`** the Edit page uses, via `studio.mountCanvas` → `studio-canvas.js`:

| Phase | Mount | Mode |
|---|---|---|
| WorldScan | `<EditorCanvas/>` (`WorldScanPhase.razor:39`) | read-only + Blocks toggle |
| WorldIslands | `IslandSelect="true"` (`WorldIslandsPhase.razor:53`) | island pick |
| WorldSymmetry | `SymmetryMode="true"` (`WorldSymmetryPhase.razor:56`) | axis overlay |
| Teams | `IslandSelect="true"` (`TeamsPhase.razor:97`) | island pick + team tint |
| Spawn | `PointPick="true"` (`SpawnPhase.razor:39`) | point-drop + marker pick |

Consequence: **wiring a capability into `EditorCanvas` + its bridge makes it available to both
surfaces at once.** Resize and arrow-key move (below) must reach the draw steps `N02` (spawn
protection), `N03` (build), `N04` (wool rooms) — those phases will mount `<EditorCanvas
DrawCategory="…">`, so they inherit the shared mechanism for free.

The `if`/mode branches that configure each canvas + toolbar per activity (e.g. Blocks toggle hidden
when `IslandSelect || SymmetryMode || PointPick`, `EditorCanvas.razor:46`) are **intentional
configuration, not duplication** — leave them.

---

## 1. Current architecture (what exists, verified)

```
CanvasBase (canvas-base.js)          pan/zoom/drag FSM + Template-Method hooks (_on*)
   ▲ extends
EditorCanvas (editor-canvas.js)      SVG render engine: layers, hit-tests, overlay, resize,
   │ composes                        island/symmetry/spawn modes, Blazor callbacks
   └─ EditorDrawController (editor-draw-controller.js)   rect/cuboid drag + cyl/circle 2-click

transform.js        pure world↔SVG math + SVG-element + path helpers (no state)
shared/shape-render.js   renderShape(type, boundsOrPoly, toSvg, attrs)
shared/converters.js     bounds/symmetry/raster math (incl. _pointInRing, applySymmetry)
shared/game-colors.js    palettes;  shared/block-render.js  blockDataToDataUrl

studio-canvas.js    the bridge: mount() → new EditorCanvas + a `handle` object C# calls
EditorCanvas.razor(.cs)   the Blazor host: parameters, [JSInvokable] callbacks, toolbar UI

# Fixed-fit preview renderers — standalone by design, do NOT extend CanvasBase, no pan/zoom:
ConfigureRenderer (configure-renderer.js)   legacy in-editor Configure + /maps/new scan preview
OverviewRenderer  (overview-renderer.js)     the overview activity
# (S2, not yet ported: SketchLayoutCanvas [extends CanvasBase] + SketchSetupCanvas [standalone])
```

`CanvasBase` provides `_scale/_panX/_panY/_viewportG/_activeTool`, wheel zoom, middle/left-drag pan,
a 4px click-vs-drag dead-zone, `_clientToSvg`, and the hook surface subclasses override
(`_onToolMousedown`, `_onPointerMove`, `_onToolMouseup`, `_onCanvasClick`, `_onViewportChanged`,
`_onZoom`, `_onMouseleave`, and the consume hooks `_onResizeMove`/`_onResizeUp` which return `true`
to intercept an event before pan logic runs).

---

## 2. Hit-testing — keep all three pickers

`EditorCanvas` has three pickers; they are **not** redundant because they pick over different data
and return different things:

| Picker | Reads | Returns | Geometry | Used by |
|---|---|---|---|---|
| `#hitTest` (`editor-canvas.js:563`) | `#nodeMap` (regions) | node | smallest-area **AABB** containment | region select (Edit; draw steps) |
| `#hitTestIsland` (`:660`) | `#ctx.islands` | island id | true **point-in-polygon** (`#pointInRing`) | WorldIslands / Teams |
| `#hitTestSpawn` (`:757`) | `#authorSpawns` (markers) | team id | nearest within ~2 blocks | Spawn |

`#hitTestSpawn` cannot be folded into `#hitTest`: spawn markers live in the `#authorSpawns` array,
not in `#nodeMap`, and have no `bounds`, so `#hitTest` would never see them. They differ in result
type (team id vs node) too. **Decision: keep all three**, but under the controller model (§5) each
picker belongs to the controller for its mode (region-select / island-select / spawn-pick) rather
than living as three `if` branches in `_onCanvasClick`.

Note the precision asymmetry to preserve: region picking is AABB-only (a click inside a circle's
bbox but outside its radius still selects it); island picking is exact. That is existing behaviour —
don't silently change it under this work.

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

### 5.3 Mode controllers (select / island / symmetry / point)
The three `_onCanvasClick` branches (`editor-canvas.js:173-183`) and the `_onToolMousedown`
point-pick branch are mode logic. Fold each into a small controller (region-select owns `#hitTest`;
island-select owns `#hitTestIsland`; spawn-pick owns `#hitTestSpawn`) so adding a mode no longer
means adding an `if`. This is the broader "controller pattern" investigation — lower urgency than
resize, but it is the abstraction the **S2 sketch port** needs anyway: `SketchDrawController` and
`SketchEditController` slot into the same contract, so establishing it now means S2 plugs in instead
of bolting on.

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

### 6.3 C# geometry (tracked under A4, not here)
`SpawnPhase` `PointInRing` (`SpawnPhase.razor.cs:136`) and `Orbit/Rotate/Reflect` (`:148-167`)
duplicate existing backend helpers (`Pgm/Editing/Geometry2d.ReflectPoint`/`RotatePoint`,
`Editing/SymmetryExpander`) and the JS `converters.applySymmetry`. This belongs to the **A4**
geometry-consolidation task (which already audits 5 C# sites) — route `SpawnPhase` through the one
geometry module and move `PointInRing` into it. The symmetry **label** (`SymLabel`, byte-identical in
`WorldScanPhase`/`WorldSymmetryPhase`) and **team-count** mapping (repeated in
`WorldSymmetryPhase`/`TeamsPhase`/`SpawnPhase`) should collapse into one shared C# `SymmetryInfo`
helper — small, low-risk, do alongside A4.

---

## 7. Pruning / realignment

`EditorCanvas` exposes a large public surface the bridge never forwards — but per the resize/move
work above, much of it is **essential-UX-not-yet-wired**, not dead. Triage:

- **Wire (the resize/move/selection chain):** `showAnchors`, `clearAnchors`, `updateRegionBounds`,
  `refreshRegionBounds` — needed by §3/§4. Add a `moveSelected` helper (§4).
- **Keep, evaluate per feature as its UI lands:** `addRegion`, `removeRegion`, `renameNode`,
  `setRegionVisible`, `setBuildVisible`, `setResolvedMode`, `setPoisVisible`, `focusRegion`,
  `refreshRegions` — these back inspector edit / visibility / focus features that are still on the
  board (e.g. `C11`). Don't delete; wire when their feature is built.
- **Realign the stale doc header** (`editor-canvas.js:5-18`) to match the actual exposed surface
  once §3/§7 land.
- **`#hitTestSpawn` decision:** keep (§2); move into the spawn-pick controller under §5.3.

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
