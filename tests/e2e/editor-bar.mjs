/**
 * The editor bar the Plan, Sketch and Configure tools share: Maps › the map's name, a stage switcher
 * naming the tool, the phase and its steps, and at the right the state, the commands, a divider and
 * Back/Next.
 *
 * What is held here is the shape of the bar rather than any tool's behaviour: the first crumb goes to the
 * unfiltered Maps list; the switcher lists the map's tools and links only the ones the map holds; its label
 * sits on the same baseline, in the same font, as the crumb's words; Sketch's Download is an icon in the
 * group Undo and Redo are in, with the divider between that group and Back; the steps become a menu on a
 * narrow window; and nothing overflows sideways at phone width.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("editor bar");
const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

const TOOLS = [
  ["sketch", `/maps/${seed.mapSlug}/sketch`],
  ["configure", `/maps/${seed.mapSlug}/configure`],
  ["plan", `/maps/${seed.planSlug}/plan`],
];

async function open(path) {
  clearFaults(page);
  await page.goto(`${BASE}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".editor-bar .editor-bar-phase", { timeout: 30000 });
}

/** Where a piece of text sits: the top and bottom of its glyph box, the y of its baseline, and the font it is
 *  set in. The baseline is read off a zero-height inline-block, whose bottom edge rests on it. */
const measure = (selector) => page.evaluate((target) => {
  const el = document.querySelector(target);
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, { acceptNode: n => n.textContent.trim() ? 1 : 3 });
  const text = walker.nextNode();
  const range = document.createRange();
  range.selectNodeContents(text);
  const box = range.getBoundingClientRect();
  const holder = text.parentElement;
  const probe = document.createElement("span");
  probe.style.cssText = "display:inline-block;width:0;height:0;vertical-align:baseline";
  holder.appendChild(probe);
  const baseline = probe.getBoundingClientRect().bottom;
  probe.remove();
  const style = getComputedStyle(holder);
  return {
    top: box.top, bottom: box.bottom, baseline,
    font: [style.fontFamily, style.fontSize, style.fontWeight, style.lineHeight, style.letterSpacing].join("|"),
    text: text.textContent.trim(),
  };
}, selector);

const near = (a, b) => Math.abs(a - b) <= 0.5;

