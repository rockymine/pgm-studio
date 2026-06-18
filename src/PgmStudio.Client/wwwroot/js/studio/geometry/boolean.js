/**
 * Boolean island computation for the Sketch tool — the one genuinely sketch-domain geometry layer.
 * Converts primitive shapes to rings (geometry/shape.js), runs union/difference via the vendored
 * polygon-clipping bundle, extracts connected-component islands, and assigns shapes to the islands
 * they contribute to. Also computes the live mirror-preview polygons for a symmetry axis.
 *
 * This drives the *live* canvas preview (the hot path stays in JS); the server rasterizes from shapes
 * for the persisted geometry (docs/contracts/sketch-authoring.md §4). No DOM.
 */

import polygonClipping from "../vendor/polygon-clipping.js";
import { toRing, ringCentroid } from "./shape.js";
import { applySymmetry } from "./symmetry.js";
import { pointInRing } from "./polygon.js";

/** Convert a shape to a polygon-clipping MultiPolygon `[[ring]]` (empty for a degenerate shape). */
export function shapeToMultiPoly(shape) {
  const ring = toRing(shape);
  return ring.length ? [[ring]] : [];
}

/** Point-in-island test: inside the exterior and outside every hole. */
export function pointInIsland(px, pz, island) {
  if (!pointInRing(px, pz, island.exterior)) return false;
  return !island.holes.some(h => pointInRing(px, pz, h));
}

// ── Main boolean computation ──────────────────────────────────────────────────

/**
 * Compute islands from the given shapes.
 *
 * Evaluation order:
 *   1. union(normal adds)
 *   2. − union(normal subtracts)
 *   3. ∪ union(override adds)       ← immune to normal subtracts
 *   4. − union(override subtracts)  ← cuts through everything
 *
 * Returns `{ islands, addUnion, afterSub, overrideAddUnion }`. `islands` is
 * `[{ id, name, mirrors, exterior, holes, shapeIds }]` — names/mirror flags are carried over from
 * `previousIslands` by centroid proximity; `shapeIds` is filled by assignShapesToIslands.
 */
