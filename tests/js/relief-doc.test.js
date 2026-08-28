// The relief document and the tools that state into it. What these hold is the half of the model the C# side
// cannot: a mark belongs to an ISLAND, and the whole document is exactly what the layout stores — so a mark
// placed on the canvas and one written by hand are the same mark.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { ReliefDoc, defaultMark, markAnchor, markPoints, markReach, pointsPatch, translateMark, isRing, isSpot,
         isPush, pushAmounts, pushAmountPatch, FALLBACK_BASE }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/relief-doc.js";
import { ReliefController, RELIEF_TOOLS }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/controllers/relief-controller.js";
import { paintReliefMarks, heightColor, liftColor }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/relief-render.js";
import { contourAt, markFromDrag }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/contour-drag.js";

// ── the document ──────────────────────────────────────────────────────────────
test("a stored relief round-trips through the document", () => {
  const stored = {
    i1: { base: 6, reach: 20, step: 1, marks: [{ kind: "point", at: [4, 4], h: 12, r: 3 }] },
  };
  const doc = ReliefDoc.from(stored);
  const out = doc.toJSON();
  assert.equal(out.i1.base, 6);
  assert.equal(out.i1.marks.length, 1);
  assert.deepEqual(out.i1.marks[0].at, [4, 4]);
});

test("a mark kind the client cannot draw is dropped rather than carried", () => {
  const doc = ReliefDoc.from({ i1: { marks: [{ kind: "point", at: [0, 0], h: 5 }, { kind: "wormhole" }] } });
  assert.equal(doc.marks.length, 1);
});

test("a stored mark keeps its id, and one without gets one", () => {
  const doc = ReliefDoc.from({ i1: { marks: [{ kind: "point", id: "r7", at: [0, 0], h: 5 }, { kind: "area", ring: [[0, 0], [2, 0], [2, 2]], h: 4 }] } });
  const [first, second] = doc.marks;
  assert.equal(first.id, "r7");
  assert.ok(second.id);
  // Minted above the highest stored id, so re-opening a document cannot hand out one that is already taken.
  assert.notEqual(second.id, "r7");
});

test("an island that states nothing is left out of the stored form entirely", () => {
  // Opening the phase and closing it again must not add a key to the layout.
  const doc = new ReliefDoc();
  doc.reliefOf("i1");
  assert.deepEqual(doc.toJSON(), {});
  assert.equal(doc.isEmpty, true);
});

test("ids are unique across islands, not per island", () => {
  // A mark can be dragged out of one island and into another; an id that changed on the way would break the
  // selection mid-drag.
  const doc = new ReliefDoc();
  const a = doc.add("i1", defaultMark("point"));
  const b = doc.add("i2", defaultMark("point"));
  assert.notEqual(a.id, b.id);
  assert.equal(doc.islandOf(a.id), "i1");
  assert.equal(doc.islandOf(b.id), "i2");
});

test("an update cannot change a mark's kind or id", () => {
  const doc = new ReliefDoc();
  const placed = doc.add("i1", defaultMark("point"));
  const updated = doc.update(placed.id, { kind: "scarp", id: "hijack", h: 14 });
  assert.equal(updated.kind, "point");
  assert.equal(updated.id, placed.id);
  assert.equal(updated.h, 14);
});

test("points live in the field their kind names them", () => {
  // An area's points close and a line's do not, so the wire format says which by the name it stores them under.
  assert.deepEqual(pointsPatch(defaultMark("area"), [[0, 0], [2, 0], [2, 2]]), { ring: [[0, 0], [2, 0], [2, 2]] });
  assert.deepEqual(pointsPatch(defaultMark("line"), [[0, 0], [4, 0]]), { points: [[0, 0], [4, 0]] });
  assert.deepEqual(pointsPatch(defaultMark("point"), [[3, 5]]), { at: [3, 5] });
  assert.ok(isRing("area") && !isRing("line"));
  assert.ok(isSpot("point") && !isSpot("area"));
});

test("a mark moves point by point, so a drag keeps its shape", () => {
  const line = { ...defaultMark("line"), points: [[0, 0], [10, 4]] };
  assert.deepEqual(markPoints(translateMark(line, 3, -2)), [[3, -2], [13, 2]]);
  const spot = { ...defaultMark("point"), at: [5, 5] };
  assert.deepEqual(translateMark(spot, 2, 2).at, [7, 7]);
});

