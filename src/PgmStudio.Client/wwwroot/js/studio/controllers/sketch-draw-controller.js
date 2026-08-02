/**
 * SketchDrawController — the three sketch draw tools (rectangle, polygon, lasso) for
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

const HANDLE_HALF = 5;

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
    else if (type === "polygon") this.#updatePolygonPreview(bx, bz);
    else if (type === "lasso")   this.#addLassoPoint(bx, bz);
  }

  /** Complete lasso (release) or rectangle (drag-release). */
  onMouseUp() {
    const type = this.#drawState?.type;
    if (type === "lasso")     { this.#completeLasso(); return true; }
    if (type === "rectangle") { this.#completeRect();  return true; }
    return false;
  }

  /** Close polygon on double-click; trims a duplicate trailing vertex. */
  onDblClick() {
    if (this.#drawState?.type !== "polygon") return;
    const ds = this.#drawState;
    if (ds.vertices.length > 1) {
      const last = ds.vertices[ds.vertices.length - 1];
      const prev = ds.vertices[ds.vertices.length - 2];
      if (last[0] === prev[0] && last[1] === prev[1]) ds.vertices.pop();
    }
    this.#closePolygon();
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

  #completeLasso() {
    const { vertices } = this.#drawState;
    this.#drawState = null;
    this.#repaint();
    if (vertices.length < 3) return;
    this.#callbacks.onShapeCreated?.({
      type: "lasso", operation: this.#activeOperation, override: false, vertices,
    });
  }
}
