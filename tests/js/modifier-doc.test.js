// The modifier stack as the sketch holds it: the list the rasterizer runs, traced and edited on the board, saved
// in the order it runs. What must hold is that a trace makes the modifier the layout stores, that a picked mask
// moves and reshapes as one, and that every edit through the bridge is an undo step.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";
import { newModifier, addArea, patchModifier, moveModifier, freshModifierId, areaPoints, withAreaPoints,
         translateModifier, maskRings }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";
import { ModifierController, opOfTool }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/modifier-controller.js";

const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];

test("a fresh modifier's id is unique in the stack", () => {
  const stack = [{ id: "hill" }, { id: "hill-2" }];
  assert.equal(freshModifierId(stack, "mound"), "hill-3");
  assert.equal(freshModifierId([], "crater"), "hollow");
});

test("a traced outline is the new modifier's polygon mask, and another outline joins it", () => {
  const hill = newModifier([], "mound", SQUARE);
  assert.deepEqual(hill.mask, { areas: [{ kind: "polygon", points: SQUARE }], mirror: true });
  assert.equal(addArea(hill, SQUARE).mask.areas.length, 2);
});

test("a patch replaces the mask whole, drops a number set to null, and drops off once it is false", () => {
  const stack = [{ id: "a", op: "mound", rise: 8, off: true, mask: { areas: [{ kind: "board" }], mirror: true } }];
  const [patched] = patchModifier(stack, "a", { rise: null, off: false, mask: { areas: [], mirror: false } });
  assert.equal("rise" in patched, false);
  assert.equal("off" in patched, false);
  assert.deepEqual(patched.mask, { areas: [], mirror: false });
});

test("a move stays inside the stack and keeps every modifier", () => {
  const stack = [{ id: "a" }, { id: "b" }, { id: "c" }];
  assert.deepEqual(moveModifier(stack, "a", 1).map(m => m.id), ["b", "a", "c"]);
  assert.deepEqual(moveModifier(stack, "a", -1), stack);
  assert.deepEqual(moveModifier(stack, "c", 5), stack);
});

test("an ellipse's outline lies on the ellipse, turned by its angle, and the board has none", () => {
  for (const [x, z] of areaPoints({ kind: "ellipse", at: [10, 0], rx: 6, rz: 2, angle: 90 })) {
    const u = z, v = -(x - 10);
    assert.ok(Math.abs((u / 6) ** 2 + (v / 2) ** 2 - 1) < 1e-9);
  }
  assert.equal(areaPoints({ kind: "board" }), null);
});

test("moving a mask moves every outline, and reshaping an ellipse makes it the polygon drawn", () => {
  const mixed = { id: "m", mask: { areas: [{ kind: "polygon", points: SQUARE }, { kind: "ellipse", at: [5, 5], rx: 3, rz: 3 }] } };
  const moved = translateModifier(mixed, 2, -1);
  assert.deepEqual(moved.mask.areas[0].points[0], [2, -1]);
  assert.deepEqual(moved.mask.areas[1].at, [7, 4]);
  assert.deepEqual(withAreaPoints(mixed, 1, SQUARE).mask.areas[1], { kind: "polygon", points: SQUARE });
});

test("a mirrored mask is outlined at each image under the symmetry, and an unmirrored one only where drawn", () => {
  const hill = { mask: { areas: [{ kind: "polygon", points: SQUARE }], mirror: true } };
  assert.equal(maskRings(hill, "rot_90").length, 4);
  const [own, image] = maskRings(hill, "rot_180");
  assert.equal(image.image, true);
  assert.equal(image.points[1][0], -10, "the half turn about the origin");
  assert.equal(own.points.length, 4);
  assert.equal(maskRings({ mask: { ...hill.mask, mirror: false } }, "rot_180").length, 1);
});

test("a drag with a modifier's tool traces its outline, and Select picks and drags the smallest mask under it", () => {
  let stack = [];
  const traced = [];
  const tools = new ModifierController(null, null, {
    getModifiers: () => stack,
    onTraced: (op, points) => { traced.push([op, points]); stack = [...stack, newModifier(stack, op, points)]; },
    onChanged: (next) => { stack = next; },
    onPreview: (next) => { stack = next; },
  });
  assert.equal(opOfTool("modifier:crater"), "crater");
  tools.onMouseDown(0, 0, "modifier:mound");
  for (const [x, z] of [[20, 0], [20, 20], [0, 20], [0, 1]]) tools.onMouseMove(x, z, "modifier:mound");
  tools.onMouseUp();
  assert.equal(traced.length, 1);
  assert.equal(traced[0][0], "mound");

  stack = [...stack, newModifier(stack, "level", [[5, 5], [8, 5], [8, 8], [5, 8]])];
  assert.equal(tools.onMouseDown(6, 6, "select"), true);
  assert.equal(tools.selectedId, "level", "the smaller outline wins");
  tools.onMouseMove(9, 6, "select");
  tools.onMouseUp();
  assert.deepEqual(stack.find(m => m.id === "level").mask.areas[0].points[0], [8, 5]);
  assert.equal(tools.onMouseDown(50, 50, "select"), false, "a press on no mask picks nothing");
  assert.equal(tools.selectedId, null);
});

test("an edit through the bridge is the layout's modifiers, and each is an undo step", async () => {
  const { handle } = await mountSketchBridge();
  handle.load({ layers: [] });
  assert.equal(handle.getState().modifiers, undefined, "an empty stack writes no key");

  const hill = newModifier([], "mound", SQUARE), rough = newModifier([hill], "noise", SQUARE);
  handle.load({ layers: [], modifiers: [hill, rough] });
  assert.deepEqual(handle.getState().modifiers, [hill, rough], "a stored stack loads as it was written");

  assert.equal(handle.updateModifier(hill.id, JSON.stringify({ rise: 20 })), null);
  handle.moveModifier(rough.id, -1);
  assert.deepEqual(handle.getState().modifiers.map(m => m.id), [rough.id, hill.id]);
  assert.equal(handle.getState().modifiers[1].rise, 20);

  handle.deleteModifier(rough.id);
  assert.deepEqual(handle.getState().modifiers.map(m => m.id), [hill.id]);
  handle.undo();
  assert.equal(handle.getState().modifiers.length, 2, "undo puts the deleted modifier back");
});
