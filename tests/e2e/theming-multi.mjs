/**
 * Per-shape theming end-to-end (finishing-model.md §4), on a board with more than one themeable unit.
 *
 * The single-island seed can't exercise per-shape assignment — it has one shape at one height. So this composes
 * a generator plan and **randomizes its piece heights**, which makes `PlanCompiler` emit one shape per plateau
 * instead of fusing everything into one. Then it drives the sketch UI to assign a theme to an individual shape
 * and proves the assignment **persisted** (a GET of the saved layout carries the shape's theme — the thing the
 * earlier drive left ambiguous), and screenshots the Blocks overlay showing two shapes in two distinct theme
 * colours against the stone the rest is built from.
 */

import { openBrowser, newPage, clearFaults, Checks, api, BASE } from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const OUT = new URL("../../.tmp/", import.meta.url).pathname;
await mkdir(OUT, { recursive: true });
const checks = new Checks("theming · per-shape (finishing-model §4)");

// The terrain (non-structural) shapes wherever the layout keeps them — legacy single `layout`, or `layers[]`
// once the editor has saved through getState().
const terrainShapes = (l) => (l?.layers?.[0]?.layout?.shapes ?? l?.layout?.shapes ?? []).filter(s => !s.role);

// ── build a multi-shape sketch from a height-randomized generator plan ─────────────────────────────────
checks.section("a height-randomized plan compiles to several themeable shapes");

const pinned = await api("/compose/pin", { method: "POST", body: { players: 12, teams: 2, symmetry: "rot_180", seed: 0, cell: 5 } });
const plan = JSON.parse(pinned.planJson);
plan.pieces.forEach((p, i) => { p.surface = 9 + (i % 3) * 6; });   // 9 / 15 / 21 — distinct plateaus, so pieces don't all fuse
const compiled = await api("/plan/compile", { method: "POST", body: JSON.stringify(plan) });
const shapeCount = terrainShapes(compiled.layout).length;
checks.add("plan yields multiple shapes", shapeCount >= 2, `${shapeCount} terrain shapes`);

const { slug } = await api("/sketch", { method: "POST", body: { name: "theme multi" } });
await api(`/map/${slug}/sketch`, { method: "PUT", body: compiled.layout });

// ── drive the UI: assign a theme to ONE shape, and prove it persisted ──────────────────────────────────
checks.section("the UI per-shape assignment persists");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
let ok = false;
try {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${slug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1200);

  await page.click('button[title="Theme"]');
  await page.waitForTimeout(500);
  await page.fill('input[placeholder="name"]', "grass");
  await page.click('button:has-text("Add")');
  await page.waitForTimeout(400);
  await page.click('button:has-text("Apply →")');
  await page.waitForTimeout(500);

  // A shape row (indented — the island rows have no pipe), then apply the theme to it.
  await page.locator('.geo-row:has(.indent-pipe)').first().click();
  await page.waitForTimeout(300);
  await page.click('button:has-text("Apply grass")');
  await page.waitForTimeout(1500);   // let the debounced save flush
  ok = true;
} catch (e) {
  page.faults.push(`drive: ${String(e).split("\n")[0]}`);
}
checks.add("Theme phase drove without error", ok);
checks.add("sketch tool clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

// The assertion the earlier run couldn't make: the shape the UI themed carries it in the saved layout.
const saved = await api(`/map/${slug}/sketch`);
const grassed = terrainShapes(saved).filter(s => s.theme === "grass");
checks.add("a shape's theme persisted through the UI", grassed.length >= 1, `${grassed.length} shape(s) → grass`);
checks.add("only the assigned shape is themed (not all)", grassed.length < terrainShapes(saved).length,
  `${grassed.length}/${terrainShapes(saved).length}`);

// ── distinct per-shape colours in the Blocks overlay ───────────────────────────────────────────────────
checks.section("two shapes, two colours, over stone");

// Give grass a clear green surface and add a sand theme on a different shape, then reload and toggle Blocks.
const shapes = terrainShapes(saved);
const grassShape = shapes.find(s => s.theme === "grass");
const other = shapes.find(s => !s.theme && s.id !== grassShape?.id) ?? shapes.find(s => s.id !== grassShape?.id);
saved.themes = {
  ...(saved.themes ?? {}),
  grass: { bedrock: { relative: false, value: 1 }, surface: { material: { kind: "solid", id: 2 }, depth: 1, enabled: true }, fill: { kind: "solid", id: 1 } },
  sand:  { bedrock: { relative: false, value: 1 }, surface: { material: { kind: "solid", id: 12 }, depth: 1, enabled: true }, fill: { kind: "solid", id: 1 } },
};
if (other) other.theme = "sand";
saved.mapTheme = "grass";   // the whole footprint reads green, the one sand-overridden shape stands out
await api(`/map/${slug}/sketch`, { method: "PUT", body: saved });

let shotOk = false;
try {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${slug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(2000);   // WASM boot + the async block-palette fetch that colours the overlay
  // Into the Theme phase's Apply step (the island tree + theme controls), and settle before toggling.
  await page.click('button[title="Theme"]');
  await page.waitForTimeout(700);
  await page.click('button:has-text("Apply →")');
  await page.waitForTimeout(1500);
  // Turn Blocks on *here* (so the raster paints while the canvas is visible) and confirm it took.
  const blocks = page.locator('button.filter-chip:has-text("Blocks")');
  const isOn = async () => blocks.evaluate(el => el.classList.contains("filter-chip--active"));
  if (!(await isOn())) await blocks.click();
  await page.waitForTimeout(2500);   // let the coloured raster render
  const blocksOn = await isOn();
  await page.screenshot({ path: `${OUT}theme-multi-blocks.png`, fullPage: false });
  checks.add("Blocks overlay is on for the shot", blocksOn);
  shotOk = true;
} catch (e) {
  page.faults.push(`blocks shot: ${String(e).split("\n")[0]}`);
}
checks.add("two shapes carry distinct themes", other?.theme === "sand" && grassShape?.theme === "grass");
checks.add("themed Blocks overlay captured", shotOk, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
