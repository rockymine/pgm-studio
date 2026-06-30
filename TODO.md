# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## Current focus — Sketch tool (S)

The depth pass on the shipped Sketch tool (`/maps/{slug}/sketch`): rotation, polygon split, snapping +
ruler feedback, the Floor/Height redefinition, and the sidebar tidy — all additive to
`SketchShape`/`SketchLayout`/`SketchRasterizer` and their JS twins. Full design — data-model diffs,
rasterizer/artifact changes, open decisions — in `docs/contracts/sketch-tool-improvements.md`. The
parked sketch slices (`S2`, `S9b`, `S10`) and the sketch world-export (`P9`) live in `BACKLOG.md`.

- [ ] **S12 — Finish P0#1: pin the Islands tree to the top of the sketch sidebar.** After `S11` removes
  Setup, the residual weight above **Islands** is the **Layers** panel + the 12-tile **Library** palette.
  Collapse both behind `<details>` accordions (Library default-collapsed once the map has shapes), or move
  the Library to a toolbar popover (it's a "reach for a primitive" action, not persistent state). Depends on
  `S11`. (`docs/sketch-tool-ux-review.md` P0#1; `docs/contracts/sketch-creation-flow.md` follow-on.)
- [ ] **S13 — Rotate an island on the canvas.** Sketch has mirror/symmetry but no rotation. Add a rotate
  affordance (rotation handle or numeric angle) that rotates a whole island — every member shape about a shared
  pivot. Polygons/lassos rotate by their vertices; an axis-aligned rectangle can't hold a non-90° angle, so a
  free-angle rotate must promote it to a polygon first (reuse `rectToPolygon`, see `S15`). Islands / mirror /
  rasterizer recompute from the rotated shapes (`geometry/shape.js` + the bridge).
- [ ] **S14 — Split-polygon tool.** A tool to cut one polygon shape into two along a drawn line / two points —
  the sketch-side analogue of the decompose cutter (`geometry/decompose-cut.js`) but producing two `SketchShape`s
  in place. Each half keeps the source's operation / override / height fields; islands recompute. Pure geometry in
  `geometry/shape.js`, wired through the bridge.
- [ ] **S15 — Rectangle→polygon promotion drops the height fields (resets to 1).** `rectToPolygon`
  (`geometry/shape.js:76`) copies only id/type/operation/override — **not** `base_height` / `floor` /
  `anchor_heights` — so a promoted rectangle's height resets to the `1` default (`SketchModels.cs` `BaseHeight = 1`;
  `clampHeight`). Carry the three height fields through `rectToPolygon` (and `promoteShape`, `sketch-bridge.js:121`).
  Small fix.
- [ ] **S16 — Resize library primitives on placement.** Library primitives (n-gons, polyominoes, composites)
  instantiate at a fixed default cell size (`geometry/shape-library.js` `instantiate`) and can't be resized — and
  since they come in as polygons they lack the rectangle's 8-handle resize. Add a scale affordance: drag-to-size
  during placement and/or a uniform resize handle on a placed polyomino group. Relates to `S10` (the parked
  polygon resize-handles decision) and `S8` (the library). Needed for the polyomino-based generation (`G15`).
- [ ] **S17 — Redefine Floor = elevation (y-offset) and Height = thickness.** Today `floor` is the column's
  bottom-Y and `base_height` its top-Y (both relative to the layer `base_y`), so the inspector's "Floor" reads
  like a second height. Redefine to the intuitive model: **Floor = where the shape's base sits** (y-offset within
  the layer) and **Height = how tall it is** (thickness), with `top = base_y + floor + height`. Update
  `SketchRasterizer` (`RasterShape` / `HeightFn` / `RasterizeColumns`), the iso preview (`sketch-bridge.js`
  `terrainOf` + the `top`/`floor` calc), the inspector fields + labels (`SketchInspector.razor`), and the
  rasterizer tests. Stored sketches re-rasterize under the new meaning (intentional — no backward-compat).
- [ ] **S18 — Ruler distance should read along the ruler line, not in the toolbar.** The measure tool draws its
  line in `#measureLayer` (world coords, `sketch-canvas.js` `#renderMeasure`) but shows the block distance in the
  `.canvas-dim` sub-bar readout (`#updateDim`, `sketch-canvas.js:442`). Render the distance as a **live label on /
  beside the ruler line** instead (screen-space text at the line midpoint so it stays legible across zoom;
  re-render on viewport change), and drop the ruler branch from `#updateDim` so the sub-bar readout keeps only the
  draw W×D / selected-extent. Relates to `S3` (the `canvas-dim` readout).
- [ ] **S19 — Snap/alignment guides should also fire when resizing a rectangle.** `S9`'s smart guides only run on
  the **move** path (`SketchCanvas._moveTo`, `sketch-canvas.js:267` — `#snapTargets` / `bestSnap` / `#renderGuides`);
  the 8-handle **resize** path (`SketchEditController.onResizeMove` rect branch, `sketch-edit-controller.js:123`)
  doesn't snap or draw a guide. Extend snapping to resize: snap the dragged edge(s) to other shapes' edges/centres
  + the symmetry centre and draw the guide, honouring the **Snap** toggle and **Alt** bypass (already plumbed via
  `altKey`). The canvas owns the targets/guides, so feed the controller a `snapEdges` hook (the resize counterpart
  of `_moveTo`). Follows `S9`; distinct from the parked `S9b` (angle/parallel snapping).
