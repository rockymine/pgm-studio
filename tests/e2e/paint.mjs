/**
 * The painted world surface: the plan editor's world layers really draw, and a zoom really re-draws them.
 *
 * The smoke sweep can only see the absence of an error, and a blank canvas raises none — it is exactly as
 * "clean" as a working one. That gap matters more for a painted surface than a retained one, because there
 * are no elements left behind to inspect: if the paint code stopped running, nothing in the DOM would say
 * so. So this asserts positively, on pixels.
 *
 * The zoom check is the one that speaks to why the surface is painted at all. A transformed SVG keeps the
 * rasterization it was painted at and lets the browser stretch it, which is what leaves a zoomed picture
 * soft in some engines; a painter re-draws the geometry at the new scale, so the pixels must actually
 * differ after a wheel burst. Identical pixels would mean the surface was being scaled rather than redrawn
 * — the very thing the change exists to stop.
 */

import { openBrowser, newPage, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("paint");
const browser = await openBrowser();
const page = await newPage(browser);

/** Painted pixels, distinct colours, and an order-sensitive signature of the surface's content. */
async function survey() {
  return page.evaluate(() => {
    const canvas = document.querySelector("canvas.world-canvas-2d");
    if (!canvas) return null;
    const { width, height } = canvas;
    const data = canvas.getContext("2d").getImageData(0, 0, width, height).data;
    const colors = new Set();
    let painted = 0, signature = 0;
    for (let i = 0; i < data.length; i += 4) {
      if (data[i + 3] === 0) continue;
      painted++;
      signature = (signature + data[i] * 3 + data[i + 1] * 5 + data[i + 2] * 7 + i) % 1_000_000_007;
      if (colors.size < 64) colors.add(`${data[i]},${data[i + 1]},${data[i + 2]}`);
    }
    const svg = document.querySelector("svg.map-canvas-svg");
    return {
      painted, signature, colors: colors.size,
      bufferW: width, bufferH: height,
      cssW: canvas.clientWidth, cssH: canvas.clientHeight,
      dpr: window.devicePixelRatio || 1,
      svgLayers: svg ? [...svg.querySelectorAll("[data-layer]")].map(g => g.getAttribute("data-layer")) : [],
    };
  });
}

await page.goto(`${BASE}/maps/${seed.planSlug}/plan`, { waitUntil: "networkidle" });
await page.waitForSelector("canvas.world-canvas-2d", { timeout: 20000 });
await page.waitForTimeout(1200);   // the first paint lands after the document arrives over interop

const before = await survey();
checks.add("the world surface exists", before !== null, before === null ? "no canvas.world-canvas-2d" : "found");

if (before) {
  // A board covers a good part of the viewport: grid, working area, pieces, zones. A blank or
  // nearly-blank surface is the failure this exists to catch, so the bar is deliberately well clear of it.
  const coverage = (before.painted / (before.bufferW * before.bufferH)) * 100;
  checks.add("the surface is painted, not blank", coverage > 5, `${coverage.toFixed(1)}% of pixels`);
  checks.add("it draws more than one thing", before.colors > 3, `${before.colors} distinct colours`);

  // The buffer must be the CSS box times the pixel ratio — the whole of why painted text and hairlines
  // are sharp. Headless runs at 1, so this catches a buffer that was never sized at all.
  checks.add("the backing store matches the box × DPR",
    before.bufferW === Math.round(before.cssW * Math.min(before.dpr, 2))
    && before.bufferH === Math.round(before.cssH * Math.min(before.dpr, 2)),
    `${before.bufferW}x${before.bufferH} buffer for ${before.cssW}x${before.cssH} @ ${before.dpr}`);

  // The split: world layers painted, screen-space chrome still in the svg where DOM semantics are useful.
  checks.add("screen chrome stays in the svg",
    before.svgLayers.includes("overlay") && before.svgLayers.includes("scale"),
    before.svgLayers.join(", ") || "none");

  const box = await page.locator("svg.map-canvas-svg").boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  for (let i = 0; i < 6; i++) await page.mouse.wheel(0, -120);
  await page.waitForTimeout(600);
  const after = await survey();

  checks.add("a zoom re-draws the surface", after && after.signature !== before.signature,
    `${before.painted} px → ${after?.painted} px`);
}

checks.add("the page raised nothing", page.faults.length === 0, page.faults.join(" · ") || "clean");

checks.finish();
await browser.close();
