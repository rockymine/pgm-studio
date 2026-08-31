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
  pieceSurface, surfaceRange, surfaceFraction, markerList, markerAt, MARKER_KINDS,
  boxMembers, boxAtCell, boxOfPiece, boxMirrorImages, rectContainsRect,
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
    plan: 2,
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
    plan: 2,
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
    placements: { spawns: [{ piece: "bar", at: [5, 10], facing: "front" }] },
  });
  assert.deepEqual(markerCell(doc, doc.placements.spawns[0]), [2, 7]);
  // Dropping a marker at absolute cell (2,7) re-derives the same offset on the piece under it.
  assert.deepEqual(attachMarker(doc, 2, 7), { piece: "bar", at: [5, 10] });
  assert.equal(attachMarker(doc, 40, 40), null);   // no piece under → cannot attach
});

test("attachMarker snaps a fractional drop to the half-block lattice", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
  });
  assert.equal(snapHalf(1.4), 1.5);
  assert.equal(snapHalf(1.1), 1);
  // dropping at fractional cell (2.4, 6.6) on piece origin (1,5) → offset (7, 8) blocks, already on the lattice
  assert.deepEqual(attachMarker(doc, 2.4, 6.6), { piece: "bar", at: [7, 8] });
  // a cell-corner drop lands on a whole block offset
  assert.deepEqual(attachMarker(doc, 2, 7), { piece: "bar", at: [5, 10] });
});

test("attachMarker snaps a 2×2-room click to the nearest half-block lattice point", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [{ id: "room", role: "piece", rect: [3, 3, 2, 2] }],   // 2×2-cell room, cells x∈{3,4}, z∈{3,4}
  });
  // a click at the room's exact centre (absolute cell (4,4)) → block offset [5,5], a grid line on both axes
  assert.deepEqual(attachMarker(doc, 4, 4), { piece: "room", at: [5, 5] });
  // a click at the first cell's own centre → block offset [2.5,2.5], a block centre on both
  assert.deepEqual(attachMarker(doc, 3.5, 3.5), { piece: "room", at: [2.5, 2.5] });
  // render position matches the compiler formula piece.min + at — the room-centre marker sits
  // on the shared cell corner (block (20,20) at cell 5), not offset into a cell.
  const centred = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "room", role: "piece", rect: [3, 3, 2, 2] }],
    placements: { wools: [{ piece: "room", at: [5, 5] }] },
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
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }],
    placements: { spawns: [{ piece: "p", at: [5, 5], facing: "front" }] },
  });
  // an offset of 5 blocks on a piece at the origin → block point (5, 5)
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
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }],
    placements: { spawns: [{ piece: "p", at: [5, 5], facing: "front" }], wools: [{ piece: "p", at: [5, 5] }] },
  });
  // both markers share the same cell; allMarkers paints spawns before wools, so the wool wins the tie
  assert.deepEqual(markerAtWorld(doc, 5, 5), { kind: "marker", markerKind: "wool", index: 0 });
});

test("pickAtWorld falls to a zone only when no marker or piece is hit", () => {
  const doc = normalizeDoc({ plan: 2, globals: { cell: 5, symmetry: "rot_180" }, zones: [{ id: "z", rect: [0, 0, 2, 2] }] });
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
    plan: 2, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "a", role: "piece", rect: [0, 0, 1, 1] }, { id: "b", role: "piece", rect: [1, 0, 1, 1], surface: 15 }],
  });
  assert.equal(pieceSurface(doc, doc.pieces[0]), 9);    // inherited from globals
  assert.equal(pieceSurface(doc, doc.pieces[1]), 15);
  assert.deepEqual(surfaceRange(doc), { min: 9, max: 15 });
  assert.equal(surfaceRange(emptyDoc()), null);
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
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "a", role: "lane", rect: [1, 1, 2, 2] }, { id: "b", role: "mid", rect: [3, 3, 1, 1], mirrors: false }],
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                   // only the mirroring piece, one rot_180 image
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -15, max_x: -5, max_z: -5 });

  const doc4 = normalizeDoc({ plan: 2, globals: { cell: 5, symmetry: "rot_90" }, pieces: [{ id: "a", role: "lane", rect: [1, 1, 2, 2] }] });
  assert.equal(pieceMirrorImages(doc4).length, 3);   // rot_90 fans three quarter-turn images
});

