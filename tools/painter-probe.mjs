/**
 * Measure what a 2-D canvas actually does with this repo's drawing vocabulary, so the questions a
 * canvas painter raises are answered by pixels rather than by argument.
 *
 *   node tools/painter-probe.mjs
 *
 * Four questions, each one a decision the painter depends on:
 *
 *   1. Does a 2-D context accept the canvas colour tokens — including `--canvas-chunk`, whose value is
 *      a `color-mix(in oklab, …)` that `getComputedStyle` hands back unevaluated? If not, the painter
 *      would have to evaluate colour syntax itself.
 *   2. Does assigning a literal `var(--x)` to `strokeStyle` fail, and fail *silently*? That is the trap
 *      the token resolver exists to prevent, and it should stay demonstrated rather than asserted.
 *   3. Does a 1px dashed grid still read when zoomed far out, drawn with the width and dash divided by
 *      the viewport scale — the by-hand form of a non-scaling stroke?
 *   4. Is canvas text legible once the backing store is scaled by the device pixel ratio?
 *
 * It drives whatever browsers are installed. The artifact this whole line of work is about is
 * browser-specific and absent from Chromium, so a second engine is worth having here: pass
 * `--browser=firefox` once a Firefox build exists locally.
 */

import { openBrowser } from "../tests/e2e/lib/harness.mjs";

// The tokens under test, lifted from css/studio/tokens.css. --canvas-chunk is the interesting one.
const TOKEN_CSS = `
  :root {
    --canvas-axis: #a78bfa;
    --canvas-chunk: color-mix(in oklab, var(--canvas-axis) 38%, transparent);
    --canvas-ink: #ffffff;
    --bg-canvas: #080f1a;
  }`;

const LOW_ZOOM = 0.08;   // cells a few pixels apart — where a dashed grid would smear if it were going to

async function probeAt(browser, dpr) {
  const context = await browser.newContext({ deviceScaleFactor: dpr });
  const page = await context.newPage();
  await page.setContent(`<html><head><style>${TOKEN_CSS}</style></head>`
    + `<body><canvas id="c" width="400" height="200"></canvas></body></html>`);
  const result = await page.evaluate(({ dpr, lowZoom }) => {
    const out = { dpr };
    const root = getComputedStyle(document.documentElement);
    out.rawChunk = root.getPropertyValue("--canvas-chunk").trim();
    const axis = root.getPropertyValue("--canvas-axis").trim();

    const canvas = document.getElementById("c");
    const ctx = canvas.getContext("2d");

    // A rejected colour leaves strokeStyle at its previous value — which is exactly why a missed token
    // paints in the last colour used instead of raising.
    const accepts = (value) => {
      ctx.strokeStyle = "#000000";
      ctx.strokeStyle = value;
      return { accepted: ctx.strokeStyle !== "#000000", normalized: String(ctx.strokeStyle) };
    };
    out.colorMix   = accepts(out.rawChunk);
    out.hex        = accepts(axis);
    out.varLiteral = accepts("var(--canvas-axis)");

    canvas.width = 400 * dpr; canvas.height = 200 * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = "#080f1a";
    ctx.fillRect(0, 0, 400, 200);

    // The dashed cell grid, drawn in world units with the screen width divided back out.
    ctx.save();
    ctx.scale(lowZoom, lowZoom);
    ctx.strokeStyle = out.rawChunk;
    ctx.lineWidth = 1 / lowZoom;
    ctx.setLineDash([3 / lowZoom, 3 / lowZoom]);
    ctx.beginPath();
    for (let i = 0; i <= 20; i++) {
      const x = (i * 5 / lowZoom) * 0.5;
      ctx.moveTo(x, 0); ctx.lineTo(x, 100 / lowZoom);
    }
    ctx.stroke();
    ctx.restore();

    // A grid that reads leaves separated runs of lit pixels on a scanline: one run per line, not a smear
    // (too many runs, no gaps) and not nothing (zero runs).
    const row = ctx.getImageData(0, Math.floor(50 * dpr), Math.floor(400 * dpr), 1).data;
    let lit = 0, runs = 0, inRun = false, peak = 0;
    for (let i = 0; i < row.length; i += 4) {
      const isBackground = row[i] === 8 && row[i + 1] === 15 && row[i + 2] === 26;
      if (!isBackground) {
        lit++; peak = Math.max(peak, row[i], row[i + 1], row[i + 2]);
        if (!inRun) { runs++; inRun = true; }
      } else inRun = false;
    }
    out.grid = { runs, lit, peak };

    // Text: the ratio of antialiased edge pixels to solid ink is the legibility proxy — the lower the
    // ratio, the less of the glyph is fringe.
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = "#080f1a"; ctx.fillRect(0, 120, 400, 80);
    ctx.fillStyle = "#ffffff";
    ctx.font = "11px ui-monospace, monospace";
    ctx.fillText("piece-3 · 24×16", 10, 150);
    const band = ctx.getImageData(0, Math.floor(140 * dpr), Math.floor(200 * dpr), Math.floor(14 * dpr)).data;
    let solid = 0, edge = 0;
    for (let i = 0; i < band.length; i += 4) {
      if (band[i] > 200) solid++;
      else if (band[i] > 40) edge++;
    }
    out.text = { solid, edge, fringeRatio: +(edge / Math.max(1, solid)).toFixed(2) };
    return out;
  }, { dpr, lowZoom: LOW_ZOOM });
  await context.close();
  return result;
}

const browser = await openBrowser();
let failures = 0;

for (const dpr of [1, 2]) {
  const r = await probeAt(browser, dpr);
  console.log(`\n── device pixel ratio ${r.dpr} ──`);
  console.log(`  --canvas-chunk resolves to  ${r.rawChunk}`);
  console.log(`  context takes color-mix()   ${r.colorMix.accepted}  → ${r.colorMix.normalized}`);
  console.log(`  context takes a plain hex   ${r.hex.accepted}  → ${r.hex.normalized}`);
  console.log(`  context takes "var(--x)"    ${r.varLiteral.accepted}   ← false is the point`);
  console.log(`  dashed grid at scale ${LOW_ZOOM}   ${r.grid.runs} runs / ${r.grid.lit} lit px / peak channel ${r.grid.peak}`);
  console.log(`  text 11px                   ${r.text.solid} solid px, ${r.text.edge} fringe px, ratio ${r.text.fringeRatio}`);

  if (!r.colorMix.accepted) { console.log("  FAIL the painter would have to evaluate color-mix itself"); failures++; }
  if (r.varLiteral.accepted) { console.log("  FAIL a var() literal was accepted — the token resolver's premise is wrong here"); failures++; }
  if (r.grid.runs < 10) { console.log("  FAIL the dashed grid did not survive the low zoom"); failures++; }
}

await browser.close();
console.log(failures ? `\n${failures} probe expectation(s) failed` : "\nall probe expectations held");
process.exit(failures ? 1 : 0);
