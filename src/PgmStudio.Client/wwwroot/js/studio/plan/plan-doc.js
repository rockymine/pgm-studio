/**
 * Pure plan-document model + geometry for the plan editor — NO DOM. All footprint coordinates are
 * signed integer proxy *cells* relative to the symmetry centre (the origin); one cell spans
 * `globals.cell` blocks. Pieces/zones are `[x, z, w, h]` cell rects; marker positions are stored
 * piece-relative (`piece` id + `at` cell offset). The document object mirrors the plan wire format
 * (`*.plan.json`) so import/export round-trips it verbatim.
 *
 * The canvas renders in block units: `rectCellsToBlocks` scales a cell rect by `globals.cell`, and the
 * symmetry mirror ghost is computed here via the shared geometry/symmetry helpers (never re-derived).
 */

import { applySymmetry, applySymmetryToBounds, orbitAxes } from "../geometry/symmetry.js";

// Piece roles — the left-toolbar palette, in display order. Pieces are anonymous by default (one neutral
// tint); the two intent-bearing roles (wool-room / spawn) keep distinct tints. Colours are theme-independent
// so a piece reads the same on the dark canvas in either theme; the fill is tinted lighter for a higher
// surface. Legacy role names (lane/hub/mid) map to "piece" on load.
export const ROLES = ["piece", "wool-room", "spawn"];
export const ROLE_COLORS = { piece: "#7c8899", "wool-room": "#3fae74", spawn: "#8f7bd6" };
export const ROLE_LABELS = { piece: "Piece", "wool-room": "Wool room", spawn: "Spawn" };

/** Fold a raw (possibly legacy or unknown) role down to a canonical one: only wool-room / spawn survive. */
export function canonicalRole(role) { return role === "wool-room" || role === "spawn" ? role : "piece"; }

// Marker facing cycles front → right → back → left on repeated clicks; the arrow points along a fixed
// screen direction per enum (front = up / −Z, matching "toward the centre" for a piece on the +Z side).
export const FACINGS = ["front", "right", "back", "left"];
export const FACING_DIR = { front: [0, -1], right: [1, 0], back: [0, 1], left: [-1, 0] };
export function nextFacing(f) { const i = FACINGS.indexOf(f); return FACINGS[(i + 1) % FACINGS.length]; }

/** A blank plan document (wire shape) with the schema defaults. */
export function emptyDoc() {
  return {
    plan: 1,
    meta: { name: "Untitled plan" },
    globals: { cell: 5, symmetry: "rot_180", maxPlayers: 12, surface: 9, headroom: 11 },
    pieces: [],
    zones: [],
    placements: { spawns: [], wools: [], iron: [] },
    cliffs: [],
    walls: [],
  };
}

/**
 * Normalise a parsed plan into a full document: fill defaults, guarantee every array/sub-array exists,
 * and drop unset optionals (so re-serialising mirrors the wire format's omit-when-null behaviour).
 * Deep-copies so the result never aliases the input.
 */
export function normalizeDoc(d) {
  const src = d || {};
  const g = src.globals || {};
  const globals = { cell: g.cell ?? 5, symmetry: g.symmetry ?? "rot_180", maxPlayers: g.maxPlayers ?? 12, surface: g.surface ?? 9, headroom: g.headroom ?? 11 };
  if (g.observerY != null) globals.observerY = g.observerY;
  const meta = { name: src.meta?.name ?? "Untitled plan" };
  if (src.meta?.notes != null) meta.notes = src.meta.notes;
  return {
    plan: src.plan ?? 1,
    meta,
    globals,
    pieces: (src.pieces || []).map(p => {
      const o = { id: p.id ?? "", role: canonicalRole(p.role), rect: [...(p.rect || [0, 0, 1, 1])] };
      if (p.surface != null) o.surface = p.surface;
      if (p.mirrors != null) o.mirrors = p.mirrors;
      return o;
    }),
    zones: (src.zones || []).map(z => ({ id: z.id ?? "", rect: [...(z.rect || [0, 0, 1, 1])], holes: (z.holes || []).map(h => [...h]) })),
    placements: {
      spawns: (src.placements?.spawns || []).map(s => ({ piece: s.piece ?? "", at: [...(s.at || [0, 0])], facing: s.facing ?? "front" })),
      wools: (src.placements?.wools || []).map(w => { const o = { piece: w.piece ?? "", at: [...(w.at || [0, 0])] }; if (w.color) o.color = w.color; return o; }),
      iron: (src.placements?.iron || []).map(i => ({ piece: i.piece ?? "", at: [...(i.at || [0, 0])] })),
    },
    cliffs: (src.cliffs || []).map(c => ({ a: c.a ?? "", b: c.b ?? "" })),
    walls: (src.walls || []).map(c => ({ a: c.a ?? "", b: c.b ?? "" })),
  };
}