test("zoneMirrorImages fans zones (and holes) about the origin per orbit axis", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    zones: [{ id: "z", rect: [1, 1, 2, 2], holes: [[1, 1, 1, 1]] }],
  });
  const imgs = zoneMirrorImages(doc);
  assert.equal(imgs.length, 1);                     // rot_180 → one image
  assert.equal(imgs[0].id, "z");
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -15, max_x: -5, max_z: -5 });
  assert.deepEqual(imgs[0].holes, [{ min_x: -10, min_z: -10, max_x: -5, max_z: -5 }]);

  const doc4 = normalizeDoc({ plan: 2, globals: { cell: 5, symmetry: "rot_90" }, zones: [{ id: "z", rect: [1, 1, 1, 1], holes: [] }] });
  assert.equal(zoneMirrorImages(doc4).length, 3);   // rot_90 fans three quarter-turn images
});

test("viewBounds includes zone mirror ghosts (never cut off)", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    zones: [{ id: "z", rect: [1, 1, 2, 2], holes: [] }],
  });
  // content (5,5)-(15,15) unioned with its ghost (-15,-15)-(-5,-5)
  assert.deepEqual(viewBounds(doc), { min_x: -15, min_z: -15, max_x: 15, max_z: 15 });
});

test("markerMirrorImages mirrors marker centres about the origin", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "bar", role: "lane", rect: [1, 1, 1, 1] }],
    placements: { spawns: [{ piece: "bar", at: [0, 0], facing: "front" }] },
  });
  const [img] = markerMirrorImages(doc);
  assert.deepEqual([img.x, img.z], [-5, -5]);   // block point (5,5) (piece.min + at·cell) rotated 180° about origin
});


test("pieceMirrorImages ghosts a piece with an inherited (unset) surface", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],   // no explicit surface
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                    // the inherited surface never drops the ghost
  assert.equal(imgs[0].surface, 9);                // resolved from globals
  assert.deepEqual(imgs[0].bounds, { min_x: -15, min_z: -55, max_x: -5, max_z: -25 });
});

test("viewBounds spans content plus its ghost images (never cut off)", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180", surface: 9 },
    pieces: [{ id: "bar", role: "lane", rect: [1, 5, 2, 6] }],
    placements: { spawns: [{ piece: "bar", at: [0, 0], facing: "front" }] },
  });
  assert.deepEqual(contentBounds(doc), { min_x: 5, min_z: 25, max_x: 15, max_z: 55 });
  assert.deepEqual(viewBounds(doc), { min_x: -15, min_z: -55, max_x: 15, max_z: 55 });
  assert.equal(viewBounds(emptyDoc()), null);

  const doc4 = normalizeDoc({ plan: 2, globals: { cell: 5, symmetry: "rot_90" }, pieces: [{ id: "a", role: "lane", rect: [4, 4, 1, 1] }] });
  // Three quarter-turn images fan the single cell into all four quadrants.
  assert.deepEqual(viewBounds(doc4), { min_x: -25, min_z: -25, max_x: 25, max_z: 25 });
});

test("contentBounds encloses pieces, zones and markers", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "a", role: "lane", rect: [0, 0, 2, 2] }],
    zones: [{ id: "z", rect: [-1, -1, 1, 1] }],
  });
  assert.deepEqual(contentBounds(doc), { min_x: -5, min_z: -5, max_x: 10, max_z: 10 });
  assert.equal(contentBounds(emptyDoc()), null);
});

test("allMarkers flattens spawns/wools/iron with kind + index", () => {
  const doc = normalizeDoc({ plan: 2, placements: { spawns: [{ piece: "a", at: [0, 0] }], wools: [{ piece: "a", at: [1, 0] }], iron: [] } });
  assert.deepEqual(allMarkers(doc).map(m => [m.kind, m.index]), [["spawn", 0], ["wool", 0]]);
});

test("ROLES palette order is stable and includes the buffer annotation", () => {
  assert.deepEqual(ROLES, ["piece", "wool-room", "spawn", "buffer"]);
  assert.ok(ROLES.includes("buffer"));
  assert.equal(ROLE_COLORS.buffer, "#f2792b");
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
  assert.equal(canonicalRole("connector"), "piece");      // the retired annotation role folds like any other
});

