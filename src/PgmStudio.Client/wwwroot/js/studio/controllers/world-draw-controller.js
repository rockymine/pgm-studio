/**
 * WorldDrawController — rectangle/cuboid drag and cylinder/circle two-click draw for WorldCanvas.
 *
 * Like the sketch draw controller, the in-progress draw is **state, not elements**: `#drawState` holds
 * the numbers and `paint` draws them into whatever frame the canvas is painting, so every mutating entry
 * point ends by asking for one (`onPreviewChanged`).
 *
 * Constructor args:
 *   callbacks     { onRegionDraw, onPreviewChanged }
 */

import { drawnBoundsFromBlocks } from "../geometry/region-convert.js";
import { paintAnchorBlock } from "../render/shape-render.js";

const REGION_COLOR = "var(--canvas-region)";
const GUIDE_COLOR  = "var(--canvas-axis)";

export class WorldDrawController {
  #callbacks;

  #drawState  = null;
  #activeTool = null;

  constructor({ onRegionDraw, onPreviewChanged } = {}) {
    this.#callbacks = { onRegionDraw, onPreviewChanged };
  }

  setTool(tool)       { this.#activeTool = tool; }
  get activeTool()    { return this.#activeTool; }
  get isDrawing()     { return this.#drawState !== null; }

  /** Dispatch mousedown by active tool. Returns true if the event was consumed. */
  onMouseDown(bx, bz) {
    const tool = this.#activeTool;
    if (tool === "rectangle" || tool === "cuboid") {
      this.#drawState = { toolType: tool, startBx: bx, startBz: bz, currentBx: bx, currentBz: bz };
      this.#repaint();
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
    this.#drawState = null;
    this.#repaint();
  }

  /**
   * Draw the in-progress region onto the canvas's painter, from the canvas's `draw` phase. A rectangle
   * shows its two corner blocks so the exact extent is legible at the block level; a radial shows the
   * centre, the radius it is being dragged to, and that radius as a number.
   */
  paint(painter) {
    const ds = this.#drawState;
    if (!ds) return;
    if (ds.toolType === "rectangle" || ds.toolType === "cuboid") {
      const { min_x, min_z, max_x, max_z } = drawnBoundsFromBlocks(ds.startBx, ds.startBz, ds.currentBx, ds.currentBz);
      painter.rect({ min_x, min_z, max_x, max_z },
        { fill: REGION_COLOR, fillAlpha: 0.12, stroke: REGION_COLOR, width: 1.5, dash: [4, 2] });
      paintAnchorBlock(painter, min_x, min_z, REGION_COLOR);
      paintAnchorBlock(painter, max_x - 1, max_z - 1, REGION_COLOR);
      return;
    }
    const { centerX, centerZ, currentRadius, cursorX, cursorZ } = ds;
    painter.ellipse({ min_x: centerX - currentRadius, max_x: centerX + currentRadius,
                      min_z: centerZ - currentRadius, max_z: centerZ + currentRadius },
                    { stroke: GUIDE_COLOR, width: 1.5, dash: [6, 3] });
    painter.line(centerX, centerZ, cursorX ?? centerX, cursorZ ?? centerZ,
                 { stroke: GUIDE_COLOR, width: 1.5, dash: [4, 2] });
    painter.dot(centerX, centerZ, { radius: 5, fill: GUIDE_COLOR, stroke: "var(--canvas-marker-stroke)", width: 1.5 });
    painter.text(`r=${currentRadius}`, cursorX ?? centerX, cursorZ ?? centerZ, {
      fill: GUIDE_COLOR, size: 11, align: "left", baseline: "alphabetic", dx: 6, dy: -4,
    });
  }

  // ── rectangle / cuboid ───────────────────────────────────────────────────────

  #repaint() { this.#callbacks.onPreviewChanged?.(); }

  #updateDrawPreview(bx, bz) {
    if (!this.#drawState) return;
    this.#drawState.currentBx = bx;
    this.#drawState.currentBz = bz;
    this.#repaint();
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
    const centerX = bx + 0.5, centerZ = bz + 0.5;
    this.#drawState = {
      toolType: this.#activeTool,
      centerX, centerZ, cursorX: centerX, cursorZ: centerZ, currentRadius: 1,
    };
    this.#repaint();
  }

  #updateRadialPreview(bx, bz) {
    if (!this.#drawState) return;
    const { centerX, centerZ } = this.#drawState;
    const cursorX = bx + 0.5, cursorZ = bz + 0.5;
    const dx = cursorX - centerX, dz = cursorZ - centerZ;
    this.#drawState.cursorX = cursorX;
    this.#drawState.cursorZ = cursorZ;
    this.#drawState.currentRadius = Math.max(1, Math.round(Math.sqrt(dx * dx + dz * dz)));
    this.#repaint();
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
