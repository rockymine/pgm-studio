/**
 * A map's authoring layers are all visible, and all one click away.
 *
 * A map is one row that can hold a sketch and a world at once. Two claims here. The list that names a layer
 * lists every map holding it, whatever the map has since become; and a row offers each layer directly, without
 * moving the map — the stage pointer is progress, not a cursor, so looking at a layer changes nothing.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("map layers");

// The seed's map slug is the case in question: a sketch carried all the way to world geometry, so it holds
// both layers and stands at configure.
const slug = seed.mapSlug;
const find = (list) => list.find(m => m.slug === slug);

checks.section("a layer list shows every map holding that layer");

const sketches = await api("/maps?stage=sketch");
const configuring = await api("/maps?stage=configure");

checks.add("the built map is in Sketches, though its stage moved on", find(sketches) != null,
  `stage=${find(configuring)?.stage ?? "?"} — listed by the sketch it holds, not by where it stands`);
checks.add("…and in Configuring, where it stands", find(configuring) != null);

const entry = find(configuring);
checks.add("its row carries both layers", entry?.hasSketch && entry?.hasSurface,
  `sketch=${entry?.hasSketch} world=${entry?.hasSurface}`);

// A draft that was never finished has one layer — the flags are the map's own history, not a constant.
const draft = sketches.find(m => m.slug === seed.sketchSlug);
checks.add("a sketch that was never built carries only its sketch",
  draft != null && draft.hasSketch && !draft.hasSurface,
  `sketch=${draft?.hasSketch} world=${draft?.hasSurface}`);

// ── the row offers each layer, and reaching one does not move the map ──────────────────────────────────
checks.section("every layer is one click, and looking changes nothing");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
let drove = false;

try {
  clearFaults(page);
  await page.goto(`${BASE}/maps?stage=configure`, { waitUntil: "networkidle" });
  await page.waitForSelector(".map-table-row", { timeout: 15000 });

  const row = page.locator(".map-table-row", { has: page.locator(`text="${slug}"`) }).first();
  const layers = row.locator(".map-layer");
  const hrefs = await layers.evaluateAll(els => els.map(e => e.getAttribute("href")));
  checks.add("the row links sketch and configure",
    hrefs.length === 2 && hrefs.every((h, i) => h === `maps/${slug}/${["sketch", "configure"][i]}`),
    hrefs.join(" · "));

  const now = await layers.evaluateAll(els =>
    els.filter(e => e.classList.contains("map-layer--now")).map(e => e.textContent.trim()));
  checks.add("the layer the map stands at is marked", now.length === 1 && now[0] === "Configure", now.join(",") || "(none marked)");

  // No surface offers a backward hop with a hard-coded preference order.
  const reopen = await page.locator('button:has-text("Reopen")').count();
  checks.add("no reopen control remains", reopen === 0, `${reopen} found`);

  // Straight to the sketch from the Configuring list — one click, no intermediate list.
  await row.locator('.map-layer:has-text("Sketch")').click();
  await page.waitForURL(`**/maps/${slug}/sketch`, { timeout: 15000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForLoadState("networkidle");
  checks.add("the sketch opens on the built map", page.url().endsWith(`/maps/${slug}/sketch`), page.url());
  checks.add("opening it raised no faults", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

  // Looking at the sketch of a configured map leaves it configured, so nothing downstream is rebuilt or
  // re-listed by a visit.
  const after = find(await api("/maps?stage=configure"));
  checks.add("the map is still where it was", after != null && after.stage === "configure",
    after ? `stage=${after.stage}` : "no longer listed under Configuring");
  drove = true;
} catch (e) {
  page.faults.push(`layers: ${String(e).split("\n")[0]}`);
}
checks.add("the layer checks ran", drove, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
