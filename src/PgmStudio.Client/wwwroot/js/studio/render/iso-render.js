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

  const footprint = s => s.vertices || s.exterior;
  const ordered = solids
    .map(s => ({ s, d: Math.max(...footprint(s).map(([x, z]) => depth(x, z))) }))
    .sort((a, b) => a.d - b.d);

  for (const { s } of ordered) {
    const pal = s.mirror ? PAL.mirror : PAL.island;
    if (s.vertices) drawTerrain(layer, s, S, depth, pts, pal);
    else            drawPrism(layer, s, S, depth, pts, ringPath, pal);
  }
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

function drawPrism(layer, isl, S, depth, pts, ringPath, pal) {
  const ext = isl.exterior;
  if (isl.top !== isl.floor) {
    const topPts = ext.map(([x, z]) => S(x, z, isl.top));
    const cu = topPts.reduce((s, p) => s + p[0], 0) / topPts.length;
    const edges = ext.map((a, i) => ({ a, b: ext[(i + 1) % ext.length] }))
      .map(e => ({ ...e, md: (depth(e.a[0], e.a[1]) + depth(e.b[0], e.b[1])) / 2 }))
      .sort((e1, e2) => e1.md - e2.md);
    for (const { a, b } of edges) {
      const quad = [S(a[0], a[1], isl.top), S(b[0], b[1], isl.top), S(b[0], b[1], isl.floor), S(a[0], a[1], isl.floor)];
      const midU = (quad[0][0] + quad[1][0]) / 2;
      layer.appendChild(svgEl("polygon", { points: pts(quad), fill: midU >= cu ? pal.wallR : pal.wallL, stroke: pal.wallL, "stroke-width": "0.5" }));
    }
  }
  let d = ringPath(ext, isl.top);
  for (const hole of (isl.holes || [])) d += " " + ringPath(hole, isl.top);
  layer.appendChild(svgEl("path", { d, "fill-rule": "evenodd", fill: pal.top, stroke: pal.wallL, "stroke-width": "1.25" }));
}

function drawTerrain(layer, s, S, depth, pts, pal) {
  const V = s.vertices, H = s.heights, n = V.length;
  const cu = V.map(([x, z], i) => S(x, z, H[i])[0]).reduce((a, b) => a + b, 0) / n;
  // Sloped walls (top edge follows vertex heights), back→front.
  const edges = V.map((a, i) => ({ ai: i, bi: (i + 1) % n }))
    .map(e => ({ ...e, md: (depth(V[e.ai][0], V[e.ai][1]) + depth(V[e.bi][0], V[e.bi][1])) / 2 }))
    .sort((e1, e2) => e1.md - e2.md);
  for (const { ai, bi } of edges) {
    const quad = [S(V[ai][0], V[ai][1], H[ai]), S(V[bi][0], V[bi][1], H[bi]), S(V[bi][0], V[bi][1], s.floor), S(V[ai][0], V[ai][1], s.floor)];
    const midU = (quad[0][0] + quad[1][0]) / 2;
    layer.appendChild(svgEl("polygon", { points: pts(quad), fill: midU >= cu ? pal.wallR : pal.wallL, stroke: pal.wallL, "stroke-width": "0.5" }));
  }
  // Triangulated top, each facet shaded by how flat it is (lit from above).
  for (const [a, b, c] of earClip(V)) {
    const e1 = [V[b][0] - V[a][0], V[b][1] - V[a][1], H[b] - H[a]];
    const e2 = [V[c][0] - V[a][0], V[c][1] - V[a][1], H[c] - H[a]];
    const nh = e1[0] * e2[1] - e1[1] * e2[0];                 // h-component of the face normal
    const nl = Math.hypot(e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], nh) || 1;
    const flat = Math.min(1, Math.abs(nh) / nl);              // 1 = level, →0 = steep
    const tri = [S(V[a][0], V[a][1], H[a]), S(V[b][0], V[b][1], H[b]), S(V[c][0], V[c][1], H[c])];
    layer.appendChild(svgEl("polygon", { points: pts(tri), fill: lerp(pal.topDark, pal.top, flat), stroke: "none" }));
  }
}
