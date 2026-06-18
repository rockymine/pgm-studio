/**
 * SketchDrawController — the four sketch draw tools (rectangle, circle, polygon, lasso) for
 * SketchCanvas. Same controller contract as the editor draw controller (onMouseDown→bool, onMouseMove,
 * onMouseUp, cancel). Completed shapes are reported via onShapeCreated; the host assigns an id and
 * triggers the island recompute.
 *
 * Constructor:
 *   drawLayer        SVGGElement  — viewport draw layer (previews)
 *   drawHandlesLayer SVGGElement  — screen-space handle overlay
 *   getViewport      () => { scale, panX, panY }
 *   callbacks        { onShapeCreated }
 */

import { svgEl, ringToPath, handleRectAttrs } from "../render/svg.js";
import { drawnBoundsFromBlocks } from "../geometry/region-convert.js";

const HANDLE_HALF = 5;

const ADD_FILL   = "var(--canvas-add-fill)";
const ADD_STROKE = "var(--canvas-add-stroke)";
const SUB_FILL   = "var(--canvas-sub-fill)";
const SUB_STROKE = "var(--canvas-sub-stroke)";

const identityTransform = (x, z) => ({ x, y: z });

export class SketchDrawController {
  #drawLayer;
  #drawHandlesLayer;
  #getViewport;
  #callbacks;

  #activeOperation = "add";
  #drawState       = null;
  #drawHandleData  = [];

  constructor(drawLayer, drawHandlesLayer, getViewport, { onShapeCreated } = {}) {
    this.#drawLayer        = drawLayer;
    this.#drawHandlesLayer = drawHandlesLayer;
    this.#getViewport      = getViewport;
    this.#callbacks        = { onShapeCreated };
  }