test("normalizeDoc maps legacy piece roles on load", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [{ id: "a", role: "lane", rect: [0, 0, 2, 2] }, { id: "b", role: "wool-room", rect: [2, 0, 2, 2] }],
  });
  assert.equal(doc.pieces[0].role, "piece");
  assert.equal(doc.pieces[1].role, "wool-room");
  assert.deepEqual(doc.walls, []);   // walls default to an empty list
});

test("normalizeDoc and toJson round-trip a buffer piece verbatim", () => {
  const doc = normalizeDoc({
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
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
    plan: 2, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "buffer", role: "buffer", rect: [1, 1, 2, 2] },
             { id: "buffer-2", role: "buffer", rect: [3, 3, 1, 1], mirrors: false }],
  });
  const imgs = pieceMirrorImages(doc);
  assert.equal(imgs.length, 1);                  // the mirroring buffer fans one rot_180 image; the pinned one does not
  assert.equal(imgs[0].role, "buffer");
});

test("toggleWall raises a mark and takes it off again, order-insensitive", () => {
  // Which face the chests open on is derived, not authored, so a click decides only whether the wall stands.
  const doc = normalizeDoc({ plan: 2 });
  assert.equal(toggleWall(doc, "a", "b"), true);
  assert.deepEqual(doc.walls, [{ a: "a", b: "b" }]);         // no side: nothing here names a face
  assert.equal(toggleWall(doc, "b", "a"), false);            // same pair reversed → the same mark, removed
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
  assert.deepEqual(doc.placements.spawns[0], { id: "spawn-1", piece: "spawn", at: [5, 5], facing: "front" });
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
    plan: 2,
    placements: {
      destroyables: [
        { piece: "bar-w", at: [2, 3] },
        { piece: "bar-e", at: [1, 1], style: "cube-4", materials: "gold block", float: 7, name: "The Vault" },
      ],
    },
  });
  // An unvaried marker carries only its identity and its position: the compiler owns the structure defaults,
  // so the plan must not bake them in. The id is not one of those — it is what the marker IS.
  assert.deepEqual(doc.placements.destroyables[0], { id: "destroyable-1", piece: "bar-w", at: [2, 3] });
  assert.deepEqual(doc.placements.destroyables[1], {
    id: "destroyable-2", piece: "bar-e", at: [1, 1], style: "cube-4", materials: "gold block", float: 7, name: "The Vault",
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
    plan: 2,
    placements: {
      cores: [
        { piece: "mid", at: [2, 2] },
        { piece: "mid", at: [1, 1], lava: 5, lavaHeight: 4, openTop: true, float: 3, leak: 4, name: "The Heart" },
        // A casing stated as a size and a wall thickness is not a core the studio can build: the interior is
        // what an author picks and the obsidian is derived from it, so those words carry no meaning here.
        { piece: "mid", at: [3, 3], size: 7, height: 7, shell: 2 },
      ],
    },
  });
  // An unvaried marker carries only its identity and its position — the compiler owns the DC1/DC2 defaults.
  assert.deepEqual(doc.placements.cores[0], { id: "core-1", piece: "mid", at: [2, 2] });
  assert.deepEqual(doc.placements.cores[1], {
    id: "core-2", piece: "mid", at: [1, 1], lava: 5, lavaHeight: 4, float: 3, leak: 4, openTop: true, name: "The Heart",
  });
  assert.deepEqual(doc.placements.cores[2], { id: "core-3", piece: "mid", at: [3, 3] });
  assert.deepEqual(JSON.parse(toJson(doc)).placements.cores, doc.placements.cores);
});

test("openTop:false and float:0 survive normalize (falsy but authored)", () => {
  // A naive `if (c.openTop)` would drop an explicit false, and `if (c.float)` an explicit 0 — which is the
  // 27% of cores that rest directly on the floor, the case where float matters most.
  const doc = normalizeDoc({ plan: 2, placements: { cores: [{ piece: "mid", at: [0, 0], openTop: false, float: 0, leak: 5 }] } });
  assert.equal(doc.placements.cores[0].openTop, false);
  assert.equal(doc.placements.cores[0].float, 0);
  assert.equal(doc.placements.cores[0].leak, 5);
});

// ── box annotations (typed envelopes grouping pieces) ───────────────────────

