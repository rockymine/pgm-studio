// Parity tests for the client sketch rasterizer (pure, no DOM). It MUST agree with the C#
// PgmStudio.Pgm.Sketch.SketchRasterizer: same ring construction, cell-centre (x+0.5, z+0.5) fill, and the
// 4-step add/subtract/override set algebra.
import { test } from "node:test";
import assert from "node:assert/strict";

import { rasterizeShapes, rasterizeBoard } from "../../src/PgmStudio.Client/wwwroot/js/studio/geometry/rasterize.js";
import { stoneShade } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/sketch-render.js";

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

// ── the board: the drawn footprint and its symmetry images ───────────────────────
const square = { id: "a", type: "rectangle", operation: "add", min_x: 2, max_x: 4, min_z: 2, max_z: 4 };

test("a mirroring group adds its image under the map's symmetry", () => {
  // rot_180 about 0,0 turns cells 2..3 onto -4..-3 — the cell under each turned centre, as ApplyCell takes it.
  const cells = rasterizeBoard([square], [{ id: "g", shapeIds: ["a"], mirrors: true }], "rot_180", 0, 0);
  assert.deepEqual(sorted(cells), ["-3,-3", "-3,-4", "-4,-3", "-4,-4", "2,2", "2,3", "3,2", "3,3"]);
});

test("a group that does not mirror stands once", () => {
  const cells = rasterizeBoard([square], [{ id: "g", shapeIds: ["a"], mirrors: false }], "rot_180", 0, 0);
  assert.deepEqual(sorted(cells), ["2,2", "2,3", "3,2", "3,3"]);
});

test("a quarter-turn board fans a mirroring group three times", () => {
  const cells = rasterizeBoard([square], [{ id: "g", shapeIds: ["a"], mirrors: true }], "rot_90", 0, 0);
  assert.equal(cells.size, 16);
});

// ── stone: blocks read apart without lines between them ─────────────────────────
test("a block's stone shade is fixed by its cell and stays grey", () => {
  const shades = [];
  for (let x = -20; x < 20; x++) for (let z = -20; z < 20; z++) shades.push(stoneShade(x, z));
  assert.equal(stoneShade(7, -3), stoneShade(7, -3));
  assert.ok(shades.every(shade => shade >= 116 && shade <= 134), `${Math.min(...shades)}..${Math.max(...shades)}`);
  assert.ok(new Set(shades).size > 10, "neighbouring blocks differ");
});
