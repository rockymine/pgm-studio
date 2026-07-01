// Characterization tests for the unified shape model (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";

import { toRing, circleToRing, sampleBezierEdge, ringCentroid, toBounds, containsPoint, rectToPolygon, boundsOfShapes, translateShape, rotateShape, scaleShape, BEZIER_SAMPLES }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/shape.js";

const r6 = (v) => Math.round(v * 1e6) / 1e6;   // tame float noise from cos/sin
const roundPts = (pts) => pts.map(([x, z]) => [r6(x), r6(z)]);

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

// ── rectToPolygon ─────────────────────────────────────────────────────────────
test("rectToPolygon keeps id/operation/override and the 4 corners CW", () => {
  const poly = rectToPolygon({ id: "r1", type: "rectangle", operation: "subtract", override: true, min_x: 0, min_z: 0, max_x: 4, max_z: 2 });
  assert.equal(poly.id, "r1");
  assert.equal(poly.type, "polygon");
  assert.equal(poly.operation, "subtract");
  assert.equal(poly.override, true);
  assert.deepEqual(poly.vertices, [[0, 0], [4, 0], [4, 2], [0, 2]]);
});

test("rectToPolygon carries the height fields (S15 — no reset to the default)", () => {
  const poly = rectToPolygon({
    id: "r1", type: "rectangle", operation: "add", override: false,
    min_x: 0, min_z: 0, max_x: 4, max_z: 4, base_height: 12, floor: 3,
  });
  assert.equal(poly.base_height, 12);
  assert.equal(poly.floor, 3);
});

test("rectToPolygon omits height fields that were never set", () => {
  const poly = rectToPolygon({ id: "r1", type: "rectangle", operation: "add", min_x: 0, min_z: 0, max_x: 4, max_z: 4 });
  assert.equal("base_height" in poly, false);
  assert.equal("floor" in poly, false);
  assert.equal("anchor_heights" in poly, false);
});

// ── translateShape ────────────────────────────────────────────────────────────
test("translateShape moves rectangle / circle / polygon coords", () => {
  assert.deepEqual(translateShape({ type: "rectangle", min_x: 0, min_z: 0, max_x: 4, max_z: 4 }, 5, -2),
    { type: "rectangle", min_x: 5, min_z: -2, max_x: 9, max_z: 2 });
  assert.deepEqual(translateShape({ type: "circle", center_x: 1, center_z: 1, radius: 3 }, 5, -2),
    { type: "circle", center_x: 6, center_z: -1, radius: 3 });
  assert.deepEqual(translateShape({ type: "polygon", vertices: [[0, 0], [4, 0], [2, 5]] }, 5, -2).vertices,
    [[5, -2], [9, -2], [7, 3]]);
});

test("translateShape carries Bézier control points with the vertices (curve doesn't distort)", () => {
  const shape = { type: "polygon", vertices: [[0, 0], [10, 0], [10, 10]], controls: { "0": { out: [3, 0] }, "1": { in: [7, -4], out: [12, 2] } } };
  const moved = translateShape(shape, 5, -2);
  assert.deepEqual(moved.controls, { "0": { out: [8, -2] }, "1": { in: [12, -6], out: [17, 0] } });
  // pure — the source's controls are untouched
  assert.deepEqual(shape.controls, { "0": { out: [3, 0] }, "1": { in: [7, -4], out: [12, 2] } });
});

// ── rotateShape ───────────────────────────────────────────────────────────────
test("rotateShape rotates polygon vertices + Bézier controls 90° CW about a pivot", () => {
  // 90° CW in (x, z-down): rot(x,z) = (-z, x) about origin.
  const shape = { type: "polygon", operation: "add", vertices: [[0, 0], [2, 0], [0, 2]], controls: { "1": { out: [3, 0] } } };
  const out = rotateShape(shape, Math.PI / 2, [0, 0]);
  assert.deepEqual(roundPts(out.vertices), [[0, 0], [0, 2], [-2, 0]]);
  assert.deepEqual([r6(out.controls["1"].out[0]), r6(out.controls["1"].out[1])], [0, 3]);
  assert.equal(out.operation, "add");
});

