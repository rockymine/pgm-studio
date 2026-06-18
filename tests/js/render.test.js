// Characterization tests for the render layer: pure path-string builders (no DOM)
// and shape dispatch (via a minimal DOM stub).
import { test } from "node:test";
import assert from "node:assert/strict";
import { installDomStub } from "./_dom-stub.js";

installDomStub(); // svgEl/renderShape read the global `document` at call time

import { ringToPath, polyToPath, boundsToRingPath, handleRectAttrs }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/svg.js";
import { renderShape } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/shape-render.js";

const id = (x, z) => ({ x, y: z }); // identity world→svg transform

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

// ── shape dispatch (DOM stub) ─────────────────────────────────────────────────
test("renderShape → rect for rectangle bounds", () => {
  const el = renderShape("rectangle", { min_x: 0, min_z: 0, max_x: 10, max_z: 5 }, id, { fill: "red" });
  assert.equal(el.tagName, "rect");
  assert.equal(el.getAttribute("x"), "0");
  assert.equal(el.getAttribute("width"), "10");
  assert.equal(el.getAttribute("height"), "5");
  assert.equal(el.getAttribute("fill"), "red");
});

test("renderShape → ellipse for radial types", () => {
  const el = renderShape("circle", { min_x: 0, min_z: 0, max_x: 10, max_z: 10 }, id);
  assert.equal(el.tagName, "ellipse");
  assert.equal(el.getAttribute("cx"), "5");
  assert.equal(el.getAttribute("rx"), "5");
});

test("renderShape → path for polygon_2d", () => {
  const el = renderShape("polygon", { exterior: [[0, 0], [2, 0], [2, 2]] }, id);
  assert.equal(el.tagName, "path");
  assert.equal(el.getAttribute("fill-rule"), "evenodd");
  assert.equal(el.getAttribute("d"), "M0.0,0.0 L2.0,0.0 L2.0,2.0 Z");
});

test("renderShape returns null for empty input", () => {
  assert.equal(renderShape("rectangle", null, id), null);
  assert.equal(renderShape("polygon", { exterior: [] }, id), null);
});