export function computeIslands(shapes, previousIslands = []) {
  const normalAdds   = shapes.filter(s => s.operation !== "subtract" && !s.override);
  const overrideAdds = shapes.filter(s => s.operation !== "subtract" &&  s.override);
  const normalSubs   = shapes.filter(s => s.operation === "subtract"  && !s.override);
  const overrideSubs = shapes.filter(s => s.operation === "subtract"  &&  s.override);

  if (normalAdds.length === 0 && overrideAdds.length === 0) {
    return { islands: [], addUnion: [], afterSub: [], overrideAddUnion: [] };
  }

  // Step 1 — union normal adds.
  let normalUnion = [];
  if (normalAdds.length > 0) {
    try {
      const polys = normalAdds.map(shapeToMultiPoly).filter(p => p.length);
      if (polys.length) normalUnion = polygonClipping.union(polys[0], ...polys.slice(1));
    } catch (err) { console.warn("boolean: normal-add union error", err); }
  }

  // Step 2 — subtract normal subs from the union.
  let afterSub = normalUnion;
  if (normalSubs.length > 0 && normalUnion.length > 0) {
    try {
      const subPolys = normalSubs.map(shapeToMultiPoly).filter(p => p.length);
      if (subPolys.length) afterSub = polygonClipping.difference(normalUnion, ...subPolys);
    } catch (err) { console.warn("boolean: normal-sub difference error", err); }
  }

  // Step 3 — union override adds (immune to normal subtracts).
  let afterOverrideAdd = afterSub;
  if (overrideAdds.length > 0) {
    try {
      const polys = overrideAdds.map(shapeToMultiPoly).filter(p => p.length);
      if (polys.length) {
        afterOverrideAdd = afterSub.length > 0
          ? polygonClipping.union(afterSub, ...polys)
          : polygonClipping.union(polys[0], ...polys.slice(1));
      }
    } catch (err) { console.warn("boolean: override-add union error", err); }
  }

  // Step 4 — override subs cut last (through everything).
  let result = afterOverrideAdd;
  if (overrideSubs.length > 0 && afterOverrideAdd.length > 0) {
    try {
      const subPolys = overrideSubs.map(shapeToMultiPoly).filter(p => p.length);
      if (subPolys.length) result = polygonClipping.difference(afterOverrideAdd, ...subPolys);
    } catch (err) { console.warn("boolean: override-sub difference error", err); }
  }

  // Build island objects, carrying name/mirror from previous islands matched by centroid proximity.
  const prevCentroids = previousIslands.map(isl => ({
    isl,
    cx: ringCentroid(isl.exterior)[0],
    cz: ringCentroid(isl.exterior)[1],
  }));

  const MATCH_THRESHOLD = 32; // blocks — centroids further apart → a new island
  const matchedPrev = new Set();

  const islands = result.map((poly, i) => {
    const exterior = poly[0];
    const holes    = poly.slice(1);
    const [ncx, ncz] = ringCentroid(exterior);

    let best = null, bestDist = MATCH_THRESHOLD, bestIdx = -1;
    for (let j = 0; j < prevCentroids.length; j++) {
      if (matchedPrev.has(j)) continue;
      const { cx, cz, isl } = prevCentroids[j];
      const d = Math.hypot(ncx - cx, ncz - cz);
      if (d < bestDist) { bestDist = d; best = isl; bestIdx = j; }
    }
    if (bestIdx !== -1) matchedPrev.add(bestIdx);

    return {
      id:      best?.id      ?? `isl_${Date.now()}_${i}`,
      name:    best?.name    ?? `Island ${i + 1}`,
      mirrors: best?.mirrors ?? true,
      exterior,
      holes,
      shapeIds: [],
    };
  });

  return { islands, addUnion: normalUnion, afterSub, overrideAddUnion: afterOverrideAdd };
}

// ── Shape → island assignment ─────────────────────────────────────────────────

/**
 * Assign each shape to the island(s) it contributes to and populate `island.shapeIds`. Uses polygon
 * intersection (not centroid) so a subtract spanning multiple islands appears under all of them.
 * Mutates `islands` in place.
 */
export function assignShapesToIslands(shapes, islands, addUnion, overrideAddUnion, afterSub) {
  if (!islands.length) return;

  const islandPolys = islands.map(isl => [[isl.exterior, ...isl.holes]]);
  const toNormalIdx   = _mapIslandsToUnion(islands, addUnion);
  const toOverrideIdx = _mapIslandsToUnion(islands, overrideAddUnion ?? []);
  const normalPath    = _normalPathSet(islands, afterSub);

  for (const shape of shapes) {
    const sp = shapeToMultiPoly(shape);
    if (!sp.length) continue;
    const toAssign = new Set();

    if (shape.operation === "subtract" && !shape.override) {
      for (let j = 0; j < addUnion.length; j++) {
        if (!_intersects(sp, [addUnion[j]])) continue;
        for (let i = 0; i < islands.length; i++) {
          if (toNormalIdx[i] === j && normalPath.has(i)) toAssign.add(i);
        }
      }
    } else if (shape.operation === "subtract" && shape.override) {
      _intersectUnionComponents(sp, overrideAddUnion ?? [], toOverrideIdx, islands, toAssign);
    } else if (shape.override) {
      for (let i = 0; i < islands.length; i++) {
        if (_intersects(sp, islandPolys[i])) toAssign.add(i);
      }
    } else {
      for (let j = 0; j < addUnion.length; j++) {
        if (!_intersects(sp, [addUnion[j]])) continue;
        const peers = islands.reduce((acc, _, i) => {
          if (toNormalIdx[i] === j && normalPath.has(i)) acc.push(i);
          return acc;
        }, []);
        if (peers.length === 1) {
          toAssign.add(peers[0]);
        } else {
          for (const i of peers) {
            if (_intersects(sp, islandPolys[i])) toAssign.add(i);
          }
        }
      }
    }

    for (const i of toAssign) islands[i].shapeIds.push(shape.id);
  }
}

