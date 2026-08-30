/**
 * Pure polygon math on rings of [[x,z], …] — no DOM.
 * The single home for point-in-polygon, polygon rasterisation, and half-plane clipping
 * (previously duplicated across world-canvas, converters, and the reference sketch tool).
 */

/** Ray-casting point-in-polygon test for a ring [[x,z], …]. */
export function pointInRing(px, pz, ring) {
  let inside = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, zi] = ring[i];
    const [xj, zj] = ring[j];
    if ((zi > pz) !== (zj > pz) && px < (xj - xi) * (pz - zi) / (zj - zi) + xi) {
      inside = !inside;
    }
  }
  return inside;
}

/** Rasterise a polygon (exterior ring + optional holes) to a list of [x,z] block cells. */
export function rasterisePolygon(exterior, holes = []) {
  if (!exterior.length) return [];
  const xs = exterior.map(([x]) => x);
  const zs = exterior.map(([, z]) => z);
  const minX = Math.floor(Math.min(...xs));
  const maxX = Math.ceil(Math.max(...xs));
  const minZ = Math.floor(Math.min(...zs));
  const maxZ = Math.ceil(Math.max(...zs));
  const result = [];
  for (let x = minX; x < maxX; x++) {
    for (let z = minZ; z < maxZ; z++) {
      const cx = x + 0.5, cz = z + 0.5;
      if (!pointInRing(cx, cz, exterior)) continue;
      if (holes.some(h => pointInRing(cx, cz, h))) continue;
      result.push([x, z]);
    }
  }
  return result;
}

/**
 * Sutherland-Hodgman half-plane clip.
 * Clips polygon `poly` ([[x,z], …]) against the half-plane defined by
 * point (ox, oz) and inward normal (nx, nz).
 * A vertex is inside when (v.x - ox)*nx + (v.z - oz)*nz >= 0.
 */
export function clipHalfPlane(poly, ox, oz, nx, nz) {
  if (!poly.length) return [];
  const dot = ([x, z]) => (x - ox) * nx + (z - oz) * nz;
  const output = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[(i + poly.length - 1) % poly.length];
    const b = poly[i];
    const da = dot(a);
    const db = dot(b);
    if (db >= 0) {
      if (da < 0) {
        const t = da / (da - db);
        output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]);
      }
      output.push(b);
    } else if (da >= 0) {
      const t = da / (da - db);
      output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]);
    }
  }
  return output;
}

/** Point-in-polygon for a `[exterior, ...holes]` polygon: inside the exterior and outside every hole. */
export function pointInPoly(px, pz, poly) {
  if (!poly.length || !pointInRing(px, pz, poly[0])) return false;
  for (let index = 1; index < poly.length; index++) if (pointInRing(px, pz, poly[index])) return false;
  return true;
}

/**
 * Do two `[exterior, ...holes]` polygons share any ground? Read off the rings themselves — containment
 * either way, then any pair of edges meeting — so it answers for the near-degenerate configurations a
 * sweepline clipper refuses to: a vertex a fraction of a block off another shape's edge, two outlines
 * drawn to share a run of vertices. Rings may be given open or closed, and an edge that only touches
 * counts: two shapes drawn to abut are one landmass.
 */
export function polysOverlap(first, second) {
  if (!first.length || !second.length) return false;
  for (const [x, z] of first[0]) if (pointInPoly(x, z, second)) return true;
  for (const [x, z] of second[0]) if (pointInPoly(x, z, first)) return true;
  for (const ringA of first)
    for (const ringB of second)
      if (ringsMeet(ringA, ringB)) return true;
  return false;
}

/** Every edge of a ring, as `[[x,z], [x,z]]` pairs, whether the ring repeats its first point or not. */
function ringEdges(ring) {
  if (ring.length < 2) return [];
  const [firstX, firstZ] = ring[0];
  const [lastX, lastZ] = ring[ring.length - 1];
  const count = firstX === lastX && firstZ === lastZ ? ring.length - 1 : ring.length;
  const edges = [];
  for (let index = 0; index < count; index++) edges.push([ring[index], ring[(index + 1) % count]]);
  return edges;
}

/** Do any two edges of these rings meet — crossing, touching or overlapping? */
function ringsMeet(ringA, ringB) {
  const edgesB = ringEdges(ringB);
  for (const [fromA, toA] of ringEdges(ringA))
    for (const [fromB, toB] of edgesB)
      if (segmentsMeet(fromA, toA, fromB, toB)) return true;
  return false;
}

/** Which side of the line through from→to the point falls: 1 left, −1 right, 0 collinear. */
function side(from, to, point) {
  const cross = (to[0] - from[0]) * (point[1] - from[1]) - (to[1] - from[1]) * (point[0] - from[0]);
  return cross > 0 ? 1 : cross < 0 ? -1 : 0;
}

/** Is the point inside the bounding box of from→to? Read only where the three are already collinear. */
function within(from, to, point) {
  return Math.min(from[0], to[0]) <= point[0] && point[0] <= Math.max(from[0], to[0])
      && Math.min(from[1], to[1]) <= point[1] && point[1] <= Math.max(from[1], to[1]);
}

/** Do two segments meet? Opposite sides both ways is a crossing; a collinear endpoint in range is a touch. */
function segmentsMeet(fromA, toA, fromB, toB) {
  const sideFromB = side(fromA, toA, fromB), sideToB = side(fromA, toA, toB);
  const sideFromA = side(fromB, toB, fromA), sideToA = side(fromB, toB, toA);
  if (sideFromB !== sideToB && sideFromA !== sideToA) return true;
  return (sideFromB === 0 && within(fromA, toA, fromB)) || (sideToB === 0 && within(fromA, toA, toB))
      || (sideFromA === 0 && within(fromB, toB, fromA)) || (sideToA === 0 && within(fromB, toB, toA));
}
