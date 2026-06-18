# Sketch authoring & persistence (S2)

Status: **plan** (task `S2` in `TODO.md`). The design for porting the reference's **Sketch tool**
(draw 2-D shapes → islands → a playable world geometry, from nothing) into pgm-studio: the JS onto the
new layered canvas structure (`canvas-interaction.md` §1), and how sketches **persist in MariaDB**.

Read alongside `routing-and-ia.md` (Sketch → Configure → Edit; Sketch originates a map) and
`new-map-authoring.md` (the intent flow this is symmetric with). Reference (the behavioural oracle):
`pgm-map-studio/.../studio/static/{canvas/sketch-*,sketch/geometry.js,shared/tool-manager.js}`,
`studio/routes/sketch_api.py`, `studio/services/{sketch_data,sketch_export}.py`, `schemas/sketch.py`.

---

## 0. The one decision that frames everything: a sketch **is a draft map**

The reference keeps sketches in a parallel store (`~/.config/.../sketches/<uuid>/sketch.json`) and
*exports* to a separate editor map. pgm-studio is **map-centric** (everything is `/maps/{id}/…`; the
authoring intent is a `map_artifact` keyed by `map_id`). So we do **not** add a separate sketch
entity. Instead:

> **Creating a sketch creates a `map` row** (identity only, no geometry yet). The drawn layout
> persists as a **`SketchLayoutJson` `map_artifact`** on that map — exactly mirroring how the
> declarative `MapIntentJson` artifact backs the Configure wizard. "Finishing" the sketch rasterizes
> the layout into the same geometry artifacts the importer writes (`LayerParquet`, `IslandsJson`,
> `layer_segment`, `symmetry`), so the sketched map drops straight into the existing
> `/maps/{slug}/configure` flow.

This makes Sketch and Configure **symmetric**: both are *an authoring source persisted as a
map_artifact, projected into editor-consumable form on demand*.

| | source artifact | projection step | output |
|---|---|---|---|
| **Configure** | `MapIntentJson` | `IntentGenerator` | regions/teams/wools (the `map.xml` config) |
| **Sketch** | `SketchLayoutJson` | rasterize (§4) | `LayerParquet` + `IslandsJson` + `symmetry` + `layer_segment` (the world geometry) |

**Map state is read from artifact presence** (no new status column): a sketch-in-progress is a map with
a `SketchLayoutJson` artifact and **no** `LayerParquet`; a finished sketch has `LayerParquet`
(`FeatureData.HasLayer` already gates on it). The maps dashboard lists in-progress sketches as drafts.

---

## 1. Lifecycle (mirrors the reference, map-scoped)

```
/maps/new ─ Sketch ─▶ create draft map ─▶ Setup ─▶ Layout (draw) ─▶ Overview ─▶ Finish
                         (map row)        bbox/    shapes+islands    name/...    rasterize→geometry
                                          center/                                 artifacts
                                          mirror                                     │
                                                                                     ▼
                                                              /maps/{slug}/configure (existing flow)
```

Endpoints (FastEndpoints, map-scoped to fit the IA — the reference's flat `/api/sketch` list becomes
"draft maps" on the dashboard, so no separate list endpoint):

| Method · route | Body | Action |
|---|---|---|
| `POST /api/sketch` | `{name?, gamemode?}` | create a draft `map` row (+ empty `SketchLayoutJson`); return `{slug}` |
| `GET  /api/map/{slug}/sketch` | — | map identity + the `SketchLayoutJson` (the `SketchProject` shape) |
| `PATCH /api/map/{slug}/sketch/setup` | `{bbox, center, mirror_mode}` | merge into the layout artifact's `setup` |
| `PATCH /api/map/{slug}/sketch/layout` | `{shapes, islands}` | replace the artifact's `layout` |
| `PATCH /api/map/{slug}/sketch/overview` | `{name, version, objective, authors}` | write `map` columns + `author` rows |
| `POST /api/map/{slug}/sketch/finish` | — | rasterize (§4) → geometry artifacts; 422 if `< 2` islands |

Each PATCH **validates then persists the partial payload** (the reference's `_reject` gate — validate
against the schema, store the raw partial so PATCH stays partial). Setup/layout live in the artifact;
overview maps to real `map`/`author` columns (identity is queryable there already).

---

## 2. Persistence shape — `SketchLayoutJson` (the wire/stored contract)

Stored verbatim as written by the browser (JS-origin **camelCase**, like the reference's `sketch.json`
— `shapeIds`, `cx`/`cz`, bezier `in`/`out`). The C# DTO keeps those keys via `JsonPropertyName`; it is
a transport/validation shape, **not** snake_case-normalised (the artifact is authoring source, not the
canonical `xml_data.json`). One new `ArtifactKind` constant `SketchLayoutJson` — **no migration**
(kinds are string discriminators on the existing `map_artifact` table).

