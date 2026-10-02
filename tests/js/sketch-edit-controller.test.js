// The sketch's two shape rungs, and what each one draws. The claim under test is the one the whole ladder
// rests on: a rung draws its own grips and NOT the rung above or below it, so no two targets share a spot.
// The second claim is that the grips say which rung they belong to by their SHAPE — a square scales the
// whole outline, a disc is one point of it — leaving colour free to say what a point is.
import { test } from "node:test";
import assert from "node:assert/strict";
import { installDomStub } from "./_dom-stub.js";

installDomStub();

const { SketchEditController } =
  await import("../../src/PgmStudio.Client/wwwroot/js/studio/controllers/sketch-edit-controller.js");

const layer = () => ({
  children: [],
  appendChild(c) { this.children.push(c); return c; },
  get firstChild() { return this.children[0] ?? null; },
  removeChild(c) { this.children = this.children.filter(x => x !== c); return c; },
});

// A four-vertex outline whose corners ARE its bounding box — the case where a box grip and a point grip
// would land on the same pixel if both rungs drew at once.
const square = {
  id: "s1", type: "polygon",
  vertices: [[0, 0], [20, 0], [20, 20], [0, 20]],
};

const viewport = { scale: 1, panX: 0, panY: 0 };
const controller = (g) => new SketchEditController(g, () => viewport, () => square, {});

const kinds = (g) => g.children.map(c => c.tagName);
const grabbable = (g) => g.children.filter(c => c.style.cursor);

test("the shape rung draws a box and not one point", () => {
  const g = layer();
  const c = controller(g);
  c.setSelected("s1", "shape");
  c.refresh();
  assert.equal(kinds(g).filter(k => k === "circle").length, 0, "a point grip is drawn on the box rung");
  assert.ok(kinds(g).includes("rect"), "no box is drawn on the box rung");
});

test("the points rung draws the points and no box at all", () => {
  const g = layer();
  const c = controller(g);
  c.setSelected("s1", "points");
  c.refresh();
  assert.equal(kinds(g).filter(k => k === "rect").length, 0, "a box grip is drawn on the points rung");
  assert.equal(kinds(g).filter(k => k === "circle").length, square.vertices.length);
});

test("a point is round where an anchor is square, and both wear the accent", () => {
  const box = layer(), points = layer();
  const bc = controller(box), pc = controller(points);
  bc.setSelected("s1", "shape"); bc.refresh();
  pc.setSelected("s1", "points"); pc.refresh();

  const anchors = grabbable(box).filter(c => c.getAttribute("fill") !== "transparent");
  assert.ok(anchors.length > 0);
  for (const a of anchors) assert.equal(a.tagName, "rect");

  const verts = grabbable(points);
  assert.equal(verts.length, square.vertices.length);
  for (const v of verts) assert.equal(v.tagName, "circle");

  // The rung is read off the shape, so it must not also be read off the colour.
  assert.equal(new Set([...anchors, ...verts].map(el => el.getAttribute("stroke"))).size, 1);
});

test("the group rung leaves the shape layer empty, so the canvas's box stands alone", () => {
  const g = layer();
  const c = controller(g);
  c.setSelected("s1", "group");
  c.refresh();
  assert.equal(g.children.length, 0);
});

test("a point grip and the insert ghost are the same size, being the same kind of thing", () => {
  const g = layer();
  const c = controller(g);
  c.setSelected("s1", "points");
  c.refresh();
  const vertexR = Number(grabbable(g)[0].getAttribute("r"));
  // Hover the middle of the top edge; the ghost that offers a new point appears there.
  c.onPointerMove(10, 0, "select");
  const ghost = g.children.find(el => el.style.cursor === "copy");
  assert.ok(ghost, "no insert ghost on an edge hover");
  assert.equal(ghost.tagName, "circle");
  assert.equal(Number(ghost.getAttribute("r")), vertexR);
});

test("no ghost is offered on a rung that cannot take a point", () => {
  const g = layer();
  const c = controller(g);
  c.setSelected("s1", "shape");
  c.refresh();
  c.onPointerMove(10, 0, "select");
  assert.equal(g.children.filter(el => el.style.cursor === "copy").length, 0);
});

// ── taking one point out ─────────────────────────────────────────────────────────────────────────────────
// The invariant: the outline loses exactly the picked point, its two neighbours become adjacent, everything
// indexed by point is renumbered with it, and a removal that would leave no outline or a folded one changes
// nothing at all.

