/**
 * Rasterize sketch shapes into the solid (x,z) block cells they voxelize into — the client twin of the C#
 * `PgmStudio.Pgm.Sketch.SketchRasterizer`. It is the WYSIWYG half of the grid-aligned sketch: the
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
import { applySymmetry, orbitAxes } from "./symmetry.js";

const key = (x, z) => `${x},${z}`;

/** The block cells a single shape covers: bbox of its ring, kept where the cell centre is inside it. `turn`
 *  moves the ring first — an orbit image is the shape turned and then rasterized, as the server does it. */
function rasterShapeCells(shape, turn) {
  const ring = turn ? toRing(shape).map(([x, z]) => turn(x, z)) : toRing(shape);
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
 * normal ops). This is one footprint; `rasterizeBoard` adds the symmetry images.
 */
export function rasterizeShapes(shapes, turn = null) {
  const add = new Set(), sub = new Set(), oadd = new Set(), osub = new Set();
  for (const shape of shapes ?? []) {
    if (shape.role) continue;   // structural annotation — not terrain, never rasterized
    const cells = rasterShapeCells(shape, turn);
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
 * Every cell the board's ground covers: the primary footprint, and each image of every group that mirrors under
 * the map's symmetry — the group's shapes turned onto that image and rasterized, the way `SketchRasterizer` fans
 * a group with `mirrors` set. A group that does not mirror stands once.
 */
export function rasterizeBoard(shapes, groups, mode, cx = 0, cz = 0) {
  const cells = rasterizeShapes(shapes);
  const byId = new Map((shapes ?? []).map(shape => [shape.id, shape]));
  for (const group of groups ?? []) {
    if (!group.mirrors) continue;
    const members = (group.shapeIds ?? []).map(id => byId.get(id)).filter(Boolean);
    for (const axis of orbitAxes(mode))
      for (const cell of rasterizeShapes(members, (x, z) => applySymmetry(x, z, axis, cx, cz))) cells.add(cell);
  }
  return cells;
}
