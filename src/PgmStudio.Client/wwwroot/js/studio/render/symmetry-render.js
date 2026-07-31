/**
 * Shared symmetry overlay — the dashed axis line(s) for a symmetry type + a centre dot, in whichever
 * dialect the surface draws in. `symmetryAxes` is the geometry and is the only place the type→lines rule
 * is written, so the painted world canvas and the retained Configure preview cannot come to disagree
 * about what `mirror_d2` looks like. Every type is handled, including the diagonals and the rotations.
 */

import { svgEl } from "./svg.js";

const COLOR = "var(--canvas-symmetry)";

/**
 * The axis line(s) for a symmetry type, in world coordinates.
 *
 * @param type    mirror_x | mirror_z | mirror_d1 | mirror_d2 | rot_90 | rot_180 | null
 * @param cx, cz  centre in world coords
 * @param bbox    {min_x, min_z, max_x, max_z} — the axes overhang it, so they read as unbounded
 * @returns {Array<{x1,z1,x2,z2}>} empty when there is nothing to draw
 */
export function symmetryAxes(type, cx, cz, bbox) {
  if (!type || !bbox) return [];
  const span = Math.max(bbox.max_x - bbox.min_x, bbox.max_z - bbox.min_z) + 20;
  const axes = [];
  if (type === "mirror_x" || type === "rot_90") axes.push({ x1: cx, z1: bbox.min_z - 10, x2: cx, z2: bbox.max_z + 10 });
  if (type === "mirror_z" || type === "rot_90" || type === "rot_180") axes.push({ x1: bbox.min_x - 10, z1: cz, x2: bbox.max_x + 10, z2: cz });
  if (type === "mirror_d1") axes.push({ x1: cx - span, z1: cz - span, x2: cx + span, z2: cz + span });
  if (type === "mirror_d2") axes.push({ x1: cx - span, z1: cz + span, x2: cx + span, z2: cz - span });
  return axes;
}

/** Paint the overlay onto a `CanvasPainter` — the world canvas's dialect. */
export function paintSymmetryOverlay(painter, type, cx, cz, bbox, { lineOpacity = 0.8, dotOpacity = 0.9 } = {}) {
  const axes = symmetryAxes(type, cx, cz, bbox);
  if (!axes.length) return;
  painter.segments(axes, { stroke: COLOR, width: 1.5, dash: [6, 3], alpha: lineOpacity });
  painter.dot(cx, cz, {
    radius: 5, fill: COLOR, stroke: "var(--canvas-marker-stroke)", width: 1.5, alpha: dotOpacity,
  });
}

/** Emit the overlay into an SVG `<g>` (cleared first) — the fixed-fit Configure preview's dialect. */
export function renderSymmetryOverlay(group, type, cx, cz, bbox, toSvg, { lineOpacity = 0.8, dotOpacity = 0.9 } = {}) {
  if (!group) return;
  while (group.firstChild) group.removeChild(group.firstChild);
  if (!toSvg) return;
  const axes = symmetryAxes(type, cx, cz, bbox);
  if (!axes.length) return;

  for (const { x1, z1, x2, z2 } of axes) {
    const a = toSvg(x1, z1), b = toSvg(x2, z2);
    group.appendChild(svgEl("line", {
      x1: a.x, y1: a.y, x2: b.x, y2: b.y,
      stroke: COLOR, "stroke-width": "1.5", "stroke-dasharray": "6 3", opacity: lineOpacity,
    }));
  }

  const p = toSvg(cx, cz);
  group.appendChild(svgEl("circle", {
    cx: p.x, cy: p.y, r: 5,
    fill: COLOR, stroke: "var(--canvas-marker-stroke)", "stroke-width": "1.5", opacity: dotOpacity,
  }));
}
