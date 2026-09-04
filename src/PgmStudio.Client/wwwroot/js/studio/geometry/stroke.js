/**
 * The band around a drawn line — the JS twin of `PgmStudio.Geom.Algorithms.StrokeOutline` (with the
 * `CatmullRom`, `Ribbon` and `PatternNoise` pieces it stands on). Pure math, NO DOM.
 *
 * The twin exists for the same reason the symmetry preview's does: the canvas rebuilds a stroke's outline on
 * every pointer move, and a round trip to the server per frame is not a preview. That makes this a **parity
 * surface** — the numbers below (`SMOOTH_SAMPLES`, the swing and taper constants, the hash) must equal the
 * C# ones or the band the author drags is not the band the map exports. The C# file is the canonical one;
 * change it first.
 *
 * The two-step is the substance: smooth the drawn points into a dense curve (centripetal Catmull-Rom, which
 * cannot cusp on a tight bend the way the uniform parameterisation does), then offset that curve to both
 * sides. Offsetting the drawn points directly would corner the band at every click, and a width that varies
 * needs somewhere dense enough to vary along.
 */

// Parity constants — must match C# Geom.Algorithms.StrokeOutline.
export const SMOOTH_SAMPLES = 8;    // curve samples per drawn segment
const ROUGH_PERIOD = 8;             // curve samples per wander of a rough edge
const ROUGH_SWING  = 0.45;          // how much of the width a rough edge may gain or lose
const ROUGH_SIDE   = 512;           // the noise row the right edge reads, so the two sides differ
const TAPER_ENDS   = 0.35;          // what is left of the width where a tapered stroke runs out

/**
 * The drawn points as the dense curve the band is centred on — what a preview strokes, too.
 *
 * A two-point line is densified rather than passed through: it has no curve to sample, but a width that
 * varies along the stroke needs somewhere to vary, and left at two points a taper reads only its two ends
 * and comes out uniformly thin.
 */
export function strokeCenterline(vertices) {
  if (!vertices || vertices.length < 2) return [];
  if (vertices.length === 2) return subdivide(vertices[0], vertices[1]);
  return catmullRom(vertices, SMOOTH_SAMPLES);
}

// The same point count a spline of one segment gives, so a straight stroke and a curved one vary at the same
// rate along their length.
function subdivide(from, to) {
  const points = [];
  for (let i = 0; i <= SMOOTH_SAMPLES; i++) {
    const t = i / SMOOTH_SAMPLES;
    points.push([from[0] + (to[0] - from[0]) * t, from[1] + (to[1] - from[1]) * t]);
  }
  return points;
}

/**
 * The **closed** outline of the band `radius` blocks to each side of a stroke's centreline (closed to the
 * `toRing` convention — last point repeats the first). `[]` for a route of fewer than two points.
 *
 * The outline is what a canvas strokes to show where a route runs; what the export actually paves is
 * `Geom.StrokeFill`, and the two deliberately differ. An outline cannot draw a gap, so a stepping-stone stroke
 * shows here as the corridor its stones fall along — which is the right thing to see while placing it.
 */
export function strokeRing(prop) {
  const centerline = strokeCenterline(prop.points);
  const radius = prop.radius ?? 2;
  if (centerline.length < 2 || radius <= 0) return [];
  // Only the two styles that vary a width change the outline; the rest gate cells inside it.
  const edge = prop.style === "tapered" || prop.style === "rough" ? prop.style : "solid";
  const seed = prop.seed ?? 0;

  const ring = edge === "solid"
    ? ribbonUniform(centerline, radius * 2)
    : ribbonVaried(centerline,
        widths(centerline.length, radius, edge, seed, 0),
        widths(centerline.length, radius, edge, seed, ROUGH_SIDE));
  if (ring.length < 3) return [];
  ring.push(ring[0]);
  return ring;
}

// The half-width at each point of the dense centerline. A taper reads how far along the stroke a point is;
// a rough edge reads a noise field, sampled a long way apart for the two sides so they wander independently
// and the band does not merely breathe in and out.
function widths(count, radius, edge, seed, row) {
  const out = new Array(count);
  for (let i = 0; i < count; i++) {
    const along = count > 1 ? i / (count - 1) : 0;
    let scale = 1;
    if (edge === "tapered")    scale = TAPER_ENDS + (1 - TAPER_ENDS) * Math.sin(Math.PI * along);
    else if (edge === "rough") scale = 1 - ROUGH_SWING + 2 * ROUGH_SWING * valueNoise(i, row, seed, ROUGH_PERIOD);
    out[i] = radius * scale;
  }
  return out;
}

