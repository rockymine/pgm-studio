// Tests for the sketch boolean-island layer. Runs in the standard `node --test` harness — boolean.js
// imports the vendored polygon-clipping bundle relatively, so no node_modules is needed.
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta,
  shapeToMultiPoly, pointInIsland,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/boolean.js";

const rect = (id, min_x, min_z, max_x, max_z, extra = {}) =>
  ({ id, type: "rectangle", operation: "add", override: false, min_x, min_z, max_x, max_z, ...extra });

// ── computeIslands ──────────────────────────────────────────────────────────
test("two disjoint adds → two islands", () => {
  const { islands } = computeIslands([rect("a", 0, 0, 5, 5), rect("b", 10, 0, 15, 5)]);
  assert.equal(islands.length, 2);
});

test("two overlapping adds → one island", () => {
  const { islands } = computeIslands([rect("a", 0, 0, 6, 5), rect("b", 4, 0, 10, 5)]);
  assert.equal(islands.length, 1);
});

test("add minus interior subtract → one island with a hole", () => {
  const { islands } = computeIslands([
    rect("a", 0, 0, 10, 10),
    rect("b", 3, 3, 7, 7, { operation: "subtract" }),
  ]);
  assert.equal(islands.length, 1);
  assert.equal(islands[0].holes.length, 1);
});

test("no adds → no islands", () => {
  assert.deepEqual(computeIslands([]).islands, []);
});

// ── assignShapesToIslands ─────────────────────────────────────────────────────
test("assignShapesToIslands attributes each shape to its island", () => {
  const shapes = [rect("a", 0, 0, 5, 5), rect("b", 10, 0, 15, 5)];
  const { islands, addUnion, afterSub, overrideAddUnion } = computeIslands(shapes);
  assignShapesToIslands(shapes, islands, addUnion, overrideAddUnion, afterSub);
  const all = islands.flatMap(i => i.shapeIds).sort();
  assert.deepEqual(all, ["a", "b"]);
  assert.ok(islands.every(i => i.shapeIds.length === 1));
});

// ── computeMirrorPreview ──────────────────────────────────────────────────────
const sqIsland = (over = {}) =>
  ({ id: "i1", mirrors: true, exterior: [[0, 0], [2, 0], [2, 2], [0, 2], [0, 0]], holes: [], ...over });

test("mirror_x → one reflected copy about cx", () => {
  const out = computeMirrorPreview([sqIsland()], "mirror_x", 10, 0);
  assert.equal(out.length, 1);
  assert.equal(out[0].sourceId, "i1");
  assert.deepEqual(out[0].exterior[0], [20, 0]); // 2*10 - 0
  assert.deepEqual(out[0].exterior[1], [18, 0]); // 2*10 - 2
});

test("rot_90 → three copies; mirrors:false → none", () => {
  assert.equal(computeMirrorPreview([sqIsland()], "rot_90", 0, 0).length, 3);
  assert.equal(computeMirrorPreview([sqIsland({ mirrors: false })], "mirror_x", 0, 0).length, 0);
});

// ── restoreIslandMeta ─────────────────────────────────────────────────────────
test("restoreIslandMeta copies fields from the best shapeId-overlap match", () => {
  const islands = [{ shapeIds: ["a", "b"], name: "Island 1", mirrors: true }];
  restoreIslandMeta(islands, [{ shapeIds: ["a"], name: "North", mirrors: false }], ["name", "mirrors"]);
  assert.equal(islands[0].name, "North");
  assert.equal(islands[0].mirrors, false);
});

// ── helpers ───────────────────────────────────────────────────────────────────
test("shapeToMultiPoly wraps a ring; degenerate → []", () => {
  assert.equal(shapeToMultiPoly(rect("a", 0, 0, 4, 4))[0][0].length, 5);
  assert.deepEqual(shapeToMultiPoly({ type: "polygon", vertices: [[0, 0], [1, 1]] }), []);
});

test("pointInIsland respects holes", () => {
  const isl = { exterior: [[0, 0], [10, 0], [10, 10], [0, 10]], holes: [[[3, 3], [7, 3], [7, 7], [3, 7]]] };
  assert.equal(pointInIsland(1, 1, isl), true);   // inside exterior, outside hole
  assert.equal(pointInIsland(5, 5, isl), false);  // inside the hole
  assert.equal(pointInIsland(20, 20, isl), false); // outside exterior
});
