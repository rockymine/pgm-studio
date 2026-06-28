/**
 * Read-only isometric preview of the sketch (S6/S5c). No WebGL/three.js — reuses the SVG stack and the
 * geometry the live preview already computes. Draws two kinds of "solid" back→front (painter's algorithm)
 * on a ground-plane reference, OPAQUE so nearer masses occlude farther ones:
 *   - prism:   { exterior, holes, top, floor, mirror } — a flat-topped island slab.
 *   - terrain: { vertices, heights, floor, mirror }    — a per-anchor shape: TIN-triangulated sloped top
 *                                                         + walls whose top edge follows the vertex heights.
 * Bright tops over darker two-tone walls read as lit-from-above.
 */

import { svgEl } from "./svg.js";
import { earClip } from "../geometry/triangulation.js";
import polygonClipping from "../vendor/polygon-clipping.js";

const COS30 = Math.cos(Math.PI / 6);
const SIN30 = Math.sin(Math.PI / 6);

const PAL = {
  island: { top: "#6d7ce8", topDark: "#4a57c4", wallR: "#5365cf", wallL: "#3f4ea8" },
  mirror: { top: "#abb2dd", topDark: "#838cc0", wallR: "#9099c9", wallL: "#737cae" },
  ground: { fill: "#ccd4e6", stroke: "#a9b2c8" },
};

const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
const lerp = (a, b, t) => { const A = hex(a), B = hex(b); return `rgb(${A.map((v, i) => Math.round(v + (B[i] - v) * t)).join(",")})`; };

// Where two prism footprints overlap, the rasterizer keeps only the taller surface. The painter's
// algorithm can't honour that with raw overlapping prisms — a single depth key picks the "front"
// shape, and a 180° mirror flips that choice, so the same overlap occludes oppositely on the two
// sides. Resolve it by clipping each prism's footprint by every taller shape, leaving the drawn
// footprints disjoint (taller shape intact; shorter shape carved). Then depth ordering is unambiguous
// and symmetric. Terrain (per-anchor) shapes are left whole (they can't be re-triangulated post-clip).
function resolveOverlaps(solids) {
  const closeRing = r => (r.length && (r[0][0] !== r[r.length - 1][0] || r[0][1] !== r[r.length - 1][1])) ? [...r, r[0]] : r;
  const openRing  = r => (r.length > 1 && r[0][0] === r[r.length - 1][0] && r[0][1] === r[r.length - 1][1]) ? r.slice(0, -1) : r;
  const topOf  = s => s.vertices ? Math.max(...s.heights) : s.top;
  const footOf = s => s.vertices || s.exterior;
  const out = [];
  for (const s of solids) {
    if (s.vertices) { out.push(s); continue; }                       // terrain: render whole
    const tallers = solids.filter(o => o !== s && topOf(o) > s.top).map(o => [closeRing(footOf(o))]);
    if (!tallers.length) { out.push(s); continue; }
    try {
      const diff = polygonClipping.difference([closeRing(s.exterior)], ...tallers);
      for (const poly of diff) out.push({ ...s, exterior: openRing(poly[0]), holes: poly.slice(1).map(openRing) });
    } catch { out.push(s); }                                          // degenerate clip → fall back to whole
  }
  return out;
}

