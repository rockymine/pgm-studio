/**
 * The Draw phase's toolbar says what the next shape will do.
 *
 * Drawing a carve where a build was meant is the classic mistake in a boolean editor, and the operation used
 * to be two square icon buttons in a row of nine — a peer of the tool that draws rather than a property of
 * what is about to be drawn. It is now one pill, ruled off from the tools, that names the mode it is in and
 * flips to the other when clicked. What is checked here is exactly that contract: one control, carrying one
 * state, that goes quiet when the tool in hand does not draw.
 */

import { openBrowser, newPage, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("draw tools");
const browser = await openBrowser();
const page = await newPage(browser);

await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
await page.waitForSelector(".canvas-subbar", { timeout: 20000 });

const pill = page.locator(".op-pill");
const cls = async () => await pill.getAttribute("class");
const label = async () => (await pill.textContent())?.trim();

// ── the operation is one control, not two ─────────────────────────────────────────────────────────────
checks.section("the operation is one control naming one state");

checks.add("there is exactly one operation control", await pill.count() === 1, `${await pill.count()}`);
checks.add("and a rule separates it from the tools",
  await page.locator(".canvas-toolbar .subbar-sep").count() === 1);
checks.add("nothing else in the strip repeats its state",
  await page.locator(".canvas-op-badge").count() === 0);

// ── idle until a tool that draws is in hand ────────────────────────────────────────────────────────────
checks.section("it goes quiet when nothing it decides is about to happen");

// The sketch opens on move, which does not draw.
checks.add("on move the pill is dimmed", (await cls()).includes("op-pill--inert"));

await page.click('button[title="Measure (drag across a void gap to read its length in blocks)"]');
await page.waitForTimeout(120);
checks.add("measure does not draw either, so it stays dimmed", (await cls()).includes("op-pill--inert"));

// ── armed, it names its mode and flips ────────────────────────────────────────────────────────────────
checks.section("armed, it names its mode and one click flips it");

await page.click('button[title="Rectangle"]');
await page.waitForTimeout(120);
checks.add("a draw tool wakes it", !(await cls()).includes("op-pill--inert"));
checks.add("and a sketch starts building", await label() === "Build" && (await cls()).includes("op-pill--build"),
  `${await label()} · ${await cls()}`);

await pill.click();
await page.waitForTimeout(120);
checks.add("one click carves instead", await label() === "Carve" && (await cls()).includes("op-pill--carve"),
  `${await label()} · ${await cls()}`);

await pill.click();
await page.waitForTimeout(120);
checks.add("and clicking again goes back", await label() === "Build", await label());

// The operation survives a tool change — it is a property of the next shape, not of the tool in hand.
await pill.click();
await page.click('button[title="Lasso (hold and trace)"]');
await page.waitForTimeout(120);
checks.add("it is remembered across a tool change",
  await label() === "Carve" && !(await cls()).includes("op-pill--inert"), await label());

checks.add("the page raised no fault", (page.faults ?? []).length === 0, (page.faults ?? []).join(" | "));

await browser.close();
checks.finish();
