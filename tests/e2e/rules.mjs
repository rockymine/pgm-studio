/**
 * A rule id leads somewhere, and a check list reads by rule.
 *
 * The rules page answers `/rules?rule=ID` by opening on that rule, and its filters narrow the list; the studio
 * bar links it and the API docs. The plan editor's Checks panel lists an evaluation as one row per rule, whose
 * id links to that page in a new tab and whose places can be pressed one at a time.
 */

import { openBrowser, newPage, Checks, api, apiRaw, BASE } from "./lib/harness.mjs";

const checks = new Checks("rules");
const browser = await openBrowser();
const page = await newPage(browser);

checks.section("the rules page opens on the rule it is asked for");
await page.goto(`${BASE}/rules?rule=PL9`, { waitUntil: "networkidle", timeout: 30000 });
const selected = page.locator('.rules-row[aria-selected="true"]');
await selected.waitFor({ timeout: 20000 });
checks.add("PL9 is the selected row", ((await selected.locator(".rules-row-id").textContent()) ?? "").trim() === "PL9");
const title = ((await page.locator(".rules-d-title b").textContent()) ?? "").trim();
checks.add("and the rule shown", title === "PL9", title);
const action = ((await page.locator(".rules-d-act b").textContent()) ?? "").trim();
checks.add("its category reads as the action it asks for", action === "Make it playable", action);

const all = await page.locator(".rules-row").count();
const served = (await api("/rules")).length;
checks.add("every rule the server answers is listed", all === served, `${all} of ${served}`);

checks.section("the filters narrow the list");
await page.locator(".rules-seg button", { hasText: "Layout" }).click();
const layoutWords = await page.locator(".rules-row-act").allTextContents();
checks.add("Layout shows only layout rules", layoutWords.length > 0 && layoutWords.every((word) => word === "Layout rule"),
  [...new Set(layoutWords)].join(", "));
await page.locator(".rules-seg button", { hasText: "All" }).click();
await page.fill('.rules-rail input[type="search"]', "WL19");
const count = ((await page.locator(".rules-count").textContent()) ?? "").trim();
checks.add("a search says how many of all it shows", count.startsWith("1 of"), count);

checks.section("the studio bar links the reference pages");
const rulesLink = page.locator(".app-nav-link", { hasText: "Rules" });
checks.add("Rules is lit on its own page", ((await rulesLink.getAttribute("class")) ?? "").includes("app-nav-link--active"));
const docs = page.locator(".app-nav-link", { hasText: "API docs" });
checks.add("API docs opens /api-docs in a new tab",
  (await docs.getAttribute("href")) === "/api-docs" && (await docs.getAttribute("target")) === "_blank");

checks.section("the plan's Checks list one row per rule");
// A spawn and a wool two cells apart: WL2's floor fires, a hard term with both pieces as its subjects.
const plan = {
  plan: 2,
  globals: { cell: 5, symmetry: "none" },
  pieces: [{ id: "spawn", role: "spawn", rect: [0, 0, 2, 2] }, { id: "wool", role: "wool-room", rect: [2, 0, 2, 2] }],
  placements: { spawns: [{ piece: "spawn", at: [5, 5], facing: "front" }], wools: [{ piece: "wool", at: [5, 5] }], iron: [] },
};
const scored = await apiRaw("/plan/evaluate", { method: "POST", body: plan });
checks.add("the fixture fires WL2", (scored.json?.violations ?? []).some((v) => v.finding.rule === "WL2"));

const slug = (await api("/plan", { method: "POST", body: { name: "e2e rules" } })).slug;
await api(`/map/${slug}/plan`, { method: "PUT", body: plan });
await page.goto(`${BASE}/maps/${slug}/plan`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector("canvas.world-canvas-2d", { timeout: 20000 });
const checksTab = page.locator(".panel-tabs button", { hasText: "Checks" });
if (await checksTab.count()) await checksTab.click();

const row = page.locator(".problem-row", { has: page.locator(".problem-id", { hasText: "WL2" }) }).first();
await row.waitFor({ timeout: 20000 });
const link = row.locator(".problem-id");
checks.add("the rule id links to its page in a new tab",
  (await link.getAttribute("href")) === "/rules?rule=WL2" && (await link.getAttribute("target")) === "_blank");
await row.locator("summary").click({ position: { x: 4, y: 8 } });
checks.add("opening the row shows where it fired", await row.locator(".problem-place").count() > 0);
const place = row.locator(".problem-place").first();
await place.click();
checks.add("a place can be pressed on its own", (await place.getAttribute("aria-pressed")) === "true");
const badge = ((await page.locator(".panel-section .badge").first().textContent()) ?? "").trim();
checks.add("the badge counts problems", Number(badge) > 0, badge);

await api(`/map/${slug}`, { method: "DELETE" });
checks.add("rules is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
