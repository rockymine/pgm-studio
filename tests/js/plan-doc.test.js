// Characterization tests for the plan-editor document model + geometry (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

import {
  emptyDoc, normalizeDoc, fromJson, toJson, uniqueId, nextFacing,
  rectCellsToBlocks, cellOfWorld, rectFromCells, rectContainsCell,
  pieceAtCell, zoneAtCell, markerCell, attachMarker, snapHalf, allMarkers,
  contentBounds, viewBounds, pieceMirrorImages, zoneMirrorImages, markerMirrorImages, ROLES, ROLE_COLORS,
  canonicalRole, toggleWall, nearestInterface,
  markerAtWorld, pickAtWorld, sameSelection, MARKER_HIT_CELLS,
  pieceSurface, surfaceRange, surfaceFraction, planIsoSolids, markerList, markerAt, MARKER_KINDS,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/plan/plan-doc.js";

const here = dirname(fileURLToPath(import.meta.url));
const seedPath = resolve(here, "../../tools/seeds/base-2wool.plan.json");

// ── coordinate + rect helpers ───────────────────────────────────────────────
test("rectCellsToBlocks scales a cell rect by the cell size", () => {
  assert.deepEqual(rectCellsToBlocks([1, 5, 2, 6], 5), { min_x: 5, min_z: 25, max_x: 15, max_z: 55 });
});

test("cellOfWorld floors a block point to its cell (incl. negatives)", () => {
  assert.deepEqual(cellOfWorld(12, 27, 5), [2, 5]);
  assert.deepEqual(cellOfWorld(-1, -6, 5), [-1, -2]);
});

test("rectFromCells covers both corner cells inclusively, ≥ 1×1", () => {
  assert.deepEqual(rectFromCells(2, 3, 4, 3), [2, 3, 3, 1]);
  assert.deepEqual(rectFromCells(4, 5, 2, 1), [2, 1, 3, 5]);   // dragged up-left
  assert.deepEqual(rectFromCells(0, 0, 0, 0), [0, 0, 1, 1]);
});

test("rectContainsCell is half-open on the far edge", () => {
  const r = [1, 1, 2, 2];   // cells x∈{1,2}, z∈{1,2}
  assert.equal(rectContainsCell(r, 1, 1), true);
  assert.equal(rectContainsCell(r, 2, 2), true);
  assert.equal(rectContainsCell(r, 3, 1), false);
});

// ── hit-testing ─────────────────────────────────────────────────────────────
test("pieceAtCell / zoneAtCell return the topmost containing item", () => {
  const doc = normalizeDoc({
    plan: 1,
    pieces: [{ id: "a", role: "lane", rect: [0, 0, 4, 4] }, { id: "b", role: "hub", rect: [1, 1, 2, 2] }],
    zones: [{ id: "z", rect: [0, 0, 2, 2] }],
  });
  assert.equal(pieceAtCell(doc, 1, 1).id, "b");   // b drawn last → on top
  assert.equal(pieceAtCell(doc, 0, 0).id, "a");
  assert.equal(pieceAtCell(doc, 9, 9), null);
  assert.equal(zoneAtCell(doc, 0, 0).id, "z");
});

test("markerCell / attachMarker resolve piece-relative offsets", () => {
  const doc = normalizeDoc({
    plan: 1,
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
    placements: { spawns: [{ piece: "bar", at: [1, 2], facing: "front" }] },
  });
  assert.deepEqual(markerCell(doc, doc.placements.spawns[0]), [2, 7]);
  // Dropping a marker at absolute cell (2,7) re-derives the same offset on the piece under it.
  assert.deepEqual(attachMarker(doc, 2, 7), { piece: "bar", at: [1, 2] });
  assert.equal(attachMarker(doc, 40, 40), null);   // no piece under → cannot attach
});

test("attachMarker snaps a fractional drop to the half-cell lattice", () => {
  const doc = normalizeDoc({
    plan: 1,
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
  });
  assert.equal(snapHalf(1.4), 1.5);
  assert.equal(snapHalf(1.1), 1);
  // dropping at fractional cell (2.4, 6.6) on piece origin (1,5) → offset (1.4,1.6) snaps to (1.5,1.5)
  assert.deepEqual(attachMarker(doc, 2.4, 6.6), { piece: "bar", at: [1.5, 1.5] });
  // an integer drop still yields an integer offset (back-compat)
  assert.deepEqual(attachMarker(doc, 2, 7), { piece: "bar", at: [1, 2] });
});

test("attachMarker snaps a 2×2-room click to the nearest half-cell lattice point (no per-cell bias)", () => {
  const doc = normalizeDoc({
    plan: 1,
    pieces: [{ id: "room", role: "piece", rect: [3, 3, 2, 2] }],   // 2×2-cell room, cells x∈{3,4}, z∈{3,4}
  });
  // a click at the room's exact centre (absolute cell (4,4)) → the room-centre lattice point [1,1]
  assert.deepEqual(attachMarker(doc, 4, 4), { piece: "room", at: [1, 1] });
  // a click inside the first cell's interior → the cell-centre lattice point [0.5,0.5]
  assert.deepEqual(attachMarker(doc, 3.5, 3.5), { piece: "room", at: [0.5, 0.5] });
  // render position matches the compiler formula piece.min + at·cell — the room-centre marker sits
  // on the shared cell corner (block (20,20) at cell 5), not offset into a cell.
  const centred = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "room", role: "piece", rect: [3, 3, 2, 2] }],
    placements: { wools: [{ piece: "room", at: [1, 1] }] },
  });
  assert.deepEqual(markerCell(centred, centred.placements.wools[0]), [4, 4]);
  assert.deepEqual(markerAtWorld(centred, 20, 20), { kind: "marker", markerKind: "wool", index: 0 });
});

