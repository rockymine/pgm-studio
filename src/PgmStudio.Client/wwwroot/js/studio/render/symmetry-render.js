/**
 * Shared symmetry-overlay renderer — the dashed axis line(s) for a symmetry type + a centre dot.
 * Used by EditorCanvas, ConfigureRenderer and OverviewRenderer so all three draw the same overlay
 * (and all handle every type, incl. the diagonals `mirror_d1`/`mirror_d2` and the rotations).
 *
 * @param group   the SVG <g> to draw into (cleared first)
 * @param type    symmetry type: mirror_x | mirror_z | mirror_d1 | mirror_d2 | rot_90 | rot_180 | null
 * @param cx, cz  centre in world coords
 * @param bbox    {min_x, min_z, max_x, max_z}
 * @param toSvg   world→SVG transform
 * @param opts    { lineOpacity = 0.8, dotOpacity = 0.9 }
 */

import { svgEl } from "./svg.js";

const COLOR = "var(--canvas-symmetry)";

export function renderSymmetryOverlay(group, type, cx, cz, bbox, toSvg, { lineOpacity = 0.8, dotOpacity = 0.9 } = {}) {
  if (!group) return;
  while (group.firstChild) group.removeChild(group.firstChild);
  if (!type || !bbox || !toSvg) return;

  const axis = (x1, z1, x2, z2) => {
    const a = toSvg(x1, z1), b = toSvg(x2, z2);
    group.appendChild(svgEl("line", {
      x1: a.x, y1: a.y, x2: b.x, y2: b.y,
      stroke: COLOR, "stroke-width": "1.5", "stroke-dasharray": "6 3", opacity: lineOpacity,
    }));
  };

  const span = Math.max(bbox.max_x - bbox.min_x, bbox.max_z - bbox.min_z) + 20;
  if (type === "mirror_x" || type === "rot_90") axis(cx, bbox.min_z - 10, cx, bbox.max_z + 10);
  if (type === "mirror_z" || type === "rot_90" || type === "rot_180") axis(bbox.min_x - 10, cz, bbox.max_x + 10, cz);
  if (type === "mirror_d1") axis(cx - span, cz - span, cx + span, cz + span);
  if (type === "mirror_d2") axis(cx - span, cz + span, cx + span, cz - span);

  const p = toSvg(cx, cz);
  group.appendChild(svgEl("circle", {
    cx: p.x, cy: p.y, r: 5,
    fill: COLOR, stroke: "var(--canvas-marker-stroke)", "stroke-width": "1.5", opacity: dotOpacity,
  }));
}
