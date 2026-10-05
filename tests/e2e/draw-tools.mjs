/**
 * The Draw phase's dock says what the next shape will do.
 *
 * Drawing a carve where a build was meant is the classic mistake in a boolean editor, and the operation is
 * a property of what is about to be drawn rather than a peer of the tool that draws, so it is the word that leads the draw group in the canvas dock, and the group
 * wears the colour of the mode, so the four shape buttons beside it state it too. What is checked here is
 * exactly that contract: one control, carrying one state, colouring the tools it decides for, that goes
 * quiet when the tool in hand does not draw.
 *
 * The polyline is the one tool that leaves its line open, so a drawn one is checked through to the stored
 * layout: clicked points and Enter make a `polyline` shape rather than a closed outline.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("draw tools");
const browser = await openBrowser();
const page = await newPage(browser);

await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
await page.waitForSelector(".canvas-dock", { timeout: 20000 });

const mode = page.locator(".canvas-dock-mode");
const drawGroup = page.locator(".canvas-dock-group", { has: page.locator(".canvas-dock-mode") });
const label = async () => (await mode.textContent())?.trim();
const accent = async () => (await drawGroup.getAttribute("style")) ?? "";
const idle = async () => ((await drawGroup.getAttribute("class")) ?? "").includes("canvas-dock-group--idle");

// ── the operation is one control, not two ─────────────────────────────────────────────────────────────
checks.section("the operation is one control naming one state");

checks.add("there is exactly one operation control", await mode.count() === 1, `${await mode.count()}`);
checks.add("it leads a group of its own, holding the four tools it decides for",
  await drawGroup.locator(".canvas-dock-btn").count() === 4,
  `${await drawGroup.locator(".canvas-dock-btn").count()}`);
checks.add("and getting around the canvas is a different box",
  await page.locator('.canvas-dock-group:not(:has(.canvas-dock-mode)) button[aria-label="Move"]').count() === 1);

// ── idle until a tool that draws is in hand ────────────────────────────────────────────────────────────
checks.section("it goes quiet when nothing it decides is about to happen");

// The sketch opens on move, which does not draw.
checks.add("on move the group is dimmed", await idle());

await page.click('button[aria-label="Measure"]');
await page.waitForTimeout(120);
checks.add("measure does not draw either, so it stays dimmed", await idle());

// ── armed, it names its mode and flips ────────────────────────────────────────────────────────────────
checks.section("armed, it names its mode, colours its tools, and one click flips it");

await page.click('button[aria-label="Rectangle"]');
await page.waitForTimeout(120);
checks.add("a draw tool wakes it", !(await idle()));
checks.add("and a sketch starts building",
  await label() === "Build" && (await accent()).includes("canvas-add-fill"),
  `${await label()} · ${await accent()}`);

await mode.click();
await page.waitForTimeout(120);
checks.add("one click carves instead",
  await label() === "Carve" && (await accent()).includes("canvas-sub-fill"),
  `${await label()} · ${await accent()}`);

await mode.click();
await page.waitForTimeout(120);
checks.add("and clicking again goes back", await label() === "Build", await label());

// The operation survives a tool change — it is a property of the next shape, not of the tool in hand.
await mode.click();
await page.click('button[aria-label="Lasso"]');
await page.waitForTimeout(120);
checks.add("it is remembered across a tool change",
  await label() === "Carve" && !(await idle()), await label());

checks.add("the page raised no fault", (page.faults ?? []).length === 0, (page.faults ?? []).join(" | "));

// ── a polyline is drawn open and stored as one ─────────────────────────────────────────────────────────
checks.section("a polyline is clicked point by point, ends on Enter, and is stored open");

// A sketch with ground on it opens on Draw; a blank one opens on Info.
const draft = await api("/sketch", { method: "POST", body: { name: "E2E polyline" } });
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: {
  setup: { mirror_mode: "none", center: { cx: 0, cz: 0 } },
  layers: [{ id: "ground", name: "Ground", base_y: 0, layout: {
    shapes: [{ id: "land", type: "rectangle", operation: "add", min_x: -40, max_x: 40, min_z: -30, max_z: 30,
               floor: 0, base_height: 8 }],
    groups: [{ id: "land-g", name: "Land", mirrors: false, shapeIds: ["land"] }] } }] } });
const linePage = await newPage(browser);
await linePage.goto(`${BASE}/maps/${draft.slug}/sketch`, { waitUntil: "networkidle" });
await linePage.waitForSelector(".canvas-dock", { timeout: 20000 });
await linePage.waitForTimeout(1500);

await linePage.keyboard.press("w");
await linePage.waitForTimeout(120);
const polylineButton = linePage.locator('.canvas-dock button[aria-label="Polyline"]');
const polylineClass = (await polylineButton.getAttribute("class")) ?? "";
checks.add("W arms the polyline tool", polylineClass.includes("canvas-dock-btn--active"), polylineClass);

const box = await linePage.locator("svg.map-canvas-svg").boundingBox();
for (const [fx, fz] of [[0.35, 0.40], [0.50, 0.55], [0.65, 0.45]]) {
  await linePage.mouse.click(box.x + box.width * fx, box.y + box.height * fz);
  await linePage.waitForTimeout(150);
}
await linePage.keyboard.press("Enter");

// The save is debounced, so the stored layout is asked until the line is in it.
let shapes = [];
let line;
for (let attempt = 0; attempt < 20 && !line; attempt++) {
  await linePage.waitForTimeout(500);
  shapes = (await api(`/map/${draft.slug}/sketch`)).layers?.flatMap((layer) => layer.layout?.shapes ?? []) ?? [];
  line = shapes.find((shape) => shape.type === "polyline");
}
checks.add("the stored shape is a polyline", !!line, shapes.map((shape) => shape.type).join(", "));
checks.add("holding the three points it was clicked through", line?.vertices?.length === 3, `${line?.vertices?.length}`);
checks.add("with the band the tool gives it", line?.radius === 3 && line?.stroke_edge === "solid",
  `${line?.radius} · ${line?.stroke_edge}`);
checks.add("the polyline page raised no fault", (linePage.faults ?? []).length === 0, (linePage.faults ?? []).join(" | "));

await browser.close();
checks.finish();