const U_RING = [[0, 0], [10, 0], [10, 10], [6, 10], [6, 2], [4, 2], [4, 10], [0, 10]];

/** A controller on the points rung over `shape`, with every callback recorded. */
function editing(shape) {
  const g = layer();
  const calls = { updated: 0, picked: [], marks: [] };
  const c = new SketchEditController(g, () => viewport, () => shape, {
    onShapeUpdated: () => { calls.updated++; },
    onVertexSelected: (id, idx) => { calls.picked.push([id, idx]); },
    onSlopeControls: (id, indices) => { calls.marks.push([id, indices]); },
  });
  c.setSelected(shape.id, "points");
  c.refresh();
  return { c, g, calls };
}

/** Click the index-th point handle: a press and a release with no drag between. */
function pick(env, index) {
  const handles = env.g.children.filter(el => el.tagName === "circle" && el.style.cursor === "move" && el.getAttribute("r") === "4");
  handles[index].fire("mousedown");
  env.c.onResizeUp();
}

test("removing a point drops exactly that point and joins its neighbours", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  pick(env, 2);
  assert.equal(env.c.selectedVertex, 2);

  assert.deepEqual(env.c.removeSelectedVertex(), { done: true });
  // (20,0) and (0,20) were either side of the removed point and are consecutive now.
  assert.deepEqual(shape.vertices, [[0, 0], [20, 0], [0, 20]]);
});

test("the pick is cleared, the inspector is told, and the edit is reported once", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  pick(env, 1);
  env.calls.picked.length = 0;

  env.c.removeSelectedVertex();
  assert.equal(env.c.selectedVertex, -1);
  assert.deepEqual(env.calls.picked, [["s", -1]]);
  assert.equal(env.calls.updated, 1);
  assert.equal(env.g.children.filter(el => el.tagName === "circle" && el.getAttribute("r") === "4").length, 3,
    "the handles were not redrawn for the shorter outline");
});

test("controls and per-point heights are renumbered with the points", () => {
  const tag = (name) => ({ in: [name, 0], out: [name, 1] });
  const shape = {
    id: "s", type: "polygon", vertices: [[0, 0], [10, -4], [20, 0], [20, 10], [10, 14], [0, 10]],
    controls: { 0: tag(0), 1: tag(1), 3: tag(3), 4: tag(4), 5: tag(5) },
    anchor_heights: [1, 2, 3, 4, 5, 6],
  };
  const env = editing(shape);
  pick(env, 2);
  env.c.removeSelectedVertex();

  assert.deepEqual(shape.anchor_heights, [1, 2, 4, 5, 6]);
  // Point 2 is gone with its handles; the two points that now share an edge (1 and what was 3) lose theirs,
  // since a handle is fitted to the edges it sat between; the rest keep theirs under the new numbering.
  assert.deepEqual(Object.keys(shape.controls).sort(), ["0", "3", "4"]);
  assert.equal(shape.controls["3"].in[0], 4);
  assert.equal(shape.controls["4"].in[0], 5);
});

test("an outline with no handles left carries no controls key", () => {
  const shape = {
    id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]],
    controls: { 2: { in: [1, 1], out: [2, 2] } },
  };
  const env = editing(shape);
  pick(env, 3);
  env.c.removeSelectedVertex();
  assert.equal(shape.controls, undefined);
});

test("a ring is never taken below three points", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [0, 20]] };
  const env = editing(shape);
  pick(env, 1);
  env.calls.picked.length = 0;

  const result = env.c.removeSelectedVertex();
  assert.match(result.refused, /three points/);
  assert.deepEqual(shape.vertices, [[0, 0], [20, 0], [0, 20]]);
  assert.equal(env.calls.updated, 0);
  assert.equal(env.c.selectedVertex, 1, "a refused removal must leave the pick where it was");
  assert.deepEqual(env.calls.picked, []);
});

test("a removal that folds the outline across itself is refused and changes nothing", () => {
  const shape = { id: "s", type: "polygon", vertices: U_RING.map(v => [...v]) };
  const env = editing(shape);
  pick(env, 0);

  const result = env.c.removeSelectedVertex();
  assert.match(result.refused, /fold/);
  assert.deepEqual(shape.vertices, U_RING);
  assert.equal(env.calls.updated, 0);

  // The same outline loses a point that does not fold it.
  pick(env, 3);
  assert.deepEqual(env.c.removeSelectedVertex(), { done: true });
  assert.equal(shape.vertices.length, U_RING.length - 1);
});

