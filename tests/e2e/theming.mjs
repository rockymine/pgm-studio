/**
 * Theme phase on the sketch tool (finishing-model.md §4) — the paint pass moved off the plan onto the sketch,
 * keyed on shapes/islands.
 *
 *   1. API round-trip — a sketch layout carrying a theme registry + map default + a per-shape override
 *      survives PUT → GET through the real sketch endpoint (the storage the export resolves paint from).
 *   2. UI — the sketch tool's new Theme phase renders: Create (author a theme) and Apply (the island tree
 *      plus the theme controls), both screenshotted and clean.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("theming (finishing-model §4)");
const OUT = new URL("../../.tmp/", import.meta.url).pathname;
await mkdir(OUT, { recursive: true });

// ── 1. themes round-trip through the sketch layout ────────────────────────────────────────────────────
checks.section("a themed sketch layout survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const firstShapeId = layout?.layers?.[0]?.layout?.shapes?.find(s => !s.role)?.id;
const themed = structuredClone(layout);
themed.themes = { forest: { bedrock: { relative: false, value: 1 },
  surface: { material: { kind: "solid", id: 2 }, depth: 1, enabled: true },   // grass — a green the overlay shows
  fill: { kind: "solid", id: 3 } } };
themed.mapTheme = "forest";
if (firstShapeId) themed.layers[0].layout.shapes.find(s => s.id === firstShapeId).theme = "forest";
await api(`/map/${seed.sketchSlug}/sketch`, { method: "PUT", body: themed });

const back = await api(`/map/${seed.sketchSlug}/sketch`);
checks.add("theme registry persisted", back.themes?.forest != null, JSON.stringify(Object.keys(back.themes ?? {})));
checks.add("map default persisted", back.mapTheme === "forest", `mapTheme=${back.mapTheme}`);
checks.add("per-shape override persisted",
  !firstShapeId || back.layers[0].layout.shapes.find(s => s.id === firstShapeId)?.theme === "forest",
  firstShapeId ? `shape ${firstShapeId}` : "(no terrain shape to theme)");

// ── 2. the Theme phase renders (Create + Apply) ───────────────────────────────────────────────────────
checks.section("the sketch Theme phase renders");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

async function shot(file) { await page.screenshot({ path: `${OUT}${file}`, fullPage: false }); }

clearFaults(page);
let ok = false;
try {
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1500);

  // Enter the Theme phase (the palette nav button), then screenshot the Create step.
  await page.click('button[title="Theme"]', { timeout: 8000 });
  await page.waitForTimeout(800);
  await shot("theme-create.png");
  checks.add("Theme · Create renders", await page.locator("text=A theme is a full terrain-paint recipe").count() > 0);

  // Author a theme, then advance to Apply.
  await page.fill('input[placeholder="name"]', "forest");
  await page.click('button:has-text("Add")');
  await page.waitForTimeout(600);
  await page.click('button:has-text("Apply →")');
  await page.waitForTimeout(800);
  await shot("theme-apply.png");
  const applyRendered = await page.locator("text=Map default").count() > 0
    && await page.locator('text=Theme to apply').count() > 0;
  checks.add("Theme · Apply renders (island tree + theme controls)", applyRendered);

  // Select the island in the tree, apply the theme, then toggle Blocks — the overlay should now paint the
  // footprint in the theme's surface colour (the paint preview, via the existing Blocks toggle).
  await page.click('.geo-row:has-text("island")');
  await page.waitForTimeout(400);
  await page.click('button:has-text("Apply forest")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Blocks")');
  await page.waitForTimeout(900);
  await shot("theme-blocks.png");
  checks.add("themed shape applied", await page.locator("text=forest").count() > 1);
  ok = true;
} catch (e) {
  page.faults.push(`theme phase drive: ${String(e).split("\n")[0]}`);
}
checks.add("Theme phase drove without error", ok);
checks.add("sketch tool is clean under theming", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
