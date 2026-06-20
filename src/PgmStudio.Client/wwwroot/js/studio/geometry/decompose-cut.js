/**
 * Pure geometry for lane decomposition: lasso enclosure, lasso∩edge markers, and splitting a piece
 * {exterior, holes} into [lane, remainder] at two seam points. Rings are [[x,z],...] (open, no closing dup).
 * A seam point is either {kind:"vertex", index} (an existing ring vertex) or {kind:"marker", edge, t, point,
 * key} (a new point on edge `edge` at parameter `t`). No DOM — testable in isolation.
 */

export function pointInRing(x, z, ring) {
  let inside = false;
  const n = ring.length;
  for (let i = 0, j = n - 1; i < n; j = i++) {
    const xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
    if (((zi > z) !== (zj > z)) && (x < (xj - xi) * (z - zi) / ((zj - zi) || 1e-12) + xi)) inside = !inside;
  }
  return inside;
}

export function centroid(ring) {
  let x = 0, z = 0;
  for (const p of ring) { x += p[0]; z += p[1]; }
  return [x / ring.length, z / ring.length];
}

// Proper intersection point of segments a-b and c-d (null if they don't cross within both).
function segInt(a, b, c, d) {
  const rx = b[0] - a[0], rz = b[1] - a[1], sx = d[0] - c[0], sz = d[1] - c[1];
  const den = rx * sz - rz * sx;
  if (Math.abs(den) < 1e-12) return null;
  const t = ((c[0] - a[0]) * sz - (c[1] - a[1]) * sx) / den;
  const u = ((c[0] - a[0]) * rz - (c[1] - a[1]) * rx) / den;
  if (t < 0 || t > 1 || u < 0 || u > 1) return null;
  return { point: [a[0] + t * rx, a[1] + t * rz], t };
}

/** Indices of `ring` vertices inside the closed lasso loop. */
export function enclosedVertices(ring, lasso) {
  const out = [];
  for (let i = 0; i < ring.length; i++)
    if (pointInRing(ring[i][0], ring[i][1], lasso)) out.push(i);
  return out;
}

/** Points where the lasso path crosses ring edges → draggable seam candidates (each with a stable key). */
export function edgeMarkers(ring, lasso, keyPrefix = "m") {
  const out = [];
  const n = ring.length, m = lasso.length;
  let id = 0;
  for (let i = 0; i < n; i++) {
    const a = ring[i], b = ring[(i + 1) % n];
    for (let j = 0; j < m; j++) {
      const hit = segInt(a, b, lasso[j], lasso[(j + 1) % m]);
      if (hit) out.push({ kind: "marker", edge: i, t: hit.t, point: hit.point, key: `${keyPrefix}${id++}` });
    }
  }
  return out;
}

/**
 * Split piece.exterior at two seam points into [lane, remainder]. The lane is the side containing `laneRep`
 * (the lasso centroid). Holes go to whichever piece contains their centroid. The two seam points end up in
 * BOTH rings (the coincident "cut" nodes). Returns null for a degenerate cut.
 */
export function splitPiece(piece, seamA, seamB, laneRep) {
  const ring = piece.exterior;
  // augment the ring with any marker seams, inserted on their edge in t-order
  const markers = [seamA, seamB].filter(s => s.kind === "marker");
  const byEdge = {};
  for (const s of markers) (byEdge[s.edge] ??= []).push(s);
  for (const e in byEdge) byEdge[e].sort((p, q) => p.t - q.t);
  const aug = [];
  for (let i = 0; i < ring.length; i++) {
    aug.push({ pt: ring[i], key: "V" + i });
    for (const s of (byEdge[i] || [])) aug.push({ pt: s.point, key: s.key });
  }
  const keyOf = (s) => s.kind === "vertex" ? "V" + s.index : s.key;
  const idxA = aug.findIndex(o => o.key === keyOf(seamA));
  const idxB = aug.findIndex(o => o.key === keyOf(seamB));
  if (idxA < 0 || idxB < 0 || idxA === idxB) return null;

  const arc = (s, e) => { const r = []; for (let i = s; ; i = (i + 1) % aug.length) { r.push(aug[i].pt); if (i === e) break; } return r; };
  const ring1 = arc(idxA, idxB), ring2 = arc(idxB, idxA);
  if (ring1.length < 3 || ring2.length < 3) return null;
  const laneIs1 = pointInRing(laneRep[0], laneRep[1], ring1);
  const lane = laneIs1 ? ring1 : ring2, rem = laneIs1 ? ring2 : ring1;

  const laneHoles = [], remHoles = [];
  for (const h of (piece.holes || [])) { const c = centroid(h); (pointInRing(c[0], c[1], lane) ? laneHoles : remHoles).push(h); }
  return [
    { exterior: lane, holes: laneHoles, role: "other" },
    { exterior: rem, holes: remHoles, role: piece.role || "other" },
  ];
}
