// The symmetry overlay's geometry. It is extracted from the two dialects that draw it — the painted world
// canvas and the retained Configure preview — precisely so the type→lines rule cannot come to differ
// between them, which is what these assertions pin down.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { symmetryAxes, paintSymmetryOverlay }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/symmetry-render.js";

const bbox = { min_x: 0, min_z: 0, max_x: 100, max_z: 60 };

test("a mirror draws the one axis it mirrors about", () => {
  const [vertical] = symmetryAxes("mirror_x", 50, 30, bbox);
  assert.deepEqual(vertical, { x1: 50, z1: -10, x2: 50, z2: 70 });
  assert.equal(symmetryAxes("mirror_x", 50, 30, bbox).length, 1);

  const [horizontal] = symmetryAxes("mirror_z", 50, 30, bbox);
  assert.deepEqual(horizontal, { x1: -10, z1: 30, x2: 110, z2: 30 });
});

test("a rotation draws what it turns about: one axis for 180°, both for 90°", () => {
  assert.equal(symmetryAxes("rot_180", 50, 30, bbox).length, 1);
  assert.equal(symmetryAxes("rot_90", 50, 30, bbox).length, 2);
});

test("a diagonal runs corner to corner through the centre, past the bbox on both sides", () => {
  const [d1] = symmetryAxes("mirror_d1", 50, 30, bbox);
  const span = Math.max(100, 60) + 20;
  assert.deepEqual(d1, { x1: 50 - span, z1: 30 - span, x2: 50 + span, z2: 30 + span });
  const [d2] = symmetryAxes("mirror_d2", 50, 30, bbox);
  assert.deepEqual(d2, { x1: 50 - span, z1: 30 + span, x2: 50 + span, z2: 30 - span });
});

test("no type and no bbox each mean nothing to draw", () => {
  assert.deepEqual(symmetryAxes(null, 0, 0, bbox), []);
  assert.deepEqual(symmetryAxes("rot_90", 0, 0, null), []);
});

test("the painted overlay is the axes in one path plus a centre dot", () => {
  const painter = recordingPainter();
  paintSymmetryOverlay(painter, "rot_90", 50, 30, bbox);
  const [runs] = painter.of("segments")[0];
  assert.equal(runs.length, 2);
  const [cx, cz] = painter.of("dot")[0];
  assert.deepEqual([cx, cz], [50, 30]);
});

test("nothing to draw draws nothing — not an empty path and a stray dot", () => {
  const painter = recordingPainter();
  paintSymmetryOverlay(painter, null, 50, 30, bbox);
  assert.deepEqual(painter.calls, []);
});