function _mapIslandsToUnion(islands, union) {
  return islands.map(isl => {
    if (!union.length) return -1;
    const islandPoly = [[isl.exterior, ...isl.holes]];
    for (let j = 0; j < union.length; j++) {
      if (_intersects(islandPoly, [union[j]])) return j;
    }
    return -1;
  });
}

function _intersectUnionComponents(sp, union, toComponentIdx, islands, toAssign) {
  for (let j = 0; j < union.length; j++) {
    if (!_intersects(sp, [union[j]])) continue;
    for (let i = 0; i < islands.length; i++) {
      if (toComponentIdx[i] === j) toAssign.add(i);
    }
  }
}

function _intersects(a, b) {
  try { return polygonClipping.intersection(a, b).length > 0; } catch { return false; }
}

// Island indices that have solid area in afterSub (produced by the normal-subtract step, not purely
// by an override-add inside a hole). When afterSub is empty, all islands are on the normal path.
function _normalPathSet(islands, afterSub) {
  if (!afterSub || !afterSub.length) return new Set(islands.map((_, i) => i));
  const result = new Set();
  for (let i = 0; i < islands.length; i++) {
    const extPoly = [[islands[i].exterior]]; // exterior ring as a filled polygon
    for (const comp of afterSub) {
      if (_intersects(extPoly, [comp])) { result.add(i); break; }
    }
  }
  return result;
}

// ── Mirror preview ────────────────────────────────────────────────────────────

/**
 * Live mirror-preview polygons for a set of islands + a symmetry axis. rot_90 → three copies
 * (90/180/270); other modes → one. Returns `[{ sourceId, exterior, holes }]` for islands with
 * `mirrors === true`.
 */
export function computeMirrorPreview(islands, axis, cx, cz) {
  const result = [];
  for (const isl of islands) {
    if (!isl.mirrors) continue;
    const copies = axis === "rot_90" ? ["rot_90", "rot_180", "rot_270"] : [axis];
    for (const copyAxis of copies) {
      result.push({
        sourceId: isl.id,
        exterior: _transformRing(isl.exterior, copyAxis, cx, cz),
        holes:    isl.holes.map(h => _transformRing(h, copyAxis, cx, cz)),
      });
    }
  }
  return result;
}

function _transformRing(ring, axis, cx, cz) {
  // rot_270 CCW = rot_90 CW: (Δx,Δz) → (Δz, −Δx). Other axes go through applySymmetry.
  if (axis === "rot_270") {
    return ring.map(([x, z]) => {
      const dx = x - cx, dz = z - cz;
      return [cx + dz, cz - dx];
    });
  }
  return ring.map(([x, z]) => applySymmetry(x, z, axis, cx, cz));
}

/**
 * Apply saved island metadata to computed islands by matching on shapeId overlap.
 * @param {object[]} islands   from computeIslands (shapeIds populated)
 * @param {object[]} savedMeta persisted island records ({shapeIds, …fields})
 * @param {string[]} fields    which fields to copy from the best match onto each island
 */
export function restoreIslandMeta(islands, savedMeta, fields) {
  if (!savedMeta.length) return;
  for (const isl of islands) {
    let best = null, bestScore = 0;
    for (const meta of savedMeta) {
      const overlap = isl.shapeIds.filter(sid => (meta.shapeIds ?? []).includes(sid)).length;
      if (overlap > bestScore) { bestScore = overlap; best = meta; }
    }
    if (!best || bestScore === 0) continue;
    for (const field of fields) {
      if (best[field] !== undefined) isl[field] = best[field];
    }
  }
}
