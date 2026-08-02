// Parity tests for the client polygon simplifier (pure, no DOM). Mirrors C# PgmStudio.Geom.PolygonSimplify
// / DouglasPeucker — the lasso freehand trace is Douglas–Peucker'd on release so it stores a handful of
// anchors, not one per block of travel.
import { test } from "node:test";
import assert from "node:assert/strict";

import { douglasPeucker, simplifyRing } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/simplify.js";

// ── douglasPeucker (open polyline) ───────────────────────────────────────────────
test("douglasPeucker keeps a straight run's endpoints only", () => {
  const line = [[0, 0], [1, 0], [2, 0], [3, 0], [4, 0]];
  assert.deepEqual(douglasPeucker(line, 1), [[0, 0], [4, 0]]);
});

test("douglasPeucker keeps a real bend beyond tolerance", () => {
  const bent = [[0, 0], [2, 5], [4, 0]];   // apex 5 off the chord
  assert.deepEqual(douglasPeucker(bent, 1), [[0, 0], [2, 5], [4, 0]]);
});

test("douglasPeucker drops a wobble within tolerance", () => {
  const wobble = [[0, 0], [2, 1], [4, 0]];   // apex only 1 off — under tol 2
  assert.deepEqual(douglasPeucker(wobble, 2), [[0, 0], [4, 0]]);
});

test("douglasPeucker passes fewer than 3 points through", () => {
  assert.deepEqual(douglasPeucker([[0, 0], [9, 9]], 1), [[0, 0], [9, 9]]);
});

// ── simplifyRing (closed ring — the lasso case) ──────────────────────────────────
test("simplifyRing collapses a staircased square back to its 4 corners", () => {
  // A 0..4 square traced as unit block steps along every edge — the dense lasso trace.
  const trace = [];
  for (let x = 0; x <= 4; x++) trace.push([x, 0]);
  for (let z = 1; z <= 4; z++) trace.push([4, z]);
  for (let x = 3; x >= 0; x--) trace.push([x, 4]);
  for (let z = 3; z >= 1; z--) trace.push([0, z]);
  const simplified = simplifyRing(trace, 1);
  // Corner set is exactly the four square corners (order/rotation not asserted).
  const asSet = new Set(simplified.map(p => `${p[0]},${p[1]}`));
  assert.equal(simplified.length, 4);
  for (const c of ["0,0", "4,0", "4,4", "0,4"]) assert.ok(asSet.has(c), `missing corner ${c}`);
});

test("simplifyRing turns a dense freehand blob into far fewer anchors, keeping the extent", () => {
  // ~120-point wobbly circle trace (integer block steps) → a handful of vertices.
  const trace = [];
  for (let i = 0; i < 120; i++) {
    const a = (2 * Math.PI * i) / 120;
    trace.push([Math.round(30 * Math.cos(a)), Math.round(30 * Math.sin(a))]);
  }
  const simplified = simplifyRing(trace, 2);
  assert.ok(simplified.length >= 6, "keeps enough to read as round");
  assert.ok(simplified.length < 30, `expected far fewer than the trace, got ${simplified.length}`);
  // Still spans the full circle (extent preserved within tolerance).
  const xs = simplified.map(p => p[0]);
  assert.ok(Math.max(...xs) >= 28 && Math.min(...xs) <= -28);
});

test("simplifyRing passes a triangle (≤3 points) through unchanged", () => {
  assert.deepEqual(simplifyRing([[0, 0], [4, 0], [2, 4]], 2), [[0, 0], [4, 0], [2, 4]]);
});

test("simplifyRing output stays block-integer (only keeps existing trace points)", () => {
  const trace = [];
  for (let i = 0; i < 40; i++) { const a = (2 * Math.PI * i) / 40; trace.push([Math.round(10 * Math.cos(a)), Math.round(10 * Math.sin(a))]); }
  for (const [x, z] of simplifyRing(trace, 2)) {
    assert.equal(x, Math.round(x));
    assert.equal(z, Math.round(z));
  }
});
