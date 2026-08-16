/**
 * Turns the server's per-column runs into the triangles the isometric preview draws.
 *
 * The payload says what is solid; this decides what is *seen*, which is the one half of the question the
 * server cannot answer for it. Two faces are worth drawing and no others: the top of a run with air above it,
 * and the side of a run where the neighbour beside it is not solid at that height. Interior faces — the
 * underside of everything, the sides of a block boxed in by its neighbours, the top of a course with another
 * course on it — are never visible in an axonometric view of an opaque world, and a board has far more of them
 * than of the faces that show.
 *
 * Everything is an axis-aligned quad, so there is no triangulation anywhere and normals are constants rather
 * than a cross product per triangle. Colour rides on the vertices, which is what lets a whole board draw in
 * one call instead of one call per material.
 *
 * Pure: no DOM, no WebGL, no imports. The payload in, three typed arrays out.
 */

// A block at (x, y, z) fills the unit cube from (x, y, z) to (x+1, y+1, z+1), so a run's top surface sits at
// yTop + 1 and its underside at yBottom.
const FACES = [
  { dx: 0, dz: -1, normal: [0, 0, -1] },
  { dx: 0, dz: 1, normal: [0, 0, 1] },
  { dx: -1, dz: 0, normal: [-1, 0, 0] },
  { dx: 1, dz: 0, normal: [1, 0, 0] },
];

/**
 * Mesh a `{ palette, cols }` payload.
 * @returns {{positions: Float32Array, normals: Float32Array, colors: Float32Array,
 *            minX: number, maxX: number, minZ: number, maxZ: number, maxY: number, quads: number}}
 */
export function meshColumns(payload) {
  const columns = decodeColumns(payload);
  const colors = (payload?.palette ?? []).map(hexRgb);

  const positions = [];
  const normals = [];
  const vertexColors = [];
  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity, maxY = 0;

  for (const [key, column] of columns) {
    const { x, z, runs } = column;
    if (x < minX) minX = x;
    if (x + 1 > maxX) maxX = x + 1;
    if (z < minZ) minZ = z;
    if (z + 1 > maxZ) maxZ = z + 1;

    const neighbours = FACES.map(face => columns.get(cellKey(x + face.dx, z + face.dz))?.runs ?? null);

    for (let i = 0; i < runs.length; i++) {
      const run = runs[i];
      const rgb = colors[run.color] ?? [1, 0, 1];   // an index the palette does not carry is a loud magenta
      const top = run.yTop + 1;
      if (top > maxY) maxY = top;

      // The top face, unless the run above sits directly on this one — which happens wherever a material
      // changes without a gap, and is most of the boundaries in a painted column.
      const above = runs[i - 1];
      if (!above || above.yBottom > run.yTop + 1) {
        quad(positions, normals, vertexColors, rgb, [0, 1, 0],
             x, top, z, x + 1, top, z, x + 1, top, z + 1, x, top, z + 1);
      }

      // The sides, one quad per span the neighbour leaves open. A neighbour that is not in the payload at all
      // is open for the whole run — the edge of the world is a face like any other.
      for (let f = 0; f < FACES.length; f++) {
        const face = FACES[f];
        for (const [spanTop, spanBottom] of openSpans(neighbours[f], run.yTop, run.yBottom)) {
          const high = spanTop + 1, low = spanBottom;
          const nearZ = face.dz > 0 ? z + 1 : z;
          const nearX = face.dx > 0 ? x + 1 : x;
          if (face.dx === 0) {
            quad(positions, normals, vertexColors, rgb, face.normal,
                 x, high, nearZ, x + 1, high, nearZ, x + 1, low, nearZ, x, low, nearZ);
          } else {
            quad(positions, normals, vertexColors, rgb, face.normal,
                 nearX, high, z, nearX, high, z + 1, nearX, low, z + 1, nearX, low, z);
          }
        }
      }
    }
  }

  if (!isFinite(minX)) { minX = maxX = minZ = maxZ = 0; }
  return {
    positions: new Float32Array(positions),
    normals: new Float32Array(normals),
    colors: new Float32Array(vertexColors),
    minX, maxX, minZ, maxZ, maxY,
    quads: positions.length / 18,
  };
}

/** The flat `cols` array as a map of `"x,z"` → `{x, z, runs}`, runs top first as the server wrote them. */
export function decodeColumns(payload) {
  const cols = payload?.cols ?? [];
  const columns = new Map();
  for (let at = 0; at < cols.length;) {
    const x = cols[at++], z = cols[at++], count = cols[at++];
    const runs = new Array(count);
    for (let i = 0; i < count; i++) {
      runs[i] = { yTop: cols[at++], yBottom: cols[at++], color: cols[at++] };
    }
    columns.set(cellKey(x, z), { x, z, runs });
  }
  return columns;
}

/**
 * The spans of `[bottom, top]` where `runs` is NOT solid — the parts of a neighbouring column's face that
 * are open, and so the parts of this column's side that can be seen. Returned high span first, each as
 * `[spanTop, spanBottom]` in inclusive block coordinates.
 */
export function openSpans(runs, top, bottom) {
  if (!runs || runs.length === 0) return [[top, bottom]];

  const spans = [];
  let ceiling = top;   // the highest block not yet accounted for
  for (const run of runs) {
    if (run.yBottom > ceiling) continue;          // wholly above the window
    if (run.yTop < bottom) break;                 // wholly below it, and the rest are lower still
    if (run.yTop < ceiling) spans.push([ceiling, run.yTop + 1]);
    ceiling = run.yBottom - 1;
    if (ceiling < bottom) return spans;
  }
  if (ceiling >= bottom) spans.push([ceiling, bottom]);
  return spans;
}

// Two triangles, six vertices, one normal and one colour repeated across them. Wound consistently, though the
// shader lights both sides — a quad seen from behind is a quad the camera should still be told about.
function quad(positions, normals, colors, rgb, normal, ...corners) {
  const [ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz] = corners;
  positions.push(ax, ay, az, bx, by, bz, cx, cy, cz, ax, ay, az, cx, cy, cz, dx, dy, dz);
  for (let i = 0; i < 6; i++) {
    normals.push(normal[0], normal[1], normal[2]);
    colors.push(rgb[0], rgb[1], rgb[2]);
  }
}

const cellKey = (x, z) => `${x},${z}`;

/** `"#rrggbb"` → `[r, g, b]` in 0..1. */
export function hexRgb(hex) {
  const value = parseInt(String(hex).replace("#", ""), 16);
  return Number.isNaN(value)
    ? [1, 0, 1]
    : [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255];
}
