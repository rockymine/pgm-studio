// The plan's own pieces (S25) on the sketch canvas: which one a point lands on, and what the paint draws
// round the one that was picked. Both are what `B107` turns from render-only context into something an
// author can select and correct a height on.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { containsPoint } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/shape.js";
import { paintStructural } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/sketch-render.js";

const region = {
  id: "wool-red", type: "rectangle", role: "woolRoom", color: "red",
  min_x: 0, min_z: 0, max_x: 30, max_z: 30, base_height: 12,
};
const building = {
  id: "wool-red-building", type: "rectangle", role: "building", color: "red",
  min_x: 10, min_z: 10, max_x: 20, max_z: 20,
};

// The canvas walks its structural list back to front, so the last containing piece wins. A building is
// appended after the region it stands in, which is what makes the inner rectangle reachable at all.
const hitStructural = (shapes, x, z) => {
  for (let i = shapes.length - 1; i >= 0; i--) if (containsPoint(shapes[i], x, z)) return shapes[i].id;
  return null;
};

test("a point inside a region picks it", () => {
  assert.equal(hitStructural([region, building], 3, 3), "wool-red");
});

test("a building inside a region is reached before the region it stands in", () => {
  assert.equal(hitStructural([region, building], 15, 15), "wool-red-building");
});

test("a point outside every piece picks none", () => {
  assert.equal(hitStructural([region, building], 40, 40), null);
});

test("an unpicked board draws no selection ring", () => {
  const painter = recordingPainter();
  paintStructural(painter, [region, building]);
  // One filled box for the region and one outline for the building, and nothing else.
  assert.equal(painter.of("rect").length, 2);
});

test("the picked piece gets a ring, and only it", () => {
  const painter = recordingPainter();
  paintStructural(painter, [region, building], "wool-red");

  const rects = painter.of("rect");
  assert.equal(rects.length, 3);                       // the ring, then the two pieces
  const ring = rects[0][rects[0].length - 1];
  assert.equal(ring.stroke, "var(--accent)");
  assert.equal(ring.width, 4);
});

test("a building can be the picked one too", () => {
  const painter = recordingPainter();
  paintStructural(painter, [region, building], "wool-red-building");

  const rings = painter.of("rect").filter(r => r[r.length - 1].width === 4);
  assert.equal(rings.length, 1);
  assert.deepEqual(rings[0][0], { min_x: 10, min_z: 10, max_x: 20, max_z: 20 });
});
