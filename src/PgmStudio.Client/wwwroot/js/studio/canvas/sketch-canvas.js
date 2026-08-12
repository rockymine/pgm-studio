/**
 * SketchCanvas — the drawing surface for the Sketch tool. Extends CanvasBase for pan/zoom/drag and
 * delegates: draw tools → SketchDrawController, resize/vertex/Bézier edit → SketchEditController,
 * all world painting → render/sketch-render, point-in-shape → geometry/shape.containsPoint. It owns the
 * shape map, selection, and the bbox/center/mode setup state; the host (bridge/sketch-bridge.js) owns
 * the island recompute loop and pushes results via setIslands/setMirror.
 *
 * A hybrid surface, like the plan canvas: the world layers are **painted** to a 2-D `<canvas>` under the
 * svg and redrawn each frame at the current scale, so no rasterization is kept for a zoom to stretch;
 * screen-space chrome (island bbox + handles, the centre marker, the ruler label, the scale bar) stays in
 * the svg, which also remains the single pointer target. Nothing is retained between frames, so every
 * setter ends in a repaint rather than in an element edit.
 *
 * World coordinates ARE the surface coordinates here — an identity transform (no buildTransform);
 * fitToBbox() sets scale/pan to frame the working bounds. (Distinct from WorldCanvas, which maps
 * through buildTransform — do not collapse the two.)
 *
 * Callbacks: onShapeCreated(partial) · onShapeUpdated(shape) · onShapeSelected(id|null) [drill] ·
 * onIslandSelected(id|null) [single-click] · onShapeDeleted(id) · onSplit(a, b) [slice a shape in two]
 */

import { CanvasBase } from "./canvas-base.js";
import { svgEl } from "../render/svg.js";
import { layerStack, INERT } from "../render/layer-stack.js";
import { CanvasPainter } from "../render/canvas-painter.js";
import { containsPoint, toBounds, translateShape, boundsOfShapes, rotateShape, scaleShape, toRing, snapShape } from "../geometry/shape.js";
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
import { DressingController } from "../controllers/dressing-controller.js";
import { DressingDoc } from "../dressing/dressing-doc.js";
import { paintDressing } from "../render/dressing-render.js";
import { ReliefController } from "../controllers/relief-controller.js";
import { ReliefDoc } from "../relief/relief-doc.js";
import { paintReliefMarks } from "../render/relief-render.js";
import { orbitAxes, applySymmetry } from "../geometry/symmetry.js";
import { SketchEditController } from "../controllers/sketch-edit-controller.js";
import {
  paintSketchShape, paintIslands, paintMirror, paintBbox, paintChunkGrid, paintAxis, paintGhostIslands, paintRaster, paintStructural,
  paintContours,
} from "../render/sketch-render.js";
import { rasterizeShapes, cellRuns } from "../geometry/rasterize.js";
import { loadBlockImage, blockImageBounds } from "../render/block-render.js";
import { viewportWorldRect, snapOut, gridStep, paintWorkArea, renderScaleBar } from "../render/canvas-chrome.js";
// iso-webgl is loaded lazily (on first 3-D toggle) so a missing/blocked WebGL stack — or any failure
// to load that module — degrades to "no 3-D preview" instead of breaking the whole editor at page load.

const FIT_MARGIN = 0.85;

// The **working area** — the tinted region that anchors how big a map should be. It exists at a default
// size on a blank sketch (4×4 chunks about the origin) and grows to enclose the content plus one chunk of
// buffer, snapped to chunk lines. Drawing past it visibly grows it; it is a size anchor, not a fence.
const CHUNK = 16;
const VIEW_BUFFER = 16;
const MIN_HALF = 32;

// The chunk grid and the symmetry axis span the VISIBLE VIEWPORT, not the working area — so there is
// always grid (and drawing surface) outside it and growing the sketch is just drawing where you already
// see grid. GRID_SNAP is the multiple the visible extent snaps out to: coarser than CHUNK so panning
// redraws the lines every four chunks of travel instead of every one.
const GRID_SNAP = CHUNK * 4;

