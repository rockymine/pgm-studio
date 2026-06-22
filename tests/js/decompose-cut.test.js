// Tests for the lane-decomposition cut geometry (bridge/decompose-bridge.js relies on it). Pure functions,
// no DOM — runs in the standard `node --test` harness.
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  pointInRing, centroid, enclosedVertices, edgeMarkers, splitPiece, deriveLaneRole, geoPolys, polygonArea, labelCut,
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

// ── auto-label (deriveLaneRole) ───────────────────────────────────────────────
const PIECE = { exterior: SQUARE, holes: [] };

test("deriveLaneRole: spawn anchor wins over a wool anchor on the same piece", () => {
  const anchors = [{ kind: "wool", x: 3, z: 3 }, { kind: "spawn", x: 5, z: 5 }];
  assert.equal(deriveLaneRole(PIECE, anchors, []), "spawn");
});

test("deriveLaneRole: a lone wool anchor → wool", () => {
  assert.equal(deriveLaneRole(PIECE, [{ kind: "wool", x: 5, z: 5 }], []), "wool");
});

test("deriveLaneRole: anchors outside the piece are ignored", () => {
  assert.equal(deriveLaneRole(PIECE, [{ kind: "spawn", x: 50, z: 50 }], []), "hub");
});

test("deriveLaneRole: an anchor inside a hole does not count", () => {
  const holed = { exterior: SQUARE, holes: [[[4, 4], [6, 4], [6, 6], [4, 6]]] };
  assert.equal(deriveLaneRole(holed, [{ kind: "spawn", x: 5, z: 5 }], []), "hub");
});

test("deriveLaneRole: anchorless lane abutting the build region → frontline", () => {
  const build = geoPolys({ type: "Polygon", coordinates: [[[10, 0], [20, 0], [20, 10], [10, 10]]] });
  assert.equal(deriveLaneRole(PIECE, [], build), "frontline");          // shares the x=10 edge (dist 0)
  assert.equal(deriveLaneRole(PIECE, [], build, 0.5), "frontline");     // still within a small tol
});

test("deriveLaneRole: anchorless lane far from the build region → hub", () => {
  const build = geoPolys({ type: "Polygon", coordinates: [[[100, 0], [110, 0], [110, 10], [100, 10]]] });
  assert.equal(deriveLaneRole(PIECE, [], build), "hub");
});

test("geoPolys: MultiPolygon flattens to one entry per part with holes split out", () => {
  const polys = geoPolys({ type: "MultiPolygon", coordinates: [
    [[[0, 0], [4, 0], [4, 4], [0, 4]], [[1, 1], [2, 1], [2, 2], [1, 2]]],
    [[[8, 8], [9, 8], [9, 9], [8, 9]]],
  ] });
  assert.equal(polys.length, 2);
  assert.equal(polys[0].holes.length, 1);
  assert.equal(polys[1].holes.length, 0);
});

test("polygonArea: a 10x10 square is 100; a smaller spur is smaller", () => {
  assert.equal(polygonArea(SQUARE), 100);
  assert.ok(polygonArea([[0, 0], [3, 0], [3, 2], [0, 2]]) < polygonArea(SQUARE));   // 6 < 100 → the peeled lane
});

// ── labelCut: markers win over size when identifying the peeled lane ───────────
test("labelCut: a wool lane wins even when it is the BIGGER piece", () => {   // the reported bug
  const bigWoolLane = { exterior: [[0, 0], [20, 0], [20, 10], [0, 10]], holes: [] };   // area 200, holds the wool
  const smallHub    = { exterior: [[20, 0], [26, 0], [26, 4], [20, 4]], holes: [] };    // area 24, no marker
  assert.deepEqual(labelCut(bigWoolLane, smallHub, [{ kind: "wool", x: 10, z: 5 }], []), ["wool", "hub"]);
});

test("labelCut: peeling a spawn spur off a hub still holding two wools → spur=spawn, rest=hub", () => {
  const rest = { exterior: [[0, 0], [30, 0], [30, 10], [0, 10]], holes: [] };       // big, holds 2 wools
  const spawnSpur = { exterior: [[0, 10], [6, 10], [6, 16], [0, 16]], holes: [] };  // small, holds the spawn
  const anchors = [{ kind: "wool", x: 5, z: 5 }, { kind: "wool", x: 25, z: 5 }, { kind: "spawn", x: 3, z: 13 }];
  assert.deepEqual(labelCut(rest, spawnSpur, anchors, []), ["hub", "spawn"]);   // fewer markers (the spur) is the lane
});

test("labelCut: a tie (one marker each) falls back to the smaller piece being the lane", () => {
  const small = { exterior: [[0, 0], [4, 0], [4, 4], [0, 4]], holes: [] };
  const big   = { exterior: [[10, 0], [30, 0], [30, 10], [10, 10]], holes: [] };
  assert.deepEqual(labelCut(small, big, [{ kind: "wool", x: 2, z: 2 }, { kind: "wool", x: 20, z: 5 }], []), ["wool", "hub"]);
});
