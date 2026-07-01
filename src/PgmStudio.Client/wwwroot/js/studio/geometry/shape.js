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

/**
 * Promote a rectangle shape to an equivalent **polygon** — its 4 corners as open vertices
 * (clockwise from min,min), keeping id/operation/override **and the height fields**
 * (base_height/floor/anchor_heights). Lets the author then drag corners off-axis, insert midpoints,
 * or curve edges (none of which a rectangle can represent). Identity geometry, so the footprint and
 * column are unchanged; only the editing affordances open up.
 */
export function rectToPolygon(shape) {
  const { min_x, min_z, max_x, max_z } = shape;
  const poly = {
    id: shape.id, type: "polygon", operation: shape.operation, override: !!shape.override,
    vertices: [[min_x, min_z], [max_x, min_z], [max_x, max_z], [min_x, max_z]],
  };
  // Carry the column through — a promoted rectangle must keep its height, not reset to the default.
  if (shape.base_height    !== undefined) poly.base_height    = shape.base_height;
  if (shape.floor          !== undefined) poly.floor          = shape.floor;
  if (shape.anchor_heights !== undefined) poly.anchor_heights = shape.anchor_heights;
  return poly;
}

/** Translate an AABB `{min_x,min_z,max_x,max_z}` by (dx,dz), returning a new bounds (the editor's region
 *  model — the shared translate for body-drag/nudge, so no canvas keeps an inline copy). */
export function translateBounds(b, dx, dz) {
  return { min_x: b.min_x + dx, min_z: b.min_z + dz, max_x: b.max_x + dx, max_z: b.max_z + dz };
}

/** Translate a shape by (dx,dz), returning a new shape (pure — used for ghost preview + drop placement). */
export function translateShape(shape, dx, dz) {
  if (shape.type === "rectangle")
    return { ...shape, min_x: shape.min_x + dx, max_x: shape.max_x + dx, min_z: shape.min_z + dz, max_z: shape.max_z + dz };
  if (shape.type === "circle")
    return { ...shape, center_x: shape.center_x + dx, center_z: shape.center_z + dz };
  if (shape.vertices) {
    const moved = { ...shape, vertices: shape.vertices.map(([x, z]) => [x + dx, z + dz]) };
    // Bézier handles are absolute coords — move them with the vertices, else the curve distorts and the
    // handles stay behind (freshly built so translate stays pure — no mutating the source's controls).
    if (shape.controls) moved.controls = translateControls(shape.controls, dx, dz);
    return moved;
  }
  return { ...shape };
}

/** Translate a Bézier `controls` dict (`{ "i": { in?, out? } }`, absolute coords) by (dx,dz) — new object. */
function translateControls(controls, dx, dz) {
  const out = {};
  for (const [k, c] of Object.entries(controls)) {
    const nc = {};
    if (c.in)  nc.in  = [c.in[0]  + dx, c.in[1]  + dz];
    if (c.out) nc.out = [c.out[0] + dx, c.out[1] + dz];
    out[k] = nc;
  }
  return out;
}

/**
 * Rotate a shape by `angleRad` about `pivot` `[px,pz]`, **baking** the rotation into the geometry (there is
 * no stored angle — islands/mirror/rasterizer just see the moved coords). Positive angle turns clockwise on
 * the canvas (x right, z down). A **rectangle** can't hold a non-axis angle, so it's promoted via
 * `rectToPolygon` first (which carries its height fields); a **circle** keeps its radius and only its centre
 * orbits; **polygon/lasso** rotate their vertices + Bézier control handles. Pure — id / operation / override /
 * base_height / floor / anchor_heights ride through untouched (height is X/Z-invariant).
 */
export function rotateShape(shape, angleRad, pivot) {
  const [px, pz] = pivot;
  const cos = Math.cos(angleRad), sin = Math.sin(angleRad);
  const rot = (x, z) => [px + (x - px) * cos - (z - pz) * sin, pz + (x - px) * sin + (z - pz) * cos];
  if (shape.type === "circle") {
    const [cx, cz] = rot(shape.center_x, shape.center_z);
    return { ...shape, center_x: cx, center_z: cz };
  }
  let s = shape;
  if (s.type === "rectangle") s = rectToPolygon(s);   // one-way promote (an AABB can't hold a free angle)
  if (!s.vertices) return { ...s };
  const moved = { ...s, vertices: s.vertices.map(([x, z]) => rot(x, z)) };
  if (s.controls) moved.controls = rotateControls(s.controls, rot);
  return moved;
}

/** Rotate a Bézier `controls` dict's absolute in/out points with `rot` (a `(x,z) => [x,z]` transform). */
function rotateControls(controls, rot) {
  const out = {};
  for (const [k, c] of Object.entries(controls)) {
    const nc = {};
    if (c.in)  nc.in  = rot(c.in[0],  c.in[1]);
    if (c.out) nc.out = rot(c.out[0], c.out[1]);
    out[k] = nc;
  }
  return out;
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

/** Union AABB `{min_x,min_z,max_x,max_z}` of several shapes' bounds (the island bbox); null if none has
 *  bounds. Degenerate polygons/lassos (no vertices) are skipped. */
export function boundsOfShapes(shapes) {
  let b = null;
  for (const s of shapes) {
    const sb = toBounds(s);
    if (!sb) continue;
    b = b ? { min_x: Math.min(b.min_x, sb.min_x), min_z: Math.min(b.min_z, sb.min_z),
              max_x: Math.max(b.max_x, sb.max_x), max_z: Math.max(b.max_z, sb.max_z) }
          : { ...sb };
  }
  return b;
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
