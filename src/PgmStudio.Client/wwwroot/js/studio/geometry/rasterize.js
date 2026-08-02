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