test("a mark's reach counts the band it holds, not only its points", () => {
  // A hit test has to reach the ground a mark states, not the line an author drew through the middle of it.
  const narrow = { ...defaultMark("line"), points: [[0, 0], [10, 0]], r: 1 };
  const broad = { ...defaultMark("line"), points: [[0, 0], [10, 0]], r: 8 };
  assert.ok(markReach(broad) > markReach(narrow));
  assert.deepEqual(markAnchor(narrow), [5, 0]);
});

// ── the tools ─────────────────────────────────────────────────────────────────
function tools({ islandAt = () => "i1", islandTop = () => null, doc = new ReliefDoc(),
                contours = () => null } = {}) {
  const events = [];
  const controller = new ReliefController(doc, null, () => ({ scale: 1, panX: 0, panY: 0 }), {
    onChanged: () => events.push("changed"),
    onSelected: (id) => events.push(`selected:${id}`),
    onPlaced: () => events.push("placed"),
    onIslandAt: islandAt,
    onIslandTop: islandTop,
    onContours: contours,
  });
  return { controller, doc, events };
}

test("a click places a spot height in the island under it", () => {
  const { controller, doc, events } = tools();
  assert.equal(controller.onMouseDown(12, 8, "relief:point"), true);
  assert.equal(doc.marks.length, 1);
  assert.deepEqual(doc.marks[0].at, [12, 8]);
  assert.equal(doc.marks[0].islandId, "i1");
  assert.ok(events.includes("placed"));
});

test("a click off every island states nothing", () => {
  // A relief is stated INSIDE an island. Off one there is no ground and no island to state it about, so the
  // press is consumed and nothing is begun.
  const { controller, doc } = tools({ islandAt: () => null });
  assert.equal(controller.onMouseDown(12, 8, "relief:point"), true);
  assert.equal(doc.marks.length, 0);
});

test("a drag traces a ridgeline and simplifies it on release", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:line");
  for (let x = 1; x <= 20; x++) controller.onMouseMove(x, 0, "relief:line");
  controller.onMouseUp();
  assert.equal(doc.marks.length, 1);
  // A straight drag is two points once the bends nobody made are dropped.
  assert.equal(markPoints(doc.marks[0]).length, 2);
});

test("a drag too small to enclose anything is a misfire, not a tiny mark", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(4, 4, "relief:area");
  controller.onMouseMove(5, 4, "relief:area");
  controller.onMouseUp();
  assert.equal(doc.marks.length, 0);
});

test("the island is decided where the trace STARTS, not where most of it lands", () => {
  // A mark is clipped, not confined: one dragged past an edge raises the ground into a corner and stops. If
  // ownership followed coverage, that authoring gesture would pick whichever island the overhang crossed.
  const { controller, doc } = tools({ islandAt: (x) => (x < 10 ? "i1" : "i2") });
  controller.onMouseDown(2, 0, "relief:line");
  for (let x = 3; x <= 40; x++) controller.onMouseMove(x, 0, "relief:line");
  controller.onMouseUp();
  assert.equal(doc.marks[0].islandId, "i1");
});

test("a fresh relief stands at the level its island already does", () => {
  // A relief REPLACES the top of every column of its island, so a base read from anywhere but the island
  // moves the whole landmass the moment a mark lands. An island drawn at 20 gets a relief at 20.
  const doc = new ReliefDoc();
  assert.equal(doc.reliefOf("i1", 20).base, 20);
  assert.equal(doc.reliefOf("i2").base, FALLBACK_BASE);       // nothing to read from
});

test("a stored line mark's reach is read under the one name it has", () => {
  // `width` is what a line mark was stored under and what the C# reader still accepts. Normalising on load
  // is what keeps the renderer, the hit test and the inspector asking one question by one name.
  const doc = ReliefDoc.from({
    i1: { base: 20, marks: [{ id: "r1", kind: "line", points: [[0, 0], [10, 0]], h: 22, width: 6 }] },
  });
  const stored = doc.byId("r1");
  assert.equal(stored.r, 6);
  assert.equal(stored.width, undefined);
  assert.ok(markReach(stored) > markReach({ ...stored, r: 1 }));
});

test("a first mark in an island starts at that island's base, not the last one's height", () => {
  // A height carried over from another island at another base would state a cliff nobody asked for.
  const doc = ReliefDoc.from({ i2: { base: 20, marks: [] } });
  const { controller } = tools({ doc, islandAt: () => "i2" });
  controller.onMouseDown(0, 0, "relief:point");
  assert.equal(doc.marks[0].h, 20);
});

