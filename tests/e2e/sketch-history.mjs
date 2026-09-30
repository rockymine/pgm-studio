/**
 * The Sketch tool's History phase: a board's changes are listed newest first, the latest one is drawn over the
 * board as it opens, the inspector says what it did, and putting the board back lands as a new change whose
 * layout is the one put back.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("sketch history");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const draft = await api("/sketch", { method: "POST", body: { name: "E2E history" } });
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: layout });

// The change an agent would make: the first shape moved over by eight blocks, stored from outside the tab.
const moved = structuredClone(layout);
const shape = moved.layers.flatMap((layer) => layer.layout?.shapes ?? []).find((candidate) => !candidate.role);
for (const key of ["min_x", "max_x", "center_x"]) if (typeof shape[key] === "number") shape[key] += 8;
if (Array.isArray(shape.vertices)) shape.vertices = shape.vertices.map(([x, z]) => [x + 8, z]);
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: moved });
const before = (await api(`/map/${draft.slug}/changes`)).changes;
const [put, edited] = before.slice(-2).map((change) => change.number);

const browser = await openBrowser();
const page = await newPage(browser);
await page.goto(`${BASE}/maps/${draft.slug}/sketch?phase=history`, { waitUntil: "networkidle" });
await page.waitForSelector("svg.map-canvas-svg", { timeout: 20000 });

checks.section("the changes are listed and the latest is drawn");
await page.waitForSelector(".list-row", { timeout: 15000 });
const rows = await page.locator(".list-row").count();
checks.add("every change is a row", rows === before.length, `${rows} rows for ${before.length} changes`);
const picked = (await page.locator(".list-row--selected").first().textContent().catch(() => "")) ?? "";
checks.add("the latest change is picked", picked.includes(`#${edited}`), picked.trim());
await page.waitForSelector(".change-edit", { timeout: 15000 });
const said = await page.locator(".change-edit").allTextContents();
checks.add("the inspector names the shape the change moved", said.some((line) => line.includes(shape.id)),
  said.slice(0, 3).join(" | "));
await page.waitForSelector(".change-world", { timeout: 60000 });
const built = (await page.locator(".change-world").textContent()) ?? "";
checks.add("and counts the ground it moved", /ground [1-9]/.test(built), built.trim());

checks.section("putting the board back lands as a new change");
await page.click("text=Put the board back as it stood at");
await page.waitForFunction(
  (count) => document.querySelectorAll(".list-row").length > count, before.length, { timeout: 20000 });
const after = (await api(`/map/${draft.slug}/changes`)).changes;
const landed = after[after.length - 1];
checks.add("the restore is the board's newest change", landed.number > edited && landed.note === `restores change ${put}`,
  JSON.stringify(landed));
const stored = await api(`/map/${draft.slug}/sketch`);
const back = stored.layers.flatMap((layer) => layer.layout?.shapes ?? []).find((candidate) => candidate.id === shape.id);
const original = layout.layers.flatMap((layer) => layer.layout?.shapes ?? []).find((candidate) => candidate.id === shape.id);
checks.add("and the stored shape stands where it stood before the move", JSON.stringify(back) === JSON.stringify(original));

checks.add("sketch history is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
