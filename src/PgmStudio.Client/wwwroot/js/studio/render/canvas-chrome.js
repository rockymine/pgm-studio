/**
 * Shared drawing-surface chrome for the authoring canvases (sketch + plan) — the three pieces that make
 * an unbounded surface readable:
 *
 *   • `viewportWorldRect` — the world rect currently on screen. The grid spans THIS (snapped out to whole
 *     grid steps) rather than the content, so the surface is never fenced in: wherever you pan or zoom
 *     there is grid to draw on, and growing the drawing is just drawing.
 *   • `renderWorkArea`    — the tinted **working area**: a default-sized region that exists even when
 *     nothing is drawn, growing to enclose the content plus a buffer. It is the size anchor — the reason
 *     a blank canvas reads as "a map is about this big" instead of an open field.
 *   • `renderScaleBar`    — a screen-space "N blocks" bar, so absolute size is legible at any zoom
 *     (the working area's edge moves as the drawing grows, so it can't serve as the scale reference).
 *
 * Stateless: each takes a layer <g> plus data and repaints it. The canvas owns the layer lifecycle.
 */

import { svgEl } from "./svg.js";

function clear(layer) { while (layer.firstChild) layer.removeChild(layer.firstChild); }

/** The world-coordinate rect currently visible in a `w`×`h` surface under the given viewport transform. */
export function viewportWorldRect(w, h, scale, panX, panY) {
  const s = scale || 1;
  return { min_x: -panX / s, min_z: -panY / s, max_x: (w - panX) / s, max_z: (h - panY) / s };
}

/** Grow a world rect outward to whole multiples of `step`. */
export function snapOut(rect, step) {
  return {
    min_x: Math.floor(rect.min_x / step) * step, min_z: Math.floor(rect.min_z / step) * step,
    max_x: Math.ceil(rect.max_x / step) * step,  max_z: Math.ceil(rect.max_z / step) * step,
  };
}

/** Smallest rect enclosing both; either may be null. */
export function unionRect(a, b) {
  if (!a) return b;
  if (!b) return a;
  return {
    min_x: Math.min(a.min_x, b.min_x), min_z: Math.min(a.min_z, b.min_z),
    max_x: Math.max(a.max_x, b.max_x), max_z: Math.max(a.max_z, b.max_z),
  };
}

/**
 * The working-area backdrop (world space, so it pans/zooms with the drawing). A low-opacity `--canvas-ink`
 * fill reads as a lift in the dark viewport and a shade in the light one, so the region is visible on both
 * themes; the solid border sets it apart from the dashed grid.
 */
export function renderWorkArea(layer, area, toSvg) {
  clear(layer);
  if (!area) return;
  const p1 = toSvg(area.min_x, area.min_z), p2 = toSvg(area.max_x, area.max_z);
  layer.appendChild(svgEl("rect", {
    x: Math.min(p1.x, p2.x), y: Math.min(p1.y, p2.y),
    width: Math.abs(p2.x - p1.x), height: Math.abs(p2.y - p1.y),
    fill: "var(--canvas-ink)", "fill-opacity": "0.05",
    stroke: "var(--canvas-axis)", "stroke-width": "1.5", "stroke-opacity": "0.45",
    "vector-effect": "non-scaling-stroke", "pointer-events": "none",
  }));
}

// Block counts a scale bar is allowed to show — the round numbers a reader converts without thinking,
// with the chunk-aligned 16/32/64… kept in so the bar usually lands on whole chunks.
const NICE_BLOCKS = [1, 2, 5, 10, 16, 25, 32, 50, 64, 100, 128, 200, 256, 500, 512, 1000, 2000, 5000];
const TARGET_PX = 120;   // the bar aims for about this long, then rounds down to a nice block count

/**
 * The scale bar, bottom-right in SCREEN space (never scaled with the map). Picks the largest nice block
 * count that fits within ~TARGET_PX at the current zoom, so the bar length stays roughly constant and the
 * number does the moving.
 */
export function renderScaleBar(layer, { w, h, scale }) {
  clear(layer);
  if (!scale || !isFinite(scale) || !w || !h) return;
  const maxBlocks = TARGET_PX / scale;
  const blocks = NICE_BLOCKS.filter(b => b <= maxBlocks).pop() ?? NICE_BLOCKS[0];
  const px = blocks * scale;
  const x2 = w - 16, x1 = x2 - px, y = h - 22, tick = 5;
  const ink = { stroke: "var(--canvas-axis)", "stroke-width": "1.5", "vector-effect": "non-scaling-stroke" };
  layer.appendChild(svgEl("line", { x1, y1: y, x2, y2: y, ...ink }));
  layer.appendChild(svgEl("line", { x1, y1: y - tick, x2: x1, y2: y, ...ink }));
  layer.appendChild(svgEl("line", { x1: x2, y1: y - tick, x2, y2: y, ...ink }));
  const t = svgEl("text", {
    x: (x1 + x2) / 2, y: y - tick - 3, "text-anchor": "middle",
    "font-size": "11", "font-family": "ui-monospace, monospace", "font-weight": "600",
    fill: "var(--canvas-axis)", "pointer-events": "none",
    "paint-order": "stroke", stroke: "var(--bg-canvas)", "stroke-width": "3", "stroke-linejoin": "round",
  });
  t.textContent = `${blocks} blocks`;
  layer.appendChild(t);
}
