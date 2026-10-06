/**
 * The glossary page lists every term the server defines, filters by what is typed, carries the filter in the
 * address, and deep-links a term by its anchor. The studio bar reads Maps, Generator, Library, Rules, Glossary
 * and the shortcuts and design icons sit at its right.
 */

import { openBrowser, newPage, clearFaults, Checks, api, BASE } from "./lib/harness.mjs";

const checks = new Checks("glossary");
const browser = await openBrowser();
const page = await newPage(browser);

checks.section("the page lists every term");
clearFaults(page);
await page.goto(`${BASE}/glossary`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".glossary-term", { timeout: 20000 });
const served = (await api("/glossary")).length;
const listed = await page.locator(".glossary-term").count();
checks.add("every term the server answers is listed", listed === served, `${listed} of ${served}`);

checks.section("a search filters by word and definition, and lives in the address");
await page.fill('input[type="search"]', "hub");
await page.waitForFunction(() => document.querySelectorAll(".glossary-term").length < 20, null, { timeout: 10000 });
const filtered = await page.locator(".glossary-term").count();
checks.add("typing narrows the list", filtered > 0 && filtered < listed, `${filtered} of ${listed}`);
checks.add("and the address carries ?q=", new URL(page.url()).searchParams.get("q") === "hub", page.url());
checks.add("the hub entry is among them", (await page.locator("#hub").count()) === 1);
await page.fill('input[type="search"]', "zzzz-no-such-word");
await page.waitForSelector(".rules-empty", { timeout: 10000 });
checks.add("a word nothing matches says so", (await page.locator(".glossary-term").count()) === 0);

checks.section("an address with ?q= opens filtered");
await page.goto(`${BASE}/glossary?q=hub`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".glossary-term", { timeout: 20000 });
checks.add("the search box holds it", (await page.inputValue('input[type="search"]')) === "hub");
checks.add("and the list is narrowed", (await page.locator(".glossary-term").count()) < listed);

checks.section("an anchor opens on its term");
await page.goto(`${BASE}/glossary#hub`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector("#hub.glossary-term--target", { timeout: 20000 });
checks.add("the entry is marked", true);

checks.section("the studio bar");
const links = (await page.locator(".app-nav-link").allTextContents()).map((text) => text.trim());
checks.add("it reads Maps, Generator, Library, Rules, Glossary", links.join(", ") === "Maps, Generator, Library, Rules, Glossary", links.join(", "));
checks.add("Glossary is lit on its own page",
  ((await page.locator(".app-nav-link", { hasText: "Glossary" }).getAttribute("class")) ?? "").includes("app-nav-link--active"));
checks.add("no divider stands in it", (await page.locator(".app-nav-divider").count()) === 0);
for (const label of ["Keyboard shortcuts", "Design"]) {
  checks.add(`${label} is an icon at the right`, (await page.locator(`.app-nav-right [aria-label^="${label}"]`).count()) === 1);
}
checks.add("the footer no longer carries them", (await page.locator(".app-footer-link", { hasText: /Keyboard|Design/ }).count()) === 0);
await page.click('.app-nav-right [aria-label="Keyboard shortcuts"]');
await page.waitForFunction(() => /Keyboard|shortcut/i.test(document.body.textContent), null, { timeout: 5000 });

checks.add("the glossary is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
