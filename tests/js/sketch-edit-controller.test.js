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
