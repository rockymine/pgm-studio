/**
 * The Dressing phase on the sketch tool (decoration.md) — the pass that adds what stands on the terrain,
 * authored beside the Theme phase and stored the same way.
 *
 *   1. API round-trip — a sketch layout carrying a dressing registry, a map default, a per-shape override
 *      and a path shape survives PUT → GET through the real sketch endpoint.
 *   2. Preview — the endpoint the phase draws with grows the recipe rather than illustrating it, so the
 *      counts move with the knobs and the theme decides whether anything grows at all.
 *   3. UI — the phase renders (Create and Apply), and the Draw toolbar offers the path tool.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("dressing (decoration.md)");
const OUT = new URL("../../.tmp/", import.meta.url).pathname;
await mkdir(OUT, { recursive: true });

// ── 1. a dressed sketch layout round-trips ────────────────────────────────────────────────────────────
checks.section("a dressed sketch layout survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const firstShapeId = layout?.layers?.[0]?.layout?.shapes?.find(s => !s.role)?.id;
const dressed = structuredClone(layout);
dressed.dressings = {
  meadow: { flora: { coverage: 0.5, flowerShare: 0.3, seed: 7 } },
  scree: { boulders: { density: 0.3, form: "angular", seed: 17 } },
};
dressed.mapDressing = "meadow";
if (firstShapeId) dressed.layers[0].layout.shapes.find(s => s.id === firstShapeId).dressing = "scree";
// A path is stored as the open line it was drawn as plus a half-width — the band is derived, never stored.
dressed.layers[0].layout.shapes.push({
  id: "e2e-path", type: "path", operation: "add", override: false,
  vertices: [[0, 0], [24, 6], [40, 24]], radius: 3, path_edge: "rough", path_seed: 5,
});
await api(`/map/${seed.sketchSlug}/sketch`, { method: "PUT", body: dressed });

const back = await api(`/map/${seed.sketchSlug}/sketch`);
checks.add("dressing registry persisted", back.dressings?.meadow != null, JSON.stringify(Object.keys(back.dressings ?? {})));
checks.add("map default persisted", back.mapDressing === "meadow", `mapDressing=${back.mapDressing}`);
checks.add("per-shape override persisted",
  !firstShapeId || back.layers[0].layout.shapes.find(s => s.id === firstShapeId)?.dressing === "scree",
  firstShapeId ? `shape ${firstShapeId}` : "(no terrain shape to dress)");

const path = back.layers?.[0]?.layout?.shapes?.find(s => s.id === "e2e-path");
checks.add("a path keeps its line, width and edge",
  path?.vertices?.length === 3 && path?.radius === 3 && path?.path_edge === "rough" && path?.path_seed === 5,
  JSON.stringify(path ?? null));

// ── 2. the preview grows the recipe ───────────────────────────────────────────────────────────────────
// The picture is the real pass run over a sample patch, so a knob that does nothing in the export does
// nothing here — which is only worth having if the numbers actually move.
checks.section("the preview grows what the recipe says");

const grassTheme = JSON.stringify({ surface: { material: { kind: "solid", id: 2 }, depth: 1, enabled: true } });
const preview = (dressingJson) =>
  api("/terrain/dressing-preview", { method: "POST", body: { dressingJson, themeJson: grassTheme } });

const sparse = await preview(JSON.stringify({ flora: { coverage: 0.2, seed: 7 } }));
const lush = await preview(JSON.stringify({ flora: { coverage: 0.9, seed: 7 } }));
const rocky = await preview(JSON.stringify({ boulders: { density: 0.5, seed: 17 } }));

checks.add("coverage moves the plant count", lush.counts.plants > sparse.counts.plants,
  `${sparse.counts.plants} → ${lush.counts.plants}`);
checks.add("boulders grow rock and not plants", rocky.counts.boulders > 0 && rocky.counts.plants === 0,
  JSON.stringify(rocky.counts));
checks.add("both views are drawn", (lush.plan?.length ?? 0) > 100 && (lush.section?.length ?? 0) > 100,
  `plan ${lush.plan?.length} · section ${lush.section?.length}`);

// Quartz takes no flora — the theme underneath is what decides whether anything grows.
const paved = await api("/terrain/dressing-preview", {
  method: "POST",
  body: {
    dressingJson: JSON.stringify({ flora: { coverage: 0.9, seed: 7 } }),
    themeJson: JSON.stringify({ surface: { material: { kind: "solid", id: 155 }, depth: 1, enabled: true } }),
  },
});
checks.add("nothing grows on quartz", paved.counts.plants === 0, `${paved.counts.plants} plants`);

// ── 3. the phase renders, and the path tool is on the Draw toolbar ────────────────────────────────────
checks.section("the sketch Dressing phase renders");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
async function shot(file) { await page.screenshot({ path: `${OUT}${file}`, fullPage: false }); }

clearFaults(page);
let ok = false, tools = [];
try {
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1500);

  tools = await page.locator(".canvas-toolbar .draw-tool-btn").evaluateAll(
    els => els.map(el => el.getAttribute("title")));
  checks.add("the Draw toolbar offers the path tool", tools.some(t => t?.startsWith("Path")), tools.join(" | "));

  await page.click('button[title="Dressing"]', { timeout: 8000 });
  await page.waitForTimeout(1200);
  await shot("dressing-create.png");
  checks.add("Dressing · Create renders", await page.locator("text=What it grows").count() > 0);

  await page.fill('input[placeholder="name"]', "meadow");
  await page.click('button:has-text("Add")');
  await page.waitForTimeout(1200);
  await shot("dressing-parts.png");
  checks.add("a new dressing grows ground cover", await page.locator("text=Ground cover").count() > 0);

  await page.click('button:has-text("Apply →")');
  await page.waitForTimeout(800);
  await shot("dressing-apply.png");
  checks.add("Dressing · Apply renders", await page.locator("text=Map default").count() > 0);
  ok = true;
} catch (e) {
  page.faults.push(`dressing phase drive: ${String(e).split("\n")[0]}`);
}
checks.add("Dressing phase drove without error", ok);
checks.add("sketch tool is clean under dressing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
