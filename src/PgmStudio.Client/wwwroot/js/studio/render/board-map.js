/**
 * The board as built, seen from straight above: one pixel a column, the colour of the block on top of it,
 * shaded the way the game's own map item shades — each column against its north neighbour — so the eye reads
 * slopes, cliffs and the lie of the ground before it reads anything else.
 *
 * It is drawn from the same `{ palette, cols, layers }` payload the 3-D preview meshes (`sketch/columns`,
 * the full build), so the trees, the houses and the fluids are in it, which the terrain paint alone never is.
 *
 * Pure: no DOM. The payload in, an RGBA buffer out, which the caller turns into a bitmap.
 */

import { decodeColumns, hexRgb } from "./column-mesh.js";

/** The game's map shading: a column lower than its northern neighbour, level with it, higher than it. */
export const MAP_SHADES = { lower: 180 / 255, level: 220 / 255, higher: 1 };

/** How much lighter the highest ground is than the lowest, over the map shading. */
const HEIGHT_LIFT = 0.12;

/**
 * Build the board map.
 *
 * @returns {{ width: number, depth: number, min_x: number, min_z: number, max_x: number, max_z: number,
 *             rgba: Uint8ClampedArray, heights: Int16Array, minY: number, maxY: number } | null}
 *   `heights` holds each column's top block height, row-major from the north-west corner, and -1 where
 *   there is no column; null for a payload with no columns at all.
 */
export function boardMap(payload) {
  const columns = decodeColumns(payload);
  if (columns.size === 0) return null;
  const palette = (payload?.palette ?? []).map(hexRgb);

  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
  for (const { x, z } of columns.values()) {
    if (x < minX) minX = x;
    if (x > maxX) maxX = x;
    if (z < minZ) minZ = z;
    if (z > maxZ) maxZ = z;
  }
  const width = maxX - minX + 1, depth = maxZ - minZ + 1;
  const heights = new Int16Array(width * depth).fill(-1);
  const colours = new Int32Array(width * depth).fill(-1);
  let minY = Infinity, maxY = -Infinity;
  for (const { x, z, runs } of columns.values()) {
    const top = runs[0];
    if (!top) continue;
    const at = (z - minZ) * width + (x - minX);
    heights[at] = top.yTop;
    colours[at] = top.color;
    if (top.yTop < minY) minY = top.yTop;
    if (top.yTop > maxY) maxY = top.yTop;
  }

  const rgba = new Uint8ClampedArray(width * depth * 4);
  const span = Math.max(1, maxY - minY);
  for (let row = 0; row < depth; row++) {
    for (let column = 0; column < width; column++) {
      const at = row * width + column;
      const height = heights[at];
      if (height < 0) continue;
      const north = row > 0 ? heights[at - width] : -1;
      const shade = north < 0 || north === height ? MAP_SHADES.level
        : height > north ? MAP_SHADES.higher : MAP_SHADES.lower;
      const lift = 1 + HEIGHT_LIFT * ((height - minY) / span - 0.5);
      const [red, green, blue] = palette[colours[at]] ?? [1, 0, 1];
      rgba[at * 4] = red * 255 * shade * lift;
      rgba[at * 4 + 1] = green * 255 * shade * lift;
      rgba[at * 4 + 2] = blue * 255 * shade * lift;
      rgba[at * 4 + 3] = 255;
    }
  }
  return { width, depth, min_x: minX, min_z: minZ, max_x: maxX + 1, max_z: maxZ + 1, rgba, heights,
           minY: Number.isFinite(minY) ? minY : 0, maxY: Number.isFinite(maxY) ? maxY : 0 };
}
