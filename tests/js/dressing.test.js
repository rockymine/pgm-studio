// Placed dressing (dressing/dressing-doc.js + controllers/dressing-controller.js) — the half of the phase
// that has no server in it. What is worth asserting is the interaction: that a drag places one thing and
// ends when the button comes up, that a click drops a marker, and that the next thing placed inherits the
// settings the last one was given — which is what lets an author place a stand of ten oaks.
import { test } from "node:test";
import assert from "node:assert/strict";

import { DressingDoc, defaultProp, isMarker, isRect, MAX_FOOTPRINT, propAnchor, rectFootprint, translateProp }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/dressing/dressing-doc.js";
import { DressingController, DRESSING_TOOLS }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/dressing-controller.js";

// No handle layer and no viewport: the point grips are the one DOM-bearing part of the controller, and
// `refreshHandles` is a no-op without a layer — so everything below exercises the interaction, not the SVG.
const controller = (callbacks = {}) => {
  const doc = new DressingDoc();
  return { doc, tools: new DressingController(doc, null, null, callbacks) };
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
  // Water is drawn like a path but cuts a bed and fills it: a plain canal is what the other forms vary on.
  assert.equal(defaultProp("water").form, "canal");
  assert.equal(defaultProp("water").depth, 2);
  assert.equal(defaultProp("water").shoreWander, true);
  // Its bank (bed floor + beach) is a full terrain material, not one block — a cellular voronoi by default.
  assert.equal(defaultProp("water").bank.kind, "voronoi");
  assert.equal(defaultProp("water").bank.bands.length, 3);
  // So are a path's paving and a boulder's rock, which is what lets either take a pattern.
  assert.equal(defaultProp("path").pave.kind, "solid");
  assert.equal(defaultProp("boulder").rock.kind, "solid");
  // A tree starts vanilla — the two forms are two trees, and the vanilla one is what a map is mostly made of.
  assert.equal(defaultProp("tree").form, "template");
  assert.equal(defaultProp("tree").species, "oak");
  assert.equal(defaultProp("tree").wood, "oak");
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

test("a marker cannot be dropped into the void — it must land on terrain", () => {
  // A tree seats on the ground; the export refuses one placed on nothing, so the canvas refuses it first. The
  // terrain predicate the canvas supplies here says only the right half is land.
  const doc = new DressingDoc();
  const tools = new DressingController(doc, null, null, { onTerrain: (bx) => bx >= 0 });

  tools.onMouseDown(-10, 5, "dress:tree");     // over the void
  assert.equal(doc.props.length, 0);           // nothing placed
  tools.onMouseDown(10, 5, "dress:boulder");   // over the land
  assert.equal(doc.props.length, 1);
  assert.deepEqual([doc.props[0].x, doc.props[0].z], [10, 5]);
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

test("a water channel is dragged as an open line, the same way a path is", () => {
  // Water shares the path's press-trace-release: an open route kept in draw order, not a closed ring.
  const { doc, tools } = controller();
  drag(tools, "dress:water", [[0, 0], [6, 3], [12, 2], [20, 6]]);
  assert.equal(doc.props.length, 1);
  assert.equal(doc.props[0].kind, "water");
  assert.ok(doc.props[0].points.length >= 2);
  assert.deepEqual(doc.props[0].points[0], [0, 0]);
  assert.deepEqual(doc.props[0].points.at(-1), [20, 6]);
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
  tools.updateSelected({ radius: 6, style: "rough" });

  drag(tools, "dress:path", [[0, 40], [10, 40], [20, 40]]);
  assert.equal(doc.props[1].radius, 6);
  assert.equal(doc.props[1].style, "rough");
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

test("a placement ends its tool, so the next click picks the prop instead of dropping another", () => {
  // The bug this replaced: the tree tool stayed armed, so clicking the tree you just placed planted a second
  // one on top of it and there was no obvious way to pick one up.
  let placed = 0;
  const { tools } = controller({ onPlaced: () => placed++ });
  tools.onMouseDown(4, 4, "dress:tree");
  assert.equal(placed, 1);
  drag(tools, "dress:path", [[0, 0], [10, 0], [20, 0]]);
  assert.equal(placed, 2);
  tools.updateSelected({ radius: 5 });        // a knob edit is not a placement
  assert.equal(placed, 2);
});

test("a route's points are draggable one at a time — the band follows the line", () => {
  const { doc, tools } = controller();
  drag(tools, "dress:path", [[0, 0], [10, 0], [20, 0]]);
  const id = tools.selectedId;
  const before = doc.byId(id).points.length;

  assert.ok(tools.beginPointDrag(id, 1));
  tools.onHandleMove(10.4, 7.6);
  assert.ok(tools.onHandleUp());
  assert.deepEqual(doc.byId(id).points[1], [10, 8]);   // block-snapped where the cursor was
  assert.equal(doc.byId(id).points.length, before);    // dragging a point never adds or drops one
  assert.ok(!tools.onHandleUp());                      // the drag is over — nothing left to release
});

test("an area's outline is reshaped the same way, so a ground cover can be corrected in place", () => {
  const { doc, tools } = controller();
  drag(tools, "dress:flora", [[0, 0], [20, 0], [20, 20], [0, 20]]);
  const id = tools.selectedId;

  tools.beginPointDrag(id, 0);
  tools.onHandleMove(-12, -9);
  tools.onHandleUp();
  assert.deepEqual(doc.byId(id).points[0], [-12, -9]);
});

test("a marker's grip drags it, and stops at the terrain edge like every other way of moving one", () => {
  const doc = new DressingDoc();
  const tools = new DressingController(doc, null, null, { onTerrain: (bx) => bx >= 0 });
  tools.onMouseDown(10, 10, "dress:tree");
  const id = tools.selectedId;

  tools.beginPointDrag(id, -1);      // -1 is the anchor: a marker has one point and it is where it stands
  tools.onHandleMove(24, 18);
  assert.deepEqual([doc.byId(id).x, doc.byId(id).z], [24, 18]);
  tools.onHandleMove(-30, 18);       // over the void — the drag simply doesn't follow
  assert.deepEqual([doc.byId(id).x, doc.byId(id).z], [24, 18]);
  tools.onHandleUp();
});

test("a point drag on a prop that is gone releases instead of throwing", () => {
  const { tools } = controller();
  tools.onMouseDown(0, 0, "dress:tree");
  const id = tools.selectedId;
  tools.beginPointDrag(id, -1);
  tools.deleteSelected();
  assert.ok(!tools.onHandleMove(5, 5));
  assert.ok(!tools.onHandleUp());
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

// ── buildings: the third interaction ──────────────────────────────────────────
test("a building is a rectangle, and reads the same whichever corner it was dragged from", () => {
  assert.equal(isRect("house"), true);
  assert.equal(isRect("flora"), false);

  const forward = rectFootprint({ points: [[4, 6], [12, 14]] });
  assert.deepEqual(forward, { minX: 4, minZ: 6, width: 9, depth: 9 });
  assert.deepEqual(rectFootprint({ points: [[12, 14], [4, 6]] }), forward);
  assert.deepEqual(rectFootprint({ points: [[12, 6], [4, 14]] }), forward);
});

test("a rectangle too small to hold two walls and an inside is no footprint at all", () => {
  assert.equal(rectFootprint({ points: [[0, 0], [1, 8]] }), null);
  assert.notEqual(rectFootprint({ points: [[0, 0], [2, 2]] }), null);
  assert.equal(rectFootprint({ points: [[0, 0]] }), null);
});

test("dragging a building keeps two corners however far the pointer wandered", () => {
  // A rectangle's second corner is rewritten by every move where a traced outline appends one — otherwise a
  // wandering drag would store a hundred points the stamp has no use for.
  const { doc, tools } = controller();
  drag(tools, "dress:house", [[2, 3], [5, 6], [9, 4], [12, 9]]);

  assert.equal(doc.props.length, 1);
  assert.equal(doc.props[0].kind, "house");
  assert.equal(doc.props[0].points.length, 2);
  assert.deepEqual(rectFootprint(doc.props[0]), { minX: 2, minZ: 3, width: 11, depth: 7 });
});

test("a building drag too small to stand up places nothing", () => {
  const { doc, tools } = controller();
  drag(tools, "dress:house", [[4, 4], [5, 9]]);
  assert.equal(doc.props.length, 0);
});

test("a building moves as a whole, corners together", () => {
  const moved = translateProp({ kind: "house", points: [[2, 3], [10, 9]] }, 5, -2);
  assert.deepEqual(moved.points, [[7, 1], [15, 7]]);
});

test("a building tool is a tool like any other", () => {
  assert.equal(DRESSING_TOOLS["dress:house"], "house");
  assert.equal(isMarker("house"), false);
  assert.deepEqual(defaultProp("house").points, []);
  assert.equal(defaultProp("house").front, null);
});

test("a building larger than a small house is refused, and the canvas refuses the same one the stamp does", () => {
  assert.equal(MAX_FOOTPRINT, 192);
  assert.notEqual(rectFootprint({ points: [[0, 0], [11, 15]] }), null);   // 12x16, the largest there is
  assert.equal(rectFootprint({ points: [[0, 0], [11, 16]] }), null);      // 12x17
  assert.notEqual(rectFootprint({ points: [[0, 0], [15, 11]] }), null);   // the same rectangle turned
  assert.equal(rectFootprint({ points: [[0, 0], [19, 29]] }), null);      // 20x30, a building, not scenery
});

test("a building drag past the cap places nothing", () => {
  const { doc, tools } = controller();
  drag(tools, "dress:house", [[0, 0], [40, 40]]);
  assert.equal(doc.props.length, 0);
});
