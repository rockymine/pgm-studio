// Characterization tests for the unified shape model (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";

import { toRing, circleToRing, sampleBezierEdge, ringCentroid, toBounds, containsPoint, BEZIER_SAMPLES }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/shape.js";

// ── toRing ────────────────────────────────────────────────────────────────────
test("toRing rectangle is a closed CW ring", () => {
  assert.deepEqual(toRing({ type: "rectangle", min_x: 0, min_z: 0, max_x: 4, max_z: 2 }),
    [[0, 0], [4, 0], [4, 2], [0, 2], [0, 0]]);
});

test("toRing polygon without controls closes back to the first vertex", () => {
  assert.deepEqual(toRing({ type: "polygon", vertices: [[0, 0], [4, 0], [4, 4]] }),
    [[0, 0], [4, 0], [4, 4], [0, 0]]);
});

test("toRing returns [] for a degenerate polygon/lasso", () => {
  assert.deepEqual(toRing({ type: "polygon", vertices: [[0, 0], [1, 1]] }), []);
  assert.deepEqual(toRing({ type: "lasso", vertices: null }), []);
});

test("toRing discretizes a Bézier edge (16 samples, endpoint excluded)", () => {
  const ring = toRing({ type: "polygon", vertices: [[0, 0], [10, 0], [10, 10]], controls: { "0": { out: [3, 0] } } });
  // edge0 (Bézier) → 16 pts, edge1 + edge2 straight → 1 pt each, + closing repeat = 19
  assert.equal(ring.length, BEZIER_SAMPLES + 1 + 1 + 1);
  assert.deepEqual(ring[0], [0, 0]);            // first Bézier sample is p0 (t=0)
  assert.deepEqual(ring[ring.length - 1], ring[0]); // closed
});

test("toRing throws on unknown type", () => {
  assert.throws(() => toRing({ type: "blob" }), /Unknown shape type/);
});

// ── circleToRing ────────────────────────────────────────────────────────────────
test("circleToRing rounds to block coords and closes", () => {
  assert.deepEqual(circleToRing(10, 10, 5, 4),
    [[15, 10], [10, 15], [5, 10], [10, 5], [15, 10]]);
});

// ── sampleBezierEdge ──────────────────────────────────────────────────────────
test("sampleBezierEdge starts at p0 and excludes the endpoint", () => {
  const pts = sampleBezierEdge([0, 0], [0, 10], [10, 10], [10, 0]);
  assert.equal(pts.length, BEZIER_SAMPLES);
  assert.deepEqual(pts[0], [0, 0]);
  assert.notDeepEqual(pts[pts.length - 1], [10, 0]); // endpoint not included
});

// ── ringCentroid ──────────────────────────────────────────────────────────────
test("ringCentroid ignores the closing repeat", () => {
  assert.deepEqual(ringCentroid([[0, 0], [4, 0], [4, 4], [0, 4], [0, 0]]), [2, 2]);
});

// ── toBounds ────────────────────────────────────────────────────────────────────
test("toBounds per type", () => {
  assert.deepEqual(toBounds({ type: "rectangle", min_x: 1, min_z: 2, max_x: 3, max_z: 4 }),
    { min_x: 1, min_z: 2, max_x: 3, max_z: 4 });
  assert.deepEqual(toBounds({ type: "circle", center_x: 0, center_z: 0, radius: 3 }),
    { min_x: -3, min_z: -3, max_x: 3, max_z: 3 });
  assert.deepEqual(toBounds({ type: "polygon", vertices: [[0, 0], [4, 0], [2, 5]] }),
    { min_x: 0, min_z: 0, max_x: 4, max_z: 5 });
  assert.equal(toBounds({ type: "lasso", vertices: [] }), null);
});

// ── containsPoint ─────────────────────────────────────────────────────────────
test("containsPoint per type", () => {
  const rect = { type: "rectangle", min_x: 0, min_z: 0, max_x: 10, max_z: 10 };
  assert.equal(containsPoint(rect, 5, 5), true);
  assert.equal(containsPoint(rect, 15, 5), false);

  const circ = { type: "circle", center_x: 0, center_z: 0, radius: 5 };
  assert.equal(containsPoint(circ, 3, 4), true);   // hypot = 5
  assert.equal(containsPoint(circ, 4, 4), false);  // hypot ≈ 5.66

  const poly = { type: "polygon", vertices: [[0, 0], [10, 0], [10, 10], [0, 10]] };
  assert.equal(containsPoint(poly, 5, 5), true);
  assert.equal(containsPoint(poly, 15, 5), false);
});

test("containsPoint includes the Bézier curve bulge (hit shape matches the drawn outline)", () => {
  // Bottom edge v0→v1 bows outward to negative z (the cubic reaches ≈ z=-3 at its midpoint).
  const curved = {
    type: "polygon",
    vertices: [[0, 0], [10, 0], [10, 10], [0, 10]],
    controls: { "0": { out: [3, -4] }, "1": { in: [7, -4] } },
  };
  assert.equal(containsPoint(curved, 5, 5), true);    // main body
  assert.equal(containsPoint(curved, 5, -2), true);   // inside the bulge — outside the raw vertex hull
  assert.equal(containsPoint(curved, 5, -5), false);  // beyond the bulge
});
