// The modifier stack as the sketch holds it: the list the rasterizer runs, traced and edited on the board, saved
// in the order it runs. What must hold is that a trace makes the modifier the layout stores, that a picked mask
// moves and reshapes as one, and that every edit through the bridge is an undo step.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";
import { newModifier, addArea, patchModifier, moveModifier, freshModifierId, areaPoints, withAreaPoints,
         translateModifier, maskRings, isLineOp, distanceToLine, LINE_WIDTH, MODIFIER_OPS }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";
import { ModifierController, opOfTool }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/modifier-controller.js";

const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];

test("a fresh modifier's id is unique in the stack", () => {
  const stack = [{ id: "mound" }, { id: "mound-2" }];
  assert.equal(freshModifierId(stack, "mound"), "mound-3");
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

const RUN = [[0, 0], [20, 0], [20, 20]];

test("a line operation's trace is a line of the standard width, and every other is a polygon", () => {
  const ridge = newModifier([], "ridge", RUN);
  assert.deepEqual(ridge.mask.areas, [{ kind: "line", points: RUN, width: LINE_WIDTH }]);
  assert.equal(LINE_WIDTH, 6);
  for (const op of ["scarp", "canyon", "road", "river", "profile", "tunnel"]) assert.ok(isLineOp(op), op);
  for (const op of ["mound", "crater", "level", "butte", "spire", "lake", "coast", "noise", "lumps", "shatter",
                    "gullies", "warp"])
    assert.ok(!isLineOp(op), op);
  assert.equal(newModifier([], "butte", SQUARE).mask.areas[0].kind, "polygon");
  assert.equal(newModifier([], "profile", RUN).mask.areas[0].width, 32, "a profile's steps need a wide band");
});

test("every operation names itself, starts with its own numbers and gets a unique id", () => {
  assert.equal(MODIFIER_OPS.length, 21);
  const stack = [];
  for (const { op, label } of MODIFIER_OPS) {
    const made = newModifier(stack, op, RUN);
    assert.equal(made.id, label.toLowerCase());
    stack.push(made);
  }
  const byOp = Object.fromEntries(stack.map(m => [m.op, m]));
  assert.equal(byOp.scarp.side, 1);
  assert.equal(byOp.canyon.bed, 0.35);
  assert.equal(byOp.river.water, 1);
  assert.equal(byOp.road.grade, 0.25);
  assert.equal(byOp.gullies.count, 8);
  assert.equal(byOp.warp.cell, 24);
  assert.equal(byOp.profile.steps.length, 2);
  assert.equal(byOp.coast.shelf, 10);
  assert.deepEqual(byOp.lumps.across, [6, 16]);
  assert.equal(byOp.shatter.cell, 10);
  assert.equal(byOp.tunnel.cover, 3);
  assert.equal(byOp.chamber.height, 6);
  assert.equal("top" in byOp.butte, false, "an absolute height is left for the server to read off the ground");
  assert.equal("level" in byOp.lake, false);
});

test("a line modifier takes another line, and a line keeps its width when reshaped or moved", () => {
  const ridge = newModifier([], "ridge", RUN);
  assert.equal(addArea(ridge, RUN).mask.areas[1].kind, "line");
  const wide = { ...ridge, mask: { ...ridge.mask, areas: [{ kind: "line", points: RUN, width: 10 }] } };
  assert.equal(withAreaPoints(wide, 0, [[1, 1], [5, 5]]).mask.areas[0].width, 10);
  assert.deepEqual(translateModifier(wide, 3, 4).mask.areas[0], { kind: "line", points: [[3, 4], [23, 4], [23, 24]], width: 10 });
  assert.deepEqual(areaPoints(wide.mask.areas[0]), RUN);
  assert.equal(areaPoints({ kind: "line", points: [[0, 0]], width: 6 }), null, "one point is not a run");
});

test("a line is outlined open, with its band, and each image under the symmetry", () => {
  const ridge = newModifier([], "ridge", RUN);
  const [own, image] = maskRings(ridge, "rot_180");
  assert.equal(own.line, true);
  assert.equal(own.width, 6);
  assert.equal(image.image, true);
  assert.equal(image.points.length, 3, "an open run is not closed by its image");
  assert.equal(maskRings(newModifier([], "butte", SQUARE), "none")[0].line, false);
});

test("distance to a line is to its nearest segment, not to its ends", () => {
  assert.equal(distanceToLine(RUN, 10, 4), 4);
  assert.equal(distanceToLine(RUN, 24, 10), 4);
  assert.equal(distanceToLine(RUN, -3, 0), 3);
});

function controllerOver(stack) {
  const log = { traced: [], changed: 0 };
  const tools = new ModifierController(null, null, {
    getModifiers: () => stack.list,
    onTraced: (op, points) => { log.traced.push([op, points]); stack.list = [...stack.list, newModifier(stack.list, op, points)]; },
    onChanged: (next) => { stack.list = next; log.changed++; },
    onPreview: (next) => { stack.list = next; },
  });
  return { tools, log };
}

test("a drag with a line tool traces an open run, dropping the points that lie on its bends' chords", () => {
  const stack = { list: [] };
  const { tools, log } = controllerOver(stack);
  tools.onMouseDown(0, 0, "modifier:ridge");
  for (const [x, z] of [[5, 0], [10, 0], [15, 0], [20, 0], [20, 5], [20, 10], [20, 20]]) tools.onMouseMove(x, z, "modifier:ridge");
  tools.onMouseUp();
  assert.equal(log.traced.length, 1);
  assert.deepEqual(log.traced[0], ["ridge", [[0, 0], [20, 0], [20, 20]]]);
  assert.equal(stack.list[0].mask.areas[0].kind, "line");
});

test("a line that goes nowhere is a misfire: a click, or a drag of a block or two", () => {
  const stack = { list: [] };
  const { tools, log } = controllerOver(stack);
  tools.onMouseDown(4, 4, "modifier:river");
  tools.onMouseUp();
  tools.onMouseDown(4, 4, "modifier:river");
  tools.onMouseMove(5, 5, "modifier:river");
  tools.onMouseUp();
  assert.equal(log.traced.length, 0);
});

test("Select picks a line within half its width and not beyond, and drags the whole run", () => {
  const stack = { list: [newModifier([], "road", [[0, 0], [30, 0]])] };
  const { tools } = controllerOver(stack);
  assert.equal(tools.onMouseDown(10, 2, "select"), true, "two blocks off a six-wide line is on it");
  assert.equal(tools.selectedId, "road");
  tools.onMouseMove(10, 12, "select");
  tools.onMouseUp();
  assert.deepEqual(stack.list[0].mask.areas[0].points, [[0, 10], [30, 10]]);
  assert.equal(tools.onMouseDown(10, 40, "select"), false, "thirty blocks off picks nothing");
});

test("a point inserted into a picked line goes in a real edge, and the last point has no edge back to the first", () => {
  const stack = { list: [newModifier([], "ridge", [[0, 0], [10, 0], [10, 10]])] };
  const { tools } = controllerOver(stack);
  tools.select("ridge", 0);
  assert.equal(tools.insertPoint(0), true);
  assert.deepEqual(stack.list[0].mask.areas[0].points, [[0, 0], [5, 0], [10, 0], [10, 10]]);
  assert.equal(tools.insertPoint(3), false, "the last point of an open run has no edge after it");
});
