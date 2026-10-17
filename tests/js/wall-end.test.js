// A picked wall's ends dragged apart in Layout (controllers/wall-end-controller.js): an end lands on block
// corners, snaps to a drawn shape's outline within three blocks, moves alone, and hands the host the end it moved
// and the wall as it stood.
import { test } from "node:test";
import assert from "node:assert/strict";

import { END_SNAP_BLOCKS, WallEndController, nearestOnEdges, wallEndAt }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/wall-end-controller.js";

// A square island from (0, 0) to (40, 40), as the closed ring a drawn rectangle is.
const island = [[0, 0], [40, 0], [40, 40], [0, 40], [0, 0]];

test("an end within reach of an outline lands on it, and one out of reach on the corner under the pointer", () => {
  assert.equal(END_SNAP_BLOCKS, 3);
  assert.deepEqual(wallEndAt(20.4, 42.2, [island]), { x: 20, z: 40, snapped: true });
  assert.deepEqual(wallEndAt(20.4, 44.6, [island]), { x: 20, z: 45, snapped: false });
  assert.equal(nearestOnEdges(20, 20, [island], 3), null);
});

function controller(structural, edited) {
  const tools = new WallEndController(null, () => ({ scale: 1, panX: 0, panY: 0 }), () => structural, () => [island], {
    onEdited: (piece, part, x, z, putBack) => edited.push({ id: piece.id, part, x, z, putBack: putBack.line }),
  });
  tools.setEnabled(true);
  tools.refresh("wall-1");
  return tools;
}

const walls = () => [{ id: "wall-1", role: "wall", piece: true, line: [20, 10, 20, 30], min_x: 19, min_z: 10, max_x: 21, max_z: 30 }];

test("dragging the second end moves it alone and hands the host where it landed", () => {
  const edited = [];
  const structural = walls();
  const tools = controller(structural, edited);
  tools.grab(true);
  assert.equal(tools.onHandleMove(24.3, 38.6), true);
  assert.deepEqual(structural[0].line, [20, 10, 24, 40], "the end snaps onto the island's south edge");
  assert.equal(tools.onHandleUp(), true);
  assert.deepEqual(edited, [{ id: "wall-1", part: "end", x: 24, z: 40, putBack: [20, 10, 20, 30] }]);
});

test("the first end is the start, and an end never lands on the other", () => {
  const edited = [];
  const structural = walls();
  const tools = controller(structural, edited);
  tools.grab(false);
  tools.onHandleMove(20.2, 29.8);
  assert.deepEqual(structural[0].line, [20, 10, 20, 30], "two ends on one corner is no wall");
  tools.onHandleMove(12, 1.5);
  tools.onHandleUp();
  assert.deepEqual(edited, [{ id: "wall-1", part: "start", x: 12, z: 0, putBack: [20, 10, 20, 30] }]);
});

test("a release that moved nothing hands nothing, and nothing is grabbed while editing is off", () => {
  const edited = [];
  const tools = controller(walls(), edited);
  tools.grab(true);
  tools.onHandleUp();
  tools.setEnabled(false);
  tools.grab(true);
  assert.equal(tools.onHandleMove(30, 30), false);
  assert.deepEqual(edited, []);
});
