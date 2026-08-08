/**
 * The Draw phase's dock says what the next shape will do.
 *
 * Drawing a carve where a build was meant is the classic mistake in a boolean editor, and the operation used
 * to be two square icon buttons in a row of nine — a peer of the tool that draws rather than a property of
 * what is about to be drawn. It is now the word that leads the draw group in the canvas dock, and the group
 * wears the colour of the mode, so the three shape buttons beside it state it too. What is checked here is
 * exactly that contract: one control, carrying one state, colouring the tools it decides for, that goes
 * quiet when the tool in hand does not draw.
 */

import { openBrowser, newPage, Checks, readSeed, BASE } from "./lib/harness.mjs";

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
checks.add("it leads a group of its own, holding the three tools it decides for",
  await drawGroup.locator(".canvas-dock-btn").count() === 3,
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

await browser.close();
checks.finish();
