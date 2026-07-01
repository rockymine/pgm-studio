/**
 * SketchCanvas — the drawing surface for the Sketch tool. Extends CanvasBase for pan/zoom/drag and
 * delegates: draw tools → SketchDrawController, resize/vertex/Bézier edit → SketchEditController,
 * all SVG emit → render/sketch-render, point-in-shape → geometry/shape.containsPoint. It owns the
 * layer <g> lifecycle, the shape DOM map, selection, and the bbox/center/mode setup state; the host
 * (bridge/sketch-bridge.js) owns the island recompute loop and pushes results via setIslands/setMirror.
 *
 * World coordinates ARE the SVG base coordinates here — an identity transform (no buildTransform);
 * fitToBbox() sets scale/pan to frame the working bounds. (Distinct from EditorCanvas, which maps
 * through buildTransform — do not collapse the two.)
 *
 * Callbacks: onShapeCreated(partial) · onShapeUpdated(shape) · onShapeSelected(id|null) · onShapeDeleted(id)
 */

import { CanvasBase } from "./canvas-base.js";
import { svgEl } from "../render/svg.js";
import { containsPoint, toBounds, translateShape } from "../geometry/shape.js";
import { SketchDrawController } from "../controllers/sketch-draw-controller.js";
import { SketchEditController } from "../controllers/sketch-edit-controller.js";
import {
  renderSketchShape, renderIslands, renderMirror, renderBbox, renderChunkGrid, renderAxis, renderPlaceGhost, renderGhostIslands,
} from "../render/sketch-render.js";
// iso-webgl is loaded lazily (on first 3-D toggle) so a missing/blocked WebGL stack — or any failure
// to load that module — degrades to "no 3-D preview" instead of breaking the whole editor at page load.

const FIT_MARGIN = 0.85;
const identityTransform = (x, z) => ({ x, y: z });

// Nearest snap target to any of `edges` within `tol`; returns { adjust, line } so edge+adjust == line.
function bestSnap(edges, targets, tol) {
  let best = null;
  for (const e of edges) for (const t of targets) {
    const d = Math.abs(e - t);
    if (d <= tol && (!best || d < best.d)) best = { d, adjust: t - e, line: t };
  }
  return best;
}

export class SketchCanvas extends CanvasBase {
  #bbox    = null;
  #center  = { cx: 0, cz: 0 };
  #mode    = "rot_180";

  #shapes      = new Map();   // id → shape (source for render / hit-test / edit)
  #shapeElMap  = new Map();   // id → <g>
  #selectedId  = null;
  #islands     = [];
  #mirrorPolys = [];

  #shapesVisible = false;
  #mirrorVisible = true;

  #draw = null;
  #edit = null;
  #callbacks = {};
  #cursorEl  = null;
  #zoomEl    = null;
  #dimEl     = null;
  #measure   = null;   // { ax, az, bx, bz, live } — the ruler measurement (drag across a void gap)
  #placeSpecs = null;  // library item being placed: shape specs centred at origin, awaiting a drop point
  #dragStartShape = null;  // snapshot of the grabbed shape at drag start (absolute snap-aware move, S9)
  #snapEnabled = true;

  // viewport layers
  #bboxLayer = null; #chunkLayer = null; #axisLayer = null;
  #mirrorLayer = null; #ghostLayer = null; #islandLayer = null; #shapesLayer = null; #drawLayer = null; #measureLayer = null; #placeLayer = null; #guideLayer = null;
  // screen-space layers (outside the viewport transform)
  #handlesLayer = null; #centerLayer = null; #drawHandlesLayer = null; #measureLabelLayer = null;
  #iso      = null;   // WebGL iso renderer (S6), lazily created on first 3-D toggle
  #isoOn    = false;

  constructor(svgEl_, wrapEl, { cursorEl, zoomEl, dimEl, ...callbacks } = {}) {
    super(svgEl_, wrapEl);
    this.#callbacks = callbacks;
    this.#cursorEl  = cursorEl ?? null;
    this.#zoomEl    = zoomEl ?? null;
    this.#dimEl     = dimEl ?? null;
    this.#build();
  }

  // ── public API ───────────────────────────────────────────────────────────────