test("a path keeps two points, may cross itself, and renumbers like a ring", () => {
  const line = { id: "p", type: "polyline", vertices: [[0, 0], [10, 0], [10, 10]], anchor_heights: [1, 2, 3] };
  const env = editing(line);
  pick(env, 1);
  assert.deepEqual(env.c.removeSelectedVertex(), { done: true });
  assert.deepEqual(line.vertices, [[0, 0], [10, 10]]);
  assert.deepEqual(line.anchor_heights, [1, 3]);

  pick(env, 0);
  assert.match(env.c.removeSelectedVertex().refused, /two points/);
  assert.equal(line.vertices.length, 2);

  // A path is allowed to cross itself, so the fold check that guards a ring does not apply to it.
  const crossing = { id: "q", type: "polyline", vertices: [[0, 0], [10, 10], [10, 0], [0, 10], [5, 20]] };
  const crossingEnv = editing(crossing);
  pick(crossingEnv, 2);
  assert.deepEqual(crossingEnv.c.removeSelectedVertex(), { done: true });
});

test("with no point picked there is nothing to remove, and a point picked off its rung is let go", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  assert.equal(env.c.removeSelectedVertex(), null);

  pick(env, 1);
  env.calls.picked.length = 0;
  env.c.setSelected("s", "shape");
  assert.equal(env.c.selectedVertex, -1);
  assert.deepEqual(env.calls.picked, [["s", -1]], "the inspector kept showing a point the rung no longer draws");
  assert.equal(env.c.removeSelectedVertex(), null);
  assert.equal(shape.vertices.length, 4);
});

// ── putting one point in ─────────────────────────────────────────────────────────────────────────────────
// The invariant: after a point goes in at index j, whatever named a point k >= j names k + 1 and whatever
// named k < j is untouched — the shift-marked slope controls and the picked point alike — and the host is
// told the indices it now has.

/** Shift-click the index-th point handle, marking it as a slope control. */
function mark(env, index) {
  const handles = env.g.children.filter(el => el.tagName === "circle" && el.style.cursor === "move");
  handles[index].fire("mousedown", { shiftKey: true });
}

/** Hover the edge through (x, z) and press the insert ghost. */
function insertAt(env, x, z) {
  env.c.onPointerMove(x, z, "select");
  env.g.children.find(el => el.style.cursor === "copy").fire("mousedown");
}

test("a slope-control mark above an insert follows its point, one below it stays", () => {
  const shape = {
    id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]],
    anchor_heights: [4, 5, 6, 7],
  };
  const env = editing(shape);
  mark(env, 0); mark(env, 2); mark(env, 3);
  env.calls.marks.length = 0;

  insertAt(env, 10, 0);   // the new point is index 1; 2 and 3 are now 3 and 4

  assert.deepEqual(shape.vertices[1], [10, 0]);
  assert.deepEqual(env.calls.marks, [["s", [0, 3, 4]]], "the host was not given the renumbered marks");
  const named = [0, 3, 4].map(at => shape.vertices[at]);
  assert.deepEqual(named, [[0, 0], [20, 20], [0, 20]], "a mark names a different point than the one it was placed on");
});

test("an insert on the closing edge puts the new point first and moves every mark up", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  mark(env, 1); mark(env, 3);
  env.calls.marks.length = 0;

  insertAt(env, 0, 10);   // the edge from (0, 20) back to (0, 0)

  assert.deepEqual(shape.vertices[0], [0, 10]);
  assert.deepEqual(env.calls.marks, [["s", [2, 4]]]);
});

test("an insert after every mark leaves the marks, and the host is not told of a change", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  mark(env, 0); mark(env, 1);
  env.calls.marks.length = 0;

  insertAt(env, 20, 10);   // the edge from (20, 0) to (20, 20): index 2

  assert.deepEqual(env.calls.marks, []);
});

test("a picked point follows an insert that moves its index", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  pick(env, 3);
  env.calls.picked.length = 0;

  insertAt(env, 10, 0);

  assert.deepEqual(shape.vertices[4], [0, 20]);
  assert.deepEqual(env.calls.picked, [["s", 4]], "the inspector was not told which index the point has now");
});

test("a picked point below an insert keeps its index", () => {
  const shape = { id: "s", type: "polygon", vertices: [[0, 0], [20, 0], [20, 20], [0, 20]] };
  const env = editing(shape);
  pick(env, 0);
  env.calls.picked.length = 0;

  insertAt(env, 10, 0);

  assert.equal(env.c.selectedVertex, 0);
  assert.deepEqual(env.calls.picked, []);
});
