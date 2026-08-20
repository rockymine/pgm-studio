/**
 * Varying an objective in the plan tool.
 *
 * A core and a destroyable are placed as bare markers and take the generator's defaults; the knobs that vary
 * them — the casing's shape and its float/leak pair, a destroyable's design and material — live in the panel
 * that opens when one is selected. Every one of them existed in the plan format, the compiler and the stamper
 * long before anything could set them, so what is checked here is the part that was missing: that the panel
 * offers them, that a change reaches the document, and that it comes back on the next selection.
 *
 * The dig-depth sentence is the assertion that proves the round trip rather than the render. It is derived
 * from leak minus float, so it can only change if the new leak was written into the plan document AND read
 * back out of it — a panel that merely echoed its own input would keep saying what it said before.
 */

import { openBrowser, newPage, Checks, BASE } from "./lib/harness.mjs";

const checks = new Checks("plan · objective variants");
const browser = await openBrowser();
const page = await newPage(browser);

/** Arm a tool from one of the dock's flyout families. */
async function pick(family, option) {
  await page.click(`.canvas-dock-chevron[aria-label="${family}"]`);
  await page.click(`.canvas-dock-flyout-row:has-text("${option}")`);
  await page.waitForTimeout(120);
}

// A field's label carries its hint in the same element ("Float (air under the casing)"), so the match is
// by substring rather than exact text.
const field = (label) => page.locator(`.field:has(.field-label:has-text("${label}")) input`).first();
const control = (label, tag) => page.locator(`.field:has(.field-label:has-text("${label}")) ${tag}`).first();
const body = () => page.textContent(".workspace");

// A blank plan opens at the origin with rot_180 — an order-2 symmetry, which is what offers the objective
// tools at all (a goal one team defends only means something when there is exactly one other).
await page.goto(`${BASE}/plan-editor`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".canvas-dock", { timeout: 20000 });
await page.waitForSelector(".map-canvas-svg", { timeout: 20000 });

const canvas = await page.locator(".map-canvas-svg").boundingBox();
const midX = canvas.x + canvas.width / 2;
const midZ = canvas.y + canvas.height / 2;

// ── a piece to stand on, then a core on it ────────────────────────────────────────────────────────────
checks.section("a core placed on a piece opens its casing panel");

await pick("Pieces", "Piece");
await page.mouse.move(midX - 120, midZ - 80);
await page.mouse.down();
await page.mouse.move(midX + 120, midZ + 80, { steps: 8 });
await page.mouse.up();
await page.waitForTimeout(250);

await pick("Markers", "Core");
await page.mouse.click(midX, midZ);
await page.waitForTimeout(300);

const opened = await body();
checks.add("the panel names the casing's parts", /Footprint/.test(opened) && /Wall thickness/.test(opened));
checks.add("and the float/leak pair", /Float/.test(opened) && /Leak/.test(opened));
// Defaults are served, not hardcoded on the client — 5×5, shell 1, float 6, leak 5.
checks.add("it opens on the generator's defaults, not on blanks",
  await field("Footprint").inputValue() === "5" && await field("Wall thickness").inputValue() === "1",
  `${await field("Footprint").inputValue()} / ${await field("Wall thickness").inputValue()}`);
checks.add("the lava starts capped", /capped by the casing/.test(opened));
checks.add("and leak 5 under float 6 means no digging", /no digging/.test(opened));

// ── a change reaches the document and comes back ──────────────────────────────────────────────────────
checks.section("a varied core states what it varies");

await field("Leak").fill("9");
await field("Leak").blur();
await page.waitForTimeout(300);
// leak 9 over float 6: the sentence can only say 4 if the plan document carries the new leak. The lava
// free-falls to the first air cell over the terrain and PGM tests its centre, so a block resting exactly at
// the leak level is half a block too high and the core leaks one course lower than the number reads.
checks.add("the dig depth follows leak through the document", /digging 4 blocks/.test(await body()));

await page.click('.field:has(.field-label:has-text("Lava")) .ctrl-row');
await page.waitForTimeout(300);
checks.add("the lava can be left flush with the rim", /open top/.test(await body()));

await field("Footprint").fill("7");
await field("Footprint").blur();
await page.waitForTimeout(300);
checks.add("the casing keeps its new footprint", await field("Footprint").inputValue() === "7");

// ── the destroyable's designs ─────────────────────────────────────────────────────────────────────────
checks.section("a destroyable offers the designs the stamper can build");

await pick("Markers", "Destroyable");
await page.mouse.click(midX + 40, midZ + 40);
await page.waitForTimeout(300);

const designs = await page.locator('.field:has(.field-label:has-text("Design")) option').allTextContents();
checks.add("all six designs are offered", designs.length === 6, designs.join(" · "));
checks.add("including the pillars and the cubes",
  designs.includes("pillar-3") && designs.includes("cube-4") && designs.includes("column-plus"),
  designs.join(" · "));

const materials = await page.locator('.field:has(.field-label:has-text("Material")) option').allTextContents();
checks.add("and only materials the stamper builds", materials.length === 4, materials.join(" · "));

await control("Design", "select").selectOption("cube-4");
await page.waitForTimeout(300);
const chosen = await control("Design", "select").inputValue();
checks.add("a chosen design survives the round trip", chosen === "cube-4", chosen);

checks.add("varying an objective raised nothing", page.faults.length === 0, page.faults.join(" | "));

await browser.close();
checks.finish();