// ── Catmull-Rom (twin of Geom.Algorithms.CatmullRom) ─────────────────────────────────────────────────
function catmullRom(points, samplesPerEdge) {
  if (points.length < 3) return points.map(([x, z]) => [x, z]);
  const p = [points[0], ...points, points[points.length - 1]];
  const out = [];
  for (let i = 1; i < p.length - 2; i++) {
    const p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
    const t0 = 0, t1 = t0 + knot(p0, p1), t2 = t1 + knot(p1, p2), t3 = t2 + knot(p2, p3);
    for (let s = 0; s < samplesPerEdge; s++) {
      const t  = t1 + (t2 - t1) * s / samplesPerEdge;
      const a1 = mix(p0, p1, (t1 - t) / (t1 - t0), (t - t0) / (t1 - t0));
      const a2 = mix(p1, p2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1));
      const a3 = mix(p2, p3, (t3 - t) / (t3 - t2), (t - t2) / (t3 - t2));
      const b1 = mix(a1, a2, (t2 - t) / (t2 - t0), (t - t0) / (t2 - t0));
      const b2 = mix(a2, a3, (t3 - t) / (t3 - t1), (t - t1) / (t3 - t1));
      out.push(mix(b1, b2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1)));
    }
  }
  out.push([points[points.length - 1][0], points[points.length - 1][1]]);
  return out;
}

// centripetal knot delta = |b−a|^½ (floored so coincident control points don't divide by zero)
function knot(a, b) {
  const d = Math.sqrt(Math.hypot(b[0] - a[0], b[1] - a[1]));
  return d < 1e-6 ? 1e-6 : d;
}
function mix(a, b, wa, wb) { return [a[0] * wa + b[0] * wb, a[1] * wa + b[1] * wb]; }

// ── Ribbon (twin of Geom.Algorithms.Ribbon) ──────────────────────────────────────────────────────────
function ribbonUniform(centerline, width) {
  const half = width / 2;
  return ribbon(centerline, centerline.map(() => half), centerline.map(() => half));
}
function ribbonVaried(centerline, leftOffset, rightOffset) {
  if (leftOffset.length !== centerline.length || rightOffset.length !== centerline.length) return [];
  return ribbon(centerline, leftOffset, rightOffset);
}
function ribbon(centerline, leftOffset, rightOffset) {
  const n = centerline.length;
  if (n < 2) return [];
  const left = [], right = [];
  for (let i = 0; i < n; i++) {
    const [nx, nz] = vertexNormal(centerline, i);
    const [px, pz] = centerline[i];
    left.push([px + nx * leftOffset[i], pz + nz * leftOffset[i]]);
    right.push([px - nx * rightOffset[i], pz - nz * rightOffset[i]]);
  }
  right.reverse();
  return left.concat(right);
}
// Averaged left-hand unit normal at centerline vertex i.
function vertexNormal(c, i) {
  const n = c.length;
  if (i === 0)     return normal(c[0], c[1]);
  if (i === n - 1) return normal(c[i - 1], c[i]);
  const [ax, az] = normal(c[i - 1], c[i]);
  const [bx, bz] = normal(c[i], c[i + 1]);
  const mx = ax + bx, mz = az + bz;
  const len = Math.hypot(mx, mz);
  return len < 1e-9 ? [ax, az] : [mx / len, mz / len];
}
function normal(a, b) {
  const tx = b[0] - a[0], tz = b[1] - a[1];
  const len = Math.hypot(tx, tz);
  return len < 1e-9 ? [0, 0] : [-tz / len, tx / len];
}

// ── Value noise (twin of Geom.Algorithms.PatternNoise) ───────────────────────────────────────────────
// Unsigned 32-bit throughout: `Math.imul` for the multiplies (JS numbers lose the low bits past 2^53) and
// `>>> 0` after every step, so the lattice hash is bit-identical to the C# one.
function hash(x, z, seed) {
  let h = (seed + 0x9E3779B9) >>> 0;
  h = (h ^ Math.imul(x, 0x85EBCA77)) >>> 0; h = ((h << 13) | (h >>> 19)) >>> 0; h = Math.imul(h, 0xC2B2AE3D) >>> 0;
  h = (h ^ Math.imul(z, 0x27D4EB2F)) >>> 0; h = ((h << 15) | (h >>> 17)) >>> 0; h = Math.imul(h, 0x165667B1) >>> 0;
  return (h ^ (h >>> 16)) >>> 0;
}
function unit(x, z, seed) { return (hash(x, z, seed) & 0xFFFFFF) / 0x1000000; }

/** Smooth (smoothstep-interpolated) value noise at a scale, in [0,1). */
export function valueNoise(x, z, seed, scale) {
  const fx = x / scale, fz = z / scale;
  const x0 = Math.floor(fx), z0 = Math.floor(fz);
  const tx = fx - x0, tz = fz - z0;
  const v00 = unit(x0, z0, seed), v10 = unit(x0 + 1, z0, seed);
  const v01 = unit(x0, z0 + 1, seed), v11 = unit(x0 + 1, z0 + 1, seed);
  const sx = tx * tx * (3 - 2 * tx), sz = tz * tz * (3 - 2 * tz);
  const a = v00 + (v10 - v00) * sx, b = v01 + (v11 - v01) * sx;
  return a + (b - a) * sz;
}
