// A picked room region, build area or water lane resized on the Layout canvas
// (controllers/piece-resize-controller.js): which pieces take the box, and where a grip drags the rectangle to.
import { test } from "node:test";
import assert from "node:assert/strict";

import { PieceResizeController, resizable, resizedRect, MIN_ROOM_SPAN }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/piece-resize-controller.js";
import { TRANSFORM_CORNERS, TRANSFORM_EDGES }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/canvas-chrome.js";

const grip = (key) => [...TRANSFORM_CORNERS, ...TRANSFORM_EDGES].find(each => each.key === key);
const rect = { min_x: 0, min_z: 0, max_x: 10, max_z: 10 };

test("a room's region, a build area and a water lane resize, and an image or anything else does not", () => {
  assert.ok(resizable({ role: "spawn" }));
  assert.ok(resizable({ role: "woolRoom" }));
  assert.ok(resizable({ role: "buildArea", piece: true }));
  assert.ok(resizable({ role: "waterLane", piece: true }));
  assert.ok(!resizable({ role: "spawn", carried: true }), "an orbit's image moves with its authored one");
  assert.ok(!resizable({ role: "building" }), "a building is reshaped by its house");
  assert.ok(!resizable({ role: "wall", piece: true }));
  assert.ok(!resizable({ role: "destroyable", piece: true }));
});

test("an edge drags its side alone and a corner both, by whole blocks, the opposite side held", () => {
  assert.deepEqual(resizedRect(rect, grip("e"), 14.4, 99), { min_x: 0, min_z: 0, max_x: 14, max_z: 10 });
  assert.deepEqual(resizedRect(rect, grip("n"), 99, -3.6), { min_x: 0, min_z: -4, max_x: 10, max_z: 10 });
  assert.deepEqual(resizedRect(rect, grip("sw"), -2, 12), { min_x: -2, min_z: 0, max_x: 10, max_z: 12 });
});

test("a side dragged past the one it faces stops the least span short of it", () => {
  assert.deepEqual(resizedRect(rect, grip("e"), -20, 0, MIN_ROOM_SPAN), { min_x: 0, min_z: 0, max_x: 4, max_z: 10 });
  assert.deepEqual(resizedRect(rect, grip("w"), 30, 0), { min_x: 9, min_z: 0, max_x: 10, max_z: 10 });
});

test("a drag that moved hands the resized region to the host with the region as it stood", () => {
  const structural = [{ id: "spawn-red", role: "spawn", ...rect }];
  const resized = [];
  const tools = new PieceResizeController(null, () => ({ scale: 1, panX: 0, panY: 0 }), () => structural, {
    onResized: (piece, putBack) => resized.push({ piece: { ...piece }, putBack }),
  });
  tools.setEnabled(true);
  tools.refresh("spawn-red");
  assert.ok(!tools.onHandleMove(5, 5), "nothing is held before a grip is pressed");
  tools.grab(grip("se"));
  assert.ok(tools.onHandleMove(16.2, 7.8));
  assert.ok(tools.onHandleUp());
  assert.deepEqual(resized, [{ piece: { id: "spawn-red", role: "spawn", min_x: 0, min_z: 0, max_x: 16, max_z: 8 },
                               putBack: { id: "spawn-red", role: "spawn", ...rect } }]);
});

test("a press that never moved hands nothing to the host", () => {
  const structural = [{ id: "build-1", role: "buildArea", piece: true, ...rect }];
  const resized = [];
  const tools = new PieceResizeController(null, () => ({ scale: 1, panX: 0, panY: 0 }), () => structural, {
    onResized: (piece) => resized.push(piece),
  });
  tools.setEnabled(true);
  tools.refresh("build-1");
  tools.grab(grip("n"));
  tools.onHandleUp();
  assert.deepEqual(resized, []);
});
