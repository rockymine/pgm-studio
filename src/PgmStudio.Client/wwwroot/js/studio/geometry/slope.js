/**
 * Surface-angle (slope) fitting for a sketch shape's per-vertex heights — the "tilt the top of the shape"
 * companion to plan rotation (which turns it in x/z). Given a few control vertices with chosen heights, fit
 * a single tilted plane and read every vertex's height off it, so the shape's top becomes a flat slope
 * instead of an undulating TIN. Pure, no DOM.
 *
 * Heights are block thicknesses (integers ≥ 1, the units `anchor_heights` stores): the fitted plane is
 * sampled at each vertex and rounded to the nearest block, which is what turns a continuous slope into the
 * neat straight steps of a staircase. Two control points define a **ramp** — the gradient runs along the
 * line between them, flat across it (contours perpendicular to that line). Three non-collinear points define
 * a **fully-aimed plane**; three collinear points (or two) fall back to the ramp.
 */

const MIN_HEIGHT = 1;

/** Height field h(x,z) from control samples `[{x,z,h}, …]` (2 or 3). Returns a `(x,z) => number` function,
 *  or null if fewer than 2 distinct control positions are given. */
export function surfacePlane(samples) {
  const pts = dedupePositions(samples);
  if (pts.length < 2) return null;
  if (pts.length >= 3) {
    const plane = fitPlane(pts[0], pts[1], pts[2]);
    if (plane) return (x, z) => plane.a * x + plane.b * z + plane.c;
  }
  // Two points, or a degenerate (collinear) triple → a ramp along the two farthest-apart samples.
  const [p, q] = farthestTwo(pts);
  return ramp(p, q);
}

/**
 * Every vertex's height from a plane fitted through the control `samples` (each `{x, z, h}`), rounded to a
 * block and clamped ≥ 1 — the array to store as `anchor_heights`. Returns null when the samples don't define
 * a plane (fewer than 2 distinct positions).
 */
export function surfaceHeights(vertices, samples) {
  const plane = surfacePlane(samples);
  if (!plane) return null;
  return vertices.map(([x, z]) => Math.max(MIN_HEIGHT, Math.round(plane(x, z))));
}

// ── internals ──────────────────────────────────────────────────────────────────

// Solve h = a·x + b·z + c through three points; null if they are collinear in x/z (no unique plane).
function fitPlane(p0, p1, p2) {
  // Cross product of the two edge vectors in (x, z, h); its (x,z) part over its h part gives the gradient.
  const e1 = [p1.x - p0.x, p1.z - p0.z, p1.h - p0.h];
  const e2 = [p2.x - p0.x, p2.z - p0.z, p2.h - p0.h];
  const nx = e1[1] * e2[2] - e1[2] * e2[1];
  const nz = e1[2] * e2[0] - e1[0] * e2[2];
  const nh = e1[0] * e2[1] - e1[1] * e2[0];   // == the 2-D cross of the footprint edges; 0 ⇒ collinear
  if (Math.abs(nh) < 1e-9) return null;
  // Plane: nx(x-x0) + nz(z-z0) + nh(h-h0) = 0  ⇒  h = a·x + b·z + c
  const a = -nx / nh, b = -nz / nh, c = p0.h - a * p0.x - b * p0.z;
  return { a, b, c };
}

// Ramp between two points: linear along p→q, flat across it (iso-height lines ⟂ to the p→q line).
function ramp(p, q) {
  const dx = q.x - p.x, dz = q.z - p.z;
  const len2 = dx * dx + dz * dz;
  if (len2 < 1e-9) return () => p.h;   // coincident (shouldn't reach here) — flat
  return (x, z) => {
    const t = ((x - p.x) * dx + (z - p.z) * dz) / len2;   // projection parameter along p→q
    return p.h + (q.h - p.h) * t;
  };
}

// Drop control samples that share a footprint position (a repeated x,z can't add plane information).
function dedupePositions(samples) {
  const out = [];
  for (const s of samples ?? [])
    if (!out.some(o => o.x === s.x && o.z === s.z)) out.push({ x: s.x, z: s.z, h: s.h });
  return out;
}

// The two samples farthest apart in the footprint — the ramp's endpoints (stable, spans the shape).
function farthestTwo(pts) {
  let a = 0, b = 1, best = -1;
  for (let i = 0; i < pts.length; i++)
    for (let j = i + 1; j < pts.length; j++) {
      const dx = pts[i].x - pts[j].x, dz = pts[i].z - pts[j].z;
      const d = dx * dx + dz * dz;
      if (d > best) { best = d; a = i; b = j; }
    }
  return [pts[a], pts[b]];
}
