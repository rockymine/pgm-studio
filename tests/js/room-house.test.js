// A room's house shaped in Layout (controllers/room-house-controller.js): which room and wing a building shape
// is, and the wing a click on a free wall adds — five deep, shrunk to the room's region, refused where none fits.
import { test } from "node:test";
import assert from "node:assert/strict";

import { RoomHouseController, wingOf }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/room-house-controller.js";

// A spawn region 0..20 square with its room's wing at 2..12 × 2..18, max-exclusive as the canvas holds them.
const shapes = () => [
  { id: "spawn-red", role: "spawn", min_x: 0, min_z: 0, max_x: 20, max_z: 20 },
  { id: "spawn-red-building", role: "building", min_x: 2, min_z: 2, max_x: 12, max_z: 18 },
];

function controller(structural, edited) {
  const tools = new RoomHouseController(null, () => ({ scale: 1, panX: 0, panY: 0 }), () => structural, {
    onEdited: (room, wings) => edited.push({ room, wings }),
    onRefused: (sentence) => edited.push({ refused: sentence }),
  });
  tools.setEnabled(true);
  tools.refresh("spawn-red-building");
  return tools;
}

test("a building shape names its room and which wing of the house it is", () => {
  assert.deepEqual(wingOf("spawn-red-building"), { room: "spawn-red", wing: 0 });
  assert.deepEqual(wingOf("wool-lime-building-3"), { room: "wool-lime", wing: 2 });
  assert.equal(wingOf("spawn-red"), null);
});

test("a click on the middle of a free wall adds a wing five deep along it, the room's first", () => {
  const edited = [];
  const tools = controller(shapes(), edited);
  tools.onPointerHover(12, 10);          // the east wall, x = 12
  tools.addWing();
  assert.equal(edited.length, 1);
  assert.equal(edited[0].room, "spawn-red");
  assert.deepEqual(edited[0].wings[0], { min_x: 2, min_z: 2, max_x: 12, max_z: 18 });
  assert.deepEqual(edited[0].wings[1], { min_x: 12, min_z: 2, max_x: 17, max_z: 18 });
});

test("a wing is shrunk to the room's region, and refused where less than a wing fits", () => {
  const edited = [];
  const near = shapes();
  near[1] = { ...near[1], max_x: 17 };   // three blocks between the east wall and the region's edge
  const tools = controller(near, edited);
  tools.onPointerHover(17, 10);
  tools.addWing();
  assert.deepEqual(edited, [{ refused: "There is no room for a wing off that wall." }]);

  const roomy = shapes();
  roomy[1] = { ...roomy[1], max_x: 14 };  // six blocks: the wing takes five
  const fits = [];
  const wide = controller(roomy, fits);
  wide.onPointerHover(14, 10);
  wide.addWing();
  assert.deepEqual(fits[0].wings[1], { min_x: 14, min_z: 2, max_x: 19, max_z: 18 });
});

test("nothing is offered while house editing is off", () => {
  const edited = [];
  const tools = controller(shapes(), edited);
  tools.setEnabled(false);
  tools.onPointerHover(12, 10);
  tools.addWing();
  assert.deepEqual(edited, []);
});
