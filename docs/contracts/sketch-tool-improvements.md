# Sketch tool improvements — footprint, height, 3D (S3–S7)

Status: **plan**. Builds on the shipped Sketch tool (`sketch-authoring.md`, S2 — draw 2-D shapes →
islands → rasterize → world geometry). This doc is the design for the next depth pass: make size
**legible** and add **verticality** without rewriting the 2-D editor. Every change here is **additive**
to `SketchShape` / `SketchLayout` / `SketchRasterizer` and their JS twins — the existing draw/edit/finish
flow keeps working with the new fields defaulted off.

Reference types: `src/PgmStudio.Pgm/Sketch/SketchLayout.cs`, `SketchRasterizer.cs`;
JS `js/studio/geometry/shape.js`, `boolean.js`, `canvas/sketch-canvas.js`, the sketch controllers.

---

## 0. The two problems (motivation)

1. **Scale is illegible.** The working canvas defaults to **512×512 blocks**. Real 2-team CTW maps fit a
   **~120-block** long axis (landscape *or* portrait), with **lanes 10–15 wide (max ~20)**. At a 512 fit,
   a 15-block lane is a handful of pixels — you draw blind and can't aim for the band that matters. The
   fix is a **tight, non-square, preset-driven footprint** plus **live dimension readouts**, not
   travel-time/jumpability cues (explicitly out of scope — gameplay-timing overlays aren't wanted).
2. **The sketch is flat (Y=0).** Everything rasterizes to a single surface layer. Minecraft CTW is
   vertical — walls, platforms, sunken lanes, sky bridges, bunkers. We add height **per shape and per
   anchor**, a **read-only 3-D preview** so it's legible, and **stacked layers** for genuinely separate
   slabs.

These line up with the intent-model direction (`new-map-authoring.md`): height *is* meaning (high-ground),
footprint *is* meaning (map class). The author expresses it; the rasterizer realizes it.

---

## 1. Footprint & scale (S3)

### 1a. Non-square, preset-driven working size

Today `SketchSetup.Bbox` is derived from a single `size` (a symmetric `size×size` square, default 512;
`SketchEditor.razor.cs`). Replace the single number with an **independent X-extent × Z-extent**, framed
by **presets** rather than a raw default:

| Preset | X × Z | For |
|---|---|---|
| **2-team landscape** *(default)* | 120 × 80 | the common case |
| **2-team portrait** | 80 × 120 | vertical footprint maps |
| **Square (4-team / D2)** | 120 × 120 | 4-team + diagonal-symmetry maps — **still needed**, keep it |
| **Custom** | author-typed X, Z | escape hatch |

- `SketchBbox` already carries `min_x/max_x/min_z/max_z` — keep the shape, just let X-extent ≠ Z-extent
  (symmetric about the centre: `min_x = cx − X/2`, etc.). **No wire change** to the artifact; only the
  Setup UI and the derive-from-size code change.
- The preset choice is UI state that lands on the bbox; the **symmetry mode** stays independent (a square
  footprint pairs with `rot_90`/diagonal D2; landscape/portrait with `rot_180`/`mirror_*`). Don't couple
  them mechanically — just default the symmetry dropdown sensibly per preset.
- Zoom-to-fit frames the (now small) bbox, so 15 blocks reads as ~15 visible blocks.

### 1b. Live dimension readout

The single highest-value, lowest-cost change for "aim for 10–15": while drawing or with a shape selected,
render its **block extent at the cursor** (`18 × 90`) and on the shape. The draw controllers already track
live geometry (`sketch-draw-controller.js`); add a text emit to `sketch-render.js`. For polygon/lasso,
show the **bounding extent**; for rectangle, exact W×D; for circle, diameter.

### 1c. Void-gap measure

Useful (unlike jumpability): report the **shortest distance between two island bodies**. Islands are
already computed live in JS (`boolean.js computeIslands`); add a nearest-point distance between two
selected island polygons and draw a **dimension line across the gap** with the block count. Either a
measure tool or auto-shown for a hovered/selected island pair. (Scale bar is **parked** — not now.)

---

## 2. Rectangles are polygons too (S4)

Author ask: a rectangle should be **promotable into a polygon** — add vertices, curve edges, set per-anchor
heights. Today `rectangle` is its own stored type (`min_x/min_z/max_x/max_z`) with an 8-handle resize.

**Keep rectangle as a creation preset and an axis-aligned fast-path**, but add a one-way **"convert to
polygon"** action (and auto-promote on any edit that a rectangle can't represent — inserting a midpoint
vertex, dragging a single corner off-axis, adding a Bézier handle, or assigning non-uniform per-anchor
heights):

