/**
 * Pure symmetry transforms on points and extent bounds — no DOM.
 */

/** Reflect/rotate a single point about a centre. */
export function applySymmetry(x, z, axis, cx, cz) {
  switch (axis) {
    case "mirror_x": return [2 * cx - x, z];
    case "mirror_z": return [x, 2 * cz - z];
    case "rot_180":  return [2 * cx - x, 2 * cz - z];
    case "rot_90":   return [cx - (z - cz), cz + (x - cx)];
    default: throw new Error(`Unknown symmetry axis: ${axis}`);
  }
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
