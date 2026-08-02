// Parity tests for the client sketch rasterizer (pure, no DOM). It MUST agree with the C#
// PgmStudio.Pgm.Sketch.SketchRasterizer: same ring construction, cell-centre (x+0.5, z+0.5) fill, and the
// 4-step add/subtract/override set algebra.
import { test } from "node:test";
import assert from "node:assert/strict";

import { rasterizeShapes, cellRuns } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/rasterize.js";

const sorted = (cells) => [...cells].sort();

// ── ring fill (cell centre in ring) ──────────────────────────────────────────────
test("a rectangle fills exactly its interior cells", () => {
  // A rect [0,3]×[0,2] spans cells x∈{0,1,2}, z∈{0,1} — the six cells whose centres lie inside.
  const cells = rasterizeShapes([{ type: "rectangle", operation: "add", min_x: 0, max_x: 3, min_z: 0, max_z: 2 }]);
  assert.deepEqual(sorted(cells), ["0,0", "0,1", "1,0", "1,1", "2,0", "2,1"]);
});

test("a 1×1 rectangle fills the single block it addresses", () => {
  const cells = rasterizeShapes([{ type: "rectangle", operation: "add", min_x: 5, max_x: 6, min_z: 7, max_z: 8 }]);
  assert.deepEqual(sorted(cells), ["5,7"]);
});

test("a circle rasterizes to a filled blocky disc (radius 3 → cell centre test)", () => {
  const cells = rasterizeShapes([{ type: "circle", operation: "add", center_x: 0, center_z: 0, radius: 3 }]);
  // Every kept cell's centre is within the 64-gon; a spot check: the centre block is in, a far corner is out.
  assert.ok(cells.has("0,0"));
  assert.ok(cells.has("-1,-1"));
  assert.ok(!cells.has("3,3"));   // outside the disc
  assert.ok(cells.size > 20 && cells.size < 40);   // ~π·3² ≈ 28 cells
});

// ── set algebra ──────────────────────────────────────────────────────────────────
test("subtract erases cells a normal add painted", () => {
  const cells = rasterizeShapes([
    { type: "rectangle", operation: "add",      min_x: 0, max_x: 3, min_z: 0, max_z: 1 },
    { type: "rectangle", operation: "subtract", min_x: 1, max_x: 2, min_z: 0, max_z: 1 },
  ]);
  assert.deepEqual(sorted(cells), ["0,0", "2,0"]);   // middle cell removed
});

test("override-add survives a normal subtract (applied last)", () => {
  const cells = rasterizeShapes([
    { type: "rectangle", operation: "add",      min_x: 0, max_x: 3, min_z: 0, max_z: 1 },
    { type: "rectangle", operation: "subtract", min_x: 0, max_x: 3, min_z: 0, max_z: 1 },
    { type: "rectangle", operation: "add", override: true, min_x: 1, max_x: 2, min_z: 0, max_z: 1 },
  ]);
  assert.deepEqual(sorted(cells), ["1,0"]);   // the override-add is immune to the normal subtract
});

test("override-subtract cuts through everything, including override-adds", () => {
  const cells = rasterizeShapes([
    { type: "rectangle", operation: "add", override: true, min_x: 0, max_x: 3, min_z: 0, max_z: 1 },
    { type: "rectangle", operation: "subtract", override: true, min_x: 1, max_x: 2, min_z: 0, max_z: 1 },
  ]);
  assert.deepEqual(sorted(cells), ["0,0", "2,0"]);
});

// ── run merging ──────────────────────────────────────────────────────────────────
test("cellRuns merges a solid row into one run and splits on a gap", () => {
  const runs = cellRuns(new Set(["0,0", "1,0", "2,0", "4,0", "0,1"]));
  const byRow = runs.sort((a, b) => a.z - b.z || a.x - b.x);
  assert.deepEqual(byRow, [
    { x: 0, z: 0, w: 3 },   // 0,1,2 contiguous
    { x: 4, z: 0, w: 1 },   // gap at 3 → separate run
    { x: 0, z: 1, w: 1 },
  ]);
});
