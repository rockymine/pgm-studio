/**
 * The editor bar the Sketch and Configure tools share: Maps › the map's name, a stage switcher
 * naming the tool, the phase and its steps, and at the right the state, the commands, a divider and
 * Back/Next.
 *
 * What is held here is the shape of the bar rather than any tool's behaviour: the first crumb goes to the
 * unfiltered Maps list; the switcher lists the map's tools and links only the ones the map holds; its label
 * sits on the same baseline, in the same font, as the crumb's words; Sketch's Download is an icon in the
 * group Undo and Redo are in, with the divider between that group and Back; the steps stay written out down to
 * 960px; below it the move is a foot pinned to the window's bottom, the bar pinned to its top, and on a phone the
 * steps are a button opening a sheet of the phases with the open phase's steps under it; and nothing overflows
 * sideways at phone width.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("editor bar");
const browser = await openBrowser();
let page = await newPage(browser, { width: 1600, height: 1000 });
const wide = page;

const TOOLS = [
  ["sketch", `/maps/${seed.mapSlug}/sketch`],
  ["configure", `/maps/${seed.mapSlug}/configure`],
];

async function open(path) {
  clearFaults(page);
  await page.goto(`${BASE}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".editor-bar .editor-crumb-name", { timeout: 30000 });
  await page.waitForTimeout(300);
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

    const word = { sketch: "Sketch", configure: "Configure" }[tool];
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
    checks.add("it lists Sketch and Configure", items.map(item => item.text).join() === "Sketch,Configure",
      items.map(item => item.text).join());
    checks.add("the current tool is marked", items.filter(item => item.current).map(item => item.text).join() === word);
    for (const item of items.filter(entry => !entry.current && !entry.disabled))
      checks.add(`${item.text} links to this map's tool`, item.href === `maps/${seed.mapSlug}/${item.text.toLowerCase()}`, String(item.href));
    checks.add("a map holding both links the other", items.every(item => item.current || (!item.disabled && item.href)),
      JSON.stringify(items));
    await page.keyboard.press("Escape").catch(() => {});
    await page.click(".editor-menu-scrim");
  }

  checks.section("a sketch that holds only a sketch leaves the other tools disabled");
  await open(`/maps/${seed.sketchSlug}/sketch`);
  await page.click(".editor-stage--button");
  await page.waitForFunction(() => !document.querySelector('.editor-menu [title^="Checking"]'), null, { timeout: 10000 });
  const draft = await page.locator(".editor-menu .editor-menu-item").evaluateAll(els => els.map(el =>
    `${el.textContent.trim()}:${el.getAttribute("aria-disabled") === "true" ? "off" : "on"}`));
  checks.add("Configure is off, Sketch is on", draft.join() === "Sketch:on,Configure:off", draft.join());
  await page.click(".editor-menu-scrim");

  checks.section("sketch: undo, redo and download are one group, apart from Back and Next");
  await open(`/maps/${seed.sketchSlug}/sketch`);
  await page.waitForFunction(() => !document.querySelector('.editor-bar-commands [title^="Checking"], .editor-bar-history [title^="Checking"]')
    && document.querySelectorAll(".editor-bar-history .action-btn, .editor-bar-commands .action-btn").length === 3, null, { timeout: 10000 });
  const group = await page.evaluate(() => {
    const bar = document.querySelector(".editor-bar");
    const commands = [...bar.querySelectorAll(".editor-bar-history .action-btn, .editor-bar-commands .action-btn")]
      .sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
    const download = commands.at(-1);
    const back = bar.querySelector(".editor-bar-back");
    const divider = bar.querySelector(".editor-bar-divider").getBoundingClientRect();
    return {
      titles: commands.map(el => el.getAttribute("title") ?? ""),
      iconOnly: commands.every(el => el.classList.contains("action-btn--icon")),
      downloadText: download.textContent.trim(),
      downloadLabel: download.getAttribute("aria-label") ?? "",
      downloadPrimary: download.classList.contains("action-btn--primary"),
      primaries: [...bar.querySelectorAll(".action-btn--primary")].map(el => el.textContent.trim()),
      gapToBack: back.getBoundingClientRect().left - download.getBoundingClientRect().right,
      gapInside: Math.max(...commands.slice(1).map((el, i) => el.getBoundingClientRect().left - commands[i].getBoundingClientRect().right)),
      dividerBetween: divider.left > download.getBoundingClientRect().right && divider.right < back.getBoundingClientRect().left,
      dividerHeight: divider.height,
      order: [...bar.querySelectorAll(".editor-bar-history, .editor-bar-commands, .editor-bar-divider, .editor-bar-back, .editor-bar-next")]
        .sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left).map(el => el.className.split(" ").find(c => c.startsWith("editor-bar-"))),
    };
  });
  checks.add("the group is Undo, Redo, Download — icons only", group.iconOnly && group.titles.length === 3
    && group.titles[0] === "Undo" && group.titles[1] === "Redo" && group.titles[2].startsWith("Download map"), group.titles.join(" | "));
  checks.add("Download has no text and says what it does", group.downloadText === ""
    && group.downloadLabel === "Download map", group.downloadLabel);
  checks.add("Download is not the primary action; Next is, naming where it goes",
    !group.downloadPrimary && group.primaries.length === 1 && /^Next: \w/.test(group.primaries[0]), group.primaries.join());
  checks.add("the buttons of the group sit together", group.gapInside < 8, `${group.gapInside}px`);
  checks.add("a divider with room either side separates the group from Back", group.dividerBetween && group.gapToBack >= 24
    && group.dividerHeight >= 20, `${group.gapToBack}px, divider ${group.dividerHeight}px tall`);
  checks.add("the bar runs undo and redo, the commands, the divider, Back and Next",
    group.order.join() === "editor-bar-history,editor-bar-commands,editor-bar-divider,editor-bar-back,editor-bar-next", group.order.join());

  checks.section("the bar is there in every phase");
  for (const [path, phase] of [[`/maps/${seed.sketchSlug}/sketch?phase=info`, "Info"],
                               [`/maps/${seed.sketchSlug}/sketch`, "History"],
                               [`/maps/${seed.mapSlug}/configure`, "Identity"]]) {
    await open(path);
    if (phase === "History") await page.click('.nav-btn:has(.nav-btn-caption:text-is("History"))');
    await page.waitForFunction((want) => document.querySelector(".editor-bar-flow .editor-bar-phase")?.textContent.trim() === want, phase,
      { timeout: 10000 }).catch(() => {});
    const bar = await page.evaluate(() => ({
      phase: document.querySelector(".editor-bar-flow .editor-bar-phase")?.textContent.trim(),
      crumb: !!document.querySelector(".editor-crumb a"),
      move: [...document.querySelectorAll(".editor-bar-back, .editor-bar-next")].map(el => el.textContent.trim()).join(),
    }));
    checks.add(`${path.split("?")[0].split("/").pop()} ${phase}: the crumb, the phase and Back/Next`,
      bar.crumb && bar.phase === phase && bar.move.startsWith("Back,"), JSON.stringify(bar));
  }

  checks.section("the steps stay written out down to 960px");
  await page.setViewportSize({ width: 1000, height: 800 });
  await open(`/maps/${seed.sketchSlug}/sketch?phase=info`);
  const narrow = await page.evaluate(() => {
    const strip = document.querySelector(".editor-bar-flow .flow-steps");
    return {
      steps: !!strip && getComputedStyle(strip).display !== "none" && strip.getBoundingClientRect().width > 0,
      overflow: document.documentElement.scrollWidth > window.innerWidth,
      barHeight: document.querySelector(".editor-bar").getBoundingClientRect().height,
    };
  });
  checks.add("the step strip is shown", narrow.steps, JSON.stringify(narrow));
  checks.add("the bar stays one row", narrow.barHeight < 60, `${narrow.barHeight}px`);
  checks.add("nothing overflows sideways", !narrow.overflow);

  /** Where the bar, its foot and the rail sit, and what the foot holds. */
  const layout = () => page.evaluate(() => {
    const box = (selector) => {
      const el = document.querySelector(selector);
      if (!el || getComputedStyle(el).display === "none") return null;
      const rect = el.getBoundingClientRect();
      return { top: Math.round(rect.top), bottom: Math.round(rect.bottom), height: Math.round(rect.height), width: Math.round(rect.width) };
    };
    return {
      window: window.innerHeight,
      bar: box(".editor-bar"), foot: box(".editor-bar-foot"), rail: box(".app-body > .nav-rail"),
      picker: box(".editor-bar-picker"), strip: box(".editor-bar-foot-steps"), next: box(".editor-bar-next"),
      history: !!document.querySelector(".editor-bar-foot .editor-bar-history .action-btn"),
    };
  });

  checks.section("on a tablet the move is a foot pinned to the window's bottom, beside a rail column");
  await page.setViewportSize({ width: 768, height: 1024 });
  await open(`/maps/${seed.sketchSlug}/sketch?phase=draw`);
  let at = await layout();
  checks.add("the foot rests on the window's bottom", at.foot?.bottom === at.window, JSON.stringify(at.foot));
  checks.add("it holds Undo and Redo and the steps written out, with Next", at.history && !!at.strip && !at.picker && !!at.next, JSON.stringify(at));
  checks.add("the rail is a column beside the page", !!at.rail && at.rail.width < 80 && at.rail.top >= at.bar.bottom - 1, JSON.stringify(at.rail));
  await page.mouse.wheel(0, 600);
  await page.waitForTimeout(400);
  at = await layout();
  checks.add("scrolled, the bar is still at the top and the foot at the bottom", at.bar.top === 0 && at.foot.bottom === at.window,
    `bar ${at.bar.top}, foot ${at.foot.bottom} of ${at.window}`);

  checks.section("on a phone the steps open a sheet of the phases");
  page = await newPage(browser, { width: 390, height: 844, touch: true });
  await open(`/maps/${seed.sketchSlug}/sketch?phase=draw`);
  const mark = await page.locator(".editor-studio-btn").boundingBox();
  checks.add("the studio's mark is a finger's size", mark.width >= 44 && mark.height >= 44, `${mark.width} × ${mark.height}`);
  at = await layout();
  checks.add("no rail is drawn", at.rail === null, JSON.stringify(at.rail));
  checks.add("the foot's middle is the phase and step", !!at.picker && !at.strip, JSON.stringify(at));
  checks.add("the bar and the foot together take under a sixth of the window",
    at.bar.height + at.foot.height < at.window / 6, `${at.bar.height} + ${at.foot.height} of ${at.window}`);
  const picked = (await page.locator(".editor-bar-picker").textContent())?.replace(/\s+/g, " ").trim();
  checks.add("it names the phase, the step and how far along", /^Draw · \d of \d \w+/.test(picked ?? ""), picked);
  await page.click(".editor-bar-picker");
  await page.waitForSelector(".editor-sheet", { timeout: 5000 });
  const sheet = await page.evaluate(() => {
    const rows = [...document.querySelectorAll(".editor-sheet .nav-btn, .editor-sheet-step")]
      .sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);
    return {
      rows: rows.map(el => (el.classList.contains("editor-sheet-step") ? "  " : "") + el.textContent.replace(/^\d+/, "").trim()),
      focus: document.activeElement?.classList.contains("editor-sheet"),
      short: Math.min(...rows.map(el => el.getBoundingClientRect().height)),
    };
  });
  const drawAt = sheet.rows.indexOf("Draw");
  checks.add("it lists every phase", sheet.rows.filter(row => !row.startsWith(" ")).length === 7, sheet.rows.join(" | "));
  checks.add("with the open phase's steps under it", drawAt >= 0 && sheet.rows[drawAt + 1]?.startsWith("  ")
    && sheet.rows.slice(drawAt + 1).findIndex(row => !row.startsWith(" ")) >= 2, sheet.rows.join(" | "));
  checks.add("it takes focus, and every row is a finger's height", sheet.focus && sheet.short >= 40, `${sheet.short}px`);
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
  checks.add("Escape closes it", await page.locator(".editor-sheet").count() === 0);
  await page.click(".editor-bar-picker");
  await page.locator('.editor-sheet .nav-btn:has(.nav-btn-caption:text-is("Terraform"))').click();
  await page.waitForTimeout(500);
  checks.add("a phase picked in it opens that phase and closes it",
    await page.locator(".editor-sheet").count() === 0
    && /Terraform/.test((await page.locator(".editor-bar-picker").textContent()) ?? ""));

  checks.section("at phone width the bar fits");
  for (const [tool, path] of TOOLS) {
    await open(path);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    checks.add(`${tool} fits the width`, overflow <= 0, `${overflow}px over`);
  }
} catch (e) {
  page.faults.push(`editor bar: ${String(e).split("\n")[0]}`);
}
const faults = page === wide ? wide.faults : [...wide.faults, ...page.faults];
checks.add("and the checks raised nothing", faults.length === 0, faults.slice(0, 3).join(" | "));

await browser.close();
checks.finish();
