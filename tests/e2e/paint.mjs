/**
 * The painted world surfaces: each authoring canvas's world layers really draw, and a zoom really
 * re-draws them.
 *
 * The smoke sweep can only see the absence of an error, and a blank canvas raises none — it is exactly as
 * "clean" as a working one. That gap matters more for a painted surface than a retained one, because there
 * are no elements left behind to inspect: if the paint code stopped running, nothing in the DOM would say
 * so. So this asserts positively, on pixels, for all three drawing surfaces.
 *
 * The zoom check is the one that speaks to why the surfaces are painted at all. A transformed SVG keeps the
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

// The three surfaces, with what each is expected to keep in the svg once its world is painted. `floor` is
// the share of the viewport the world must cover: well clear of blank, and set per surface because a plan
// board fills more of its frame than a world map fills its bounding box. `enter` is for a surface whose
// route does not land on it — the Edit tool opens on Identity, which mounts no canvas, so the world one is
// reached through its nav rail (a state switch, so nothing is advanced or persisted to get there).
const SURFACES = [
  { name: "plan",   path: `/maps/${seed.planSlug}/plan`,     floor: 5, screen: ["overlay", "scale"] },
  { name: "sketch", path: `/maps/${seed.sketchSlug}/sketch`, floor: 5, screen: ["islandChrome", "handles", "center", "scale"] },
  { name: "world",  path: `/maps/${seed.mapSlug}/edit`,      floor: 1, screen: ["overlay"],
    enter: (page) => page.click('button.nav-btn[title="Setup"]') },
];

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

for (const surface of SURFACES) {
  await page.goto(`${BASE}${surface.path}`, { waitUntil: "networkidle", timeout: 30000 });
  if (surface.enter) await surface.enter(page).catch(() => null);
  const mounted = await page.waitForSelector("canvas.world-canvas-2d", { timeout: 20000 }).catch(() => null);
  checks.add(`${surface.name}: the world surface exists`, mounted !== null, mounted ? "found" : "no canvas.world-canvas-2d");
  if (!mounted) continue;
  await page.waitForTimeout(1200);   // the first paint lands after the document arrives over interop

  const before = await survey();

  // A blank or nearly-blank surface is the failure this exists to catch, so the bar is deliberately well
  // clear of it — and above it lies whatever the surface actually draws.
  const coverage = (before.painted / (before.bufferW * before.bufferH)) * 100;
  checks.add(`${surface.name}: the surface is painted, not blank`, coverage > surface.floor,
    `${coverage.toFixed(1)}% of pixels (floor ${surface.floor}%)`);
  checks.add(`${surface.name}: it draws more than one thing`, before.colors > 3, `${before.colors} distinct colours`);

  // The buffer must be the CSS box times the pixel ratio — the whole of why painted text and hairlines
  // are sharp. Headless runs at 1, so this catches a buffer that was never sized at all.
  checks.add(`${surface.name}: the backing store matches the box × DPR`,
    before.bufferW === Math.round(before.cssW * Math.min(before.dpr, 2))
    && before.bufferH === Math.round(before.cssH * Math.min(before.dpr, 2)),
    `${before.bufferW}x${before.bufferH} buffer for ${before.cssW}x${before.cssH} @ ${before.dpr}`);

  // The split: world layers painted, screen-space chrome still in the svg where DOM semantics are useful.
  const missing = surface.screen.filter(name => !before.svgLayers.includes(name));
  checks.add(`${surface.name}: screen chrome stays in the svg`, missing.length === 0,
    missing.length ? `missing ${missing.join(", ")}` : before.svgLayers.join(", "));

  const box = await page.locator("svg.map-canvas-svg").boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  for (let i = 0; i < 6; i++) await page.mouse.wheel(0, -120);
  await page.waitForTimeout(600);
  const after = await survey();

  checks.add(`${surface.name}: a zoom re-draws the surface`, after && after.signature !== before.signature,
    `${before.painted} px → ${after?.painted} px`);
}

checks.add("the pages raised nothing", page.faults.length === 0, page.faults.join(" · ") || "clean");

checks.finish();
await browser.close();
