/**
 * SketchDrawController — the four sketch draw tools (rectangle, polygon, lasso, path) for
 * SketchCanvas. Same controller contract as the world draw controller (onMouseDown→bool, onMouseMove,
 * onMouseUp, cancel). Completed shapes are reported via onShapeCreated; the host assigns an id and
 * triggers the island recompute.
 *
 * The in-progress draw is **state, not elements**: `#drawState` holds the numbers and `paint` draws them
 * on whatever frame the canvas is painting, which is why every mutating entry point ends by asking for
 * one (`onPreviewChanged`). Its screen-space vertex handles are the exception and stay SVG, since they
 * live in the overlay above the painted surface where a fixed pixel size and DOM semantics are wanted.
 *
 * Constructor:
 *   drawHandlesLayer SVGGElement  — screen-space handle overlay
 *   getViewport      () => { scale, panX, panY }
 *   callbacks        { onShapeCreated, onPreviewChanged }
 */

import { svgEl, handleRectAttrs } from "../render/svg.js";
import { drawnBoundsFromBlocks } from "../geometry/region-convert.js";
import { opColors } from "../render/primitive-style.js";
import { toScreen } from "../geometry/transform.js";
import { simplifyRing } from "../geometry/simplify.js";
import { pathRing, pathCenterline } from "../geometry/path.js";

const HANDLE_HALF = 5;

// How far (in blocks) a dropped lasso point may sit from the simplified outline. 4 is deliberately chunky:
// it collapses the freehand staircase and hand wobble to a handful of anchors (a big round blob → ~10)
// while real bends and sharp corners survive. Add points back by hand, or round edges with the Bézier
// handles, if you want more detail — the raw trace is one point per block, so this is a large reduction.
const LASSO_SIMPLIFY_TOLERANCE = 4;

// What a freshly drawn path starts as: wide enough to walk two abreast, clean-edged, and a fixed seed so a
// path is identical on every export until an author rerolls it. Width, edge and seed are all editable after.
const PATH_DEFAULT_RADIUS = 3;
const PATH_DEFAULT_SEED   = 0;

// The in-progress outline: the operation's colours, dashed, over a light fill — "not committed yet".
const PREVIEW_FILL_ALPHA = 0.20;
const PREVIEW_DASH = [5, 3];

export class SketchDrawController {
  #drawHandlesLayer;
  #getViewport;
  #callbacks;

  #activeOperation = "add";
  #drawState       = null;
  #drawHandleData  = [];

  constructor(drawHandlesLayer, getViewport, { onShapeCreated, onPreviewChanged } = {}) {
    this.#drawHandlesLayer = drawHandlesLayer;
    this.#getViewport      = getViewport;
    this.#callbacks        = { onShapeCreated, onPreviewChanged };
  }

