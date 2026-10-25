// The solids (box, wedge, cylinder, sphere, torus, arch) as the canvas draws and moves them: each is the
// footprint of the kind it reads, so it translates, scales, snaps and hit-tests as that footprint, and a turned
// rectangle is drawn turned. Pure geometry, no DOM.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import {
  SOLID_FOOTPRINTS, SOLIDS_OF, isSolid, footprintOf, isAxisRect, asSolid,
  toRing, toBounds, containsPoint, translateShape, rotateShape, scaleShape, snapShape,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/shape.js";

const r6 = (v) => Math.round(v * 1e6) / 1e6;
const box = { id: "b", type: "box", min_x: 0, min_z: 0, max_x: 8, max_z: 4, base_height: 5, floor: 2 };
const sphere = { id: "s", type: "sphere", center_x: 10, center_z: 10, radius: 4 };

/** The `public const string` values of ShapeKinds, in declaration order. */
function kindWords() {
  const cs = readFileSync("src/PgmStudio.Vocabulary/ShapeKinds.cs", "utf8");
  return [...cs.matchAll(/public const string \w+\s*=\s*"([^"]+)"/g)].map((match) => match[1]);
}

test("the solids are the ShapeKinds the C# spells, and each reads a footprint kind", () => {
  const words = kindWords();
  assert.deepEqual(Object.keys(SOLID_FOOTPRINTS), words.slice(5));
  for (const footprint of Object.values(SOLID_FOOTPRINTS)) assert.ok(words.slice(0, 5).includes(footprint), footprint);
});

test("a solid is drawn as the footprint it reads", () => {
  assert.equal(footprintOf(box), "rectangle");
  assert.equal(footprintOf(sphere), "circle");
  assert.equal(footprintOf({ type: "arch", vertices: [[0, 0], [9, 0]], radius: 3 }), "polyline");
  assert.equal(footprintOf({ type: "polygon" }), "polygon");
  assert.ok(isSolid(box) && !isSolid({ type: "rectangle" }));
});

test("a box rings, bounds and hit-tests as its rectangle", () => {
  assert.deepEqual(toRing(box), [[0, 0], [8, 0], [8, 4], [0, 4], [0, 0]]);
  assert.deepEqual(toBounds(box), { min_x: 0, min_z: 0, max_x: 8, max_z: 4 });
  assert.ok(containsPoint(box, 4, 2));
  assert.ok(!containsPoint(box, 9, 2));
});

test("a sphere rings and hit-tests as its circle", () => {
  assert.equal(toRing(sphere).length, 65);
  assert.deepEqual(toBounds(sphere), { min_x: 6, min_z: 6, max_x: 14, max_z: 14 });
  assert.ok(containsPoint(sphere, 12, 10));
  assert.ok(!containsPoint(sphere, 15, 10));
});

test("a turned box is drawn turned about its middle and hit-tested on the turn", () => {
  const turned = { ...box, turn: 90 };
  const ring = toRing(turned).map(([x, z]) => [r6(x), r6(z)]);
  assert.deepEqual(ring, [[6, -2], [6, 6], [2, 6], [2, -2], [6, -2]]);
  const turnedBounds = toBounds(turned);
  assert.deepEqual([r6(turnedBounds.min_x), r6(turnedBounds.min_z), r6(turnedBounds.max_x), r6(turnedBounds.max_z)], [2, -2, 6, 6]);
  assert.ok(containsPoint(turned, 4, 5), "inside the turned footprint");
  assert.ok(!containsPoint(turned, 0.5, 2), "inside the unturned one only");
  assert.ok(isAxisRect(box) && !isAxisRect(turned));
});

