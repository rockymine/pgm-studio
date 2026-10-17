// building.js — the law about a building's wings that both authoring canvases have to apply while the pointer is
// still down, before anything could be asked of the server: the least span a wing takes, which runs of a house's
// walls are free to grow one from, and the wing a click on such a wall adds. A room's house and a dressed house
// are one kind of building, so Layout and Decoration are held to one rule rather than to a copy each.
//
// The twin of `PgmStudio.Domain.RoomFrames` (docs/world-export/structures.md WX2, HP2). Restated rather than
// served for the reason above; `RoomFramesTests` pins the C# side and `tests/js/` this one.

/**
 * The least span any building footprint may be, in blocks: two blocks of something with a block on each side
 * of it — a pad and the clear floor it keeps for a room, two walls and an inside for a dressed wing.
 */
export const MIN_FOOTPRINT_SPAN = 4;

/** How deep a wing added off a wall is, in blocks, before it is shrunk to fit. */
export const NEW_WING_DEPTH = 5;

/**
 * The free runs of a house's walls: each side of each wing, less the cells another wing already stands against,
 * kept where at least `MIN_FOOTPRINT_SPAN` long. Wings are inclusive cell rects `{minX, minZ, maxX, maxZ}`.
 * A run is `{wing, facing, line, lo, hi, mid}`: `facing` the side's edge word, `line` the first row or column of
 * cells outside it, `lo`/`hi` its inclusive span along the side, and `mid` the `[x, z]` of the middle of the
 * wall's outer edge, where its insert marker stands.
 */
export function freeWalls(rects) {
  const covered = (x, z) => rects.some(r => x >= r.minX && x <= r.maxX && z >= r.minZ && z <= r.maxZ);
  const walls = [];
  rects.forEach((r, wing) => {
    const sides = [
      { facing: "-x", line: r.minX - 1, lo: r.minZ, hi: r.maxZ, cell: (along) => [r.minX - 1, along] },
      { facing: "+x", line: r.maxX + 1, lo: r.minZ, hi: r.maxZ, cell: (along) => [r.maxX + 1, along] },
      { facing: "-z", line: r.minZ - 1, lo: r.minX, hi: r.maxX, cell: (along) => [along, r.minZ - 1] },
      { facing: "+z", line: r.maxZ + 1, lo: r.minX, hi: r.maxX, cell: (along) => [along, r.maxZ + 1] },
    ];
    for (const side of sides) {
      let start = null;
      for (let along = side.lo; along <= side.hi + 1; along++) {
        const free = along <= side.hi && !covered(...side.cell(along));
        if (free && start === null) start = along;
        if (!free && start !== null) {
          if (along - start >= MIN_FOOTPRINT_SPAN) walls.push(run(wing, side.facing, side.line, start, along - 1));
          start = null;
        }
      }
    }
  });
  return walls;
}

function run(wing, facing, line, lo, hi) {
  const middle = (lo + hi + 1) / 2;
  const edge = facing[0] === "-" ? line + 1 : line;
  const mid = facing.endsWith("x") ? [edge, middle] : [middle, edge];
  return { wing, facing, line, lo, hi, mid };
}

/** The free wall whose outer edge passes within `reach` blocks of (x, z), the nearest first; null for none. */
export function wallNear(rects, x, z, reach) {
  let best = null, bestDistance = reach;
  for (const wall of freeWalls(rects)) {
    const alongX = !wall.facing.endsWith("x");
    const [edgeX, edgeZ] = wall.mid;
    const across = alongX ? Math.abs(z - edgeZ) : Math.abs(x - edgeX);
    const along = alongX ? x : z;
    const off = along < wall.lo ? wall.lo - along : along > wall.hi + 1 ? along - wall.hi - 1 : 0;
    const distance = Math.hypot(across, off);
    if (distance <= bestDistance) { best = wall; bestDistance = distance; }
  }
  return best;
}

/**
 * The wing a click on `wall` adds: the wall's whole run long and `NEW_WING_DEPTH` deep, shrunk to fit — short of
 * any wing it would run into and inside `bound`, an inclusive cell rect or null for none. Null where less than
 * `MIN_FOOTPRINT_SPAN` fits either way.
 */
export function wingOffWall(rects, wall, bound = null, depth = NEW_WING_DEPTH) {
  const outward = wall.facing[0] === "-" ? -1 : 1;
  const alongX = !wall.facing.endsWith("x");
  let lo = wall.lo, hi = wall.hi;
  if (bound) {
    lo = Math.max(lo, alongX ? bound.minX : bound.minZ);
    hi = Math.min(hi, alongX ? bound.maxX : bound.maxZ);
  }
  if (hi - lo + 1 < MIN_FOOTPRINT_SPAN) return null;
  const rectOf = (deep) => {
    const near = wall.line, far = wall.line + outward * (deep - 1);
    const [a, b] = [Math.min(near, far), Math.max(near, far)];
    return alongX ? { minX: lo, maxX: hi, minZ: a, maxZ: b } : { minX: a, maxX: b, minZ: lo, maxZ: hi };
  };
  const fits = (r) => !rects.some(o => r.minX <= o.maxX && r.maxX >= o.minX && r.minZ <= o.maxZ && r.maxZ >= o.minZ)
    && (!bound || (r.minX >= bound.minX && r.maxX <= bound.maxX && r.minZ >= bound.minZ && r.maxZ <= bound.maxZ));
  for (let deep = depth; deep >= MIN_FOOTPRINT_SPAN; deep--) {
    const rect = rectOf(deep);
    if (fits(rect)) return rect;
  }
  return null;
}
