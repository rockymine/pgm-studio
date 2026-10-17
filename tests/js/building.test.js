// The law about a building's wings both authoring canvases apply in the pointer (shared/building.js): which
// runs of a house's walls are free to grow a wing from, which one a pointer is near, and the wing a click adds.
import { test } from "node:test";
import assert from "node:assert/strict";

import { freeWalls, MIN_FOOTPRINT_SPAN, NEW_WING_DEPTH, wallNear, wingOffWall }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/shared/building.js";

const room = { minX: 0, minZ: 0, maxX: 9, maxZ: 11 };

test("a lone wing has a free wall on every side, each marked at the middle of its outer edge", () => {
  const walls = freeWalls([room]);
  assert.deepEqual(walls.map(w => w.facing).sort(), ["+x", "+z", "-x", "-z"]);
  assert.deepEqual(walls.find(w => w.facing === "+x").mid, [10, 6]);
  assert.deepEqual(walls.find(w => w.facing === "-z").mid, [5, 0]);
});

test("a wall another wing stands against is free only where it is not, and only a run of four or more", () => {
  const east = { minX: 10, minZ: 0, maxX: 14, maxZ: 7 };
  const runs = freeWalls([room, east]).filter(w => w.wing === 0 && w.facing === "+x");
  assert.deepEqual(runs.map(w => [w.lo, w.hi]), [[8, 11]]);
  const shorter = { minX: 10, minZ: 0, maxX: 14, maxZ: 9 };
  assert.equal(freeWalls([room, shorter]).some(w => w.wing === 0 && w.facing === "+x"), false,
    "two free cells are not a wall a wing can stand on");
});

test("the wall a pointer is near is found, and none where it is far", () => {
  assert.equal(wallNear([room], 10.4, 6, 1).facing, "+x");
  assert.equal(wallNear([room], 5, 5, 1), null);
});

test("a click adds a wing the wall's length and five deep", () => {
  const wall = freeWalls([room]).find(w => w.facing === "+x");
  assert.deepEqual(wingOffWall([room], wall), { minX: 10, maxX: 9 + NEW_WING_DEPTH, minZ: 0, maxZ: 11 });
});

test("a wing is shrunk to fit its bound and the wings beside it, and refused where less than four fits", () => {
  const wall = freeWalls([room]).find(w => w.facing === "+x");
  assert.deepEqual(wingOffWall([room], wall, { minX: -2, minZ: -2, maxX: 13, maxZ: 9 }),
                   { minX: 10, maxX: 13, minZ: 0, maxZ: 9 });
  assert.equal(wingOffWall([room], wall, { minX: -2, minZ: -2, maxX: 12, maxZ: 13 }), null,
    "three blocks of room past the wall is less than a wing");
  const beyond = { minX: 14, minZ: 0, maxX: 20, maxZ: 11 };
  assert.equal(wingOffWall([room, beyond], wall).maxX, 13, "short of the wing it would run into");
  assert.ok(MIN_FOOTPRINT_SPAN === 4);
});