test("a solid keeps its kind and its numbers when moved, scaled and snapped", () => {
  const moved = translateShape({ ...box, turn: 30, tilt: 10 }, 3, -1);
  assert.deepEqual([moved.type, moved.min_x, moved.min_z, moved.turn, moved.tilt], ["box", 3, -1, 30, 10]);

  const grown = scaleShape(sphere, 2, 2, [0, 0]);
  assert.deepEqual([grown.type, grown.center_x, grown.radius], ["sphere", 20, 8]);

  const snapped = snapShape({ ...sphere, center_x: 10.4, radius: 3.6 });
  assert.deepEqual([snapped.type, snapped.center_x, snapped.radius], ["sphere", 10, 4]);
});

test("rotating a solid turns it rather than turning it into a polygon", () => {
  const quarter = rotateShape(box, Math.PI / 2, [4, 2]);
  assert.equal(quarter.type, "box");
  assert.equal(r6(quarter.turn), 90);
  assert.deepEqual([r6(quarter.min_x), r6(quarter.max_x), r6(quarter.min_z), r6(quarter.max_z)], [0, 8, 0, 4]);
  const around = rotateShape(sphere, Math.PI, [0, 0]);
  assert.deepEqual([around.type, r6(around.center_x), r6(around.center_z), r6(around.turn)], ["sphere", -10, -10, 180]);
});

test("rotating an arch moves its centreline", () => {
  const arch = { type: "arch", vertices: [[0, 0], [10, 0]], radius: 3 };
  const turned = rotateShape(arch, Math.PI / 2, [0, 0]);
  assert.equal(turned.type, "arch");
  assert.deepEqual(turned.vertices.map(([x, z]) => [r6(x), r6(z)]), [[0, 0], [0, 10]]);
});

test("a rectangle or circle is read as a solid and back, and a polygon is neither", () => {
  const ball = asSolid({ id: "r", type: "rectangle", min_x: 0, min_z: 0, max_x: 8, max_z: 6, base_height: 4 }, "sphere");
  assert.deepEqual([ball.type, ball.center_x, ball.center_z, ball.radius, ball.min_x], ["sphere", 4, 3, 3, undefined]);
  assert.equal(ball.base_height, 4);

  const slab = asSolid(sphere, "box");
  assert.deepEqual([slab.type, slab.min_x, slab.max_x, slab.min_z, slab.max_z, slab.radius], ["box", 6, 14, 6, 14, undefined]);

  const ring = asSolid({ ...sphere, id: "t" }, "torus");
  assert.equal(ring.tube, 1, "a torus states its tube, a quarter of its radius at least one block");

  const flat = asSolid({ ...box, turn: 20, tilt: 10, sunk: 2, tube: 1 }, "");
  assert.deepEqual([flat.type, flat.turn, flat.tilt, flat.sunk, flat.tube], ["rectangle", undefined, undefined, undefined, undefined]);

  const poly = { id: "p", type: "polygon", vertices: [[0, 0], [4, 0], [4, 4]] };
  assert.equal(asSolid(poly, "box"), poly);
  assert.equal(asSolid(poly, ""), poly);
  assert.equal(asSolid({ type: "rectangle", min_x: 0, max_x: 2, min_z: 0, max_z: 2 }, "arch").type, "rectangle");
});

test("a path becomes an arch and back, and only an arch", () => {
  const path = { id: "a", type: "polyline", vertices: [[0, 0], [10, 0]], radius: 3 };
  const arch = asSolid(path, "arch");
  assert.equal(arch.type, "arch");
  assert.deepEqual(arch.vertices, path.vertices);
  assert.equal(asSolid(path, "box"), path);
  assert.equal(asSolid({ ...arch, deck: [12, 4] }, "").deck, undefined);
});

test("each footprint offers the solids it can be", () => {
  assert.deepEqual(SOLIDS_OF.rectangle, ["box", "wedge", "cylinder", "sphere", "torus"]);
  assert.deepEqual(SOLIDS_OF.circle, SOLIDS_OF.rectangle);
  assert.deepEqual(SOLIDS_OF.polyline, ["arch"]);
});
