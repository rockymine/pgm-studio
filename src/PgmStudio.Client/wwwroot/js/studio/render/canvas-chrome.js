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
 *   • `renderTransformBox` — the box a selection is scaled and rotated by: four corner anchors and four
 *     edge grab bands. One emission for every surface that offers a transform, so a region, a piece and a
 *     shape wear the same box in the same place and a reader learns one set of grips.
 *
 * The split between the two dialects is the hybrid surface itself: the working area is world content and
 * is painted, the scale bar and the transform box are screen chrome and stay SVG layers. All are
 * stateless — data in, drawn out.
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

/**
 * A transform box has **four** anchors, one per corner, and its four edges are grab zones rather than
 * things drawn. A corner pushes and pulls both axes at once; hovering an edge shows the one-dimensional
 * double-headed arrow and dragging it stretches or squashes along that axis alone.
 *
 * That is the whole reason the box can sit ON the selection's bounds with no offset. A ninth grip at an
 * edge midpoint is the one that has nowhere to go: on a rectangular outline it lands on the midpoint-insert
 * ghost, and one of the two always loses. An edge with no anchor on it has nothing to collide with, so the
 * insert ghost and the stretch share the edge — the ghost is a target on the outline, the stretch a band on
 * the box, and the pointer is never over both meaning two things.
 *
 * `nx`/`nz` are normalized positions on the box (0 = min · 0.5 = mid · 1 = max) and are the vocabulary every
 * caller reads through `gripSideX`/`gripSideZ`; `key` names the grip for callers that map it to fields.
 */
export const TRANSFORM_CORNERS = [
  { key: "nw", nx: 0, nz: 0, cursor: "nwse-resize" },
  { key: "ne", nx: 1, nz: 0, cursor: "nesw-resize" },
  { key: "se", nx: 1, nz: 1, cursor: "nwse-resize" },
  { key: "sw", nx: 0, nz: 1, cursor: "nesw-resize" },
];

export const TRANSFORM_EDGES = [
  { key: "n", nx: 0.5, nz: 0,   cursor: "ns-resize" },
  { key: "e", nx: 1,   nz: 0.5, cursor: "ew-resize" },
  { key: "s", nx: 0.5, nz: 1,   cursor: "ns-resize" },
  { key: "w", nx: 0,   nz: 0.5, cursor: "ew-resize" },
];

/** Which side of the box a grip drags on each axis — −1 the min side, 1 the max, 0 neither. The anchor a
 *  scale works from is the opposite side. Each caller maps this to what it states: a bound, a cell edge or
 *  a field name. */
export const gripSideX = (grip) => (grip.nx === 0.5 ? 0 : grip.nx === 0 ? -1 : 1);
export const gripSideZ = (grip) => (grip.nz === 0.5 ? 0 : grip.nz === 0 ? -1 : 1);

const ROTATE_HALF = 9;
// How far outside the box a rotate zone sits. The corner anchors are drawn ON the box, so a zone at the
// corner would take the press that scales; it starts past the anchor it flanks instead.
const ROTATE_PAD  = 14;
// An edge grab band straddles its edge by this much either way, and stops this far short of each corner so
// the corner anchor owns the corner. An edge with no room left for a band between them gets none.
const EDGE_GRAB   = 5;
const EDGE_INSET  = 9;

// A rotate cursor (circular arrow, white halo so it reads on both themes).
const ROTATE_ICON = "<svg xmlns='http://www.w3.org/2000/svg' width='26' height='26' viewBox='0 0 24 24' fill='none' stroke-linecap='round' stroke-linejoin='round'><g stroke='white' stroke-width='4'><path d='M21 12a9 9 0 1 1-3-6.7'/><path d='M21 3v5h-5'/></g><g stroke='black' stroke-width='2'><path d='M21 12a9 9 0 1 1-3-6.7'/><path d='M21 3v5h-5'/></g></svg>";
export const ROTATE_CURSOR = `url("data:image/svg+xml,${encodeURIComponent(ROTATE_ICON)}") 13 13, crosshair`;