test("uniqueId suffixes on collision", () => {
  assert.equal(uniqueId(["lane", "hub"], "mid"), "mid");
  assert.equal(uniqueId(["lane", "lane-2"], "lane"), "lane-3");
});

test("nextFacing cycles front → right → back → left → front", () => {
  assert.equal(nextFacing("front"), "right");
  assert.equal(nextFacing("right"), "back");
  assert.equal(nextFacing("left"), "front");
});

// ── pick priority (markers paint above pieces) ────────────────────────────────
test("markerAtWorld picks a marker within its radius; pickAtWorld prefers it over the piece under it", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }],
    placements: { spawns: [{ piece: "p", at: [1, 1], facing: "front" }] },
  });
  // marker at cell (1,1) → block point (5, 5) (piece.min + at·cell, no half-cell offset)
  assert.deepEqual(markerAtWorld(doc, 5, 5), { kind: "marker", markerKind: "spawn", index: 0 });
  // a click on the marker selects it even though a piece covers that cell (paint order: markers on top)
  assert.deepEqual(pickAtWorld(doc, 5, 5), { kind: "marker", markerKind: "spawn", index: 0 });
  // a click on the piece but clear of every marker radius selects the piece
  assert.deepEqual(pickAtWorld(doc, 18, 18), { kind: "piece", id: "p" });
  // just past the pick radius → no marker
  const r = MARKER_HIT_CELLS * 5;
  assert.equal(markerAtWorld(doc, 5 + r + 0.01, 5), null);
});

test("markerAtWorld breaks ties to the later-painted (topmost) marker", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }],
    placements: { spawns: [{ piece: "p", at: [1, 1], facing: "front" }], wools: [{ piece: "p", at: [1, 1] }] },
  });
  // both markers share the same cell; allMarkers paints spawns before wools, so the wool wins the tie
  assert.deepEqual(markerAtWorld(doc, 5, 5), { kind: "marker", markerKind: "wool", index: 0 });
});

test("pickAtWorld falls to a zone only when no marker or piece is hit", () => {
  const doc = normalizeDoc({ plan: 1, globals: { cell: 5, symmetry: "rot_180" }, zones: [{ id: "z", rect: [0, 0, 2, 2] }] });
  assert.deepEqual(pickAtWorld(doc, 2, 2), { kind: "zone", id: "z" });
  assert.equal(pickAtWorld(doc, 99, 99), null);
});

