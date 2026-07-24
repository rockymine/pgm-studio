# Sketch

Draw 2-D shapes → islands → a playable world geometry, from nothing (or from a compiled Plan). Route
`/maps/{slug}/sketch` (`SketchTool.razor`/`.cs`, `Features/Sketch/`). Sketch is one of the map lifecycle's
origination stages: **Plan → Sketch → Configure → Edit**. A map's `stage` column tracks where it sits;
Sketch's own in-progress/finished distinction isn't a separate flag — it's read from artifact presence
(§2). A plan (`/maps/{slug}/plan`) can compile straight into a sketch draft (`PlanCompiler.Compile(plan) →
SketchLayout`, `docs/tools/generate.md`) as an alternative starting point to a blank canvas.

Finishing a sketch (rasterize) turns the drawn layout into the same geometry artifacts a scanned/imported
world produces, so a finished sketch drops straight into the existing `/maps/{slug}/configure` flow with
zero new consumer code on that side.

---

## 1. Data model & persistence

**A sketch is a draft map, not a separate entity.** Creating one creates a `map` row (identity only, no
geometry yet); the drawn layout persists as a `SketchLayoutJson` `map_artifact` on that map — mirroring how
`MapIntentJson` backs the Configure wizard:

| | source artifact | projection step | output |
|---|---|---|---|
| **Configure** | `MapIntentJson` | `IntentGenerator` | regions/teams/wools (the `map.xml` config) |
| **Sketch** | `SketchLayoutJson` | rasterize | `LayerParquet` + `IslandsJson` + `symmetry` + `layer_segment` (world geometry) |

**Map state is read from artifact presence** (no status column): a sketch-in-progress has a
`SketchLayoutJson` artifact and no `LayerParquet`; a finished sketch has `LayerParquet`
(`FeatureData.HasLayer` gates on it). `SketchStore` (`SketchEndpoints.cs`) mirrors `IntentStore` — it
lives outside the entity-replace codec so it survives `MapWriter.SaveDocAsync`. One `ArtifactKind` constant
`SketchLayoutJson`, no migration (kinds are string discriminators on `map_artifact`).

**Endpoints** (FastEndpoints, map-scoped):

| Method · route | Action |
|---|---|
| `POST /api/sketch` | create a draft `map` row + seed the layout artifact (`{name}`, optional working frame — see §3); returns `{slug}` |
| `GET  /api/map/{slug}/sketch` | the stored layout blob (or `{}`) |
| `PUT  /api/map/{slug}/sketch` | replace the whole layout blob (debounced 800 ms client-side + flush on dispose) |
| `POST /api/map/{slug}/sketch/finish` | rasterize (§4) → geometry artifacts; 422 if `< 2` islands |
| `DELETE /api/map/{slug}/sketch/discard-if-empty` | delete a still-pristine draft (default name, no authors, no shapes) on tool dispose |

The per-step `setup`/`layout`/`overview` PATCH split originally designed collapsed early to one `PUT` of
the whole `{setup, layout}` blob — the bridge's `getState()` already carries both, so one round-trip beat
three.

**Persistence shape** — stored verbatim as written by the browser (JS-origin camelCase — `shapeIds`,
`cx`/`cz`, bezier `in`/`out`); the C# DTOs (`SketchModels.cs`, `PgmStudio.Pgm.Sketch/SketchLayout.cs`) keep
those keys via `JsonPropertyName` as a transport/validation shape, not snake_case-normalised (authoring
source, not the canonical `xml_data.json`):

```jsonc
{
  "setup":  { "bbox": {min_x,min_z,max_x,max_z}, "center": {cx,cz}, "mirror_mode": "rot_180" },
  "layers": [                                   // ordered stack, §4 "Stacked layers" — S7
    { "id", "name", "base_y": 0, "layout": {
        "shapes": [ /* Shape[] */ ],
        "islands": [ { "id", "name", "mirrors": true, "shapeIds": [..] } ]  // user-set meta only
    } }
  ]
}
```

A legacy single `layout: {shapes, islands}` (pre-S7) loads as one layer at `base_y = 0` — read-both, no
hard migration. Identity (name, authors) lives on the `map` row / author table via the map-metadata
endpoint, not in this artifact.