```
rectangle{min,max}  ──promote──▶  polygon{ vertices: [4 corners CW], controls?, heights? }
```

- Promotion is `type: "rectangle" → "polygon"` with `vertices` = the 4 corners; from then on it's an
  ordinary polygon (vertex drag, midpoint insert, Bézier, per-anchor height all already apply). The
  rasterizer needs **no special case** — `RingOf` already maps `polygon` directly.
- The fast-path stays for the common "I just want a box" flow (drag-create, 8-handle resize). Only edits
  that exceed a rectangle trigger promotion, so we don't lose the cheap axis-aligned interaction.
- JS `shape.js` + the edit controller gain the promote step; C# `SketchShape` needs no new field for this
  (the type flip + `vertices` already exist).

---

## 3. Height — per-shape and per-anchor (S5)

The core win. A shape gains a **surface height**; the rasterizer emits a **column** instead of a single
`(x,z)` cell at Y=0.

### 3a. Data model (additive to `SketchShape`)

```csharp
// SketchShape — new fields, all optional, default = today's flat Y=0 behaviour
[JsonPropertyName("base_height")] public double? BaseHeight { get; set; }   // uniform surface Y for the shape
[JsonPropertyName("anchor_heights")] public double[]? AnchorHeights { get; set; } // per-vertex Y, index-aligned to Vertices (polygon/lasso)
[JsonPropertyName("floor")]       public double? Floor { get; set; }        // bottom Y of the column (default 0)
```

- **Per-shape height** = `BaseHeight` only → a flat slab from `Floor` (default 0) up to `BaseHeight`.
  Trivial; ship first.
- **Per-anchor height** = `AnchorHeights[i]` paired with `Vertices[i]` → a **sloped/varied top surface**.
  Rectangle uses its 4 corners (bilinear); polygon/lasso use their vertices.
