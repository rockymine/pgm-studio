/**
 * The icon layer: the vendored lucide subset renders, on the real pages, offline.
 *
 * Icons used to come from cdn.jsdelivr.net at runtime, so they silently vanished wherever egress was
 * restricted — the nav rail, toolbars and chips came up blank while the app otherwise worked. The smoke
 * sweep can only see the *absence* of an error; nothing asserted that an icon actually appeared. This
 * does, positively: every <i data-lucide> placeholder on a real route must have been replaced by an svg
 * with real geometry in it.
 *
 * It also pins the shim's contract against lucide's own semantics — alias names, caller attributes,
 * class merging, the a11y opt-out, and the loud failure on a name that was never vendored (the guard
 * that stops a missed dynamic name from shipping as a blank square).
 */

import { readFileSync } from "node:fs";
import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const VENDORED = new URL("../../src/PgmStudio.Client/wwwroot/js/studio/vendor/lucide-icons.js", import.meta.url);
const STUDIO_JS = new URL("../../src/PgmStudio.Client/wwwroot/js/studio.js", import.meta.url);

const seed = await readSeed();
const checks = new Checks("icons");
const browser = await openBrowser();
const page = await newPage(browser);

// Routes whose chrome is icon-dense — the nav rail plus a canvas tool's toolbars.
const ROUTES = [
  { path: "/maps", name: "maps dashboard" },
  { path: `/maps/${seed.sketchSlug}/sketch`, name: "sketch tool" },
  { path: `/maps/${seed.mapSlug}/configure`, name: "configure tool" },
];

checks.section("icons render on the real pages");
for (const route of ROUTES) {
  clearFaults(page);
  await page.goto(`${BASE}${route.path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("body", { timeout: 20000 });
  // The factory runs after each Blazor render; give the first pass a moment to land.
  await page.waitForFunction(() => document.querySelector("svg.lucide") !== null, { timeout: 20000 })
    .catch(() => {});

  const state = await page.evaluate(() => ({
    rendered: document.querySelectorAll("svg.lucide").length,
    unreplaced: Array.from(document.querySelectorAll("i[data-lucide]")).map(e => e.getAttribute("data-lucide")),
    empty: Array.from(document.querySelectorAll("svg.lucide")).filter(s => s.children.length === 0).length,
  }));

  checks.add(`${route.name}: icons rendered`, state.rendered > 0, `${state.rendered} svg.lucide`);
  checks.add(`${route.name}: no placeholder left behind`, state.unreplaced.length === 0,
    state.unreplaced.slice(0, 5).join(", "));
  checks.add(`${route.name}: every icon has geometry`, state.empty === 0, `${state.empty} empty`);
  checks.add(`${route.name}: no icon fetched from a CDN`, page.faults.every(f => !/cdn\./.test(f)),
    page.faults.filter(f => /cdn\./.test(f)).join(" | "));
}

// The shim's contract, against a synthetic page — the cases the app's own markup does not all exercise.
checks.section("the vendored shim matches lucide's semantics");
clearFaults(page);
await page.setContent(`<!doctype html><html><body>
  <i data-lucide="scissors" class="tool-icon"></i>
  <i data-lucide="check-circle-2"></i>
  <i data-lucide="trash-2" aria-label="delete"></i>
  <i data-lucide="definitely-not-an-icon"></i>
</body></html>`);
await page.addScriptTag({ content: readFileSync(VENDORED, "utf8") });
await page.addScriptTag({ content: readFileSync(STUDIO_JS, "utf8") });
await page.evaluate(() => window.studio.icons());

const svgs = await page.$$eval("svg", els => els.map(e => ({
  name: e.getAttribute("data-lucide"),
  cls: e.getAttribute("class"),
  ariaHidden: e.getAttribute("aria-hidden"),
  ariaLabel: e.getAttribute("aria-label"),
  width: e.getAttribute("width"),
  strokeWidth: e.getAttribute("stroke-width"),
  kids: e.children.length,
})));
const leftover = await page.$$eval("i[data-lucide]", els => els.map(e => e.getAttribute("data-lucide")));
const by = (n) => svgs.find(s => s.name === n);

checks.add("a renamed alias still resolves", (by("check-circle-2")?.kids ?? 0) > 0,
  "check-circle-2 — lucide renamed it; the pinned vendor keeps the alias");
checks.add("the element keeps its own class", !!by("scissors")?.cls?.includes("tool-icon")
  && !!by("scissors")?.cls?.includes("lucide-scissors"), by("scissors")?.cls ?? "-");
checks.add("studio.js attrs are applied", by("scissors")?.width === "16" && by("scissors")?.strokeWidth === "1.5",
  `${by("scissors")?.width}px / ${by("scissors")?.strokeWidth}`);
checks.add("an a11y prop suppresses aria-hidden", by("trash-2")?.ariaHidden === null
  && by("trash-2")?.ariaLabel === "delete", JSON.stringify(by("trash-2") ?? null));
checks.add("a plain icon is aria-hidden", by("scissors")?.ariaHidden === "true", by("scissors")?.ariaHidden ?? "-");
checks.add("an unvendored name is left in place", leftover.length === 1, JSON.stringify(leftover));
checks.add("an unvendored name is reported loudly",
  page.faults.some(f => /definitely-not-an-icon/.test(f)),
  "a missed dynamic name must fail a run, not ship as a blank");

checks.finish();
await browser.close();
