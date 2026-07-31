// Characterization tests for the shared primitive-style vocabulary: each treatment's fill/stroke/dash
// knobs, colour-is-caller-supplied, and the ghost/selected/primary state variants. The values are in the
// painter's dialect — a `width` is screen pixels at every zoom, and a `dash` is in the same unit.
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
  assert.equal(normal.fillAlpha, 0.20);
  assert.equal(normal.width, 1.5);
  assert.deepEqual(normal.dash, [4, 2]);

  const ghost = primitiveStyle("region", { color: "red", state: "ghost" });
  assert.equal(ghost.fillAlpha, 0.06);
  assert.deepEqual(ghost.dash, [2, 3]);

  const selected = primitiveStyle("region", { color: "red", state: "selected" });
  assert.equal(selected.width, 2.5);
  assert.equal(selected.fillAlpha, 0.22);
  assert.equal(selected.dash, undefined); // solid when selected
});

test("marker: fixed radius, brighter/larger when primary", () => {
  const primary = primitiveStyle("marker", { color: "gold", primary: true });
  assert.equal(primary.radius, 6);
  assert.equal(primary.width, 2);
  assert.equal(primary.alpha, 1);
  assert.equal(primary.stroke, "var(--canvas-marker-stroke)");

  const orbit = primitiveStyle("marker", { color: "gold", primary: false });
  assert.equal(orbit.radius, 5);
  assert.equal(orbit.alpha, 0.55);
});

test("sketch: distinct fill/stroke shades, override adds the dash", () => {
  const { fill, stroke } = opColors("add");
  const plain = primitiveStyle("sketch", { fill, stroke });
  assert.equal(plain.fill, "var(--canvas-add-fill)");
  assert.equal(plain.stroke, "var(--canvas-add-stroke)");
  assert.equal(plain.fillAlpha, 0.28);
  assert.equal(plain.dash, undefined);

  const overridden = primitiveStyle("sketch", { fill, stroke, override: true });
  assert.deepEqual(overridden.dash, [6, 3]);
});

test("opColors maps add vs subtract", () => {
  assert.deepEqual(opColors("subtract"), OP_COLORS.subtract);
  assert.deepEqual(opColors("add"), OP_COLORS.add);
  assert.deepEqual(opColors(undefined), OP_COLORS.add); // anything not "subtract" → add
});

test("terrain: solid opaque, height-map mode a touch more opaque; ghost is faint dashed", () => {
  const solid = primitiveStyle("terrain", { color: "#3fae74" });
  assert.equal(solid.fillAlpha, 0.7);
  assert.equal(solid.dash, undefined);

  assert.equal(primitiveStyle("terrain", { color: "x", heightMap: true }).fillAlpha, 0.85);

  const ghost = primitiveStyle("terrain", { color: "x", state: "ghost" });
  assert.equal(ghost.fillAlpha, 0.08);
  assert.deepEqual(ghost.dash, [5, 4]);
});

test("technical: hatch fill supplied by caller, dashed same-colour stroke", () => {
  const hatch = { kind: "a CanvasPattern the caller built" };
  const t = primitiveStyle("technical", { color: "#f2792b", fill: hatch });
  assert.equal(t.fill, hatch);   // caller-supplied hatch, not the colour
  assert.equal(t.stroke, "#f2792b");
  assert.deepEqual(t.dash, [5, 4]);
  assert.equal(t.fillAlpha, 0.9);
});

test("zone: translucent accent, dashed; ghost fainter", () => {
  const z = primitiveStyle("zone", { color: "var(--accent)" });
  assert.equal(z.fillAlpha, 0.22);
  assert.deepEqual(z.dash, [7, 4]);

  assert.equal(primitiveStyle("zone", { color: "var(--accent)", state: "ghost" }).fillAlpha, 0.07);
});
