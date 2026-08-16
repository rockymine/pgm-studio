/**
 * S25 — the plan's spawn/wool pieces surface as locked, labelled rectangles in the sketch.
 *
 * Two halves, both driven on a REAL composed board (the seed's plan is straight from the generator, single
 * height — so its same-plane pieces fuse into one island polygon, exactly the case where a spawn/wool would
 * otherwise vanish):
 *   1. an API check — compiling the plan yields role-tagged spawn/wool rectangles ALONGSIDE the fused terrain,
 *      and they carry the link (intentRef) + colour the sketch renders from;
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
const shapes = compiled.layout?.layout?.shapes ?? [];
const spawns  = shapes.filter(s => s.role === "spawn");
const wools   = shapes.filter(s => s.role === "woolRoom");
const terrain = shapes.filter(s => !s.role);

checks.add("spawn pieces surfaced", spawns.length >= 2, `${spawns.length} spawn rectangles`);
checks.add("wool rooms surfaced", wools.length >= 2, `${wools.length} wool rectangles`);
checks.add("structural shapes are rectangles",
  [...spawns, ...wools].every(s => s.type === "rectangle" && s.min_x !== undefined),
  "each carries a rectangle footprint");
checks.add("each is linked back to its intent entity",
  [...spawns, ...wools].every(s => s.intentRef) && spawns.every(s => s.color),
  "intentRef + colour present");
// The very case the feature exists for: the single-height board fused its terrain, yet the pieces survive.
checks.add("terrain fused below the surfaced pieces",
  terrain.length > 0 && terrain.length < spawns.length + wools.length,
  `${terrain.length} terrain polygon(s) vs ${spawns.length + wools.length} structural pieces`);

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
