/**
 * A rule id leads somewhere, and a check list reads by rule.
 *
 * The rules page answers `/rules?rule=ID` by opening on that rule, and its filters narrow the list; the studio
 * bar links it and the account menu links the API docs for an admin. A check list reads one row per rule, whose id links to
 * that page in a new tab.
 */

import { openBrowser, newPage, Checks, api, BASE } from "./lib/harness.mjs";

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
checks.add("the bar reads Maps, Generator, Library, Rules, Glossary",
  (await page.locator(".app-nav-link").allTextContents()).map((text) => text.trim()).join(", ")
    === "Maps, Generator, Library, Rules, Glossary");
await page.click(".app-nav .account-local, .app-nav .account-button");
const docs = page.locator('.account-menu a[href="/api-docs"]');
checks.add("API docs, in the account menu, opens /api-docs in a new tab",
  (await docs.getAttribute("href")) === "/api-docs" && (await docs.getAttribute("target")) === "_blank");

checks.section("a check list reads one row per rule");
// The design page's ProblemList is the component every check list draws, fed from the live rules.
await page.goto(`${BASE}/design`, { waitUntil: "networkidle", timeout: 30000 });
const row = page.locator(".ds-problems-sample .problem-row").first();
await row.waitFor({ timeout: 20000 });
const link = row.locator(".problem-id");
const rule = ((await link.textContent()) ?? "").trim();
checks.add("the rule id links to its page in a new tab",
  (await link.getAttribute("href")) === `/rules?rule=${rule}` && (await link.getAttribute("target")) === "_blank", rule);
checks.add("the worst row opens first", (await row.getAttribute("open")) !== null);

checks.add("rules is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
