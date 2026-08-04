// The band a drawn line stands for (geometry/path.js) — and, above all, that it is the same band the C#
// side builds. The two implementations exist because the canvas redraws a path's outline on every pointer
// move and a round trip per frame is not a preview; the moment they disagree, the route an author drags
// stops matching the route the map paves. So the parity numbers below are pinned against the C# constants
// and the lattice hash is checked bit for bit.
import { test } from "node:test";
import assert from "node:assert/strict";

import { pathRing, pathCenterline, valueNoise, SMOOTH_SAMPLES }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/path.js";

const line = (radius, extra = {}) => ({ points: [[0, 0], [40, 0]], radius, ...extra });
const bent = (extra = {}) => ({ points: [[0, 0], [20, 0], [30, 20], [50, 24]], radius: 5, ...extra });

// ── the band ──────────────────────────────────────────────────────────────────
test("a straight line becomes a closed band of the width asked for", () => {
  const ring = pathRing(line(4));
  assert.ok(ring.length > 3);
  assert.deepEqual(ring[0], ring[ring.length - 1]);          // closed, the toRing convention
  assert.equal(Math.min(...ring.map(([, z]) => z)), -4);
  assert.equal(Math.max(...ring.map(([, z]) => z)), 4);
  assert.equal(Math.min(...ring.map(([x]) => x)), 0);        // square ends, not round caps
  assert.equal(Math.max(...ring.map(([x]) => x)), 40);
});

test("a line with nowhere to go is no band", () => {
  assert.deepEqual(pathRing({ points: [[5, 5]], radius: 3 }), []);
  assert.deepEqual(pathRing(line(0)), []);
});

test("two drawn points are densified along the line they drew", () => {
  // No curve to sample, but a varying width needs somewhere to vary — and the endpoints must not move.
  const curve = pathCenterline([[0, 0], [40, 0]]);
  assert.equal(curve.length, SMOOTH_SAMPLES + 1);
  assert.deepEqual(curve[0], [0, 0]);
  assert.deepEqual(curve[curve.length - 1], [40, 0]);
  assert.ok(curve.every(([, z]) => z === 0));
});

test("three drawn points smooth into SMOOTH_SAMPLES per segment", () => {
  const curve = pathCenterline([[0, 0], [20, 0], [30, 20]]);
  assert.equal(curve.length, 2 * SMOOTH_SAMPLES + 1);        // two segments sampled, plus the final point
});

// ── edges ─────────────────────────────────────────────────────────────────────
const widthAcross = (ring, x) => {
  const at = ring.filter(([px]) => Math.abs(px - x) < 0.5).map(([, z]) => z);
  return at.length < 2 ? 0 : Math.max(...at) - Math.min(...at);
};

test("a tapered path is widest in the middle and thinnest at its ends", () => {
  const ring = pathRing(line(6, { style: "tapered" }));
  assert.ok(widthAcross(ring, 20) > widthAcross(ring, 2));
  assert.ok(widthAcross(ring, 20) > widthAcross(ring, 38));
  assert.ok(Math.abs(widthAcross(ring, 20) - 12) < 0.2);
});

test("a rough edge wanders, and the same seed always wanders the same way", () => {
  const rough = pathRing(line(5, { style: "rough", seed: 11 }));
  const again = pathRing(line(5, { style: "rough", seed: 11 }));
  const other = pathRing(line(5, { style: "rough", seed: 12 }));
  assert.deepEqual(rough, again);
  assert.notDeepEqual(rough, other);

  const widths = [];
  for (let x = 2; x < 38; x++) { const w = widthAcross(rough, x); if (w > 0) widths.push(w); }
  assert.ok(new Set(widths).size > 5, "the width should actually vary");
  const mean = widths.reduce((a, b) => a + b, 0) / widths.length;
  assert.ok(Math.abs(mean - 10) < 2, `wandered away from the width asked for (mean ${mean})`);
});

test("a bend does not pinch the band", () => {
  const ring = pathRing(bent());
  for (const [px, pz] of pathCenterline(bent().points)) {
    const nearest = Math.min(...ring.map(([x, z]) => Math.hypot(x - px, z - pz)));
    assert.ok(nearest > 4, `pinched to ${nearest} at ${px},${pz}`);
  }
});

// ── parity with C# ────────────────────────────────────────────────────────────
// PatternNoise's lattice hash is 32-bit unsigned arithmetic, which JS numbers do not do by accident:
// without Math.imul the multiplies lose their low bits and the field silently becomes a different one.
// These are values taken from the C# implementation.
test("the lattice hash matches Geom.PatternNoise bit for bit", () => {
  const cases = [
    [0, 0, 0, 4], [7, 0, 11, 8], [-3, 512, 0, 8], [1000, -1000, 42, 16],
  ];
  const expected = [
    0.6066385507583618, 0.8794001552741975, 0.3443915860261768, 0.2714862674474716,
  ];
  cases.forEach(([x, z, seed, scale], i) => {
    assert.ok(Math.abs(valueNoise(x, z, seed, scale) - expected[i]) < 1e-12,
      `valueNoise(${x},${z},${seed},${scale}) = ${valueNoise(x, z, seed, scale)}, expected ${expected[i]}`);
  });
});

test("SMOOTH_SAMPLES matches the C# parity constant", () => {
  assert.equal(SMOOTH_SAMPLES, 8);
});
