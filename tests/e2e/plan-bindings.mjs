/**
 * One plan editor on two bindings. A plan opens on a map (`/maps/{slug}/plan`) or as a plan row
 * (`/plans/{id}`, `/plans/new`), and both are the same tool: the rail's Info and Draw phases, the editor bar,
 * the sidebar's three panel chips, and the sidebar folding away. Only the bar's commands follow the binding, because
 * only saving differs — a map-backed plan saves into its map, a plan row saves as a row and forks when it was
 * generated or imported.
 *
 * So the check is a comparison rather than a list of selectors per route: the structure is read off each
 * route the same way and has to come out equal, and the bar's commands are the one reading that has to come out
 * different.
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
  const chips = await texts(".plan-panel-switch .flow-step");

  // The sidebar folds away and comes back, and the canvas is what takes the width.
  await page.click('button[title="Hide panel"]');
  await page.waitForTimeout(150);
  const folded = await page.locator(".workspace-sidebar").count() === 0
              && await page.locator(".plan-sidebar-stub").count() === 1;
  await page.click('button[title="Show panel"]');
  await page.waitForTimeout(150);
  const unfolded = await page.locator(".workspace-sidebar").count() === 1;

  return JSON.stringify({ rail, phase, next, chips, folds: folded && unfolded });
}

/** The editor bar's commands, by their words. */
const actions = () => texts(".editor-bar-commands .action-btn");

let drove = false;
try {
  checks.section("the same tool on every route");

  await open(`/maps/${seed.planSlug}/plan`);
  const onMap = await structure();
  const mapActions = await actions();

  await open(`/plans/${seed.planId}`);
  const onRow = await structure();
  const rowActions = await actions();

  await open("/plans/new");
  const onNew = await structure();

  const expected = JSON.stringify({
    rail: ["Info", "Draw"], phase: "Draw", next: "Compile",
    chips: ["Settings", "Checks", "Generator"], folds: true,
  });
  checks.add("a map-backed plan has the rail, the editor bar, the chips and a folding sidebar", onMap === expected, onMap);
  checks.add("a plan row has the same", onRow === onMap, onRow);
  checks.add("and so does a new plan", onNew === onMap, onNew);

  checks.section("only the bar's commands follow the binding");
  checks.add("a map-backed plan's commands are Save alone", JSON.stringify(mapActions) === '["Save"]', mapActions.join(", "));
  checks.add("a plan row's commands start, import, open and save rows",
    ["New", "Import", "Open", "Save"].every(word => rowActions.includes(word)), rowActions.join(", "));

  checks.section("the generator's hand-off opens the row, and saving it forks");
  await open(`/plans/${seed.planId}`);
  const origin = (await texts(".editor-bar-commands .badge")).join();
  checks.add("a pinned candidate opens as a generated row", origin === "generated", origin || "(no badge)");
  checks.add("the studio bar lights the plan editor on a row",
    (await texts(".app-nav-link--active")).join() === "Plan editor", (await texts(".app-nav-link--active")).join());

  await page.click('.editor-bar-commands button:has-text("Save")');
  await page.waitForURL(url => /\/plans\/\d+$/.test(url.pathname) && !url.pathname.endsWith(`/plans/${seed.planId}`),
    { timeout: 10000 });
  const forkId = Number(new URL(page.url()).pathname.split("/").pop());
  checks.add("Save forks a generated row and the address follows the copy", forkId !== seed.planId, page.url());
  checks.add("the copy is authored", (await texts(".editor-bar-commands .badge")).join() === "authored",
    (await texts(".editor-bar-commands .badge")).join());
  const original = await api(`/plans/${seed.planId}`);
  checks.add("and the candidate is untouched", original.origin === "generated", original.origin);
  await apiRaw(`/plans/${forkId}`, { method: "DELETE" });

  checks.section("leaving a map for a new plan rebinds the editor");
  await open(`/maps/${seed.planSlug}/plan`);
  const mapName = (await texts(".editor-crumb-name")).at(0);
  await page.click('.app-nav-link:has-text("Plan editor")');
  await page.waitForURL(url => url.pathname.endsWith("/plans/new"), { timeout: 10000 });
  await page.waitForFunction(() => document.querySelector(".editor-crumb-name")?.textContent.trim() === "Untitled plan",
    null, { timeout: 10000 }).catch(() => {});
  const crumb = (await texts(".editor-crumb-name")).at(0);
  checks.add("the new plan does not carry the map's", crumb === "Untitled plan", `${mapName} → ${crumb}`);
  checks.add("and its commands are a plan row's", (await actions()).includes("New"), (await actions()).join(", "));

  drove = true;
} catch (e) {
  page.faults.push(`bindings: ${String(e).split("\n")[0]}`);
}
checks.add("the binding checks ran", drove, page.faults.slice(0, 3).join(" | "));
checks.add("and raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
