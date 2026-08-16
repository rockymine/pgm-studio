// The mesher that turns per-column runs into the isometric preview's triangles. What these assert is which
// faces are *seen* — an opaque world hides far more of itself than it shows, and drawing the hidden half is
// both slower and, where two surfaces land on the same plane, visibly wrong.
import { test } from "node:test";
import assert from "node:assert/strict";

import { meshColumns, decodeColumns, openSpans, hexRgb }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/column-mesh.js";

// A payload built the way the server writes one: [x, z, runCount, (yTop, yBottom, colorIdx) × runCount, …]
const payload = (palette, ...columns) => ({
  palette,
  cols: columns.flatMap(([x, z, ...runs]) =>
    [x, z, runs.length, ...runs.flatMap(([top, bottom, color = 0]) => [top, bottom, color])]),
});

const QUAD_FLOATS = 18;   // six vertices, three floats each

// ── the open-span walk (what a neighbour leaves visible) ──────────────────────

test("a missing neighbour leaves the whole run open", () => {
  assert.deepEqual(openSpans(null, 10, 0), [[10, 0]]);
  assert.deepEqual(openSpans([], 10, 0), [[10, 0]]);
});

test("a neighbour covering the run leaves nothing open", () => {
  assert.deepEqual(openSpans([{ yTop: 20, yBottom: 0 }], 10, 0), []);
});

test("a shorter neighbour leaves the part above it open", () => {
  assert.deepEqual(openSpans([{ yTop: 10, yBottom: 0 }], 20, 0), [[20, 11]]);
});

test("a gap in the neighbour is open even with solid above and below it", () => {
  const neighbour = [{ yTop: 30, yBottom: 20 }, { yTop: 10, yBottom: 0 }];
  assert.deepEqual(openSpans(neighbour, 30, 0), [[19, 11]]);
});

test("a neighbour entirely above or below the window does not close it", () => {
  assert.deepEqual(openSpans([{ yTop: 90, yBottom: 80 }], 10, 0), [[10, 0]]);
  assert.deepEqual(openSpans([{ yTop: 5, yBottom: 0 }], 20, 10), [[20, 10]]);
});

// ── decoding ──────────────────────────────────────────────────────────────────

test("decodeColumns walks the flat array by its run counts", () => {
  const columns = decodeColumns(payload(["#fff"], [3, 4, [10, 0, 0]], [5, 6, [20, 18, 0], [12, 0, 0]]));

  assert.equal(columns.size, 2);
  assert.deepEqual(columns.get("3,4").runs, [{ yTop: 10, yBottom: 0, color: 0 }]);
  assert.deepEqual(columns.get("5,6").runs,
    [{ yTop: 20, yBottom: 18, color: 0 }, { yTop: 12, yBottom: 0, color: 0 }]);
});

test("an empty payload meshes to nothing rather than throwing", () => {
  const mesh = meshColumns({ palette: [], cols: [] });
  assert.equal(mesh.positions.length, 0);
  assert.equal(mesh.quads, 0);
});

// ── which faces are drawn ─────────────────────────────────────────────────────

test("a column standing alone shows its top and all four sides", () => {
  const mesh = meshColumns(payload(["#808080"], [0, 0, [10, 0]]));
  assert.equal(mesh.quads, 5);
  assert.equal(mesh.positions.length, 5 * QUAD_FLOATS);
});

test("two neighbours of equal height show no wall between them", () => {
  const mesh = meshColumns(payload(["#808080"], [0, 0, [10, 0]], [1, 0, [10, 0]]));
  // Five each standing alone, less the one face each now has hidden by the other.
  assert.equal(mesh.quads, 8);
});

test("a step shows one wall, and only on the taller side", () => {
  const mesh = meshColumns(payload(["#808080"], [0, 0, [20, 0]], [1, 0, [10, 0]]));
  // The tall column keeps all five of its faces (the shared one is only partly covered); the short one
  // loses the face the tall column hides completely.
  assert.equal(mesh.quads, 9);
});

test("the wall of a step is as tall as the step, not as tall as the column", () => {
  const mesh = meshColumns(payload(["#808080"], [0, 0, [20, 0]], [1, 0, [10, 0]]));

  // The +x face of the tall column is the only quad at x = 1 whose vertices span y 11..21.
  const ys = [];
  for (let at = 0; at < mesh.positions.length; at += 3) {
    if (mesh.positions[at] === 1 && mesh.normals[at] === 1) ys.push(mesh.positions[at + 1]);
  }
  assert.ok(ys.length > 0, "the tall column has an +x face");
  assert.equal(Math.min(...ys), 11);
  assert.equal(Math.max(...ys), 21);
});

test("a span floating over ground keeps both tops and both sets of sides", () => {
  const mesh = meshColumns(payload(["#808080"], [0, 0, [62, 60], [40, 0]]));
  // Two runs, each with a top face (there is air over both) and four sides. No undersides: the bridge's
  // soffit is never seen from an isometric camera looking down.
  assert.equal(mesh.quads, 10);
});

test("a material change with no gap hides the lower run's top", () => {
  const mesh = meshColumns(payload(["#6b8f3a", "#8a8a8a"], [0, 0, [63, 61, 0], [60, 0, 1]]));
  // Top run: a top and four sides. Lower run: four sides only — the run above is sitting on it.
  assert.equal(mesh.quads, 9);
});

test("a wall shows each course in its own colour rather than one smeared down it", () => {
  const grass = "#6b8f3a", stone = "#8a8a8a";
  const mesh = meshColumns(payload([grass, stone], [0, 0, [63, 61, 0], [60, 0, 1]]));

  // Read back at the precision the mesh stores: colours land in a Float32Array, so the expectation goes
  // through the same narrowing rather than comparing a float64 against its rounded self.
  const stored = (hex) => Array.from(new Float32Array(hexRgb(hex))).join(",");

  // Colours present on the −z wall (normal 0,0,−1), by the y each vertex sits at.
  const seen = new Map();
  for (let vertex = 0; vertex * 3 < mesh.positions.length; vertex++) {
    const at = vertex * 3;
    if (mesh.normals[at + 2] !== -1) continue;
    const key = mesh.positions[at + 1] > 61 ? "upper" : "lower";
    seen.set(key, [mesh.colors[at], mesh.colors[at + 1], mesh.colors[at + 2]].join(","));
  }
  assert.equal(seen.get("upper"), stored(grass));
  assert.equal(seen.get("lower"), stored(stone));
});

test("the mesh reports the extent it covers, in block coordinates", () => {
  const mesh = meshColumns(payload(["#808080"], [-4, -2, [10, 0]], [6, 8, [30, 0]]));
  assert.equal(mesh.minX, -4);
  assert.equal(mesh.maxX, 7);    // the far column's block fills through x+1
  assert.equal(mesh.minZ, -2);
  assert.equal(mesh.maxZ, 9);
  assert.equal(mesh.maxY, 31);   // a block at y=30 has its top surface at 31
});

test("hexRgb turns a palette entry into unit floats, and says so loudly when it cannot", () => {
  assert.deepEqual(hexRgb("#ff8000"), [1, 128 / 255, 0]);
  assert.deepEqual(hexRgb("nonsense"), [1, 0, 1]);
});
