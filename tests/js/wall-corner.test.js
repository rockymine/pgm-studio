// A picked wall's corners in Layout (controllers/wall-corner-controller.js): a corner lands on block corners, snaps
// to a drawn shape's outline within three blocks and moves alone; a shift-press adds a corner on a leg or takes one
// out while two stay; every change hands the host the whole line and the wall as it stood.
import { test } from "node:test";
import assert from "node:assert/strict";

import { END_SNAP_BLOCKS, WallCornerController, nearestOnEdges, wallEndAt, withCorner }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/wall-corner-controller.js";

// A square island from (0, 0) to (40, 40), as the closed ring a drawn rectangle is.
const island = [[0, 0], [40, 0], [40, 40], [0, 40], [0, 0]];

test("a corner within reach of an outline lands on it, and one out of reach on the block corner under the pointer", () => {
  assert.equal(END_SNAP_BLOCKS, 3);
  assert.deepEqual(wallEndAt(20.4, 42.2, [island]), { x: 20, z: 40, snapped: true });
  assert.deepEqual(wallEndAt(20.4, 44.6, [island]), { x: 20, z: 45, snapped: false });
  assert.equal(nearestOnEdges(20, 20, [island], 3), null);
});

test("a corner goes into the leg a press lands beside, on the leg, and nowhere off the line", () => {
  assert.deepEqual(withCorner([[20, 10], [20, 30], [30, 30]], 20.6, 18.2), [[20, 10], [20, 18], [20, 30], [30, 30]]);
  assert.deepEqual(withCorner([[20, 10], [20, 30], [30, 30]], 24.2, 29.4), [[20, 10], [20, 30], [24, 30], [30, 30]]);
  assert.equal(withCorner([[20, 10], [20, 30]], 26, 18), null);
});

function controller(structural, edited) {
  const tools = new WallCornerController(null, () => ({ scale: 1, panX: 0, panY: 0 }), () => structural, () => [island], {
    onEdited: (piece, points, putBack) => edited.push({ id: piece.id, points, putBack: putBack.line }),
  });
  tools.setEnabled(true);
  tools.refresh("wall-1");
  return tools;
}

const walls = () => [{ id: "wall-1", role: "wall", piece: true, line: [[20, 10], [20, 30]], min_x: 19, min_z: 10, max_x: 21, max_z: 30 }];

test("dragging the last corner moves it alone and hands the host the whole line", () => {
  const edited = [];
  const structural = walls();
  const tools = controller(structural, edited);
  tools.grab(1);
  assert.equal(tools.onHandleMove(24.3, 38.6), true);
  assert.deepEqual(structural[0].line, [[20, 10], [24, 40]], "the corner snaps onto the island's south edge");
  assert.equal(tools.onHandleUp(), true);
  assert.deepEqual(edited, [{ id: "wall-1", points: [[20, 10], [24, 40]], putBack: [[20, 10], [20, 30]] }]);
});

test("a corner never lands on the one beside it", () => {
  const edited = [];
  const structural = walls();
  const tools = controller(structural, edited);
  tools.grab(0);
  tools.onHandleMove(20.2, 29.8);
  assert.deepEqual(structural[0].line, [[20, 10], [20, 30]], "two corners in a row on one point is no leg");
  tools.onHandleMove(12, 1.5);
  tools.onHandleUp();
  assert.deepEqual(edited, [{ id: "wall-1", points: [[12, 0], [20, 30]], putBack: [[20, 10], [20, 30]] }]);
});

test("a corner added on a leg is handed over at once, and a corner is taken out only while two stay", () => {
  const edited = [];
  const structural = walls();
  const tools = controller(structural, edited);
  assert.equal(tools.addAt(20.4, 20.2), true);
  assert.deepEqual(structural[0].line, [[20, 10], [20, 20], [20, 30]]);
  assert.equal(tools.remove(1), true);
  assert.deepEqual(structural[0].line, [[20, 10], [20, 30]]);
  assert.equal(tools.remove(0), false, "two corners are the least a wall has");
  assert.equal(edited.length, 2);
});

test("a release that moved nothing hands nothing, and nothing is grabbed while editing is off", () => {
  const edited = [];
  const tools = controller(walls(), edited);
  tools.grab(1);
  tools.onHandleUp();
  tools.setEnabled(false);
  tools.grab(1);
  assert.equal(tools.onHandleMove(30, 30), false);
  assert.deepEqual(edited, []);
});