test("sameSelection compares piece/zone ids and marker kind+index", () => {
  assert.equal(sameSelection({ kind: "piece", id: "a" }, { kind: "piece", id: "a" }), true);
  assert.equal(sameSelection({ kind: "piece", id: "a" }, { kind: "piece", id: "b" }), false);
  assert.equal(sameSelection({ kind: "marker", markerKind: "spawn", index: 2 }, { kind: "marker", markerKind: "spawn", index: 2 }), true);
  assert.equal(sameSelection({ kind: "marker", markerKind: "spawn", index: 2 }, { kind: "marker", markerKind: "wool", index: 2 }), false);
  assert.equal(sameSelection(null, { kind: "piece", id: "a" }), false);
  assert.equal(sameSelection({ kind: "piece", id: "a" }, { kind: "zone", id: "a" }), false);
});

// ── height-map ────────────────────────────────────────────────────────────────
test("surfaceRange / pieceSurface resolve inherited surfaces across pieces", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "a", role: "piece", rect: [0, 0, 1, 1] }, { id: "b", role: "piece", rect: [1, 0, 1, 1], surface: 15 }],
  });
  assert.equal(pieceSurface(doc, doc.pieces[0]), 9);    // inherited from globals
  assert.equal(pieceSurface(doc, doc.pieces[1]), 15);
  assert.deepEqual(surfaceRange(doc), { min: 9, max: 15 });
  assert.equal(surfaceRange(emptyDoc()), null);
});

// ── iso preview solids (G27) ───────────────────────────────────────────────────
test("planIsoSolids extrudes each generating piece from 0 to its surface, plus a mirror per orbit axis", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "a", role: "piece", rect: [1, 0, 2, 1], surface: 13 }],
  });
  const solids = planIsoSolids(doc);
  assert.equal(solids.length, 2);   // the piece + its rot_180 mirror

  const [self, mirror] = solids;
  assert.equal(self.mirror, false);
  assert.equal(self.floor, 0);
  assert.equal(self.top, 13);       // its own surface, not the global base
  // [1,0,2,1] cells × 5 → block AABB x∈[5,15], z∈[0,5], as a CCW-ish corner ring.
  assert.deepEqual(self.exterior, [[5, 0], [15, 0], [15, 5], [5, 5]]);

  assert.equal(mirror.mirror, true);
  assert.equal(mirror.top, 13);
  // rot_180 about the origin negates both axes → x∈[-15,-5], z∈[-5,0].
  const xs = mirror.exterior.map(([x]) => x), zs = mirror.exterior.map(([, z]) => z);
  assert.deepEqual([Math.min(...xs), Math.max(...xs)], [-15, -5]);
  assert.deepEqual([Math.min(...zs), Math.max(...zs)], [-5, 0]);
});

test("planIsoSolids inherits the global surface, skips annotations, and honours mirrors:false", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [
      { id: "base", role: "piece", rect: [0, 0, 1, 1] },                    // inherits surface 9
      { id: "solo", role: "wool-room", rect: [2, 0, 1, 1], mirrors: false }, // no mirror copy
      { id: "buf", role: "buffer", rect: [4, 0, 1, 1] },                    // annotation → no terrain
    ],
  });
  const solids = planIsoSolids(doc);
  // base (self + mirror) + solo (self only) = 3; the buffer produces nothing.
  assert.equal(solids.length, 3);
  assert.equal(solids.filter(s => s.mirror).length, 1);
  assert.equal(solids.find(s => !s.mirror).top, 9);   // inherited global surface
});

test("planIsoSolids under no symmetry emits one solid per piece (no mirrors)", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "none", surface: 9 },
    pieces: [{ id: "a", role: "piece", rect: [0, 0, 1, 1] }],
  });
  const solids = planIsoSolids(doc);
  assert.equal(solids.length, 1);
  assert.equal(solids[0].mirror, false);
});

test("surfaceFraction maps a surface onto 0..1; a flat plan pins to the top of the ramp", () => {
  assert.equal(surfaceFraction(9, { min: 9, max: 15 }), 0);
  assert.equal(surfaceFraction(15, { min: 9, max: 15 }), 1);
  assert.equal(surfaceFraction(12, { min: 9, max: 15 }), 0.5);
  assert.equal(surfaceFraction(9, { min: 9, max: 9 }), 1);   // flat → highest (lightest) tint
  assert.equal(surfaceFraction(9, null), 1);
});