/** Parse a plan JSON string into a normalised document, or throw if it isn't a plan object. */
export function fromJson(text) {
  const parsed = JSON.parse(text);
  if (!parsed || typeof parsed !== "object" || !("plan" in parsed)) throw new Error("Not a plan document");
  return normalizeDoc(parsed);
}

/** Serialise a document back to the plan wire format (pretty-printed, matching the seed files). */
export function toJson(doc) { return JSON.stringify(normalizeDoc(doc), null, 2); }

// ── coordinate + rect helpers ───────────────────────────────────────────────

/** A `[x, z, w, h]` cell rect → block AABB `{min_x, min_z, max_x, max_z}` at the given cell size. */
export function rectCellsToBlocks(rect, cell) {
  const [x, z, w, h] = rect;
  return { min_x: x * cell, min_z: z * cell, max_x: (x + w) * cell, max_z: (z + h) * cell };
}

/** The cell containing a world/block point (floored so a point maps to exactly one cell). */
export function cellOfWorld(wx, wz, cell) { return [Math.floor(wx / cell), Math.floor(wz / cell)]; }

/** A cell rect covering the two (inclusive) corner cells of a click-drag — always ≥ 1×1. */
export function rectFromCells(ax, az, bx, bz) {
  const x = Math.min(ax, bx), z = Math.min(az, bz);
  return [x, z, Math.abs(bx - ax) + 1, Math.abs(bz - az) + 1];
}

/** True if cell `(cx, cz)` falls inside the `[x, z, w, h]` cell rect. */
export function rectContainsCell(rect, cx, cz) {
  const [x, z, w, h] = rect;
  return cx >= x && cx < x + w && cz >= z && cz < z + h;
}

export function pieceById(doc, id) { return doc.pieces.find(p => p.id === id) || null; }

/** The topmost piece whose footprint contains cell `(cx, cz)`, or null. */
export function pieceAtCell(doc, cx, cz) {
  for (let i = doc.pieces.length - 1; i >= 0; i--) if (rectContainsCell(doc.pieces[i].rect, cx, cz)) return doc.pieces[i];
  return null;
}

/** The topmost zone whose footprint contains cell `(cx, cz)`, or null. */
export function zoneAtCell(doc, cx, cz) {
  for (let i = doc.zones.length - 1; i >= 0; i--) if (rectContainsCell(doc.zones[i].rect, cx, cz)) return doc.zones[i];
  return null;
}

// ── pick priority (markers paint above pieces, so they pick first) ───────────

/** Pick radius, in cell units, of a marker's visual disc/box — a click this close to a centre hits it. */
export const MARKER_HIT_CELLS = 0.42;

/**
 * The marker whose visual disc/box is nearest a world/block point within its pick radius, or null. Markers
 * render on top of pieces, so this must be consulted before `pieceAtCell` — a click inside a marker's radius
 * selects the marker even when a piece lies under it. Ties break to the later-painted (topmost) marker.
 */
export function markerAtWorld(doc, wx, wz) {
  const cell = doc.globals.cell;
  const r = MARKER_HIT_CELLS * cell;
  let best = null, bestD = r;
  for (const { kind, index, marker } of allMarkers(doc)) {
    const c = markerCell(doc, marker);
    if (!c) continue;
    const mx = c[0] * cell, mz = c[1] * cell;
    const d = Math.hypot(wx - mx, wz - mz);
    if (d <= bestD) { bestD = d; best = { kind: "marker", markerKind: kind, index }; }
  }
  return best;
}

/**
 * The item a click at world/block point `(wx, wz)` selects, honouring paint order: a marker (topmost, within
 * its pick radius) first, then the topmost containing piece, then a zone. Returns a selection ref or null.
 */
export function pickAtWorld(doc, wx, wz) {
  const m = markerAtWorld(doc, wx, wz);
  if (m) return m;
  const [cx, cz] = cellOfWorld(wx, wz, doc.globals.cell);
  const p = pieceAtCell(doc, cx, cz);
  if (p) return { kind: "piece", id: p.id };
  const z = zoneAtCell(doc, cx, cz);
  if (z) return { kind: "zone", id: z.id };
  return null;
}

/** True if two selection refs point at the same item (piece/zone id, or marker kind+index). */
export function sameSelection(a, b) {
  if (!a || !b || a.kind !== b.kind) return false;
  if (a.kind === "marker") return a.markerKind === b.markerKind && a.index === b.index;
  return a.id === b.id;
}