`Shape` fields vary by `type`:
- `rectangle`: `min_x/min_z/max_x/max_z`
- `circle`: `center_x/center_z/radius`
- `polygon` | `lasso`: `vertices: [[x,z],…]`, optional `controls`
- all: `operation` (`"add"|"subtract"`), `override: bool`, `id`, plus the height fields from §4
  (`base_height`, `anchor_heights`, `floor`)

**Bézier control model** (lock-step with `render/svg.js ringToPath` + the rasterizer): `controls` is a dict
keyed by **stringified vertex index** (`"0"`, `"1"`, …), each `{ in?: [x,z], out?: [x,z] }`. For edge *i→j*
the cubic is `(p_i, controls[i].out, controls[j].in, p_j)`; a missing handle falls back to its endpoint; an
edge with neither is a straight segment. (C# can't name a field `in` — `[JsonPropertyName("in")] In`.)

Island **geometry is not stored** — only metadata (`name`/`mirrors`/`shapeIds`); geometry is recomputed
from `shapes` on load (live, JS `geometry/boolean.js computeIslands`) and at finish (server). Saved metas
re-attach to recomputed islands by centroid proximity (live) / shape-id-set overlap (finish).

**Parity constants** kept in lock-step between the JS rasterizer/geometry and the C# side: circle = 64-gon,
Bézier = 16 samples/edge, the 4-step add/subtract/override order, the stringified-index control dict,
`rot_270 = (Δx,Δz)→(Δz,−Δx)`.

---

## 2. JS ⇄ C# split

Pure geometry lives in `geometry/shape.js` (`toRing`/`toBounds`/`containsPoint`/`circleToRing`/
`sampleBezier`/`centroid`) and `geometry/boolean.js` (`computeIslands`/`assignShapesToIslands`/
`computeMirrorPreview`/`restoreIslandMeta`, over `polygon-clipping` vendored as a self-contained ESM bundle
at `vendor/polygon-clipping.js` — esbuild-inlined `splaytree` + `robust-predicates`, imported relatively so
it loads with no importmap/bundler in the hosted-WASM app and its tests still run in the plain
`node --test` harness).

Canvas + controllers: `canvas/sketch-canvas.js` (extends `CanvasBase`; **world coords are svg base
coords** — identity transform, no `buildTransform`, unlike `EditorCanvas`), `controllers/
sketch-draw-controller.js` (rect drag / circle 2-click / polygon click+close / lasso drag-trace),
`controllers/sketch-edit-controller.js` (resize / vertex drag / Bézier tangent handles / midpoint-insert).
`bridge/sketch-bridge.js` is the JS activity Blazor drives (`mount()`, shape list, island recompute loop,
edit commands, `OnLayout`/`OnShapeSelected`/`OnIslandSelected`/`OnDirty`/`OnToolChanged`).

**Live boolean in JS, authoritative rasterize in C#**: the live island preview runs `boolean.js` in the
browser (hot path); the server rasterizes from `shapes` for the persisted geometry and re-detects islands
with the existing `IslandDetector` — no C# polygon-boolean dependency. The editor's region hit-test stays
AABB+margin (forgiving marquee select); the sketch tool uses `shape.containsPoint` (true per-type,
including point-in-polygon for lasso/polygon) — two different needs over the same primitive, kept
separately by design.

---

## 3. Creation

**Current flow: no dedicated creation page.** "New sketch" (`Maps.razor.cs NewSketch()`) posts
`{name: "Untitled sketch"}` to `POST /api/sketch` and opens the tool directly at
`/maps/{slug}/sketch?phase=info` — the Sketch tool has no separate creation page because **the canvas
auto-grows to whatever you draw**, so there's no size to pick up front. `SketchTool` is a phase host (like
Configure/Plan): an **Info** phase with **Identity** (name + username-verified authors via the shared
`AuthorsEditor`, saved through the map-metadata endpoint since a sketch is a map row) and **Settings**
(symmetry mode + centre X/Z — `SketchInfoPhase.razor`, step 2: *"The mirror used for the live preview
while you draw. The map area itself grows to fit what you draw — there's no fixed size to set."*), and a
**Draw** phase (the canvas, kept mounted/hidden across phase switches so zoom/pan survive the trip). A
freshly created draft lands on Info to be named; opening an existing sketch goes straight to Draw. A
draft abandoned still pristine (default name, no authors, no shapes) is auto-discarded
(`DELETE .../sketch/discard-if-empty`, called on tool dispose) so an idle click doesn't litter the
dashboard.

`POST /api/sketch` still accepts an optional working frame (`{width, depth, mode, centerX, centerZ}`) and,
when given, seeds the artifact's `setup` bbox/centre/mode from it (defaulting missing fields to the
landscape frame, 120×80 / `rot_180`, origin-centred) — this exists for callers that want to pre-seed a
symmetry frame, but the production "New sketch" entry point no longer sends one.

