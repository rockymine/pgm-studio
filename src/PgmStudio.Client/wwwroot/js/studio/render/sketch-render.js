/**
 * Stateless SVG emit for the sketch tool — draw primitives, island/mirror result polygons, and the
 * setup overlays (bbox / chunk grid / symmetry axis). Each function takes a layer <g> (or returns an
 * element) + data + a world→SVG transform; the canvas owns the layer lifecycle and calls these, so
 * sketch-canvas.js stays focused on state + interaction (no rendering bulk). Reuses render/shape-render
 * + render/svg. No interaction wiring — the canvas attaches handlers to the returned shape group.
 */

import { svgEl, ringToPath, polyToPath } from "./svg.js";
import { renderShape } from "./shape-render.js";

const ADD_FILL   = "var(--canvas-add-fill)";
const ADD_STROKE = "var(--canvas-add-stroke)";
const SUB_FILL   = "var(--canvas-sub-fill)";
const SUB_STROKE = "var(--canvas-sub-stroke)";

function clear(layer) { while (layer.firstChild) layer.removeChild(layer.firstChild); }

function shapeAttrs(shape) {
  const isAdd = shape.operation !== "subtract";
  return {
    fill: isAdd ? ADD_FILL : SUB_FILL,
    stroke: isAdd ? ADD_STROKE : SUB_STROKE,
    "stroke-width": "1.2", "fill-opacity": "0.28", "vector-effect": "non-scaling-stroke",
    ...(shape.override ? { "stroke-dasharray": "6 3" } : {}),
  };
}

/**
 * Build the SVG group for a draw primitive — `<g class="sk-shape" data-id>` containing the styled
 * shape (add/subtract colour, dashed when override). No event handlers (the canvas attaches them).
 */
export function renderSketchShape(shape, toSvg) {
  const attrs = shapeAttrs(shape);
  const g = svgEl("g", { class: "sk-shape", "data-id": shape.id });

  if (shape.type === "rectangle") {
    const el = renderShape("rectangle", shape, toSvg, attrs);
    if (el) g.appendChild(el);
  } else if (shape.type === "circle") {
    const b = {
      min_x: shape.center_x - shape.radius, max_x: shape.center_x + shape.radius,
      min_z: shape.center_z - shape.radius, max_z: shape.center_z + shape.radius,
    };
    const el = renderShape("circle", b, toSvg, attrs);
    if (el) g.appendChild(el);
  } else if (shape.type === "polygon" || shape.type === "lasso") {
    if ((shape.vertices?.length ?? 0) >= 3) {
      g.appendChild(svgEl("path", {
        d: ringToPath(shape.vertices, toSvg, shape.controls || {}),
        "fill-rule": "evenodd", ...attrs,
      }));
    }
  }
  return g;
}

/** Ghost preview of a library item being placed — the (already world-positioned) shape specs at
 *  reduced opacity. Cleared first; `specs` null/empty clears the layer. */
export function renderPlaceGhost(layer, specs, toSvg) {
  clear(layer);
  if (!specs?.length) return;
  for (const spec of specs) {
    const g = renderSketchShape({ ...spec, id: "ghost" }, toSvg);
    g.setAttribute("opacity", "0.55");
    layer.appendChild(g);
  }
}

/** Paint the computed island result polygons (exterior + holes) into `layer` (cleared first). */
export function renderIslands(layer, islands, toSvg) {
  clear(layer);
  for (const isl of islands) {
    if (!isl?.exterior?.length) continue;
    layer.appendChild(svgEl("path", {
      d: polyToPath({ exterior: isl.exterior, holes: isl.holes ?? [] }, toSvg),
      fill: "var(--canvas-result-fill)", stroke: "var(--canvas-result-stroke)",
      "stroke-width": "1.5", "fill-opacity": "0.22", "fill-rule": "evenodd",
      "vector-effect": "non-scaling-stroke",
    }));
  }
}

/** Paint the *other* layers' island outlines faintly (S7) — context for aligning the active layer. */
export function renderGhostIslands(layer, polys, toSvg) {
  clear(layer);
  for (const p of polys) {
    if (!p?.exterior?.length) continue;
    layer.appendChild(svgEl("path", {
      d: polyToPath({ exterior: p.exterior, holes: p.holes ?? [] }, toSvg),
      fill: "var(--canvas-island)", "fill-opacity": "0.07",
      stroke: "var(--canvas-island)", "stroke-width": "1", "stroke-dasharray": "2 4",
      "fill-rule": "evenodd", "vector-effect": "non-scaling-stroke",
    }));
  }
}

/** Paint the live mirror-preview polygons into `layer` (cleared first). */
export function renderMirror(layer, polys, toSvg) {
  clear(layer);
  for (const poly of polys) {
    if (!poly?.exterior?.length) continue;
    layer.appendChild(svgEl("path", {
      d: polyToPath({ exterior: poly.exterior, holes: poly.holes ?? [] }, toSvg),
      fill: "var(--canvas-mirror-fill)", stroke: "var(--canvas-mirror-stroke)",
      "stroke-width": "1", "fill-rule": "evenodd", "vector-effect": "non-scaling-stroke",
    }));
  }
}

/** The working-bounds rectangle. */
export function renderBbox(layer, bbox, toSvg) {
  clear(layer);
  if (!bbox) return;
  const p1 = toSvg(bbox.min_x, bbox.min_z), p2 = toSvg(bbox.max_x, bbox.max_z);
  layer.appendChild(svgEl("rect", {
    x: Math.min(p1.x, p2.x), y: Math.min(p1.y, p2.y),
    width: Math.abs(p2.x - p1.x), height: Math.abs(p2.y - p1.y),
    fill: "none", stroke: "var(--border)", "stroke-width": "1", "vector-effect": "non-scaling-stroke",
  }));
}

/** 16-block chunk grid clipped to the bbox. */
export function renderChunkGrid(layer, bbox, toSvg) {
  clear(layer);
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const attrs = {
    stroke: "var(--canvas-chunk)", "stroke-width": "1", "stroke-dasharray": "3 3",
    "vector-effect": "non-scaling-stroke",
  };
  const line = (x1, z1, x2, z2) => {
    const a = toSvg(x1, z1), b = toSvg(x2, z2);
    layer.appendChild(svgEl("line", { x1: a.x, y1: a.y, x2: b.x, y2: b.y, ...attrs }));
  };
  for (let x = Math.ceil(min_x / 16) * 16; x <= max_x; x += 16) line(x, min_z, x, max_z);
  for (let z = Math.ceil(min_z / 16) * 16; z <= max_z; z += 16) line(min_x, z, max_x, z);
}

/** The symmetry axis line(s) for the current mirror mode, through the centre, clipped to the bbox. */
export function renderAxis(layer, bbox, center, mode, toSvg) {
  clear(layer);
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const cx = center?.cx ?? 0, cz = center?.cz ?? 0;
  const attrs = {
    stroke: "var(--canvas-axis)", "stroke-width": "1", "stroke-dasharray": "6 4", opacity: "0.75",
    "vector-effect": "non-scaling-stroke",
  };
  const line = (x1, z1, x2, z2) => {
    const a = toSvg(x1, z1), b = toSvg(x2, z2);
    layer.appendChild(svgEl("line", { x1: a.x, y1: a.y, x2: b.x, y2: b.y, ...attrs }));
  };
  if (mode === "mirror_x")      line(cx, min_z, cx, max_z);
  else if (mode === "mirror_z") line(min_x, cz, max_x, cz);
  else { line(cx, min_z, cx, max_z); line(min_x, cz, max_x, cz); } // rot_180 / rot_90 → both
}
