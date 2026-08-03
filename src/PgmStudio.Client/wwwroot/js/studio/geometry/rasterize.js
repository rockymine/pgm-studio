/**
 * Rasterize sketch shapes into the solid (x,z) block cells they voxelize into — the client twin of the C#
 * `PgmStudio.Pgm.Sketch.SketchRasterizer` (S23). It is the WYSIWYG half of the grid-aligned sketch: the
 * author draws smooth curves, and this shows the exact blocks the export will place, so nothing about the
 * voxelization is hidden. Pure, no DOM.
 *
 * Parity with the server (the two MUST agree — same rings, same fill rule): each shape becomes a ring via
 * {@link toRing} (circle = 64-gon, Bézier = 16 samples/edge — the shared constants), a cell `(x,z)` is
 * solid when its **centre `(x+0.5, z+0.5)`** falls inside the ring, and the four draw operations resolve in
 * the fixed order `((adds − subtracts) ∪ override-adds) − override-subtracts`.
 */

import { toRing } from "./shape.js";
import { pointInRing } from "./polygon.js";

const key = (x, z) => `${x},${z}`;

/** The block cells a single shape covers: bbox of its ring, kept where the cell centre is inside it. */
function rasterShapeCells(shape) {
  const ring = toRing(shape);
  if (ring.length < 3) return [];
  let minX = Infinity, minZ = Infinity, maxX = -Infinity, maxZ = -Infinity;
  for (const [x, z] of ring) {
    if (x < minX) minX = x; if (x > maxX) maxX = x;
    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
  }
  const cells = [];
  for (let x = Math.floor(minX); x < Math.ceil(maxX); x++)
    for (let z = Math.floor(minZ); z < Math.ceil(maxZ); z++)
      if (pointInRing(x + 0.5, z + 0.5, ring)) cells.push([x, z]);
  return cells;
}

/**
 * The solid footprint of a shape group as a Set of `"x,z"` cell keys. Mirrors the server's 4-step set
 * algebra: normal add/subtract, then override add/subtract applied last (immune to / cutting through the
 * normal ops). Mirror copies are the host's concern and not applied here — this is the primary footprint.
 */
export function rasterizeShapes(shapes) {
  const add = new Set(), sub = new Set(), oadd = new Set(), osub = new Set();
  for (const shape of shapes ?? []) {
    if (shape.role) continue;   // structural annotation (S25) — not terrain, never rasterized
    const cells = rasterShapeCells(shape);
    if (shape.operation === "subtract") {
      const set = shape.override ? osub : sub;
      for (const [x, z] of cells) set.add(key(x, z));
    } else {
      const set = shape.override ? oadd : add;
      for (const [x, z] of cells) set.add(key(x, z));
    }
  }
  const result = new Set(add);
  for (const k of sub) result.delete(k);
  for (const k of oadd) result.add(k);
  for (const k of osub) result.delete(k);
  return result;
}

/**
 * Like {@link rasterizeShapes}, but each solid cell is mapped to the id of the add shape that owns it — the
 * smallest-area (most specific) add winning an overlap, then normal/override subtracts cutting as in the set
 * algebra. Feeds the themed Blocks overlay: a cell's colour is its owner shape's theme surface. Role-tagged
 * (structural, S25) shapes are skipped. Primary footprint only — mirror copies are the host's concern.
 */
export function rasterizeOwners(shapes) {
  const add = new Map(), area = new Map(), oadd = new Map(), oarea = new Map();
  const sub = new Set(), osub = new Set();
  for (const shape of shapes ?? []) {
    if (shape.role) continue;
    const cells = rasterShapeCells(shape);
    if (shape.operation === "subtract") {
      const set = shape.override ? osub : sub;
      for (const [x, z] of cells) set.add(key(x, z));
    } else {
      const [ownerMap, areaMap] = shape.override ? [oadd, oarea] : [add, area];
      const a = cells.length;
      for (const [x, z] of cells) {
        const k = key(x, z);
        if (!ownerMap.has(k) || a < areaMap.get(k)) { ownerMap.set(k, shape.id); areaMap.set(k, a); }
      }
    }
  }
  const result = new Map(add);
  for (const k of sub) result.delete(k);
  for (const [k, id] of oadd) result.set(k, id);
  for (const k of osub) result.delete(k);
  return result;
}

/**
 * Merge an owner map (`"x,z"` → shape id) into coloured horizontal runs `{ x, z, w, color }`, splitting a run
 * where the colour changes. `colorOf(shapeId)` returns the cell's fill (or null for the default stone).
 */
export function cellRunsColored(owners, colorOf) {
  const byRow = new Map();
  for (const [k, id] of owners) {
    const comma = k.indexOf(",");
    const x = parseInt(k.slice(0, comma), 10), z = parseInt(k.slice(comma + 1), 10);
    let row = byRow.get(z);
    if (!row) { row = []; byRow.set(z, row); }
    row.push([x, colorOf(id) ?? null]);
  }
  const runs = [];
  for (const [z, cells] of byRow) {
    cells.sort((a, b) => a[0] - b[0]);
    let start = cells[0][0], prev = cells[0][0], color = cells[0][1];
    for (let i = 1; i < cells.length; i++) {
      const [x, c] = cells[i];
      if (x === prev + 1 && c === color) { prev = x; continue; }
      runs.push({ x: start, z, w: prev - start + 1, color });
      start = prev = x; color = c;
    }
    runs.push({ x: start, z, w: prev - start + 1, color });
  }
  return runs;
}

/**
 * Merge a Set of `"x,z"` cell keys into horizontal runs `{ x, z, w }` (one fillRect each), so painting the
 * footprint is a few hundred rects instead of one per block. Rows are swept left→right; a gap closes a run.
 */
export function cellRuns(cells) {
  const byRow = new Map();
  for (const k of cells) {
    const comma = k.indexOf(",");
    const x = parseInt(k.slice(0, comma), 10), z = parseInt(k.slice(comma + 1), 10);
    let row = byRow.get(z);
    if (!row) { row = []; byRow.set(z, row); }
    row.push(x);
  }
  const runs = [];
  for (const [z, xs] of byRow) {
    xs.sort((a, b) => a - b);
    let start = xs[0], prev = xs[0];
    for (let i = 1; i < xs.length; i++) {
      if (xs[i] === prev + 1) { prev = xs[i]; continue; }
      runs.push({ x: start, z, w: prev - start + 1 });
      start = prev = xs[i];
    }
    runs.push({ x: start, z, w: prev - start + 1 });
  }
  return runs;
}