- Heights are **invariant under reflection/rotation**, so the mirror/orbit path (`MirrorShape`) carries
  `BaseHeight`/`AnchorHeights`/`Floor` through **unchanged** — only X/Z transform. (When promoting a
  rectangle's per-corner heights into a polygon, copy them index-aligned to the 4 corner vertices.)

### 3b. Rasterizer change

`RasterShape` currently yields `(int X, int Z)`. Extend the pipeline to carry a per-cell **surface height**:

```
(int X, int Z)  ──▶  (int X, int Z, int YTop, int YFloor)
```

- For a cell passing `PointInRing`, compute its surface Y:
  - no heights → `YTop = 0` (unchanged behaviour),
  - `BaseHeight` only → `YTop = round(BaseHeight)`,
  - `AnchorHeights` → **interpolate** at `(x+0.5, z+0.5)`: bilinear for a rectangle's 4 corners;
    for a polygon, **triangulate the ring (TIN) and barycentric-interpolate**, or IDW from vertices as the
    cheap first cut. Put this in `Geom` (pure, testable) so JS parity is a direct port.
- The 4-step set algebra (`RasterGroup`) keys cells by `(x,z)`; height rides along on the winning add.
  `subtract`/`override` still operate on `(x,z)` membership — height is a property of the surviving cell.

### 3c. Downstream artifacts

The finish writers currently emit a single surface layer at Y=0 (`WriteSketchAsync`: `LayerParquet`
`block_id=1` at the surface, one `[0,0]` `layer_segment` per column). With height:

- **`LayerParquet`**: emit the column `block_id=1` for `Y ∈ [YFloor, YTop]` (or just the top surface +
  a segment, TBD by what the Configure build-step/side-view consumes — see §7).
- **`layer_segment`**: the per-column segment becomes `[YFloor, YTop]` instead of `[0,0]` — which is
  exactly what the build-step **side-view** (`SliceView`) already renders, so height shows up there for
  free. This is the same data N08's monument-Y work reads.
- **`IslandsJson`** / island detection is **X/Z only** — unaffected (islands are footprints).

---

## 4. 3-D preview (S6)

Read-only, no new authoring model — it just makes §3/§5 legible while drawing.

- A **three.js orbit view** over the rasterized/extruded result: box-render the `(x,z,YTop,YFloor)`
  columns (greedy-merge runs of equal-height cells into boxes to keep it cheap). Per-layer colour for §5.
- Drives off the **same JS rasterizer** that backs the live preview (parity with C# `SketchRasterizer`),
  so the 3-D view and the finished world agree. Update on edit-settle (debounced), not per-mousemove.
- Lives beside the 2-D canvas (toggle/split). No interaction beyond camera — editing stays 2-D.
- Build **right after per-shape `BaseHeight`** (§3a) so extrusion is visible the moment it exists.

---

## 5. Stacked layers (S7)

Genuinely separate slabs at different Y (ground / sky bridge / bunker), each a **full reuse of the 2-D
editor** — shapes, islands, symmetry, height all unchanged within a layer.

### 5a. Data model (wraps `SketchLayout.Layout`)

Today `SketchLayout.Layout` is one `SketchShapes{ shapes, islands }`. Generalize to an **ordered list of
layers**, each a `{ baseY, layout }`:

```csharp
public sealed class SketchLayer
{
    [JsonPropertyName("id")]     public string? Id { get; set; }
    [JsonPropertyName("name")]   public string? Name { get; set; }   // "Ground", "Sky bridge", "Bunker"
    [JsonPropertyName("base_y")] public double BaseY { get; set; }   // Y offset of the whole slab
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }
}
```

- **Back-compat**: a layout with the old single `layout:{shapes,islands}` loads as **one layer at
  `base_y = 0`**. Persist new sketches as `layers:[…]`; read both (one-shot upgrade on load). Keep the
  artifact camelCase/snake convention (`base_y`).
- **Composition with height (§3)**: a column at `(x,z)` in layer L spans
  `[L.baseY + Floor, L.baseY + YTop]`. Per-shape/anchor height handles verticality **within** a slab;
  `baseY` stacks slabs. They compose with no conflict.
- **Symmetry** applies per layer (each layer carries its own islands + mirror flags); the **setup**
  (`mirror_mode`, `center`, `bbox`) stays global.
- **Rasterize**: union every layer's cells (each offset by its `baseY`). `IslandDetector` runs on the
  **X/Z footprint union** (overlapping layers share a footprint island) — or per-layer if Configure needs
  per-slab islands (decide in §7).

### 5b. Editor

- A **layer list** in the Setup sidebar (add/rename/reorder/delete, set `baseY`, active-layer select).
- The active layer edits exactly as today; **lower layers render ghosted** under it (and feed the 3-D
  preview as distinct slabs). This is the cheapest path to 3-D: the editor surface is unchanged — it's an
  outer selector over the existing per-layer `{shapes,islands}`.

---

## 6. Sequencing

1. **S3 — footprint & scale**: non-square preset bbox (incl. the square 4-team/D2 preset), live dimension
   readout, void-gap measure. Small; removes the daily friction immediately. No data-model change beyond
   bbox derivation.
2. **S4 — rectangle→polygon promotion**: unblocks per-anchor editing on boxes.
3. **S5 — per-shape `BaseHeight`** → then **per-anchor `AnchorHeights`** (TIN/IDW fill). Rasterizer +
   `layer_segment`/`LayerParquet` carry Y. Land **S6 (3-D preview)** alongside so height is visible.
4. **S7 — stacked layers**: wrap `layout` in `layers[]`, ghost lower layers, compose `baseY` with height.

Each step builds on the last; none forces a rewrite of the 2-D editor.

---

## 7. Open decisions (settle before building the affected step)

- **Column fill vs. surface-only in `LayerParquet`.** Does the Configure build-step/side-view want the
  full `[YFloor, YTop]` column materialized, or just the top surface + the segment? `layer_segment`
  clearly wants `[YFloor, YTop]`; `LayerParquet` may only need the surface. Check `SliceView` +
  `WorldFeatureWriter.WriteSketchAsync` consumers before S5.
- **Per-layer vs. unioned island detection** (§5a). If Configure teams/island-roles need per-slab
  islands, detect per layer and tag with layer id; otherwise union the footprint. Driven by what the
  Configure island consumers expect.
- **Per-anchor interpolation kernel** (§3b): TIN/barycentric (exact, needs triangulation) vs. IDW
  (trivial, approximate on concave rings). Start IDW, upgrade to TIN if slopes look wrong on concave
  shapes. Put it in `Geom` with shared JS parity either way.
- **Height parity constant.** Rounding of interpolated Y (floor vs. round) must match JS ⇄ C# exactly,
  like the existing circle=64 / Bézier=16 constants (`SketchRasterizer` ⇄ `shape.js`).