```jsonc
// SketchLayoutJson artifact (one per draft map)
{
  "setup":  { "bbox": {min_x,min_z,max_x,max_z}, "center": {cx,cz}, "mirror_mode": "rot_180" },
  "layout": {
    "shapes": [ /* Shape[] */ ],
    "islands": [ { "id", "name", "mirrors": true, "shapeIds": [..] } ]   // user-set meta only
  }
}
// identity (name/version/objective/gamemode/authors) lives on the map row + author table, not here.
```

`Shape` — fields vary by `type` (exactly `schemas/sketch.py`):
- `rectangle`: `min_x/min_z/max_x/max_z`
- `circle`: `center_x/center_z/radius`
- `polygon` | `lasso`: `vertices: [[x,z],…]`, optional `controls`
- all: `operation` (`"add"|"subtract"`), `override: bool`, `id`

**Bézier control model (lock-step with `render/svg.js ringToPath` + the rasterizer):** `controls` is a
dict **keyed by stringified vertex index** (`"0"`,`"1"`,…), each `{ in?: [x,z], out?: [x,z] }`. For
edge *i→j* the cubic is `(p_i, controls[i].out, controls[j].in, p_j)`; a missing handle falls back to
its endpoint; an edge with neither is a straight segment. (C# can't name a field `in` — use
`[JsonPropertyName("in")] In`.)

Island geometry is **not** stored — only the metadata (`name`/`mirrors`/`shapeIds`); geometry is
recomputed from `shapes` on load (live) and at finish (server). On reload, saved metas are re-attached
to recomputed islands by **centroid proximity** (live, JS) / **shapeId-set overlap** (finish, server) —
the reference's `restoreIslandMeta` / `_match_metadata`.

---

## 3. JS port onto the layered structure

The reorg put the geometry the sketch tool needs into importable layers, and the sketch tool is the
**real consumer** that makes the deferred shape model (`canvas-interaction.md` §11) non-speculative —
build it here. Reference file → new module:

| Reference | New module (layer) | Notes |
|---|---|---|
| `sketch/geometry.js` (low half: `shapeToRing`, `circleToRing`, `sampleBezierEdge`, `pointInRing`, `ringCentroid`) | **`geometry/shape.js`** (NEW) | the unified primitive model: `toRing/toBounds/containsPoint/circleToRing/sampleBezier/centroid`. Pure, unit-tested. `pointInRing` already in `geometry/polygon.js`. |
| `sketch/geometry.js` (high half: `computeIslands`, `assignShapesToIslands`, `computeMirrorPreview`, `restoreIslandMeta`) | **`geometry/boolean.js`** (NEW) | the only genuinely sketch-domain layer; needs `polygon-clipping` (browser importmap; tests need the `/root` node_modules runner — see §6). |
| `canvas/sketch-layout-canvas.js` | **`canvas/sketch-canvas.js`** | extends `CanvasBase`; **world coords ARE svg base coords** (identity transform, no `buildTransform`) — keep that distinction from EditorCanvas. |
| `canvas/sketch-draw-controller.js` | **`controllers/sketch-draw-controller.js`** | rect (drag) / circle (2-click) / polygon (click+close) / **lasso** (drag-trace). Same controller contract as `editor-draw-controller` (`onMouseDown→bool`, …). |
| `canvas/sketch-edit-controller.js` | **`controllers/sketch-edit-controller.js`** | 8-handle rect resize + vertex drag + **Bézier tangent handles** (ctrl-drag to extrude, alt for asymmetric) + midpoint-insert. |
| `shared/tool-manager.js` | **`render/`/host toolbar** | the editor toolbar already toggles tools; reuse rather than re-add a `ToolManager`. |
| `sketch-{setup,layout,overview}-activity.js` + panels | **Blazor `Sketch*Phase.razor`** | mirror the Configure phase components; `mount()` via `bridge/sketch-bridge.js`. |
| `api.js` sketch calls | C# `JS.InvokeAsync` + the §1 endpoints | |

`render/shape-render.renderShape` already dispatches rect/ellipse/polygon-path, and `ringToPath`
already does Bézier — the sketch shapes reuse them. New: **`bridge/sketch-bridge.js`** (`mount()` →
handle Blazor drives, same pattern as the other bridges).

**Editor `#hitTest` stays AABB+margin** (forgiving region select); the sketch tool uses
`shape.containsPoint` (true per-type, incl. point-in-polygon for lasso/polygon). Two needs over the
same primitive — keep both (`canvas-interaction.md` §2, §11).

---