**Superseded design, for context.** Earlier iterations of this flow: a dedicated full-screen
`/maps/new-sketch` page authoring footprint + symmetry at creation time (`S11`), and a non-square,
**preset-driven working size** — 2-team landscape 120×80 default / portrait 80×120 / square 120×120 for
4-team-D2 / custom (`S3`). Both shipped and were later superseded when footprint/size presets
were dropped in favour of the auto-growing canvas (bounds = drawn content + a one-chunk buffer, min 64×64,
snapped to chunk lines, the same model the Plan editor uses) — there is no fixed frame to author at
creation time or in the sidebar any more. The live on-canvas dimension readout (`canvas-dim` — active
draw's `W×D`, or a selected shape's extent) from the same `S3` work is still current; only the
preset-picker part of that design is gone. Also gone: generating a starter layout from an archetype
(`POST /api/sketch/generate`, the "Generated layout" tab) — the archetype generators were retired in
favour of the plan-then-realize direction (`docs/tools/generate.md`); a Plan is now the way to
seed a non-blank starting layout, compiled in rather than generated inline.

---

## 4. Editing capabilities

The Draw phase toolbar: **tools** (move/pan, select/edit, rectangle, circle, polygon, lasso, measure,
split — one `draw-tool-btn` radio group) and **operation** (add/subtract, styled as two more buttons in
the same strip) plus independent view toggles (mirror, shapes, chunks, snap, 3D).

**Select / drag / rotate / scale / split** (the "depth pass" that shipped on top of the original S2 draw
tool):

- **Body-drag move** (`CV10`) — a `CanvasBase` seam (`_hitMovable`/`_moveBy`/`_commitMove`) drags a
  selected shape's body, alongside arrow-nudge; block-snapped, with a click/drag threshold. The same seam
  backs Edit's region drag — no duplicated translate logic (`geometry/shape.js translateShape` /
  `translateBounds`).
- **Alignment snapping / smart guides** (`S9`) — while dragging, a shape's bbox edges + centre snap to
  other shapes' edges/centres and the symmetry centre, with dashed guide lines; a **Snap** toggle disables
  it, **Alt** bypasses per-drag. Also fires on the 8-handle rectangle resize (`S19`,
  `SketchEditController.onResizeMove` → `snapEdges`). **Position alignment only** — angle/parallel snapping
  and manually-droppable guide lines are parked (`S9b`, §6).
- **Figma-style island/shape selection** (`S20`) — single-click selects the *containing island* (bbox +
  corner anchors); double-click drills into the member shape under the cursor (its own resize/vertex
  handles); Esc pops back out. A single-primitive island shows the shape's own handles at the island level
  too. The whole island body-drags together via the same `CanvasBase` move seam extended to a multi-shape
  handle.
- **Rotate an island** (`S13`) — four rotate zones just outside the bbox corners; the angle is the
  cursor's swept angle around the bbox centre — distance-independent, relative to grab, unwrapped past
  360°, **Shift snaps to 15°**. A numeric Rotate(°) inspector field applies the same rotate-by. Pure
  `rotateShape(shape, angleRad, pivot)` **bakes** the rotation into geometry (polygon/lasso rotate
  vertices + Bézier controls; a circle's centre orbits, radius kept; a rectangle promotes to a polygon
  first, carrying its height fields).
