// The transform box every authoring surface wears. The claims under test are the ones that make a box
// safe to draw ON a selection's bounds rather than offset outside it: that it puts an anchor on each
// corner and NOWHERE else, that each edge is a band driving one axis, and that a band never reaches a
// corner. An edge midpoint carrying no anchor is what leaves the midpoint-insert ghost a clear target.
import { test } from "node:test";
import assert from "node:assert/strict";
import { installDomStub } from "./_dom-stub.js";

installDomStub();

const { renderTransformBox, TRANSFORM_CORNERS, TRANSFORM_EDGES, gripSideX, gripSideZ } =
  await import("../../src/PgmStudio.Client/wwwroot/js/studio/render/canvas-chrome.js");

const layer = () => ({
  children: [],
  appendChild(c) { this.children.push(c); return c; },
});
const num = (el, k) => Number(el.getAttribute(k));
const box = { l: 100, t: 200, r: 300, b: 400 };

// An element is an anchor when it is the grip square; a band is the transparent zone over an edge.
const anchors = (g) => g.children.filter(c => c.getAttribute("fill") !== "transparent" && c.style.cursor && !c.style.cursor.startsWith("url("));
const bands   = (g) => g.children.filter(c => c.getAttribute("fill") === "transparent" && c.style.cursor && !c.style.cursor.startsWith("url("));
const rotates = (g) => g.children.filter(c => c.style.cursor?.startsWith("url("));

test("a box carries four anchors, one per corner, and none on an edge", () => {
  const g = layer();
  renderTransformBox(g, box, { onScale: () => {} });
  const found = anchors(g).map(el => [num(el, "x") + num(el, "width") / 2, num(el, "y") + num(el, "height") / 2]);
  assert.equal(found.length, 4);
  const corners = [[100, 200], [300, 200], [300, 400], [100, 400]];
  for (const c of corners) assert.ok(found.some(f => f[0] === c[0] && f[1] === c[1]), `no anchor at ${c}`);
  // The four edge midpoints — the spots a ninth grip used to take from the insert ghost — carry none.
  for (const m of [[200, 200], [300, 300], [200, 400], [100, 300]])
    assert.ok(!found.some(f => f[0] === m[0] && f[1] === m[1]), `an anchor sits on edge midpoint ${m}`);
});

test("each edge is one band, and it stops short of both corners", () => {
  const g = layer();
  renderTransformBox(g, box, { onScale: () => {} });
  const found = bands(g);
  assert.equal(found.length, 4);
  for (const el of found) {
    const x = num(el, "x"), y = num(el, "y"), w = num(el, "width"), h = num(el, "height");
    assert.ok(x > box.l && x + w < box.r || y > box.t && y + h < box.b,
      "a band runs corner to corner and would swallow the anchor there");
  }
});

test("a band drives one axis and a corner drives both", () => {
  for (const grip of TRANSFORM_EDGES)
    assert.equal(Number(gripSideX(grip) !== 0) + Number(gripSideZ(grip) !== 0), 1,
      `edge ${grip.key} does not stretch along exactly one axis`);
  for (const grip of TRANSFORM_CORNERS)
    assert.ok(gripSideX(grip) !== 0 && gripSideZ(grip) !== 0, `corner ${grip.key} does not drive both axes`);
});

test("a side names the bound it drags, and its anchor is the opposite one", () => {
  const nw = TRANSFORM_CORNERS.find(g => g.key === "nw");
  const se = TRANSFORM_CORNERS.find(g => g.key === "se");
  assert.deepEqual([gripSideX(nw), gripSideZ(nw)], [-1, -1]);
  assert.deepEqual([gripSideX(se), gripSideZ(se)], [1, 1]);
  const n = TRANSFORM_EDGES.find(g => g.key === "n");
  assert.deepEqual([gripSideX(n), gripSideZ(n)], [0, -1]);
});

test("an edge with no room between the corners gets no band", () => {
  const g = layer();
  renderTransformBox(g, { l: 0, t: 0, r: 8, b: 400 }, { onScale: () => {} });
  // Too narrow for a horizontal band; the vertical ones still fit.
  assert.equal(bands(g).filter(el => num(el, "width") > num(el, "height")).length, 0);
});

test("pressing a grip reports the grip that was pressed", () => {
  const g = layer();
  const seen = [];
  renderTransformBox(g, box, { onScale: (grip) => seen.push(grip.key) });
  for (const el of [...bands(g), ...anchors(g)]) el.fire("mousedown");
  assert.deepEqual(seen.sort(), ["e", "n", "ne", "nw", "s", "se", "sw", "w"]);
});

test("rotate zones are drawn only when a caller turns, and sit outside every corner", () => {
  const without = layer();
  renderTransformBox(without, box, { onScale: () => {} });
  assert.equal(rotates(without).length, 0);

  const withRotate = layer();
  renderTransformBox(withRotate, box, { onScale: () => {}, onRotate: () => {} });
  const zones = rotates(withRotate);
  assert.equal(zones.length, 4);
  for (const el of zones) {
    const x = num(el, "x"), y = num(el, "y"), w = num(el, "width"), h = num(el, "height");
    assert.ok(x + w <= box.l || x >= box.r || y + h <= box.t || y >= box.b,
      "a rotate zone overlaps the box and would take a scale press");
  }
});

test("a box with nothing to grab draws its outline and no target", () => {
  const g = layer();
  renderTransformBox(g, box, {});
  assert.equal(g.children.length, 1);
  assert.equal(g.children[0].getAttribute("fill"), "none");
  assert.equal(anchors(g).length + bands(g).length + rotates(g).length, 0);
});
