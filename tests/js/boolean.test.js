// Tests for the sketch boolean-group layer. Runs in the standard `node --test` harness — boolean.js
// imports the vendored polygon-clipping bundle relatively, so no node_modules is needed.
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  computeGroups, assignShapesToGroups, computeMirrorPreview, restoreGroupMeta,
  shapeToMultiPoly, pointInGroup,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/boolean.js";

const rect = (id, min_x, min_z, max_x, max_z, extra = {}) =>
  ({ id, type: "rectangle", operation: "add", override: false, min_x, min_z, max_x, max_z, ...extra });

// ── computeGroups ──────────────────────────────────────────────────────────
test("two disjoint adds → two groups", () => {
  const { groups } = computeGroups([rect("a", 0, 0, 5, 5), rect("b", 10, 0, 15, 5)]);
  assert.equal(groups.length, 2);
});

test("two overlapping adds → one group", () => {
  const { groups } = computeGroups([rect("a", 0, 0, 6, 5), rect("b", 4, 0, 10, 5)]);
  assert.equal(groups.length, 1);
});

test("add minus interior subtract → one group with a hole", () => {
  const { groups } = computeGroups([
    rect("a", 0, 0, 10, 10),
    rect("b", 3, 3, 7, 7, { operation: "subtract" }),
  ]);
  assert.equal(groups.length, 1);
  assert.equal(groups[0].holes.length, 1);
});

test("no adds → no groups", () => {
  assert.deepEqual(computeGroups([]).groups, []);
});

// ── assignShapesToGroups ─────────────────────────────────────────────────────
test("assignShapesToGroups attributes each shape to its group", () => {
  const shapes = [rect("a", 0, 0, 5, 5), rect("b", 10, 0, 15, 5)];
  const { groups, addUnion, afterSub, overrideAddUnion } = computeGroups(shapes);
  assignShapesToGroups(shapes, groups, addUnion, overrideAddUnion, afterSub);
  const all = groups.flatMap(i => i.shapeIds).sort();
  assert.deepEqual(all, ["a", "b"]);
  assert.ok(groups.every(i => i.shapeIds.length === 1));
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

// ── restoreGroupMeta ─────────────────────────────────────────────────────────
test("restoreGroupMeta copies fields from the best shapeId-overlap match", () => {
  const groups = [{ shapeIds: ["a", "b"], name: "Group 1", mirrors: true }];
  restoreGroupMeta(groups, [{ shapeIds: ["a"], name: "North", mirrors: false }], ["name", "mirrors"]);
  assert.equal(groups[0].name, "North");
  assert.equal(groups[0].mirrors, false);
});

test("restoreGroupMeta gives one saved record to one group", () => {
  // The board's own group and two strays a brush stroke left outside it, all overlapping the single
  // saved record. Handing that record to all three would write three groups under one id — and an id
  // is what a relief is keyed by, so the board comes back flat.
  const groups = [
    { shapeIds: ["s0", "a", "b", "c"], id: "isl_1", name: "Group 1" },
    { shapeIds: ["d"], id: "isl_2", name: "Group 2" },
    { shapeIds: ["e"], id: "isl_3", name: "Group 3" },
  ];
  restoreGroupMeta(groups, [{ shapeIds: ["s0", "a", "b", "c", "d", "e"], id: "team", name: "Team group" }],
                    ["id", "name"]);
  assert.deepEqual(groups.map(i => i.id), ["team", "isl_2", "isl_3"]);
  assert.equal(new Set(groups.map(i => i.id)).size, 3);
});

test("restoreGroupMeta pairs two records with two groups by strongest overlap first", () => {
  const groups = [{ shapeIds: ["a", "b", "c"] }, { shapeIds: ["d", "e"] }];
  restoreGroupMeta(groups, [{ shapeIds: ["d", "e"], id: "south" }, { shapeIds: ["a", "b", "c"], id: "north" }],
                    ["id"]);
  assert.deepEqual(groups.map(i => i.id), ["north", "south"]);
});

// ── helpers ───────────────────────────────────────────────────────────────────
test("shapeToMultiPoly wraps a ring; degenerate → []", () => {
  assert.equal(shapeToMultiPoly(rect("a", 0, 0, 4, 4))[0][0].length, 5);
  assert.deepEqual(shapeToMultiPoly({ type: "polygon", vertices: [[0, 0], [1, 1]] }), []);
});

test("pointInGroup respects holes", () => {
  const isl = { exterior: [[0, 0], [10, 0], [10, 10], [0, 10]], holes: [[[3, 3], [7, 3], [7, 7], [3, 7]]] };
  assert.equal(pointInGroup(1, 1, isl), true);   // inside exterior, outside hole
  assert.equal(pointInGroup(5, 5, isl), false);  // inside the hole
  assert.equal(pointInGroup(20, 20, isl), false); // outside exterior
});
