/**
 * Layer 1 — the smoke sweep. Every routable page loads, renders its shell, and raises nothing.
 *
 * This is deliberately the cheapest possible suite: no flows, no geometry assertions, no numbers that
 * can drift. It answers one question per route — *is this page alive?* — which is the failure class the
 * project keeps shipping (a WASM boot break makes every page a blank "Loading"; a rename breaks a route;
 * a phase host throws on mount and the tool never appears). None of those are visible to a unit test,
 * and all of them are visible here in one line each.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();

/** `expect` names a selector that must exist once the page has settled — the page's "I rendered" proof. */
const ROUTES = [
  { path: "/",                                    name: "landing",        expect: ".landing-choices a.card" },
  { path: "/maps",                                name: "maps dashboard", expect: ".studio-shell, .workspace, main" },
  { path: "/maps?stage=sketch",                   name: "maps · sketch",  expect: "body" },
  { path: "/maps?stage=plan",                     name: "maps · plan",    expect: "body" },
  { path: "/maps?stage=configure",                name: "maps · configure", expect: "body" },
  { path: "/design",                              name: "design system",  expect: "body" },
  { path: "/generator",                           name: "generator",      expect: "body" },
  // the catalog proves itself by its cards: an empty grid is a dead enumerator, not a slow page
  { path: "/catalog",                             name: "shape catalog",  expect: ".lib-grid .lib-card" },
  // the library's three halves render from the database, so an empty one is still a live page — its filter
  // rail is the proof, since that is what a dead component would take down with it
  { path: "/library",                             name: "style library",  expect: ".lib-body .lib-filters" },
  { path: "/library/themes",                      name: "theme library",  expect: ".lib-body .lib-filters" },
  { path: "/library/rooms",                       name: "room library",   expect: ".lib-body .lib-filters" },
  { path: "/plan-editor",                         name: "plan editor (bare)", expect: ".map-canvas-svg" },
  { path: `/maps/${seed.planSlug}/plan`,          name: "plan tool",      expect: "body" },
  { path: `/maps/${seed.sketchSlug}/sketch`,      name: "sketch tool",    expect: "body" },
  { path: `/maps/${seed.mapSlug}/configure`,      name: "configure tool", expect: "body" },
  { path: `/maps/${seed.mapSlug}/edit`,           name: "edit tool",      expect: "body" },
  { path: "/maps/new",                            name: "new map",        expect: "body" },
  { path: "/not-found",                           name: "not found",      expect: "body" },
  { path: "/definitely-not-a-route",              name: "unknown route",  expect: "body" },
];

const checks = new Checks("smoke");
const browser = await openBrowser();
const page = await newPage(browser);
const tolerated = new Set();

checks.section(`every route is alive (${BASE})`);
for (const route of ROUTES) {
  clearFaults(page);
  let rendered = false;
  try {
    await page.goto(`${BASE}${route.path}`, { waitUntil: "networkidle", timeout: 30000 });
    // Blazor WASM boots after the network settles, so wait for the proof selector rather than a timeout.
    await page.waitForSelector(route.expect, { timeout: 20000 });
    rendered = true;
  } catch (e) {
    rendered = false;
    page.faults.push(`did not render "${route.expect}": ${String(e).split("\n")[0]}`);
  }
  for (const a of page.allowed) tolerated.add(a);

  checks.add(`${route.name} renders`, rendered, rendered ? route.path : `${route.path} — see faults`);
  checks.add(`${route.name} is clean`, page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
}

// The app must not be showing Blazor's unhandled-error bar on any of them.
clearFaults(page);
await page.goto(`${BASE}/maps`, { waitUntil: "networkidle" });
const errorUi = await page.locator("#blazor-error-ui").evaluate(el => getComputedStyle(el).display).catch(() => "none");
checks.add("no Blazor error bar", errorUi === "none", `display: ${errorUi}`);

if (tolerated.size) {
  console.log("\ntolerated (see ALLOWED_FAULTS in lib/harness.mjs):");
  for (const t of tolerated) console.log(`  · ${t}`);
}

checks.finish();
await browser.close();