## 4. Finish (server rasterize) — reuse, don't rebuild

The C# analog of `sketch_export.py`, but it writes **DB artifacts**, not files, and reuses what exists:

1. **Rasterize shapes → cell set**, honouring the 4-step order (the reference's `_compute_island_polys`
   semantics, applied per-cell): `add` paints, `subtract` erases, `override` add/subtract apply last
   (immune to / cutting through normal ops). Each shape → ring (`geometry/shape.js` parity:
   circle=64 pts, Bézier=16 samples/edge), then point-in-cell-centre fill (`(x+0.5, z+0.5)`).
2. **Detect islands**: `IslandDetector.DetectCleaned(cells)` (already exists — connected components +
   cleanup) → island polygons + bounds. **No polygon-boolean lib.**
3. **Mirror copies**: for each island whose meta `mirrors=true`, transform via
   `Geometry2d.RotatePoint`/`ReflectPoint` (`rot_90` → 3 copies 90/180/270; others → 1) and re-rasterise
   into the cell set.
4. **Re-attach metadata**: match detected islands → saved `islands[]` metas by shape-block overlap
   (server-side `restoreIslandMeta`), defaulting `name="Island N"`, `mirrors=true`.
5. **Write artifacts** (new `WorldFeatureWriter.WriteSketchAsync(mapId, cells, metas, setup, ct)`,
   sibling to `WriteAsync`, reusing `StoreArtifactAsync` + `IslandDetector.SerializeJson`):
   `LayerParquet` (synthetic stone, `block_id=1`, at the surface), `IslandsJson`, `layer_segment` rows
   (single `[0,0]` column each, so the build-step side view has data), and the `symmetry` row/artifact
   (`status=confirmed`, the chosen `mirror_mode` + center).
6. **Guard**: `< 2` islands ⇒ 422 (the reference's export rule — a CTW needs ≥2 sides).

Result: the draft map now has the exact geometry artifacts a scanned/imported map has → it flows into
`/maps/{slug}/configure` with zero new consumer code.

---

## 5. Tasks (S2 breakdown)

- **S2a — geometry**: `geometry/shape.js` (+ unit tests) and `geometry/boolean.js` (port
  `computeIslands`/`assignShapesToIslands`/`computeMirrorPreview`/`restoreIslandMeta`; add
  `polygon-clipping`). Resolves `canvas-interaction.md` §11.
- **S2b — canvas + controllers**: `canvas/sketch-canvas.js`, `controllers/sketch-draw-controller.js`,
  `controllers/sketch-edit-controller.js` (reuse `CanvasBase` + the controller contract + `render/`).
- **S2c — bridge + Blazor pages**: `bridge/sketch-bridge.js`; `Sketch{Setup,Layout,Overview}Phase`
  components (mirror Configure phases); the `/maps/new` Sketch entry.
- **S2d — persistence**: `SketchLayoutJson` `ArtifactKind` + DTO; the §1 endpoints (create / get /
  setup / layout / overview).
- **S2e — finish/rasterize**: `WorldFeatureWriter.WriteSketchAsync` + the rasterizer (§4) +
  `POST …/finish`; flow into Configure.

## 6. Decisions & trade-offs (settle before building)

- **Sketch = draft map** (not a separate entity). Chosen for IA symmetry with Configure; the only cost
  is "draft maps" appearing in the maps list (desirable — they're resumable).
- **Live boolean in JS, authoritative rasterise in C#.** The live island preview runs `boolean.js`
  (polygon-clipping) in the browser (hot path, per the hybrid principle); the server rasterizes from
  `shapes` for the persisted geometry and re-detects islands with the existing `IslandDetector`. This
  avoids a C# polygon-boolean dependency entirely. (Alternative: send client island polygons to the
  server and skip re-detection — simpler but trusts client geometry; rejected for authority.)
- **JS tests for `boolean.js`** need `polygon-clipping` from `node_modules`, which the VirtualBox shared
  folder can't host — so those tests use the reference's out-of-tree runner pattern (`/root/...`), while
  the pure `geometry/shape.js` tests stay in the no-deps `node --test` harness (`tools/js-test.sh`).
- **Parity constants are load-bearing**: circle=64 pts, Bézier=16 samples, the 4-step add/sub/override
  order, the stringified-index control dict, `rot_270 = (Δx,Δz)→(Δz,−Δx)`. Keep JS rasterizer ⇄ C#
  rasterizer ⇄ `render/svg.js` in lock-step (the reference keeps three copies aligned; we keep two).
- **`map.xml` validity**: a finished sketch is an xml-less world (regions/filters empty dicts) — Configure
  then authors the XML. Validate generated XML against the PGM parser (`reference_pgm_plugin_xml`).
