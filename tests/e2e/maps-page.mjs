/**
 * The maps page is one table of every map, and its filters narrow it in place.
 *
 * Two claims. Every row says who made the map: the list endpoint carries each map's credited authors, and a
 * credit with no account behind it, which is how an agent is credited, is drawn as a robot. And the filters are
 * the address: a stage chip, a ticked author and Select all each land in the query, narrow the table to what
 * they name, and come back off it with one click.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("maps page");

checks.section("the list carries who made each map and when it last changed");

const all = await api("/maps");
const fixture = all.find(m => m.slug === seed.mapSlug);
checks.add("every row carries its authors and a last-written time",
  all.every(m => Array.isArray(m.authors) && typeof m.updatedAt === "string"), `${all.length} rows`);
checks.add("the seed's finished map is credited by name, with no account",
  fixture?.authors.some(a => a.name === "E2E fixture" && a.uuid === ""), JSON.stringify(fixture?.authors));

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
const rows = () => page.locator(".map-table-row");
const slugs = () => page.locator(".map-table-row .map-row-slug").allTextContents();
let drove = false;

try {
  clearFaults(page);
  await page.goto(`${BASE}/maps`, { waitUntil: "networkidle" });
  await page.waitForSelector(".map-table-row", { timeout: 15000 });

  checks.section("one table, every map");
  checks.add("the table lists every map", await rows().count() === all.length, `${await rows().count()} of ${all.length}`);
  const fixtureRow = rows().filter({ has: page.locator(`text="${seed.mapSlug}"`) }).first();
  checks.add("an agent-style credit shows a robot, not a head",
    await fixtureRow.locator(".agent-mark svg.lucide").count() === 1);

  checks.section("a stage chip narrows the table and lands in the address");
  const sidebar = page.locator(".filter-sidebar");
  await sidebar.locator(".filter-chip", { hasText: "Configure" }).click();
  await page.waitForURL(/stage=configure/, { timeout: 5000 });
  const configuring = all.filter(m => m.stage === "configure" || m.stage === "edit").map(m => m.slug).sort();
  checks.add("only maps standing at configure are listed",
    JSON.stringify((await slugs()).sort()) === JSON.stringify(configuring), (await slugs()).join(", "));
  await sidebar.locator(".filter-chip", { hasText: "All" }).first().click();
  await page.waitForURL(u => !u.search.includes("stage="), { timeout: 5000 });

  checks.section("an author ticks on and off in the sidebar");
  const author = sidebar.locator(".filter-group--authors .filter-chip", { hasText: "E2E fixture" });
  await author.click();
  await page.waitForURL(/author=E2E(%20|\+)fixture/, { timeout: 5000 });
  const credited = all.filter(m => m.authors.some(a => a.name === "E2E fixture")).map(m => m.slug).sort();
  checks.add("only maps credited to that author are listed",
    JSON.stringify((await slugs()).sort()) === JSON.stringify(credited), (await slugs()).join(", "));
  checks.add("the ticked author is marked", await author.evaluate(e => e.classList.contains("filter-chip--active")));
  await author.click();
  await page.waitForURL(u => !u.search.includes("author="), { timeout: 5000 });
  checks.add("unticking it lists every map again", await rows().count() === all.length, page.url());

  checks.section("Select all under Agents picks every agent");
  await sidebar.locator(".filter-group--authors").filter({ has: page.locator(".field-label", { hasText: "Agents" }) }).locator(".filter-group-action").click();
  await page.waitForURL(/author=/, { timeout: 5000 });
  const byAgents = all.filter(m => m.authors.some(a => a.role !== "contributor" && a.uuid === "")).map(m => m.slug).sort();
  checks.add("it lists the maps credited by name alone",
    JSON.stringify((await slugs()).sort()) === JSON.stringify(byAgents), (await slugs()).join(", "));

  checks.add("the page raised no faults", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
  drove = true;
} catch (e) {
  page.faults.push(`maps page: ${String(e).split("\n")[0]}`);
}
checks.add("the page checks ran", drove, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
