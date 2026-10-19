// The plan's own pieces on the sketch canvas: which one a point lands on, and what the paint draws
// round the one that was picked. Together they turn a render-only annotation into something an
// author can select, correct a height on and drag.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { structuralAt } from "../../src/PgmStudio.Client/wwwroot/js/studio/shared/pick.js";
import { paintStructural } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/sketch-render.js";

const region = {
  id: "wool-red", type: "rectangle", role: "woolRoom", color: "red",
  min_x: 0, min_z: 0, max_x: 30, max_z: 30, base_height: 12,
};
const building = {
  id: "wool-red-building", type: "rectangle", role: "building", color: "red",
  min_x: 10, min_z: 10, max_x: 20, max_z: 20,
};

test("a point inside a region picks it", () => {
  assert.equal(structuralAt([region, building], 3, 3), "wool-red");
});

test("a press on a building picks its room whole first, and the wing once the room is picked", () => {
  assert.equal(structuralAt([region, building], 15, 15), "wool-red");
  assert.equal(structuralAt([region, building], 15, 15, "wool-red"), "wool-red-building");
  const second = { ...building, id: "wool-red-building-2", min_x: 20, max_x: 26 };
  assert.equal(structuralAt([region, building, second], 22, 15, "wool-red-building"), "wool-red-building-2",
    "another wing of the picked house is one press away");
});

test("a press on a room's marker picks the room first, and the marker once the room is picked", () => {
  const marker = { id: "wool-red-marker", type: "rectangle", role: "woolSpawn", min_x: 14, min_z: 14, max_x: 16, max_z: 16 };
  const board = [region, building, marker];
  assert.equal(structuralAt(board, 15, 15), "wool-red");
  assert.equal(structuralAt(board, 15, 15, "wool-red"), "wool-red-marker");
  assert.equal(structuralAt(board, 15, 15, "wool-red-building"), "wool-red-marker",
    "a wing of the same room picked reaches the marker too");
});

test("an orbit's image is never picked, and a building whose room is one picks the wing", () => {
  assert.equal(structuralAt([{ ...region, carried: true }, building], 3, 3), null);
  assert.equal(structuralAt([{ ...region, carried: true }, building], 15, 15), "wool-red-building");
});

test("a point outside every piece picks none", () => {
  assert.equal(structuralAt([region, building], 40, 40), null);
});

test("no piece carries a name on the board, picked or not", () => {
  const painter = recordingPainter();
  paintStructural(painter, [region, building], "wool-red");
  assert.equal(painter.of("text").length, 0);
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
