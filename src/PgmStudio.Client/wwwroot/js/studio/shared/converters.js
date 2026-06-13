/**
 * Shared spatial converters.
 * Each function has exactly one implementation here — no caller applies +1 or
 * rotation arithmetic directly. See docs/contracts/geometry.md §6 (required converters) for contracts.
 */

// ── Block position → extent bounds ───────────────────────────────────────────

export function blockToExtentBounds(x, z) {
  return { min_x: x, max_x: x + 1, min_z: z, max_z: z + 1 };
}

// ── Drawn block range → extent bounds ────────────────────────────────────────

export function drawnBoundsFromBlocks(b1x, b1z, b2x, b2z) {
  return {
    min_x: Math.min(b1x, b2x),
    max_x: Math.max(b1x, b2x) + 1,
    min_z: Math.min(b1z, b2z),
    max_z: Math.max(b1z, b2z) + 1,
  };
}

// ── PGM region → 2D display bounds ───────────────────────────────────────────

const _COMPOSITE = new Set([
  "union", "intersect", "negative", "complement",
  "mirror", "translate", "join", "block-range", "void", "nowhere", "everywhere",
]);

export function regionToBounds2d(region) {
  if (!region) return null;
  const { type } = region;
  if (_COMPOSITE.has(type)) return null;
  switch (type) {
    case "rectangle":
    case "cuboid": {
      const { min_x, min_z, max_x, max_z } = region;
      return { min_x, min_z, max_x, max_z };
    }
    case "cylinder":
      return {
        min_x: region.base_x - region.radius,
        max_x: region.base_x + region.radius,
        min_z: region.base_z - region.radius,
        max_z: region.base_z + region.radius,
      };
    case "circle":
      return {
        min_x: region.center_x - region.radius,
        max_x: region.center_x + region.radius,
        min_z: region.center_z - region.radius,
        max_z: region.center_z + region.radius,
      };
    case "sphere":
      return {
        min_x: region.origin_x - region.radius,
        max_x: region.origin_x + region.radius,
        min_z: region.origin_z - region.radius,
        max_z: region.origin_z + region.radius,
      };
    case "block":
      return blockToExtentBounds(region.x, region.z);
    case "point":
      return {
        min_x: region.x - 0.5,
        max_x: region.x + 0.5,
        min_z: region.z - 0.5,
        max_z: region.z + 0.5,
      };
    default:
      return null;
  }
}

// ── Symmetry transform on a point ─────────────────────────────────────────────

export function applySymmetry(x, z, axis, cx, cz) {
  switch (axis) {
    case "mirror_x": return [2 * cx - x, z];
    case "mirror_z": return [x, 2 * cz - z];
    case "rot_180":  return [2 * cx - x, 2 * cz - z];
    case "rot_90":   return [cx - (z - cz), cz + (x - cx)];
    default: throw new Error(`Unknown symmetry axis: ${axis}`);
  }
}

// ── Symmetry transform on extent bounds ──────────────────────────────────────

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

// ── Rasterise polygon → block list ───────────────────────────────────────────

function _pointInRing(px, pz, ring) {
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
      if (!_pointInRing(cx, cz, exterior)) continue;
      if (holes.some(h => _pointInRing(cx, cz, h))) continue;
      result.push([x, z]);
    }
  }
  return result;
}

// ── Sketch shape → PGM region ─────────────────────────────────────────────────

export function sketchShapeToPgmRegion(shape) {
  switch (shape.type) {
    case "rectangle": {
      const { min_x, max_x, min_z, max_z } = shape;
      return { type: "rectangle", min_x, max_x, min_z, max_z };
    }
    case "circle": {
      const { center_x, center_z, radius } = shape;
      return { type: "circle", center_x, center_z, radius };
    }
    default:
      return null;
  }
}
