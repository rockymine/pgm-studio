/**
 * SVG element factory + path-string builders.
 *
 * The **retained** dialect: the screen-space overlays every canvas keeps in the svg (labels, handles,
 * pills, the scale bar) and the fixed-fit previews that have no viewport transform to go stale. The
 * painted dialect is `render/canvas-painter.js`.
 *
 * The path builders are shared by both, not duplicated for each: they are pure string math (no DOM,
 * testable with a stub `toSvg`), and a canvas `Path2D` takes SVG path data outright — so a ring is
 * turned into a `d` here whichever surface ends up drawing it.
 */

/** Create an SVG element with given attributes. */
export function svgEl(tag, attrs = {}, children = []) {
  const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  for (const ch of children) el.appendChild(ch);
  return el;
}

/** Return the x/y/width/height attributes for a centered square handle rect. */
export function handleRectAttrs(cx, cy, half) {
  return { x: cx - half, y: cy - half, width: half * 2, height: half * 2 };
}

/**
 * Convert a polygon ring [[x,z],...] to an SVG path string.
 * Pass controls (shape.controls or {}) to emit cubic Bézier C commands for
 * curved edges; omit or pass {} for all-straight output (backward compatible).
 *
 * controls format: { "vertexIdx": { in?: [x,z], out?: [x,z] } }
 *   out = control point on the outgoing edge from that vertex
 *   in  = control point on the incoming edge to that vertex
 */
export function ringToPath(ring, toSvg, controls = {}) {
  const n = ring.length;
  if (n === 0) return "";
  const fmt = ({ x, y }) => `${x.toFixed(1)},${y.toFixed(1)}`;
  const pt  = (wx, wz) => toSvg(wx, wz);

  let d = `M${fmt(pt(ring[0][0], ring[0][1]))}`;

  for (let i = 1; i < n; i++) {
    const [x, z] = ring[i];
    const p     = pt(x, z);
    const cpOut = controls[String(i - 1)]?.out;
    const cpIn  = controls[String(i)]?.in;
    if (cpOut || cpIn) {
      const c1 = cpOut ? pt(cpOut[0], cpOut[1]) : pt(ring[i - 1][0], ring[i - 1][1]);
      const c2 = cpIn  ? pt(cpIn[0],  cpIn[1])  : p;
      d += ` C${fmt(c1)} ${fmt(c2)} ${fmt(p)}`;
    } else {
      d += ` L${fmt(p)}`;
    }
  }

  // Closing edge: ring[n-1] → ring[0]
  const cpOut = controls[String(n - 1)]?.out;
  const cpIn  = controls["0"]?.in;
  if (cpOut || cpIn) {
    const first = pt(ring[0][0], ring[0][1]);
    const c1 = cpOut ? pt(cpOut[0], cpOut[1]) : pt(ring[n - 1][0], ring[n - 1][1]);
    const c2 = cpIn  ? pt(cpIn[0],  cpIn[1])  : first;
    d += ` C${fmt(c1)} ${fmt(c2)} ${fmt(first)}`;
  }

  return d + " Z";
}

/** Convert a GeoJSON-like polygon (exterior + optional holes) to a path. */
export function polyToPath(poly, toSvg) {
  if (poly.polygons) {
    return poly.polygons
      .map(p => ringToPath(p.exterior, toSvg) + (p.holes || []).map(h => " " + ringToPath(h, toSvg)).join(""))
      .join(" ");
  }
  let d = ringToPath(poly.exterior, toSvg);
  for (const hole of (poly.holes || [])) d += " " + ringToPath(hole, toSvg);
  return d;
}

/** Convert a bounds {min_x, min_z, max_x, max_z} to an SVG ring path. */
export function boundsToRingPath(bounds, toSvg) {
  const { min_x, min_z, max_x, max_z } = bounds;
  return ringToPath(
    [[min_x, min_z], [max_x, min_z], [max_x, max_z], [min_x, max_z]],
    toSvg,
  );
}