/** A piece's surface height, resolving the inherited base from globals when the piece has none set. */
export function pieceSurface(doc, p) { return p.surface ?? doc.globals.surface; }

/** The min/max resolved surface height across every piece, or null for a piece-less document. */
export function surfaceRange(doc) {
  let min = null, max = null;
  for (const p of doc.pieces) {
    const s = pieceSurface(doc, p);
    if (min == null || s < min) min = s;
    if (max == null || s > max) max = s;
  }
  return min == null ? null : { min, max };
}

/**
 * A piece's fraction (0..1) along the plan's surface range — 0 = lowest, 1 = highest — for the height-map
 * fill ramp. A flat plan (min == max, or a single piece) maps everything to the top of the ramp.
 */
export function surfaceFraction(surf, range) {
  if (!range || range.max <= range.min) return 1;
  return (surf - range.min) / (range.max - range.min);
}

/** Snap a value to the nearest half-cell step (0.5 in cell units) — the marker lattice. */
export function snapHalf(v) { return Math.round(v * 2) / 2; }

/** The absolute cell of a marker (its piece's origin + the piece-relative offset), or null if orphaned. */
export function markerCell(doc, marker) {
  const p = pieceById(doc, marker.piece);
  return p ? [p.rect[0] + marker.at[0], p.rect[1] + marker.at[1]] : null;
}

/**
 * Attach a marker dropped at absolute cell `(cx, cz)` (fractional allowed): the piece under it + the
 * piece-relative offset snapped to the nearest half-cell lattice point, or null when no piece sits under
 * the cell (markers must ride a piece). The offset resolves to block `piece.min + at·cell`, so an integer
 * offset lands on a cell corner (the centre of a 2×2-cell room) and a half offset on a cell centre.
 */
export function attachMarker(doc, cx, cz) {
  const p = pieceAtCell(doc, Math.floor(cx), Math.floor(cz));
  return p ? { piece: p.id, at: [snapHalf(cx - p.rect[0]), snapHalf(cz - p.rect[1])] } : null;
}

// ── wall marks (land-interface annotations) ─────────────────────────────────

/** True if wall pair `w` marks the (unordered) piece pair `a`/`b`. */
export function wallMatches(w, a, b) { return (w.a === a && w.b === b) || (w.a === b && w.b === a); }

/**
 * Toggle a wall mark on the (unordered) piece pair `a`/`b` in the document's `walls` list; returns true when
 * the mark was added, false when an existing mark was removed. Mutates `doc.walls` in place.
 */
export function toggleWall(doc, a, b) {
  if (!doc.walls) doc.walls = [];
  const i = doc.walls.findIndex(w => wallMatches(w, a, b));
  if (i >= 0) { doc.walls.splice(i, 1); return false; }
  doc.walls.push({ a, b });
  return true;
}

/** Distance (blocks) from point `(px, pz)` to the segment `(x1,z1)-(x2,z2)`. */
function pointSegDist(px, pz, x1, z1, x2, z2) {
  const dx = x2 - x1, dz = z2 - z1;
  const len2 = dx * dx + dz * dz;
  let t = len2 === 0 ? 0 : ((px - x1) * dx + (pz - z1) * dz) / len2;
  t = Math.max(0, Math.min(1, t));
  const cx = x1 + t * dx, cz = z1 + t * dz;
  return Math.hypot(px - cx, pz - cz);
}

/**
 * The land-interface segment nearest a world/block point, within `maxDist` blocks, or null. Any land interface
 * — full-width "land" or a "narrow" seam — can carry a wall (a wall across a narrow step is legal), so those
 * are eligible; corner point contacts (degenerate segments) are skipped. The segments come from the
 * /api/plan/inspect feed (each already resolved to block coordinates, carrying its `a`/`b` pair).
 */
export function nearestInterface(interfaces, wx, wz, maxDist) {
  let best = null, bestD = maxDist == null ? Infinity : maxDist;
  for (const it of interfaces || []) {
    if (it.kind !== "land" && it.kind !== "narrow") continue;
    if (it.x1 === it.x2 && it.z1 === it.z2) continue;   // degenerate (corner point)
    const d = pointSegDist(wx, wz, it.x1, it.z1, it.x2, it.z2);
    if (d <= bestD) { bestD = d; best = it; }
  }
  return best;
}

/** An id unique among `existing`, derived from `base` with a numeric suffix when needed. */
export function uniqueId(existing, base) {
  const set = new Set(existing);
  if (!set.has(base)) return base;
  for (let i = 2; ; i++) { const c = `${base}-${i}`; if (!set.has(c)) return c; }
}

// ── content bounds + mirror ghost ───────────────────────────────────────────

