/**
 * Shared drawing-surface chrome for the authoring canvases (sketch + plan) — the three pieces that make
 * an unbounded surface readable:
 *
 *   • `viewportWorldRect` — the world rect currently on screen. The grid spans THIS (snapped out to whole
 *     grid steps) rather than the content, so the surface is never fenced in: wherever you pan or zoom
 *     there is grid to draw on, and growing the drawing is just drawing.
 *   • `paintWorkArea`     — the tinted **working area**: a default-sized region that exists even when
 *     nothing is drawn, growing to enclose the content plus a buffer. It is the size anchor — the reason
 *     a blank canvas reads as "a map is about this big" instead of an open field.
 *   • `renderScaleBar`    — a screen-space "N blocks" bar, so absolute size is legible at any zoom
 *     (the working area's edge moves as the drawing grows, so it can't serve as the scale reference).
 *
 * The split between the two is the hybrid surface itself: the working area is world content and is
 * painted, the scale bar is screen chrome and stays an SVG layer. Both are stateless — data in, drawn out.
 */

import { svgEl } from "./svg.js";

function clear(layer) { while (layer.firstChild) layer.removeChild(layer.firstChild); }

/** The world-coordinate rect currently visible in a `w`×`h` surface under the given viewport transform. */
export function viewportWorldRect(w, h, scale, panX, panY) {
  const s = scale || 1;
  return { min_x: -panX / s, min_z: -panY / s, max_x: (w - panX) / s, max_z: (h - panY) / s };
}

/**
 * How coarse a grid should draw, given how many pixels one of its units spans on screen. The authoring
 * grids span the VISIBLE world, so zooming out adds lines without bound — and past a few pixels apart they
 * are unreadable anyway, so drawing every unit is simultaneously the most expensive and the least useful
 * option. Stepping up at fixed thresholds is what map software calls a zoom level: detail changes at a few
 * points instead of continuously, which keeps the line count flat and lets a grid memo hold across most of
 * a zoom instead of missing on every wheel tick. Pairs: [min px per unit, multiple to draw].
 */
const GRID_STEPS = [[10, 1], [5, 2], [2, 5], [0, 10]];

export function gridStep(unitPx) {
  for (const [minPx, step] of GRID_STEPS) if (unitPx >= minPx) return step;
  return 10;
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
 * themes; the border sets it apart from the dashed grid. `strokeAlpha` is the one thing the two surfaces
 * differ on — the plan board frames its area solidly, the sketch keeps the frame quieter than its content.
 */
export function paintWorkArea(painter, area, { strokeAlpha = 0.45 } = {}) {
  if (!area) return;
  painter.rect(area, {
    fill: "var(--canvas-ink, #ffffff)", fillAlpha: 0.05,
    stroke: "var(--canvas-axis, #a78bfa)", strokeAlpha, width: 1.5,
  });
}

// Block counts a scale bar is allowed to show — the round numbers a reader converts without thinking,
// with the chunk-aligned 16/32/64… kept in so the bar usually lands on whole chunks.
const NICE_BLOCKS = [1, 2, 5, 10, 16, 25, 32, 50, 64, 100, 128, 200, 256, 500, 512, 1000, 2000, 5000];
const TARGET_PX = 120;   // the bar aims for about this long, then rounds down to a nice block count

/**
 * How big the selected thing is, as a pill under it — the one place every editor answers that question, so a
 * piece, a region and a shape all say it in the same words in the same place. Screen space: the pill stays a
 * fixed size and stays put under the selection at any zoom.
 *
 * `left`/`right`/`bottom` are the selection's screen box; `width`/`depth` are what it measures in blocks.
 */
export function renderDimensionPill(layer, { left, right, bottom, width, depth, color = "var(--accent)" }) {
  if (!layer || !isFinite(left) || !isFinite(right) || !isFinite(bottom)) return;
  const fmt = value => (Number.isInteger(value) ? String(value) : value.toFixed(1));
  const text = `${fmt(width)} × ${fmt(depth)}`;
  const FONT = 10, PAD_X = 6, PAD_Y = 3;
  const boxH = FONT + PAD_Y * 2;
  const boxW = text.length * (FONT * 0.6) + PAD_X * 2;
  const mid = (left + right) / 2;
  const top = bottom + 5;
  layer.appendChild(svgEl("rect", {
    x: mid - boxW / 2, y: top, width: boxW, height: boxH, rx: 3,
    fill: color, "fill-opacity": "0.85", "pointer-events": "none",
  }));
  const label = svgEl("text", {
    x: mid, y: top + PAD_Y + FONT - 1, "text-anchor": "middle", "dominant-baseline": "auto",
    "font-size": FONT, "font-family": "ui-monospace, monospace", "font-weight": "600",
    // Dark ink: the pill is always a bright accent, so the number stays legible in either theme.
    fill: "var(--canvas-handle-fill)", "pointer-events": "none",
  });
  label.textContent = text;
  layer.appendChild(label);
}

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
