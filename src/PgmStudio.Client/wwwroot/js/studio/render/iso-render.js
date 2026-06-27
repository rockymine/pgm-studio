/**
 * Read-only isometric preview of the sketch — the composed island polygons extruded to their height,
 * drawn as shaded SVG prisms (S6). No WebGL/three.js: it reuses the SVG stack and the islands the live
 * preview already computes (geometry/boolean.js). Pure render: given island polygons + a yaw, it fits
 * the projection to the viewport and paints walls (back→front) then top faces. Per-anchor terrain shows
 * as a flat top for now (top = an island's tallest shape); orbit/true-3-D is a later upgrade.
 *
 * Island input: { exterior:[[x,z],…], holes:[[[x,z],…],…], top, floor, mirror }.
 */

import { svgEl } from "./svg.js";

const COS30 = Math.cos(Math.PI / 6);
const SIN30 = Math.sin(Math.PI / 6);

export function renderIso(layer, islands, w, h, yawDeg) {
  while (layer.firstChild) layer.removeChild(layer.firstChild);
  if (!islands?.length) return;

  const yaw = (yawDeg * Math.PI) / 180, cy = Math.cos(yaw), sy = Math.sin(yaw);
  // Ground-plane yaw, then 2:1 isometric; height raises screen-up (−v). `d` = ground depth (painter sort).
  const uv = (x, z, hh) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return [(rx - rz) * COS30, (rx + rz) * SIN30 - hh]; };
  const depth = (x, z) => { const rx = x * cy - z * sy, rz = x * sy + z * cy; return rx + rz; };

  // Fit every corner (floor + top) into the viewport.
  let umin = Infinity, umax = -Infinity, vmin = Infinity, vmax = -Infinity;
  for (const isl of islands)
    for (const ring of [isl.exterior, ...(isl.holes || [])])
      for (const [x, z] of ring)
        for (const hh of [isl.floor, isl.top]) {
          const [u, v] = uv(x, z, hh);
          if (u < umin) umin = u; if (u > umax) umax = u;
          if (v < vmin) vmin = v; if (v > vmax) vmax = v;
        }
  const bw = (umax - umin) || 1, bh = (vmax - vmin) || 1;
  const scale = Math.min((w - 24) / bw, (h - 24) / bh);
  const ox = (w - bw * scale) / 2 - umin * scale, oy = (h - bh * scale) / 2 - vmin * scale;
  const S = (x, z, hh) => { const [u, v] = uv(x, z, hh); return [ox + u * scale, oy + v * scale]; };
  const pts = a => a.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(" ");
  const ringPath = (ring, hh) => "M " + ring.map(([x, z], i) => { const [sx, sz] = S(x, z, hh); return (i ? "L" : "") + `${sx.toFixed(1)} ${sz.toFixed(1)}`; }).join(" ") + " Z";

  // Islands back→front so nearer masses overdraw farther ones (painter's algorithm).
  const ordered = islands
    .map(isl => ({ isl, d: Math.max(...isl.exterior.map(([x, z]) => depth(x, z))) }))
    .sort((a, b) => a.d - b.d);

  for (const { isl } of ordered) {
    const topFill   = isl.mirror ? "var(--canvas-mirror-fill)"   : "var(--canvas-result-fill)";
    const stroke    = isl.mirror ? "var(--canvas-mirror-stroke)" : "var(--canvas-result-stroke)";
    const ext = isl.exterior;

    // Side walls (exterior edges), back→front; only when the prism has thickness.
    if (isl.top !== isl.floor) {
      const edges = ext.map((a, i) => ({ a, b: ext[(i + 1) % ext.length] }))
        .map(e => ({ ...e, md: (depth(e.a[0], e.a[1]) + depth(e.b[0], e.b[1])) / 2 }))
        .sort((e1, e2) => e1.md - e2.md);
      for (const { a, b } of edges) {
        const quad = [S(a[0], a[1], isl.top), S(b[0], b[1], isl.top), S(b[0], b[1], isl.floor), S(a[0], a[1], isl.floor)];
        layer.appendChild(svgEl("polygon", {
          points: pts(quad), fill: stroke, "fill-opacity": "0.5",
          stroke, "stroke-width": "0.75", "stroke-opacity": "0.5",
        }));
      }
    }

    // Top face (exterior + holes via evenodd).
    let d = ringPath(ext, isl.top);
    for (const hole of (isl.holes || [])) d += " " + ringPath(hole, isl.top);
    layer.appendChild(svgEl("path", {
      d, "fill-rule": "evenodd", fill: topFill, "fill-opacity": "0.92", stroke, "stroke-width": "1.25",
    }));
  }
}
