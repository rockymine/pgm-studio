// Characterization tests for the plan-editor document model + geometry (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

import {
  emptyDoc, normalizeDoc, fromJson, toJson, uniqueId, nextFacing,
  rectCellsToBlocks, cellOfWorld, rectFromCells, rectContainsCell,
  pieceAtCell, zoneAtCell, markerCell, attachMarker, allMarkers,
  contentBounds, viewBounds, pieceMirrorImages, markerMirrorImages, ROLES,
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

test("uniqueId suffixes on collision", () => {
  assert.equal(uniqueId(["lane", "hub"], "mid"), "mid");
  assert.equal(uniqueId(["lane", "lane-2"], "lane"), "lane-3");
});

test("nextFacing cycles front → right → back → left → front", () => {
  assert.equal(nextFacing("front"), "right");
  assert.equal(nextFacing("right"), "back");
  assert.equal(nextFacing("left"), "front");
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

test("markerMirrorImages mirrors marker centres about the origin", () => {
  const doc = normalizeDoc({
    plan: 1, globals: { cell: 5, symmetry: "rot_180" },
    pieces: [{ id: "bar", role: "lane", rect: [1, 1, 1, 1] }],
    placements: { spawns: [{ piece: "bar", at: [0, 0], facing: "front" }] },
  });
  const [img] = markerMirrorImages(doc);
  assert.deepEqual([img.x, img.z], [-7.5, -7.5]);   // centre (7.5,7.5) rotated 180° about origin
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

test("ROLES palette order is stable", () => {
  assert.deepEqual(ROLES, ["lane", "hub", "wool-room", "mid"]);
});

// ── round-trip a real seed plan ──────────────────────────────────────────────
test("fromJson → toJson round-trips a seed plan's data", () => {
  const text = readFileSync(seedPath, "utf8");
  const doc = fromJson(text);
  const back = fromJson(toJson(doc));
  assert.deepEqual(back, doc);                                   // stable under re-serialisation
  // Core data survives verbatim.
  assert.equal(doc.globals.symmetry, "rot_180");
  assert.equal(doc.pieces.length, 6);
  assert.deepEqual(doc.pieces.find(p => p.id === "bar-e").rect, [1, 5, 2, 6]);
  assert.equal(doc.pieces.find(p => p.id === "wl2-a").surface, 13);
  assert.deepEqual(doc.placements.spawns[0], { piece: "bar-e", at: [1, 5], facing: "front" });
});
