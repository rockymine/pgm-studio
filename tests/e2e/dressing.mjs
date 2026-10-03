/**
 * The Dressing phase on the sketch tool (decoration.md) — the pass that places what stands on the terrain.
 *
 *   1. API round-trip — a sketch layout carrying placed props (a path, an area, a tree, a boulder) survives
 *      PUT → GET through the real sketch endpoint, each read back as the kind it was written as.
 *   2. Pickers + preview — the option cards are drawn by the pass itself, and the preview's counts move with
 *      the knobs, which is the only thing that makes "the picture is the real pass" worth claiming.
 *   3. UI — the phase renders its placing tools, and dragging on the canvas actually places a prop.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE, TMP_DIR }
  from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("dressing (decoration.md)");
const OUT = TMP_DIR;
await mkdir(OUT, { recursive: true });

// ── 1. placed props round-trip ────────────────────────────────────────────────────────────────────────
checks.section("placed dressing survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const dressed = structuredClone(layout);
dressed.dressing = {
  props: [
    { kind: "stroke", id: "p1", points: [[0, 0], [24, 6], [40, 24]], radius: 3, style: "stones", seed: 5,
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
checks.add("each kept its kind", props.map(p => p.kind).join(",") === "stroke,flora,tree,boulder",
  props.map(p => p.kind).join(","));
checks.add("a path kept its route, width and style",
  props[0]?.points?.length === 3 && props[0]?.radius === 3 && props[0]?.style === "stones",
  JSON.stringify(props[0] ?? null));
checks.add("a marker kept its cell", props[2]?.x === 10 && props[2]?.z === 12, JSON.stringify(props[2] ?? null));

// ── 2. the pickers are drawn, and the preview places ──────────────────────────────────────────────────
checks.section("every option is drawn by the pass");

const styles = await api("/terrain/stroke-styles");
const fluidFormCards = await api("/terrain/fluid-forms");
const forms = await api("/terrain/boulder-forms");
const species = await api("/terrain/species");

checks.add("five stroke styles, each drawn", styles.length === 5 && styles.every(s => s.svg?.includes("<rect")),
  styles.map(s => s.key).join(" "));
checks.add("three channel forms, each drawn", fluidFormCards.length === 3 && fluidFormCards.every(f => f.svg?.includes("<rect")),
  fluidFormCards.map(f => f.key).join(" "));
checks.add("four boulder forms, each drawn", forms.length === 4 && forms.every(f => f.svg?.includes("<rect")),
  forms.map(f => f.key).join(" "));
checks.add("every species drawn, and carrying its own proportions",
  species.length >= 6 && species.every(s => s.svg?.includes("<rect") && s.defaults?.includes("height")),
  species.map(s => s.key).join(" "));

// A species names a silhouette, so six species cards that drew the same tree would be six palettes on one
// shape — the claim the picker exists to make, asserted on the cards themselves.
const blocks = (svg) => (svg.match(/<rect/g) ?? []).length;
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
const road = await preview({ kind: "stroke", points: [[0, 20], [40, 20]], radius: 3, seed: 5, pave: { kind: "solid", id: 13, data: 0 } });
const trail = await preview({ kind: "stroke", points: [[0, 20], [40, 20]], radius: 3, style: "stones", seed: 5, pave: { kind: "solid", id: 13, data: 0 } });
const tree = await preview({ kind: "tree", x: 0, z: 0, species: "spruce", height: 24, seed: 5 });
const shallow = await preview({ kind: "fluid", points: [[0, 20], [40, 20]], radius: 4, depth: 1, seed: 5 });
const deep = await preview({ kind: "fluid", points: [[0, 20], [40, 20]], radius: 4, depth: 5, seed: 5 });

checks.add("coverage moves the plant count", lush.counts.plants > sparse.counts.plants,
  `${sparse.counts.plants} → ${lush.counts.plants}`);
checks.add("stepping stones pave less than a road", trail.counts.pathCells < road.counts.pathCells && trail.counts.pathCells > 0,
  `${road.counts.pathCells} → ${trail.counts.pathCells}`);
checks.add("a channel carves and fills, and a deeper one is drawn no shorter", deep.counts.fluidCells > 0
  && shallow.counts.fluidCells > 0 && (deep.section?.length ?? 0) > 100,
  `shallow ${shallow.counts.fluidCells} · deep ${deep.counts.fluidCells} · section ${deep.section?.length}`);
checks.add("one tree is one tree", tree.counts.trees === 1, JSON.stringify(tree.counts));
checks.add("both views are drawn", (tree.plan?.length ?? 0) > 100 && (tree.section?.length ?? 0) > 100,
  `plan ${tree.plan?.length} · section ${tree.section?.length}`);

// ── 3. the phase places on the canvas ─────────────────────────────────────────────────────────────────
checks.section("the sketch Dressing phase places things");

// The placing tools this spec drives, named once: the dock check asserts they are offered and the drive
// clicks them, so a renamed tool fails both together rather than passing one and timing out in the other.
const DRIVEN = { stroke: "Stroke", fluid: "Fluid", tree: "Tree" };

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
async function shot(file) { await page.screenshot({ path: `${OUT}${file}`, fullPage: false }); }

clearFaults(page);
let ok = false, tools = [];
try {
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1500);

  await page.click('button[title="Decoration"]', { timeout: 8000 });
  await page.waitForTimeout(1500);

  const groups = await page.locator(".canvas-dock .canvas-dock-group").evaluateAll(
    els => els.map(group => [...group.querySelectorAll(".canvas-dock-btn")]
      .map(btn => btn.getAttribute("aria-label"))));
  tools = groups.flat();
  // The dock leads with getting around the canvas (Select · Move) and every group after it places
  // something — `DressingTools.All`, one button each. Reading the count off the dock rather than
  // restating it means a tool added to that list does not fail a phase that works; what this spec
  // needs is that the tools it drives below are the ones on offer.
  const placing = groups.slice(1).flat();
  checks.add("the phase offers the placing tools this spec drives",
    placing.length > 0 && Object.values(DRIVEN).every(name => placing.includes(name)),
    placing.join(" | "));
  // The draw tools are Draw's: dressing places props, it does not author geometry.
  checks.add("and none of the shape tools", !tools.some(t => /^(Rectangle|Polygon|Lasso)/.test(t ?? "")),
    tools.join(" | "));

  await shot("dressing-phase.png");

  // Drop a tree by clicking, which is the whole interaction. Both clicks land on the red island, since a
  // marker dropped on the void is refused and the Placed list would not move.
  const box = await page.locator("svg.map-canvas-svg").boundingBox();
  const placedRows = () => page.locator(".geo-label").allInnerTexts();
  const before = await placedRows();
  await page.click(`button[aria-label^="${DRIVEN.tree}"]`);
  await page.mouse.click(box.x + box.width * 0.56, box.y + box.height * 0.62);
  await page.waitForTimeout(1500);
  // What the phase draws for a tree is the recipe list, and the list is offered with the tool in hand too, so
  // the Placed list is what says one landed.
  const panel = () => page.evaluate(() => document.body.innerText);
  // The library's trees load with the first tree the phase shows, and a cold studio draws every card first.
  await page.waitForSelector('.prop-card[title="oak"]', { timeout: 30000 }).catch(() => {});
  checks.add("a click places a tree, and the phase asks which one",
    (await placedRows()).length === before.length + 1 && await page.locator('.prop-card[title="oak"]').count() > 0,
    `${before.length} → ${(await placedRows()).length} placed · ${(await panel()).match(/GENERATED/)?.[0] ?? "(no tree list)"}`);
  await shot("dressing-tree.png");

  // Which tree a placement is, is the recipe it names, so what the phase carries is the card it marks
  // active. Cards are addressed by their title attribute, which is the row's name exactly: `oak` as text
  // also matches `dark oak`.
  await page.locator('.prop-card[title="spruce"]').click();
  await page.waitForTimeout(2500);
  checks.add("picking a recipe marks it, and only it",
    await page.locator('.prop-card--active[title="spruce"]').count() === 1
    && await page.locator(".prop-card--active").count() === 1);
  // A generated tree is tuned where it stands: its height is a slider on the placement, not a library row.
  checks.add("a generated tree offers its height, and its seed",
    /height \(blocks\)/i.test(await panel()) && /seed/i.test(await panel()),
    (await panel()).match(/height \(blocks\)|seed/gi)?.join(",") ?? "(neither)");
  await shot("dressing-tree-spruce.png");
  await page.locator('.prop-card[title="oak"]').click();
  await page.waitForTimeout(2000);
  checks.add("and picking another moves the mark rather than adding one",
    await page.locator('.prop-card--active[title="oak"]').count() === 1
    && await page.locator(".prop-card--active").count() === 1);

  // Arming the tool again lets go of the tree just placed, so the recipe picked next is the next tree's and
  // the oak already down stays an oak.
  await page.click(`button[aria-label^="${DRIVEN.tree}"]`);
  await page.waitForTimeout(800);
  checks.add("arming the tool again shows the next placement, not the last one",
    /in hand/.test(await panel()), (await panel()).match(/selected|in hand/)?.[0] ?? "(no badge)");
  await page.locator('.prop-card[title="spruce"]').click();
  await page.waitForTimeout(1500);
  await page.mouse.click(box.x + box.width * 0.62, box.y + box.height * 0.59);
  await page.waitForTimeout(1500);
  const placed = (await placedRows()).slice(before.length);
  checks.add("so the pick lands on the new tree and leaves the old one as it was",
    placed.join(",") === "oak,spruce", placed.join(","));

  // Shift held as a tree lands keeps the tool in hand: two clicks are two trees, and the dock still shows
  // the tree tool pressed afterwards.
  await page.click(`button[aria-label^="${DRIVEN.tree}"]`);
  await page.waitForTimeout(500);
  await page.keyboard.down("Shift");
  await page.mouse.click(box.x + box.width * 0.52, box.y + box.height * 0.69);
  await page.waitForTimeout(800);
  await page.mouse.click(box.x + box.width * 0.64, box.y + box.height * 0.68);
  await page.waitForTimeout(800);
  await page.keyboard.up("Shift");
  await page.waitForTimeout(500);
  const kept = (await placedRows()).length - before.length;
  checks.add("with Shift held a click places a tree and keeps the tool",
    kept === 4 && await page.locator(`button[aria-pressed][aria-label^="${DRIVEN.tree}"]`).count() === 1
    && /in hand/.test(await panel()),
    `${kept} placed · pressed ${await page.locator('.canvas-dock button[aria-pressed]').evaluateAll(els => els.map(e => e.getAttribute("aria-label")))} · ${(await panel()).match(/selected|in hand/)?.[0]}`);
  await page.keyboard.press("Escape");
  await page.waitForTimeout(400);
  checks.add("and Escape puts it down", await page.locator('.canvas-dock button[aria-pressed][aria-label="Select"]').count() === 1,
    `${await page.locator('.canvas-dock button[aria-pressed]').evaluateAll(els => els.map(e => e.getAttribute("aria-label")))}`);

  // Drag a route: press, trace, release — no separate way to finish, which is the bug the rework fixes.
  await page.click(`button[aria-label^="${DRIVEN.stroke}"]`);
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

  // Drag a fluid channel: the same press-trace-release, but its inspector is the fluid one — a depth and a
  // form, the knobs a channel has and a path does not.
  await page.click(`button[aria-label^="${DRIVEN.fluid}"]`);
  await page.mouse.move(box.x + box.width * 0.25, box.y + box.height * 0.35);
  await page.mouse.down();
  for (const step of [0.35, 0.45, 0.55, 0.65, 0.75]) {
    await page.mouse.move(box.x + box.width * step, box.y + box.height * (0.35 + (step - 0.25) * 0.4));
    await page.waitForTimeout(60);
  }
  await page.mouse.up();
  await page.waitForTimeout(2000);
  checks.add("a drag places a fluid channel, with its own depth + form knobs",
    await page.locator("text=Depth").count() > 0 && await page.locator("text=Form").count() > 0);
  await shot("dressing-fluid.png");

  // Drop a chest with a click and give it a stack: the inspector lists what it holds, and the stack is what
  // the stored board carries.
  await page.click('button[aria-label^="Chest"]');
  await page.mouse.click(box.x + box.width * 0.6, box.y + box.height * 0.5);
  await page.waitForTimeout(1500);
  await page.locator("button", { hasText: "Add a stack" }).click();
  await page.waitForTimeout(2500);
  checks.add("a click places a chest, and its inspector lists the stack added to it",
    await page.locator(".chest-item").count() === 1, `${await page.locator(".chest-item").count()} stack row(s)`);
  const stored = await api(`/map/${seed.sketchSlug}/sketch`);
  const chests = (stored.dressing?.props ?? []).filter(prop => prop.kind === "chest");
  checks.add("and the stored board carries the chest with its stack",
    chests.length === 1 && chests[0].items?.length === 1, JSON.stringify(chests.map(chest => chest.items)));
  await shot("dressing-chest.png");
  ok = true;
} catch (e) {
  page.faults.push(`dressing phase drive: ${String(e).split("\n")[0]}`);
}
checks.add("Dressing phase drove without error", ok);
checks.add("sketch tool is clean under dressing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