/**
 * Draw the box a selection is transformed by into a screen-space layer: four corner anchors on the
 * selection's own bounds, an invisible grab band along each edge, and — where a caller offers one — four
 * rotate zones outside the corners. Every authoring surface draws this one box, so a reader who has learnt
 * the corners of a region has learnt a piece's and a shape's.
 *
 * `box` is the selection's screen rect `{ l, t, r, b }`. `onScale(grip, event)` opens a scale drag and
 * `onRotate(event)` a rotation; omitting either leaves that affordance undrawn, which is how a read-only
 * surface shows what is selected and offers no hold on it. `outline` draws the dashed box itself — off for
 * a caller that has already drawn its own selection outline.
 */
export function renderTransformBox(layer, box, {
  onScale, onRotate, outline = true, gripHalf = 5,
  fill = "var(--bg-deep)", stroke = "var(--accent)",
} = {}) {
  if (!layer || !box || !isFinite(box.l) || !isFinite(box.t) || !isFinite(box.r) || !isFinite(box.b)) return;
  const { l, t, r, b } = box;
  if (outline) layer.appendChild(svgEl("rect", {
    x: l, y: t, width: r - l, height: b - t, fill: "none",
    stroke, "stroke-width": "1.5", "stroke-dasharray": "5 3", "pointer-events": "none",
  }));
  if (!onScale) return;

  // Edge bands first, then the rotate zones, then the corner anchors — so where two targets meet, the one
  // drawn later takes the press, and a corner always beats the edge running up to it.
  const spanX = r - l, spanZ = b - t;
  for (const grip of TRANSFORM_EDGES) {
    const horizontal = grip.nz !== 0.5;
    const along = horizontal ? spanX : spanZ;
    if (along < EDGE_INSET * 2 + 6) continue;   // nothing left between the corners to grab
    const zone = svgEl("rect", horizontal
      ? { x: l + EDGE_INSET, y: t + grip.nz * spanZ - EDGE_GRAB, width: along - EDGE_INSET * 2, height: EDGE_GRAB * 2 }
      : { x: l + grip.nx * spanX - EDGE_GRAB, y: t + EDGE_INSET, width: EDGE_GRAB * 2, height: along - EDGE_INSET * 2 });
    zone.setAttribute("fill", "transparent");
    zone.style.cursor = grip.cursor;
    zone.addEventListener("mousedown", (e) => onScale(grip, e));
    zone.addEventListener("click", (e) => e.stopPropagation());
    layer.appendChild(zone);
  }

  if (onRotate) {
    for (const [ax, ay, sx, sy] of [[l, t, -1, -1], [r, t, 1, -1], [r, b, 1, 1], [l, b, -1, 1]]) {
      const zone = svgEl("rect", {
        x: ax + sx * ROTATE_PAD - ROTATE_HALF, y: ay + sy * ROTATE_PAD - ROTATE_HALF,
        width: ROTATE_HALF * 2, height: ROTATE_HALF * 2, fill: "transparent",
      });
      zone.style.cursor = ROTATE_CURSOR;
      zone.addEventListener("mousedown", (e) => onRotate(e));
      zone.addEventListener("click", (e) => e.stopPropagation());
      layer.appendChild(zone);
    }
  }

  for (const grip of TRANSFORM_CORNERS) {
    const hx = l + grip.nx * spanX, hy = t + grip.nz * spanZ;
    const el = svgEl("rect", {
      x: hx - gripHalf, y: hy - gripHalf, width: gripHalf * 2, height: gripHalf * 2, rx: 1,
      fill, stroke, "stroke-width": "1.5",
    });
    el.style.cursor = grip.cursor;
    el.addEventListener("mousedown", (e) => onScale(grip, e));
    el.addEventListener("click", (e) => e.stopPropagation());
    layer.appendChild(el);
  }
}
