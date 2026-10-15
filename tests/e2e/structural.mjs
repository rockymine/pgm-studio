/**
 * The plan's spawn/wool pieces surface as locked, labelled rectangles in the sketch.
 *
 * Two halves, both driven on a REAL composed board (the seed's plan is straight from the generator, single
 * height — so its same-plane pieces fuse into one island polygon, exactly the case where a spawn/wool would
 * otherwise vanish):
 *   1. an API check — the compiled layout stores no room, only what it says about each room's ground, and the
 *      sketch reads it back with role-tagged spawn/wool rectangles drawn from the intent ALONGSIDE the fused
 *      terrain, each under its piece's id and with the colour the sketch renders from;
 *   2. a visual check — the plan tool and the sketch tool are screenshotted (the sketch is the seed draft that
 *      carries that same compiled layout), so the surfaced pieces can be seen where the terrain has fused.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE, TMP_DIR }
  from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("structural (S25)");
const OUT = TMP_DIR;
await mkdir(OUT, { recursive: true });

// ── 1. the compiled layout carries the surfaced pieces ────────────────────────────────────────────────
checks.section("compiled layout surfaces the plan's structural pieces");

const compiled = await api("/plan/compile", { method: "POST", body: seed.planJson });
const stored = (compiled.layout?.layers ?? []).flatMap(layer => layer.layout?.shapes ?? []);
checks.add("the compiled layout stores no room", !stored.some(s => s.role), `${stored.length} shapes`);
checks.add("the compiled layout states the rooms' ground", Object.keys(compiled.layout?.rooms ?? {}).length > 0,
  Object.keys(compiled.layout?.rooms ?? {}).join(", "));

// The draft holds that layout and its intent; the sketch reads it with the rooms drawn in. The layout is a
// stack of layers and a layer holds its own drawing, so the shapes are read per layer.
const read = await api(`/map/${seed.sketchSlug}/sketch`);
const intent = await api(`/map/${seed.sketchSlug}/intent`);
const shapes = (read.layers ?? []).flatMap(layer => layer.layout?.shapes ?? []);
const spawns  = shapes.filter(s => s.role === "spawn");
const wools   = shapes.filter(s => s.role === "woolRoom");
const terrain = shapes.filter(s => !s.role);

checks.add("spawn pieces surfaced", spawns.length >= 2, `${spawns.length} spawn rectangles`);
checks.add("wool rooms surfaced", wools.length >= 2, `${wools.length} wool rectangles`);
checks.add("structural shapes are rectangles",
  [...spawns, ...wools].every(s => s.type === "rectangle" && s.min_x !== undefined),
  "each carries a rectangle footprint");
const pieces = new Set([...(intent.spawns ?? []), ...(intent.wools ?? [])].map(piece => piece.id));
checks.add("each is drawn under its piece's id",
  [...spawns, ...wools].every(s => pieces.has(s.id)) && spawns.every(s => s.color),
  "piece id + colour present");
// The very case the feature exists for: the single-height board fused its terrain, yet the pieces survive.
checks.add("terrain fused below the surfaced pieces",
  terrain.length > 0 && terrain.length < spawns.length + wools.length,
  `${terrain.length} terrain polygon(s) vs ${spawns.length + wools.length} structural pieces`);

// ── 1b. every other piece is drawn from the intent and moves through the piece route ──────────────────
checks.section("every play piece is read where the sketch draws it");

const drawn = await api(`/map/${seed.sketchSlug}/intent/pieces`);
const kinds = new Set(drawn.map(piece => piece.kind));
checks.add("the pieces read carries the rooms and the rest", kinds.has("spawn") && kinds.has("buildArea"),
  [...kinds].join(", "));
checks.add("every piece has an id", drawn.every(piece => piece.id), `${drawn.length} pieces`);
const area = drawn.find(piece => piece.kind === "buildArea" && !piece.carried);
if (area) {
  await api(`/map/${seed.sketchSlug}/intent/pieces/${encodeURIComponent(area.id)}`, {
    method: "PATCH",
    body: JSON.stringify({ minX: area.minX + 1, minZ: area.minZ, maxX: area.maxX + 1, maxZ: area.maxZ }),
  });
  const after = (await api(`/map/${seed.sketchSlug}/intent/pieces`)).find(piece => piece.id === area.id);
  checks.add("a build area dragged a block stands where it was dragged", after?.minX === area.minX + 1,
    `${area.id}: ${area.minX} → ${after?.minX}`);
  await api(`/map/${seed.sketchSlug}/intent/pieces/${encodeURIComponent(area.id)}`, {
    method: "PATCH",
    body: JSON.stringify({ minX: area.minX, minZ: area.minZ, maxX: area.maxX, maxZ: area.maxZ }),
  });
}

// ── 2. screenshot the plan tool and the sketch tool ───────────────────────────────────────────────────
checks.section("plan → sketch, captured");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

async function capture(path, file, waitFor) {
  clearFaults(page);
  let ok = false;
  try {
    await page.goto(`${BASE}${path}`, { waitUntil: "networkidle", timeout: 30000 });
    await page.waitForSelector(waitFor, { timeout: 20000 });
    await page.waitForTimeout(2500);   // WASM boot + canvas paint + fit-to-bbox settle
    await page.screenshot({ path: `${OUT}${file}`, fullPage: false });
    ok = true;
  } catch (e) {
    page.faults.push(`capture ${path}: ${String(e).split("\n")[0]}`);
  }
  checks.add(`${file} captured`, ok, ok ? path : "see faults");
  checks.add(`${path} is clean`, page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
}

// The plan tool renders the generated plan (the source); the sketch draft carries the same compiled layout,
// so the surfaced spawn/wool boxes appear over the fused island.
await capture(`/maps/${seed.planSlug}/plan`, "s25-plan.png", "canvas, .map-canvas-svg");
await capture(`/maps/${seed.sketchSlug}/sketch`, "s25-sketch.png", "canvas");

checks.finish();
await browser.close();
