/**
 * The sketch's 3-D preview zooms and pans the world it already built (docs/tools/sketch.md, *the isometric
 * preview*).
 *
 * What is held: entering the preview shows the whole board at 100%; the wheel zooms it and the readout says by
 * how much; a drag moves the picture and keeps the zoom; a double-click shows the whole board again; none of it
 * asks for a second build; and the 2-D board under the preview is left at the zoom it had, so going back to 2-D
 * finds it where it was.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("iso preview");
const browser = await openBrowser();
const page = await newPage(browser, { width: 1440, height: 900 });

const builds = [];
page.on("request", (request) => { if (/\/sketch\/columns/.test(request.url())) builds.push(request.url()); });

const zoomText = () => page.evaluate(() =>
  [...document.querySelectorAll(".canvas-readout-item")].map(el => el.textContent.trim()).find(text => /%$/.test(text)) ?? "");
const percent = (text) => parseInt(text, 10);
const picture = () => page.locator(".svg-area").screenshot();

try {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".canvas-dock", { timeout: 30000 });
  await page.waitForTimeout(1000);
  const flat = await zoomText();

  checks.section("the preview opens on the whole board");
  await page.click(".canvas-mode-toggle");
  await page.waitForLoadState("networkidle");
  await page.waitForFunction(() => {
    const busy = document.querySelector(".canvas-iso-busy");
    return !!document.querySelector(".svg-area canvas") && (!busy || busy.style.display === "none");
  }, null, { timeout: 30000 });
  await page.waitForTimeout(300);
  checks.add("the readout says 100%", (await zoomText()) === "100%", await zoomText());
  const built = builds.length;

  checks.section("the wheel zooms the picture where the pointer is");
  const box = await page.locator(".svg-area").boundingBox();
  const whole = await picture();
  await page.mouse.move(box.x + box.width * 0.6, box.y + box.height * 0.45);
  for (let notch = 0; notch < 6; notch++) { await page.mouse.wheel(0, -100); await page.waitForTimeout(50); }
  await page.waitForTimeout(300);
  const zoomed = await zoomText();
  checks.add("the readout climbs past 100%", percent(zoomed) > 100, zoomed);
  const close = await picture();
  checks.add("and the picture is drawn closer", !close.equals(whole));

  checks.section("a drag moves the picture and keeps the zoom");
  await page.mouse.down();
  await page.mouse.move(box.x + box.width * 0.4, box.y + box.height * 0.55, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  checks.add("the picture moved", !(await picture()).equals(close));
  checks.add("the zoom is as it was", (await zoomText()) === zoomed, await zoomText());

  checks.section("a double-click shows the whole board again");
  await page.mouse.dblclick(box.x + box.width / 2, box.y + box.height / 2);
  await page.waitForTimeout(300);
  checks.add("the readout is back at 100%", (await zoomText()) === "100%", await zoomText());
  checks.add("and nothing was built again", builds.length === built, `${built} build(s) on entering, ${builds.length} after`);

  checks.section("the 2-D board was left where it was");
  await page.click(".canvas-mode-toggle");
  await page.waitForTimeout(500);
  checks.add("going back to 2-D finds its own zoom", (await zoomText()) === flat, `${flat} before, ${await zoomText()} after`);
} catch (e) {
  page.faults.push(`iso preview: ${String(e).split("\n")[0]}`);
}
checks.add("and the checks raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
