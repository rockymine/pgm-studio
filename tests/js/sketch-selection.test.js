// Which group and which shape are selected is held once, by the canvas. The bridge asks it rather than
// keeping a copy, so the host is told what the canvas shows: a single-member group drills to its member, a
// multi-member group stays a group, and a phase that places things of its own drills to neither.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";

const rectangle = (id, minX, minZ, maxX, maxZ) =>
  ({ id, type: "rectangle", min_x: minX, min_z: minZ, max_x: maxX, max_z: maxZ, operation: "add", base_height: 9, floor: 0 });

// Two overlapping rectangles fuse into one group; the third stands alone.
const SHAPES = [rectangle("a", 0, 0, 10, 10), rectangle("b", 5, 5, 15, 15), rectangle("c", 40, 40, 50, 50)];

async function board() {
  const mounted = await mountSketchBridge();
  mounted.handle.load({ layers: [{ id: "ground", name: "Ground", base_y: 0, layout: { shapes: SHAPES } }] });
  const { groups } = JSON.parse(mounted.firedOf("OnLayout").at(-1)[0]);
  const group = (shapeId) => groups.find((entry) => entry.shapeIds.includes(shapeId)).id;
  mounted.fired.length = 0;
  return { ...mounted, fusedId: group("a"), loneId: group("c") };
}

test("a single-member group drills to its member and announces no group", async () => {
  const { handle, firedOf, loneId } = await board();
  handle.selectGroup(loneId);
  assert.deepEqual(firedOf("OnShapeSelected").at(-1), ["c"]);
  assert.deepEqual(firedOf("OnGroupSelected").at(-1), [null]);
});

test("a multi-member group stays a group", async () => {
  const { handle, firedOf, fusedId } = await board();
  handle.selectGroup(fusedId);
  assert.deepEqual(firedOf("OnShapeSelected").at(-1), [null]);
  assert.deepEqual(firedOf("OnGroupSelected").at(-1), [fusedId]);
});

test("a phase placing its own things drills a single-member group to nothing", async () => {
  for (const phase of ["setReliefMode", "setDressingMode"]) {
    const { handle, firedOf, loneId } = await board();
    handle[phase](true);
    handle.selectGroup(loneId);
    assert.deepEqual(firedOf("OnShapeSelected").at(-1), [null], phase);
    assert.deepEqual(firedOf("OnGroupSelected").at(-1), [loneId], phase);
  }
});

test("selecting a shape lets the group go", async () => {
  const { handle, firedOf, fusedId } = await board();
  handle.selectGroup(fusedId);
  handle.selectShape("a");
  assert.deepEqual(firedOf("OnShapeSelected").at(-1), ["a"]);
  assert.deepEqual(firedOf("OnGroupSelected").at(-1), [null]);
});

test("rotating acts on every member of the selected group and on nothing outside it", async () => {
  const { handle, fusedId } = await board();
  const shapesById = () => new Map(handle.getState().layers[0].layout.shapes.map((shape) => [shape.id, JSON.stringify(shape)]));
  const before = shapesById();

  handle.selectGroup(fusedId);
  handle.rotateSelected(90);
  const after = shapesById();
  assert.notEqual(after.get("a"), before.get("a"), "a turned");
  assert.notEqual(after.get("b"), before.get("b"), "b turned");
  assert.equal(after.get("c"), before.get("c"), "c, outside the group, did not");
});
