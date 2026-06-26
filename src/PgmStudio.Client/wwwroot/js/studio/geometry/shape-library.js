/**
 * Shape library — pre-defined geometric primitives the author drags onto the sketch canvas. **Pure
 * geometry, no semantics** (no gameplay/lane presets, no corpus shapes). A library entry is a *source
 * of ordinary SketchShapes*, not a new type: on drop it instantiates plain polygons (+ the one add/sub
 * composite), so the rasterizer / islands / mirror / height all just work. Pure: no DOM, no DB.
 *
 * Two families:
 *  - **Regular** n-gons {3,5,6,8} — generated from a regular polygon inscribed in a circle.
 *  - **Polyomino** ("tetris+") — rectilinear shapes on an integer **cell grid**; scaled by a single
 *    drop-time cell size so they land on clean block coords with right angles preserved.
 *  - **Composite** — hole-square (an `add` rect + a centred `subtract` rect), the only multi-shape entry.
 *
 * Unit coordinates are in **cells**; `instantiate()` scales by the cell size, centres on the drop point,
 * and rounds to blocks.
 */

import { translateShape } from "./shape.js";

const poly = (vertices) => ({ type: "polygon", operation: "add", vertices });
const rect = (min_x, min_z, max_x, max_z, operation = "add") => ({ type: "rectangle", operation, min_x, min_z, max_x, max_z });

// Regular n-gon vertices on a circle of radius `r` cells, first vertex at the top, clockwise.
function ngon(sides, r) {
  const v = [];
  for (let i = 0; i < sides; i++) {
    const a = -Math.PI / 2 + (2 * Math.PI * i) / sides;
    v.push([r * Math.cos(a), r * Math.sin(a)]);
  }
  return v;
}
const NGON_R = 1.6;          // cells — keeps n-gons comparable in size to the polyominoes
const DEFAULT_CELL = 12;     // blocks per cell at drop ≈ a lane width

// The catalog. `unit` shapes are in cell units (x→right, z→down). Polyomino rings are the locked
// templates (scythe/branch verified by boundary trace; see docs/contracts/sketch-tool-improvements.md §8).
export const LIBRARY = [
  // ── Regular ──────────────────────────────────────────────────────────────────
  { id: "tri",  name: "Triangle", category: "Regular", unit: [poly(ngon(3, NGON_R))] },
  { id: "pent", name: "Pentagon", category: "Regular", unit: [poly(ngon(5, NGON_R))] },
  { id: "hex",  name: "Hexagon",  category: "Regular", unit: [poly(ngon(6, NGON_R))] },
  { id: "oct",  name: "Octagon",  category: "Regular", unit: [poly(ngon(8, NGON_R))] },
  // ── Polyomino ────────────────────────────────────────────────────────────────
  { id: "L",      name: "L",            category: "Polyomino", unit: [poly([[0,0],[1,0],[1,2],[2,2],[2,3],[0,3]])] },
  { id: "U",      name: "U",            category: "Polyomino", unit: [poly([[0,0],[1,0],[1,2],[2,2],[2,0],[3,0],[3,3],[0,3]])] },
  { id: "T",      name: "T",            category: "Polyomino", unit: [poly([[0,0],[3,0],[3,1],[2,1],[2,3],[1,3],[1,1],[0,1]])] },
  { id: "I",      name: "I (bar)",      category: "Polyomino", unit: [rect(0, 0, 1, 4)] },
  { id: "scythe", name: "Scythe",       category: "Polyomino", unit: [poly([[0,0],[1,0],[1,1],[2,1],[2,0],[4,0],[4,1],[3,1],[3,2],[0,2]])] },
  { id: "cross",  name: "Cross (+)",    category: "Polyomino", unit: [poly([[1,0],[2,0],[2,1],[3,1],[3,2],[2,2],[2,3],[1,3],[1,2],[0,2],[0,1],[1,1]])] },
  { id: "branch", name: "Line+branch",  category: "Polyomino", unit: [poly([[0,1],[1,1],[1,0],[2,0],[2,1],[4,1],[4,2],[0,2]])] },
  // ── Composite ────────────────────────────────────────────────────────────────
  { id: "holesquare", name: "Hole-square", category: "Composite", unit: [rect(0, 0, 4, 4), rect(1, 1, 3, 3, "subtract")] },
];

// ── geometry helpers (operate on rect/polygon unit shapes) ─────────────────────

function shapeBounds(s) {
  if (s.type === "rectangle") return { min_x: s.min_x, min_z: s.min_z, max_x: s.max_x, max_z: s.max_z };
  const xs = s.vertices.map(v => v[0]), zs = s.vertices.map(v => v[1]);
  return { min_x: Math.min(...xs), min_z: Math.min(...zs), max_x: Math.max(...xs), max_z: Math.max(...zs) };
}

function combinedBounds(shapes) {
  const bs = shapes.map(shapeBounds);
  return {
    min_x: Math.min(...bs.map(b => b.min_x)), min_z: Math.min(...bs.map(b => b.min_z)),
    max_x: Math.max(...bs.map(b => b.max_x)), max_z: Math.max(...bs.map(b => b.max_z)),
  };
}

const scaleShape = (s, k) => s.type === "rectangle"
  ? { ...s, min_x: s.min_x * k, min_z: s.min_z * k, max_x: s.max_x * k, max_z: s.max_z * k }
  : { ...s, vertices: s.vertices.map(([x, z]) => [x * k, z * k]) };

const roundShape = (s) => s.type === "rectangle"
  ? { ...s, min_x: Math.round(s.min_x), min_z: Math.round(s.min_z), max_x: Math.round(s.max_x), max_z: Math.round(s.max_z) }
  : { ...s, vertices: s.vertices.map(([x, z]) => [Math.round(x), Math.round(z)]) };

/**
 * Instantiate a library item as ordinary shape specs (no id) centred on the drop point `(px,pz)`,
 * scaled by `cell` blocks/unit and snapped to blocks. The bridge assigns ids and adds them.
 */
export function instantiate(item, px, pz, cell = DEFAULT_CELL) {
  const scaled = item.unit.map(s => scaleShape(s, cell));
  const b = combinedBounds(scaled);
  const dx = px - (b.min_x + b.max_x) / 2, dz = pz - (b.min_z + b.max_z) / 2;
  return scaled.map(s => roundShape(translateShape(s, dx, dz)));
}

// ── thumbnail metadata for the palette (an SVG `d` + viewBox in cell units) ─────

function shapePathD(s) {
  const ring = s.type === "rectangle"
    ? [[s.min_x, s.min_z], [s.max_x, s.min_z], [s.max_x, s.max_z], [s.min_x, s.max_z]]
    : s.vertices;
  return "M " + ring.map(([x, z], i) => `${i ? "L" : ""}${x} ${z}`).join(" ") + " Z";
}

/** Per-item palette metadata: id/name/category + a thumbnail path (add+sub combined; evenodd → hole). */
export function libraryMeta() {
  return LIBRARY.map(item => {
    const b = combinedBounds(item.unit);
    const pad = 0.3;
    const w = (b.max_x - b.min_x) + 2 * pad, h = (b.max_z - b.min_z) + 2 * pad;
    return {
      id: item.id, name: item.name, category: item.category,
      thumbD: item.unit.map(shapePathD).join(" "),
      thumbVB: `${b.min_x - pad} ${b.min_z - pad} ${w} ${h}`,
    };
  });
}
