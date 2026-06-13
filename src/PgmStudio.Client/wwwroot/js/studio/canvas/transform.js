/**
 * Pure coordinate-math and SVG-element helpers.
 * Accepts bounding_box as {min_x, min_z, max_x, max_z}.
 */

const PAD = 20;

/**
 * Build a world→SVG transform from a bounding box.
 * @param {{min_x,min_z,max_x,max_z}} bbox
 * @returns {(wx:number,wz:number)=>{x:number,y:number}}
 */
export function buildTransform(bbox, svgW, svgH) {
  const { min_x, min_z, max_x, max_z } = bbox;
  const worldW = max_x - min_x, worldH = max_z - min_z;
  const drawW = svgW - 2 * PAD, drawH = svgH - 2 * PAD;
  const scale = Math.min(drawW / worldW, drawH / worldH);
  const offX = PAD + (drawW - worldW * scale) / 2;
  const offY = PAD + (drawH - worldH * scale) / 2;
  return (wx, wz) => ({
    x: offX + (wx - min_x) * scale,
    y: offY + (wz - min_z) * scale,
  });
}

export function buildInverseTransform(bbox, svgW, svgH) {
  const { min_x, min_z, max_x, max_z } = bbox;
  const worldW = max_x - min_x, worldH = max_z - min_z;
  const drawW = svgW - 2 * PAD, drawH = svgH - 2 * PAD;
  const scale = Math.min(drawW / worldW, drawH / worldH);
  const offX = PAD + (drawW - worldW * scale) / 2;
  const offY = PAD + (drawH - worldH * scale) / 2;
  return (px, py) => ({
    x: (px - offX) / scale + min_x,
    z: (py - offY) / scale + min_z,
  });
}

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

/** Create an SVG rect element representing a 1×1 block anchor at (bx, bz). */
export function anchorBlockEl(toSvg, bx, bz, color) {
  const p1 = toSvg(bx, bz), p2 = toSvg(bx + 1, bz + 1);
  return svgEl("rect", {
    x: Math.min(p1.x, p2.x), y: Math.min(p1.y, p2.y),
    width: Math.abs(p2.x - p1.x), height: Math.abs(p2.y - p1.y),
    fill: color, "fill-opacity": "0.5", stroke: color, "stroke-width": "2",
    "vector-effect": "non-scaling-stroke", "pointer-events": "none",
  });
}

/** Reposition an existing anchor block element to (bx, bz). */
export function moveAnchorBlockEl(toSvg, el, bx, bz) {
  const p1 = toSvg(bx, bz), p2 = toSvg(bx + 1, bz + 1);
  el.setAttribute("x",      Math.min(p1.x, p2.x));
  el.setAttribute("y",      Math.min(p1.y, p2.y));
  el.setAttribute("width",  Math.abs(p2.x - p1.x));
  el.setAttribute("height", Math.abs(p2.y - p1.y));
}

/**
 * Sutherland-Hodgman half-plane clip.
 * Clips polygon `poly` ([[x,z],...]) against the half-plane defined by
 * point (ox, oz) and inward normal (nx, nz).
 * A vertex is inside when (v.x - ox)*nx + (v.z - oz)*nz >= 0.
 */
export function clipHalfPlane(poly, ox, oz, nx, nz) {
  if (!poly.length) return [];
  const dot = ([x, z]) => (x - ox) * nx + (z - oz) * nz;
  const output = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[(i + poly.length - 1) % poly.length];
    const b = poly[i];
    const da = dot(a);
    const db = dot(b);
    if (db >= 0) {
      if (da < 0) {
        const t = da / (da - db);
        output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]);
      }
      output.push(b);
    } else if (da >= 0) {
      const t = da / (da - db);
      output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]);
    }
  }
  return output;
}
