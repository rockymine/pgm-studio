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
