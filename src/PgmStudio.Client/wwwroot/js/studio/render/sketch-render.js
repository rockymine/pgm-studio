/**
 * Stateless paint for the sketch tool — draw primitives, island/mirror result polygons, and the setup
 * overlays (bbox / chunk grid / symmetry axis). Each function takes the canvas's `CanvasPainter` + data
 * and draws; nothing is retained between frames, so the canvas owns *when* to paint and this owns *what*
 * a thing looks like — which keeps sketch-canvas.js focused on state + interaction. Reuses
 * render/shape-render for type dispatch and render/primitive-style for the treatment tiers.
 */

import { paintShape } from "./shape-render.js";
import { primitiveStyle, opColors } from "./primitive-style.js";

function shapeStyle(shape, selected) {
  const { fill, stroke } = opColors(shape.operation);
  const style = primitiveStyle("sketch", { fill, stroke, override: shape.override });
  // Selection chrome without a class to toggle: a heavier stroke and a denser fill on the shape itself.
  return selected ? { ...style, width: 2.5, fillAlpha: 0.4 } : style;
}

/** One draw primitive: add/subtract coloured, dashed when it overrides the normal boolean order. */
export function paintSketchShape(painter, shape, { selected = false, alpha = 1 } = {}) {
  const style = { ...shapeStyle(shape, selected), alpha };
  if (shape.type === "rectangle") {
    paintShape(painter, "rectangle", shape, style);
  } else if (shape.type === "circle") {
    paintShape(painter, "circle", {
      min_x: shape.center_x - shape.radius, max_x: shape.center_x + shape.radius,
      min_z: shape.center_z - shape.radius, max_z: shape.center_z + shape.radius,
    }, style);
  } else if ((shape.type === "polygon" || shape.type === "lasso") && (shape.vertices?.length ?? 0) >= 3) {
    painter.ring(shape.vertices, { ...style, fillRule: "evenodd" }, shape.controls || {});
  }
}

/** Ghost preview of a library item being placed — the (already world-positioned) shape specs, faded. */
export function paintPlaceGhost(painter, specs) {
  for (const spec of specs ?? []) paintSketchShape(painter, spec, { alpha: 0.55 });
}

/** The computed island result polygons (exterior + holes). */
export function paintIslands(painter, islands) {
  for (const island of islands ?? []) {
    if (!island?.exterior?.length) continue;
    painter.poly({ exterior: island.exterior, holes: island.holes ?? [] }, {
      fill: "var(--canvas-result-fill)", fillAlpha: 0.22,
      stroke: "var(--canvas-result-stroke)", width: 1.5,
    });
  }
}

/** The *other* layers' island outlines, faintly — context for aligning the active layer. */
export function paintGhostIslands(painter, polys) {
  for (const poly of polys ?? []) {
    if (!poly?.exterior?.length) continue;
    painter.poly({ exterior: poly.exterior, holes: poly.holes ?? [] }, {
      fill: "var(--canvas-island)", fillAlpha: 0.07,
      stroke: "var(--canvas-island)", width: 1, dash: [2, 4],
    });
  }
}

/** The live mirror-preview polygons. */
export function paintMirror(painter, polys) {
  for (const poly of polys ?? []) {
    if (!poly?.exterior?.length) continue;
    painter.poly({ exterior: poly.exterior, holes: poly.holes ?? [] }, {
      fill: "var(--canvas-mirror-fill)", stroke: "var(--canvas-mirror-stroke)", width: 1,
    });
  }
}

/** The working-bounds rectangle — the tight world bound of what a finish would rasterize. */
export function paintBbox(painter, bbox) {
  if (!bbox) return;
  painter.rect(bbox, { stroke: "var(--border)", width: 1 });
}

/**
 * The chunk grid across the visible extent. `step` is a multiple of the chunk: 1 draws every chunk line,
 * higher values thin the grid out when a chunk is only a few pixels across (see canvas-chrome's
 * gridStep), so zooming out cannot grow the line count without bound. Every line is one path.
 */
export function paintChunkGrid(painter, bbox, step = 1) {
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const span = 16 * Math.max(1, step);
  const runs = [];
  for (let x = Math.ceil(min_x / span) * span; x <= max_x; x += span) runs.push({ x1: x, z1: min_z, x2: x, z2: max_z });
  for (let z = Math.ceil(min_z / span) * span; z <= max_z; z += span) runs.push({ x1: min_x, z1: z, x2: max_x, z2: z });
  painter.segments(runs, { stroke: "var(--canvas-chunk)", width: 1, dash: [3, 3] });
}

/** The symmetry axis line(s) for the current mirror mode, through the centre, clipped to the bbox. */
export function paintAxis(painter, bbox, center, mode) {
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const cx = center?.cx ?? 0, cz = center?.cz ?? 0;
  const style = { stroke: "var(--canvas-axis)", width: 1, dash: [6, 4], alpha: 0.75 };
  const runs = [];
  if (mode !== "mirror_z") runs.push({ x1: cx, z1: min_z, x2: cx, z2: max_z });
  if (mode !== "mirror_x") runs.push({ x1: min_x, z1: cz, x2: max_x, z2: cz });
  painter.segments(runs, style);
}
