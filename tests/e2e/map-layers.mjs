/**
 * A map's authoring layers are all visible, and all one click away.
 *
 * A map is one row that can hold a plan, a sketch and a world at once, but the overview filed it under the
 * single stage its pointer named and offered one backward hop at a time. So a planned, sketched, configured
 * map appeared only under Configuring, its own plan was unreachable from the Plans list, and reaching that
 * plan meant two reopens through two different lists — the second only discoverable after taking the first.
 *
 * Two claims here. The lists that name a layer list every map holding it, whatever the map has since become;
 * and a row offers each layer directly, without moving the map. The second matters most: opening a layer used
 * to be a stage change, and a stage change is what made "just look at the plan" feel dangerous.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("map layers");

// The seed's map slug is the case in question: a composed plan carried all the way to world geometry, so it
// holds all three layers and stands at configure.
const slug = seed.mapSlug;
const find = (list) => list.find(m => m.slug === slug);

checks.section("a layer list shows every map holding that layer");

const plans = await api("/maps?stage=plan");
const sketches = await api("/maps?stage=sketch");
const configuring = await api("/maps?stage=configure");

checks.add("the built map is in Plans, though its stage moved on", find(plans) != null,
  `stage=${find(configuring)?.stage ?? "?"} — listed by the plan it holds, not by where it stands`);
checks.add("…and in Sketches", find(sketches) != null);
checks.add("…and in Configuring, where it stands", find(configuring) != null);

const entry = find(configuring);
checks.add("its row carries all three layers", entry?.hasPlan && entry?.hasSketch && entry?.hasSurface,
  `plan=${entry?.hasPlan} sketch=${entry?.hasSketch} world=${entry?.hasSurface}`);

// A plan-only map has exactly one layer — the flags are the map's own history, not a constant.
const planOnly = plans.find(m => m.slug === seed.planSlug);
checks.add("a plan that was never built carries only its plan",
  planOnly != null && planOnly.hasPlan && !planOnly.hasSketch && !planOnly.hasSurface,
  `plan=${planOnly?.hasPlan} sketch=${planOnly?.hasSketch} world=${planOnly?.hasSurface}`);

// ── the row offers each layer, and reaching one does not move the map ──────────────────────────────────
checks.section("every layer is one click, and looking changes nothing");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
let drove = false;

try {
  clearFaults(page);
  await page.goto(`${BASE}/maps?stage=configure`, { waitUntil: "networkidle" });
  await page.waitForSelector(".panel-list .list-row", { timeout: 15000 });

  const row = page.locator(".list-row-pair", { has: page.locator(`text="${slug}"`) }).first();
  const layers = row.locator(".map-layer");
  const hrefs = await layers.evaluateAll(els => els.map(e => e.getAttribute("href")));
  checks.add("the row links plan, sketch and configure",
    hrefs.length === 3 && hrefs.every((h, i) => h === `maps/${slug}/${["plan", "sketch", "configure"][i]}`),
    hrefs.join(" · "));

  const now = await layers.evaluateAll(els =>
    els.filter(e => e.classList.contains("map-layer--now")).map(e => e.textContent.trim()));
  checks.add("the layer the map stands at is marked", now.length === 1 && now[0] === "Configure", now.join(",") || "(none marked)");

  // The old surface: one backward hop with a hard-coded preference order. Nothing should offer that now.
  const reopen = await page.locator('button:has-text("Reopen")').count();
  checks.add("no reopen control remains", reopen === 0, `${reopen} found`);

  // Straight to the plan from the Configuring list — one click, no intermediate list.
  await row.locator('.map-layer:has-text("Plan")').click();
  await page.waitForURL(`**/maps/${slug}/plan`, { timeout: 15000 });
  await page.waitForSelector(".map-canvas-svg", { timeout: 20000 });
  checks.add("the plan editor opens on the built map", page.url().endsWith(`/maps/${slug}/plan`), page.url());
  checks.add("opening it raised no faults", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

  // The claim that makes it safe: the stage pointer is progress, not a cursor. Looking at the plan of a
  // configured map leaves it configured, so nothing downstream is rebuilt or re-listed by a visit.
  const after = find(await api("/maps?stage=configure"));
  checks.add("the map is still where it was", after != null && after.stage === "configure",
    after ? `stage=${after.stage}` : "no longer listed under Configuring");
  drove = true;
} catch (e) {
  page.faults.push(`layers: ${String(e).split("\n")[0]}`);
}
checks.add("the layer checks ran", drove, page.faults.slice(0, 3).join(" | "));

// ── the build says which of the two things it is about to do ──────────────────────────────────────────
checks.section("a rebuild asks; a first build does not");

let asked = false;
try {
  // Standing in the plan of the already-built map from above: the same button now means "replace the board
  // someone has been working on", so it states the trade instead of just doing it.
  await page.click('button:has-text("Compile")');
  await page.waitForSelector(".plan-compile-json", { timeout: 20000 });

  const label = (await page.locator(".plan-compile-draft button").first().textContent()).trim();
  checks.add("the button names a rebuild, not a build", label.includes("Rebuild this map"), label);

  await page.click('button:has-text("Rebuild this map")');
  await page.waitForSelector(".plan-rebuild-warn", { timeout: 5000 });
  const warn = await page.locator(".plan-rebuild-warn").textContent();
  checks.add("it names what is replaced", /Replaces/.test(warn) && /terrain/.test(warn), "board + structure");
  checks.add("…and what is kept", /Keeps/.test(warn) && /themes/.test(warn) && /authors/.test(warn),
    "themes, room shells, dressing, authors");

  // Cancelling is a real exit, not a shrug: the map is untouched and the button is back.
  await page.click('button:has-text("Cancel")');
  await page.waitForSelector(".plan-rebuild-warn", { state: "detached", timeout: 5000 });
  const untouched = find(await api("/maps?stage=configure"));
  checks.add("cancelling rebuilds nothing", untouched != null && untouched.stage === "configure",
    "the map is where it was");

  // A plan that was never built has nothing to lose, so it is not interrupted.
  await page.goto(`${BASE}/maps/${seed.planSlug}/plan`, { waitUntil: "networkidle" });
  await page.waitForSelector(".map-canvas-svg", { timeout: 15000 });
  // The svg exists before the plan document has been fetched into it, and compiling an empty plan is a 422
  // refusal by design — so settle before asking, or the drawer this step reads never opens.
  await page.waitForTimeout(1500);
  await page.click('button:has-text("Compile")');
  await page.waitForSelector(".plan-compile-json", { timeout: 20000 });
  const first = (await page.locator(".plan-compile-draft button").first().textContent()).trim();
  checks.add("a first build is offered plainly", first.includes("Build the map"), first);
  asked = true;
} catch (e) {
  page.faults.push(`confirm: ${String(e).split("\n")[0]}`);
}
checks.add("the confirmation checks ran", asked, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
