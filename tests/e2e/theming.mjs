/**
 * The sketch tool's Theme phase (docs/tools/sketch.md) — the paint pass, keyed on shapes and islands.
 *
 *   1. API round-trip — a sketch layout carrying a theme registry + map default + a per-shape override
 *      survives PUT → GET through the real sketch endpoint (the storage the export resolves paint from).
 *   2. UI — the sketch tool's Theme phase renders as one step: the island tree, the swatch strip at the foot
 *      of the canvas, and the inspector. A theme is taken in hand from the strip and a click paints a shape.
 *   3. The phase edits nothing — its dock is select + move, and a drag moves no geometry.
 *   4. The room shells bind from the inspector's board defaults, and what is stored is the snapshot.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, worldAimer, shapeAimPoints, BASE, TMP_DIR }
  from "./lib/harness.mjs";
import { mkdir } from "node:fs/promises";

const seed = await readSeed();
const checks = new Checks("theming (sketch · Theme phase)");
const OUT = TMP_DIR;
await mkdir(OUT, { recursive: true });

// ── 1. themes round-trip through the sketch layout ────────────────────────────────────────────────────
checks.section("a themed sketch layout survives PUT → GET");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const firstShapeId = layout?.layers?.[0]?.layout?.shapes?.find(s => !s.role)?.id;
const themed = structuredClone(layout);
const recipe = (surfaceId, fillId) => ({ bedrock: { relative: false, value: 1 },
  surface: { material: { kind: "solid", id: surfaceId }, depth: 1, enabled: true },
  fill: { kind: "solid", id: fillId } });
// Two, because the brush is only testable against a theme no shape already carries.
themed.themes = { forest: recipe(2, 3), scar: recipe(1, 1) };   // grass — a green the overlay shows
themed.mapTheme = "forest";
if (firstShapeId) themed.layers[0].layout.shapes.find(s => s.id === firstShapeId).theme = "forest";
await api(`/map/${seed.sketchSlug}/sketch`, { method: "PUT", body: themed });

const back = await api(`/map/${seed.sketchSlug}/sketch`);
checks.add("theme registry persisted", back.themes?.forest != null, JSON.stringify(Object.keys(back.themes ?? {})));
checks.add("map default persisted", back.mapTheme === "forest", `mapTheme=${back.mapTheme}`);
checks.add("per-shape override persisted",
  !firstShapeId || back.layers[0].layout.shapes.find(s => s.id === firstShapeId)?.theme === "forest",
  firstShapeId ? `shape ${firstShapeId}` : "(no terrain shape to theme)");

// A library row for §4 to bind, made before the page loads: the inspector reads the room library when the
// Theme phase opens, so a row created after that is not in the list it offers. Given a wall height no
// built-in shell has, so finding it in the layout says the snapshot travelled rather than that a default
// happened to match. No courses: a part with none keeps the built-in finish, which is what makes a style
// that only changes its geometry a legal one.
const style = await api("/room-styles", { method: "POST", body: {
  name: "e2e-tall", floorDepth: 1, wallHeight: 11, roofThickness: 1,
  roofForm: "flat", roofHole: true, door: "stained-glass-pane", doorHeight: 3,
  windows: { form: "none", block: 0, data: 0, sill: 0, width: 0, height: 0, spacing: 0 },
  storeyStack: [], courses: [] } });
checks.add("a room style exists to bind", style?.id > 0, `id=${style?.id}`);

// ── 2. the Theme phase renders, and the strip is a brush ──────────────────────────────────────────────
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

  // Enter the Theme phase (the palette nav button). One step, so there is nothing to advance through.
  await page.click('button[title="Theme"]', { timeout: 8000 });
  await page.waitForTimeout(1200);
  await shot("theme-phase.png");

  // The registry is chrome: one swatch per board theme, the map default badged, and no apply button anywhere.
  const swatches = await page.locator(".canvas-theme-swatch").allInnerTexts();
  checks.add("the strip carries the board's themes", swatches.length === 2, swatches.join(" | "));
  checks.add("the map default is badged on its swatch",
    await page.locator('.canvas-theme-swatch:has-text("forest") .canvas-theme-swatch-tag').count() === 1);
  // Nothing in the strip promises a chord the tool does not answer: the number keys are the phases'.
  checks.add("no swatch claims a number key",
    await page.locator(".canvas-theme-swatch-key").count() === 0);
  checks.add("nothing is applied by a button", await page.locator('button:has-text("Apply")').count() === 0);

  // With nothing selected the inspector describes the board rather than a selection.
  checks.add("the inspector shows the board's defaults with nothing selected",
    await page.locator("text=Board defaults").count() > 0
    && await page.locator("text=Room shells").count() > 0);

  // The Blocks overlay is the phase's own, on without being asked for: the paint is what the phase acts on.
  checks.add("Blocks is on when the phase opens",
    await page.locator('button.canvas-chip:has-text("Blocks")')
      .evaluate(el => el.classList.contains("canvas-chip--on")));
  // And the contour chip is not offered, because the painted ground already carries the height.
  checks.add("the contour chip is not offered here",
    await page.locator('button.canvas-chip:has-text("Relief")').count() === 0);

  // Take the unassigned theme in hand and paint a shape with a click on the board.
  await page.click('.canvas-theme-swatch:has-text("scar")');
  await page.waitForTimeout(500);
  checks.add("a swatch clicked is in hand",
    await page.locator(".canvas-theme-swatch--on").count() === 1
    && await page.locator("text=In hand").count() > 0);

  // Aim at a shape the layout actually carries rather than at a fraction of the viewport: where the board
  // sits on screen is the fit's business, and a click that misses proves nothing about the brush.
  const shapes = (await api(`/map/${seed.sketchSlug}/sketch`)).layers.flatMap(l => l.layout?.shapes ?? [])
    .filter(s => !s.role);
  const aim = await worldAimer(page);
  checks.add("the canvas reports where a world block is", aim != null);
  const scarCount = async () => (await api(`/map/${seed.sketchSlug}/sketch`)).layers
    .flatMap(layer => layer.layout?.shapes ?? []).filter(shape => shape.theme === "scar").length;
  let scarred = 0;
  for (const point of shapes.flatMap(shape => shapeAimPoints(shape))) {
    if (!aim) break;
    const at = aim(point.x, point.z);
    await page.mouse.click(at.x, at.y);
    await page.waitForTimeout(1400);   // past the autosave debounce
    scarred = await scarCount();
    if (scarred >= 1) break;
  }
  await shot("theme-painted.png");
  checks.add("a click on the board painted a shape", scarred >= 1, `${scarred} shape(s) → scar`);
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
  // Still in the Theme phase from above. Put the brush down first: with one in hand a click paints rather
  // than selects, which is a different contract from the one this section is about.
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
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

// ── 4. the board defaults bind a room shell ───────────────────────────────────────────────────────────
// A shell is a fallback in the sense the map default is (structures.md §9): one for every wool cage and one
// for every spawn cube, snapshotted into the layout rather than referenced, and bound from the inspector's
// board defaults where the other fallbacks are. What has to survive is the snapshot itself — the export
// reads the layout and nothing else, so a binding that did not come back from GET would silently stamp the
// built-in shell.
checks.section("the board defaults bind a room shell");

clearFaults(page);
let bound = false;
try {
  // The board defaults show only with nothing selected — the column describes the board when it has no one
  // thing to describe — so clear whatever the paint above left picked.
  await page.keyboard.press("Escape");
  await page.keyboard.press("Escape");
  await page.waitForTimeout(1000);
  await shot("theme-rooms.png");
  checks.add("the room shells render under the board defaults",
    await page.locator("text=Wool cages").count() > 0
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
  page.faults.push(`room shells: ${String(e).split("\n")[0]}`);
}
checks.add("room shells drove without error", bound, page.faults.slice(0, 3).join(" | "));
checks.add("sketch tool is clean under the room shells", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.finish();
await browser.close();