export function renderIso(layer, solids, w, h, yawDeg, bbox) {
  while (layer.firstChild) layer.removeChild(layer.firstChild);
  if (!solids?.length) { if (bbox) drawGround(layer, bbox, w, h, yawDeg); return; }
  solids = resolveOverlaps(solids);
  if (!solids.length) { if (bbox) drawGround(layer, bbox, w, h, yawDeg); return; }

  const yaw = (yawDeg * Math.PI) / 180, cy = Math.cos(yaw), sy = Math.sin(yaw);
  const uv = (x, z, hh) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return [(rx - rz) * COS30, (rx + rz) * SIN30 - hh]; };
  const depth = (x, z) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return rx + rz; };

  const groundCorners = bbox
    ? [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.min_z], [bbox.max_x, bbox.max_z], [bbox.min_x, bbox.max_z]]
    : null;

  // Fit every corner (ground + each solid's footprint at floor and top) into the viewport.
  let umin = Infinity, umax = -Infinity, vmin = Infinity, vmax = -Infinity;
  const grow = (x, z, hh) => { const [u, v] = uv(x, z, hh); if (u < umin) umin = u; if (u > umax) umax = u; if (v < vmin) vmin = v; if (v > vmax) vmax = v; };
  for (const c of (groundCorners || [])) grow(c[0], c[1], 0);
  for (const s of solids) {
    if (s.vertices) s.vertices.forEach((p, i) => { grow(p[0], p[1], s.floor); grow(p[0], p[1], s.heights[i]); });
    else for (const ring of [s.exterior, ...(s.holes || [])]) for (const [x, z] of ring) { grow(x, z, s.floor); grow(x, z, s.top); }
  }
  const bw = (umax - umin) || 1, bh = (vmax - vmin) || 1;
  const scale = Math.min((w - 24) / bw, (h - 24) / bh);
  const ox = (w - bw * scale) / 2 - umin * scale, oy = (h - bh * scale) / 2 - vmin * scale;
  const S = (x, z, hh) => { const [u, v] = uv(x, z, hh); return [ox + u * scale, oy + v * scale]; };
  const pts = a => a.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(" ");
  const ringPath = (ring, hh) => "M " + ring.map(([x, z], i) => { const [sx, sz] = S(x, z, hh); return (i ? "L" : "") + `${sx.toFixed(1)} ${sz.toFixed(1)}`; }).join(" ") + " Z";

  // Ground reference plane.
  if (groundCorners) layer.appendChild(svgEl("polygon", {
    points: pts(groundCorners.map(c => S(c[0], c[1], 0))),
    fill: PAL.ground.fill, "fill-opacity": "0.45", stroke: PAL.ground.stroke, "stroke-width": "1", "stroke-dasharray": "4 3",
  }));

  // A single per-OBJECT painter's sort cannot resolve mutual occlusion between neighbouring solids,
  // and any single-scalar footprint key (max/mean corner depth) orders the two halves of a 180°
  // mirror inconsistently — the same cluster occludes differently on each side. Instead decompose
  // every solid into individual FACES (each wall quad + the flat/TIN top), give each a depth key =
  // mean screen-depth of its corners, and sort ALL faces together back→front. Because the rot_180
  // depth reflection maps a primary face's key d to its mirror's K−d, a centroid-depth sort commutes
  // with the mirror: the mirror set draws in exactly the reversed order, so the two halves are
  // provably consistent. Back walls are culled (they never show through an opaque front), which also
  // removes the seams a full-wall redraw left behind.
  const grad = [cy + sy, cy - sy];   // ∂depth/∂x, ∂depth/∂z — for back-wall culling
  const faces = [];
  for (const s of solids) collectFaces(s, faces, S, depth, pts, grad);
  faces.sort((a, b) => a.key - b.key);
  for (const f of faces) layer.appendChild(f.el());
}

// Mean screen-depth of a set of world (x,z) corners — the per-face painter's key.
const faceDepth = (depth, corners) => corners.reduce((s, [x, z]) => s + depth(x, z), 0) / corners.length;

// A vertical wall on edge a→b faces the viewer when stepping OUTWARD from the footprint RAISES the
// screen-depth (nearer = larger depth = drawn in front). Depth gradient along (dx,dz) is
// dx·(cos+sin)+dz·(cos−sin); outward ≈ edge-mid minus the footprint centroid. Winding-independent, so
// it works identically for primary and mirror.
function wallFrontFacing(a, b, cx, cz, gx, gz) {
  const mx = (a[0] + b[0]) / 2, mz = (a[1] + b[1]) / 2;
  return (mx - cx) * gx + (mz - cz) * gz > 0;
}

// Two-tone wall shading from a FIXED screen-space light (lit from the upper-right): a wall is the
// brighter wallR when its outward normal points screen-right. screen-u of a world direction (dx,dz) is
// proportional to dx·(cos−sin) − dz·(cos+sin) = dx·gz − dz·gx. Using the outward normal (not the ring
// winding) keeps the light consistent across the whole scene, so both mirror halves are lit identically
// for the single camera — no per-face flip.
function wallLitRight(a, b, cx, cz, gx, gz) {
  const mx = (a[0] + b[0]) / 2, mz = (a[1] + b[1]) / 2;
  return (mx - cx) * gz - (mz - cz) * gx >= 0;
}

// Emit a solid's faces into `faces` (each = { key, el() }). Shared by prism + terrain.
function collectFaces(s, faces, S, depth, pts, grad) {
  const pal = s.mirror ? PAL.mirror : PAL.island;
  if (s.vertices) collectTerrainFaces(s, faces, S, depth, pts, pal, grad);
  else            collectPrismFaces(s, faces, S, depth, pts, pal, grad);
}

function drawGround(layer, bbox, w, h, yawDeg) {
  const yaw = (yawDeg * Math.PI) / 180, cy = Math.cos(yaw), sy = Math.sin(yaw);
  const uv = (x, z) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return [(rx - rz) * COS30, (rx + rz) * SIN30]; };
  const cs = [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.min_z], [bbox.max_x, bbox.max_z], [bbox.min_x, bbox.max_z]].map(c => uv(c[0], c[1]));
  let umin = Math.min(...cs.map(c => c[0])), umax = Math.max(...cs.map(c => c[0])), vmin = Math.min(...cs.map(c => c[1])), vmax = Math.max(...cs.map(c => c[1]));
  const scale = Math.min((w - 24) / ((umax - umin) || 1), (h - 24) / ((vmax - vmin) || 1));
  const ox = (w - (umax - umin) * scale) / 2 - umin * scale, oy = (h - (vmax - vmin) * scale) / 2 - vmin * scale;
  layer.appendChild(svgEl("polygon", {
    points: cs.map(c => `${(ox + c[0] * scale).toFixed(1)},${(oy + c[1] * scale).toFixed(1)}`).join(" "),
    fill: PAL.ground.fill, "fill-opacity": "0.45", stroke: PAL.ground.stroke, "stroke-width": "1", "stroke-dasharray": "4 3",
  }));
}

