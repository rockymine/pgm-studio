// Tests for the lane-decomposition cut geometry (bridge/decompose-bridge.js relies on it). Pure functions,
// no DOM — runs in the standard `node --test` harness.
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  pointInRing, centroid, enclosedVertices, edgeMarkers, splitPiece,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/decompose-cut.js";

const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];   // open ring, CCW-ish

test("pointInRing: inside / outside", () => {
  assert.equal(pointInRing(5, 5, SQUARE), true);
  assert.equal(pointInRing(-1, 5, SQUARE), false);
});

test("enclosedVertices: a top band encloses the top two corners", () => {
  const lasso = [[-1, -1], [11, -1], [11, 3], [-1, 3]];
  assert.deepEqual(enclosedVertices(SQUARE, lasso), [0, 1]);
});

test("edgeMarkers: a top band crosses the two side edges", () => {
  const lasso = [[-1, -1], [11, -1], [11, 3], [-1, 3]];
  const m = edgeMarkers(SQUARE, lasso);
  assert.equal(m.length, 2);
  assert.deepEqual(m.map(x => x.edge).sort(), [1, 3]);
  // markers land on the side edges at z=3
  for (const x of m) assert.equal(Math.round(x.point[1]), 3);
});

test("splitPiece: a diagonal vertex→vertex cut yields the two triangles sharing the seam", () => {
  const piece = { exterior: SQUARE, holes: [], role: "other" };
  const [a, b] = splitPiece(piece, { kind: "vertex", index: 0 }, { kind: "vertex", index: 2 }, [8, 2]);
  assert.deepEqual(a.exterior, [[0, 0], [10, 0], [10, 10]]);   // lane = side containing (8,2)
  assert.deepEqual(b.exterior, [[10, 10], [0, 10], [0, 0]]);
  // both rings contain the seam endpoints (the coincident cut nodes)
  for (const ring of [a.exterior, b.exterior]) {
    assert.ok(ring.some(p => p[0] === 0 && p[1] === 0));
    assert.ok(ring.some(p => p[0] === 10 && p[1] === 10));
  }
});

test("splitPiece: a marker cut (lasso band) splits across the inserted edge points", () => {
  const piece = { exterior: SQUARE, holes: [], role: "other" };
  const lasso = [[-1, -1], [11, -1], [11, 3], [-1, 3]];
  const mk = edgeMarkers(SQUARE, lasso);
  const res = splitPiece(piece, mk[0], mk[1], centroid(lasso));   // lane = the top band
  assert.equal(res.length, 2);
  const top = res.find(p => p.exterior.every(v => v[1] <= 3.001));
  assert.ok(top, "one piece is the top band (z ≤ 3)");
  assert.ok(top.exterior.some(v => v[1] === 0), "the top band keeps the original top corners");
});

test("splitPiece: a hole goes to whichever piece contains it", () => {
  // hole centroid (8,2) sits in the lower-right triangle (the lane, which contains laneRep (8,2))
  const piece = { exterior: SQUARE, holes: [[[7, 1], [9, 1], [9, 3], [7, 3]]], role: "other" };
  const [lane, rem] = splitPiece(piece, { kind: "vertex", index: 0 }, { kind: "vertex", index: 2 }, [8, 2]);
  assert.equal(lane.holes.length, 1);
  assert.equal(rem.holes.length, 0);
});
