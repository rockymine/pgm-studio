/**
 * Shared SVG shape renderer.
 * Used by both EditorCanvas (editor region outlines) and the sketch canvas
 * (sketch shapes) to avoid duplicating type-dispatch logic.
 */

import { svgEl, polyToPath } from "./svg.js";

const RADIAL_TYPES = new Set(["cylinder", "circle", "sphere"]);

/**
 * Create an SVG element for a geometric shape.
 *
 * @param {string} type          — shape/region type ("rectangle", "cylinder", "circle", etc.)
 * @param {object} boundsOrPoly  — either {min_x, min_z, max_x, max_z} extent bounds,
 *                                 or a polygon_2d object {exterior, holes} / {polygons}
 * @param {Function} toSvg       — world→SVG coordinate transform (wx, wz) => {x, y}
 * @param {object} [attrs={}]    — SVG attributes to set on the element
 * @returns {SVGElement|null}
 */
export function renderShape(type, boundsOrPoly, toSvg, attrs = {}) {
  if (!boundsOrPoly) return null;

  // Polygon case: polygon_2d object with exterior ring
  if (boundsOrPoly.exterior !== undefined || boundsOrPoly.polygons !== undefined) {
    if (!boundsOrPoly.exterior?.length && !boundsOrPoly.polygons?.length) return null;
    const d = polyToPath(boundsOrPoly, toSvg);
    if (!d) return null;
    return svgEl("path", { d, "fill-rule": "evenodd", ...attrs });
  }

  const { min_x, min_z, max_x, max_z } = boundsOrPoly;
  const p1 = toSvg(min_x, min_z);
  const p2 = toSvg(max_x, max_z);

  // A point is a fixed-size dot at the bounds centre (radius in screen units, so it doesn't shrink with
  // zoom like a 1-block rect would). `attrs.r` overrides the default (a marker sets its own radius).
  if (type === "point") {
    return svgEl("circle", { cx: (p1.x + p2.x) / 2, cy: (p1.y + p2.y) / 2, r: 5, ...attrs });
  }

  if (RADIAL_TYPES.has(type)) {
    return svgEl("ellipse", {
      cx: (p1.x + p2.x) / 2,
      cy: (p1.y + p2.y) / 2,
      rx: Math.abs(p2.x - p1.x) / 2,
      ry: Math.abs(p2.y - p1.y) / 2,
      ...attrs,
    });
  }

  return svgEl("rect", {
    x:      Math.min(p1.x, p2.x),
    y:      Math.min(p1.y, p2.y),
    width:  Math.abs(p2.x - p1.x),
    height: Math.abs(p2.y - p1.y),
    ...attrs,
  });
}
