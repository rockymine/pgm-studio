// The point edits every outline on the canvas takes (geometry/outline-points.js), and the three owners that ask
// them — a modifier's outline, a dressing route or area, an approach wall: a press on an edge's middle adds a point
// there, a press that does not move picks a point, and the delete key takes the picked one out while the outline
// keeps enough to stay one.
import { test } from "node:test";
import assert from "node:assert/strict";

import { leastPoints, edgeCount, edgeNear, withMidpoint, withoutPoint }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/outline-points.js";
import { ModifierController }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/modifier-controller.js";
import { newModifier } from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";
import { DressingController }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/dressing-controller.js";
import { DressingDoc } from "../../src/PgmStudio.Client/wwwroot/js/studio/dressing/dressing-doc.js";
import { WallCornerController }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/wall-corner-controller.js";

const VIEW = { scale: 1, panX: 0, panY: 0 };
const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];
const RUN = [[0, 0], [10, 0], [10, 10]];

test("a closed ring has an edge back to its first point, and an open run does not", () => {
  assert.equal(edgeCount(SQUARE, true), 4);
  assert.equal(edgeCount(RUN, false), 2);
  assert.equal(edgeCount([[0, 0]], false), 0);
  assert.deepEqual(withMidpoint(SQUARE, 3, true), { points: [[0, 0], [10, 0], [10, 10], [0, 10], [0, 5]], index: 4 });
  assert.equal(withMidpoint(RUN, 2, false), null, "the last point of a run has no edge after it");
});

test("the edge near the pointer is found within reach, and its ghost stands at the edge's middle", () => {
  assert.deepEqual(edgeNear(SQUARE, true, { x: 4, y: 2 }, VIEW, 5), { edge: 0, x: 5, y: 0 });
  assert.deepEqual(edgeNear(SQUARE, true, { x: 2, y: 6 }, VIEW, 5), { edge: 3, x: 0, y: 5 });
  assert.equal(edgeNear(RUN, false, { x: 2, y: 6 }, VIEW, 5), null, "a run has no closing edge to be near");
  assert.equal(edgeNear(SQUARE, true, { x: 5, y: 5 }, VIEW, 3), null);
});

test("a point comes out only while the outline keeps three for a ring and two for a run", () => {
  assert.equal(leastPoints(true), 3);
  assert.equal(leastPoints(false), 2);
  assert.deepEqual(withoutPoint(SQUARE, 1, true), [[0, 0], [10, 10], [0, 10]]);
  assert.equal(withoutPoint(SQUARE.slice(0, 3), 0, true), null);
  assert.deepEqual(withoutPoint(RUN, 2, false), [[0, 0], [10, 0]]);
  assert.equal(withoutPoint([[0, 0], [10, 0]], 0, false), null);
});

test("a modifier's outline: a still press picks a point, delete takes it out, and a ring keeps three", () => {
  const stack = { list: [newModifier([], "mound", SQUARE)] };
  const tools = new ModifierController(null, null, {
    getModifiers: () => stack.list, onChanged: (next) => { stack.list = next; }, onPreview: (next) => { stack.list = next; },
  });
  tools.select("mound", 0);
  assert.equal(tools.selectedPoint, -1);
  assert.ok(tools.beginPointDrag(2));
  tools.onHandleUp();
  assert.equal(tools.selectedPoint, 2);
  assert.ok(tools.deleteSelectedPoint());
  assert.deepEqual(stack.list[0].mask.areas[0].points, [[0, 0], [10, 0], [0, 10]]);
  assert.equal(tools.selectedPoint, -1);
  tools.beginPointDrag(0);
  tools.onHandleUp();
  assert.equal(tools.deleteSelectedPoint(), false, "a ring of three keeps its three");
});

test("a dressing route: a point goes in at an edge's middle, is picked, and comes out while two stay", () => {
  const doc = new DressingDoc();
  const tools = new DressingController(doc, null, null, {});
  doc.add({ kind: "stroke", points: [[0, 0], [20, 0]] });
  const id = doc.props[0].id;
  tools.select(id);
  assert.ok(tools.insertPoint(0));
  tools.onHandleUp();
  assert.deepEqual(doc.byId(id).points, [[0, 0], [10, 0], [20, 0]]);
  assert.equal(tools.selectedPoint, 1, "the point a press adds is the one picked");
  assert.ok(tools.deleteSelectedPoint());
  assert.deepEqual(doc.byId(id).points, [[0, 0], [20, 0]]);
  tools.beginPointDrag(id, 0);
  tools.onHandleUp();
  assert.equal(tools.selectedPoint, 0);
  assert.equal(tools.deleteSelectedPoint(), false, "a route keeps two");
});

test("an approach wall: a corner goes in at a leg's middle, a still press picks one, and delete keeps two", () => {
  const structural = [{ id: "wall-1", role: "wall", piece: true, line: [[20, 10], [20, 30], [30, 30]], min_x: 19, min_z: 10, max_x: 31, max_z: 31 }];
  const edited = [];
  const tools = new WallCornerController(null, () => VIEW, () => structural, () => [], {
    onEdited: (piece, points) => edited.push(points),
  });
  tools.setEnabled(true);
  tools.refresh("wall-1");
  assert.ok(tools.insertCorner(1));
  tools.onHandleUp();
  assert.deepEqual(structural[0].line, [[20, 10], [20, 30], [25, 30], [30, 30]]);
  assert.equal(tools.selectedCorner, 2);
  assert.ok(tools.removeSelected());
  assert.deepEqual(structural[0].line, [[20, 10], [20, 30], [30, 30]]);
  tools.grab(0);
  tools.onHandleUp();
  assert.equal(tools.selectedCorner, 0);
  assert.ok(tools.removeSelected());
  tools.grab(0);
  tools.onHandleUp();
  assert.equal(tools.removeSelected(), false, "a wall keeps two corners");
  assert.equal(edited.length, 3);
});
