/**
 * EditorDrawController — rectangle/cuboid drag and cylinder/circle two-click draw
 * for EditorCanvas. Extracted from editor-canvas.js.
 *
 * Constructor args:
 *   getDrawLayer  () => SVGGElement | null   — getter, layer is rebuilt on repaint
 *   getToSvg      () => Function | null      — getter, transform is rebuilt on repaint
 *   callbacks     { onRegionDraw }
 */

import { svgEl, anchorBlockEl, moveAnchorBlockEl } from "./transform.js";
import { drawnBoundsFromBlocks } from "../shared/converters.js";

export class EditorDrawController {
  #getDrawLayer;
  #getToSvg;
  #callbacks;

  #drawState  = null;
  #activeTool = null;

  constructor(getDrawLayer, getToSvg, { onRegionDraw } = {}) {
    this.#getDrawLayer = getDrawLayer;
    this.#getToSvg     = getToSvg;
    this.#callbacks    = { onRegionDraw };
  }

  setTool(tool)       { this.#activeTool = tool; }
  get activeTool()    { return this.#activeTool; }
  get isDrawing()     { return this.#drawState !== null; }

  /** Dispatch mousedown by active tool. Returns true if the event was consumed. */
  onMouseDown(bx, bz) {
    const tool = this.#activeTool;
    if (tool === "rectangle" || tool === "cuboid") {
      this.#startDraw(bx, bz);
      return true;
    }
    if (tool === "cylinder" || tool === "circle") {
      if (!this.#drawState) this.#startRadialDraw(bx, bz);
      else                  this.#completeRadialDraw(bx, bz);
      return true;
    }
    return false;
  }

  /** Drive previews on every pointer move. */
  onMouseMove(bx, bz) {
    if (!this.#drawState) return;
    const tool = this.#activeTool;
    if (tool === "rectangle" || tool === "cuboid") this.#updateDrawPreview(bx, bz);
    else if (tool === "cylinder" || tool === "circle") this.#updateRadialPreview(bx, bz);
  }

  /** Complete rectangle/cuboid drag on mouse-up. */
  onMouseUp() {
    const tool = this.#activeTool;
    if ((tool === "rectangle" || tool === "cuboid") && this.#drawState) {
      this.#completeDraw();
    }
  }

  /** Cancel any in-progress draw (called from canvas.setActiveTool and #repaint). */
  cancel() {
    if (!this.#drawState) return;
    const layer = this.#getDrawLayer();
    if (layer) while (layer.firstChild) layer.removeChild(layer.firstChild);
    this.#drawState = null;
  }

  // ── rectangle / cuboid ───────────────────────────────────────────────────────

  #startDraw(bx, bz) {
    const layer = this.#getDrawLayer();
    const toSvg = this.#getToSvg();
    if (!layer || !toSvg) return;
    const color = "var(--canvas-region)";
    const previewRect = svgEl("rect", {
      x: 0, y: 0, width: 0, height: 0,
      fill: color, "fill-opacity": "0.12",
      stroke: color, "stroke-width": "1.5", "stroke-dasharray": "4,2",
      "vector-effect": "non-scaling-stroke", "pointer-events": "none",
    });
    const anchor1 = anchorBlockEl(toSvg, bx, bz, color);
    const anchor2 = anchorBlockEl(toSvg, bx, bz, color);
    layer.appendChild(previewRect);
    layer.appendChild(anchor1);
    layer.appendChild(anchor2);
    this.#drawState = {
      toolType: this.#activeTool,
      startBx: bx, startBz: bz, currentBx: bx, currentBz: bz,
      previewRect, anchor1, anchor2,
    };
    this.#updateDrawPreview(bx, bz);
  }

  #updateDrawPreview(bx, bz) {
    const toSvg = this.#getToSvg();
    if (!this.#drawState || !toSvg) return;
    this.#drawState.currentBx = bx;
    this.#drawState.currentBz = bz;
    const { startBx, startBz, previewRect, anchor1, anchor2 } = this.#drawState;
    const { min_x, min_z, max_x, max_z } = drawnBoundsFromBlocks(startBx, startBz, bx, bz);
    const p1 = toSvg(min_x, min_z), p2 = toSvg(max_x, max_z);
    previewRect.setAttribute("x",      Math.min(p1.x, p2.x));
    previewRect.setAttribute("y",      Math.min(p1.y, p2.y));
    previewRect.setAttribute("width",  Math.abs(p2.x - p1.x));
    previewRect.setAttribute("height", Math.abs(p2.y - p1.y));
    moveAnchorBlockEl(toSvg, anchor1, min_x,     min_z);
    moveAnchorBlockEl(toSvg, anchor2, max_x - 1, max_z - 1);
  }

  #completeDraw() {
    if (!this.#drawState) return;
    const { toolType, startBx, startBz, currentBx, currentBz } = this.#drawState;
    const bounds = drawnBoundsFromBlocks(startBx, startBz, currentBx, currentBz);
    this.cancel();
    this.#callbacks.onRegionDraw?.({ type: toolType, ...bounds });
  }

  // ── cylinder / circle (two-click radial) ────────────────────────────────────

  #startRadialDraw(bx, bz) {
    const layer = this.#getDrawLayer();
    const toSvg = this.#getToSvg();
    if (!layer || !toSvg) return;
    const centerX = bx + 0.5, centerZ = bz + 0.5;
    const pt = toSvg(centerX, centerZ);
    const dot = svgEl("circle", {
      cx: pt.x, cy: pt.y, r: 5,
      fill: "var(--canvas-axis)", stroke: "var(--canvas-marker-stroke)", "stroke-width": "1.5",
      "pointer-events": "none",
    });
    const line = svgEl("line", {
      x1: pt.x, y1: pt.y, x2: pt.x, y2: pt.y,
      stroke: "var(--canvas-axis)", "stroke-width": "1.5", "stroke-dasharray": "4 2",
      "vector-effect": "non-scaling-stroke", "pointer-events": "none",
    });
    const previewCircle = svgEl("ellipse", {
      cx: pt.x, cy: pt.y, rx: 0, ry: 0,
      fill: "none", stroke: "var(--canvas-axis)", "stroke-width": "1.5",
      "stroke-dasharray": "6 3", "vector-effect": "non-scaling-stroke",
      "pointer-events": "none",
    });
    const label = svgEl("text", {
      x: pt.x, y: pt.y,
      fill: "var(--canvas-axis)", "font-size": "11",
      "text-anchor": "start", "pointer-events": "none",
    });
    layer.append(previewCircle, line, dot, label);
    this.#drawState = {
      toolType: this.#activeTool,
      centerX, centerZ, dot, line, previewCircle, label, currentRadius: 1,
    };
  }

  #updateRadialPreview(bx, bz) {
    const toSvg = this.#getToSvg();
    if (!this.#drawState || !toSvg) return;
    const { centerX, centerZ, line, previewCircle, label } = this.#drawState;
    const cursorX = bx + 0.5, cursorZ = bz + 0.5;
    const dx = cursorX - centerX, dz = cursorZ - centerZ;
    const radius = Math.max(1, Math.round(Math.sqrt(dx * dx + dz * dz)));
    this.#drawState.currentRadius = radius;
    const cPt  = toSvg(centerX, centerZ);
    const rxPt = toSvg(centerX + radius, centerZ);
    const rzPt = toSvg(centerX, centerZ + radius);
    const endPt = toSvg(cursorX, cursorZ);
    line.setAttribute("x2", endPt.x); line.setAttribute("y2", endPt.y);
    previewCircle.setAttribute("rx", Math.abs(rxPt.x - cPt.x));
    previewCircle.setAttribute("ry", Math.abs(rzPt.y - cPt.y));
    label.setAttribute("x", endPt.x + 6); label.setAttribute("y", endPt.y - 4);
    label.textContent = `r=${radius}`;
  }

  #completeRadialDraw(bx, bz) {
    if (!this.#drawState) return;
    const { toolType, centerX, centerZ } = this.#drawState;
    this.#updateRadialPreview(bx, bz);
    const r = this.#drawState.currentRadius;
    this.cancel();
    if (!this.#callbacks.onRegionDraw) return;
    if (toolType === "circle") {
      this.#callbacks.onRegionDraw({ type: "circle", center_x: centerX, center_z: centerZ, radius: r });
    } else {
      this.#callbacks.onRegionDraw({ type: "cylinder", base_x: centerX - 0.5, base_z: centerZ - 0.5, radius: r });
    }
  }
}