test("a first mark in an untouched island starts at the level that island stands at", () => {
  // The island has no relief yet, so there is no stated base to read: the level comes from the ground the
  // author already drew, and the mark lands flush with it instead of twelve blocks under it.
  const { controller, doc } = tools({ islandAt: () => "i7", islandTop: () => 20 });
  controller.onMouseDown(0, 0, "relief:point");
  assert.equal(doc.marks[0].h, 20);
  assert.equal(doc.peek("i7").base, 20);
});

test("a base typed before any mark is stated against the island, not a constant", () => {
  // Reaching an island's settings must not depend on having placed something in it first.
  const { controller, doc } = tools({ islandTop: () => 24 });
  controller.updateRelief("i1", { reach: 30 });
  assert.equal(doc.peek("i1").base, 24);
});

test("a later mark in the same island carries the tool's settings forward", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:point");
  controller.updateSelected({ h: 17, r: 9 });
  controller.onMouseDown(20, 20, "relief:point");
  assert.equal(doc.marks[1].h, 17);
  assert.equal(doc.marks[1].r, 9);
});

test("a selected mark can be dragged past its island's edge", () => {
  // The one place this differs from a placed prop, which stops at the void: a hill authored into a corner IS
  // a mark hanging over the edge.
  const { controller, doc } = tools({ islandAt: (x) => (x < 10 ? "i1" : null) });
  controller.onMouseDown(4, 4, "relief:point");
  controller.setTool("select");
  controller.onMouseDown(4, 4, "select");
  controller.onMouseMove(40, 4, "select");
  controller.onMouseUp();
  assert.deepEqual(doc.marks[0].at, [40, 4]);
});

// ── point grips ───────────────────────────────────────────────────────────────
// A mark is reshaped the way a drawn polygon is: one grip per point, dragged in world coordinates. Without
// these a traced ridgeline that runs two blocks wide of where it was wanted has to be traced again.
test("a grip moves the point it holds and leaves the rest alone", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:line");
  controller.onMouseMove(10, 0, "relief:line");
  controller.onMouseMove(20, 0, "relief:line");
  controller.onMouseUp();

  const id = doc.marks[0].id;
  assert.equal(controller.beginPointDrag(id, 1), true);
  assert.equal(controller.onHandleMove(20.4, 9.6), true);
  assert.equal(controller.onHandleUp(), true);

  // Rounded to the block, because a mark states cells and a point between two of them states neither.
  assert.deepEqual(markPoints(doc.marks[0]), [[0, 0], [20, 10]]);
});

test("a grip writes back to the field its kind names, not to a shared one", () => {
  // A bench keeps its points in `ring` and a ridgeline in `points`. A grip that wrote to the wrong one would
  // leave the mark looking edited on the canvas and unchanged in the solve.
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:area");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10]]) controller.onMouseMove(x, z, "relief:area");
  controller.onMouseUp();

  const bench = doc.marks[0];
  controller.beginPointDrag(bench.id, 0);
  controller.onHandleMove(-6, -6);
  controller.onHandleUp();

  assert.deepEqual(doc.toJSON().i1.marks[0].ring[0], [-6, -6]);
  assert.equal(doc.toJSON().i1.marks[0].points, undefined);
});

test("a spot height has exactly one grip, and it moves the spot", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(4, 4, "relief:point");
  const spot = doc.marks[0];
  assert.equal(markPoints(spot).length, 1);

  controller.beginPointDrag(spot.id, 0);
  controller.onHandleMove(9, 12);
  controller.onHandleUp();
  assert.deepEqual(doc.marks[0].at, [9, 12]);
});

test("a grip may be dragged off the island — a mark is clipped, not confined", () => {
  // The same rule the body drag follows, and the reason both exist: a hill authored into a corner with no
  // wasted ground behind it IS a mark whose points hang over the edge.
  const { controller, doc } = tools({ islandAt: (x) => (x < 10 ? "i1" : null) });
  controller.onMouseDown(0, 0, "relief:line");
  controller.onMouseMove(8, 0, "relief:line");
  controller.onMouseUp();

  const id = doc.marks[0].id;
  controller.beginPointDrag(id, 1);
  controller.onHandleMove(60, 0);
  controller.onHandleUp();
  assert.deepEqual(markPoints(doc.marks[0])[1], [60, 0]);
});