// ── mirror ghost ────────────────────────────────────────────────────────────
test("pieceMirrorImages fans one image per orbit axis, honouring mirrors:false", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "a", role: "lane", rect: [1, 1, 2, 2] }, { id: "b", role: "mid", rect: [3, 3, 1, 1], mirrors: false }],
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                   // only the mirroring piece, one rot_180 image
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -15, max_x: -5, max_z: -5 });

  const doc4 = normalizeDoc({ plan: 1, globals: { cell: 5, symmetry: "rot_90" }, pieces: [{ id: "a", role: "lane", rect: [1, 1, 2, 2] }] });
  assert.equal(pieceMirrorImages(doc4).length, 3);   // rot_90 fans three quarter-turn images
});

test("zoneMirrorImages fans zones (and holes) about the origin per orbit axis", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    zones: [{ id: "z", rect: [1, 1, 2, 2], holes: [[1, 1, 1, 1]] }],
  });
  const imgs = zoneMirrorImages(doc);
  assert.equal(imgs.length, 1);                     // rot_180 → one image
  assert.equal(imgs[0].id, "z");
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -15, max_x: -5, max_z: -5 });
  assert.deepEqual(imgs[0].holes, [{ min_x: -10, min_z: -10, max_x: -5, max_z: -5 }]);

  const doc4 = normalizeDoc({ plan: 1, globals: { cell: 5, symmetry: "rot_90" }, zones: [{ id: "z", rect: [1, 1, 1, 1], holes: [] }] });
  assert.equal(zoneMirrorImages(doc4).length, 3);   // rot_90 fans three quarter-turn images
});

test("viewBounds includes zone mirror ghosts (never cut off)", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    zones: [{ id: "z", rect: [1, 1, 2, 2], holes: [] }],
  });
  // content (5,5)-(15,15) unioned with its ghost (-15,-15)-(-5,-5)
  assert.deepEqual(viewBounds(doc), { min_x: -15, min_z: -15, max_x: 15, max_z: 15 });
});

test("markerMirrorImages mirrors marker centres about the origin", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "bar", role: "lane", rect: [1, 1, 1, 1] }],
    placements: { spawns: [{ piece: "bar", at: [0, 0], facing: "front" }] },
  });
  const [img] = markerMirrorImages(doc);
  assert.deepEqual([img.x, img.z], [-5, -5]);   // block point (5,5) (piece.min + at·cell) rotated 180° about origin
});


test("pieceMirrorImages ghosts a piece with an inherited (unset) surface", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],   // no explicit surface
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                    // the inherited surface never drops the ghost
  assert.equal(imgs[0].surface, 9);                // resolved from globals
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -55, max_x: -5, max_z: -25 });
});

test("viewBounds spans content plus its ghost images (never cut off)", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
    placements: { spawns: [{ piece: "bar", at: [0, 0], facing: "front" }] },
  });
  assert.deepEqual(contentBounds(doc), { min_x: 5, min_z: 25, max_x: 15, max_z: 55 });
  assert.deepEqual(viewBounds(doc), { min_x: -15, min_z: -55, max_x: 15, max_z: 55 });
  assert.equal(viewBounds(emptyDoc()), null);

  const doc4 = normalizeDoc({ plan: 1, globals: { cell: 5, symmetry: "rot_90" }, pieces: [{ id: "a", role: "lane", rect: [4, 4, 1, 1] }] });
  // Three quarter-turn images fan the single cell into all four quadrants.
  assert.deepEqual(viewBounds(doc4), { min_x: -25, min_z: -25, max_x: 25, max_z: 25 });
});

test("contentBounds encloses pieces, zones and markers", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "a", role: "lane", rect: [0, 0, 2, 2] }],
    zones: [{ id: "z", rect: [-1, -1, 1, 1] }],
  });
  assert.deepEqual(contentBounds(doc), { min_x: -5, min_z: -5, max_x: 10, max_z: 10 });
  assert.equal(contentBounds(emptyDoc()), null);
});

test("allMarkers flattens spawns/wools/iron with kind + index", () => {
  const doc = normalizeDoc({ plan: 1, placements: { spawns: [{ piece: "a", at: [0, 0] }], wools: [{ piece: "a", at: [1, 0] }], iron: [] } });
  assert.deepEqual(allMarkers(doc).map(m => [m.kind, m.index]), [["spawn", 0], ["wool", 0]]);
});

