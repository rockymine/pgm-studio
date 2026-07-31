// Characterization tests for the render layer: the pure path-string builders (no DOM, shared by both
// dialects — a canvas Path2D takes SVG path data) and the shape dispatch that turns a type into a drawing.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { ringToPath, polyToPath, boundsToRingPath, handleRectAttrs }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/svg.js";
import { paintShape, paintAnchorBlock }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/shape-render.js";

const id = (x, z) => ({ x, y: z }); // identity world→surface transform

// ── path builders (pure) ──────────────────────────────────────────────────────
test("ringToPath straight ring closes with Z", () => {
  assert.equal(ringToPath([[0, 0], [10, 0], [10, 10]], id), "M0.0,0.0 L10.0,0.0 L10.0,10.0 Z");
});

test("ringToPath emits cubic C for a control point", () => {
  const d = ringToPath([[0, 0], [10, 0]], id, { "0": { out: [2, 0] } });
  assert.equal(d, "M0.0,0.0 C2.0,0.0 10.0,0.0 10.0,0.0 Z");
});

test("boundsToRingPath traces the AABB", () => {
  assert.equal(boundsToRingPath({ min_x: 0, min_z: 0, max_x: 10, max_z: 5 }, id),
    "M0.0,0.0 L10.0,0.0 L10.0,5.0 L0.0,5.0 Z");
});

test("polyToPath appends holes after the exterior", () => {
  const poly = { exterior: [[0, 0], [4, 0], [4, 4], [0, 4]], holes: [[[1, 1], [2, 1], [2, 2], [1, 2]]] };
  assert.equal(polyToPath(poly, id),
    "M0.0,0.0 L4.0,0.0 L4.0,4.0 L0.0,4.0 Z M1.0,1.0 L2.0,1.0 L2.0,2.0 L1.0,2.0 Z");
});

test("handleRectAttrs centres a square", () => {
  assert.deepEqual(handleRectAttrs(10, 20, 5), { x: 5, y: 15, width: 10, height: 10 });
});

// ── shape dispatch (recording painter) ────────────────────────────────────────
test("paintShape → rect for rectangle bounds", () => {
  const painter = recordingPainter();
  paintShape(painter, "rectangle", { min_x: 0, min_z: 0, max_x: 10, max_z: 5 }, { fill: "red" });
  assert.deepEqual(painter.of("rect"), [[{ min_x: 0, min_z: 0, max_x: 10, max_z: 5 }, { fill: "red" }]]);
});

test("paintShape → ellipse for radial types", () => {
  const painter = recordingPainter();
  paintShape(painter, "circle", { min_x: 0, min_z: 0, max_x: 10, max_z: 10 });
  assert.equal(painter.of("ellipse").length, 1);
  assert.equal(painter.of("rect").length, 0);
});

test("paintShape → a dot at the bounds centre for a point (not a zoom-shrinking rect)", () => {
  const painter = recordingPainter();
  paintShape(painter, "point", { min_x: 4, min_z: 4, max_x: 6, max_z: 6 });
  const [cx, cz, style] = painter.of("dot")[0];
  assert.deepEqual([cx, cz], [5, 5]);      // bounds centre
  assert.equal(style.radius, 5);           // default, in surface units rather than world ones
});

test("paintShape → the point radius is overridable by the style (marker treatment)", () => {
  const painter = recordingPainter();
  paintShape(painter, "point", { min_x: 0, min_z: 0, max_x: 0, max_z: 0 }, { radius: 6, fill: "gold" });
  const style = painter.styleOf("dot");
  assert.equal(style.radius, 6);
  assert.equal(style.fill, "gold");
});

test("paintShape → poly for a polygon_2d, even-odd so holes read as holes", () => {
  const painter = recordingPainter();
  paintShape(painter, "polygon", { exterior: [[0, 0], [2, 0], [2, 2]] });
  assert.equal(painter.of("poly").length, 1);
});

test("paintShape draws nothing for empty input", () => {
  const painter = recordingPainter();
  paintShape(painter, "rectangle", null);
  paintShape(painter, "polygon", { exterior: [] });
  assert.deepEqual(painter.calls, []);
});

test("paintAnchorBlock marks the whole block, not its corner", () => {
  const painter = recordingPainter();
  paintAnchorBlock(painter, 7, -3, "#abc");
  const [box, style] = painter.of("rect")[0];
  assert.deepEqual(box, { min_x: 7, min_z: -3, max_x: 8, max_z: -2 });
  assert.equal(style.fill, "#abc");
  assert.equal(style.fillAlpha, 0.5);
});
