// The modifier stack as the sketch holds it: the list the rasterizer runs, edited in place and saved in the order
// it runs. What must hold is that an edit through the bridge is the document the layout stores, and an undo step.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";
import { newModifier, patchModifier, moveModifier, freshModifierId, areaRing, maskRings }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";

test("a fresh modifier's id is unique in the stack", () => {
  const stack = [{ id: "hill" }, { id: "hill-2" }];
  assert.equal(freshModifierId(stack, "mound"), "hill-3");
  assert.equal(freshModifierId([], "crater"), "hollow");
});

test("a fresh Roughness covers the board and every other operation an ellipse at the point given", () => {
  assert.deepEqual(newModifier([], "noise", [3, 4]).mask.areas, [{ kind: "board" }]);
  assert.deepEqual(newModifier([], "mound", [3.4, 4.6]).mask.areas, [{ kind: "ellipse", at: [3, 5], rx: 12, rz: 12 }]);
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

test("an ellipse's outline lies on the ellipse, turned by its angle", () => {
  const ring = areaRing({ kind: "ellipse", at: [10, 0], rx: 6, rz: 2, angle: 90 });
  for (const [x, z] of ring) {
    const u = z, v = -(x - 10);   // undo the quarter turn
    assert.ok(Math.abs((u / 6) ** 2 + (v / 2) ** 2 - 1) < 1e-9);
  }
  assert.equal(areaRing({ kind: "board" }), null);
});

test("a mirrored mask is outlined at each image under the symmetry, and an unmirrored one only where drawn", () => {
  const hill = { mask: { areas: [{ kind: "ellipse", at: [10, 0], rx: 3, rz: 3 }], mirror: true } };
  assert.equal(maskRings(hill, "rot_90").length, 4);
  const [own, image] = maskRings(hill, "rot_180");
  assert.equal(image.image, true);
  assert.ok(Math.abs(own.points[0][0] + image.points[0][0]) < 1e-9, "the half turn about the origin");
  assert.equal(maskRings({ mask: { ...hill.mask, mirror: false } }, "rot_180").length, 1);
});

test("a modifier added, edited, moved and deleted through the bridge is the layout's modifiers, each an undo step", async () => {
  const { handle, firedOf } = await mountSketchBridge();
  handle.load({ layers: [] });
  assert.equal(handle.getState().modifiers, undefined, "an empty stack writes no key");

  handle.addModifier("mound");
  handle.addModifier("noise");
  const [hill, rough] = handle.getState().modifiers;
  assert.equal(hill.op, "mound");
  assert.equal(JSON.parse(firedOf("OnModifiers").at(-1)[0]).selectedId, rough.id, "the one added is picked");

  assert.equal(handle.updateModifier(hill.id, JSON.stringify({ rise: 20 })), null);
  handle.moveModifier(rough.id, -1);
  assert.deepEqual(handle.getState().modifiers.map(m => m.id), [rough.id, hill.id]);
  assert.equal(handle.getState().modifiers[1].rise, 20);

  handle.deleteModifier(rough.id);
  assert.deepEqual(handle.getState().modifiers.map(m => m.id), [hill.id]);

  handle.undo();
  assert.equal(handle.getState().modifiers.length, 2, "undo puts the deleted modifier back");
  handle.load({ layers: [], modifiers: [hill] });
  assert.deepEqual(handle.getState().modifiers, [hill], "a stored stack loads as it was written");
});
