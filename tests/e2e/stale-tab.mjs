/**
 * A Sketch tab writes only what was drawn in it, and only over the board it read.
 *
 * The tab holds the revision the layout was read at and states it as `If-Match` on every save. A board
 * stored from somewhere else after the tab opened it — an agent driving the API, a second tab — refuses the
 * tab's save `RQ5` at 409, so the older board on screen is never written back over the newer one. And a
 * flush with no edit behind it, entering Review, sends nothing at all.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("stale tab");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const draft = await api("/sketch", { method: "POST", body: { name: "E2E stale tab" } });
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: layout });
const shapesStored = (board) => board.layers.reduce((n, layer) => n + (layer.layout?.shapes?.length ?? 0), 0);
const before = shapesStored(await api(`/map/${draft.slug}/sketch`));

const browser = await openBrowser();
const page = await newPage(browser);
const saves = [];
page.on("response", (response) => {
  const request = response.request();
  if (request.method() === "PUT" && new URL(request.url()).pathname === `/api/map/${draft.slug}/sketch`)
    saves.push({ status: response.status(), ifMatch: request.headers()["if-match"] ?? null });
});

await page.goto(`${BASE}/maps/${draft.slug}/sketch`, { waitUntil: "networkidle" });
await page.waitForSelector("svg.map-canvas-svg", { timeout: 20000 });

// ── nothing drawn, nothing written ────────────────────────────────────────────────────────────────────
checks.section("a flush with no edit behind it sends nothing");

await page.click('button.nav-btn[title="Review"]');
await page.waitForTimeout(1500);
checks.add("entering Review on an untouched board writes nothing", saves.length === 0,
  JSON.stringify(saves));
await page.click('button.nav-btn[title="Draw"]');
await page.waitForSelector('.canvas-dock button[aria-label="Rectangle"]', { timeout: 10000 });

// ── the board moves on without the tab ────────────────────────────────────────────────────────────────
checks.section("a save over a board stored since the tab read it is refused");

// Stored from somewhere else: the same board, so only its revision moves.
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: layout });

const box = await page.locator("svg.map-canvas-svg").boundingBox();
await page.click('.canvas-dock button[aria-label="Rectangle"]');
await page.mouse.move(box.x + box.width * 0.45, box.y + box.height * 0.45);
await page.mouse.down();
await page.mouse.move(box.x + box.width * 0.52, box.y + box.height * 0.52, { steps: 6 });
await page.mouse.up();
await page.waitForTimeout(2500);

checks.add("the tab's save states the revision it read", saves.length > 0 && saves[0].ifMatch !== null,
  JSON.stringify(saves));
checks.add("and is refused 409", saves.length > 0 && saves.every((save) => save.status === 409),
  JSON.stringify(saves));
await page.locator(".problems-btn", { hasText: "Can’t save" }).click().catch(() => {});
const warning = (await page.locator(".problems-pop .problems-verdict").textContent().catch(() => "")) ?? "";
checks.add("the topbar says the board was saved from somewhere else",
  warning.includes("saved from somewhere else"), warning);
checks.add("and the stored board is the one stored from elsewhere",
  shapesStored(await api(`/map/${draft.slug}/sketch`)) === before);

// A second edit sends nothing: the tab knows it is behind.
const refused = saves.length;
await page.mouse.move(box.x + box.width * 0.30, box.y + box.height * 0.30);
await page.mouse.down();
await page.mouse.move(box.x + box.width * 0.36, box.y + box.height * 0.36, { steps: 6 });
await page.mouse.up();
await page.waitForTimeout(2000);
checks.add("no further save is sent until the page is reloaded", saves.length === refused,
  JSON.stringify(saves));

// ── a reload takes the stored board up, and saves land again ──────────────────────────────────────────
checks.section("a reloaded tab saves over the board it read");

await page.reload({ waitUntil: "networkidle" });
await page.waitForSelector("svg.map-canvas-svg", { timeout: 20000 });
const reloaded = saves.length;
const again = await page.locator("svg.map-canvas-svg").boundingBox();
await page.click('.canvas-dock button[aria-label="Rectangle"]');
await page.mouse.move(again.x + again.width * 0.45, again.y + again.height * 0.45);
await page.mouse.down();
await page.mouse.move(again.x + again.width * 0.52, again.y + again.height * 0.52, { steps: 6 });
await page.mouse.up();
await page.waitForTimeout(2500);
const landed = saves.slice(reloaded);
checks.add("its save lands", landed.length > 0 && landed.every((save) => save.status === 200),
  JSON.stringify(landed));
checks.add("and the drawn shape is stored", shapesStored(await api(`/map/${draft.slug}/sketch`)) > before);

// The refused saves are the point of the spec, so they are the one fault it expects.
const faults = page.faults.filter((fault) => fault !== `HTTP 409: ${BASE}/api/map/${draft.slug}/sketch`);
checks.add("sketch tool is otherwise clean", faults.length === 0, faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
