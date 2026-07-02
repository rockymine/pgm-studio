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

// Piece roles — the left-toolbar palette, in display order. Colours are theme-independent so a piece
// reads the same on the dark canvas in either theme; the fill is tinted lighter for a higher surface.
export const ROLES = ["lane", "hub", "wool-room", "mid"];
export const ROLE_COLORS = { lane: "#4a90d9", hub: "#9b6bd0", "wool-room": "#3fae74", mid: "#d9a441" };
export const ROLE_LABELS = { lane: "Lane", hub: "Hub", "wool-room": "Wool room", mid: "Mid" };

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
      const o = { id: p.id ?? "", role: p.role ?? "lane", rect: [...(p.rect || [0, 0, 1, 1])] };
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

/** The absolute cell of a marker (its piece's origin + the piece-relative offset), or null if orphaned. */
export function markerCell(doc, marker) {
  const p = pieceById(doc, marker.piece);
  return p ? [p.rect[0] + marker.at[0], p.rect[1] + marker.at[1]] : null;
}

/**
 * Attach a marker dropped at absolute cell `(cx, cz)`: the piece under it + the piece-relative offset,
 * or null when no piece sits under the cell (markers must ride a piece).
 */
export function attachMarker(doc, cx, cz) {
  const p = pieceAtCell(doc, cx, cz);
  return p ? { piece: p.id, at: [cx - p.rect[0], cz - p.rect[1]] } : null;
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

/** The mirror-image centre points (block coords) of every marker on a mirroring piece — for the ghost. */
export function markerMirrorImages(doc) {
  const { cell, symmetry } = doc.globals;
  const out = [];
  for (const { kind, marker } of allMarkers(doc)) {
    const p = pieceById(doc, marker.piece);
    if (!p || p.mirrors === false) continue;
    const c = markerCell(doc, marker);
    const cx = (c[0] + 0.5) * cell, cz = (c[1] + 0.5) * cell;
    for (const axis of orbitAxes(symmetry)) { const [x, z] = applySymmetry(cx, cz, axis, 0, 0); out.push({ kind, x, z }); }
  }
  return out;
}
