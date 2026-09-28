// The board seen from straight above: one pixel a column in the colour of its top block, shaded against the
// column to its north the way the game's own map item is.
import { test } from "node:test";
import assert from "node:assert/strict";

import { boardMap, MAP_SHADES }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/render/board-map.js";

// [x, z, runCount, (yTop, yBottom, colorIdx, layerIdx) × runCount, …], the way sketch/columns writes it.
const payload = (palette, ...columns) => ({
  palette,
  cols: columns.flatMap(([x, z, ...runs]) =>
    [x, z, runs.length, ...runs.flatMap(([top, bottom, color = 0]) => [top, bottom, color, -1])]),
});

const pixel = (map, x, z) => {
  const at = ((z - map.min_z) * map.width + (x - map.min_x)) * 4;
  return [...map.rgba.slice(at, at + 4)];
};

test("each column takes the colour of the block on top of it, not of the ground under it", () => {
  const map = boardMap(payload(["#808080", "#00ff00"], [0, 0, [12, 12, 1], [10, 0, 0]]));
  const [red, green, blue, alpha] = pixel(map, 0, 0);
  assert.equal(alpha, 255);
  assert.ok(green > 150 && red === 0 && blue === 0);
  assert.equal(map.heights[0], 12);
});

test("a column higher than its north neighbour is lighter than one level with it, and a lower one darker", () => {
  const flat = payload(["#c8c8c8"], [0, 0, [10, 0]], [0, 1, [10, 0]]);
  const rising = payload(["#c8c8c8"], [0, 0, [10, 0]], [0, 1, [11, 0]]);
  const falling = payload(["#c8c8c8"], [0, 0, [10, 0]], [0, 1, [9, 0]]);
  const level = pixel(boardMap(flat), 0, 1)[0];
  assert.ok(pixel(boardMap(rising), 0, 1)[0] > level);
  assert.ok(pixel(boardMap(falling), 0, 1)[0] < level);
  assert.ok(MAP_SHADES.lower < MAP_SHADES.level && MAP_SHADES.level < MAP_SHADES.higher);
});

test("a cell no column covers is left transparent", () => {
  const map = boardMap(payload(["#ffffff"], [0, 0, [5, 0]], [2, 0, [5, 0]]));
  assert.equal(map.width, 3);
  assert.equal(pixel(map, 1, 0)[3], 0);
  assert.equal(boardMap({ palette: [], cols: [] }), null);
});
