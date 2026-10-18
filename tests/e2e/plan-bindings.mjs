/**
 * The plan editor is bound to a map (`/maps/{slug}/plan`) and to nothing else: the rail's Info and Draw phases,
 * the editor bar whose one command is Save, one sidebar with no tabs, and the sidebar folding away. A plan row
 * has no editor — the stored rows are history, read through `/api/plans` — and a navigation from one map's plan
 * to another's rebinds the mounted editor rather than carrying the first map's document.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, apiRaw, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("plan bindings (G154)");
const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

async function open(path) {
  clearFaults(page);
  await page.goto(`${BASE}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".map-canvas-svg", { timeout: 20000 });
  // The plan load runs after first render, and Compile says "Loading…" until it lands.
  await page.waitForSelector(".editor-bar .editor-bar-move .action-btn--primary:not([disabled])",
    { timeout: 20000 });
}

const texts = (selector) => page.locator(selector).evaluateAll(els => els.map(el => el.textContent.trim()));

/** What the tool is, read the same way on every route. */
async function structure() {
  const rail = await page.locator(".nav-rail .nav-btn").evaluateAll(els => els.map(el => el.title));
  const phase = (await texts(".editor-bar .editor-bar-phase")).join();
  const next = (await texts(".editor-bar .editor-bar-move .action-btn--primary")).join();
  const tablists = await page.locator(".workspace-sidebar [role=tablist]").count();

  // The sidebar folds away and comes back, and the canvas is what takes the width.
  await page.click('button[title="Hide panel"]');
  await page.waitForTimeout(150);
  const folded = await page.locator(".workspace-sidebar").count() === 0
              && await page.locator(".plan-sidebar-stub").count() === 1;
  await page.click('button[title="Show panel"]');
  await page.waitForTimeout(150);
  const unfolded = await page.locator(".workspace-sidebar").count() === 1;

  return JSON.stringify({ rail, phase, next, tablists, folds: folded && unfolded });
}

/** The editor bar's commands, by their words. */
const actions = () => texts(".editor-bar-commands .action-btn");

let drove = false;
try {
  checks.section("the tool on a map");

  await open(`/maps/${seed.planSlug}/plan`);
  const onMap = await structure();
  const expected = JSON.stringify({
    rail: ["Info", "Draw"], phase: "Draw", next: "Compile",
    tablists: 0, folds: true,
  });
  checks.add("a plan has the rail, the editor bar and one folding sidebar with no tabs", onMap === expected, onMap);
  const mapActions = await actions();
  checks.add("its commands are Save alone", JSON.stringify(mapActions) === '["Save"]', mapActions.join(", "));

  checks.section("a plan row has no editor");
  clearFaults(page);
  await page.goto(`${BASE}/plans/new`, { waitUntil: "networkidle", timeout: 30000 });
  checks.add("/plans/new opens no plan editor", await page.locator(".map-canvas-svg").count() === 0);
  clearFaults(page);

  checks.section("starting a plan from the Maps page rebinds the editor");
  await open(`/maps/${seed.planSlug}/plan`);
  const mapName = (await texts(".editor-crumb-name")).at(0);
  await page.goto(`${BASE}/maps`, { waitUntil: "networkidle", timeout: 30000 });
  await page.click('button:has-text("New plan")');
  await page.waitForURL(url => /\/maps\/[^/]+\/plan$/.test(url.pathname) && !url.pathname.includes(`/${seed.planSlug}/`),
    { timeout: 10000 });
  const newSlug = new URL(page.url()).pathname.split("/")[2];
  await page.waitForFunction(() => document.querySelector(".editor-crumb-name")?.textContent.trim() === "Untitled plan",
    null, { timeout: 10000 }).catch(() => {});
  const crumb = (await texts(".editor-crumb-name")).at(0);
  checks.add("the new plan does not carry the map's", crumb === "Untitled plan", `${mapName} → ${crumb}`);
  await apiRaw(`/map/${newSlug}`, { method: "DELETE" });

  drove = true;
} catch (e) {
  page.faults.push(`bindings: ${String(e).split("\n")[0]}`);
}
checks.add("the binding checks ran", drove, page.faults.slice(0, 3).join(" | "));
checks.add("and raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