test("normalizeDoc defaults boxes to [] and folds an unknown kind to mid", () => {
  assert.deepEqual(emptyDoc().boxes, []);
  const doc = normalizeDoc({ plan: 2, boxes: [{ id: "b", kind: "nonsense", rect: [0, 0, 2, 2] }] });
  assert.equal(doc.boxes[0].kind, "mid");
});

test("a box's members are kept only when named, so a containment box stays bare", () => {
  const doc = normalizeDoc({
    plan: 2,
    boxes: [
      { id: "a", kind: "wool", rect: [0, 0, 2, 2] },
      { id: "b", kind: "hub", rect: [0, 0, 2, 2], members: [] },
      { id: "c", kind: "hub", rect: [0, 0, 2, 2], members: ["p"] },
    ],
  });
  assert.deepEqual(doc.boxes[0], { id: "a", kind: "wool", rect: [0, 0, 2, 2] });
  assert.deepEqual(doc.boxes[1], { id: "b", kind: "hub", rect: [0, 0, 2, 2] });   // empty list → containment
  assert.deepEqual(doc.boxes[2].members, ["p"]);
  assert.deepEqual(JSON.parse(toJson(doc)).boxes, doc.boxes);
});

test("boxMembers takes the generating pieces wholly inside, never annotations", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [
      { id: "entry", role: "piece", rect: [0, 0, 2, 1] },
      { id: "room", role: "wool-room", rect: [2, 0, 1, 1] },
      { id: "far", role: "piece", rect: [8, 8, 1, 1] },
      { id: "gap", role: "buffer", rect: [0, 1, 3, 1] },
    ],
    boxes: [{ id: "wool-a", kind: "wool", rect: [0, 0, 3, 2] }],
  });
  assert.deepEqual(boxMembers(doc, doc.boxes[0]).map(p => p.id), ["entry", "room"]);
});

test("named members win over containment and ignore the rect", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [
      { id: "entry", role: "piece", rect: [0, 0, 2, 1] },
      { id: "far", role: "piece", rect: [8, 8, 1, 1] },
    ],
    boxes: [{ id: "b", kind: "hub", rect: [0, 0, 1, 1], members: ["far"] }],
  });
  assert.deepEqual(boxMembers(doc, doc.boxes[0]).map(p => p.id), ["far"]);
});

test("rectContainsRect counts touching edges as inside", () => {
  assert.equal(rectContainsRect([0, 0, 2, 2], [0, 0, 2, 2]), true);
  assert.equal(rectContainsRect([0, 0, 2, 2], [1, 1, 1, 1]), true);
  assert.equal(rectContainsRect([0, 0, 2, 2], [1, 1, 2, 1]), false);
});

test("boxAtCell takes the smallest containing box, whatever the draw order", () => {
  const doc = normalizeDoc({
    plan: 2,
    boxes: [{ id: "inner", kind: "wool", rect: [1, 1, 2, 2] }, { id: "outer", kind: "hub", rect: [0, 0, 4, 4] }],
  });
  assert.equal(boxAtCell(doc, 0, 0)?.id, "outer");   // only the outer box covers this cell
  assert.equal(boxAtCell(doc, 2, 2)?.id, "inner");   // the tightest group wins, though it was drawn first
  assert.equal(boxAtCell(doc, 9, 9), null);
});

test("boxOfPiece finds the box that groups a piece (what Escape pops out to)", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [{ id: "p", role: "piece", rect: [1, 1, 1, 1] }, { id: "loose", role: "piece", rect: [9, 9, 1, 1] }],
    boxes: [{ id: "b", kind: "hub", rect: [0, 0, 4, 4] }],
  });
  assert.equal(boxOfPiece(doc, "p")?.id, "b");
  assert.equal(boxOfPiece(doc, "loose"), null);
});

test("pickAtWorld picks the box over its pieces, and drill reaches past it", () => {
  const doc = normalizeDoc({
    plan: 2,
    pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }],
    boxes: [{ id: "b", kind: "hub", rect: [0, 0, 4, 4] }],
    placements: { spawns: [{ piece: "p", at: [0, 10], facing: "front" }] },
  });
  // Single-click: the marker still wins on its own tight radius; elsewhere the box (the group) does.
  assert.deepEqual(pickAtWorld(doc, 0, 10), { kind: "marker", markerKind: "spawn", index: 0 });
  assert.deepEqual(pickAtWorld(doc, 10, 10), { kind: "box", id: "b" });
  // Double-click drills past the box to the piece under the cursor.
  assert.deepEqual(pickAtWorld(doc, 10, 10, { drill: true }), { kind: "piece", id: "p" });
  assert.deepEqual(pickAtWorld(doc, 0, 10, { drill: true }), { kind: "marker", markerKind: "spawn", index: 0 });
});

