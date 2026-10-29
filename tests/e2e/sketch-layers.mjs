/**
 * The Sketch tool's layers live in the sidebar: a list of the board's floors, highest first, each with its base y
 * and an eye; the open one in the inspector, where it is renamed, raised and deleted. What is held here: a layer
 * added lands above, the list orders itself by base y, a picked row opens the layer in the inspector with what
 * it holds, the eye takes another layer's ghost off the drawing and puts it back, the eye of the layer drawn on is
 * closed, a delete from the inspector removes the row, and the canvas carries no layer chrome of its own.
 */

import { openBrowser, newPage, Checks, api, BASE } from "./lib/harness.mjs";

const checks = new Checks("sketch layers");
const draft = await api("/sketch", { method: "POST", body: { name: "E2E layers" } });
const browser = await openBrowser();
const page = await newPage(browser);

const rows = () => page.locator(".workspace-sidebar .layer-row");
const names = async () => (await rows().locator(".geo-label").allTextContents()).map(text => text.trim());
const bases = async () => (await rows().locator(".layer-row-y").allTextContents()).map(text => text.trim());
const row = (name) => rows().filter({ has: page.locator(".geo-label", { hasText: new RegExp(`^${name}$`) }) });

try {
  await page.goto(`${BASE}/maps/${draft.slug}/sketch?phase=draw&step=ground`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".workspace-sidebar .layer-row", { timeout: 30000 });

  checks.section("a new layer lands above, and the list reads highest first");
  const add = page.locator('.workspace-sidebar button[title="Add a layer above"]');
  await add.click();
  await page.waitForFunction(() => document.querySelectorAll(".layer-row").length === 2, null, { timeout: 10000 });
  await add.click();
  await page.waitForFunction(() => document.querySelectorAll(".layer-row").length === 3, null, { timeout: 10000 });
  checks.add("three floors, highest first", (await names()).join() === "Layer 3,Layer 2,Ground", (await names()).join());
  checks.add("each with its base", (await bases()).join() === "y 20,y 10,y 0", (await bases()).join());
  checks.add("the newest is the one drawn on", (await row("Layer 3").getAttribute("class")).includes("layer-row--active"));
  checks.add("the canvas draws no layer chrome", (await page.locator(".svg-area ~ * .layer-row, .canvas-layer-strip").count()) === 0);

  checks.section("a shape drawn on the top floor");
  await page.click('.canvas-dock button[aria-label="Rectangle"]');
  const area = await page.locator(".svg-area").boundingBox();
  await page.mouse.move(area.x + area.width * 0.42, area.y + area.height * 0.3);
  await page.mouse.down();
  await page.mouse.move(area.x + area.width * 0.6, area.y + area.height * 0.55, { steps: 8 });
  await page.mouse.up();
  await page.waitForFunction(() => document.querySelectorAll(".workspace-sidebar .geo-row:not(.layer-row)").length > 0, null,
    { timeout: 10000 }).catch(() => {});

  checks.section("a picked row opens the layer in the inspector");
  await row("Layer 3").locator(".geo-label").click();
  await page.waitForSelector(".workspace-inspector .detail-label:text-is('Layer')", { timeout: 10000 }).catch(() => {});
  const inspector = page.locator(".workspace-inspector");
  checks.add("the inspector is the layer's", (await inspector.locator("input.field-input").first().inputValue()) === "Layer 3");
  const holds = ((await inspector.locator(".inspector-readout").textContent().catch(() => "")) ?? "").trim();
  checks.add("and says what it holds", /^Holds 1 shape in 1 group\.$/.test(holds), holds);
  checks.add("with its delete at the foot", (await inspector.locator(".inspector-delete button", { hasText: "Delete this layer" }).count()) === 1);

  checks.section("the eye takes another floor off the drawing");
  await row("Ground").locator(".geo-label").click();
  await page.waitForFunction(() => document.querySelector(".layer-row--active .geo-label")?.textContent.trim() === "Ground", null,
    { timeout: 10000 });
  await page.mouse.move(area.x + 4, area.y + area.height - 4);
  await page.waitForTimeout(500);
  const shown = await page.locator(".svg-area").screenshot();
  const eye = (name) => row(name).locator(".stack-row-tools button");
  await row("Layer 3").hover();
  await eye("Layer 3").click();
  await page.mouse.move(area.x + 4, area.y + area.height - 4);
  await page.waitForTimeout(500);
  const hidden = await page.locator(".svg-area").screenshot();
  checks.add("hiding Layer 3 takes its ghost off", !shown.equals(hidden));
  checks.add("and its row says so", (await row("Layer 3").getAttribute("class")).includes("stack-row--off"));
  await row("Layer 3").hover();
  await eye("Layer 3").click();
  await page.mouse.move(area.x + 4, area.y + area.height - 4);
  await page.waitForTimeout(500);
  checks.add("showing it puts the ghost back", !hidden.equals(await page.locator(".svg-area").screenshot()));
  checks.add("and its row is on again", !(await row("Layer 3").getAttribute("class")).includes("stack-row--off"));
  await row("Ground").hover();
  const own = eye("Ground");
  checks.add("the eye of the floor drawn on is closed, and says why",
    await own.isDisabled() && /always shown/.test((await own.getAttribute("title")) ?? ""), await own.getAttribute("title"));

  checks.section("raising a layer moves its row; deleting it from the inspector removes it");
  await row("Layer 2").locator(".geo-label").click();
  await page.waitForFunction(() => document.querySelector(".layer-row--active .geo-label")?.textContent.trim() === "Layer 2", null,
    { timeout: 10000 });
  const base = inspector.locator(".field:has(.field-label:has-text(\"Base\")) input");
  await base.fill("40");
  await base.press("Enter");
  await page.waitForFunction(() => document.querySelector(".layer-row .geo-label")?.textContent.trim() === "Layer 2", null,
    { timeout: 10000 }).catch(() => {});
  checks.add("Layer 2 at y 40 heads the list", (await names()).join() === "Layer 2,Layer 3,Ground", (await names()).join());
  await inspector.locator(".inspector-delete button").click();
  await page.waitForFunction(() => document.querySelectorAll(".layer-row").length === 2, null, { timeout: 10000 }).catch(() => {});
  checks.add("deleting it leaves two", (await names()).join() === "Layer 3,Ground", (await names()).join());
  checks.add("and the inspector is no longer a layer's",
    (await inspector.locator(".detail-label:text-is('Layer')").count()) === 0);

  checks.section("every canvas phase lists the layers; only Draw adds one");
  await page.locator('.nav-btn:has(.nav-btn-caption:text-is("Palette"))').click();
  await page.waitForTimeout(800);
  checks.add("Palette lists them", (await rows().count()) === 2);
  checks.add("and offers no add", (await page.locator('.workspace-sidebar button[title="Add a layer above"]').count()) === 0);

  checks.add("the page raised no faults", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
} catch (e) {
  page.faults.push(`sketch layers: ${String(e).split("\n")[0]}`);
  checks.add("the checks ran", false, page.faults.slice(0, 3).join(" | "));
}

await browser.close();
checks.finish();
