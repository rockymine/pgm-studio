/**
 * Pure symmetry transforms on points and extent bounds — no DOM.
 */

/** Reflect/rotate a single point about a centre. Mirrors the server's Geometry2d (all six modes). */
export function applySymmetry(x, z, axis, cx, cz) {
  switch (axis) {
    case "mirror_x":  return [2 * cx - x, z];
    case "mirror_z":  return [x, 2 * cz - z];
    case "mirror_d1": return [cx + (z - cz), cz + (x - cx)];   // reflect across the main diagonal (normal 1,-1)
    case "mirror_d2": return [cx - (z - cz), cz - (x - cx)];   // reflect across the anti-diagonal (normal 1,1)
    case "rot_180":   return [2 * cx - x, 2 * cz - z];
    case "rot_90":    return [cx - (z - cz), cz + (x - cx)];
    case "rot_270":   return [cx + (z - cz), cz - (x - cx)];   // internal: the 3rd image of a rot_90 orbit
    default: throw new Error(`Unknown symmetry axis: ${axis}`);
  }
}

/**
 * The orbit images (other than the source) a symmetry produces: `rot_90` fills three quarter-turns,
 * `none` produces no image at all (a single un-fanned unit — the freeform authoring mode), every other
 * mode is a single reflection/half-turn. Used to fan one authored shape across all sides.
 */
export function orbitAxes(type) {
  if (type === "rot_90") return ["rot_90", "rot_180", "rot_270"];
  if (!type || type === "none") return [];
  return [type];
}

/** Apply a symmetry to an extent bounds, returning the transformed AABB. */
export function applySymmetryToBounds(bounds, axis, cx, cz) {
  const { min_x, max_x, min_z, max_z } = bounds;
  const corners = [
    [min_x, min_z],
    [max_x, min_z],
    [max_x, max_z],
    [min_x, max_z],
  ].map(([x, z]) => applySymmetry(x, z, axis, cx, cz));
  const xs = corners.map(([x]) => x);
  const zs = corners.map(([, z]) => z);
  return {
    min_x: Math.min(...xs),
    max_x: Math.max(...xs),
    min_z: Math.min(...zs),
    max_z: Math.max(...zs),
  };
}