test("a grip that never moved reports no change", () => {
  // Grabbing a grip and letting go is not an edit, and reporting one would mark the sketch dirty and post a
  // fresh solve for a surface nobody touched.
  const { controller, doc, events } = tools();
  controller.onMouseDown(4, 4, "relief:point");
  const before = events.filter(event => event === "changed").length;
  controller.beginPointDrag(doc.marks[0].id, 0);
  assert.equal(controller.onHandleUp(), true);
  assert.equal(events.filter(event => event === "changed").length, before);
});

test("a grip on a mark that is gone releases rather than throwing", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(4, 4, "relief:point");
  const id = doc.marks[0].id;
  controller.beginPointDrag(id, 0);
  doc.remove(id);
  assert.equal(controller.onHandleMove(9, 9), false);
});

test("deleting the selection removes it and clears the selection", () => {
  const { controller, doc, events } = tools();
  controller.onMouseDown(0, 0, "relief:point");
  assert.equal(controller.deleteSelected(), true);
  assert.equal(doc.marks.length, 0);
  assert.ok(events.includes("selected:null"));
});

test("island settings are edited through the document, not through a mark", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:point");
  controller.updateRelief("i1", { base: 14, reach: 30 });
  assert.equal(doc.toJSON().i1.base, 14);
  assert.equal(doc.toJSON().i1.reach, 30);
});

// ── the paint ─────────────────────────────────────────────────────────────────
test("a mark is drawn as the ground it holds, and wears its height", () => {
  const painter = recordingPainter({ scale: 8 });
  paintReliefMarks(painter, [{ id: "r1", islandId: "i1", kind: "point", at: [5, 5], h: 14, r: 3 }], { baseOf: () => 8 });
  assert.equal(painter.of("ring").length, 1);
  assert.deepEqual(painter.of("text")[0].slice(0, 3), ["14", 5, 5]);
});

test("a falling ridgeline wears both of its ends", () => {
  const painter = recordingPainter({ scale: 8 });
  paintReliefMarks(painter, [{ id: "r1", islandId: "i1", kind: "line", points: [[0, 0], [10, 0]], h: [16, 9], r: 2 }]);
  assert.equal(painter.of("text")[0][0], "16–9");
});

test("a label holds its size as the board is zoomed", () => {
  // Asked in world units a label grows with the map, which is what put an eleven-BLOCK number on a hundred-
  // block island. What a mark states does not get bigger as the board is zoomed into.
  const sizeAt = (scale) => {
    const painter = recordingPainter({ scale });
    paintReliefMarks(painter, [{ id: "r1", islandId: "i1", kind: "area",
                                 ring: [[0, 0], [40, 0], [40, 40], [0, 40]], h: 22 }], { baseOf: () => 20 });
    const call = painter.of("text")[0];
    return call[call.length - 1].size * scale;
  };
  assert.equal(sizeAt(2), sizeAt(16));
});

test("a spot's number rides clear of its own grip", () => {
  // A spot's anchor is where its point grip sits, so a label centred on it is drawn under the grip. Every
  // other kind is anchored on ground it covers and reads centred.
  const painter = recordingPainter({ scale: 8 });
  paintReliefMarks(painter, [
    { id: "r1", islandId: "i1", kind: "point", at: [5, 5], h: 22, r: 4 },
    { id: "r2", islandId: "i1", kind: "area", ring: [[0, 0], [40, 0], [40, 40], [0, 40]], h: 22 },
  ], { baseOf: () => 20 });
  const [spot, bench] = painter.of("text").map(call => call[call.length - 1]);
  assert.ok(spot.dy < -4, `a spot lifts its label, got dy ${spot.dy}`);
  assert.ok(!bench.dy, `a bench centres its label, got dy ${bench.dy}`);
});

test("a mark drawn smaller than its own number wears none", () => {
  // At a zoom where a mark is a dot, a number over it is wider than the thing it labels.
  const painter = recordingPainter({ scale: 0.5 });
  paintReliefMarks(painter, [{ id: "r1", islandId: "i1", kind: "point", at: [5, 5], h: 22, r: 3 }],
                   { baseOf: () => 20 });
  assert.equal(painter.of("ring").length, 1);      // the ground it holds is still drawn
  assert.equal(painter.of("text").length, 0);
});

