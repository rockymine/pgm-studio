/**
 * Douglas–Peucker polygon simplification — collapse a dense ring (a freehand lasso trace, a block-step
 * staircase) into a readable handful of vertices, keeping only the points at real bends. Pure, no DOM.
 *
 * The client twin of C# `PgmStudio.Geom.PolygonSimplify` / `DouglasPeucker` (same algorithm, so a lasso
 * reads the same wherever it is simplified): for a closed ring, split at the two farthest-apart vertices
 * into two open chains, run Douglas–Peucker on each, stitch, then drop leftover collinear points. Raising
 * `tolerance` trades fidelity for fewer vertices — a unit staircase straightens once tolerance ≥ ~1.
 */

/** Perpendicular distance from `p` to the segment a–b (point-to-point when a == b). */
function perpDistance(p, a, b) {
  const dx = b[0] - a[0], dz = b[1] - a[1];
  const len2 = dx * dx + dz * dz;
  if (len2 < 1e-12) return Math.hypot(p[0] - a[0], p[1] - a[1]);
  const cross = Math.abs(dx * (a[1] - p[1]) - (a[0] - p[0]) * dz);
  return cross / Math.sqrt(len2);
}

/** Simplify an OPEN polyline: keep the endpoints and every vertex whose perpendicular deviation from the
 *  running chord exceeds `tolerance`. Fewer than 3 points pass through unchanged. */
export function douglasPeucker(points, tolerance) {
  const n = points.length;
  if (n < 3) return points.map(p => [p[0], p[1]]);
  const keep = new Array(n).fill(false);
  keep[0] = keep[n - 1] = true;
  const stack = [[0, n - 1]];
  while (stack.length) {
    const [lo, hi] = stack.pop();
    if (hi <= lo + 1) continue;
    let maxD = -1, idx = -1;
    for (let i = lo + 1; i < hi; i++) {
      const d = perpDistance(points[i], points[lo], points[hi]);
      if (d > maxD) { maxD = d; idx = i; }
    }
    if (maxD <= tolerance || idx < 0) continue;
    keep[idx] = true;
    stack.push([lo, idx], [idx, hi]);
  }
  const out = [];
  for (let i = 0; i < n; i++) if (keep[i]) out.push([points[i][0], points[i][1]]);
  return out;
}

/** Drop consecutive duplicates + a duplicated closing point, returning an open ring. */
function dedup(ring) {
  const pts = [];
  for (const p of ring)
    if (!pts.length || pts[pts.length - 1][0] !== p[0] || pts[pts.length - 1][1] !== p[1]) pts.push([p[0], p[1]]);
  if (pts.length > 1 && pts[0][0] === pts[pts.length - 1][0] && pts[0][1] === pts[pts.length - 1][1]) pts.pop();
  return pts;
}

/** The lowest-leftmost vertex and the vertex farthest from it — a stable pair to split a closed ring on. */
function farthestPair(pts) {
  let a = 0;
  for (let i = 1; i < pts.length; i++)
    if (pts[i][0] < pts[a][0] || (pts[i][0] === pts[a][0] && pts[i][1] < pts[a][1])) a = i;
  let b = a, best = -1;
  for (let i = 0; i < pts.length; i++) {
    const dx = pts[i][0] - pts[a][0], dz = pts[i][1] - pts[a][1];
    const d = dx * dx + dz * dz;
    if (d > best) { best = d; b = i; }
  }
  return [a, b];
}

/** Points from i to j inclusive, walking forward modulo n. */
function slice(pts, i, j) {
  const n = pts.length, out = [pts[i]];
  let k = i;
  while (k !== j) { k = (k + 1) % n; out.push(pts[k]); }
  return out;
}

/** Drop vertices whose removal moves the outline by no more than sqrt(areaEps) (collinear cleanup). */
function removeCollinear(pts, areaEps) {
  const n = pts.length;
  if (n <= 3) return pts;
  const keep = new Array(n).fill(true);
  for (let i = 0; i < n; i++) {
    const prev = pts[(i - 1 + n) % n], cur = pts[i], next = pts[(i + 1) % n];
    const area2 = Math.abs((cur[0] - prev[0]) * (next[1] - prev[1]) - (next[0] - prev[0]) * (cur[1] - prev[1]));
    if (area2 <= areaEps) keep[i] = false;
  }
  const out = [];
  for (let i = 0; i < n; i++) if (keep[i]) out.push(pts[i]);
  return out.length >= 3 ? out : pts;
}

/**
 * Simplify a closed ring (input may be open or closed) to the vertices that keep every dropped point within
 * `tolerance` of the kept outline. Returns an OPEN ring (no duplicated closing point). ≤3 distinct points
 * pass through unchanged.
 */
export function simplifyRing(ring, tolerance) {
  const pts = dedup(ring);
  if (pts.length <= 3) return pts;

  const [a, b] = farthestPair(pts);
  const s1 = douglasPeucker(slice(pts, a, b), tolerance);   // a → b (forward)
  const s2 = douglasPeucker(slice(pts, b, a), tolerance);   // b → a (wrap)

  // Each chain shares its endpoints with the other; stitch without duplicating them.
  const out = [...s1];
  for (let i = 1; i < s2.length - 1; i++) out.push(s2[i]);
  return removeCollinear(out, tolerance * tolerance + 1e-9);
}
