// Characterization tests for the pure geometry layer (no DOM).
// Concrete expected values pin behaviour so relocations/splits stay faithful.
import { test } from "node:test";
import assert from "node:assert/strict";

import { buildTransform, buildInverseTransform } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/transform.js";
import { pointInRing, rasterisePolygon, clipHalfPlane } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/polygon.js";
import { applySymmetry, applySymmetryToBounds } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/symmetry.js";
import { blockToExtentBounds, drawnBoundsFromBlocks, regionToBounds2d, sketchShapeToPgmRegion }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/region-convert.js";

// ── transform.js ──────────────────────────────────────────────────────────────
test("buildTransform maps corners and centre (PAD=20)", () => {
  const bbox = { min_x: 0, min_z: 0, max_x: 100, max_z: 50 };
  const f = buildTransform(bbox, 240, 240); // scale=2, offX=20, offY=70
  assert.deepEqual(f(0, 0), { x: 20, y: 70 });
  assert.deepEqual(f(100, 50), { x: 220, y: 170 });
  assert.deepEqual(f(50, 25), { x: 120, y: 120 });
});

test("buildInverseTransform round-trips buildTransform", () => {
  const bbox = { min_x: -10, min_z: 5, max_x: 30, max_z: 45 };
  const f = buildTransform(bbox, 300, 220);
  const g = buildInverseTransform(bbox, 300, 220);
  for (const [wx, wz] of [[-10, 5], [30, 45], [0, 20], [12, 33]]) {
    const back = g(f(wx, wz).x, f(wx, wz).y);
    assert.ok(Math.abs(back.x - wx) < 1e-9, `x ${back.x} ≈ ${wx}`);
    assert.ok(Math.abs(back.z - wz) < 1e-9, `z ${back.z} ≈ ${wz}`);
  }
});

// ── polygon.js ──────────────────────────────────────────────────────────────
test("pointInRing inside/outside", () => {
  const sq = [[0, 0], [10, 0], [10, 10], [0, 10]];
  assert.equal(pointInRing(5, 5, sq), true);
  assert.equal(pointInRing(15, 5, sq), false);
  assert.equal(pointInRing(-1, 5, sq), false);
});

test("rasterisePolygon fills cell centres", () => {
  const sq = [[0, 0], [2, 0], [2, 2], [0, 2]];
  assert.deepEqual(rasterisePolygon(sq), [[0, 0], [0, 1], [1, 0], [1, 1]]);
});

test("rasterisePolygon subtracts a hole", () => {
  const ext = [[0, 0], [4, 0], [4, 4], [0, 4]];          // 16 cells
  const hole = [[1, 1], [3, 1], [3, 3], [1, 3]];          // removes 4 centres
  assert.equal(rasterisePolygon(ext, [hole]).length, 12);
});

test("clipHalfPlane clips a square to x>=5", () => {
  const sq = [[0, 0], [10, 0], [10, 10], [0, 10]];
  assert.deepEqual(clipHalfPlane(sq, 5, 0, 1, 0), [[5, 0], [10, 0], [10, 10], [5, 10]]);
});

// ── symmetry.js ──────────────────────────────────────────────────────────────
test("applySymmetry per axis", () => {
  assert.deepEqual(applySymmetry(2, 3, "mirror_x", 5, 0), [8, 3]);
  assert.deepEqual(applySymmetry(2, 3, "mirror_z", 0, 5), [2, 7]);
  assert.deepEqual(applySymmetry(2, 3, "rot_180", 5, 5), [8, 7]);
  assert.deepEqual(applySymmetry(2, 3, "rot_90", 0, 0), [-3, 2]);
  assert.throws(() => applySymmetry(0, 0, "bogus", 0, 0), /Unknown symmetry axis/);
});

test("applySymmetryToBounds mirrors an AABB", () => {
  const b = { min_x: 0, max_x: 4, min_z: 0, max_z: 2 };
  assert.deepEqual(applySymmetryToBounds(b, "mirror_x", 10, 0),
    { min_x: 16, max_x: 20, min_z: 0, max_z: 2 });
});

// ── region-convert.js ─────────────────────────────────────────────────────────
test("block / drawn bounds", () => {
  assert.deepEqual(blockToExtentBounds(3, 5), { min_x: 3, max_x: 4, min_z: 5, max_z: 6 });
  assert.deepEqual(drawnBoundsFromBlocks(5, 5, 2, 8), { min_x: 2, max_x: 6, min_z: 5, max_z: 9 });
});

test("regionToBounds2d per type", () => {
  assert.deepEqual(regionToBounds2d({ type: "rectangle", min_x: 1, min_z: 2, max_x: 3, max_z: 4 }),
    { min_x: 1, min_z: 2, max_x: 3, max_z: 4 });
  assert.deepEqual(regionToBounds2d({ type: "cylinder", base_x: 10, base_z: 20, radius: 5 }),
    { min_x: 5, max_x: 15, min_z: 15, max_z: 25 });
  assert.deepEqual(regionToBounds2d({ type: "circle", center_x: 0, center_z: 0, radius: 3 }),
    { min_x: -3, max_x: 3, min_z: -3, max_z: 3 });
  assert.deepEqual(regionToBounds2d({ type: "sphere", origin_x: 1, origin_z: 1, radius: 2 }),
    { min_x: -1, max_x: 3, min_z: -1, max_z: 3 });
  assert.deepEqual(regionToBounds2d({ type: "block", x: 2, z: 3 }),
    { min_x: 2, max_x: 3, min_z: 3, max_z: 4 });
  assert.deepEqual(regionToBounds2d({ type: "point", x: 5, z: 5 }),
    { min_x: 4.5, max_x: 5.5, min_z: 4.5, max_z: 5.5 });
  assert.equal(regionToBounds2d({ type: "union" }), null);
  assert.equal(regionToBounds2d(null), null);
});

test("sketchShapeToPgmRegion", () => {
  assert.deepEqual(sketchShapeToPgmRegion({ type: "rectangle", min_x: 0, max_x: 2, min_z: 0, max_z: 2 }),
    { type: "rectangle", min_x: 0, max_x: 2, min_z: 0, max_z: 2 });
  assert.deepEqual(sketchShapeToPgmRegion({ type: "circle", center_x: 1, center_z: 1, radius: 3 }),
    { type: "circle", center_x: 1, center_z: 1, radius: 3 });
  assert.equal(sketchShapeToPgmRegion({ type: "polygon", vertices: [] }), null);
});
