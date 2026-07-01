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
 * Callbacks: onShapeCreated(partial) · onShapeUpdated(shape) · onShapeSelected(id|null) [drill] ·
 * onIslandSelected(id|null) [single-click] · onShapeDeleted(id) · onSplit(a, b) [slice a shape in two]
 */

import { CanvasBase } from "./canvas-base.js";
import { svgEl } from "../render/svg.js";
import { containsPoint, toBounds, translateShape, boundsOfShapes, rotateShape, scaleShape } from "../geometry/shape.js";
import { pointInRing } from "../geometry/polygon.js";

// Island scale handles (S21): normalized bbox position (0=min · 0.5=mid · 1=max) + which axes each drives +
// its cursor. Corners scale both axes, edge midpoints one. Anchored on the opposite corner/edge (or the
// centre with Alt); Shift locks a corner to a uniform (aspect-preserving) scale.
const SCALE_HANDLES = [
  { nx: 0,   nz: 0,   axX: 1, axZ: 1, cur: "nwse-resize" },
  { nx: 1,   nz: 0,   axX: 1, axZ: 1, cur: "nesw-resize" },
  { nx: 1,   nz: 1,   axX: 1, axZ: 1, cur: "nwse-resize" },
  { nx: 0,   nz: 1,   axX: 1, axZ: 1, cur: "nesw-resize" },
  { nx: 0.5, nz: 0,   axX: 0, axZ: 1, cur: "ns-resize" },
  { nx: 1,   nz: 0.5, axX: 1, axZ: 0, cur: "ew-resize" },
  { nx: 0.5, nz: 1,   axX: 0, axZ: 1, cur: "ns-resize" },
  { nx: 0,   nz: 0.5, axX: 1, axZ: 0, cur: "ew-resize" },
];

