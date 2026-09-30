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
import { toRing, ringCentroid, snapShape } from "./shape.js";
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

// ── The groups a layer is saved with ─────────────────────────────────────────

/**
 * What a shape's outline is, as a comparable key: its operation, whether it overrides, and the ring it
 * rasterizes to. Grouping reads nothing else, so a theme, a height or a name changed on a shape leaves the
 * key — and the shape's group — where they were.
 */
export function outlineKey(shape) {
  let ring;
  try { ring = toRing(snapShape(shape)); } catch { ring = []; }
  return `${shape.operation ?? "add"}|${shape.override ? 1 : 0}|${JSON.stringify(ring)}`;
}

/**
 * The groups a layer holds, settled against the ones it was loaded with.
 *
 * **A stated group is kept exactly as stated until an edit touches it.** The server builds a group's
 * mirror fan, its relief and its keep-clear from the `shapeIds` it lists, and an API caller can state a
 * group the geometry would not have made — pieces that are not one island, or one shape held apart from the
 * island it overlaps. So a stated group keeps its id, name, `mirrors` and `shapeIds` while none of its shapes
 * has changed outline or gone, and no new or reshaped shape overlaps one of them. Every other shape — the
 * members of a touched group, and any shape no stated group lists — is grouped from the geometry the way a
 * drawn board always is: connected pieces, identity carried from the stated record the piece overlaps most,
 * else from the previous group by centroid.
 *
 * Answers the groups as canvas parts `{id, name, mirrors, shapeIds, exterior, holes}`: a group whose shapes
 * are several islands is one part per island, each carrying the whole group's `shapeIds`, so the saved
 * document is `uniqueGroups` of the answer.
 *
 * @param {object[]} shapes   the layer's terrain shapes as they stand
 * @param {object[]} stated   the groups the layer was loaded with ({id, name, mirrors, shapeIds})
 * @param {Map<string,string>} baseline shape id -> `outlineKey` as loaded
 * @param {object[]} previous the parts the last settle answered, for a piece no stated record reaches
 */
export function settleGroups(shapes, stated = [], baseline = new Map(), previous = []) {
  const byId = new Map(shapes.map(shape => [shape.id, shape]));
  const touched = shapes.filter(shape => baseline.get(shape.id) !== outlineKey(shape));
  const touchedIds = new Set(touched.map(shape => shape.id));
  const touchedPolys = touched.map(shape => [toRing(shape)]).filter(poly => poly[0].length);

  const keeps = (group) => {
    const members = group.shapeIds ?? [];
    if (!members.length || members.some(id => !byId.has(id) || touchedIds.has(id))) return false;
    return !members.some(id => {
      const ring = toRing(byId.get(id));
      return ring.length && touchedPolys.some(poly => polysOverlap([ring], poly));
    });
  };
  const kept = stated.filter(keeps);
  const keptShapes = new Set(kept.flatMap(group => group.shapeIds));

  // Everything a kept group does not hold is grouped from the geometry, among itself.
  const free = shapes.filter(shape => !keptShapes.has(shape.id));
  const keptIds = new Set(kept.map(group => group.id));
  const { groups: pieces, addUnion, afterSub, overrideAddUnion } =
    computeGroups(free, previous.filter(part => !keptIds.has(part.id)));
  assignShapesToGroups(free, pieces, addUnion, overrideAddUnion, afterSub);
  const touchedStated = stated.filter(group => !keptIds.has(group.id));
  const matched = restoreGroupMeta(pieces, touchedStated, ["id", "name", "mirrors"]);

  // A piece whose carried id another group already answers to takes a fresh one: an id is what a relief is
  // keyed by, and two groups answering to it would hand one of them the other's ground.
  const used = new Set([...keptIds, ...[...matched].map(piece => piece.id)]);
  let fresh = 0;
  pieces.forEach((piece, index) => {
    if (matched.has(piece)) return;
    if (!used.has(piece.id)) { used.add(piece.id); return; }
    do { piece.id = `isl_${Date.now()}_${fresh++}`; } while (used.has(piece.id));
    piece.name = `Group ${index + 1}`;
    piece.mirrors = true;
    used.add(piece.id);
  });

  // The kept groups' parts, each island of its own shapes one part.
  const partsOf = (group) => {
    const members = group.shapeIds.map(id => byId.get(id));
    const outline = computeGroups(members, []).groups;
    const base = { id: group.id, name: group.name, mirrors: group.mirrors, shapeIds: [...group.shapeIds] };
    return outline.length
      ? outline.map(part => ({ ...base, exterior: part.exterior, holes: part.holes }))
      : [{ ...base, exterior: [], holes: [] }];
  };

  // In the order the layer stated them, a touched group standing where its piece took its id; new pieces
  // after.
  const out = [];
  const placed = new Set();
  for (const group of stated) {
    if (keptIds.has(group.id)) { out.push(...partsOf(group)); continue; }
    for (const piece of pieces) if (piece.id === group.id && !placed.has(piece)) { out.push(piece); placed.add(piece); }
  }
  for (const piece of pieces) if (!placed.has(piece)) out.push(piece);
  return out;
}

/** The groups as the document states them: one record per id, from the first part carrying it. */
export function uniqueGroups(parts) {
  const seen = new Set();
  const out = [];
  for (const part of parts) {
    if (seen.has(part.id)) continue;
    seen.add(part.id);
    out.push({ id: part.id, name: part.name, mirrors: part.mirrors, shapeIds: part.shapeIds });
  }
  return out;
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
 * @returns {Set<object>} the groups a record was matched to
 */
export function restoreGroupMeta(groups, savedMeta, fields) {
  const takenGroups = new Set();
  if (!savedMeta.length) return takenGroups;
  const pairs = [];
  for (const isl of groups) {
    for (const meta of savedMeta) {
      const saved = new Set(meta.shapeIds ?? []);
      const overlap = isl.shapeIds.reduce((n, sid) => n + (saved.has(sid) ? 1 : 0), 0);
      if (overlap > 0) pairs.push({ isl, meta, overlap });
    }
  }
  pairs.sort((a, b) => b.overlap - a.overlap);
  const takenMeta = new Set();
  for (const { isl, meta } of pairs) {
    if (takenGroups.has(isl) || takenMeta.has(meta)) continue;
    takenGroups.add(isl); takenMeta.add(meta);
    for (const field of fields) {
      if (meta[field] !== undefined) isl[field] = meta[field];
    }
  }
  return takenGroups;
}
