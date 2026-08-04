// Placed dressing (dressing/dressing-doc.js + controllers/dressing-controller.js) — the half of the phase
// that has no server in it. What is worth asserting is the interaction: that a drag places one thing and
// ends when the button comes up, that a click drops a marker, and that the next thing placed inherits the
// settings the last one was given — which is what lets an author place a stand of ten oaks.
import { test } from "node:test";
import assert from "node:assert/strict";

import { DressingDoc, defaultProp, isMarker, propAnchor, translateProp }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/dressing/dressing-doc.js";
import { DressingController, DRESSING_TOOLS }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/dressing-controller.js";

const controller = () => {
  const doc = new DressingDoc();
  return { doc, tools: new DressingController(doc) };
};

// A drag: press, trace, release — the whole interaction, and the reason there is no way to get stuck.
function drag(tools, tool, points) {
  tools.onMouseDown(points[0][0], points[0][1], tool);
  for (const [x, z] of points.slice(1)) tools.onMouseMove(x, z, tool);
  tools.onMouseUp();
}

// ── the document ──────────────────────────────────────────────────────────────
test("a fresh prop of each kind starts at the same numbers the server does", () => {
  assert.equal(defaultProp("path").radius, 3);
  assert.equal(defaultProp("path").style, "solid");
  assert.equal(defaultProp("tree").species, "oak");
  assert.equal(defaultProp("boulder").form, "round");
  assert.equal(defaultProp("flora").spec.coverage, 0.45);
  assert.throws(() => defaultProp("unicorn"));
});

test("markers are points and the rest are areas", () => {
  assert.ok(isMarker("tree") && isMarker({ kind: "boulder" }));
  assert.ok(!isMarker("path") && !isMarker({ kind: "flora" }));
  assert.deepEqual(propAnchor({ kind: "tree", x: 4, z: 9 }), [4, 9]);
  assert.deepEqual(propAnchor({ kind: "path", points: [[0, 0], [10, 20]] }), [5, 10]);
});

test("moving an area moves every point, so a drag keeps its shape", () => {
  const moved = translateProp({ kind: "path", points: [[0, 0], [10, 4]] }, 3, -2);
  assert.deepEqual(moved.points, [[3, -2], [13, 2]]);
  assert.deepEqual(translateProp({ kind: "tree", x: 1, z: 1 }, 2, 2), { kind: "tree", x: 3, z: 3 });
});

test("a stored document round-trips, and a kind the client cannot draw is dropped", () => {
  const doc = DressingDoc.from({ props: [
    { kind: "tree", id: "d1", x: 1, z: 2 },
    { kind: "wormhole", id: "d2" },
  ] });
  assert.equal(doc.props.length, 1);
  assert.equal(doc.toJSON().props[0].kind, "tree");
});

test("ids are minted for props that arrive without one, and never collide with the ones that have", () => {
  const doc = DressingDoc.from({ props: [{ kind: "tree", id: "d7", x: 0, z: 0 }, { kind: "tree", x: 1, z: 1 }] });
  const ids = doc.props.map(p => p.id);
  assert.equal(new Set(ids).size, 2);
  const added = doc.add(defaultProp("boulder"));
  assert.ok(!ids.includes(added.id));
});

// ── placing ───────────────────────────────────────────────────────────────────
test("a click drops one marker where it was clicked", () => {
  const { doc, tools } = controller();
  tools.onMouseDown(12, 34, DRESSING_TOOLS ? "dress:tree" : "");
  assert.equal(doc.props.length, 1);
  assert.deepEqual([doc.props[0].x, doc.props[0].z], [12, 34]);
  assert.equal(doc.props[0].kind, "tree");
});

test("a drag places one route, and releasing is what ends it", () => {
  // The bug this replaced: a click-by-click path had no way to stop. Here the pointer-up *is* the end.
  const { doc, tools } = controller();
  drag(tools, "dress:path", [[0, 0], [4, 2], [9, 6], [14, 4], [20, 8]]);
  assert.equal(doc.props.length, 1);
  assert.equal(doc.props[0].kind, "path");
  assert.ok(doc.props[0].points.length >= 2);
  assert.deepEqual(doc.props[0].points[0], [0, 0]);
  assert.deepEqual(doc.props[0].points.at(-1), [20, 8]);
});

test("a route keeps its direction; an area is simplified as an outline", () => {
  const { doc, tools } = controller();
  drag(tools, "dress:path", [[0, 0], [10, 0], [20, 0], [30, 0]]);
  const route = doc.props[0].points;
  assert.deepEqual(route[0], [0, 0]);
  assert.deepEqual(route.at(-1), [30, 0]);

  drag(tools, "dress:flora", [[0, 0], [10, 0], [10, 10], [0, 10], [0, 5]]);
  assert.equal(doc.props[1].kind, "flora");
  assert.ok(doc.props[1].points.length >= 3);
});

test("a drag too short to be anything places nothing", () => {
  const { doc, tools } = controller();
  tools.onMouseDown(5, 5, "dress:path");
  tools.onMouseUp();
  assert.equal(doc.props.length, 0);
});

test("two props of the same kind are two different props", () => {
  // Same knobs, different seed — or a stand of oaks would be one oak stamped ten times.
  const { doc, tools } = controller();
  tools.onMouseDown(0, 0, "dress:boulder");
  tools.onMouseDown(20, 20, "dress:boulder");
  assert.notEqual(doc.props[0].seed, doc.props[1].seed);
});

// ── editing ───────────────────────────────────────────────────────────────────
test("editing the selection carries into the next one placed", () => {
  // The whole point of the tool having settings: widen one path and the next is already that wide.
  const { doc, tools } = controller();
  drag(tools, "dress:path", [[0, 0], [10, 0], [20, 0]]);
  tools.updateSelected({ radius: 6, style: "cobble" });

  drag(tools, "dress:path", [[0, 40], [10, 40], [20, 40]]);
  assert.equal(doc.props[1].radius, 6);
  assert.equal(doc.props[1].style, "cobble");
});

test("a click picks the prop under it, and the smallest one wins an overlap", () => {
  const { doc, tools } = controller();
  tools.onMouseDown(0, 0, "dress:tree");
  drag(tools, "dress:flora", [[-20, -20], [20, -20], [20, 20], [-20, 20]]);

  tools.onMouseDown(0, 0, "select");
  assert.equal(doc.byId(tools.selectedId).kind, "tree");   // the marker, not the area it stands in
  tools.onMouseDown(18, 18, "select");
  assert.equal(doc.byId(tools.selectedId).kind, "flora");
});

test("dragging a placed prop moves it, and a press with nothing under it clears the selection", () => {
  const { doc, tools } = controller();
  tools.onMouseDown(10, 10, "dress:tree");
  const id = tools.selectedId;

  tools.onMouseDown(10, 10, "select");
  tools.onMouseMove(16, 13, "select");
  tools.onMouseUp();
  assert.deepEqual([doc.byId(id).x, doc.byId(id).z], [16, 13]);

  tools.onMouseDown(200, 200, "select");
  assert.equal(tools.selectedId, null);
});

test("delete removes the selection and nothing else", () => {
  const { doc, tools } = controller();
  tools.onMouseDown(0, 0, "dress:tree");
  tools.onMouseDown(30, 30, "dress:boulder");
  assert.ok(tools.deleteSelected());
  assert.equal(doc.props.length, 1);
  assert.equal(doc.props[0].kind, "tree");
  assert.ok(!tools.deleteSelected());
});