// A rotate cursor (circular arrow, white halo so it reads on both themes) for the island corner zones (S13).
const ROTATE_ICON = "<svg xmlns='http://www.w3.org/2000/svg' width='26' height='26' viewBox='0 0 24 24' fill='none' stroke-linecap='round' stroke-linejoin='round'><g stroke='white' stroke-width='4'><path d='M21 12a9 9 0 1 1-3-6.7'/><path d='M21 3v5h-5'/></g><g stroke='black' stroke-width='2'><path d='M21 12a9 9 0 1 1-3-6.7'/><path d='M21 3v5h-5'/></g></svg>";
const ROTATE_CURSOR = `url("data:image/svg+xml,${encodeURIComponent(ROTATE_ICON)}") 13 13, crosshair`;
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
  #selectedId  = null;        // drilled/single-member shape (drives the edit-controller handles)
  #selectedIslandId = null;   // selected island (drives the island bbox chrome + whole-island drag)
  #islands     = [];          // [{ id, shapeIds, exterior, holes }] from the bridge
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
  #split     = null;   // { ax, az } — the first cut point (S14), awaiting the second click
  #splitLine = null;   // the slice preview line element
  #placeSpecs = null;  // library item being placed: shape specs centred at origin, awaiting a drop point
  #dragStartShape = null;  // snapshot of the grabbed shape at drag start (absolute snap-aware move, S9)
  #dragStartShapes = null; // id→snapshot of every member when body-dragging a whole island (S20)
  #rotateState = null;     // { snapshots, pivot, lastAngle, total } while rotating a selected island (S13)
  #scaleState = null;      // { snapshots, orig, h } while scaling a selected island via a bbox handle (S21)
  #snapEnabled = true;

  // viewport layers
  #bboxLayer = null; #chunkLayer = null; #axisLayer = null;
  #mirrorLayer = null; #ghostLayer = null; #islandLayer = null; #shapesLayer = null; #drawLayer = null; #measureLayer = null; #placeLayer = null; #guideLayer = null;
  // screen-space layers (outside the viewport transform)
  #handlesLayer = null; #centerLayer = null; #drawHandlesLayer = null; #measureLabelLayer = null; #islandChromeLayer = null;
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
    if (this._activeTool === "split" && tool !== "split") this.#clearSplit();
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

  // Select a shape (the drill / panel-shape path): shows its edit handles, clears any island bbox chrome.
  selectShape(id) {
    this.#selectedIslandId = null;
    this.#selectedId = id;
    this.#applySelection();
    this.#edit?.setSelected(id);
    this.#edit?.refresh();
    this.#renderIslandChrome();
    this.#updateDim();
  }

  // Select an island (single-click / panel-island): draws its bbox chrome; a single-member island also
  // shows that member's edit handles (nothing to drill into). Setter only — the click path fires the callback.
  selectIsland(id) {
    this.#selectedIslandId = id ?? null;
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    this.#selectedId = (isl && isl.shapeIds?.length === 1) ? isl.shapeIds[0] : null;
    this.#applySelection();
    this.#edit?.setSelected(this.#selectedId);
    this.#edit?.refresh();
    this.#renderIslandChrome();
    this.#updateDim();
  }

  getShape(id)  { return this.#shapes.get(id); }
  getShapes()   { return [...this.#shapes.values()]; }
  get selectedId() { return this.#selectedId; }
  #islandOfShape(shapeId) { return this.#islands.find(i => i.shapeIds?.includes(shapeId)) ?? null; }

  setIslands(islands)        { this.#islands = islands ?? []; renderIslands(this.#islandLayer, this.#islands, identityTransform); this.#renderIslandChrome(); }
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
    for (const g of [this._viewportG, this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer, this.#islandChromeLayer]) g.style.display = "none";
    this.#iso.show();
    this.#iso.render(islands, w, h, yawDeg, bbox);
    return true;
  }
  hideIso() {
    this.#isoOn = false;
    this.#iso?.hide();
    this._viewportG.style.display = "";
    for (const g of [this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer, this.#islandChromeLayer]) g.style.display = "";
  }

  // ── CanvasBase hooks ───────────────────────────────────────────────────────────

  _onViewportChanged() { this.#edit?.refresh(); this.#refreshCenter(); this.#draw?.refreshDrawHandles(); this.#renderMeasureLabel(); this.#renderIslandChrome(); }
  _onZoom(scale)       { if (this.#zoomEl) this.#zoomEl.textContent = `${Math.round(scale * 100)}%`; }

  _onToolMousedown(e, svgPt) {
    if (this.#isoOn) return;   // iso preview is read-only
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this._activeTool === "place") { if (this.#placeSpecs) this.#callbacks.onPlace?.(bx, bz); return; }
    if (this._activeTool === "measure") { this.#measure = { ax: bx, az: bz, bx, bz, live: true }; this.#renderMeasure(); this.#updateDim(); return; }
    if (this._activeTool === "split") { this.#onSplitClick(bx, bz); return; }
    this.#draw?.onMouseDown(bx, bz, this._activeTool);
  }

  _onPointerMove(e, svgPt) {
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this.#cursorEl) this.#cursorEl.textContent = `X ${bx}  Z ${bz}`;
    if (this._activeTool === "place") {
      if (this.#placeSpecs) renderPlaceGhost(this.#placeLayer, this.#placeSpecs.map(s => translateShape(s, bx, bz)), identityTransform);
    } else if (this._activeTool === "measure") {
      if (this.#measure?.live) { this.#measure.bx = bx; this.#measure.bz = bz; this.#renderMeasure(); }
    } else if (this._activeTool === "split") {
      if (this.#splitLine) { this.#splitLine.setAttribute("x2", bx); this.#splitLine.setAttribute("y2", bz); }
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

  // Single-click selects the containing ISLAND (null = deselect); double-click drills to the shape (below).
  _onCanvasClick(e, svgPt) {
    if (this.#isoOn) return;
    this.#callbacks.onIslandSelected?.(this.#hitIsland(svgPt.x, svgPt.y));
  }

  _onMouseleave() { if (this.#cursorEl) this.#cursorEl.textContent = ""; this.#updateDim(); }

  _onResizeMove(e) {
    if (this.#rotateState) { const p = this._clientToSvg(e.clientX, e.clientY); this.#rotateMove(p.x, p.y, e.shiftKey); return true; }
    if (this.#scaleState)  { const p = this._clientToSvg(e.clientX, e.clientY); this.#scaleMove(p.x, p.y, e.shiftKey, e.altKey); return true; }
    if (!this.#edit) return false;
    const p = this._clientToSvg(e.clientX, e.clientY);
    return this.#edit.onResizeMove(p.x, p.y, e.altKey);
  }
  _onResizeUp(e) {
    if (this.#rotateState) { if (e.button !== 0) return false; this.#rotateState = null; return true; }
    if (this.#scaleState)  { if (e.button !== 0) return false; this.#scaleState = null; return true; }
    const consumed = e.button === 0 ? (this.#edit?.onResizeUp() ?? false) : false;
    this.#renderGuides(null, null);   // drop any resize alignment guide
    return consumed;
  }

  // Body-drag (CV10 shape / S20 island): drag a selected shape's body — or a whole selected island — to
  // move it. World == svg base coords here, so the default _toWorld (identity) is correct — no override.
  // A shape handle is its id (string); an island handle is `{ islandId }`.
  #isIslandHandle(h) { return !!(h && typeof h === "object" && h.islandId); }

  _hitMovable(world) {
    if (this.#isoOn) return null;
    // Island selected → drag the whole island when the point is inside its footprint.
    if (this.#selectedIslandId) {
      const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
      if (isl && isl.exterior?.length >= 4 && pointInRing(world.x, world.z, isl.exterior) &&
          !(isl.holes ?? []).some(h => pointInRing(world.x, world.z, h))) return { islandId: this.#selectedIslandId };
    }
    // Else a drilled/selected shape → drag that shape.
    if (this.#selectedId) {
      const s = this.#shapes.get(this.#selectedId);
      if (s && containsPoint(s, world.x, world.z)) return this.#selectedId;
    }
    return null;
  }
  _moveBy(handle, dx, dz) {
    if (this.#isIslandHandle(handle)) {
      const isl = this.#islands.find(i => i.id === handle.islandId);
      for (const id of (isl?.shapeIds ?? [])) { const s = this.#shapes.get(id); if (s) this.updateShape(translateShape(s, dx, dz)); }
      this.#renderIslandChrome();
      this.#callbacks.onShapeUpdated?.();
      return;
    }
    const s = this.#shapes.get(handle);
    if (!s) return;
    const moved = translateShape(s, dx, dz);
    this.updateShape(moved);
    this.#callbacks.onShapeUpdated?.(moved);   // bridge recomputes islands + marks dirty each step
  }

  setSnapEnabled(v) { this.#snapEnabled = !!v; }

  _moveStart(handle) {
    if (this.#isIslandHandle(handle)) {
      const isl = this.#islands.find(i => i.id === handle.islandId);
      const entries = (isl?.shapeIds ?? []).map(id => [id, this.#shapes.get(id)]).filter(([, s]) => s);
      this.#dragStartShapes = new Map(entries.map(([id, s]) => [id, structuredClone(s)]));
      this.#dragStartShape = null;
    } else {
      const s = this.#shapes.get(handle);
      this.#dragStartShape = s ? structuredClone(s) : null;
      this.#dragStartShapes = null;
    }
  }

  // Absolute, snap-aware move (S9): place the shape at start + (dx,dz), snapping its bbox edges/centre to
  // other shapes' edges/centres + the symmetry centre; draws alignment guides. Alt bypasses snapping.
  _moveTo(handle, dx, dz, alt) {
    if (this.#isIslandHandle(handle)) return this.#moveIslandTo(dx, dz, alt);
    const start = this.#dragStartShape;
    if (!start || start.id !== handle) return false;
    const sb = toBounds(start);
    let sdx = dx, sdz = dz, gx = null, gz = null;
    if (sb && this.#snapEnabled && !alt) {
      const tol = 6 / (this._scale || 1);
      const { xs, zs } = this.#snapTargets(handle);
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

  // Whole-island absolute move: snap the island's bbox edges/centre to other shapes + the symmetry centre
  // (excluding every island member), draw the guide, and translate all members from their snapshots.
  #moveIslandTo(dx, dz, alt) {
    const starts = this.#dragStartShapes;
    if (!starts || !starts.size) return false;
    const sb = boundsOfShapes([...starts.values()]);
    let sdx = dx, sdz = dz, gx = null, gz = null;
    if (sb && this.#snapEnabled && !alt) {
      const tol = 6 / (this._scale || 1);
      const { xs, zs } = this.#snapTargets([...starts.keys()]);
      const sx = bestSnap([sb.min_x + dx, (sb.min_x + sb.max_x) / 2 + dx, sb.max_x + dx], xs, tol);
      const sz = bestSnap([sb.min_z + dz, (sb.min_z + sb.max_z) / 2 + dz, sb.max_z + dz], zs, tol);
      if (sx) { sdx = dx + sx.adjust; gx = sx.line; }
      if (sz) { sdz = dz + sz.adjust; gz = sz.line; }
    }
    this.#renderGuides(gx, gz);
    const rdx = Math.round(sdx), rdz = Math.round(sdz);
    for (const [, start] of starts) this.updateShape(translateShape(start, rdx, rdz));
    this.#renderIslandChrome();
    this.#callbacks.onShapeUpdated?.();   // one island recompute for the whole move
    return true;
  }

  _commitMove() { this.#dragStartShape = null; this.#dragStartShapes = null; this.#renderGuides(null, null); }

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
  // `exclude` is a shape id or a list of ids (all members of a dragged island).
  #snapTargets(exclude) {
    const ex = Array.isArray(exclude) ? new Set(exclude) : new Set([exclude]);
    const xs = new Set([this.#center.cx]), zs = new Set([this.#center.cz]);
    for (const [id, s] of this.#shapes) {
      if (ex.has(id)) continue;
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

  // Draw the selected island's bounding box + corner anchors (screen-space, so legible at any zoom), plus
  // the four rotate zones just OUTSIDE the corners (S13 — hover shows the rotate cursor, drag rotates the
  // island). The edges are reserved for scale (S21). Re-rendered on selection + every viewport change.
  #renderIslandChrome() {
    const layer = this.#islandChromeLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    if (!this.#selectedIslandId) return;
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    if (!isl) return;
    const members = (isl.shapeIds ?? []).map(id => this.#shapes.get(id)).filter(Boolean);
    const b = boundsOfShapes(members);
    if (!b) return;
    const x0 = b.min_x * this._scale + this._panX, y0 = b.min_z * this._scale + this._panY;
    const x1 = b.max_x * this._scale + this._panX, y1 = b.max_z * this._scale + this._panY;
    const l = Math.min(x0, x1), r = Math.max(x0, x1), t = Math.min(y0, y1), bot = Math.max(y0, y1);
    layer.appendChild(svgEl("rect", {
      x: l, y: t, width: r - l, height: bot - t,
      fill: "none", stroke: "var(--accent)", "stroke-width": "1.5", "stroke-dasharray": "5 3", "pointer-events": "none",
    }));
    const HALF = 4, OUT = 9, ZONE = 9;   // anchor half-size · rotate-zone offset outward · rotate-zone half
    // Rotate zones just outside the four corners (all islands; transparent fill so they still hit-test).
    for (const [ax, ay, sx, sy] of [[l, t, -1, -1], [r, t, 1, -1], [r, bot, 1, 1], [l, bot, -1, 1]]) {
      const zone = svgEl("rect", { x: ax + sx * OUT - ZONE, y: ay + sy * OUT - ZONE, width: ZONE * 2, height: ZONE * 2, fill: "transparent" });
      zone.style.cursor = ROTATE_CURSOR;
      zone.addEventListener("mousedown", (e) => this.#startRotate(e));
      layer.appendChild(zone);
    }
    // Scale handles (corner + edge, S21) show for a multi-shape island and for a single non-rectangle
    // member (a lone rectangle already squashes via its own 8-handle resize, so it just gets inert corner
    // markers). A single polygon/lasso keeps its vertex handles on top for point editing.
    const soleRect = members.length === 1 && members[0]?.type === "rectangle";
    if (!soleRect) {
      for (const hd of SCALE_HANDLES) {
        const hx = l + hd.nx * (r - l), hy = t + hd.nz * (bot - t);
        const h = svgEl("rect", {
          x: hx - HALF, y: hy - HALF, width: HALF * 2, height: HALF * 2, rx: 1,
          fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5",
        });
        h.style.cursor = hd.cur;
        h.addEventListener("mousedown", (e) => this.#startScale(e, hd));
        layer.appendChild(h);
      }
    } else {
      for (const [ax, ay] of [[l, t], [r, t], [r, bot], [l, bot]]) {
        layer.appendChild(svgEl("rect", {
          x: ax - HALF, y: ay - HALF, width: HALF * 2, height: HALF * 2, rx: 1,
          fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", "pointer-events": "none",
        }));
      }
    }
  }

  // Begin scaling the selected island via a bbox handle: freeze its original bbox + snapshot every member;
  // #scaleMove then derives the per-axis factors from the cursor and re-applies from those snapshots.
  #startScale(e, hd) {
    if (e.button !== 0 || !this.#selectedIslandId) return;
    e.stopPropagation();
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    const members = (isl?.shapeIds ?? []).map(id => this.#shapes.get(id)).filter(Boolean);
    const b = boundsOfShapes(members);
    if (!b) return;
    this.#scaleState = { snapshots: new Map(members.map(s => [s.id, structuredClone(s)])), orig: b, h: hd };
  }

  // Scale the island: per axis, factor = (cursor − anchor) / (originalHandle − anchor), anchored on the
  // opposite corner/edge (or the centre with Alt). Shift locks a corner to a uniform scale. Clamped so the
  // island can't collapse or flip (each axis stays >= 1 block).
  #scaleMove(wx, wz, shift, alt) {
    const st = this.#scaleState; if (!st) return;
    const o = st.orig, h = st.h;
    const cx = (o.min_x + o.max_x) / 2, cz = (o.min_z + o.max_z) / 2;
    const anchorX = alt ? cx : (h.nx === 1 ? o.min_x : o.max_x);
    const anchorZ = alt ? cz : (h.nz === 1 ? o.min_z : o.max_z);
    const handleX = h.nx === 1 ? o.max_x : o.min_x, handleZ = h.nz === 1 ? o.max_z : o.min_z;
    const div = (a, b) => Math.abs(b) < 1e-9 ? 1 : a / b;
    let sx = h.axX ? div(wx - anchorX, handleX - anchorX) : 1;
    let sz = h.axZ ? div(wz - anchorZ, handleZ - anchorZ) : 1;
    if (shift && h.axX && h.axZ) { const s = Math.abs(sx - 1) >= Math.abs(sz - 1) ? sx : sz; sx = s; sz = s; }
    sx = Math.max(sx, 1 / Math.max(o.max_x - o.min_x, 1));   // no collapse / flip (extent stays >= 1 block)
    sz = Math.max(sz, 1 / Math.max(o.max_z - o.min_z, 1));
    for (const [, snap] of st.snapshots) this.updateShape(scaleShape(snap, sx, sz, [anchorX, anchorZ]));
    this.#renderIslandChrome();
    this.#callbacks.onShapeUpdated?.();
  }

  // Begin rotating the selected island (Figma model): freeze the pivot = its bbox centre + snapshot every
  // member, then #rotateMove re-applies the accumulated angle from those snapshots each drag step.
  #startRotate(e) {
    if (e.button !== 0 || !this.#selectedIslandId) return;
    e.stopPropagation();
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    const members = (isl?.shapeIds ?? []).map(id => this.#shapes.get(id)).filter(Boolean);
    const b = boundsOfShapes(members);
    if (!b) return;
    const pivot = [(b.min_x + b.max_x) / 2, (b.min_z + b.max_z) / 2];
    const w = this._clientToSvg(e.clientX, e.clientY);
    this.#rotateState = {
      snapshots: new Map(members.map(s => [s.id, structuredClone(s)])),
      pivot, lastAngle: Math.atan2(w.y - pivot[1], w.x - pivot[0]), total: 0,
    };
  }

  // Rotate the island to the accumulated angle from the cursor's swept angle about the pivot (distance-
  // independent, unwrapped across ±π so you can spin past 360°). Shift snaps the total to 15°.
  #rotateMove(wx, wz, shift) {
    const st = this.#rotateState;
    if (!st) return;
    const cur = Math.atan2(wz - st.pivot[1], wx - st.pivot[0]);
    let d = cur - st.lastAngle;
    while (d > Math.PI) d -= 2 * Math.PI;
    while (d < -Math.PI) d += 2 * Math.PI;
    st.total += d;
    st.lastAngle = cur;
    let angle = st.total;
    if (shift) { const step = Math.PI / 12; angle = Math.round(angle / step) * step; }   // 15°
    for (const [, snap] of st.snapshots) this.updateShape(rotateShape(snap, angle, st.pivot));
    this.#renderIslandChrome();
    this.#callbacks.onShapeUpdated?.();
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

    this.#islandChromeLayer = svgEl("g");   // island bbox (inert) + interactive rotate zones, below the handles
    this.#handlesLayer      = svgEl("g");
    this.#centerLayer       = svgEl("g", { "pointer-events": "none" });
    this.#drawHandlesLayer  = svgEl("g", { "pointer-events": "none" });
    this.#measureLabelLayer = svgEl("g", { "pointer-events": "none" });
    for (const g of [this.#islandChromeLayer, this.#handlesLayer, this.#centerLayer, this.#drawHandlesLayer, this.#measureLabelLayer]) this._svg.appendChild(g);

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
      if (e.key === "Escape") {
        this.#draw.cancel(); this.#clearMeasure(); this.#clearSplit();
        // Drilled into a member → pop back out to its island; otherwise clear the selection.
        if (this.#selectedId && !this.#selectedIslandId) {
          const parent = this.#islandOfShape(this.#selectedId);
          if (parent) { this.#callbacks.onIslandSelected?.(parent.id); return; }
        }
        if (this.#selectedIslandId || this.#selectedId) this.#callbacks.onIslandSelected?.(null);
      }
      if ((e.key === "Delete" || e.key === "Backspace") && this.#selectedId) {
        this.#callbacks.onShapeDeleted?.(this.#selectedId);
      }
      if ((e.key === "p" || e.key === "P") && this.#selectedId) this.#callbacks.onShapePromote?.(this.#selectedId);
    });
    // Double-click closes a polygon while drawing; in select mode it drills into the member shape under
    // the cursor (Figma group model — single-click picks the island, double-click enters a member).
    this._svg.addEventListener("dblclick", (e) => {
      if (this._activeTool === "polygon") { e.stopPropagation(); this.#draw.onDblClick(); return; }
      if (this.#isoOn || (this._activeTool !== null && this._activeTool !== "select")) return;
      const p = this._clientToSvg(e.clientX, e.clientY);
      const shapeId = this.#hitTest(p.x, p.y);
      if (shapeId) this.#callbacks.onShapeSelected?.(shapeId);   // drill
    });
  }

  #renderSetup() {
    renderBbox(this.#bboxLayer, this.#bbox, identityTransform);
    renderChunkGrid(this.#chunkLayer, this.#bbox, identityTransform);
    renderAxis(this.#axisLayer, this.#bbox, this.#center, this.#mode, identityTransform);
  }

  // Build the shape group (render layer). Selection is handled canvas-wide (single-click = island,
  // double-click = drill to shape) via the svg-level click/dblclick + hit-testing, so no per-shape
  // click listener — a click bubbles to the svg handler.
  #shapeEl(shape) {
    const g = renderSketchShape(shape, identityTransform);
    g.style.cursor = "pointer";
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

  // Split tool (S14): first click sets the cut's start + a preview line; the second click fires onSplit
  // (the host cuts the crossed shape into two). The slice line lives in the viewport draw layer.
  #onSplitClick(bx, bz) {
    if (!this.#split) {
      this.#split = { ax: bx, az: bz };
      this.#splitLine = svgEl("line", {
        x1: bx, y1: bz, x2: bx, y2: bz, stroke: "var(--canvas-sub-stroke)", "stroke-width": "1.5",
        "stroke-dasharray": "5 3", "vector-effect": "non-scaling-stroke", "pointer-events": "none",
      });
      this.#drawLayer.appendChild(this.#splitLine);
    } else {
      this.#callbacks.onSplit?.([this.#split.ax, this.#split.az], [bx, bz]);
      this.#clearSplit();
    }
  }
  #clearSplit() {
    this.#split = null;
    if (this.#splitLine?.parentNode) this.#splitLine.parentNode.removeChild(this.#splitLine);
    this.#splitLine = null;
  }

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

  // The island whose footprint (exterior minus holes) contains (wx,wz), topmost first; null if none.
  #hitIsland(wx, wz) {
    for (let i = this.#islands.length - 1; i >= 0; i--) {
      const isl = this.#islands[i];
      if (isl.exterior?.length >= 4 && pointInRing(wx, wz, isl.exterior) &&
          !(isl.holes ?? []).some(h => pointInRing(wx, wz, h))) return isl.id;
    }
    return null;
  }
}
