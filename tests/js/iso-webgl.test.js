import { test } from "node:test";
import assert from "node:assert/strict";
import { cameraBasis, groundShift, nextZoom, anchorShift } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/iso-webgl.js";

// The 3-D preview's camera: a zoom of the fitted board and a pan of the point it looks at, along the ground.

const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const near = (a, b) => Math.abs(a - b) < 1e-9;

test("a ground shift moves the picture by exactly the screen offset asked, at every quarter turn", () => {
  for (const yaw of [30, 120, 210, 300]) {
    const { s, u } = cameraBasis(yaw);
    for (const [dx, dy] of [[5, 0], [0, 5], [-3.5, 7.25]]) {
      const shift = groundShift(dx, dy, yaw);
      assert.ok(near(dot(shift, s), dx), `yaw ${yaw}: across ${dot(shift, s)} for ${dx}`);
      assert.ok(near(dot(shift, u), dy), `yaw ${yaw}: up ${dot(shift, u)} for ${dy}`);
    }
  }
});

test("a ground shift stays on the ground", () => {
  for (const yaw of [30, 120, 210, 300]) assert.equal(groundShift(4, -9, yaw)[1], 0);
});

test("a screen-up move is a longer step across the ground than the same screen-right move", () => {
  const right = Math.hypot(...groundShift(1, 0, 30));
  const up = Math.hypot(...groundShift(0, 1, 30));
  assert.ok(near(right, 1));
  assert.ok(up > right, `up ${up} against right ${right}`);
});

test("the point under the pointer stays under it as the zoom changes", () => {
  for (const [zoom, next, offset] of [[1, 1.15, 12], [2, 4, -30], [6, 3, 8.5]]) {
    // The pointer sits `offset` camera units from the middle at `zoom`, so `offset / next * zoom` at the next.
    const after = offset * zoom / next;
    assert.ok(near(anchorShift(offset, zoom, next) + after, offset), `${zoom} → ${next} at ${offset}`);
  }
});

test("the pointer in the middle of the picture moves nothing", () => {
  assert.equal(anchorShift(0, 1, 2), 0);
});

test("the zoom stays between the whole board and close enough to count blocks", () => {
  assert.equal(nextZoom(1, 1 / 1.15), 1);
  assert.equal(nextZoom(39, 2), 40);
  assert.equal(nextZoom(2, 1.5), 3);
});
