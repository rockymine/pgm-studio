/**
 * Ear-clipping triangulation of a simple polygon ring — the JS twin of C# Geom.Triangulation.EarClip
 * (deterministic: CCW normalisation + first-ear-each-pass). Used by the iso preview to drape a per-anchor
 * shape's TIN top (S5c). Pure: no DOM. Returns triangles as index triples into `poly`.
 */

const EPS = 1e-9;

function signedArea(poly) {
  let a = 0;
  for (let i = 0, n = poly.length; i < n; i++) { const p = poly[i], q = poly[(i + 1) % n]; a += p[0] * q[1] - q[0] * p[1]; }
  return a / 2;
}
function cross(a, b, c) { return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]); }
function bary(px, pz, a, b, c) {
  const v0x = b[0] - a[0], v0z = b[1] - a[1], v1x = c[0] - a[0], v1z = c[1] - a[1];
  const den = v0x * v1z - v1x * v0z;
  if (Math.abs(den) < 1e-12) return null;
  const v2x = px - a[0], v2z = pz - a[1];
  const v = (v2x * v1z - v1x * v2z) / den, w = (v0x * v2z - v2x * v0z) / den;
  return [1 - v - w, v, w];
}
function inTri(p, a, b, c) { const w = bary(p[0], p[1], a, b, c); return w && w[0] >= -EPS && w[1] >= -EPS && w[2] >= -EPS; }

export function earClip(poly) {
  const tris = [];
  const n = poly.length;
  if (n < 3) return tris;
  const idx = [];
  if (signedArea(poly) < 0) for (let i = n - 1; i >= 0; i--) idx.push(i);
  else                      for (let i = 0; i < n; i++)     idx.push(i);

  let guard = n * n;
  while (idx.length > 3 && guard-- > 0) {
    let clipped = false;
    const m = idx.length;
    for (let i = 0; i < m; i++) {
      const i0 = idx[(i + m - 1) % m], i1 = idx[i], i2 = idx[(i + 1) % m];
      const a = poly[i0], b = poly[i1], c = poly[i2];
      if (cross(a, b, c) <= EPS) continue;
      let empty = true;
      for (let j = 0; j < m; j++) { const vj = idx[j]; if (vj === i0 || vj === i1 || vj === i2) continue; if (inTri(poly[vj], a, b, c)) { empty = false; break; } }
      if (!empty) continue;
      tris.push([i0, i1, i2]); idx.splice(i, 1); clipped = true; break;
    }
    if (!clipped) break;
  }
  if (idx.length === 3) tris.push([idx[0], idx[1], idx[2]]);
  else if (idx.length > 3) for (let i = 1; i + 1 < idx.length; i++) tris.push([idx[0], idx[i], idx[i + 1]]);
  return tris;
}

// ── polygon-with-holes triangulation (renderer-only) ────────────────────────────────────────────
// No C# twin: the rasterizer carves holes via point-in-ring set algebra, so only the 3-D preview needs
// to triangulate a holed footprint. Each hole is spliced into the outer ring by a zero-width bridge
// (Eberly's visible-vertex method), then the single merged ring is ear-clipped. Rings are OPEN arrays
// of [x,z] (no closing duplicate). Returns triangles as point triples [[x,z],[x,z],[x,z]].
export function earClipWithHoles(outer, holes) {
  // Outer CCW, holes CW (opposite winding) so each bridge yields a simple polygon.
  let ring = signedArea(outer) < 0 ? outer.slice().reverse() : outer.slice();
  const ordered = (holes ?? [])
    .filter(h => h && h.length >= 3)
    .map(h => (signedArea(h) > 0 ? h.slice().reverse() : h.slice()))
    .map(h => ({ h, mi: maxXIndex(h) }))
    .sort((a, b) => b.h[b.mi][0] - a.h[a.mi][0]);   // rightmost hole first (Eberly)
  for (const { h, mi } of ordered) ring = bridgeHole(ring, h, mi);
  return earClipPoints(ring);
}

