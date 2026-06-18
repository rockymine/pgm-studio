/**
 * Pure polygon math on rings of [[x,z], …] — no DOM.
 * The single home for point-in-polygon, polygon rasterisation, and half-plane clipping
 * (previously duplicated across editor-canvas, converters, and the reference sketch tool).
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
