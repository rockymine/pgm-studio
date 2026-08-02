// Surface-angle (slope) plane fitting for per-vertex heights (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";

import { surfacePlane, surfaceHeights } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/slope.js";

// ── two-point ramp ───────────────────────────────────────────────────────────────
test("a two-point ramp runs linearly along the line, flat across it", () => {
  // Control: (0,0)=1 and (10,0)=11 — gradient +1/block along +x, flat in z.
  const p = surfacePlane([{ x: 0, z: 0, h: 1 }, { x: 10, z: 0, h: 11 }]);
  assert.equal(p(0, 0), 1);
  assert.equal(p(10, 0), 11);
  assert.equal(p(5, 0), 6);        // halfway along
  assert.equal(p(5, 7), 6);        // same x → same height regardless of z (contours ⟂ to the line)
});

test("surfaceHeights fills every vertex from a ramp and rounds to blocks (clean steps)", () => {
  // A 0..4 square; ramp from the left edge (h=1) to the right edge (h=5).
  const verts = [[0, 0], [4, 0], [4, 4], [0, 4], [2, 2]];
  const hs = surfaceHeights(verts, [{ x: 0, z: 0, h: 1 }, { x: 4, z: 0, h: 5 }]);
  assert.deepEqual(hs, [1, 5, 5, 1, 3]);   // left corners 1, right corners 5, centre 3 — integer steps
});

test("a ramp never drops a vertex below height 1", () => {
  const hs = surfaceHeights([[0, 0], [10, 0]], [{ x: 0, z: 0, h: 1 }, { x: 10, z: 0, h: 20 }]);
  // Extrapolating the far side stays ≥ 1; the near control holds at 1.
  assert.ok(hs.every(h => h >= 1));
});

// ── three-point plane ────────────────────────────────────────────────────────────
test("three non-collinear points define a fully-aimed plane", () => {
  // h = 2x + 3z + 1 sampled at three corners.
  const s = [{ x: 0, z: 0, h: 1 }, { x: 1, z: 0, h: 3 }, { x: 0, z: 1, h: 4 }];
  const p = surfacePlane(s);
  assert.equal(Math.round(p(0, 0)), 1);
  assert.equal(Math.round(p(2, 0)), 5);    // 2·2 + 1
  assert.equal(Math.round(p(0, 2)), 7);    // 3·2 + 1
  assert.equal(Math.round(p(2, 2)), 11);   // 2·2 + 3·2 + 1
});

test("three collinear points fall back to a ramp along the extremes", () => {
  // All on the x axis — no unique plane; ramp from (0)→(10).
  const p = surfacePlane([{ x: 0, z: 0, h: 1 }, { x: 5, z: 0, h: 6 }, { x: 10, z: 0, h: 11 }]);
  assert.equal(p(0, 0), 1);
  assert.equal(p(10, 0), 11);
  assert.equal(p(5, 3), 6);   // flat across the line
});

// ── guards ───────────────────────────────────────────────────────────────────────
test("fewer than two distinct control positions yields no plane", () => {
  assert.equal(surfacePlane([{ x: 3, z: 3, h: 5 }]), null);
  assert.equal(surfacePlane([{ x: 3, z: 3, h: 5 }, { x: 3, z: 3, h: 9 }]), null);   // same position twice
  assert.equal(surfaceHeights([[0, 0]], [{ x: 0, z: 0, h: 1 }]), null);
});