- **Squash / scale an island** (`S21`) — 8 scale handles (4 corners + 4 edge midpoints) on the bbox: an
  edge stretches/squashes one axis, a corner scales both, anchored on the opposite edge/corner; **Shift**
  locks to a uniform scale, **Alt** scales about the centre; clamped against collapse/flip. `scaleShape`
  bakes it in — a rectangle stays axis-aligned, a circle stays round (radius scaled by the geometric mean,
  no ellipse type), polygon/lasso scale vertices + Bézier controls.
- **Split tool** (`S14`) — a scissors tool: two clicks draw a slice line (rubber-band preview, Esc
  cancels); the shape the segment crosses is cut into two polygons in place. Reuses the plan decompose
  cutter's `splitPiece` to arc-split the ring. A rectangle promotes first; circles are unsupported. Both
  halves keep operation/override/`base_height`/`floor`; Bézier controls and per-vertex `anchor_heights`
  are dropped on a cut.
- **Selection outline highlight** (`S22`) — the selected shape's outline (or, for a multi-shape island,
  the island's exterior + holes) glows accent in an always-visible overlay layer, independent of the
  **Shapes** toggle.

**Rectangles are polygons too — promotion** (`S4`/`S15`): rectangle stays the creation preset and
axis-aligned fast path (drag-create, 8-handle resize), but a one-way **Convert to polygon** action (inspector
button + `P` shortcut) turns it into a 4-corner polygon — id/operation/override *and* the height fields
(`base_height`/`floor`/`anchor_heights`) carry over, so a promoted box keeps its column instead of resetting
to the 1-block default. From then on it's an ordinary polygon: vertex drag, midpoint insert, Bézier, and
per-anchor height all apply with no rasterizer special case (`RingOf` already maps `polygon` directly).
Auto-promotion also fires on any edit a rectangle can't represent (midpoint insert, an off-axis corner
drag, a Bézier handle, non-uniform per-anchor heights).

