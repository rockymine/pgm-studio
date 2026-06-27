/**
 * Read-only isometric preview of the sketch — the composed island polygons extruded to their height,
 * drawn as shaded SVG prisms (S6). No WebGL/three.js: it reuses the SVG stack and the islands the live
 * preview already computes (geometry/boolean.js). Pure render.
 *
 * Reads as "looking down" via three cues: a ground-plane reference (the working bbox at y=0), OPAQUE faces
 * (so nearer masses occlude farther ones — no see-through), and bright tops over darker, two-tone side
 * walls (lit from above). Painter's algorithm orders everything back→front. Per-anchor terrain shows as a
 * flat top for now; orbit/true-3-D is a later upgrade.
 *
 * Island input: { exterior:[[x,z],…], holes:[[[x,z],…],…], top, floor, mirror }.
 */

import { svgEl } from "./svg.js";

const COS30 = Math.cos(Math.PI / 6);
const SIN30 = Math.sin(Math.PI / 6);

// Opaque palette (a 3-D preview keeps a fixed, readable scheme rather than the translucent canvas vars).
const PAL = {
  island: { top: "#6d7ce8", wallR: "#5365cf", wallL: "#3f4ea8" },
  mirror: { top: "#abb2dd", wallR: "#9099c9", wallL: "#737cae" },
  ground: { fill: "#ccd4e6", stroke: "#a9b2c8" },
};

export function renderIso(layer, islands, w, h, yawDeg, bbox) {
  while (layer.firstChild) layer.removeChild(layer.firstChild);
  if (!islands?.length) return;

  const yaw = (yawDeg * Math.PI) / 180, cy = Math.cos(yaw), sy = Math.sin(yaw);
  const uv = (x, z, hh) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return [(rx - rz) * COS30, (rx + rz) * SIN30 - hh]; };
  const depth = (x, z) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return rx + rz; };

  const groundCorners = bbox
    ? [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.min_z], [bbox.max_x, bbox.max_z], [bbox.min_x, bbox.max_z]]
    : null;

  // Fit every corner (ground + island floors/tops) into the viewport.
  let umin = Infinity, umax = -Infinity, vmin = Infinity, vmax = -Infinity;
  const grow = (x, z, hh) => { const [u, v] = uv(x, z, hh); if (u < umin) umin = u; if (u > umax) umax = u; if (v < vmin) vmin = v; if (v > vmax) vmax = v; };
  for (const c of (groundCorners || [])) grow(c[0], c[1], 0);
  for (const isl of islands)
    for (const ring of [isl.exterior, ...(isl.holes || [])])
      for (const [x, z] of ring) { grow(x, z, isl.floor); grow(x, z, isl.top); }

  const bw = (umax - umin) || 1, bh = (vmax - vmin) || 1;
  const scale = Math.min((w - 24) / bw, (h - 24) / bh);
  const ox = (w - bw * scale) / 2 - umin * scale, oy = (h - bh * scale) / 2 - vmin * scale;
  const S = (x, z, hh) => { const [u, v] = uv(x, z, hh); return [ox + u * scale, oy + v * scale]; };
  const pts = a => a.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(" ");
  const ringPath = (ring, hh) => "M " + ring.map(([x, z], i) => { const [sx, sz] = S(x, z, hh); return (i ? "L" : "") + `${sx.toFixed(1)} ${sz.toFixed(1)}`; }).join(" ") + " Z";

  // Ground reference plane at y=0 (anchors up/down + scale), behind everything.
  if (groundCorners) {
    layer.appendChild(svgEl("polygon", {
      points: pts(groundCorners.map(c => S(c[0], c[1], 0))),
      fill: PAL.ground.fill, "fill-opacity": "0.45", stroke: PAL.ground.stroke, "stroke-width": "1", "stroke-dasharray": "4 3",
    }));
  }

  // Islands back→front so nearer masses overdraw farther ones (painter's algorithm).
  const ordered = islands
    .map(isl => ({ isl, d: Math.max(...isl.exterior.map(([x, z]) => depth(x, z))) }))
    .sort((a, b) => a.d - b.d);

  for (const { isl } of ordered) {
    const pal = isl.mirror ? PAL.mirror : PAL.island;
    const ext = isl.exterior;

    if (isl.top !== isl.floor) {
      // Screen centroid of the top face → shade right-facing walls lighter, left-facing darker (form cue).
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

    // Top face (exterior + holes via evenodd), opaque.
    let d = ringPath(ext, isl.top);
    for (const hole of (isl.holes || [])) d += " " + ringPath(hole, isl.top);
    layer.appendChild(svgEl("path", { d, "fill-rule": "evenodd", fill: pal.top, stroke: pal.wallL, "stroke-width": "1.25" }));
  }
}
