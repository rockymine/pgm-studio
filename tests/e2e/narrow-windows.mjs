/**
 * The studio at a phone's and a tablet's width (docs/client/ui-conventions.md, *A narrow window*).
 *
 * What is held: no page lets a control or a column run past the window's right edge, unless it sits in a box that
 * scrolls sideways; the studio bar folds its links into a menu that opens, takes focus, closes on Escape, hands
 * focus back and closes on a link; an editor draws no studio bar, and the mark in its own bar opens the same menu,
 * which then carries the search and the account; and a filter rail folds behind its button above the list.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("narrow windows");
const browser = await openBrowser();
const page = await newPage(browser, { width: 390, height: 844 });

const PAGES = ["/", "/maps", "/generator", "/library", "/rules", "/glossary",
  `/maps/${seed.sketchSlug}/sketch`, `/maps/${seed.mapSlug}/configure`];

async function open(path) {
  clearFaults(page);
  await page.goto(`${BASE}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".editor-page", { timeout: 30000 });
  await page.waitForTimeout(400);
}

/** The first few visible elements whose right edge passes the window's. A box that scrolls sideways is allowed to
 *  hold one, and so is a drawing; a box that only clips it is not, since what it clips is out of reach. */
const strays = () => page.evaluate(() => {
  const width = document.documentElement.clientWidth;
  const found = [];
  for (const el of document.querySelectorAll(".editor-page *")) {
    if (el.closest("svg")) continue;
    const style = getComputedStyle(el);
    if (style.visibility === "hidden" || style.position === "fixed") continue;
    const box = el.getBoundingClientRect();
    if (box.width === 0 || box.height === 0 || box.right <= width + 1) continue;
    let scrolls = false;
    for (let up = el.parentElement; up && !up.classList.contains("editor-page"); up = up.parentElement) {
      if (/auto|scroll/.test(getComputedStyle(up).overflowX) && up.getBoundingClientRect().right <= width + 1) { scrolls = true; break; }
    }
    if (!scrolls) found.push(`${el.tagName.toLowerCase()}.${[...el.classList].join(".")} ends at ${Math.round(box.right)}px`);
  }
  return found.slice(0, 3);
});

const visible = (selector) => page.locator(selector).first().isVisible();

try {
  for (const [label, width, height] of [["a phone", 390, 844], ["a tablet", 768, 1024]]) {
    checks.section(`nothing runs past the edge of ${label}`);
    await page.setViewportSize({ width, height });
    for (const path of PAGES) {
      await open(path);
      const out = await strays();
      checks.add(`${path.split("?")[0]} fits ${width}px`, out.length === 0, out.join(" | "));
    }
  }

  checks.section("the studio bar folds into a menu");
  await page.setViewportSize({ width: 390, height: 844 });
  await open("/maps");
  checks.add("the links are not in the bar", !(await visible(".app-nav-link")));
  await page.click(".app-nav-menu");
  await page.waitForTimeout(300);
  const opened = await page.evaluate(() => ({
    links: [...document.querySelectorAll(".app-nav-panel .app-nav-link")].map(a => a.textContent.trim()).join(", "),
    focus: document.activeElement?.id,
    expanded: document.querySelector(".app-nav-menu")?.getAttribute("aria-expanded"),
  }));
  checks.add("the menu lists the studio's pages", opened.links === "Maps, Generator, Library, Rules, Glossary", opened.links);
  checks.add("it takes focus and says it is open", opened.focus === "app-nav-panel" && opened.expanded === "true", JSON.stringify(opened));
  checks.add("it holds the four tools", (await page.locator(".app-nav-panel .app-nav-tools .topbar-icon-btn").count()) >= 4);
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
  checks.add("Escape closes it", !(await visible(".app-nav-panel .app-nav-link")));
  checks.add("and focus goes back to its button",
    await page.evaluate(() => document.activeElement?.classList.contains("app-nav-menu")));
  await page.click(".app-nav-menu");
  await page.locator(".app-nav-panel .app-nav-link", { hasText: "Generator" }).click();
  await page.waitForURL(/\/generator/, { timeout: 10000 });
  await page.waitForTimeout(300);
  checks.add("a link closes it", !(await visible(".app-nav-panel .app-nav-link")));

  checks.section("a filter rail folds above the list");
  await open("/maps");
  checks.add("its filters start folded", !(await visible(".filter-sidebar .filter-chip")));
  await page.click(".filter-sidebar-toggle");
  checks.add("its button opens them", await visible(".filter-sidebar .filter-chip"));

  checks.section("an editor opens the menu from its own bar");
  await page.setViewportSize({ width: 1600, height: 900 });
  await open(`/maps/${seed.sketchSlug}/sketch`);
  await page.waitForSelector(".canvas-dock", { timeout: 20000 });
  await page.waitForTimeout(1000);
  checks.add("no studio bar stands over the editor", (await page.locator(".app-nav").count()) === 0);
  await page.click(".editor-studio-btn");
  // The tool rewrites its own address as it settles; the menu is the reader's until they close it.
  await page.waitForTimeout(2000);
  checks.add("the mark opens the menu, and it stays open while the tool writes its address",
    await visible(".app-nav-panel .app-nav-link"));
  checks.add("which carries the search and the account",
    (await visible(".app-nav-panel .app-nav-search")) && (await page.locator(".app-nav-panel .account").count()) >= 1);
  await page.click(".app-nav-scrim");
  await page.waitForTimeout(300);
  checks.add("the dimmed page closes it", !(await visible(".app-nav-panel .app-nav-link")));
} catch (e) {
  page.faults.push(`narrow windows: ${String(e).split("\n")[0]}`);
}
checks.add("and the checks raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
