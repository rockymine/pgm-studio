/**
 * Unified primitive-shape model — the one vocabulary for rectangle / circle / polygon / lasso,
 * used by the sketch tool. (The editor canvas hit-tests and renders regions via AABB + `polygon_2d`,
 * not this shape model.) Pure math, NO DOM.
 *
 * A *shape* is `{ type, …params }`: rectangle `{min_x,min_z,max_x,max_z}`, circle
 * `{center_x,center_z,radius}`, polygon|lasso `{vertices:[[x,z],…], controls?}`. Region/sketch
 * metadata (category/color, operation/override) lives on top — not in the geometry.
 *
 * Bézier model (lock-step with render/svg.js `ringToPath` and the C# rasterizer): `controls` is a
 * dict keyed by the *stringified vertex index* (`"0"`,`"1"`,…), each `{ in?:[x,z], out?:[x,z] }`;
 * for edge i→j the cubic is `(p_i, controls[i].out, controls[j].in, p_j)`, a missing handle falling
 * back to its endpoint.
 */

import { pointInRing } from "./polygon.js";

// Parity constants — must match the C# rasterizer / export (docs/contracts/sketch-authoring.md §6).
export const CIRCLE_POINTS  = 64;   // vertices approximating a circle
export const BEZIER_SAMPLES = 16;   // points sampled per curved edge (endpoint excluded)

/**
 * Convert a shape to a **closed** coordinate ring `[[x,z],…]` (last point equals the first — the
 * polygon-clipping convention). Returns `[]` for a degenerate polygon/lasso (< 3 vertices).
 */
export function toRing(shape) {
  switch (shape.type) {
    case "rectangle": {
      const { min_x, max_x, min_z, max_z } = shape;
      return [
        [min_x, min_z], [max_x, min_z],
        [max_x, max_z], [min_x, max_z],
        [min_x, min_z],
      ];
    }
    case "circle":
      return circleToRing(shape.center_x, shape.center_z, shape.radius);
    case "polygon":
    case "lasso": {
      const verts = shape.vertices;
      if (!verts || verts.length < 3) return [];
      const controls = shape.controls || {};
      if (!Object.keys(controls).length) {
        const ring = verts.map(([x, z]) => [x, z]);
        ring.push(ring[0]);
        return ring;
      }
      // Discretize Bézier edges into a dense straight-segment ring.
      const n = verts.length;
      const ring = [];
      for (let i = 0; i < n; i++) {
        const j     = (i + 1) % n;
        const p0    = verts[i], p3 = verts[j];
        const cpOut = controls[String(i)]?.out;
        const cpIn  = controls[String(j)]?.in;
        if (cpOut || cpIn) {
          ring.push(...sampleBezierEdge(p0, cpOut ?? p0, cpIn ?? p3, p3));
        } else {
          ring.push([p0[0], p0[1]]);
        }
      }
      ring.push(ring[0]);
      return ring;
    }
    default:
      throw new Error(`Unknown shape type: ${shape.type}`);
  }
}

/** Approximate a circle as a closed polygon ring, vertices rounded to the nearest block. */
export function circleToRing(cx, cz, radius, nPoints = CIRCLE_POINTS) {
  const pts = [];
  for (let i = 0; i < nPoints; i++) {
    const angle = (2 * Math.PI * i) / nPoints;
    pts.push([
      Math.round(cx + radius * Math.cos(angle)),
      Math.round(cz + radius * Math.sin(angle)),
    ]);
  }
  pts.push(pts[0]);
  return pts;
}

/**
 * Sample BEZIER_SAMPLES points along a cubic Bézier edge p0→p3 (endpoint excluded).
 * c1 = out-control of p0, c2 = in-control of p3.
 */
export function sampleBezierEdge(p0, c1, c2, p3) {
  const pts = [];
  for (let k = 0; k < BEZIER_SAMPLES; k++) {
    const t = k / BEZIER_SAMPLES, u = 1 - t;
    pts.push([
      u*u*u*p0[0] + 3*u*u*t*c1[0] + 3*u*t*t*c2[0] + t*t*t*p3[0],
      u*u*u*p0[1] + 3*u*u*t*c1[1] + 3*u*t*t*c2[1] + t*t*t*p3[1],
    ]);
  }
  return pts;
}

/** Centroid `[x,z]` of a closed ring (last point == first). */
export function ringCentroid(ring) {
  const n = ring.length - 1; // exclude the closing repeat
  let sumX = 0, sumZ = 0;
  for (let i = 0; i < n; i++) { sumX += ring[i][0]; sumZ += ring[i][1]; }
  return [sumX / n, sumZ / n];
}

/** Axis-aligned bounds `{min_x,min_z,max_x,max_z}` of a shape (null for a degenerate polygon/lasso). */
export function toBounds(shape) {
  switch (shape.type) {
    case "rectangle": {
      const { min_x, min_z, max_x, max_z } = shape;
      return { min_x, min_z, max_x, max_z };
    }
    case "circle": {
      const { center_x, center_z, radius } = shape;
      return { min_x: center_x - radius, min_z: center_z - radius,
               max_x: center_x + radius, max_z: center_z + radius };
    }
    case "polygon":
    case "lasso": {
      const verts = shape.vertices;
      if (!verts || !verts.length) return null;
      const xs = verts.map(([x]) => x), zs = verts.map(([, z]) => z);
      return { min_x: Math.min(...xs), min_z: Math.min(...zs),
               max_x: Math.max(...xs), max_z: Math.max(...zs) };
    }
    default:
      return null;
  }
}

/**
 * True if `(x,z)` is inside the shape. Per-type: rectangle = bounds, circle = radius, polygon/lasso =
 * ray-cast over the **same closed ring that is rendered** (`toRing`, so Bézier curve bulge is included —
 * the hit shape matches the drawn outline). A degenerate polygon/lasso (< 3 vertices) contains nothing.
 */
export function containsPoint(shape, x, z) {
  switch (shape.type) {
    case "rectangle":
      return x >= shape.min_x && x <= shape.max_x && z >= shape.min_z && z <= shape.max_z;
    case "circle":
      return Math.hypot(x - shape.center_x, z - shape.center_z) <= shape.radius;
    case "polygon":
    case "lasso": {
      const ring = toRing(shape);
      return ring.length >= 4 && pointInRing(x, z, ring);
    }
    default:
      return false;
  }
}
