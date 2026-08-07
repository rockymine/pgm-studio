/**
 * G152 — the plan editor shows the plan it names, and a built map keeps the plan it was built from.
 *
 * Both halves are about a document arriving from somewhere it should not. The editor used to restore a
 * localStorage copy of the last-edited plan at mount, before any route-specific load ran, so a map with no
 * stored plan yet rendered whatever that browser last drew: New opened someone else's board, the same map
 * looked different in two browsers, and saving carried the stray drawing into the map that was open. The
 * other half is the same fact from the far end — a map built from the bare route held a layout whose source
 * document was nowhere, so opening it in the plan editor showed a blank board over a plainly-authored map.
 *
 * Driven through the real UI on purpose. The bug lived entirely in what the browser held at mount, which no
 * API-level test can see: every request involved was already correct.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("plan identity (G152)");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

const pieceCount = (plan) => (plan?.pieces ?? []).length;

// Two fresh plan maps, both blank on the server. `drawn` is then given a real plan so the browser has
// something to remember; `blank` is left as the `{}` a New plan starts as.
const drawn = (await api("/plan", { method: "POST", body: { name: "e2e identity drawn" } })).slug;
const blank = (await api("/plan", { method: "POST", body: { name: "e2e identity blank" } })).slug;
await api(`/map/${drawn}/plan`, { method: "PUT", body: seed.planJson });

checks.section("a blank plan starts blank on the server");
checks.add("the drawn map has a plan", pieceCount(await api(`/map/${drawn}/plan`)) > 0,
  `${pieceCount(await api(`/map/${drawn}/plan`))} pieces`);
checks.add("the blank map has none", pieceCount(await api(`/map/${blank}/plan`)) === 0, "{}");

// ── the editor holds only what its route loaded ───────────────────────────────────────────────────────
checks.section("opening one plan does not carry it into the next");

async function openPlan(slug) {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${slug}/plan`, { waitUntil: "networkidle" });
  await page.waitForSelector(".map-canvas-svg", { timeout: 15000 });
  await page.waitForTimeout(1200);   // the artifact load runs after first render
}

let drove = false;
try {
  // Open the drawn plan first, so anything the editor caches has been cached by the time the blank one opens.
  await openPlan(drawn);
  checks.add("the drawn plan opens without faults", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

  // The deletion itself: no document key, whatever else the browser keeps. The three that remain are UI
  // preferences (overlay chips, height-map fill, surface stepper) and are named here so a fourth appearing
  // is a decision rather than a drift back.
  const keys = await page.evaluate(() => Object.keys(localStorage).filter(k => k.startsWith("pgm-plan")));
  checks.add("no plan document is cached in the browser", !keys.includes("pgm-plan-editor"),
    keys.length ? keys.join(", ") : "(nothing stored)");

  // Now the blank one. Saving immediately writes back exactly what the editor was holding, which is the
  // question: before the fix this wrote the drawn plan into the blank map.
  await openPlan(blank);
  await page.click('button:has-text("Save")');
  await page.waitForTimeout(1500);

  const after = await api(`/map/${blank}/plan`);
  checks.add("the blank plan is still blank after opening a drawn one first", pieceCount(after) === 0,
    `${pieceCount(after)} pieces — ${pieceCount(after) ? "the previous plan followed the browser" : "empty"}`);
  checks.add("and the drawn plan is untouched", pieceCount(await api(`/map/${drawn}/plan`)) > 0,
    `${pieceCount(await api(`/map/${drawn}/plan`))} pieces`);
  drove = true;
} catch (e) {
  page.faults.push(`identity: ${String(e).split("\n")[0]}`);
}
checks.add("the identity checks ran", drove, page.faults.slice(0, 3).join(" | "));

// ── a built map carries the plan it was built from ────────────────────────────────────────────────────
// The seed's plan is a real composed board, so compiling it is a compile that succeeds. Building it from a
// map-backed route puts layout, intent AND plan on the one row — which is what lets reopen reach both tools.
checks.section("building a draft records the plan on the map");

let built = false;
try {
  const target = (await api("/plan", { method: "POST", body: { name: "e2e identity built" } })).slug;
  await api(`/map/${target}/plan`, { method: "PUT", body: seed.planJson });
  await openPlan(target);

  await page.click('button:has-text("Compile")');
  await page.waitForSelector(".plan-compile-json", { timeout: 20000 });
  await page.click('button:has-text("Build draft")');
  // Scoped to the drawer: the Score panel uses the same class for "No rules fired".
  await page.waitForSelector(".plan-compile-draft .plan-lint-ok", { timeout: 90000 });

  const plan = await api(`/map/${target}/plan`);
  checks.add("the built map still holds its plan", pieceCount(plan) > 0, `${pieceCount(plan)} pieces`);
  const layout = await api(`/map/${target}/sketch`);
  checks.add("…beside the layout it compiled into", (layout?.layers ?? layout?.layout) != null,
    "both blobs on the one row — what lets reopen reach either tool");
  built = true;
} catch (e) {
  page.faults.push(`build: ${String(e).split("\n")[0]}`);
}
checks.add("the build checks ran", built, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
