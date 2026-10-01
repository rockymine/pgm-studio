// The overlay a change draws over the board. It is read off the two layouts by id, so what is asserted is that
// each kind of edit lands in its own state and carries the geometry a reader needs to see it: where a thing was
// and where it is.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { diffOverlay, paintDiff } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/diff-render.js";

const layout = (shapes, props = []) => ({
  layers: [{ id: "ground", layout: { shapes, groups: [] } }],
  dressing: { props },
});

const square = { id: "s1", type: "rectangle", operation: "add", min_x: 0, min_z: 0, max_x: 10, max_z: 10, floor: 4 };
const tree = { kind: "tree", id: "t1", x: 3, z: 4, seed: 1 };

test("a shape only one side holds is added or removed, with the outline it has or had", () => {
  const circle = { id: "c1", type: "circle", operation: "add", center_x: 20, center_z: 0, radius: 3 };
  const overlay = diffOverlay(layout([square]), layout([circle]), null);

  const added = overlay.shapes.find(shape => shape.id === "c1");
  const removed = overlay.shapes.find(shape => shape.id === "s1");
  assert.equal(added.state, "added");
  assert.ok(added.after.length > 3);
  assert.equal(removed.state, "removed");
  assert.deepEqual(removed.before[0], [0, 0]);
});

test("an outline that moved is reshaped and carries both; a field beside the outline is only changed", () => {
  const wider = { ...square, max_x: 14 };
  const raised = { ...square, floor: 9 };

  const [reshaped] = diffOverlay(layout([square]), layout([wider]), null).shapes;
  const [changed] = diffOverlay(layout([square]), layout([raised]), null).shapes;

  assert.equal(reshaped.state, "reshaped");
  assert.deepEqual(reshaped.before[1], [10, 0]);
  assert.deepEqual(reshaped.after[1], [14, 0]);
  assert.equal(changed.state, "changed");
  assert.equal(changed.before, null);
});

test("the same layout draws nothing", () => {
  const overlay = diffOverlay(layout([square], [tree]), layout([square], [tree]), null);
  assert.deepEqual(overlay, { shapes: [], props: [], runs: [] });
});

test("a prop that moved is drawn from where it stood to where it stands", () => {
  const [moved] = diffOverlay(layout([], [tree]), layout([], [{ ...tree, x: 6, z: 8 }]), null).props;

  assert.equal(moved.state, "moved");
  assert.deepEqual(moved.from, [3, 4]);
  assert.deepEqual(moved.to, [6, 8]);
});

test("a prop drawn along points stands at the middle of them", () => {
  const road = { kind: "stroke", id: "r1", points: [[0, 0], [10, 0]], radius: 2 };
  const [placed] = diffOverlay(layout([]), layout([], [road]), null).props;

  assert.equal(placed.state, "placed");
  assert.deepEqual(placed.to, [5, 0]);
});

test("each run of changed columns is one box over the blocks it covers", () => {
  const world = { ground: { columns: 40, runs: [{ cells: 40, minX: 10, minZ: -20, maxX: 13, maxZ: -11 }] },
                  surface: { columns: 0, runs: [] }, structure: { columns: 0, runs: [] } };
  const overlay = diffOverlay(layout([]), layout([]), world);
  const painter = recordingPainter();

  paintDiff(painter, overlay);

  const [[box, style]] = painter.of("rect");
  assert.deepEqual(box, { min_x: 10, min_z: -20, max_x: 14, max_z: -10 });
  assert.match(style.fill, /ground/);
});