test("ROLES palette order is stable and includes the buffer + connector annotations", () => {
  assert.deepEqual(ROLES, ["piece", "wool-room", "spawn", "buffer", "connector"]);
  assert.ok(ROLES.includes("buffer"));
  assert.ok(ROLES.includes("connector"));
  assert.equal(ROLE_COLORS.buffer, "#f2792b");
  assert.equal(ROLE_COLORS.connector, "#2dd4bf");
});

// ── schema v2: anonymous roles + wall marks + the buffer annotation ──────────
test("canonicalRole folds legacy/unknown roles to piece, keeps intent + annotation roles", () => {
  assert.equal(canonicalRole("lane"), "piece");
  assert.equal(canonicalRole("hub"), "piece");
  assert.equal(canonicalRole("mid"), "piece");
  assert.equal(canonicalRole(undefined), "piece");
  assert.equal(canonicalRole("nonsense"), "piece");
  assert.equal(canonicalRole("wool-room"), "wool-room");
  assert.equal(canonicalRole("spawn"), "spawn");
  assert.equal(canonicalRole("buffer"), "buffer");        // annotation role preserved, never folded
  assert.equal(canonicalRole("connector"), "connector");  // the second annotation role, likewise preserved
});

test("normalizeDoc maps legacy piece roles on load", () => {
  const doc = normalizeDoc({
    plan: 1,
    pieces: [{ id: "a", role: "lane", rect: [0, 0, 2, 2] }, { id: "b", role: "wool-room", rect: [2, 0, 2, 2] }],
  });
  assert.equal(doc.pieces[0].role, "piece");
  assert.equal(doc.pieces[1].role, "wool-room");
  assert.deepEqual(doc.walls, []);   // walls default to an empty list
});

test("normalizeDoc and toJson round-trip a buffer piece verbatim", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "buffer", role: "buffer", rect: [0, 0, 2, 2] },
             { id: "buffer-2", role: "buffer", rect: [3, 0, 2, 2], mirrors: false }],
  });
  assert.equal(doc.pieces[0].role, "buffer");   // preserved, not folded to piece
  assert.equal(doc.pieces[1].mirrors, false);
  const back = fromJson(toJson(doc));
  assert.deepEqual(back, doc);                   // stable under re-serialisation
});

test("pieceMirrorImages fans a buffer with mirrors unset and skips one with mirrors:false", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "buffer", role: "buffer", rect: [1, 1, 2, 2] },
             { id: "buffer-2", role: "buffer", rect: [3, 3, 1, 1], mirrors: false }],
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                  // the mirroring buffer fans one rot_180 image; the pinned one does not
  assert.equal(imgs[0].role, "buffer");
});

test("toggleWall adds then removes a wall mark, order-insensitive", () => {
  const doc = normalizeDoc({ plan: 1 });
  assert.equal(toggleWall(doc, "a", "b"), true);
  assert.deepEqual(doc.walls, [{ a: "a", b: "b" }]);
  assert.equal(toggleWall(doc, "b", "a"), false);   // same pair, reversed → removes it
  assert.deepEqual(doc.walls, []);
});

test("nearestInterface picks the closest land or narrow seam within range, skipping corner points", () => {
  const interfaces = [
    { a: "a", b: "b", kind: "land", x1: 0, z1: 0, x2: 0, z2: 20 },       // vertical seam at x=0
    { a: "c", b: "d", kind: "narrow", x1: 6, z1: 0, x2: 6, z2: 5 },      // narrow seam — a wall across it is legal
    { a: "e", b: "f", kind: "corner", x1: 30, z1: 30, x2: 30, z2: 30 },  // bare corner point — never wall-capable
  ];
  assert.equal(nearestInterface(interfaces, 1, 10, 5).a, "a");          // 1 block from the x=0 land seam
  assert.equal(nearestInterface(interfaces, 6, 2, 5).a, "c");           // on a narrow seam → now eligible, picked
  assert.equal(nearestInterface(interfaces, 30, 30, 5), null);          // only a corner point nearby → skipped
});