/** Block AABB enclosing every piece, zone and marker cell — null for an empty document. */
export function contentBounds(doc) {
  const cell = doc.globals.cell;
  let b = null;
  const add = (bb) => { b = b ? { min_x: Math.min(b.min_x, bb.min_x), min_z: Math.min(b.min_z, bb.min_z), max_x: Math.max(b.max_x, bb.max_x), max_z: Math.max(b.max_z, bb.max_z) } : { ...bb }; };
  for (const p of doc.pieces) add(rectCellsToBlocks(p.rect, cell));
  for (const z of doc.zones) add(rectCellsToBlocks(z.rect, cell));
  for (const m of allMarkers(doc)) { const c = markerCell(doc, m.marker); if (c) add(rectCellsToBlocks([c[0], c[1], 1, 1], cell)); }
  return b;
}

/**
 * Block AABB enclosing the authored content AND its symmetry ghost images — what fit-to-view and the
 * grid must span so the mirrored half of the board is never cut off. Null for an empty document.
 */
export function viewBounds(doc) {
  let b = contentBounds(doc);
  if (!b) return null;
  const add = (bb) => { b = { min_x: Math.min(b.min_x, bb.min_x), min_z: Math.min(b.min_z, bb.min_z), max_x: Math.max(b.max_x, bb.max_x), max_z: Math.max(b.max_z, bb.max_z) }; };
  for (const img of pieceMirrorImages(doc)) add(img.bounds);
  for (const img of zoneMirrorImages(doc)) add(img.bounds);
  for (const m of markerMirrorImages(doc)) add({ min_x: m.x, min_z: m.z, max_x: m.x, max_z: m.z });
  return b;
}

/** Flatten the placements into `{ kind, index, marker }` records (spawn/wool/iron), for iteration. */
export function allMarkers(doc) {
  const out = [];
  doc.placements.spawns.forEach((m, i) => out.push({ kind: "spawn", index: i, marker: m }));
  doc.placements.wools.forEach((m, i) => out.push({ kind: "wool", index: i, marker: m }));
  doc.placements.iron.forEach((m, i) => out.push({ kind: "iron", index: i, marker: m }));
  return out;
}

const markerList = (doc, kind) => kind === "spawn" ? doc.placements.spawns : kind === "wool" ? doc.placements.wools : doc.placements.iron;

/** The marker record for a `{ kind, index }` reference, or null. */
export function markerAt(doc, kind, index) { return markerList(doc, kind)[index] || null; }

/**
 * The symmetry mirror images of every mirroring piece — one `{ role, surface, bounds }` per orbit image
 * (block AABBs), for the dimmed non-editable ghost. Uses the shared symmetry helpers about the origin
 * (cells are relative to the symmetry centre, so the block centre is `0,0`).
 */
export function pieceMirrorImages(doc) {
  const { cell, symmetry } = doc.globals;
  const out = [];
  for (const p of doc.pieces) {
    if (p.mirrors === false) continue;
    const b = rectCellsToBlocks(p.rect, cell);
    for (const axis of orbitAxes(symmetry)) out.push({ role: p.role, surface: p.surface ?? doc.globals.surface, bounds: applySymmetryToBounds(b, axis, 0, 0) });
  }
  return out;
}

/**
 * The symmetry mirror images of every zone — one `{ id, bounds, holes }` per orbit image (block AABBs, holes
 * transformed alongside their zone), for the dimmed non-editable ghost. Zones always mirror (no opt-out).
 */
export function zoneMirrorImages(doc) {
  const { cell, symmetry } = doc.globals;
  const out = [];
  for (const z of doc.zones) {
    const b = rectCellsToBlocks(z.rect, cell);
    for (const axis of orbitAxes(symmetry)) {
      const holes = (z.holes || []).map(h => applySymmetryToBounds(rectCellsToBlocks(h, cell), axis, 0, 0));
      out.push({ id: z.id, bounds: applySymmetryToBounds(b, axis, 0, 0), holes });
    }
  }
  return out;
}

/** The mirror-image centre points (block coords) of every marker on a mirroring piece — for the ghost. */
export function markerMirrorImages(doc) {
  const { cell, symmetry } = doc.globals;
  const out = [];
  for (const { kind, marker } of allMarkers(doc)) {
    const p = pieceById(doc, marker.piece);
    if (!p || p.mirrors === false) continue;
    const c = markerCell(doc, marker);
    const cx = c[0] * cell, cz = c[1] * cell;
    for (const axis of orbitAxes(symmetry)) { const [x, z] = applySymmetry(cx, cz, axis, 0, 0); out.push({ kind, x, z }); }
  }
  return out;
}
