/**
 * Shared shape dispatch — the one place a *type* becomes a drawing.
 * Used by WorldCanvas (region outlines) and the sketch canvas (sketch shapes), so the rule that a
 * cylinder is an ellipse and a point is a fixed-size dot is written once.
 */

const RADIAL_TYPES = new Set(["cylinder", "circle", "sphere"]);

/**
 * Paint a geometric shape onto a `CanvasPainter`.
 *
 * @param {CanvasPainter} painter
 * @param {string} type          — shape/region type ("rectangle", "cylinder", "circle", "point", …)
 * @param {object} boundsOrPoly  — either {min_x, min_z, max_x, max_z} extent bounds,
 *                                 or a polygon object {exterior, holes} / {polygons}
 * @param {object} [style={}]     — the painter's style vocabulary
 */
export function paintShape(painter, type, boundsOrPoly, style = {}) {
  if (!boundsOrPoly) return;

  // Polygon case: a resolved region or an island outline, holes and all.
  if (boundsOrPoly.exterior !== undefined || boundsOrPoly.polygons !== undefined) {
    if (!boundsOrPoly.exterior?.length && !boundsOrPoly.polygons?.length) return;
    painter.poly(boundsOrPoly, style);
    return;
  }

  const { min_x, min_z, max_x, max_z } = boundsOrPoly;

  // A point is a fixed-size dot at the bounds centre — its radius is in surface units rather than world
  // ones, so it stays findable instead of shrinking away like the 1×1 rect it stands for.
  if (type === "point") {
    painter.dot((min_x + max_x) / 2, (min_z + max_z) / 2, { radius: 5, ...style });
    return;
  }

  if (RADIAL_TYPES.has(type)) painter.ellipse(boundsOrPoly, style);
  else                        painter.rect(boundsOrPoly, style);
}

/**
 * The 1×1 block marker that pins a corner — a drawn region's anchors and a draw preview's endpoints are
 * the same thing, so they are drawn the same way: the block itself, half-filled and firmly outlined.
 */
export function paintAnchorBlock(painter, bx, bz, color) {
  painter.rect({ min_x: bx, min_z: bz, max_x: bx + 1, max_z: bz + 1 },
    { fill: color, fillAlpha: 0.5, stroke: color, width: 2 });
}