  setBbox(bbox)        { this.#bbox = bbox; this.#renderSetup(); }
  setCenter(cx, cz)    { this.#center = { cx, cz }; renderAxis(this.#axisLayer, this.#bbox, this.#center, this.#mode, identityTransform); this.#refreshCenter(); }
  setMode(mode)        { this.#mode = mode; renderAxis(this.#axisLayer, this.#bbox, this.#center, this.#mode, identityTransform); }
  setOperation(op)     { this.#draw?.setOperation(op); }

  fitToBbox() {
    if (!this.#bbox) return;
    const { min_x, max_x, min_z, max_z } = this.#bbox;
    const { w, h } = this.#size();
    const bw = max_x - min_x, bh = max_z - min_z;
    if (!bw || !bh) return;
    const scale = Math.min(w / bw, h / bh) * FIT_MARGIN;
    this._scale = scale;
    this._panX  = w / 2 - ((min_x + max_x) / 2) * scale;
    this._panY  = h / 2 - ((min_z + max_z) / 2) * scale;
    this._applyViewportTransform();
    this._onZoom(this._scale);
  }

  resize() {
    const { w, h } = this.#size();
    this._svg.setAttribute("width",  w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);
    this.#edit?.refresh();
    this.#refreshCenter();
  }

  setActiveTool(tool) {
    this.#draw?.cancel();
    if (this._activeTool === "measure" && tool !== "measure") this.#clearMeasure();
    if (this._activeTool === "place" && tool !== "place") this.disarmPlace();
    this._activeTool = tool;
    const isDraw = tool !== null && tool !== "move" && tool !== "select";
    this._svg.style.cursor = isDraw ? "crosshair" : (tool === "select" ? "default" : "");
  }

  // Arm placement of a library item (shape specs centred at origin) — enters "place" mode; the next
  // canvas click drops them (translated to the click) via onPlace. Esc / a tool change disarms.
  armPlace(specs) { this.#placeSpecs = specs ?? null; this.setActiveTool("place"); }
  disarmPlace()   { this.#placeSpecs = null; renderPlaceGhost(this.#placeLayer, null, identityTransform); }

  addShape(shape) {
    this.#shapes.set(shape.id, shape);
    const g = this.#shapeEl(shape);
    this.#shapeElMap.set(shape.id, g);
    this.#shapesLayer.appendChild(g);
  }

  updateShape(shape) {
    this.#shapes.set(shape.id, shape);
    const old = this.#shapeElMap.get(shape.id);
    if (old?.parentNode) old.parentNode.removeChild(old);
    const g = this.#shapeEl(shape);
    this.#shapeElMap.set(shape.id, g);
    this.#shapesLayer.appendChild(g);
    if (this.#selectedId === shape.id) { this.#applySelection(); this.#edit?.refresh(); }
  }

  removeShape(id) {
    this.#shapes.delete(id);
    const el = this.#shapeElMap.get(id);
    if (el?.parentNode) el.parentNode.removeChild(el);
    this.#shapeElMap.delete(id);
    if (this.#selectedId === id) { this.#selectedId = null; this.#edit?.setSelected(null); this.#edit?.refresh(); }
  }

  clearShapes() { for (const id of [...this.#shapes.keys()]) this.removeShape(id); }

  selectShape(id) {
    this.#selectedId = id;
    this.#applySelection();
    this.#edit?.setSelected(id);
    this.#edit?.refresh();
    this.#updateDim();
  }

  getShape(id)  { return this.#shapes.get(id); }
  getShapes()   { return [...this.#shapes.values()]; }
  get selectedId() { return this.#selectedId; }

  setIslands(islands)        { this.#islands = islands ?? []; renderIslands(this.#islandLayer, this.#islands, identityTransform); }
  setGhostIslands(polys)     { renderGhostIslands(this.#ghostLayer, polys ?? [], identityTransform); }
  setMirrorPolygons(polys)   { this.#mirrorPolys = polys ?? []; renderMirror(this.#mirrorLayer, this.#mirrorPolys, identityTransform); }
  setShapesVisible(v) { this.#shapesVisible = v; if (this.#shapesLayer) this.#shapesLayer.style.display = v ? "" : "none"; }
  setMirrorVisible(v) { this.#mirrorVisible = v; if (this.#mirrorLayer) this.#mirrorLayer.style.display = v ? "" : "none"; }
  setChunkVisible(v)  { if (this.#chunkLayer) this.#chunkLayer.style.display = v ? "" : "none"; }

  // ── isometric preview (S6) ─────────────────────────────────────────────────────
  // Swap the top-down viewport for a read-only iso render of the extruded islands. Lazily loads and
  // creates the WebGL renderer on first use; returns false (leaving the 2-D viewport untouched) if the
  // preview module can't load or WebGL is unavailable, so the caller can fall back gracefully.
  async showIso(islands, yawDeg, bbox) {
    if (!this.#iso) {
      try {
        const { IsoScene } = await import("../render/iso-webgl.js");
        this.#iso = new IsoScene(this._wrap);
      } catch (e) {
        console.warn("[sketch] 3-D preview unavailable:", e?.message ?? e);
        return false;
      }
    }
    this.#isoOn = true;
    this.#draw?.cancel();
    const { w, h } = this.#size();
    for (const g of [this._viewportG, this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer]) g.style.display = "none";
    this.#iso.show();
    this.#iso.render(islands, w, h, yawDeg, bbox);
    return true;
  }
  hideIso() {
    this.#isoOn = false;
    this.#iso?.hide();
    this._viewportG.style.display = "";
    for (const g of [this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer]) g.style.display = "";
  }

  // ── CanvasBase hooks ───────────────────────────────────────────────────────────

  _onViewportChanged() { this.#edit?.refresh(); this.#refreshCenter(); this.#draw?.refreshDrawHandles(); this.#renderMeasureLabel(); }
  _onZoom(scale)       { if (this.#zoomEl) this.#zoomEl.textContent = `${Math.round(scale * 100)}%`; }

  _onToolMousedown(e, svgPt) {
    if (this.#isoOn) return;   // iso preview is read-only
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this._activeTool === "place") { if (this.#placeSpecs) this.#callbacks.onPlace?.(bx, bz); return; }
    if (this._activeTool === "measure") { this.#measure = { ax: bx, az: bz, bx, bz, live: true }; this.#renderMeasure(); this.#updateDim(); return; }
    this.#draw?.onMouseDown(bx, bz, this._activeTool);
  }

  _onPointerMove(e, svgPt) {
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this.#cursorEl) this.#cursorEl.textContent = `X ${bx}  Z ${bz}`;
    if (this._activeTool === "place") {
      if (this.#placeSpecs) renderPlaceGhost(this.#placeLayer, this.#placeSpecs.map(s => translateShape(s, bx, bz)), identityTransform);
    } else if (this._activeTool === "measure") {
      if (this.#measure?.live) { this.#measure.bx = bx; this.#measure.bz = bz; this.#renderMeasure(); }
    } else {
      this.#draw?.onMouseMove(bx, bz);
    }
    this.#updateDim();
    this.#edit?.onPointerMove(svgPt.x, svgPt.y, this._activeTool);
  }

  _onToolMouseup(e, svgPt) {
    if (this._activeTool === "measure") { if (this.#measure) this.#measure.live = false; return; }
    this.#draw?.onMouseUp();
  }

  _onCanvasClick(e, svgPt) {
    if (this.#isoOn) return;
    this.#callbacks.onShapeSelected?.(this.#hitTest(svgPt.x, svgPt.y));
  }

  _onMouseleave() { if (this.#cursorEl) this.#cursorEl.textContent = ""; this.#updateDim(); }

  _onResizeMove(e) {
    if (!this.#edit) return false;
    const p = this._clientToSvg(e.clientX, e.clientY);
    return this.#edit.onResizeMove(p.x, p.y, e.altKey);
  }
  _onResizeUp(e) {
    const consumed = e.button === 0 ? (this.#edit?.onResizeUp() ?? false) : false;
    this.#renderGuides(null, null);   // drop any resize alignment guide
    return consumed;
  }

  // Body-drag (CV10): drag the selected shape's body to move it. World == svg base coords here, so the
  // default _toWorld (identity) is correct — no override.
  _hitMovable(world) {
    if (this.#isoOn || !this.#selectedId) return null;
    const s = this.#shapes.get(this.#selectedId);
    return (s && containsPoint(s, world.x, world.z)) ? this.#selectedId : null;
  }
  _moveBy(id, dx, dz) {
    const s = this.#shapes.get(id);
    if (!s) return;
    const moved = translateShape(s, dx, dz);
    this.updateShape(moved);
    this.#callbacks.onShapeUpdated?.(moved);   // bridge recomputes islands + marks dirty each step
  }

  setSnapEnabled(v) { this.#snapEnabled = !!v; }

  _moveStart(id) { const s = this.#shapes.get(id); this.#dragStartShape = s ? structuredClone(s) : null; }

  // Absolute, snap-aware move (S9): place the shape at start + (dx,dz), snapping its bbox edges/centre to
  // other shapes' edges/centres + the symmetry centre; draws alignment guides. Alt bypasses snapping.
  _moveTo(id, dx, dz, alt) {
    const start = this.#dragStartShape;
    if (!start || start.id !== id) return false;
    const sb = toBounds(start);
    let sdx = dx, sdz = dz, gx = null, gz = null;
    if (sb && this.#snapEnabled && !alt) {
      const tol = 6 / (this._scale || 1);
      const { xs, zs } = this.#snapTargets(id);
      const sx = bestSnap([sb.min_x + dx, (sb.min_x + sb.max_x) / 2 + dx, sb.max_x + dx], xs, tol);
      const sz = bestSnap([sb.min_z + dz, (sb.min_z + sb.max_z) / 2 + dz, sb.max_z + dz], zs, tol);
      if (sx) { sdx = dx + sx.adjust; gx = sx.line; }
      if (sz) { sdz = dz + sz.adjust; gz = sz.line; }
    }
    this.#renderGuides(gx, gz);
    const moved = translateShape(start, Math.round(sdx), Math.round(sdz));
    this.updateShape(moved);
    this.#callbacks.onShapeUpdated?.(moved);
    return true;
  }

  _commitMove() { this.#dragStartShape = null; this.#renderGuides(null, null); }

  // Snap-aware rectangle resize: snap the dragged edge coord(s) to other shapes' edges/centres + the
  // symmetry centre, draw the alignment guide, and return the (possibly) adjusted coords. The resize
  // counterpart of _moveTo's snapping; fed to the edit controller as its `snapEdges` hook. `edges` = the
  // proposed dragged-edge values `{ x, z }` (either may be null when that axis isn't dragged). Alt or the
  // Snap toggle off → pass through unchanged and clear the guide.
  #snapResize(excludeId, edges, alt) {
    if (!this.#snapEnabled || alt) { this.#renderGuides(null, null); return edges; }
    const tol = 6 / (this._scale || 1);
    const { xs, zs } = this.#snapTargets(excludeId);
    let gx = null, gz = null;
    const out = { x: edges.x, z: edges.z };
    if (edges.x != null) { const s = bestSnap([edges.x], xs, tol); if (s) { out.x = s.line; gx = s.line; } }
    if (edges.z != null) { const s = bestSnap([edges.z], zs, tol); if (s) { out.z = s.line; gz = s.line; } }
    this.#renderGuides(gx, gz);
    return out;
  }

  // Candidate snap coordinates: every OTHER shape's bbox min/centre/max + the symmetry centre.
  #snapTargets(excludeId) {
    const xs = new Set([this.#center.cx]), zs = new Set([this.#center.cz]);
    for (const [id, s] of this.#shapes) {
      if (id === excludeId) continue;
      const b = toBounds(s); if (!b) continue;
      xs.add(b.min_x); xs.add((b.min_x + b.max_x) / 2); xs.add(b.max_x);
      zs.add(b.min_z); zs.add((b.min_z + b.max_z) / 2); zs.add(b.max_z);
    }
    return { xs: [...xs], zs: [...zs] };
  }

  #renderGuides(gx, gz) {
    const layer = this.#guideLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    if (!this.#bbox) return;
    const { min_x, max_x, min_z, max_z } = this.#bbox;
    const attrs = { stroke: "var(--accent)", "stroke-width": "1", "stroke-dasharray": "4 3", "vector-effect": "non-scaling-stroke" };
    if (gx !== null) layer.appendChild(svgEl("line", { x1: gx, y1: min_z, x2: gx, y2: max_z, ...attrs }));
    if (gz !== null) layer.appendChild(svgEl("line", { x1: min_x, y1: gz, x2: max_x, y2: gz, ...attrs }));
  }

  // ── private ────────────────────────────────────────────────────────────────────

  // Subtract the .svg-area padding (12px each side) so the svg's viewBox equals its rendered size.
  // Otherwise `.map-canvas-svg { max-width:100% }` shrinks the svg to fit while the viewBox stays
  // larger, scaling the client→world mapping — the cursor would land off the drawn anchors/preview,
  // worse toward the right/bottom. Matches EditorCanvas (clientWidth − 24).
  #size() { return { w: (this._wrap.clientWidth || 600) - 24, h: (this._wrap.clientHeight || 600) - 24 }; }

  #build() {
    const { w, h } = this.#size();
    this._svg.setAttribute("width", w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);

    this._viewportG   = svgEl("g", { id: "sk-viewport" });
    this.#bboxLayer   = svgEl("g", { "pointer-events": "none" });
    this.#chunkLayer  = svgEl("g", { "pointer-events": "none" });
    this.#axisLayer   = svgEl("g", { "pointer-events": "none" });
    this.#mirrorLayer = svgEl("g", { "pointer-events": "none" });
    this.#ghostLayer  = svgEl("g", { id: "sk-ghost", "pointer-events": "none" });
    this.#islandLayer = svgEl("g", { "pointer-events": "none" });
    this.#shapesLayer = svgEl("g");
    this.#drawLayer   = svgEl("g", { "pointer-events": "none" });
    this.#measureLayer = svgEl("g", { "pointer-events": "none" });
    this.#placeLayer   = svgEl("g", { "pointer-events": "none" });
    this.#guideLayer   = svgEl("g", { "pointer-events": "none" });
    for (const g of [this.#bboxLayer, this.#chunkLayer, this.#axisLayer, this.#mirrorLayer, this.#ghostLayer,
                     this.#islandLayer, this.#shapesLayer, this.#drawLayer, this.#measureLayer, this.#placeLayer, this.#guideLayer]) this._viewportG.appendChild(g);
    this._svg.appendChild(this._viewportG);

    this.#handlesLayer      = svgEl("g");
    this.#centerLayer       = svgEl("g", { "pointer-events": "none" });
    this.#drawHandlesLayer  = svgEl("g", { "pointer-events": "none" });
    this.#measureLabelLayer = svgEl("g", { "pointer-events": "none" });
    for (const g of [this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer]) this._svg.appendChild(g);

    if (!this.#shapesVisible) this.#shapesLayer.style.display = "none";
    if (!this.#mirrorVisible) this.#mirrorLayer.style.display = "none";
    this._applyViewportTransform();

    const getViewport = () => ({ scale: this._scale, panX: this._panX, panY: this._panY });
    this.#draw = new SketchDrawController(this.#drawLayer, this.#drawHandlesLayer, getViewport, {
      onShapeCreated: (partial) => this.#callbacks.onShapeCreated?.(partial),
    });
    this.#edit = new SketchEditController(this.#handlesLayer, getViewport, (id) => this.#shapes.get(id), {
      onShapeUpdated: (shape) => { this.updateShape(shape); this.#callbacks.onShapeUpdated?.(shape); },
      onVertexSelected: (shapeId, idx) => this.#callbacks.onVertexSelected?.(shapeId, idx),
      snapEdges: (id, edges, alt) => this.#snapResize(id, edges, alt),
    });

    // Escape cancels an in-progress draw; Delete/Backspace removes the selected shape. (Arrow-nudge is
    // owned by the host/bridge — the activity layer.) Guarded by visibility + not-typing-in-a-field.
    document.addEventListener("keydown", (e) => {
      if (this._wrap?.offsetParent == null) return;
      if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
      if (e.key === "Escape") { this.#draw.cancel(); this.#clearMeasure(); }
      if ((e.key === "Delete" || e.key === "Backspace") && this.#selectedId) {
        this.#callbacks.onShapeDeleted?.(this.#selectedId);
      }
      if ((e.key === "p" || e.key === "P") && this.#selectedId) this.#callbacks.onShapePromote?.(this.#selectedId);
    });
    // Double-click closes a polygon (duplicate trailing vertex trimmed inside the controller).
    this._svg.addEventListener("dblclick", (e) => {
      if (this._activeTool !== "polygon") return;
      e.stopPropagation();
      this.#draw.onDblClick();
    });
  }

  #renderSetup() {
    renderBbox(this.#bboxLayer, this.#bbox, identityTransform);
    renderChunkGrid(this.#chunkLayer, this.#bbox, identityTransform);
    renderAxis(this.#axisLayer, this.#bbox, this.#center, this.#mode, identityTransform);
  }

  // Build the shape group (render layer) + attach the select click handler (interaction stays here).
  #shapeEl(shape) {
    const g = renderSketchShape(shape, identityTransform);
    g.style.cursor = "pointer";
    g.addEventListener("click", (e) => {
      if (this._activeTool !== null && this._activeTool !== "move" && this._activeTool !== "select") return;
      e.stopPropagation();
      this.#callbacks.onShapeSelected?.(shape.id);
    });
    if (shape.id === this.#selectedId) this.#markSelected(g, true);
    return g;
  }

  #applySelection() {
    for (const [id, g] of this.#shapeElMap) this.#markSelected(g, id === this.#selectedId);
  }

  // Selection chrome without a dedicated CSS class — brighten stroke + fill on the shape's element.
  #markSelected(g, on) {
    const el = g.firstElementChild;
    if (!el) return;
    el.setAttribute("stroke-width", on ? "2.5" : "1.2");
    el.setAttribute("fill-opacity", on ? "0.4" : "0.28");
  }

  #refreshCenter() {
    const layer = this.#centerLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const sx = this.#center.cx * this._scale + this._panX;
    const sy = this.#center.cz * this._scale + this._panY;
    const arm = 10, col = "var(--canvas-axis)";
    layer.appendChild(svgEl("line", { x1: sx - arm, y1: sy, x2: sx + arm, y2: sy, stroke: col, "stroke-width": "1" }));
    layer.appendChild(svgEl("line", { x1: sx, y1: sy - arm, x2: sx, y2: sy + arm, stroke: col, "stroke-width": "1" }));
    layer.appendChild(svgEl("circle", { cx: sx, cy: sy, r: 4, fill: "none", stroke: col, "stroke-width": "1.5" }));
  }

  // The ruler: a line in world coords (so it pans/zooms with the map) plus a live distance label on the
  // line itself (#renderMeasureLabel) — the reading rides the ruler, not the sub-bar.
  #renderMeasure() {
    const layer = this.#measureLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const m = this.#measure;
    if (m) layer.appendChild(svgEl("line", {
      x1: m.ax, y1: m.az, x2: m.bx, y2: m.bz,
      stroke: "var(--canvas-axis)", "stroke-width": "1.5", "stroke-dasharray": "4 3", "vector-effect": "non-scaling-stroke",
    }));
    this.#renderMeasureLabel();
  }

  // The ruler distance as pure screen-space text running ALONG the ruler line — legible at any zoom (a
  // world-space label would scale with the map), so it's repositioned on every viewport change too. A
  // thin halo (paint-order stroke in the canvas bg) keeps it readable over shapes without a box/pill.
  #renderMeasureLabel() {
    const layer = this.#measureLabelLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const m = this.#measure;
    if (!m) return;
    const text = `${Math.round(Math.hypot(m.bx - m.ax, m.bz - m.az))} blocks`;
    // Endpoints + midpoint in screen space (identity world→svg, then the viewport pan/scale).
    const ax = m.ax * this._scale + this._panX, ay = m.az * this._scale + this._panY;
    const bx = m.bx * this._scale + this._panX, by = m.bz * this._scale + this._panY;
    const mx = (ax + bx) / 2, my = (ay + by) / 2;
    let deg = Math.atan2(by - ay, bx - ax) * 180 / Math.PI;
    if (deg > 90 || deg < -90) deg += 180;                 // keep the text upright, never mirrored
    const rad = deg * Math.PI / 180, OFF = 7;              // float just off the line, on its upper side
    const tx = mx + Math.sin(rad) * OFF, ty = my - Math.cos(rad) * OFF;
    const t = svgEl("text", {
      x: tx, y: ty, transform: `rotate(${deg.toFixed(1)} ${tx.toFixed(1)} ${ty.toFixed(1)})`,
      "text-anchor": "middle", "dominant-baseline": "middle",
      "font-size": "11", "font-family": "ui-monospace, monospace", "font-weight": "600",
      fill: "var(--canvas-axis)", "pointer-events": "none",
      "paint-order": "stroke", stroke: "var(--bg-canvas)", "stroke-width": "3", "stroke-linejoin": "round",
    });
    t.textContent = text;
    layer.appendChild(t);
  }

  #clearMeasure() { this.#measure = null; this.#renderMeasure(); this.#updateDim(); }

  // On-canvas size readout (sub-bar): the active draw's W×D, else the selected shape's extent — so the
  // author can aim for a target block size while drawing. (The ruler distance reads on the ruler line
  // itself via #renderMeasureLabel, not here.)
  #updateDim() {
    if (!this.#dimEl) return;
    let label = this.#draw?.activeDimLabel?.() || "";
    if (!label && this.#selectedId) {
      const b = toBounds(this.#shapes.get(this.#selectedId));
      if (b) label = `${Math.round(b.max_x - b.min_x)} × ${Math.round(b.max_z - b.min_z)}`;
    }
    this.#dimEl.textContent = label;
  }

  #hitTest(wx, wz) {
    const ids = [...this.#shapes.keys()];
    for (let i = ids.length - 1; i >= 0; i--) {
      const shape = this.#shapes.get(ids[i]);
      if (containsPoint(shape, wx, wz)) return shape.id;
    }
    return null;
  }
}