test("rotateShape orbits a circle's centre and keeps its radius", () => {
  const out = rotateShape({ type: "circle", center_x: 2, center_z: 0, radius: 5 }, Math.PI / 2, [0, 0]);
  assert.deepEqual([r6(out.center_x), r6(out.center_z)], [0, 2]);
  assert.equal(out.radius, 5);
});

test("rotateShape promotes a rectangle to a polygon, carrying its height fields", () => {
  const out = rotateShape({ type: "rectangle", id: "r", operation: "add", min_x: 0, min_z: 0, max_x: 4, max_z: 2, base_height: 12, floor: 3 }, Math.PI / 2, [0, 0]);
  assert.equal(out.type, "polygon");
  assert.equal(out.base_height, 12);
  assert.equal(out.floor, 3);
  assert.equal(out.vertices.length, 4);
});

test("rotateShape is pure (source vertices/controls untouched)", () => {
  const shape = { type: "polygon", vertices: [[1, 1]], controls: { "0": { out: [2, 2] } } };
  rotateShape(shape, Math.PI / 3, [0, 0]);
  assert.deepEqual(shape.vertices, [[1, 1]]);
  assert.deepEqual(shape.controls, { "0": { out: [2, 2] } });
});

// ── scaleShape ────────────────────────────────────────────────────────────────
test("scaleShape scales polygon vertices + Bézier controls about an anchor", () => {
  const shape = { type: "polygon", operation: "add", vertices: [[0, 0], [2, 0], [0, 2]], controls: { "1": { out: [3, 0] } } };
  const out = scaleShape(shape, 2, 3, [0, 0]);
  assert.deepEqual(out.vertices, [[0, 0], [4, 0], [0, 6]]);
  assert.deepEqual(out.controls["1"].out, [6, 0]);
});

test("scaleShape keeps a rectangle axis-aligned (min/max scaled + normalised)", () => {
  const out = scaleShape({ type: "rectangle", min_x: 0, min_z: 0, max_x: 4, max_z: 2 }, 2, 1, [0, 0]);
  assert.deepEqual([out.min_x, out.min_z, out.max_x, out.max_z], [0, 0, 8, 2]);
  assert.equal(out.type, "rectangle");
});

test("scaleShape keeps a circle round — centre moves, radius by the geometric mean", () => {
  const out = scaleShape({ type: "circle", center_x: 2, center_z: 0, radius: 5 }, 2, 2, [0, 0]);
  assert.deepEqual([out.center_x, out.center_z], [4, 0]);
  assert.equal(out.radius, 10);   // 5 · sqrt(2·2)
});

test("scaleShape is pure (source untouched)", () => {
  const shape = { type: "polygon", vertices: [[1, 1]], controls: { "0": { in: [2, 2] } } };
  scaleShape(shape, 2, 2, [0, 0]);
  assert.deepEqual(shape.vertices, [[1, 1]]);
  assert.deepEqual(shape.controls, { "0": { in: [2, 2] } });
});

// ── boundsOfShapes ────────────────────────────────────────────────────────────
test("boundsOfShapes unions member bounds (the island bbox)", () => {
  const b = boundsOfShapes([
    { type: "rectangle", min_x: 0, min_z: 0, max_x: 4, max_z: 4 },
    { type: "circle", center_x: 20, center_z: 10, radius: 5 },   // 15..25 x, 5..15 z
  ]);
  assert.deepEqual(b, { min_x: 0, min_z: 0, max_x: 25, max_z: 15 });
});

test("boundsOfShapes skips degenerate shapes, null when nothing has bounds", () => {
  assert.deepEqual(
    boundsOfShapes([{ type: "polygon", vertices: [] }, { type: "rectangle", min_x: 1, min_z: 2, max_x: 3, max_z: 4 }]),
    { min_x: 1, min_z: 2, max_x: 3, max_z: 4 });
  assert.equal(boundsOfShapes([{ type: "polygon", vertices: [] }]), null);
  assert.equal(boundsOfShapes([]), null);
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