// ── round-trip a real seed plan ──────────────────────────────────────────────
test("fromJson → toJson round-trips a seed plan's data", () => {
  const text = readFileSync(seedPath, "utf8");
  const doc = fromJson(text);
  const back = fromJson(toJson(doc));
  assert.deepEqual(back, doc);                                   // stable under re-serialisation
  // Core data survives verbatim.
  assert.equal(doc.globals.symmetry, "rot_180");
  assert.equal(doc.pieces.length, 8);
  assert.deepEqual(doc.pieces.find(p => p.id === "piece-2").rect, [-3, 4, 2, 7]);
  assert.equal(doc.pieces.find(p => p.id === "wool").role, "wool-room");
  assert.deepEqual(doc.placements.spawns[0], { piece: "spawn", at: [1, 1], facing: "front" });
  assert.equal(doc.placements.wools.length, 2);
});

// The kind→list dispatch used to be a ternary chain whose final branch was iron, so any kind that was not
// "spawn" or "wool" silently resolved to the iron list — a new kind would place, select and delete the
// wrong markers rather than fail. It must be a lookup: every kind its own list, an unknown kind nothing.
test("markerList maps each kind to its own list, and an unknown kind to nothing", () => {
  const doc = emptyDoc();
  for (const kind of MARKER_KINDS) assert.ok(markerList(doc, kind), `${kind} has a list`);

  const lists = MARKER_KINDS.map(k => markerList(doc, k));
  assert.equal(new Set(lists).size, MARKER_KINDS.length, "no two kinds share a list");

  assert.equal(markerList(doc, "destroyable"), doc.placements.destroyables);
  assert.equal(markerList(doc, "iron"), doc.placements.iron);

  assert.equal(markerList(doc, "nonsense"), null);   // unknown must be nothing, not the last branch
  assert.equal(markerAt(doc, "nonsense", 0), null);  // and reading through it must not throw
});

test("a destroyable placement round-trips, keeping only its authored fields", () => {
  const doc = normalizeDoc({
    plan: 1,
    placements: {
      destroyables: [
        { piece: "bar-w", at: [2, 3] },
        { piece: "bar-e", at: [1, 1], style: "cube-4", materials: "gold block", float: 7, name: "The Vault" },
      ],
    },
  });
  // A bare marker stays bare: the compiler owns the defaults, so the plan must not bake them in.
  assert.deepEqual(doc.placements.destroyables[0], { piece: "bar-w", at: [2, 3] });
  assert.deepEqual(doc.placements.destroyables[1], {
    piece: "bar-e", at: [1, 1], style: "cube-4", materials: "gold block", float: 7, name: "The Vault",
  });
  assert.deepEqual(JSON.parse(toJson(doc)).placements.destroyables, doc.placements.destroyables);
});

test("allMarkers includes destroyables, tagged with their kind", () => {
  const doc = emptyDoc();
  doc.placements.destroyables.push({ piece: "bar-w", at: [1, 1] });
  const found = allMarkers(doc).filter(m => m.kind === "destroyable");
  assert.equal(found.length, 1);
  assert.equal(found[0].index, 0);
});

test("a core placement round-trips, keeping only its authored knobs", () => {
  const doc = normalizeDoc({
    plan: 1,
    placements: {
      cores: [
        { piece: "mid", at: [2, 2] },
        { piece: "mid", at: [1, 1], size: 7, height: 7, shell: 2, openTop: true, float: 3, leak: 4, name: "The Heart" },
      ],
    },
  });
  // A bare marker stays bare — the compiler owns the DC1/DC2 defaults, so the plan must not bake them in.
  assert.deepEqual(doc.placements.cores[0], { piece: "mid", at: [2, 2] });
  assert.deepEqual(doc.placements.cores[1], {
    piece: "mid", at: [1, 1], size: 7, height: 7, shell: 2, float: 3, leak: 4, openTop: true, name: "The Heart",
  });
  assert.deepEqual(JSON.parse(toJson(doc)).placements.cores, doc.placements.cores);
});

test("openTop:false and float:0 survive normalize (falsy but authored)", () => {
  // A naive `if (c.openTop)` would drop an explicit false, and `if (c.float)` an explicit 0 — which is the
  // 27% of cores that rest directly on the floor, the case where float matters most.
  const doc = normalizeDoc({ plan: 1, placements: { cores: [{ piece: "mid", at: [0, 0], openTop: false, float: 0, leak: 5 }] } });
  assert.equal(doc.placements.cores[0].openTop, false);
  assert.equal(doc.placements.cores[0].float, 0);
  assert.equal(doc.placements.cores[0].leak, 5);
});