test("an unboxed plan is unaffected by the group model", () => {
  const doc = normalizeDoc({ plan: 2, pieces: [{ id: "p", role: "piece", rect: [0, 0, 4, 4] }] });
  assert.deepEqual(pickAtWorld(doc, 10, 10), { kind: "piece", id: "p" });
});

test("boxes fan into the mirror ghost and widen the view bounds", () => {
  const doc = normalizeDoc({ plan: 2, globals: { symmetry: "rot_180" }, boxes: [{ id: "b", kind: "hub", rect: [1, 1, 2, 2] }] });
  const images = boxMirrorImages(doc);
  assert.equal(images.length, 1);
  assert.deepEqual(images[0].bounds, { min_x: -15, min_z: -15, max_x: -5, max_z: -5 });
  assert.deepEqual(contentBounds(doc), { min_x: 5, min_z: 5, max_x: 15, max_z: 15 });
  assert.deepEqual(viewBounds(doc), { min_x: -15, min_z: -15, max_x: 15, max_z: 15 });
});

test("the shifted-frontline exemplars carry their partition as typed boxes", () => {
  const doc = fromJson(readFileSync(resolve(here, "../../tools/seeds/shifted-u-frontline-attach-hole-hub.plan.json"), "utf8"));
  assert.deepEqual(doc.boxes.map(b => b.kind), ["hub", "frontline", "spawn", "wool", "wool"]);
  // The overlay `buffer` pieces the boxes replaced are gone — buffer means a reserved gap again.
  assert.equal(doc.pieces.some(p => p.role === "buffer"), false);
  // Every box groups the pieces it annotates, and every piece lands in exactly one box.
  const owned = doc.boxes.flatMap(b => boxMembers(doc, b).map(p => p.id));
  assert.equal(new Set(owned).size, owned.length, "no piece belongs to two boxes");
  assert.equal(owned.length, doc.pieces.length, "every piece belongs to a box");
});

// ── marker identity ─────────────────────────────────────────────────────────
test("every marker is minted an id, counting per kind across the whole set", () => {
  const doc = normalizeDoc({
    plan: 2,
    placements: {
      spawns: [{ piece: "home", at: [1, 1] }],
      cores: [{ piece: "mid", at: [0, 0] }, { piece: "mid", at: [2, 2] }],
    },
  });
  assert.equal(doc.placements.spawns[0].id, "spawn-1");
  assert.deepEqual(doc.placements.cores.map(c => c.id), ["core-1", "core-2"]);
});

test("an authored id is kept, and a later marker mints around it", () => {
  // Identity is the author's to set — an id that means something to them must survive a load, and the mint
  // has to route around it rather than issue the same name twice.
  const doc = normalizeDoc({
    plan: 2,
    placements: { cores: [{ id: "core-2", piece: "mid", at: [0, 0] }, { piece: "mid", at: [2, 2] }] },
  });
  assert.deepEqual(doc.placements.cores.map(c => c.id), ["core-2", "core-1"]);
});

test("a duplicate id is not an id — the later marker is re-minted", () => {
  // Two markers answering to one name is worse than no name: a finding or an agent naming it would resolve
  // to whichever came first, silently.
  const doc = normalizeDoc({
    plan: 2,
    placements: { cores: [{ id: "heart", piece: "mid", at: [0, 0] }, { id: "heart", piece: "mid", at: [2, 2] }] },
  });
  assert.equal(doc.placements.cores[0].id, "heart");
  assert.equal(doc.placements.cores[1].id, "core-1");
});

test("ids are unique across kinds, not merely within one", () => {
  const doc = normalizeDoc({
    plan: 2,
    placements: { wools: [{ id: "core-1", piece: "vault", at: [0, 0] }], cores: [{ piece: "mid", at: [0, 0] }] },
  });
  assert.equal(doc.placements.cores[0].id, "core-2");
});