try {
  for (const [tool, path] of TOOLS) {
    checks.section(`${tool}: the crumb and the stage switcher`);
    await open(path);

    checks.add("the old topbar is gone", await page.locator(".topbar").count() === 0);
    const link = page.locator(".editor-crumb a").first();
    checks.add("the first crumb is Maps", (await link.textContent())?.trim() === "Maps");
    checks.add("and goes to the unfiltered list", new URL(await link.evaluate(el => el.href)).pathname === "/maps"
      && new URL(await link.evaluate(el => el.href)).search === "");
    const name = (await page.locator(".editor-crumb-name").textContent())?.trim() ?? "";
    checks.add("the map's name follows, as text", name.length > 0 && await page.locator(".editor-crumb-name a").count() === 0, name);
    checks.add("no phase in the crumb", !(await page.locator(".editor-crumb").textContent())?.includes(
      (await page.locator(".editor-bar-phase").first().textContent())?.trim() ?? "?"));

    const word = { sketch: "Sketch", configure: "Configure", plan: "Plan" }[tool];
    checks.add("the switcher names the tool", (await page.locator(".editor-stage--button span").first().textContent())?.trim() === word);

    // Alignment: the three crumb texts and the switcher's label share one baseline and one font.
    const maps = await measure(".editor-crumb-link");
    const title = await measure(".editor-crumb-name");
    const label = await measure(".editor-crumb .editor-stage--button > span");
    for (const [what, other] of [["the map's name", title], ["Maps", maps]]) {
      checks.add(`the switcher's label shares ${what}'s baseline`, near(label.baseline, other.baseline),
        `${label.baseline} vs ${other.baseline}`);
      checks.add(`and its text top`, near(label.top, other.top), `${label.top} vs ${other.top}`);
      checks.add(`and its text bottom`, near(label.bottom, other.bottom), `${label.bottom} vs ${other.bottom}`);
      checks.add(`and its font`, label.font === other.font, `${label.font} vs ${other.font}`);
    }

    // The menu.
    await page.click(".editor-stage--button");
    await page.waitForSelector(".editor-menu .editor-menu-item", { timeout: 10000 });
    await page.waitForFunction(() => !document.querySelector('.editor-menu [title^="Checking"]'), null, { timeout: 10000 });
    const items = await page.locator(".editor-menu .editor-menu-item").evaluateAll(els => els.map(el => ({
      text: el.textContent.trim(), disabled: el.getAttribute("aria-disabled") === "true",
      current: el.hasAttribute("aria-current"), href: el.getAttribute("href"),
    })));
    checks.add("it lists Plan, Sketch and Configure", items.map(item => item.text).join() === "Plan,Sketch,Configure",
      items.map(item => item.text).join());
    checks.add("the current tool is marked", items.filter(item => item.current).map(item => item.text).join() === word);
    const slug = tool === "plan" ? seed.planSlug : seed.mapSlug;
    for (const item of items.filter(entry => !entry.current && !entry.disabled))
      checks.add(`${item.text} links to this map's tool`, item.href === `maps/${slug}/${item.text.toLowerCase()}`, String(item.href));
    if (tool === "plan") {
      // The seed's plan holds a plan and nothing else.
      checks.add("a layer the map lacks is shown disabled, not linked",
        items.filter(item => item.disabled).map(item => item.text).join() === "Sketch,Configure"
        && items.filter(item => item.disabled).every(item => item.href === null),
        JSON.stringify(items));
    } else {
      checks.add("a map holding all three links the other two", items.every(item => item.current || (!item.disabled && item.href)),
        JSON.stringify(items));
    }
    await page.keyboard.press("Escape").catch(() => {});
    await page.click(".editor-menu-scrim");
  }

  checks.section("a sketch that holds only a sketch leaves the other tools disabled");
  await open(`/maps/${seed.sketchSlug}/sketch`);
  await page.click(".editor-stage--button");
  await page.waitForFunction(() => !document.querySelector('.editor-menu [title^="Checking"]'), null, { timeout: 10000 });
  const draft = await page.locator(".editor-menu .editor-menu-item").evaluateAll(els => els.map(el =>
    `${el.textContent.trim()}:${el.getAttribute("aria-disabled") === "true" ? "off" : "on"}`));
  checks.add("Plan and Configure are off, Sketch is on", draft.join() === "Plan:off,Sketch:on,Configure:off", draft.join());
  await page.click(".editor-menu-scrim");

  checks.section("sketch: undo, redo and download are one group, apart from Back and Next");
  await open(`/maps/${seed.sketchSlug}/sketch`);
  await page.waitForFunction(() => !document.querySelector('.editor-bar-commands [title^="Checking"]'), null, { timeout: 10000 });
  const group = await page.evaluate(() => {
    const bar = document.querySelector(".editor-bar");
    const commands = [...bar.querySelectorAll(".editor-bar-commands .action-btn")];
    const download = commands.at(-1);
    const back = [...bar.querySelectorAll(".editor-bar-move .action-btn")].find(el => el.textContent.trim() === "Back");
    const divider = bar.querySelector(".editor-bar-divider").getBoundingClientRect();
    return {
      titles: commands.map(el => el.getAttribute("title") ?? ""),
      iconOnly: commands.every(el => el.classList.contains("action-btn--icon")),
      downloadText: download.textContent.trim(),
      downloadLabel: download.getAttribute("aria-label") ?? "",
      downloadPrimary: download.classList.contains("action-btn--primary"),
      primaries: [...bar.querySelectorAll(".action-btn--primary")].map(el => el.textContent.trim()),
      gapToBack: back.getBoundingClientRect().left - download.getBoundingClientRect().right,
      gapInside: commands[1].getBoundingClientRect().left - commands[0].getBoundingClientRect().right,
      dividerBetween: divider.left > download.getBoundingClientRect().right && divider.right < back.getBoundingClientRect().left,
      dividerHeight: divider.height,
      order: [...bar.querySelectorAll(".editor-bar-commands, .editor-bar-divider, .editor-bar-move")].map(el => el.className.split(" ")[0]),
    };
  });
  checks.add("the group is Undo, Redo, Download — icons only", group.iconOnly && group.titles.length === 3
    && group.titles[0] === "Undo" && group.titles[1] === "Redo" && group.titles[2].startsWith("Download map"), group.titles.join(" | "));
  checks.add("Download has no text and says what it does", group.downloadText === ""
    && group.downloadLabel === "Download map: its world and map.xml, ready for a server", group.downloadLabel);
  checks.add("Download is not the primary action; Next is", !group.downloadPrimary && group.primaries.join() === "Next", group.primaries.join());
  checks.add("the buttons of the group sit together", group.gapInside < 8, `${group.gapInside}px`);
  checks.add("a divider with room either side separates the group from Back", group.dividerBetween && group.gapToBack >= 24
    && group.dividerHeight >= 20, `${group.gapToBack}px, divider ${group.dividerHeight}px tall`);
  checks.add("the bar runs commands, divider, move", group.order.join() === "editor-bar-commands,editor-bar-divider,editor-bar-move",
    group.order.join());

  checks.section("a plan row has no map to switch within");
  await open("/plans/new");
  checks.add("the stage word is plain text", await page.locator(".editor-stage--button").count() === 0
    && (await page.locator(".editor-crumb .editor-stage").textContent())?.trim() === "Plan");
  const rowLabel = await measure(".editor-crumb .editor-stage > span");
  const rowName = await measure(".editor-crumb-name");
  checks.add("and still sits on the crumb's baseline", near(rowLabel.baseline, rowName.baseline), `${rowLabel.baseline} vs ${rowName.baseline}`);
  checks.add("the first crumb is Maps", (await page.locator(".editor-crumb a").first().textContent())?.trim() === "Maps");

  checks.section("the bar is there in every phase");
  for (const [path, phase] of [[`/maps/${seed.sketchSlug}/sketch?phase=info`, "Info"],
                               [`/maps/${seed.sketchSlug}/sketch`, "History"],
                               [`/maps/${seed.planSlug}/plan?phase=info`, "Info"]]) {
    await open(path);
    if (phase === "History") await page.click('.nav-btn[title="History"]');
    await page.waitForFunction((want) => document.querySelector(".editor-bar-phase")?.textContent.trim() === want, phase,
      { timeout: 10000 }).catch(() => {});
    const bar = await page.evaluate(() => ({
      phase: document.querySelector(".editor-bar-phase")?.textContent.trim(),
      crumb: !!document.querySelector(".editor-crumb a"),
      move: [...document.querySelectorAll(".editor-bar-move .action-btn")].map(el => el.textContent.trim()).join(),
    }));
    checks.add(`${path.split("?")[0].split("/").pop()} ${phase}: the crumb, the phase and Back/Next`,
      bar.crumb && bar.phase === phase && bar.move.startsWith("Back,"), JSON.stringify(bar));
  }

  checks.section("a narrow window folds the steps into a menu");
  await page.setViewportSize({ width: 1000, height: 800 });
  await open(`/maps/${seed.sketchSlug}/sketch?phase=info`);
  const narrow = await page.evaluate(() => {
    const visible = (el) => !!el && getComputedStyle(el).display !== "none";
    return {
      steps: visible(document.querySelector(".flow-steps")),
      compact: visible(document.querySelector(".flow-steps-compact")),
      compactText: document.querySelector(".flow-steps-compact")?.textContent.trim(),
      overflow: document.documentElement.scrollWidth > window.innerWidth,
      barHeight: document.querySelector(".editor-bar").getBoundingClientRect().height,
    };
  });
  checks.add("the step strip is hidden and the menu stands in", !narrow.steps && narrow.compact, JSON.stringify(narrow));
  checks.add("it says where the work is", /^Step \d+ of \d+$/.test(narrow.compactText ?? ""), narrow.compactText);
  checks.add("the bar stays one row", narrow.barHeight < 60, `${narrow.barHeight}px`);
  checks.add("nothing overflows sideways", !narrow.overflow);
  await page.click(".flow-steps-compact .editor-stage--button");
  const stepItems = await page.locator(".flow-steps-compact .editor-menu-item").count();
  checks.add("the menu lists the steps", stepItems >= 2, String(stepItems));
  await page.click(".flow-steps-compact .editor-menu-scrim");

  checks.section("at phone width the bar wraps rather than overflowing");
  await page.setViewportSize({ width: 390, height: 800 });
  for (const [tool, path] of TOOLS) {
    await open(path);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    checks.add(`${tool} fits the width`, overflow <= 0, `${overflow}px over`);
  }
} catch (e) {
  page.faults.push(`editor bar: ${String(e).split("\n")[0]}`);
}
checks.add("and the checks raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