const maxXIndex = (h) => { let mi = 0; for (let i = 1; i < h.length; i++) if (h[i][0] > h[mi][0]) mi = i; return mi; };
const coincident = (p, q) => Math.abs(p[0] - q[0]) < 1e-6 && Math.abs(p[1] - q[1]) < 1e-6;

// Splice `hole` into `outer` at its rightmost vertex M: cast a ray from M toward +x to the nearest outer
// edge, pick the visible outer vertex P, and stitch outer→M→(around the hole)→outer via a zero-width bridge.
function bridgeHole(outer, hole, mIdx) {
  const M = hole[mIdx];
  let bestX = Infinity, edgeP = -1;
  for (let i = 0; i < outer.length; i++) {
    const a = outer[i], b = outer[(i + 1) % outer.length];
    if ((a[1] > M[1]) === (b[1] > M[1])) continue;                 // edge doesn't straddle M.z
    const x = a[0] + ((M[1] - a[1]) / (b[1] - a[1])) * (b[0] - a[0]);
    if (x >= M[0] - EPS && x < bestX) { bestX = x; edgeP = (a[0] >= b[0]) ? i : (i + 1) % outer.length; }
  }
  if (edgeP < 0) return outer;                                     // no edge to the right — drop the hole
  const I = [bestX, M[1]], Pp = outer[edgeP];
  let P = edgeP, bestAng = Infinity;
  for (let i = 0; i < outer.length; i++) {                         // reflex vertex inside (M,I,P) → use it instead
    if (i === edgeP || !inTri(outer[i], M, I, Pp)) continue;
    const ang = Math.atan2(Math.abs(outer[i][1] - M[1]), outer[i][0] - M[0]);
    if (ang < bestAng) { bestAng = ang; P = i; }
  }
  const merged = [];
  for (let i = 0; i <= P; i++) merged.push(outer[i]);
  for (let k = 0; k <= hole.length; k++) merged.push(hole[(mIdx + k) % hole.length]);
  for (let i = P; i < outer.length; i++) merged.push(outer[i]);
  return merged;
}

// Ear-clip a single ring that may carry zero-width bridges; the empty-ear test ignores candidates
// coincident with an ear corner so the duplicated bridge vertices can't block valid ears.
function earClipPoints(poly) {
  const tris = [], n = poly.length;
  if (n < 3) return tris;
  const idx = [];
  if (signedArea(poly) < 0) for (let i = n - 1; i >= 0; i--) idx.push(i);
  else                      for (let i = 0; i < n; i++)     idx.push(i);
  let guard = n * n;
  while (idx.length > 3 && guard-- > 0) {
    let clipped = false;
    const m = idx.length;
    for (let i = 0; i < m; i++) {
      const i0 = idx[(i + m - 1) % m], i1 = idx[i], i2 = idx[(i + 1) % m];
      const a = poly[i0], b = poly[i1], c = poly[i2];
      if (cross(a, b, c) <= EPS) continue;
      let empty = true;
      for (let j = 0; j < m; j++) {
        const vj = idx[j];
        if (vj === i0 || vj === i1 || vj === i2) continue;
        const p = poly[vj];
        if (coincident(p, a) || coincident(p, b) || coincident(p, c)) continue;
        if (inTri(p, a, b, c)) { empty = false; break; }
      }
      if (!empty) continue;
      tris.push([a, b, c]); idx.splice(i, 1); clipped = true; break;
    }
    if (!clipped) break;
  }
  if (idx.length === 3) tris.push([poly[idx[0]], poly[idx[1]], poly[idx[2]]]);
  else if (idx.length > 3) for (let i = 1; i + 1 < idx.length; i++) tris.push([poly[idx[0]], poly[idx[i]], poly[idx[i + 1]]]);
  return tris;
}