// Footprint centroid of a closed/open ring (ignores a closing repeat).
function ringCentroid(ring) {
  const n = (ring.length > 1 && ring[0][0] === ring[ring.length - 1][0] && ring[0][1] === ring[ring.length - 1][1]) ? ring.length - 1 : ring.length;
  let sx = 0, sz = 0;
  for (let i = 0; i < n; i++) { sx += ring[i][0]; sz += ring[i][1]; }
  return [sx / n, sz / n];
}

function collectPrismFaces(isl, faces, S, depth, pts, pal, grad) {
  const ext = isl.exterior;
  const [gx, gz] = grad;
  const [cx, cz] = ringCentroid(ext);
  if (isl.top !== isl.floor) {
    const n = ext.length;
    for (let i = 0; i < n; i++) {
      const a = ext[i], b = ext[(i + 1) % n];
      if (a[0] === b[0] && a[1] === b[1]) continue;                 // skip the closing repeat
      if (!wallFrontFacing(a, b, cx, cz, gx, gz)) continue;         // cull back walls
      const quad = [S(a[0], a[1], isl.top), S(b[0], b[1], isl.top), S(b[0], b[1], isl.floor), S(a[0], a[1], isl.floor)];
      const fill = wallLitRight(a, b, cx, cz, gx, gz) ? pal.wallR : pal.wallL;
      faces.push({ key: faceDepth(depth, [a, b]), el: () => svgEl("polygon", { points: pts(quad), fill, stroke: pal.wallL, "stroke-width": "0.5" }) });
    }
  }
  // Flat top — one face, keyed at the footprint centroid depth.
  const ringPath = (ring) => "M " + ring.map(([x, z], i) => { const [sx, sz] = S(x, z, isl.top); return (i ? "L" : "") + `${sx.toFixed(1)} ${sz.toFixed(1)}`; }).join(" ") + " Z";
  let d = ringPath(ext);
  for (const hole of (isl.holes || [])) d += " " + ringPath(hole);
  faces.push({ key: depth(cx, cz), el: () => svgEl("path", { d, "fill-rule": "evenodd", fill: pal.top, stroke: pal.wallL, "stroke-width": "1.25" }) });
}

function collectTerrainFaces(s, faces, S, depth, pts, pal, grad) {
  const V = s.vertices, H = s.heights, n = V.length;
  const [gx, gz] = grad;
  const [cx, cz] = ringCentroid(V);
  // Sloped walls (top edge follows vertex heights) — one face each, back walls culled.
  for (let i = 0; i < n; i++) {
    const ai = i, bi = (i + 1) % n;
    const a = V[ai], b = V[bi];
    if (a[0] === b[0] && a[1] === b[1]) continue;
    if (!wallFrontFacing(a, b, cx, cz, gx, gz)) continue;
    const quad = [S(a[0], a[1], H[ai]), S(b[0], b[1], H[bi]), S(b[0], b[1], s.floor), S(a[0], a[1], s.floor)];
    const fill = wallLitRight(a, b, cx, cz, gx, gz) ? pal.wallR : pal.wallL;
    faces.push({ key: faceDepth(depth, [a, b]), el: () => svgEl("polygon", { points: pts(quad), fill, stroke: pal.wallL, "stroke-width": "0.5" }) });
  }
  // Triangulated top — each facet its own face (shaded by how flat it is, lit from above).
  for (const [a, b, c] of earClip(V)) {
    const e1 = [V[b][0] - V[a][0], V[b][1] - V[a][1], H[b] - H[a]];
    const e2 = [V[c][0] - V[a][0], V[c][1] - V[a][1], H[c] - H[a]];
    const nh = e1[0] * e2[1] - e1[1] * e2[0];                 // h-component of the face normal
    const nl = Math.hypot(e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], nh) || 1;
    const flat = Math.min(1, Math.abs(nh) / nl);              // 1 = level, →0 = steep
    const tri = [S(V[a][0], V[a][1], H[a]), S(V[b][0], V[b][1], H[b]), S(V[c][0], V[c][1], H[c])];
    const key = (depth(V[a][0], V[a][1]) + depth(V[b][0], V[b][1]) + depth(V[c][0], V[c][1])) / 3;
    const fill = lerp(pal.topDark, pal.top, flat);
    faces.push({ key, el: () => svgEl("polygon", { points: pts(tri), fill, stroke: "none" }) });
  }
}