**Height — per-shape and per-anchor** (`S5`, redefined `S17`): a shape carries `base_height` (thickness),
`anchor_heights[]` (per-vertex thickness, index-aligned to `Vertices`), and `floor` (elevation — where the
base sits). **Floor is elevation, Height is thickness**: a column spans `[floor, floor + height]`
(`top = base_y + floor + height` once layers are stacked, §4 below) — this is the current, intuitive
reading; an earlier version had `floor` as the absolute bottom-Y and `base_height` as an absolute top-Y,
which read like a second height field, and was corrected in `S17`. Heights are invariant under
reflection/rotation — the mirror/orbit path carries them through unchanged, only X/Z transforms. Per-shape
height alone gives a flat slab from `floor` up `base_height` blocks; per-anchor height gives a
sloped/varied top — the rasterizer TIN-interpolates it (`Geom.Triangulation` ear-clip + barycentric,
resolving the shape/IDW-vs-TIN open question in the original design in favour of TIN) for polygons whose
`anchor_heights` match their vertices; a rectangle's 4 corners use the same path once promoted.
`SketchRasterizer.RasterizeColumns` carries the pipeline from `(X, Z)` to `(X, Z, YFloor, YTop)`; the
4-step add/subtract/override set algebra still keys cells by `(x,z)`, with the taller `add` winning on
overlap and height riding along as a property of the surviving cell. `WriteSketchAsync` writes the real
span to `layer_segment` (read by Configure's height side-view, `SliceView`) and the surface block at
`YTop`.

**Per-vertex height editing** (`S5b`): with a polygon selected, click a vertex to set its height (inspector
*Vertex N height* field, writes `anchor_heights[]`); every vertex shows its current height as a label on
the canvas, the selected one highlighted. Click-vs-drag is split by a movement threshold in
`sketch-edit-controller.js` (click-without-move sets height; dragging moves the vertex).

**3-D preview — bespoke WebGL renderer** (`S6`, sloped solids `S5c`): a read-only, GPU-depth-buffered
isometric view (`render/iso-webgl.js`) that swaps in for the top-down SVG canvas via a **3D** toggle (the
SVG hides its viewport and ignores tool input while active); a button rotates yaw. It consumes the
per-shape "solids" the bridge builds (one prism/terrain per shape + rotation/mirror copies): flat shapes
are a prism (footprint extruded floor→top), per-anchor shapes are a TIN-draped sloped top with walls
following the vertex heights, all lit by a fixed orthographic camera with key/fill/ambient (flat-Lambert)
lighting over a y=0 ground-plane reference.

*Why a real renderer, replacing the earlier SVG extrusion*: the original SVG approach was a painter's
algorithm — solids sorted by a single screen-depth key and drawn opaque — which could not resolve mutual
occlusion between overlapping/neighbouring masses, and whose depth key didn't commute with the `rot_180`
mirror, so the two mirrored halves occluded inconsistently. That recurred through several point fixes
(per-shape heights, overlap clipping, per-face sort) without actually being fixable; a GPU depth buffer
resolves occlusion per-fragment, correctly and symmetrically, by construction.

*Why hand-written, not a 3-D library*: the render need is narrow (a fixed orthographic camera, flat Lambert
shading, a depth buffer over a few dozen prisms) — the actual fix was one `gl.enable(DEPTH_TEST)`, not a
scene graph. It's one shader program, a small column-major mat4 helper, and triangle-soup geometry from the
in-repo `earClip` triangulator (the JS twin of `Geom.Triangulation`) — no vendored dependency, fitting the
firewalled no-bundler hosted-WASM stack without shipping a megabyte-plus library for a read-only preview.
Not built: free orbit/elevation camera control (the camera already supports it structurally; only yaw is
wired) and textured/coloured materials per team.

**Stacked layers** (`S7` rasterization, `S7b` editor): `SketchLayout` wraps `layout` in an ordered
`layers[]`, each `{ id, name, base_y, layout }` (§1); a legacy single-layer sketch loads as one layer at
`base_y = 0`. `SketchRasterizer.RasterizeColumns` rasterizes each layer in its own Y (with its own per-layer
island mirror), shifts its columns by `base_y`, and concatenates — a column spanning multiple layers keeps
**separate segments** (ground + a sky bridge, the gap between them preserved), which `WriteSketchAsync`
writes to `layer_segment` per segment plus the surface row at each column's max top. The `SketchLayers`
panel supports add/select(active)/delete and each layer's name + Base Y; the active layer edits normally
while other layers render **ghosted** (faint dashed outlines, `renderGhostIslands`); the iso 3-D preview
stacks every layer by `base_y` (a block floating 30 above ground reads as a sky platform).

**Shape library — drag-in primitives** (`S8`): a sidebar palette of pure-geometry primitives, deliberately
**no gameplay semantics and no corpus-derived shapes** (corpus shapes were explicitly ruled out — too much
variation, and they bias toward "another map like X"). Catalog (`geometry/shape-library.js`):
- **n-gons** {3, 5, 6, 8} (triangle/pentagon/hexagon/octagon; no square — that's the rectangle tool; no 7),
  generated parametrically at drop time.
- **Polyominoes**: L, U, T, I-bar, scythe, cross (+), line-with-branch — rectilinear axis-aligned templates
  on a small integer cell grid, each a single polygon ring.
- **One composite**: hole-square — an `add` rectangle + a centred `subtract` rectangle (a frame/ring); the
  only multi-shape entry, demonstrating the add+sub group.

A library entry is **a source of ordinary `SketchShape`s, not a new shape type** — on drop it instantiates
plain polygons (centred + block-snapped at a default cell size), so the rasterizer, island detection,
height, mirror/orbit, and layers all just work with zero downstream code (same principle as the
rectangle→polygon promotion). Interaction is **click a thumbnail to arm → click the canvas to place** (a
ghost preview follows the cursor; Esc cancels) — the design originally specced drag-from-thumbnail, but
click-to-arm is what shipped. Off the agenda by decision: rounded rectangles, gameplay presets (spawn/lane
templates), corpus-derived shapes, and user-saved custom templates.

---

## 5. World export

Sketch-originated maps have no real voxel world — their "world" is the synthetic column geometry from
`SketchRasterizer.RasterizeColumns`. `GET /api/map/{slug}/export` (`SketchWorldBuilder`, behind the shared
traversability gate / `MapXmlComposer`) assembles a real Anvil world from that geometry + the authored
`MapIntent` and returns a ZIP of a single `{slug}/` folder (`map.xml`, `level.dat`, `region/r.<x>.<z>.mca`)
for sketch-origin maps; normal Configure-imported maps (which already ship a real world) still export XML
only. Sketch origin is detected by the durable presence of the `sketch_layout_json` artifact (it survives
the stage advancing to `configure` on finish). **Anvil format is the 1.8–1.12 numeric block format**
(`Blocks`/`Data`/`Add` nibble-packed sections) the read-only `AnvilRegion` already understands;
`AnvilRegionWriter` + `LevelDatWriter` (`PgmStudio.Minecraft`) are the write-side mirror, round-trip tested.
The wizard's manual Monuments sub-step is dropped entirely for sketch-origin maps — monument placement is
fully derived, not authored (see below).

**Terrain** (`SketchTerrainBuilder`): from each solid `(X, Z, YFloor, YTop)` column — `y = 0` is bedrock,
the solid span above is stone. Flat materials for now; structures are stamped on top of existing terrain,
never embedded or floating.

**Cube template** (`CubeStamper`, shared by the wool cage and the spawn cube — a hollow 8×8×8 bedrock
shell, layers numbered from the floor): roof (layer 8) has a 4×4 centre hole; layer 6 is a true gap (light
slit, not glass); layer 4 is the colour strip — **wool** for the room colour on the wool cage, **stained
clay** for the team colour on the spawn cube (no stained clay on the wool cage); the floor (layer 0) has a
2×2 centre wool marker (wool-spawn point / player spawn); doors begin at layer 1 — the wool cage has **four**
doors, one per wall, 2-wide × 3-tall stained-glass panes (id 160) in the room's wool colour; the spawn cube
has a **single** 4×4 open-air door on one wall. `WoolCageStamper` + `WoolCageChests` add two stacked chests
per interior corner (Chest A: planks / Speed I potions / golden apples; Chest B: diamond leggings / Power+
Infinity bows / planks) — the spawn cube has no chests.

**Wool monuments** (`SpawnCubeStamper`) sit in the spawn cube's corners and are **fully auto-wired** — the
exporter derives every monument from the wool set + cube geometry, never from a freely-authored
`MonumentIntent.Location`: bedrock pedestal elevated one block off the cube floor → an air placement cell
above it (wool goes here) → a stained-glass cap in that wool's colour → a sign against the pedestal.
Placement by captured-wool count per team: 1–2 wools go in the corners against the door wall; 3–4 go in
the other corners against the back wall; 5+ (rare) fill the back wall. Sign label is always 4 lines
(`Place the` / bold colour name / `Wool` / `here!`).

**Observer platform** (`ObserverPlatformStamper`, standalone, not a cube): a solid 6×6 bedrock platform
placed at the observer's floating authored Y (**not** terrain-snapped, unlike everything else). Each of
its four edges gets an identical inward-facing info board (a 1-tall × 2-wide bedrock wall + a 2-sign pair):
the left sign is the map name (`[CTW]` tag + bold name), the right sign is the author list.

**`level.dat`** (`LevelDatWriter`): gzipped NBT `Data` compound — world spawn at the observer/default
spawn, flat generator, `LevelName = {slug}`, 1.8–1.12 version tags, and a correct (non-degenerate) creation
timestamp.

**Placement anchoring & coordinate constraints** (`PositionSnap`) — this section's structure synthesis
applies only to sketch-finish-originated maps, never to normal imports:
- The authored spawn/wool positions anchor the structures (move the point, move the structure) — the spawn
  cube centres on the player spawn, each wool cage on its wool spawn point.
- X/Z snap to full integers (the 2×2 centre needs a whole-block midpoint, not Minecraft's `.5` block
  centre).
- Y snaps to the column top (`ymax` from the `layer_segment` blob) — structures sit on terrain surface,
  never embedded or floating.
- Spawn yaw and the door wall are derived together so the player spawns facing out through the door;
  monuments then sit against that door wall / the back wall per the placement rule above.

`SketchWorldBuilder` returns a **resolved intent** (integer-snapped spawns + monument locations derived
from the world's actual air cells, capturers defaulted to every non-owner team) alongside the world, so the
exported `map.xml` agrees with the exported world exactly.

---

## 6. Known gaps / open issues

**Open, tracked in `BACKLOG.md`:**

- **`S9b` — angle/parallel snapping + droppable guide lines.** `S9` landed *position* alignment (edges/
  centres snap to other shapes + the symmetry centre, with guides). Still missing: **angle/parallel**
  snapping (rotate a shape so its edges run parallel to another's — e.g. holding two lanes truly parallel),
  and **manually droppable** guide lines shapes snap to (today's guides are auto-derived from other shapes
  only). Both parked as their own piece of work.
- **`S12` — pin the Islands tree to the top of the sidebar.** The original Setup block is gone (superseded
  by the auto-grow model, §3), which resolved part of the original sidebar-overload complaint, but the
  **Layers** panel and the **Library** palette still render above **Islands** in the Draw sidebar
  (`SketchTool.razor`) with no collapse — confirmed still the current order. Fix: collapse both behind
  `<details>` accordions (Library default-collapsed once the map has shapes), or move the Library to a
  toolbar popover (it's a "reach for a primitive" action, not persistent state).

**From the outside-eye UX review done after the depth pass shipped — still open (no evidence of a fix
beyond the above):**

- **Add/Subtract legibility (P0).** The operation toggle is still two more `draw-tool-btn`-styled buttons
  in the same strip as the seven tools — visually a peer, not a distinct highest-stakes state, with no
  on-canvas/cursor signal of which mode you're in before you draw.
- **Three orthogonal state axes, one flat button row (P1).** Tool mode (radio), operation (2-state), and
  the view toggles (mirror/shapes/chunks/snap/3D) are still one undifferentiated strip; `3D` in particular
  is a chip, not a segmented 2D/3D view control, even though toggling it suppresses all tool input.
- **Measure tool doesn't measure what it's for (P1).** The tool tooltip still reads "drag across a void gap
  to read its length in blocks" — a free-drag ruler, not the specced "shortest distance between two island
  bodies" with endpoints snapped to island/shape edges. (A later change, `S18`, moved the distance readout
  onto the ruler line itself for legibility, but that only changed *where the number renders*, not the
  underlying measurement.)
- **Height editing is form-only, no direct manipulation (P1).** Height/Floor are still plain number fields;
  there's no drag/scrub affordance, and the 3-D preview is still a modal swap (toggling it hides the 2-D
  canvas and ignores tool input) rather than a live companion view.
- **Convert-to-polygon has inconsistent entry points (P2).** Still reachable via the inspector button, the
  `P` shortcut, and silent auto-promotion on an edit a rectangle can't represent — no confirmation/toast on
  auto-promote.
- **No central keyboard-shortcut surface (P2).** Shortcuts (Esc, Delete, `P`, arrows/Shift-arrows,
  Ctrl-drag for Bézier, Alt for no-snap, double-click to close) remain scattered across
  `sketch-canvas.js`/`sketch-bridge.js`/`sketch-edit-controller.js` with no discoverable listing.
- **Library palette density and hidden click-to-arm interaction (P2).** Confirmed still click-to-arm →
  click-to-place (§4) rather than the originally specced drag-from-thumbnail, and the toolbar shows no
  persistent "placing: X" indicator while armed.

**May already be (partially) addressed by later work — re-check the live UI before treating these as
still-open:**

- **Per-vertex height discoverability (P1).** The review predates `S5b`'s shipped per-vertex height
  editing, which renders an **always-visible** height label at every vertex (not just when one is
  selected) — this may already soften the "undiscoverable, gated behind a conditional inspector sentence"
  complaint, but the click-vs-drag ambiguity on the vertex handle itself (the review's specific concern) is
  unverified either way.
- **"Coordinate soup" of Floor / Base Y / layer stacking (P2).** `S17` explicitly redefined the column model
  to the intuitive Floor = elevation / Height = thickness split, which was largely the semantic confusion
  the review flagged. Whether the relationship between per-shape Floor and per-layer Base Y is now
  explained anywhere in the UI (vs. just being less conceptually muddled) is unverified.
- **Shape vs. island selection "silently clearing" each other (P2).** The review describes a model where
  selecting a shape nulls the island selection and vice versa, with no visible link between them. The
  later Figma-style selection model (`S20` + `S13`/`S21`) is a different interaction entirely — single-click
  selects the containing island, double-click drills into a member shape, Esc pops back out — so the
  original complaint may no longer apply in the form described; worth re-reviewing under the current model
  rather than assuming either way.
