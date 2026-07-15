// Characterization tests for the shared primitive-style vocabulary: each treatment's fill/stroke/dash
// knobs, colour-is-caller-supplied, and the ghost/selected/primary state variants.
import { test } from "node:test";
import assert from "node:assert/strict";

import { primitiveStyle, opColors, OP_COLORS }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/primitive-style.js";

test("colour is always caller-supplied — fill and stroke default to `color`", () => {
  const s = primitiveStyle("region", { color: "#abc" });
  assert.equal(s.fill, "#abc");
  assert.equal(s.stroke, "#abc");
});

test("region: normal / ghost / selected differ in opacity, width, dash", () => {
  const normal = primitiveStyle("region", { color: "red" });
  assert.equal(normal["fill-opacity"], "0.20");
  assert.equal(normal["stroke-width"], "1.5");
  assert.equal(normal["stroke-dasharray"], "4 2");

  const ghost = primitiveStyle("region", { color: "red", state: "ghost" });
  assert.equal(ghost["fill-opacity"], "0.06");
  assert.equal(ghost["stroke-dasharray"], "2 3");

  const selected = primitiveStyle("region", { color: "red", state: "selected" });
  assert.equal(selected["stroke-width"], "2.5");
  assert.equal(selected["fill-opacity"], "0.22");
  assert.equal(selected["stroke-dasharray"], undefined); // solid when selected
});

test("marker: fixed radius, brighter/larger when primary", () => {
  const primary = primitiveStyle("marker", { color: "gold", primary: true });
  assert.equal(primary.r, 6);
  assert.equal(primary["stroke-width"], "2");
  assert.equal(primary.opacity, "1");
  assert.equal(primary.stroke, "var(--canvas-marker-stroke)");

  const orbit = primitiveStyle("marker", { color: "gold", primary: false });
  assert.equal(orbit.r, 5);
  assert.equal(orbit.opacity, "0.55");
});

test("sketch: distinct fill/stroke shades, override adds the dash", () => {
  const { fill, stroke } = opColors("add");
  const plain = primitiveStyle("sketch", { fill, stroke });
  assert.equal(plain.fill, "var(--canvas-add-fill)");
  assert.equal(plain.stroke, "var(--canvas-add-stroke)");
  assert.equal(plain["fill-opacity"], "0.28");
  assert.equal(plain["stroke-dasharray"], undefined);

  const overridden = primitiveStyle("sketch", { fill, stroke, override: true });
  assert.equal(overridden["stroke-dasharray"], "6 3");
});

test("opColors maps add vs subtract", () => {
  assert.deepEqual(opColors("subtract"), OP_COLORS.subtract);
  assert.deepEqual(opColors("add"), OP_COLORS.add);
  assert.deepEqual(opColors(undefined), OP_COLORS.add); // anything not "subtract" → add
});

test("terrain: solid opaque, height-map mode a touch more opaque; ghost is faint dashed", () => {
  const solid = primitiveStyle("terrain", { color: "#3fae74" });
  assert.equal(solid["fill-opacity"], "0.7");
  assert.equal(solid["stroke-dasharray"], undefined);

  assert.equal(primitiveStyle("terrain", { color: "x", heightMap: true })["fill-opacity"], "0.85");

  const ghost = primitiveStyle("terrain", { color: "x", state: "ghost" });
  assert.equal(ghost["fill-opacity"], "0.08");
  assert.equal(ghost["stroke-dasharray"], "5 4");
});

test("technical: hatch fill supplied by caller, dashed same-colour stroke", () => {
  const t = primitiveStyle("technical", { color: "#f2792b", fill: "url(#buffer-hatch)" });
  assert.equal(t.fill, "url(#buffer-hatch)");   // caller-supplied hatch, not the colour
  assert.equal(t.stroke, "#f2792b");
  assert.equal(t["stroke-dasharray"], "5 4");
  assert.equal(t["fill-opacity"], "0.9");
});

test("zone: translucent accent, dashed; ghost fainter", () => {
  const z = primitiveStyle("zone", { color: "var(--accent)" });
  assert.equal(z["fill-opacity"], "0.22");
  assert.equal(z["stroke-dasharray"], "7 4");

  assert.equal(primitiveStyle("zone", { color: "var(--accent)", state: "ghost" })["fill-opacity"], "0.07");
});
