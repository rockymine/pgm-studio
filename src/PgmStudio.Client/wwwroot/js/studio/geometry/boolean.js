/**
 * Boolea group computation for the Sketch tool — the one genuinely sketch-domain geometry layer.
 * Converts primitive shapes to rings (geometry/shape.js), runs union/difference via the vendored
 * polygon-clipping bundle, extracts connected-component groups, and assigns shapes to the groups
 * they contribute to. Also computes the live mirror-preview polygons for a symmetry axis.
 *
 * This drives the *live* canvas preview (the hot path stays in JS); the server rasterizes from shapes
 * for the persisted geometry (docs/tools/sketch.md). No DOM.
 */

import polygonClipping from "../vendor/polygon-clipping.js";
import { toRing, ringCentroid } from "./shape.js";
import { applySymmetry } from "./symmetry.js";
import { pointInRing, polysOverlap } from "./polygon.js";

/** Convert a shape to a polygon-clipping MultiPolygon `[[ring]]` (empty for a degenerate shape). */
export function shapeToMultiPoly(shape) {
  const ring = toRing(shape);
  return ring.length ? [[ring]] : [];
}

/** Point-in-group test: inside the exterior and outside every hole. */
export function pointInGroup(px, pz, group) {
  if (!pointInRing(px, pz, group.exterior)) return false;
  return !group.holes.some(h => pointInRing(px, pz, h));
}

// ── Main boolean computation ──────────────────────────────────────────────────

/**
 * Compute groups from the given shapes.
 *
 * Evaluation order:
 *   1. union(normal adds)
 *   2. − union(normal subtracts)
 *   3. ∪ union(override adds)       ← immune to normal subtracts
 *   4. − union(override subtracts)  ← cuts through everything
 *
 * Returns `{ groups, addUnion, afterSub, overrideAddUnion }`. `groups` is
 * `[{ id, name, mirrors, exterior, holes, shapeIds }]` — names/mirror flags are carried over from
 * `previousGroups` by centroid proximity; `shapeIds` is filled by assignShapesToGroups.
 */
export function computeGroups(shapes, previousGroups = []) {
  const normalAdds   = shapes.filter(s => s.operation !== "subtract" && !s.override);
  const overrideAdds = shapes.filter(s => s.operation !== "subtract" &&  s.override);
  const normalSubs   = shapes.filter(s => s.operation === "subtract"  && !s.override);
  const overrideSubs = shapes.filter(s => s.operation === "subtract"  &&  s.override);

  if (normalAdds.length === 0 && overrideAdds.length === 0) {
    return { groups: [], addUnion: [], afterSub: [], overrideAddUnion: [] };
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

  // Build group objects, carrying name/mirror from previous groups matched by centroid proximity.
  const prevCentroids = previousGroups.map(isl => ({
    isl,
    cx: ringCentroid(isl.exterior)[0],
    cz: ringCentroid(isl.exterior)[1],
  }));

  const MATCH_THRESHOLD = 32; // blocks — centroids further apart → a new group
  const matchedPrev = new Set();

  const groups = result.map((poly, i) => {
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
      name:    best?.name    ?? `Group ${i + 1}`,
      mirrors: best?.mirrors ?? true,
      exterior,
      holes,
      shapeIds: [],
    };
  });

  return { groups, addUnion: normalUnion, afterSub, overrideAddUnion: afterOverrideAdd };
}

// ── Shape → group assignment ─────────────────────────────────────────────────

/**
 * Assign each shape to the group(s) it contributes to and populate `group.shapeIds`. Uses polygon
 * intersection (not centroid) so a subtract spanning multiple groups appears under all of them.
 * Mutates `groups` in place.
 */
export function assignShapesToGroups(shapes, groups, addUnion, overrideAddUnion, afterSub) {
  if (!groups.length) return;

  const groupPolys = groups.map(isl => [[isl.exterior, ...isl.holes]]);
  const toNormalIdx   = _mapGroupsToUnion(groups, addUnion);
  const toOverrideIdx = _mapGroupsToUnion(groups, overrideAddUnion ?? []);
  const normalPath    = _normalPathSet(groups, afterSub);

  for (const shape of shapes) {
    const sp = shapeToMultiPoly(shape);
    if (!sp.length) continue;
    const toAssign = new Set();

    if (shape.operation === "subtract" && !shape.override) {
      for (let j = 0; j < addUnion.length; j++) {
        if (!_intersects(sp, [addUnion[j]])) continue;
        for (let i = 0; i < groups.length; i++) {
          if (toNormalIdx[i] === j && normalPath.has(i)) toAssign.add(i);
        }
      }
    } else if (shape.operation === "subtract" && shape.override) {
      _intersectUnionComponents(sp, overrideAddUnion ?? [], toOverrideIdx, groups, toAssign);
    } else if (shape.override) {
      for (let i = 0; i < groups.length; i++) {
        if (_intersects(sp, groupPolys[i])) toAssign.add(i);
      }
    } else {
      for (let j = 0; j < addUnion.length; j++) {
        if (!_intersects(sp, [addUnion[j]])) continue;
        const peers = groups.reduce((acc, _, i) => {
          if (toNormalIdx[i] === j && normalPath.has(i)) acc.push(i);
          return acc;
        }, []);
        if (peers.length === 1) {
          toAssign.add(peers[0]);
        } else {
          for (const i of peers) {
            if (_intersects(sp, groupPolys[i])) toAssign.add(i);
          }
        }
      }
    }

    for (const i of toAssign) groups[i].shapeIds.push(shape.id);
  }
}

