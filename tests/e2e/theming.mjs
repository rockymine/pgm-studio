/**
 * Theme phase on the sketch tool (finishing-model.md §4) — the paint pass moved off the plan onto the sketch,
 * keyed on shapes/islands.
 *
 *   1. API round-trip — a sketch layout carrying a theme registry + map default + a per-shape override
 *      survives PUT → GET through the real sketch endpoint (the storage the export resolves paint from).
 *   2. UI — the sketch tool's new Theme phase renders: Create (author a theme) and Apply (the island tree
 *      plus the theme controls), both screenshotted and clean.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE, TMP_DIR }
  from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("theming (finishing-model §4)");
const OUT = TMP_DIR;
await mkdir(OUT, { recursive: true });

// ── 1. themes round-trip through the sketch layout ────────────────────────────────────────────────────
checks.section("a themed sketch layout survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const firstShapeId = layout?.layers?.[0]?.layout?.shapes?.find(s => !s.role)?.id;
const themed = structuredClone(layout);
themed.themes = { forest: { bedrock: { relative: false, value: 1 },
  surface: { material: { kind: "solid", id: 2 }, depth: 1, enabled: true },   // grass — a green the overlay shows
  fill: { kind: "solid", id: 3 } } };
themed.mapTheme = "forest";
if (firstShapeId) themed.layers[0].layout.shapes.find(s => s.id === firstShapeId).theme = "forest";
await api(`/map/${seed.sketchSlug}/sketch`, { method: "PUT", body: themed });

const back = await api(`/map/${seed.sketchSlug}/sketch`);
checks.add("theme registry persisted", back.themes?.forest != null, JSON.stringify(Object.keys(back.themes ?? {})));
checks.add("map default persisted", back.mapTheme === "forest", `mapTheme=${back.mapTheme}`);
checks.add("per-shape override persisted",
  !firstShapeId || back.layers[0].layout.shapes.find(s => s.id === firstShapeId)?.theme === "forest",
  firstShapeId ? `shape ${firstShapeId}` : "(no terrain shape to theme)");

// ── 2. the Theme phase renders (Create + Apply) ───────────────────────────────────────────────────────
checks.section("the sketch Theme phase renders");

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });

async function shot(file) { await page.screenshot({ path: `${OUT}${file}`, fullPage: false }); }

clearFaults(page);
let ok = false;
try {
  await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector("canvas", { timeout: 20000 });
  await page.waitForTimeout(1500);

  // Enter the Theme phase (the palette nav button), then screenshot the Create step.
  await page.click('button[title="Theme"]', { timeout: 8000 });
  await page.waitForTimeout(800);
  await shot("theme-create.png");
  checks.add("Theme · Create renders", await page.locator("text=A theme is a full terrain-paint recipe").count() > 0);

  // Author a theme, then advance to Apply.
  await page.fill('input[placeholder="name"]', "forest");
  await page.click('button:has-text("Add")');
  await page.waitForTimeout(600);
  await page.click('button:has-text("Apply →")');
  await page.waitForTimeout(800);
  await shot("theme-apply.png");
  const applyRendered = await page.locator("text=Map default").count() > 0
    && await page.locator('text=Theme to apply').count() > 0;
  checks.add("Theme · Apply renders (island tree + theme controls)", applyRendered);

  // Select the island in the tree, apply the theme, then toggle Blocks — the overlay should now paint the
  // footprint in the theme's surface colour (the paint preview, via the existing Blocks toggle).
  await page.click('.geo-row:has-text("island")');
  await page.waitForTimeout(400);
  await page.click('button:has-text("Apply forest")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Blocks")');
  await page.waitForTimeout(900);
  await shot("theme-blocks.png");
  checks.add("themed shape applied", await page.locator("text=forest").count() > 1);
  ok = true;
} catch (e) {
  page.faults.push(`theme phase drive: ${String(e).split("\n")[0]}`);
}
checks.add("Theme phase drove without error", ok);
checks.add("sketch tool is clean under theming", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

// ── 3. the Theme phase is selection-only ──────────────────────────────────────────────────────────────
// Editing geometry belongs to the Draw phase. Theme assigns paint to shapes it does not own, so its canvas
// offers exactly two things: pick something, and move the view. The dock is the visible half of that
// contract and a drag on a selected island is the load-bearing half — a canvas that still moved geometry
// here would let an author reshape the map from a rail with no undo, no snapping and no height controls.
checks.section("the Theme phase edits nothing");

clearFaults(page);
let restricted = false, tools = [];
try {
  // Still in the Apply step from above, with the island selected.
  tools = await page.locator(".canvas-dock .canvas-dock-btn").evaluateAll(
    els => els.map(el => el.getAttribute("aria-label")));

  // A drag across the selected island must pan the view, not move the island.
  const before = JSON.stringify(await api(`/map/${seed.sketchSlug}/sketch`));
  const box = await page.locator("canvas").boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 90, cy + 60, { steps: 12 });
  await page.mouse.up();
  await page.waitForTimeout(1800);   // past the autosave debounce
  const after = JSON.stringify(await api(`/map/${seed.sketchSlug}/sketch`));
  checks.add("dragging a selection moves no geometry", before === after,
    before === after ? "layout byte-identical" : "the layout changed under a Theme-phase drag");
  restricted = true;
} catch (e) {
  page.faults.push(`select-only: ${String(e).split("\n")[0]}`);
}
checks.add("the dock offers select + move only", tools.length === 2, tools.join(" | ") || "(no dock)");
checks.add("select-only checks ran", restricted, page.faults.slice(0, 3).join(" | "));

// ── 4. the Rooms step binds a shell to the map ────────────────────────────────────────────────────────
// The phase's third step (structures.md §9): one shell for every wool cage and one for every spawn cube,
// snapshotted into the layout rather than referenced. What has to survive is the snapshot itself — the
// export reads the layout and nothing else, so a binding that did not come back from GET would silently
// stamp the built-in shell.
checks.section("the Rooms step binds a shell");

// A library row to bind. Made here rather than assumed, since a fresh database ships no room styles — and
// given a wall height no built-in shell has, so finding it in the layout says the snapshot travelled rather
// than that a default happened to match. No courses: a part with none keeps the built-in finish, which is
// what makes a style that only changes its geometry a legal one.
const style = await api("/room-styles", { method: "POST", body: {
  name: "e2e-tall", floorDepth: 1, wallHeight: 11, roofThickness: 1,
  roofForm: "flat", roofHole: true, door: "stained-glass-pane", doorHeight: 3,
  windows: { form: "none", block: 0, data: 0, sill: 0, width: 0, height: 0, spacing: 0 },
  storeyStack: [], courses: [] } });
checks.add("a room style exists to bind", style?.id > 0, `id=${style?.id}`);

clearFaults(page);
let bound = false;
try {
  await page.click('.flow-step:has-text("Rooms")', { timeout: 8000 });
  await page.waitForTimeout(1000);
  await shot("theme-rooms.png");
  checks.add("Theme · Rooms renders", await page.locator("text=Wool cages").count() > 0
    && await page.locator("text=Spawn cubes").count() > 0);

  const picker = page.locator(".lib-bind select").first();
  await picker.selectOption(String(style.id));
  await page.waitForTimeout(2000);   // past the autosave debounce

  const doc = await api(`/map/${seed.sketchSlug}/sketch`);
  const cage = doc?.roomStyles?.cage;
  checks.add("the cage binding survives PUT → GET", cage != null,
    JSON.stringify(Object.keys(doc?.roomStyles ?? {})));
  // A snapshot, not a reference: what came back is the style itself, carrying the wall this one was given
  // and no library id to go stale.
  checks.add("what is stored is the style, not its id",
    cage?.wall?.extent === 11 && cage.styleId == null && cage.id == null,
    JSON.stringify(Object.keys(cage ?? {})));
  // The other kind is untouched — the two bind independently.
  checks.add("binding the cage leaves the spawn on its built-in shell", doc.roomStyles.spawn == null);
  await shot("theme-rooms-bound.png");
  bound = true;
} catch (e) {
  page.faults.push(`rooms step: ${String(e).split("\n")[0]}`);
}
checks.add("Rooms step drove without error", bound, page.faults.slice(0, 3).join(" | "));
checks.add("sketch tool is clean under the Rooms step", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