test("a scarp wears its drop", () => {
  const painter = recordingPainter({ scale: 8 });
  paintReliefMarks(painter, [{ id: "r1", islandId: "i1", kind: "scarp", points: [[0, 0], [10, 0]], high: 18, low: 6 }]);
  assert.equal(painter.of("text")[0][0], "18↓6");
});

test("colour carries the height: a rise and a cut are not two shades of one hue", () => {
  const rise = heightColor(20, 8), flat = heightColor(8, 8), cut = heightColor(-4, 8);
  assert.notEqual(rise, flat);
  assert.notEqual(cut, flat);
  assert.notEqual(rise, cut);
});

// ── pushes ────────────────────────────────────────────────────────────────────
// A push is placed like a bench and does something else entirely: it MOVES the solved surface rather than
// pinning it, so two over the same ground add where two marks would have to argue. What these hold is that
// the shared pipeline carries it without either half leaking into the other.
test("a push is stored in its own array, and without a kind", () => {
  // The array it sits in already says what it is; a stored field repeating that is one more thing able to
  // disagree with it.
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:push");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10]]) controller.onMouseMove(x, z, "relief:push");
  controller.onMouseUp();

  const stored = doc.toJSON().i1;
  assert.equal(stored.marks.length, 0);
  assert.equal(stored.pushes.length, 1);
  assert.equal(stored.pushes[0].kind, undefined);
  // Handed out, it wears the word — everything downstream asks a statement what it is, not where it came from.
  assert.equal(doc.statements[0].kind, "push");
});

test("marks and pushes are selected, edited and deleted through one pipeline", () => {
  const { controller, doc } = tools();
  controller.onMouseDown(4, 4, "relief:point");
  controller.onMouseDown(0, 0, "relief:push");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10]]) controller.onMouseMove(x, z, "relief:push");
  controller.onMouseUp();

  assert.equal(doc.statements.length, 2);
  const push = doc.statements.find(isPush);
  controller.select(push.id);
  controller.updateSelected({ amount: -6 });
  assert.equal(doc.toJSON().i1.pushes[0].amount, -6);

  assert.equal(controller.deleteSelected(), true);
  assert.equal(doc.toJSON().i1.pushes.length, 0);
  assert.equal(doc.toJSON().i1.marks.length, 1);   // the mark is untouched
});

test("a push needs no re-basing — it states a lift, not a level", () => {
  // A mark placed first in an island takes that island's base. A push must not: five blocks up is five
  // blocks up wherever it is drawn, and re-basing it would turn its lift into an elevation.
  const doc = ReliefDoc.from({ i1: { base: 30, marks: [], pushes: [] } });
  const { controller } = tools({ doc });
  controller.onMouseDown(0, 0, "relief:push");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10]]) controller.onMouseMove(x, z, "relief:push");
  controller.onMouseUp();
  assert.equal(doc.statements.find(isPush).amount, 5);
});

test("per-vertex lifts expand to one number per ring corner", () => {
  const push = { kind: "push", ring: [[0, 0], [10, 0], [10, 10], [0, 10]], amount: 4 };
  assert.deepEqual(pushAmounts(push), [4, 4, 4, 4]);
  assert.deepEqual(pushAmounts({ ...push, amounts: [6, 2] }), [6, 2, 2, 2]);
});

test("varying one corner writes an array; levelling them collapses it back", () => {
  // The way out matters as much as the way in: an author who undoes a variation should be left with the push
  // they started from, not with an array that happens to be flat.
  const push = { kind: "push", ring: [[0, 0], [10, 0], [10, 10], [0, 10]], amount: 4 };
  const varied = pushAmountPatch(push, 2, 9);
  assert.deepEqual(varied.amounts, [4, 4, 9, 4]);

  const levelled = pushAmountPatch({ ...push, ...varied }, 2, 4);
  assert.equal(levelled.amounts, undefined);
  assert.equal(levelled.amount, 4);
});

test("an undefined in a patch un-states the field rather than storing an undefined", () => {
  // How a per-corner array collapses back to one number, through the document rather than around it.
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:push");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10]]) controller.onMouseMove(x, z, "relief:push");
  controller.onMouseUp();

  const id = doc.statements.find(isPush).id;
  controller.select(id);
  controller.updateSelected({ amount: 4, amounts: [4, 9, 4] });
  assert.deepEqual(doc.toJSON().i1.pushes[0].amounts, [4, 9, 4]);

  controller.updateSelected({ amount: 4, amounts: undefined });
  assert.equal("amounts" in doc.toJSON().i1.pushes[0], false);
});

