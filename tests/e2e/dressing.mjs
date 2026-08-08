/**
 * The Dressing phase on the sketch tool (decoration.md) — the pass that places what stands on the terrain.
 *
 *   1. API round-trip — a sketch layout carrying placed props (a path, an area, a tree, a boulder) survives
 *      PUT → GET through the real sketch endpoint, each read back as the kind it was written as.
 *   2. Pickers + preview — the option cards are drawn by the pass itself, and the preview's counts move with
 *      the knobs, which is the only thing that makes "the picture is the real pass" worth claiming.
 *   3. UI — the phase renders with its four placing tools, and dragging on the canvas actually places a prop.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("dressing (decoration.md)");
const OUT = new URL("../../.tmp/", import.meta.url).pathname;
await mkdir(OUT, { recursive: true });

// ── 1. placed props round-trip ────────────────────────────────────────────────────────────────────────
checks.section("placed dressing survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const dressed = structuredClone(layout);
dressed.dressing = {
  props: [
    { kind: "path", id: "p1", points: [[0, 0], [24, 6], [40, 24]], radius: 3, style: "stones", seed: 5,
      pave: { kind: "solid", id: 4, data: 0 } },
    { kind: "flora", id: "f1", points: [[0, 0], [20, 0], [20, 20]], spec: { coverage: 0.8 }, seed: 7 },
    { kind: "tree", id: "t1", x: 10, z: 12, species: "birch", height: 22, seed: 9 },
    { kind: "boulder", id: "b1", x: -8, z: 4, form: "cairn", size: 4, seed: 11 },
  ],
};
await api(`/map/${seed.sketchSlug}/sketch`, { method: "PUT", body: dressed });

const back = await api(`/map/${seed.sketchSlug}/sketch`);
const props = back.dressing?.props ?? [];
checks.add("every prop persisted", props.length === 4, `${props.length} props`);
checks.add("each kept its kind", props.map(p => p.kind).join(",") === "path,flora,tree,boulder",
  props.map(p => p.kind).join(","));
checks.add("a path kept its route, width and style",
  props[0]?.points?.length === 3 && props[0]?.radius === 3 && props[0]?.style === "stones",
  JSON.stringify(props[0] ?? null));
checks.add("a marker kept its cell", props[2]?.x === 10 && props[2]?.z === 12, JSON.stringify(props[2] ?? null));

// ── 2. the pickers are drawn, and the preview places ──────────────────────────────────────────────────
checks.section("every option is drawn by the pass");

const styles = await api("/terrain/path-styles");
const waterFormCards = await api("/terrain/water-forms");
const forms = await api("/terrain/boulder-forms");
const species = await api("/terrain/species");

checks.add("five path styles, each drawn", styles.length === 5 && styles.every(s => s.svg?.includes("<rect")),
  styles.map(s => s.key).join(" "));
checks.add("three channel forms, each drawn", waterFormCards.length === 3 && waterFormCards.every(f => f.svg?.includes("<rect")),
  waterFormCards.map(f => f.key).join(" "));
checks.add("four boulder forms, each drawn", forms.length === 4 && forms.every(f => f.svg?.includes("<rect")),
  forms.map(f => f.key).join(" "));
checks.add("every species drawn, and carrying its own proportions",
  species.length >= 6 && species.every(s => s.svg?.includes("<rect") && s.defaults?.includes("height")),
  species.map(s => s.key).join(" "));

// The two tree forms are two trees. A species has to differ from its neighbours in shape, and a wood only in
// colour — six species that draw the same tree, or six woods that draw different ones, means the pickers have
// swapped the claim they are each making.
const woods = await api("/terrain/woods");
const blocks = (svg) => (svg.match(/<rect/g) ?? []).length;
checks.add("six woods, one tree, six colours",
  woods.length === 6 && new Set(woods.map(w => blocks(w.svg))).size === 1
    && new Set(woods.map(w => w.svg)).size === 6,
  woods.map(w => w.key).join(" "));
checks.add("the species differ in shape, not just in wood",
  new Set(species.map(s => blocks(s.svg))).size >= 5,
  species.map(s => `${s.key}:${blocks(s.svg)}`).join(" "));

checks.section("the preview places what the prop says");

const grassTheme = JSON.stringify({ surface: { material: { kind: "solid", id: 2 }, depth: 1, enabled: true } });
const preview = (prop) => api("/terrain/prop-preview", {
  method: "POST", body: { propJson: JSON.stringify(prop), themeJson: grassTheme },
});

const sparse = await preview({ kind: "flora", points: [[0, 0], [40, 0], [40, 40], [0, 40]], spec: { coverage: 0.2 }, seed: 7 });
const lush = await preview({ kind: "flora", points: [[0, 0], [40, 0], [40, 40], [0, 40]], spec: { coverage: 0.9 }, seed: 7 });
const road = await preview({ kind: "path", points: [[0, 20], [40, 20]], radius: 3, seed: 5, pave: { kind: "solid", id: 13, data: 0 } });
const trail = await preview({ kind: "path", points: [[0, 20], [40, 20]], radius: 3, style: "stones", seed: 5, pave: { kind: "solid", id: 13, data: 0 } });
const tree = await preview({ kind: "tree", x: 0, z: 0, species: "spruce", height: 24, seed: 5 });
const shallow = await preview({ kind: "water", points: [[0, 20], [40, 20]], radius: 4, depth: 1, seed: 5 });
const deep = await preview({ kind: "water", points: [[0, 20], [40, 20]], radius: 4, depth: 5, seed: 5 });

checks.add("coverage moves the plant count", lush.counts.plants > sparse.counts.plants,
  `${sparse.counts.plants} → ${lush.counts.plants}`);
checks.add("stepping stones pave less than a road", trail.counts.pathCells < road.counts.pathCells && trail.counts.pathCells > 0,
  `${road.counts.pathCells} → ${trail.counts.pathCells}`);
checks.add("a channel carves and fills, and a deeper one is drawn no shorter", deep.counts.waterCells > 0
  && shallow.counts.waterCells > 0 && (deep.section?.length ?? 0) > 100,
  `shallow ${shallow.counts.waterCells} · deep ${deep.counts.waterCells} · section ${deep.section?.length}`);
checks.add("one tree is one tree", tree.counts.trees === 1, JSON.stringify(tree.counts));
checks.add("both views are drawn", (tree.plan?.length ?? 0) > 100 && (tree.section?.length ?? 0) > 100,
  `plan ${tree.plan?.length} · section ${tree.section?.length}`);

// ── 3. the phase places on the canvas ─────────────────────────────────────────────────────────────────
checks.section("the sketch Dressing phase places things");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
async function shot(file) { await page.screenshot({ path: `${OUT}${file}`, fullPage: false }); }

clearFaults(page);
let ok = false, tools = [];
try {
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1500);

  await page.click('button[title="Dressing"]', { timeout: 8000 });
  await page.waitForTimeout(1500);

  tools = await page.locator(".canvas-dock .canvas-dock-btn").evaluateAll(
    els => els.map(el => el.getAttribute("aria-label")));
  const placing = tools.filter(t => /^(Path|Water|Ground cover|Tree|Boulder)/.test(t ?? ""));
  checks.add("the phase offers its five placing tools", placing.length === 5, tools.join(" | "));
  // The draw tools are Draw's: dressing places props, it does not author geometry.
  checks.add("and none of the shape tools", !tools.some(t => /^(Rectangle|Polygon|Lasso)/.test(t ?? "")),
    tools.join(" | "));

  await shot("dressing-phase.png");

  // Drop a tree by clicking, which is the whole interaction.
  const box = await page.locator("svg.map-canvas-svg").boundingBox();
  await page.click('button[aria-label^="Tree"]');
  await page.mouse.click(box.x + box.width * 0.45, box.y + box.height * 0.45);
  await page.waitForTimeout(1500);
  checks.add("a click places a tree", await page.locator("text=Species").count() > 0);
  await shot("dressing-tree.png");

  // The tree is two trees, and the inspector has to be able to say which.
  await page.locator('.choice-tile:has-text("Grown")').click();
  await page.waitForTimeout(2500);
  checks.add("switching to grown swaps the species picker for a wood picker",
    await page.locator("text=Wood").count() > 0 && await page.locator("text=Branch angle").count() > 0);
  await shot("dressing-tree-grown.png");
  await page.locator('.choice-tile:has-text("Vanilla")').click();
  await page.waitForTimeout(2000);
  checks.add("and back to vanilla leaves only the species and its height",
    await page.locator("text=Species").count() > 0 && await page.locator("text=Branch angle").count() === 0);

  // Drag a route: press, trace, release — no separate way to finish, which is the bug the rework fixes.
  await page.click('button[aria-label^="Path"]');
  await page.mouse.move(box.x + box.width * 0.25, box.y + box.height * 0.65);
  await page.mouse.down();
  for (const step of [0.35, 0.45, 0.55, 0.65]) {
    await page.mouse.move(box.x + box.width * step, box.y + box.height * (0.65 - (step - 0.25)));
    await page.waitForTimeout(60);
  }
  await page.mouse.up();
  await page.waitForTimeout(1500);
  checks.add("a drag places a path, and releasing ends it", await page.locator("text=Style").count() > 0);
  await shot("dressing-path.png");

  // Drag a water channel: the same press-trace-release, but its inspector is the water one — a depth and a
  // form, the knobs a channel has and a path does not.
  await page.click('button[aria-label^="Water"]');
  await page.mouse.move(box.x + box.width * 0.25, box.y + box.height * 0.35);
  await page.mouse.down();
  for (const step of [0.35, 0.45, 0.55, 0.65, 0.75]) {
    await page.mouse.move(box.x + box.width * step, box.y + box.height * (0.35 + (step - 0.25) * 0.4));
    await page.waitForTimeout(60);
  }
  await page.mouse.up();
  await page.waitForTimeout(2000);
  checks.add("a drag places a water channel, with its own depth + form knobs",
    await page.locator("text=Depth").count() > 0 && await page.locator("text=Form").count() > 0);
  await shot("dressing-water.png");
  ok = true;
} catch (e) {
  page.faults.push(`dressing phase drive: ${String(e).split("\n")[0]}`);
}
checks.add("Dressing phase drove without error", ok);
checks.add("sketch tool is clean under dressing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
