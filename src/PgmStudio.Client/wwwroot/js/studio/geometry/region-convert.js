/**
 * PGM-contract conversions between region/shape descriptors and 2D display bounds.
 * These encode contract semantics (the +1 rule, region type → footprint) and are kept
 * separate from the generic shape geometry. No DOM.
 * Each conversion has exactly one implementation here.
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