test("a per-vertex array is never carried to the next push of the same kind", () => {
  // Settings carry forward so an author can draw six pushes without configuring six. A per-corner array is
  // sized to the ring it was stated on, so carrying it would hand a fresh four-corner ring numbers it cannot
  // mean — and the same goes for a ridgeline whose heights have become one per vertex.
  const { controller, doc } = tools();
  controller.onMouseDown(0, 0, "relief:push");
  for (const [x, z] of [[10, 0], [10, 10], [0, 10], [5, 14]]) controller.onMouseMove(x, z, "relief:push");
  controller.onMouseUp();
  controller.updateSelected({ amount: 4, amounts: [4, 9, 4, 1] });

  assert.equal(controller.settingsFor("push").amounts, undefined);
  assert.deepEqual(controller.settingsFor("push").ring, []);   // never the ring that was drawn
  assert.equal(controller.settingsFor("push").amount, 4);      // the plain number does carry
});

test("a push's reach counts its skirt, so a click on the slope finds it", () => {
  const push = { kind: "push", ring: [[0, 0], [10, 0], [10, 10], [0, 10]], amount: 5, falloff: 12 };
  const tight = { ...push, falloff: 0 };
  assert.ok(markReach(push) > markReach(tight) + 10);
});

test("a push wears its lift with a sign, and a varying one wears both ends", () => {
  const painter = recordingPainter({ scale: 8 });
  paintReliefMarks(painter, [
    { id: "p1", islandId: "i1", kind: "push", ring: [[0, 0], [10, 0], [10, 10], [0, 10]], amount: 5, falloff: 0 },
  ], { baseOf: () => 8 });
  assert.equal(painter.of("text")[0][0], "+5");

  const other = recordingPainter({ scale: 8 });
  paintReliefMarks(other, [
    { id: "p2", islandId: "i1", kind: "push", ring: [[0, 0], [10, 0], [10, 10], [0, 10]],
      amount: 6, amounts: [6, 6, 1, 1], falloff: 0 },
  ], { baseOf: () => 8 });
  assert.equal(other.of("text")[0][0], "+6→+1");
});

test("a push draws its skirt outside the ring, whichever way the ring was traced", () => {
  // Which side is "out" comes from the ring's own winding. Traced the other way round, an unflipped offset
  // would draw the skirt INSIDE the push, which reads as a smaller push.
  const clockwise = [[0, 0], [10, 0], [10, 10], [0, 10]];
  for (const ring of [clockwise, [...clockwise].reverse()]) {
    const painter = recordingPainter();
    paintReliefMarks(painter, [{ id: "p1", islandId: "i1", kind: "push", ring, amount: 5, falloff: 6 }],
                     { baseOf: () => 8 });
    // Two rings are drawn: the skirt and the ring itself. Whichever order they come in, the skirt is the
    // outer one and it has to clear the drawn ring by about the falloff — inside it would read as a
    // SMALLER push, which is the failure a flipped winding produces.
    const extents = painter.of("ring").map(([points]) => Math.max(...points.map(([x]) => x)));
    assert.equal(extents.length, 2);
    const [inner, outer] = [Math.min(...extents), Math.max(...extents)];
    assert.equal(inner, 10);                                    // the ring as traced
    assert.ok(outer >= 15 && outer <= 18, `skirt reached ${outer}, expected about 16`);
  }
});

test("a lift is coloured from zero, so a push and a mark at the same height agree", () => {
  assert.equal(liftColor(5), heightColor(13, 8));
  assert.equal(liftColor(-5), heightColor(3, 8));
});

// ── dragging a contour ────────────────────────────────────────────────────────
// The one interaction that starts from what the SOLVER produced rather than from an empty tool. A contour is a
// line of constant height, so moving one says the ground reaches that height here now — which is a line mark
// at that level, and what makes the reading of a surface and the way it is edited the same object.
const CONTOURS = {
  islands: [{
    island: "i1",
    lines: [
      // A plain line at z = 20 and an index line at z = 24, both running the width of the board.
      { level: 7, closed: false, points: [0, 20, 20, 20, 40, 20] },
      { level: 10, closed: false, points: [0, 24, 20, 24, 40, 24] },
    ],
  }],
};