  setOperation(op)      { this.#activeOperation = op; }
  get activeOperation() { return this.#activeOperation; }

  /** Dispatch mousedown by tool. Returns true if consumed. */
  onMouseDown(bx, bz, activeTool) {
    if (activeTool === "rectangle") { this.#startRect(bx, bz); return true; }
    if (activeTool === "lasso")     { this.#startLasso(bx, bz); return true; }
    if (activeTool === "circle") {
      if (!this.#drawState) this.#startCircle(bx, bz);
      else                  this.#completeCircle(bx, bz);
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
    else if (type === "circle")  this.#updateCirclePreview(bx, bz);
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
      if (last[0] === prev[0] && last[1] === prev[1]) {
        ds.vertices.pop();
        const line = ds.lines.pop();
        line?.parentNode?.removeChild(line);
      }
    }
    this.#closePolygon();
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
    const ds = this.#drawState;
    this.#drawState      = null;
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    for (const el of [ds.preview, ds.previewPath, ds.previewLine, ds.dot, ...(ds.lines ?? [])]) {
      el?.parentNode?.removeChild(el);
    }
  }

  // ── private ────────────────────────────────────────────────────────────────

  #toScreen(wx, wz) {
    const { scale, panX, panY } = this.#getViewport();
    return { x: wx * scale + panX, y: wz * scale + panY };
  }

  #opFill()   { return this.#activeOperation === "subtract" ? SUB_FILL   : ADD_FILL; }
  #opStroke() { return this.#activeOperation === "subtract" ? SUB_STROKE : ADD_STROKE; }

  // Rectangle ──────────────────────────────────────────────────────────────────
  #startRect(bx, bz) {
    const preview = svgEl("rect", {
      fill: this.#opFill(), stroke: this.#opStroke(),
      "stroke-width": "1", "fill-opacity": "0.20", "stroke-dasharray": "5 3",
      "vector-effect": "non-scaling-stroke", x: bx, y: bz, width: 1, height: 1,
    });
    this.#drawLayer.appendChild(preview);
    this.#drawState      = { type: "rectangle", startBx: bx, startBz: bz, currentBx: bx, currentBz: bz, preview };
    this.#drawHandleData = [{ wx: bx, wz: bz, isFirst: true }];
    this.refreshDrawHandles();
  }

  #updateRectPreview(bx, bz) {
    const { startBx, startBz, preview } = this.#drawState;
    this.#drawState.currentBx = bx;
    this.#drawState.currentBz = bz;
    const { min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ } = drawnBoundsFromBlocks(startBx, startBz, bx, bz);
    preview.setAttribute("x", minX);
    preview.setAttribute("y", minZ);
    preview.setAttribute("width",  maxX - minX);
    preview.setAttribute("height", maxZ - minZ);
    this.#drawHandleData = [
      { wx: minX, wz: minZ, isFirst: false }, { wx: maxX, wz: minZ, isFirst: false },
      { wx: maxX, wz: maxZ, isFirst: false }, { wx: minX, wz: maxZ, isFirst: false },
    ];
    this.refreshDrawHandles();
  }

  #completeRect() {
    const { startBx, startBz, currentBx, currentBz, preview } = this.#drawState;
    preview?.parentNode?.removeChild(preview);
    this.#drawState      = null;
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    const { min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ } = drawnBoundsFromBlocks(startBx, startBz, currentBx, currentBz);
    if (maxX - minX <= 1 && maxZ - minZ <= 1) return;  // reject single-click misfire
    this.#callbacks.onShapeCreated?.({
      type: "rectangle", operation: this.#activeOperation, override: false,
      min_x: minX, max_x: maxX, min_z: minZ, max_z: maxZ,
    });
  }

  // Circle (two-click: center → radius) ────────────────────────────────────────
  #startCircle(bx, bz) {
    const dot = svgEl("rect", {
      x: bx - 0.5, y: bz - 0.5, width: 1, height: 1, fill: "var(--text-muted)", "pointer-events": "none",
    });
    const preview = svgEl("ellipse", {
      fill: this.#opFill(), stroke: this.#opStroke(),
      "stroke-width": "1", "fill-opacity": "0.20", "stroke-dasharray": "5 3",
      "vector-effect": "non-scaling-stroke", cx: bx, cy: bz, rx: 1, ry: 1,
    });
    this.#drawLayer.appendChild(preview);
    this.#drawLayer.appendChild(dot);
    this.#drawState = { type: "circle", centerX: bx, centerZ: bz, currentRadius: 1, preview, dot };
  }

  #updateCirclePreview(bx, bz) {
    const { centerX, centerZ, preview } = this.#drawState;
    const r = Math.max(1, Math.round(Math.hypot(bx - centerX, bz - centerZ)));
    this.#drawState.currentRadius = r;
    preview.setAttribute("cx", centerX);
    preview.setAttribute("cy", centerZ);
    preview.setAttribute("rx", r);
    preview.setAttribute("ry", r);
  }

  #completeCircle(bx, bz) {
    const { centerX, centerZ, preview, dot } = this.#drawState;
    preview?.parentNode?.removeChild(preview);
    dot?.parentNode?.removeChild(dot);
    this.#drawState = null;
    const radius = Math.max(1, Math.round(Math.hypot(bx - centerX, bz - centerZ)));
    this.#callbacks.onShapeCreated?.({
      type: "circle", operation: this.#activeOperation, override: false,
      center_x: centerX, center_z: centerZ, radius,
    });
  }

  // Polygon (click vertices, close on first-vertex click or dblclick) ──────────
  #startPolygon(bx, bz) {
    this.#drawHandleData = [{ wx: bx, wz: bz, isFirst: true }];
    this.refreshDrawHandles();
    const previewLine = svgEl("line", {
      x1: bx, y1: bz, x2: bx, y2: bz, stroke: "var(--text-muted)", "stroke-width": "1",
      "stroke-dasharray": "4 3", "pointer-events": "none", "vector-effect": "non-scaling-stroke",
    });
    this.#drawLayer.appendChild(previewLine);
    this.#drawState = { type: "polygon", vertices: [[bx, bz]], lines: [], previewLine };
  }

  #addPolygonVertex(bx, bz) {
    this.#drawHandleData.push({ wx: bx, wz: bz, isFirst: false });
    this.refreshDrawHandles();
    const ds   = this.#drawState;
    const prev = ds.vertices[ds.vertices.length - 1];
    const seg  = svgEl("line", {
      x1: prev[0], y1: prev[1], x2: bx, y2: bz, stroke: "var(--text-muted)", "stroke-width": "1",
      "pointer-events": "none", "vector-effect": "non-scaling-stroke",
    });
    this.#drawLayer.insertBefore(seg, ds.previewLine);
    ds.lines.push(seg);
    ds.vertices.push([bx, bz]);
    ds.previewLine.setAttribute("x1", bx);
    ds.previewLine.setAttribute("y1", bz);
    ds.previewLine.setAttribute("x2", bx);
    ds.previewLine.setAttribute("y2", bz);
  }

  #updatePolygonPreview(bx, bz) {
    if (!this.#drawState?.previewLine) return;
    this.#drawState.previewLine.setAttribute("x2", bx);
    this.#drawState.previewLine.setAttribute("y2", bz);
  }

  #closePolygon() {
    this.#drawHandleData = [];
    this.refreshDrawHandles();
    const saved = this.#drawState;
    this.#drawState = null;
    for (const el of [...(saved.lines ?? []), saved.previewLine]) el?.parentNode?.removeChild(el);
    if (saved.vertices.length < 3) return;
    this.#callbacks.onShapeCreated?.({
      type: "polygon", operation: this.#activeOperation, override: false, vertices: saved.vertices,
    });
  }

  // Lasso (hold drag to trace freeform; release to close) ──────────────────────
  #startLasso(bx, bz) {
    const previewPath = svgEl("path", {
      fill: this.#opFill(), stroke: this.#opStroke(),
      "stroke-width": "1", "fill-opacity": "0.20", "stroke-dasharray": "5 3",
      "fill-rule": "evenodd", "vector-effect": "non-scaling-stroke",
    });
    this.#drawLayer.appendChild(previewPath);
    this.#drawState = { type: "lasso", vertices: [[bx, bz]], previewPath };
  }

  #addLassoPoint(bx, bz) {
    const { vertices } = this.#drawState;
    const last = vertices[vertices.length - 1];
    if (bx === last[0] && bz === last[1]) return;
    vertices.push([bx, bz]);
    this.#updateLassoPreview();
  }

  #updateLassoPreview() {
    const { vertices, previewPath } = this.#drawState;
    if (vertices.length < 2) return;
    previewPath.setAttribute("d", ringToPath(vertices, identityTransform));
  }

  #completeLasso() {
    const { vertices, previewPath } = this.#drawState;
    previewPath?.parentNode?.removeChild(previewPath);
    this.#drawState = null;
    if (vertices.length < 3) return;
    this.#callbacks.onShapeCreated?.({
      type: "lasso", operation: this.#activeOperation, override: false, vertices,
    });
  }
}