// fitToBbox frames the working area with this much of it again added on each side, so the tinted region
// sits inside a visible margin of grid rather than filling the surface edge to edge.
const FIT_PAD_FRACTION = 0.2;

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
  #bbox    = null;    // the current working bounds (content + buffer); recomputed by #renderSetup
  #tight   = null;    // the exact content bounds — the frame outline, and null when the sketch is empty
  #center  = { cx: 0, cz: 0 };
  #mode    = "rot_180";

  #shapes      = new Map();   // id → shape (source for paint / hit-test / edit)
  #structural  = [];          // locked plan pieces (S25) — render-only, never hit-tested/edited/rasterized
  #selectedId  = null;        // drilled/single-member shape (drives the edit-controller handles)
  #selectedIslandId = null;   // selected island (drives the island bbox chrome + whole-island drag)
  #islands     = [];          // [{ id, shapeIds, exterior, holes }] from the bridge
  #mirrorPolys = [];
  #ghostPolys  = [];          // other layers' island outlines (S7)

  // Placed dressing (decoration.md). The document lives here rather than in the bridge because the canvas is
  // where a prop is put, moved and picked; the bridge asks for it when it saves.
  #dressingDoc = new DressingDoc();
  #dressing    = null;
  #dressingOn  = false;      // only the Dressing phase draws and edits props

  // The stated relief (docs/contracts/sketch-relief.md). Here for the same reason the dressing document is:
  // this is where a mark is put, moved and picked. Unlike dressing it is keyed by island, because a relief is
  // solved over an island's fused footprint rather than placed loose on the map.
  #reliefDoc   = new ReliefDoc();
  #reliefTools = null;
  #reliefOn    = false;      // only the Relief phase draws and edits marks
  // A placement hands the canvas back to select mid-press, so the click that finished it would arrive with
  // select already active and be read as "pick the island under the cursor" — deselecting the prop just put
  // down. This marks that click as spent. Cleared on the next press, so it can only ever swallow its own.
  #placementClick = false;

  #shapesVisible = false;
  #mirrorVisible = true;
  #chunkVisible  = true;
  #blocksVisible = false;
  #rasterRuns    = null;   // cached rasterized cell runs; recomputed on shape change while blocks are shown
  #paintData     = null;   // the server's block-pixel payload for the terrain paint (finishing-model.md §4)
  #paintImage    = null;   // that payload decoded — a canvas blits a bitmap, not a data URL
  #relief        = null;   // the server's traced contours for the relief the layout carries
  #selectOnly    = false;  // Theme phase: pick islands/shapes and pan/zoom, edit nothing

  #draw = null;
  #edit = null;
  #callbacks = {};
  #cursorEl  = null;
  #zoomEl    = null;
  #dimEl     = null;
  #measure   = null;   // { ax, az, bx, bz, live } — the ruler measurement (drag across a void gap)
  #split     = null;   // { ax, az, bx, bz } — the first cut point (S14) + the cursor, awaiting the second click
  #guides     = { x: null, z: null };   // alignment guide lines drawn during a snapped move/resize
  #dragStartShape = null;  // snapshot of the grabbed shape at drag start (absolute snap-aware move, S9)
  #dragStartShapes = null; // id→snapshot of every member when body-dragging a whole island (S20)
  #rotateState = null;     // { snapshots, pivot, lastAngle, total } while rotating a selected island (S13)
  #scaleState = null;      // { snapshots, orig, h } while scaling a selected island via a bbox handle (S21)
  #snapEnabled = true;

  // The world layers are PAINTED: `#painter` owns a <canvas> under the svg and redraws the whole world
  // each frame at the current scale. Screen-space chrome stays in `#screen`, where nothing is transformed
  // and DOM semantics (a cursor, a mousedown on a handle) are worth having.
  #painter  = null;
  #canvasEl = null;
  #screen = {};  // screen-space layers (outside the viewport transform)

  constructor(svgEl_, wrapEl, { cursorEl, zoomEl, dimEl, ...callbacks } = {}) {
    super(svgEl_, wrapEl);
    this.#callbacks = callbacks;
    this.#cursorEl  = cursorEl ?? null;
    this.#zoomEl    = zoomEl ?? null;
    this.#dimEl     = dimEl ?? null;
    // The painted surface is created here rather than in the host markup, so the Blazor component and the
    // bridge signature are unchanged by what the world layers are drawn with.
    this.#canvasEl = document.createElement("canvas");
    this.#canvasEl.className = "world-canvas-2d";
    this._svg.parentNode.insertBefore(this.#canvasEl, this._svg);
    this.#painter = new CanvasPainter(this.#canvasEl);
    this.#build();
  }

  // ── public API ───────────────────────────────────────────────────────────────

  // The frame is no longer author-set — the grid auto-grows to the content. setBbox is kept for the
  // bridge's load path but its value is ignored; the bounds come from the drawn shapes + mirror.
  setBbox()            { this.#renderSetup(); }
  setCenter(cx, cz)    { this.#center = { cx, cz }; this.#renderSetup(); this.#refreshCenter(); }
  setMode(mode)        { this.#mode = mode; this.#renderSetup(); }
  setOperation(op)     { this.#draw?.setOperation(op); }

  // Frame the working area, with a margin of grid visible around it. Deferred when the wrap has no layout
  // box yet — the tool mounts this canvas while the Draw phase is still display:none, and _size()'s
  // fallback would frame the view for a viewport that isn't the real one.
  fitToBbox() {
    if (!this._hasBox()) { this._fitPending = true; return; }
    this._fitWorldBox(this.#bbox, { margin: FIT_MARGIN, pad: FIT_PAD_FRACTION });
  }
  _fit() { this.fitToBbox(); }

  resize() {
    const { w, h } = this._resizeSvg();
    this.#painter.resize(w, h);   // the backing store follows the box, and re-reads the pixel ratio
    this.#paintWorld();           // a bigger/smaller viewport shows more/less grid
    this.#edit?.refresh();
    this.#dressing?.refreshHandles();
    this.#refreshCenter();
  }

  dispose() {
    this.#painter?.dispose();
    this.#canvasEl?.remove();
    this._disposeCanvasBase();
  }

  setActiveTool(tool) {
    this.#draw?.cancel();
    if (this._activeTool === "measure" && tool !== "measure") this.#clearMeasure();
    if (this._activeTool === "split" && tool !== "split") this.#clearSplit();
    this._activeTool = tool;
    const isDraw = tool !== null && tool !== "move" && tool !== "select";
    this._svg.style.cursor = isDraw ? "crosshair" : (tool === "select" ? "default" : "");
  }

  // addShape / updateShape are the single chokepoint through which every drawn, moved, resized, rotated,
  // scaled or vertex-edited shape reaches the stored map — so snapping here (S23) makes "shapes are
  // block-integer" an invariant of the store, no matter which edit path produced the shape. A live drag
  // re-applies from a pristine snapshot each frame, so snapping every frame gives a block-accurate preview
  // without accumulating rounding error.
  addShape(shape) {
    this.#shapes.set(shape.id, snapShape(shape));
    this.#renderSetup();   // grow the grid/frame to include the new shape, and repaint
  }

  updateShape(shape) {
    this.#shapes.set(shape.id, snapShape(shape));
    if (this.#selectedId === shape.id) this.#edit?.refresh();
    this.#renderSetup();   // follow the shape as it's dragged/resized past the current edge
  }

  removeShape(id) {
    this.#shapes.delete(id);
    if (this.#selectedId === id) { this.#selectedId = null; this.#edit?.setSelected(null); this.#edit?.refresh(); }
    this.#renderSetup();   // shrink back when content is removed
  }

  clearShapes() { for (const id of [...this.#shapes.keys()]) this.removeShape(id); }

  // Select a shape (the drill / panel-shape path): shows its edit handles, clears any island bbox chrome.
  selectShape(id) {
    this.#selectedIslandId = null;
    this.#selectedId = id;
    this.#edit?.setSelected(id);
    this.#edit?.refresh();
    this.#renderIslandChrome();
    this.#paintWorld();
    this.#updateDim();
  }

  // Select an island (single-click / panel-island): draws its bbox chrome; a single-member island also
  // shows that member's edit handles (nothing to drill into). Setter only — the click path fires the callback.
  selectIsland(id) {
    this.#selectedIslandId = id ?? null;
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    this.#selectedId = (isl && isl.shapeIds?.length === 1) ? isl.shapeIds[0] : null;
    this.#edit?.setSelected(this.#selectedId);
    this.#edit?.refresh();
    this.#renderIslandChrome();
    this.#paintWorld();
    this.#updateDim();
  }

  getShape(id)  { return this.#shapes.get(id); }
  getShapes()   { return [...this.#shapes.values()]; }
  get selectedId() { return this.#selectedId; }
  #islandOfShape(shapeId) { return this.#islands.find(i => i.shapeIds?.includes(shapeId)) ?? null; }

  // Whether a cell centre sits on the rasterized terrain — inside some island's footprint and clear of its
  // holes. The same exterior-minus-holes test the island hit-test uses, at the cell's own centre so it agrees
  // with the rasterizer's membership rule (a cell is terrain when its centre is inside a shape).
  #cellOnTerrain(bx, bz) {
    const cx = bx + 0.5, cz = bz + 0.5;
    return this.#islands.some(isl => (isl.exterior?.length ?? 0) >= 4
      && pointInRing(cx, cz, isl.exterior)
      && !(isl.holes ?? []).some(hole => pointInRing(cx, cz, hole)));
  }

  setIslands(islands)        { this.#islands = islands ?? []; this.#paintWorld(); this.#renderIslandChrome(); }
  setGhostIslands(polys)     { this.#ghostPolys = polys ?? []; this.#paintWorld(); }
  setMirrorPolygons(polys)   { this.#mirrorPolys = polys ?? []; this.#renderSetup(); }
  /** Show and edit placed dressing — on for the Dressing phase, off everywhere else. */
  setDressingMode(on) {
    this.#dressingOn = !!on;
    if (!on) { this.#dressing?.cancel(); this.#dressing?.select(null); }
    this.#dressing?.refreshHandles();
    this.#paintWorld();
  }
  setDressing(stored) { this.#dressingDoc = DressingDoc.from(stored); this.#dressing?.setDoc(this.#dressingDoc); this.#paintWorld(); }
  get dressing()      { return this.#dressingDoc; }
  get dressingTools() { return this.#dressing; }

  /** Show and edit the stated relief — on for the Relief phase, off everywhere else. Distinct from the
   *  Relief layer chip, which shows the *solved* contours and is available in every phase: one is the
   *  statement, the other is what the statement produced, and seeing the second while editing the first is
   *  the whole point of keeping them separate. */
  setReliefMode(on) {
    this.#reliefOn = !!on;
    if (!on) { this.#reliefTools?.cancel(); this.#reliefTools?.select(null); }
    this.#reliefTools?.refreshHandles();
    this.#paintWorld();
  }
  setReliefDoc(stored) { this.#reliefDoc = ReliefDoc.from(stored); this.#reliefTools?.setDoc(this.#reliefDoc); this.#paintWorld(); }
  get relief()      { return this.#reliefDoc; }
  get reliefTools() { return this.#reliefTools; }

  setShapesVisible(v) { this.#shapesVisible = v; this.#paintWorld(); }
  setMirrorVisible(v) { this.#mirrorVisible = v; this.#paintWorld(); }
  setChunkVisible(v)  { this.#chunkVisible = v; this.#paintWorld(); }
  setBlocksVisible(v) { this.#blocksVisible = v; this.#rebuildRaster(); this.#paintWorld(); }

  // Rasterize the drawn shapes into the block cells they voxelize into (the S23 WYSIWYG preview), merged
  // into horizontal runs so the fill is a few hundred rects, not one per block. Cached — recomputed only on
  // a shape change while the Blocks layer is on, not per frame. (Only the primary footprint; the mirror
  // copies already read as smooth polygons on their own layer.)
  #rebuildRaster() {
    this.#rasterRuns = this.#blocksVisible ? cellRuns(rasterizeShapes([...this.#shapes.values()])) : null;
  }

  /**
   * Make the canvas a selection surface: islands and shapes can be picked (and the view panned and zoomed),
   * but nothing can be moved, resized, rotated, reshaped or given a vertex. The Theme phase runs in this
   * mode — it assigns paint to geometry the Draw phase owns, so offering an edit there would let an author
   * change the map from a rail that has no undo, no snapping and no height controls to make sense of it.
   *
   * Restricted at the source rather than by ignoring the results: the edit controller draws no handles, the
   * island chrome draws no rotate/scale grips, and `_hitMovable` reports nothing draggable — so a drag has
   * nothing to begin on, instead of beginning and being discarded.
   */
  setSelectOnly(on) {
    this.#selectOnly = !!on;
    this.#edit?.setEnabled(!this.#selectOnly);
    this.#renderIslandChrome();
  }

  /**
   * The terrain paint as a block-pixel payload (`{xs, zs, colors, min_x, min_z, max_x, max_z}` — the shape the
   * top-surface overlay returns), decoded once into a bitmap the Blocks layer blits: one opaque pixel per
   * block, the colour of the block the export places there. This is the whole theme — voronoi cells, noise
   * fields, wall runs, rims, team tints — because the server paints it through the real painter rather than
   * the client approximating a theme by one representative colour. A falsy payload drops back to the plain
   * stone footprint.
   */
  loadPaintLayer(data) {
    this.#paintData = (data && (data.runs?.length || data.xs?.length)) ? data : null;   // either wire form
    this.#paintImage = null;
    if (!this.#paintData) { this.#paintWorld(); return; }
    loadBlockImage(this.#paintData, (image) => {
      if (this.#paintData !== data) return;   // a newer payload arrived while this one decoded
      this.#paintImage = image;
      this.#paintWorld();
    });
  }

  /**
   * The relief overlay: the traced contours `sketch/relief` returns for the layout on screen. A falsy
   * payload clears it. Nothing is decoded and nothing is cached — the lines are world-space points and are
   * stroked at whatever the current zoom is, so the overlay stays sharp where the block bitmap would have
   * had to be re-fetched.
   */
  loadReliefLayer(relief) {
    this.#relief = relief?.islands?.length ? relief : null;
    this.#paintWorld();
  }

  /** Whether a relief is on screen — what a phase asks before offering to read a height off it. */
  get hasRelief() { return this.#relief !== null; }

  // ── isometric preview (S6) ─────────────────────────────────────────────────────
  // Swap the top-down viewport for a read-only iso render of the extruded islands. Lazily loads and
  // creates the WebGL renderer on first use; returns false (leaving the 2-D viewport untouched) if the
  // preview module can't load or WebGL is unavailable, so the caller can fall back gracefully.
  showIso(islands, yawDeg, bbox) { return this._showIso(islands, yawDeg, bbox); }
  hideIso() { this._hideIso(); }

  _isoTag() { return "sketch"; }
  _onIsoEnter() { this.#draw?.cancel(); }
  // What the preview covers: the painted world, the (inert) viewport group, and every screen-space overlay.
  _isoLayers() {
    const { handles, dressingHandles, reliefHandles, center, drawHandles, measureLabel, islandChrome } = this.#screen;
    return [this.#canvasEl, this._viewportG, handles, dressingHandles, reliefHandles, center, drawHandles, measureLabel, islandChrome];
  }

  // ── CanvasBase hooks ───────────────────────────────────────────────────────────

  _onViewportChanged() { this.#paintWorld(); this.#edit?.refresh(); this.#dressing?.refreshHandles(); this.#reliefTools?.refreshHandles(); this.#refreshCenter(); this.#draw?.refreshDrawHandles(); this.#renderMeasureLabel(); this.#renderIslandChrome(); }
  _onZoom(scale)       { if (this.#zoomEl) this.#zoomEl.textContent = `${Math.round(scale * 100)}%`; }

  _onToolMousedown(e, svgPt) {
    if (this._isoOn) return;   // iso preview is read-only
    this.#placementClick = false;
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this._activeTool === "measure") { this.#measure = { ax: bx, az: bz, bx, bz, live: true }; this.#renderMeasure(); this.#updateDim(); return; }
    if (this._activeTool === "split") { this.#onSplitClick(bx, bz); return; }
    if (this.#reliefOn && this.#reliefTools?.onMouseDown(bx, bz, this._activeTool)) return;
    if (this.#dressingOn && this.#dressing?.onMouseDown(bx, bz, this._activeTool)) return;
    this.#draw?.onMouseDown(bx, bz, this._activeTool);
  }

  _onPointerMove(e, svgPt) {
    const bx = Math.floor(svgPt.x), bz = Math.floor(svgPt.y);
    if (this.#cursorEl) this.#cursorEl.textContent = `X ${bx}  Z ${bz}`;
    if (this._activeTool === "measure") {
      if (this.#measure?.live) { this.#measure.bx = bx; this.#measure.bz = bz; this.#renderMeasure(); }
    } else if (this._activeTool === "split") {
      if (this.#split) { this.#split.bx = bx; this.#split.bz = bz; this.#paintWorld(); }
    } else if (this.#reliefOn && this.#reliefTools?.onMouseMove(bx, bz, this._activeTool)) {
      // consumed by a trace, a spot ghost or a mark being dragged
    } else if (this.#dressingOn && this.#dressing?.onMouseMove(bx, bz, this._activeTool)) {
      // consumed by a trace, a marker ghost or a prop being dragged
    } else {
      this.#draw?.onMouseMove(bx, bz);
    }
    this.#updateDim();
    this.#edit?.onPointerMove(svgPt.x, svgPt.y, this._activeTool);
  }

  _onToolMouseup(e, svgPt) {
    if (this._activeTool === "measure") { if (this.#measure) this.#measure.live = false; return; }
    if (this.#reliefOn && this.#reliefTools?.onMouseUp()) return;
    if (this.#dressingOn && this.#dressing?.onMouseUp()) return;
    this.#draw?.onMouseUp();
  }

  // Single-click selects the containing ISLAND (null = deselect); double-click drills to the shape (below).
  _onCanvasClick(e, svgPt) {
    if (this._isoOn) return;
    if (this.#placementClick) { this.#placementClick = false; return; }
    this.#callbacks.onIslandSelected?.(this.#hitIsland(svgPt.x, svgPt.y));
  }

  _onMouseleave() { if (this.#cursorEl) this.#cursorEl.textContent = ""; this.#updateDim(); }

  _onResizeMove(e) {
    if (this.#dressing || this.#reliefTools) {
      const p = this._clientToSvg(e.clientX, e.clientY);
      if (this.#dressing?.onHandleMove(p.x, p.y)) return true;
      if (this.#reliefTools?.onHandleMove(p.x, p.y)) return true;
    }
    if (this.#rotateState) { const p = this._clientToSvg(e.clientX, e.clientY); this.#rotateMove(p.x, p.y, e.shiftKey); return true; }
    if (this.#scaleState)  { const p = this._clientToSvg(e.clientX, e.clientY); this.#scaleMove(p.x, p.y, e.shiftKey, e.altKey); return true; }
    if (!this.#edit) return false;
    const p = this._clientToSvg(e.clientX, e.clientY);
    return this.#edit.onResizeMove(p.x, p.y, e.altKey);
  }
  _onResizeUp(e) {
    if (e.button === 0 && this.#dressing?.onHandleUp()) return true;
    if (e.button === 0 && this.#reliefTools?.onHandleUp()) return true;
    if (this.#rotateState) { if (e.button !== 0) return false; this.#rotateState = null; return true; }
    if (this.#scaleState)  { if (e.button !== 0) return false; this.#scaleState = null; return true; }
    const consumed = e.button === 0 ? (this.#edit?.onResizeUp() ?? false) : false;
    this.#setGuides(null, null);   // drop any resize alignment guide
    return consumed;
  }

  // Body-drag (CV10 shape / S20 island): drag a selected shape's body — or a whole selected island — to
  // move it. World == surface coords here, so the default _toWorld (identity) is correct — no override.
  // A shape handle is its id (string); an island handle is `{ islandId }`.
  #isIslandHandle(h) { return !!(h && typeof h === "object" && h.islandId); }

  _hitMovable(world) {
    if (this._isoOn || this.#selectOnly) return null;   // Theme phase: a selected thing is not draggable
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
    this.#setGuides(gx, gz);
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
    this.#setGuides(gx, gz);
    const rdx = Math.round(sdx), rdz = Math.round(sdz);
    for (const [, start] of starts) this.updateShape(translateShape(start, rdx, rdz));
    this.#renderIslandChrome();
    this.#callbacks.onShapeUpdated?.();   // one island recompute for the whole move
    return true;
  }

  _commitMove() { this.#dragStartShape = null; this.#dragStartShapes = null; this.#setGuides(null, null); }

  // Snap-aware rectangle resize: snap the dragged edge coord(s) to other shapes' edges/centres + the
  // symmetry centre, draw the alignment guide, and return the (possibly) adjusted coords. The resize
  // counterpart of _moveTo's snapping; fed to the edit controller as its `snapEdges` hook. `edges` = the
  // proposed dragged-edge values `{ x, z }` (either may be null when that axis isn't dragged). Alt or the
  // Snap toggle off → pass through unchanged and clear the guide.
  #snapResize(excludeId, edges, alt) {
    if (!this.#snapEnabled || alt) { this.#setGuides(null, null); return edges; }
    const tol = 6 / (this._scale || 1);
    const { xs, zs } = this.#snapTargets(excludeId);
    let gx = null, gz = null;
    const out = { x: edges.x, z: edges.z };
    if (edges.x != null) { const s = bestSnap([edges.x], xs, tol); if (s) { out.x = s.line; gx = s.line; } }
    if (edges.z != null) { const s = bestSnap([edges.z], zs, tol); if (s) { out.z = s.line; gz = s.line; } }
    this.#setGuides(gx, gz);
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

  #setGuides(gx, gz) {
    if (this.#guides.x === gx && this.#guides.z === gz) return;
    this.#guides = { x: gx, z: gz };
    this.#paintWorld();
  }

  // ── painting ──────────────────────────────────────────────────────────────────

  /**
   * Paint every world layer, bottom first. This is the whole of the world's z-order, and unlike a group
   * stack it is also the whole of its cost: a layer that draws nothing costs a function call, and nothing
   * survives between frames to be stretched by a later transform.
   */
  #paintWorld() {
    if (!this.#painter) return;
    const painter = this.#painter;
    painter.begin(this._scale, this._panX, this._panY);
    painter.layer("work",      () => paintWorkArea(painter, this.#bbox));
    painter.layer("bbox",      () => paintBbox(painter, this.#tight));
    painter.layer("chunk",     () => { if (this.#chunkVisible) paintChunkGrid(painter, this.#gridBounds(), gridStep(CHUNK * this._scale)); });
    painter.layer("axis",      () => paintAxis(painter, this.#gridBounds(), this.#center, this.#mode));
    painter.layer("mirror",    () => { if (this.#mirrorVisible) paintMirror(painter, this.#mirrorPolys); });
    painter.layer("ghost",     () => paintGhostIslands(painter, this.#ghostPolys));
    // The Blocks layer is the terrain paint when the server has sent it, and the plain stone footprint until
    // then — so drawing keeps its immediate voxelization feedback while the painted blocks are in flight.
    const painted = this.#blocksVisible && this.#paintImage;
    painter.layer("raster",    () => {
      if (!this.#blocksVisible) return;
      if (painted) painter.image(this.#paintImage, blockImageBounds(this.#paintData));
      else paintRaster(painter, this.#rasterRuns);
    });
    // The island fill would tint every painted block towards the result purple, so under the paint the
    // island contributes its outline only.
    painter.layer("island",    () => paintIslands(painter, this.#islands, { filled: !painted }));
    // Contours sit over the blocks and the island fill and under everything drawn or selected: they describe
    // the ground, so they belong on it, but an author's own shapes have to stay legible across them.
    painter.layer("relief",    () => { if (this.#relief) paintContours(painter, this.#relief); });
    painter.layer("shapes",    () => this.#paintShapes());
    // Structural pieces (S25) are locked plan context, not drawn primitives — always shown (like the island
    // outlines), not behind the Shapes toggle, so they stay visible while a plan is refined.
    painter.layer("structural", () => paintStructural(painter, this.#structural));
    painter.layer("selection", () => this.#paintSelectionHighlight());
    painter.layer("marks",     () => this.#paintReliefMarks(painter));
    painter.layer("dressing",  () => this.#paintDressing(painter));
    painter.layer("draw",      () => this.#draw?.paint(painter));
    painter.layer("measure",   () => this.#paintMeasure());
    painter.layer("guide",     () => this.#paintGuides());
    // The scale bar is screen-space chrome and stays in the svg, but it reads the zoom, so it is
    // refreshed on the same beat as the world.
    const { w, h } = this._viewSize ?? this._size();
    renderScaleBar(this.#screen.scale, { w, h, scale: this._scale });
  }

  /** The painted layer names of the last frame, bottom first — what `data-layer` offered on a group stack. */
  get paintOrder() { return this.#painter?.layers ?? []; }

  #paintShapes() {
    if (!this.#shapesVisible) return;
    for (const shape of this.#shapes.values())
      paintSketchShape(this.#painter, shape, { selected: shape.id === this.#selectedId });
  }

  /** The locked plan pieces (S25) — follow the Shapes toggle, but are drawn read-only (no selection chrome). */
  setStructural(shapes) { this.#structural = shapes ?? []; this.#paintWorld(); }

  #paintGuides() {
    const { min_x, max_x, min_z, max_z } = this.#gridBounds();   // guides run the full visible width/height
    const style = { stroke: "var(--accent)", width: 1, dash: [4, 3] };
    if (this.#guides.x !== null) this.#painter.line(this.#guides.x, min_z, this.#guides.x, max_z, style);
    if (this.#guides.z !== null) this.#painter.line(min_x, this.#guides.z, max_x, this.#guides.z, style);
  }

  /**
   * Accent-outline the current selection (S22) so it's findable even when the Shapes layer is hidden: the
   * selected shape's own outline (its Bézier curve) when a shape is selected/drilled, else the selected
   * island's outline (exterior + holes). World space, so it follows move / rotate / scale / resize with
   * everything else on the frame.
   */
  #paintSelectionHighlight() {
    const style = { fill: "var(--accent)", fillAlpha: 0.12, fillRule: "evenodd", stroke: "var(--accent)", width: 2.5 };
    if (this.#selectedId) {
      const shape = this.#shapes.get(this.#selectedId);
      if (!shape) return;
      if ((shape.type === "polygon" || shape.type === "lasso") && shape.vertices?.length >= 3) {
        this.#painter.ring(shape.vertices, style, shape.controls || {});
      } else {
        const ring = toRing(shape);                       // rectangle / circle → closed ring
        if (ring.length >= 4) this.#painter.ring(ring.slice(0, -1), style);
      }
      return;
    }
    if (!this.#selectedIslandId) return;
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    if (isl?.exterior?.length >= 3) this.#painter.poly({ exterior: isl.exterior, holes: isl.holes ?? [] }, style);
  }

  // Placed dressing, plus each prop's mirror images — a prop is fanned across the orbit at export, so an
  // author who cannot see the other half is placing cover blind. Only while the phase is up: on the draw
  // canvas the props are context that would only crowd the geometry being edited.
  #paintDressing(painter) {
    if (!this.#dressingOn) return;
    const axes = orbitAxes(this.#mode);
    paintDressing(painter, this.#dressingDoc.props, {
      selectedId: this.#dressing?.selectedId ?? null,
      order: axes.length + 1,
      mirrorPoint: (x, z, k) => applySymmetry(x, z, axes[k - 1], this.#center.cx, this.#center.cz),
    });
    this.#dressing?.paint(painter);
  }

  // The stated marks, plus each mark's mirror images — a relief is folded across the symmetry at solve time,
  // so an author who cannot see the other half is stating terrain blind. Only while the phase is up: on the
  // draw canvas the marks would crowd the geometry being edited, and the contour chip is the read-only view
  // that belongs there instead.
  #paintReliefMarks(painter) {
    if (!this.#reliefOn) return;
    const axes = orbitAxes(this.#mode);
    paintReliefMarks(painter, this.#reliefDoc.marks, {
      selectedId: this.#reliefTools?.selectedId ?? null,
      order: axes.length + 1,
      mirrorPoint: (x, z, k) => applySymmetry(x, z, axes[k - 1], this.#center.cx, this.#center.cz),
      baseOf: (islandId) => this.#reliefDoc.peek(islandId)?.base ?? 8,
    });
    this.#reliefTools?.paint(painter);
  }

  // The ruler line in world coords (so it pans/zooms with the map); the live distance rides the line
  // itself as screen-space text (#renderMeasureLabel), not the sub-bar. The slice preview (S14) shares
  // this layer — it is the same kind of thing, a line drawn between two clicks.
  #paintMeasure() {
    const m = this.#measure;
    if (m) this.#painter.line(m.ax, m.az, m.bx, m.bz,
      { stroke: "var(--canvas-axis)", width: 1.5, dash: [4, 3] });
    const s = this.#split;
    if (s) this.#painter.line(s.ax, s.az, s.bx, s.bz,
      { stroke: "var(--canvas-sub-stroke)", width: 1.5, dash: [5, 3] });
  }

  // ── screen-space chrome ────────────────────────────────────────────────────────

  // Draw the selected island's bounding box + corner anchors (screen-space, so legible at any zoom), plus
  // the four rotate zones just OUTSIDE the corners (S13 — hover shows the rotate cursor, drag rotates the
  // island). The edges are reserved for scale (S21). Re-rendered on selection + every viewport change.
  #renderIslandChrome() {
    const layer = this.#screen.islandChrome;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    if (!this.#selectedIslandId) return;
    const isl = this.#islands.find(i => i.id === this.#selectedIslandId);
    if (!isl) return;
    const members = (isl.shapeIds ?? []).map(id => this.#shapes.get(id)).filter(Boolean);
    const b = boundsOfShapes(members);
    if (!b) return;
    const p0 = this._toScreen(b.min_x, b.min_z), p1 = this._toScreen(b.max_x, b.max_z);
    const l = Math.min(p0.x, p1.x), r = Math.max(p0.x, p1.x);
    const t = Math.min(p0.y, p1.y), bot = Math.max(p0.y, p1.y);
    layer.appendChild(svgEl("rect", {
      x: l, y: t, width: r - l, height: bot - t,
      fill: "none", stroke: "var(--accent)", "stroke-width": "1.5", "stroke-dasharray": "5 3", "pointer-events": "none",
    }));
    // In select-only mode the dashed box is the whole chrome: it says what is selected, which the Theme
    // phase needs, while every grabbable part below it — rotate zones, scale handles — is an edit
    // affordance and is not drawn at all, so there is nothing to take hold of.
    if (this.#selectOnly) return;

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

  #build() {
    const { w, h } = this._resizeSvg();
    this.#painter.resize(w, h);

    // The world's paint order lives in #paintWorld, bottom first — one sequence of painter.layer calls,
    // and `painter.layers` reports it by name. `_viewportG` stays as an empty group because CanvasBase
    // treats it as the "surface is ready" flag its pointer handlers guard on, and puts the viewport
    // matrix there; nothing hangs off it now, so that matrix is inert.
    this._viewportG = svgEl("g", { id: "sk-viewport" });
    this._svg.appendChild(this._viewportG);

    this.#screen = layerStack(this._svg, {
      islandChrome: null,                          // island bbox (inert) + interactive rotate zones
      handles:      null,
      dressingHandles: null,                       // the selected prop's point grips (Dressing phase)
      reliefHandles: null,                         // the selected mark's point grips (Relief phase)
      center:       INERT,
      drawHandles:  INERT,
      measureLabel: INERT,
      scale:        INERT,                         // "N blocks" bar, bottom-right
    });

    this._applyViewportTransform();

    this._observeResize();

    const getViewport = () => ({ scale: this._scale, panX: this._panX, panY: this._panY });
    this.#draw = new SketchDrawController(this.#screen.drawHandles, getViewport, {
      onShapeCreated: (partial) => this.#callbacks.onShapeCreated?.(partial),
      onPreviewChanged: () => this.#paintWorld(),
    });
    // Dressing places props on the same canvas the shapes are drawn on, because a tree's position is a
    // decision about the map's geometry and belongs next to it. Its own controller, so the draw tools and the
    // placing tools never have to know about each other.
    this.#dressing = new DressingController(this.#dressingDoc, this.#screen.dressingHandles, getViewport, {
      onChanged: () => { this.#paintWorld(); this.#callbacks.onDressingChanged?.(); },
      onSelected: (id) => this.#callbacks.onPropSelected?.(id),
      onPreviewChanged: () => this.#paintWorld(),
      // A finished placement hands the canvas back, the same as a finished draw tool: the prop just put down
      // is the one to move or tune, and select is the mode that does either.
      onPlaced: () => { this.#placementClick = true; this.#callbacks.onDressingPlaced?.(); },
      // A marker (a tree, a boulder) may only land on the rasterized terrain — the export refuses one seated on
      // nothing, so the canvas refuses to drop it there in the first place.
      onTerrain: (bx, bz) => this.#cellOnTerrain(bx, bz),
    });
    // Relief states the ground inside an island, so its tools live on the same canvas for the same reason
    // dressing's do — and its own controller for the same reason too.
    this.#reliefTools = new ReliefController(this.#reliefDoc, this.#screen.reliefHandles, getViewport, {
      onChanged: () => { this.#paintWorld(); this.#callbacks.onReliefChanged?.(); },
      onSelected: (id) => this.#callbacks.onMarkSelected?.(id),
      onPreviewChanged: () => this.#paintWorld(),
      onPlaced: () => { this.#placementClick = true; this.#callbacks.onReliefPlaced?.(); },
      // A mark belongs to the island it was started in. Unlike a prop it may then be dragged past that
      // island's edge — a mark is clipped, not confined, and a hill authored into a corner is exactly that
      // gesture — so this decides ownership at placement and never again.
      onIslandAt: (bx, bz) => this.#hitIsland(bx + 0.5, bz + 0.5),
    });
    this.#edit = new SketchEditController(this.#screen.handles, getViewport, (id) => this.#shapes.get(id), {
      onShapeUpdated: (shape) => { this.updateShape(shape); this.#callbacks.onShapeUpdated?.(shape); },
      onVertexSelected: (shapeId, idx) => this.#callbacks.onVertexSelected?.(shapeId, idx),
      onSlopeControls: (shapeId, indices) => this.#callbacks.onSlopeControls?.(shapeId, indices),
      snapEdges: (id, edges, alt) => this.#snapResize(id, edges, alt),
    });

    this.#renderSetup();

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
    // Double-click ends a click-by-click draw — closing a polygon, leaving a path open; in select mode it
    // drills into the member shape under the cursor (Figma group model — single-click picks the island,
    // double-click enters a member).
    this._svg.addEventListener("dblclick", (e) => {
      if (this._activeTool === "polygon" || this._activeTool === "path") {
        e.stopPropagation();
        this.#draw.onDblClick();
        return;
      }
      if (this._isoOn || (this._activeTool !== null && this._activeTool !== "select")) return;
      const p = this._clientToSvg(e.clientX, e.clientY);
      const shapeId = this.#hitTest(p.x, p.y);
      if (shapeId) this.#callbacks.onShapeSelected?.(shapeId);   // drill
    });
  }

  // The exact block AABB of the drawn content — every shape plus the mirror-preview polygons (the
  // reflected half). This is the tight world bound; null when the sketch is empty.
  #contentBounds() {
    let b = null;
    const grow = (mnx, mnz, mxx, mxz) => {
      if (!b) b = { min_x: mnx, min_z: mnz, max_x: mxx, max_z: mxz };
      else {
        b.min_x = Math.min(b.min_x, mnx); b.min_z = Math.min(b.min_z, mnz);
        b.max_x = Math.max(b.max_x, mxx); b.max_z = Math.max(b.max_z, mxz);
      }
    };
    for (const s of this.#shapes.values()) {
      const sb = toBounds(s); if (sb) grow(sb.min_x, sb.min_z, sb.max_x, sb.max_z);
    }
    for (const poly of this.#mirrorPolys) {
      for (const [x, z] of (poly?.exterior ?? [])) grow(x, z, x, z);
    }
    return b;
  }

  // The working area: content padded by one chunk and snapped out to chunk lines, but never smaller than
  // the 64×64 (4×4 chunk) default about the origin. Drawn as the tinted region and framed by fit — the
  // anchor for how big a map should be, present from the first blank canvas.
  #viewBounds(tight) {
    let mnx = -MIN_HALF, mnz = -MIN_HALF, mxx = MIN_HALF, mxz = MIN_HALF;
    if (tight) {
      mnx = Math.min(mnx, Math.floor((tight.min_x - VIEW_BUFFER) / CHUNK) * CHUNK);
      mnz = Math.min(mnz, Math.floor((tight.min_z - VIEW_BUFFER) / CHUNK) * CHUNK);
      mxx = Math.max(mxx, Math.ceil((tight.max_x + VIEW_BUFFER) / CHUNK) * CHUNK);
      mxz = Math.max(mxz, Math.ceil((tight.max_z + VIEW_BUFFER) / CHUNK) * CHUNK);
    }
    return { min_x: mnx, min_z: mnz, max_x: mxx, max_z: mxz };
  }

  // The world rect currently on screen, snapped out to GRID_SNAP — the extent the chunk grid, the
  // symmetry axis and the alignment guides span. Viewport-driven rather than content-driven, so the
  // drawing surface is never fenced in by the grid: wherever you pan or zoom, there is grid to draw on.
  #gridBounds() {
    const { w, h } = this._viewSize ?? this._size();   // cached — this runs on every pan/drag frame
    return snapOut(viewportWorldRect(w, h, this._scale, this._panX, this._panY), GRID_SNAP);
  }

  // Recompute the derived bounds the working area and the frame outline are drawn from, then repaint.
  #renderSetup() {
    this.#tight = this.#contentBounds();
    this.#bbox = this.#viewBounds(this.#tight);   // the working area — drives the tint and fit()/pan
    this.#rebuildRaster();                         // keep the block preview in step with the shapes (no-op when off)
    this.#paintWorld();
  }

  #refreshCenter() {
    const layer = this.#screen.center;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const { x: sx, y: sy } = this._toScreen(this.#center.cx, this.#center.cz);
    const arm = 10, col = "var(--canvas-axis)";
    layer.appendChild(svgEl("line", { x1: sx - arm, y1: sy, x2: sx + arm, y2: sy, stroke: col, "stroke-width": "1" }));
    layer.appendChild(svgEl("line", { x1: sx, y1: sy - arm, x2: sx, y2: sy + arm, stroke: col, "stroke-width": "1" }));
    layer.appendChild(svgEl("circle", { cx: sx, cy: sy, r: 4, fill: "none", stroke: col, "stroke-width": "1.5" }));
  }

  #renderMeasure() { this.#paintWorld(); this.#renderMeasureLabel(); }

  // The ruler distance as pure screen-space text running ALONG the ruler line — legible at any zoom (a
  // world-space label would scale with the map), so it's repositioned on every viewport change too. A
  // thin halo (paint-order stroke in the canvas bg) keeps it readable over shapes without a box/pill.
  #renderMeasureLabel() {
    const layer = this.#screen.measureLabel;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const m = this.#measure;
    if (!m) return;
    const text = `${Math.round(Math.hypot(m.bx - m.ax, m.bz - m.az))} blocks`;
    // Endpoints + midpoint in screen space (identity world→surface, then the viewport pan/scale).
    const { x: ax, y: ay } = this._toScreen(m.ax, m.az);
    const { x: bx, y: by } = this._toScreen(m.bx, m.bz);
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
  // (the host cuts the crossed shape into two). The slice line rides the measure layer.
  #onSplitClick(bx, bz) {
    if (!this.#split) {
      this.#split = { ax: bx, az: bz, bx, bz };
      this.#paintWorld();
    } else {
      this.#callbacks.onSplit?.([this.#split.ax, this.#split.az], [bx, bz]);
      this.#clearSplit();
    }
  }
  #clearSplit() { this.#split = null; this.#paintWorld(); }

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