test("a press near a contour grabs it, and one away from every contour grabs nothing", () => {
  assert.equal(contourAt(CONTOURS, 20, 20)?.level, 7);
  assert.equal(contourAt(CONTOURS, 20, 40), null);
  assert.equal(contourAt(null, 20, 20), null);
});

test("an index line wins a press between two contours", () => {
  // Several run close together on a steep face — that is what steep means — so a plain nearest test hands an
  // author whichever one the cursor fell on the near side of. The labelled ones are the ones aimable at.
  const between = contourAt(CONTOURS, 20, 21.8);   // nearer the level-7 line, but the 10 is the index line
  assert.equal(between.level, 10);
  assert.equal(between.index, true);

  // Far enough onto the plain line and it still wins: the preference is a nudge, not an override.
  assert.equal(contourAt(CONTOURS, 20, 20).level, 7);
});

test("a dragged contour becomes a line mark at that contour's own height", () => {
  const grabbed = contourAt(CONTOURS, 20, 20);
  const stated = markFromDrag(grabbed, 0, -6);

  assert.equal(stated.islandId, "i1");
  assert.equal(stated.mark.kind, "line");
  assert.equal(stated.mark.h, 7);                       // the height it already was
  assert.deepEqual(stated.mark.points[0], [0, 14]);     // moved by the drag
  assert.deepEqual(stated.mark.points.at(-1), [40, 14]);
});

test("a contour pressed and released without moving states nothing", () => {
  const grabbed = contourAt(CONTOURS, 20, 20);
  assert.equal(markFromDrag(grabbed, 0, 0), null);
  assert.equal(markFromDrag(grabbed, 0.4, 0.2), null);
  assert.equal(markFromDrag(null, 5, 5), null);
});

test("a dragged contour is simplified, not stored as the tracer drew it", () => {
  // A traced contour is one point per cell boundary. Stored raw it is a mark with hundreds of grips, which is
  // not a thing an author can then edit.
  const dense = { islands: [{ island: "i1", lines: [{ level: 5, closed: false,
    points: Array.from({ length: 120 }, (_, i) => (i % 2 === 0 ? i : 30)) }] }] };
  const stated = markFromDrag(contourAt(dense, 20, 30), 0, -4);
  assert.ok(stated.mark.points.length < 8, `kept ${stated.mark.points.length} points`);
});

test("a mark under the cursor wins the press over a contour beneath it", () => {
  // A mark is a thing an author put there; the contours are what the solver made of them. Grabbing the
  // contour instead would make a placed mark unselectable wherever its own contour crossed it.
  const { controller, doc } = tools({ contours: () => CONTOURS });
  controller.onMouseDown(20, 20, "relief:point");     // a spot height right on the level-7 line
  const placed = doc.marks[0].id;

  controller.onMouseDown(20, 20, "select");
  assert.equal(controller.selectedId, placed);
});

test("dragging a contour places the mark and selects it", () => {
  const { controller, doc, events } = tools({ contours: () => CONTOURS });
  controller.onMouseDown(20, 24, "select");
  controller.onMouseMove(20, 32, "select");
  controller.onMouseUp();

  assert.equal(doc.marks.length, 1);
  assert.equal(doc.marks[0].kind, "line");
  assert.equal(doc.marks[0].h, 10);
  assert.equal(controller.selectedId, doc.marks[0].id);
  assert.ok(events.includes("changed"));
});

test("a mark is ghosted only on an island that mirrors", () => {
  // The rasterizer fans only the islands that opted in, so ghosting a mark on one that did not draws ground
  // the export will never build — the one thing an overlay must not do.
  const mark = { id: "r1", islandId: "i1", kind: "point", at: [5, 5], h: 12, r: 3 };

  const mirrored = recordingPainter();
  paintReliefMarks(mirrored, [mark], { baseOf: () => 8, orderOf: () => 2, mirrorPoint: (x, z) => [x + 50, z] });
  assert.equal(mirrored.of("ring").length, 2);

  const alone = recordingPainter();
  paintReliefMarks(alone, [mark], { baseOf: () => 8, orderOf: () => 1, mirrorPoint: (x, z) => [x + 50, z] });
  assert.equal(alone.of("ring").length, 1);

  // No order at all is one image, not none: a caller that has not been taught about mirroring should draw
  // the mark rather than nothing.
  const bare = recordingPainter();
  paintReliefMarks(bare, [mark], { baseOf: () => 8 });
  assert.equal(bare.of("ring").length, 1);
});