  setOperation(op)      { this.#activeOperation = op; }
  get activeOperation() { return this.#activeOperation; }

  /** Live dimensions of the in-progress draw as a compact block label (`W × D`), or "" when nothing is
   *  being drawn — fed to the canvas's on-canvas size readout. */
  activeDimLabel() {
    const ds = this.#drawState;
    if (!ds) return "";
    if (ds.type === "rectangle") {
      const { min_x, max_x, min_z, max_z } = drawnBoundsFromBlocks(ds.startBx, ds.startBz, ds.currentBx, ds.currentBz);
      return `${max_x - min_x} × ${max_z - min_z}`;
    }
    if (ds.vertices?.length >= 2) {
      const xs = ds.vertices.map(v => v[0]), zs = ds.vertices.map(v => v[1]);
      return `${Math.max(...xs) - Math.min(...xs)} × ${Math.max(...zs) - Math.min(...zs)}`;
    }
    return "";
  }

  /** Dispatch mousedown by tool. Returns true if consumed. */
  onMouseDown(bx, bz, activeTool) {
    if (activeTool === "rectangle") { this.#startRect(bx, bz); return true; }
    if (activeTool === "lasso")     { this.#startLasso(bx, bz); return true; }
    if (activeTool === "path") {
      if (!this.#drawState) this.#startPath(bx, bz); else this.#addPolygonVertex(bx, bz);
      return true;
    }
    if (activeTool === "polygon") {
      if (!this.#drawState) {
        this.#startPolygon(bx, bz);
      } else {
        const [fx, fz] = this.#drawState.vertices[0];
        if (Math.abs(bx - fx) <= 2 && Math.abs(bz - fz) <= 2 && this.#drawState.vertices.length >= 3) {
          this.#closePolygon();
        } else {
          this.#addPolygonVertex(bx, bz);
        }
      }
      return true;
    }
    return false;
  }

  /** Drive previews on every pointer move. */
  onMouseMove(bx, bz) {
    if (!this.#drawState) return;
    const type = this.#drawState.type;
    if (type === "rectangle")    this.#updateRectPreview(bx, bz);
    else if (type === "polygon" || type === "path") this.#updatePolygonPreview(bx, bz);
    else if (type === "lasso")   this.#addLassoPoint(bx, bz);
  }

  /** Complete lasso (release) or rectangle (drag-release). */
  onMouseUp() {
    const type = this.#drawState?.type;
    if (type === "lasso")     { this.#completeLasso(); return true; }
    if (type === "rectangle") { this.#completeRect();  return true; }
    return false;
  }

  /** Finish a polygon (closing it) or a path (leaving it open) on double-click; trims a duplicate trailing
   *  vertex either way, since the dblclick's own mousedown already dropped one where the cursor sat. */
  onDblClick() {
    const type = this.#drawState?.type;
    if (type !== "polygon" && type !== "path") return;
    const ds = this.#drawState;
    if (ds.vertices.length > 1) {
      const last = ds.vertices[ds.vertices.length - 1];
      const prev = ds.vertices[ds.vertices.length - 2];
      if (last[0] === prev[0] && last[1] === prev[1]) ds.vertices.pop();
    }
    if (type === "path") this.#completePath(); else this.#closePolygon();
  }

  /**
   * Draw the in-progress primitive onto the canvas's painter. Called from the sketch canvas's `draw`
   * phase, so the preview is in the same frame — and at the same scale — as everything under it.
   */
  paint(painter) {
    const ds = this.#drawState;
    if (!ds) return;
    const { fill, stroke } = opColors(this.#activeOperation);
    const style = { fill, fillAlpha: PREVIEW_FILL_ALPHA, stroke, width: 1, dash: PREVIEW_DASH };
    const guide = { stroke: "var(--text-muted)", width: 1 };

    if (ds.type === "rectangle") {
      painter.rect(drawnBoundsFromBlocks(ds.startBx, ds.startBz, ds.currentBx, ds.currentBz), style);
    } else if (ds.type === "polygon") {
      const runs = [];
      for (let i = 1; i < ds.vertices.length; i++)
        runs.push({ x1: ds.vertices[i - 1][0], z1: ds.vertices[i - 1][1], x2: ds.vertices[i][0], z2: ds.vertices[i][1] });
      painter.segments(runs, guide);
      // The rubber-band run to the cursor is dashed, so the edge not yet committed reads as pending.
      const last = ds.vertices[ds.vertices.length - 1];
      painter.line(last[0], last[1], ds.cursorX ?? last[0], ds.cursorZ ?? last[1], { ...guide, dash: [4, 3] });
    } else if (ds.type === "path") {
      // The band, live — a path's width is most of what the author is deciding, so previewing only the line
      // would hide it until the shape was committed. The cursor's pending point is included, so the band
      // grows with the pointer, and the centerline is stroked over it as the thing being edited.
      const pending = [...ds.vertices, [ds.cursorX ?? ds.vertices[0][0], ds.cursorZ ?? ds.vertices[0][1]]];
      const ring = pathRing({ vertices: pending, radius: PATH_DEFAULT_RADIUS });
      if (ring.length >= 3) painter.ring(ring.slice(0, -1), { ...style, fillRule: "evenodd" });
      const curve = pathCenterline(pending);
      const runs = [];
      for (let i = 1; i < curve.length; i++)
        runs.push({ x1: curve[i - 1][0], z1: curve[i - 1][1], x2: curve[i][0], z2: curve[i][1] });
      painter.segments(runs, guide);
    } else if (ds.type === "lasso" && ds.vertices.length >= 2) {
      painter.ring(ds.vertices, { ...style, fillRule: "evenodd" });
    }
  }

  /** Reposition screen-space draw handles after viewport changes. */
  refreshDrawHandles() {
    if (!this.#drawHandlesLayer) return;
    while (this.#drawHandlesLayer.firstChild) this.#drawHandlesLayer.removeChild(this.#drawHandlesLayer.firstChild);
    for (const { wx, wz, isFirst } of this.#drawHandleData) {
      const sp = this.#toScreen(wx, wz);
      this.#drawHandlesLayer.appendChild(svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, HANDLE_HALF),
        fill:   isFirst ? "var(--accent-light)" : "var(--canvas-handle-fill)",
        stroke: isFirst ? "var(--accent)"       : "var(--canvas-handle-stroke)",
        "stroke-width": "1.5",
      }));
    }
  }

  /** Cancel any in-progress draw (Escape, tool change). */
  cancel() {
    if (!this.#drawState) return;
    this.#drawState      = null;
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    this.#repaint();
  }

  // ── private ────────────────────────────────────────────────────────────────

  #toScreen(wx, wz) { return toScreen(wx, wz, this.#getViewport()); }
  #repaint() { this.#callbacks.onPreviewChanged?.(); }

  // Rectangle ──────────────────────────────────────────────────────────────────
  #startRect(bx, bz) {
    this.#drawState      = { type: "rectangle", startBx: bx, startBz: bz, currentBx: bx, currentBz: bz };
    this.#drawHandleData = [{ wx: bx, wz: bz, isFirst: true }];
    this.refreshDrawHandles();
    this.#repaint();
  }

  #updateRectPreview(bx, bz) {
    const { startBx, startBz } = this.#drawState;
    this.#drawState.currentBx = bx;
    this.#drawState.currentBz = bz;
    const { min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ } = drawnBoundsFromBlocks(startBx, startBz, bx, bz);
    this.#drawHandleData = [
      { wx: minX, wz: minZ, isFirst: false }, { wx: maxX, wz: minZ, isFirst: false },
      { wx: maxX, wz: maxZ, isFirst: false }, { wx: minX, wz: maxZ, isFirst: false },
    ];
    this.refreshDrawHandles();
    this.#repaint();
  }

  #completeRect() {
    const { startBx, startBz, currentBx, currentBz } = this.#drawState;
    this.#drawState      = null;
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    this.#repaint();
    const { min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ } = drawnBoundsFromBlocks(startBx, startBz, currentBx, currentBz);
    if (maxX - minX <= 1 && maxZ - minZ <= 1) return;  // reject single-click misfire
    this.#callbacks.onShapeCreated?.({
      type: "rectangle", operation: this.#activeOperation, override: false,
      min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ,
    });
  }

  // Polygon (click vertices, close on first-vertex click or dblclick) ──────────
  #startPolygon(bx, bz) {
    this.#drawHandleData = [{ wx: bx, wz: bz, isFirst: true }];
    this.refreshDrawHandles();
    this.#drawState = { type: "polygon", vertices: [[bx, bz]], cursorX: bx, cursorZ: bz };
    this.#repaint();
  }

  #addPolygonVertex(bx, bz) {
    this.#drawHandleData.push({ wx: bx, wz: bz, isFirst: false });
    this.refreshDrawHandles();
    const ds = this.#drawState;
    ds.vertices.push([bx, bz]);
    ds.cursorX = bx;
    ds.cursorZ = bz;
    this.#repaint();
  }

  #updatePolygonPreview(bx, bz) {
    if (!this.#drawState) return;
    this.#drawState.cursorX = bx;
    this.#drawState.cursorZ = bz;
    this.#repaint();
  }

  #closePolygon() {
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    const saved = this.#drawState;
    this.#drawState = null;
    this.#repaint();
    if (saved.vertices.length < 3) return;
    this.#callbacks.onShapeCreated?.({
      type: "polygon", operation: this.#activeOperation, override: false, vertices: saved.vertices,
    });
  }

  // Path (click points along a route, dblclick to end) ─────────────────────────
  // Points are added by the polygon's own handler: a path is drawn exactly like a polygon and differs only
  // in that it never closes, so sharing the add keeps the two from drifting apart under the cursor.
  #startPath(bx, bz) {
    this.#drawHandleData = [{ wx: bx, wz: bz, isFirst: true }];
    this.refreshDrawHandles();
    this.#drawState = { type: "path", vertices: [[bx, bz]], cursorX: bx, cursorZ: bz };
    this.#repaint();
  }

  #completePath() {
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    const saved = this.#drawState;
    this.#drawState = null;
    this.#repaint();
    if (saved.vertices.length < 2) return;   // a path needs somewhere to go
    this.#callbacks.onShapeCreated?.({
      type: "path", operation: this.#activeOperation, override: false, vertices: saved.vertices,
      radius: PATH_DEFAULT_RADIUS, path_edge: "solid", path_seed: PATH_DEFAULT_SEED,
    });
  }

  // Lasso (hold drag to trace freeform; release to close) ──────────────────────
  #startLasso(bx, bz) {
    this.#drawState = { type: "lasso", vertices: [[bx, bz]] };
    this.#repaint();
  }

  #addLassoPoint(bx, bz) {
    const { vertices } = this.#drawState;
    const last = vertices[vertices.length - 1];
    if (bx === last[0] && bz === last[1]) return;
    vertices.push([bx, bz]);
    this.#repaint();
  }

  // A lasso is just a freehand way to draw a polygon with a lot of anchor points. The raw trace is one
  // vertex per block of pointer travel — unreadable to edit — so on release it is Douglas–Peucker
  // simplified to the points at real bends and emitted as a plain polygon (add curves back with the Bézier
  // handles if you want rounding). The trace is already block-integer, and the simplifier only keeps
  // existing points, so the result stays on the grid.
  #completeLasso() {
    const { vertices } = this.#drawState;
    this.#drawState = null;
    this.#repaint();
    if (vertices.length < 3) return;
    const simplified = simplifyRing(vertices, LASSO_SIMPLIFY_TOLERANCE);
    if (simplified.length < 3) return;
    this.#callbacks.onShapeCreated?.({
      type: "polygon", operation: this.#activeOperation, override: false, vertices: simplified,
    });
  }
}