function _mapGroupsToUnion(groups, union) {
  return groups.map(isl => {
    if (!union.length) return -1;
    const groupPoly = [[isl.exterior, ...isl.holes]];
    for (let j = 0; j < union.length; j++) {
      if (_intersects(groupPoly, [union[j]])) return j;
    }
    return -1;
  });
}

function _intersectUnionComponents(sp, union, toComponentIdx, groups, toAssign) {
  for (let j = 0; j < union.length; j++) {
    if (!_intersects(sp, [union[j]])) continue;
    for (let i = 0; i < groups.length; i++) {
      if (toComponentIdx[i] === j) toAssign.add(i);
    }
  }
}

// Do these two multipolygons share ground? The clipper answers it, and where the clipper refuses the
// question — a sweepline failure on a vertex a fraction of a block off another shape's edge — the rings do.
// A thrown answer is not "no": read as one it takes the shape out of every group it belongs to, and a shape
// in no group is rasterized where it was drawn and never fanned onto its symmetry orbit.
function _intersects(a, b) {
  try { return polygonClipping.intersection(a, b).length > 0; }
  catch (err) {
    console.warn("boolean: intersection failed, reading the rings instead —", err?.message ?? err);
    return a.some(polyA => b.some(polyB => polysOverlap(polyA, polyB)));
  }
}

// Group indices that have solid area in afterSub (produced by the normal-subtract step, not purely
// by an override-add inside a hole). When afterSub is empty, all groups are on the normal path.
function _normalPathSet(groups, afterSub) {
  if (!afterSub || !afterSub.length) return new Set(groups.map((_, i) => i));
  const result = new Set();
  for (let i = 0; i < groups.length; i++) {
    const extPoly = [[groups[i].exterior]]; // exterior ring as a filled polygon
    for (const comp of afterSub) {
      if (_intersects(extPoly, [comp])) { result.add(i); break; }
    }
  }
  return result;
}

// ── Mirror preview ────────────────────────────────────────────────────────────

/**
 * Live mirror-preview polygons for a set of groups + a symmetry axis. rot_90 → three copies
 * (90/180/270); other modes → one. Returns `[{ sourceId, exterior, holes }]` for groups with
 * `mirrors === true`.
 */
export function computeMirrorPreview(groups, axis, cx, cz) {
  const result = [];
  for (const isl of groups) {
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
 * Apply saved group metadata to computed groups by matching on shapeId overlap.
 *
 * **A saved record is claimed by one group.** One of the fields carried across is the id, and an id is
 * what a relief is keyed by, so handing the same record to two groups writes a layout in which two
 * groups answer to one name — the relief then belongs to whichever of them the solver reaches first and
 * the rest of the board comes back flat. The pairing is therefore resolved greedily by overlap: the
 * strongest (group, record) pair first, then the next over what is left, and a group no record
 * reaches keeps the identity it was computed with.
 *
 * @param {object[]} groups   from computeGroups (shapeIds populated)
 * @param {object[]} savedMeta persisted group records ({shapeIds, …fields})
 * @param {string[]} fields    which fields to copy from the matched record onto each group
 */
export function restoreGroupMeta(groups, savedMeta, fields) {
  if (!savedMeta.length) return;
  const pairs = [];
  for (const isl of groups) {
    for (const meta of savedMeta) {
      const saved = new Set(meta.shapeIds ?? []);
      const overlap = isl.shapeIds.reduce((n, sid) => n + (saved.has(sid) ? 1 : 0), 0);
      if (overlap > 0) pairs.push({ isl, meta, overlap });
    }
  }
  pairs.sort((a, b) => b.overlap - a.overlap);
  const takenGroups = new Set(), takenMeta = new Set();
  for (const { isl, meta } of pairs) {
    if (takenGroups.has(isl) || takenMeta.has(meta)) continue;
    takenGroups.add(isl); takenMeta.add(meta);
    for (const field of fields) {
      if (meta[field] !== undefined) isl[field] = meta[field];
    }
  }
}
