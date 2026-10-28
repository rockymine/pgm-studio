/**
 * Draw's Places step: the places are drawn over the board with the notes step's tools, listed, named, and stored as
 * a change in the map's history. The step arrives once the caller is known to be an admin, and a step clicked on
 * the bar drawn before it lands where it was clicked.
 */

import { mkdir } from "node:fs/promises";
import { openBrowser, newPage, Checks, readSeed, api, BASE, TMP_DIR } from "./lib/harness.mjs";

const OUT = TMP_DIR;
await mkdir(OUT, { recursive: true });

const seed = await readSeed();
const checks = new Checks("sketch places");

const browser = await openBrowser();
const page = await newPage(browser);

checks.section("Draw opens on Ground, and Places comes before it");
await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
await page.locator(".nav-rail .nav-step[aria-current]", { hasText: "Ground" }).waitFor({ timeout: 60000 });
await page.locator(".nav-rail .nav-step", { hasText: "Places" }).waitFor({ timeout: 30000 }).catch(() => {});
const steps = await page.$$eval(".nav-rail .nav-step", els => els.map(e => e.textContent.trim()));
checks.add("Draw's steps are Places, Ground and Layout", steps.join(",") === "Places,Ground,Layout", steps.join(","));
await page.click(".nav-rail .nav-step >> text=Places");
await page.locator(".nav-rail .nav-step[aria-current]", { hasText: "Places" }).waitFor({ timeout: 30000 });

checks.section("a box drawn in Places is a place, stored and listed");
await page.click('.canvas-dock button[aria-label="Box"]');
const area = await page.locator(".svg-area").boundingBox();
const from = { x: area.x + area.width * 0.45, y: area.y + area.height * 0.45 };
const to = { x: area.x + area.width * 0.55, y: area.y + area.height * 0.55 };
await page.mouse.move(from.x, from.y);
await page.mouse.down();
await page.mouse.move(to.x, to.y, { steps: 8 });
await page.mouse.up();
await page.locator(".panel-list .list-label", { hasText: "Place 1" }).waitFor({ timeout: 30000 });
const stored = await api(`/map/${seed.sketchSlug}/places`);
const place = stored.places?.[0];
checks.add("the place is stored as a box", place?.shape === "box" && place.points?.length === 2, JSON.stringify(place));
const changes = await api(`/map/${seed.sketchSlug}/changes`);
const last = changes.changes.at(-1);
checks.add("its write is a change naming the places", last.documents.includes("places"), last.documents.join(","));

checks.section("a place is renamed in its panel");
const name = page.locator(".workspace-inspector input.field-input").first();
await name.fill("Abbey Hill");
await name.press("Enter");
await page.locator(".panel-list .list-label", { hasText: "Abbey Hill" }).waitFor({ timeout: 30000 });
checks.add("the name is stored", (await api(`/map/${seed.sketchSlug}/places`)).places[0].name === "Abbey Hill");

checks.section("the place's panel is one conversation, and a note written in it is pinned to the place");
checks.add("the panel has no New note step and no filters",
  !(await page.locator(".workspace-inspector .notes-column button:has-text('New note')").count())
  && !(await page.locator(".workspace-inspector .notes-column__filters").count()));
await page.fill(".workspace-inspector #note-reply", "Is the hill tall enough to hold?");
await page.click(".workspace-inspector .notes-column button:has-text('Send')");
let notes = [];
for (let tries = 0; tries < 30 && !notes.some(note => note.anchor.kind === "place"); tries++) {
  await page.waitForTimeout(500);
  notes = await api(`/map/${seed.sketchSlug}/notes`);
}
const pinned = notes.find(note => note.anchor.kind === "place");
checks.add("the note is pinned to the place", pinned?.anchor.place === place?.id,
  JSON.stringify(pinned?.anchor ?? notes.map(note => note.anchor)));
await page.locator(".workspace-inspector .notes-column__thread", { hasText: "Is the hill tall enough" })
  .waitFor({ timeout: 30000 }).catch(() => {});
checks.add("the conversation shows the note",
  await page.locator(".workspace-inspector .notes-column__thread", { hasText: "Is the hill tall enough" }).count() > 0);
await page.fill(".workspace-inspector #note-reply", "And the east face?");
await page.click(".workspace-inspector .notes-column button:has-text('Send')");
await page.locator(".workspace-inspector .notes-column__thread", { hasText: "And the east face?" })
  .waitFor({ timeout: 30000 }).catch(() => {});
const threads = (await api(`/map/${seed.sketchSlug}/notes`)).filter(note => note.anchor.kind === "place");
checks.add("a second message answers the place's thread", threads.length === 1 && threads[0].messages.length === 2,
  JSON.stringify(threads.map(note => note.messages.length)));
checks.add("the place's row carries a read speech bubble",
  await page.locator(".panel-list .note-pin--read").count() === 1);
await page.waitForTimeout(800);
await page.screenshot({ path: `${OUT}sketch-places.png` });


await api(`/map/${seed.sketchSlug}/places`, { method: "PUT", body: {} });

checks.section("a step clicked while the Places step is on its way lands where it was clicked");
// The places read is held for eight seconds, which holds the bar between learning the caller is an admin and
// drawing the Places step; Layout is clicked inside that window, on the bar as it stands.
const { slug: lateSlug } = await api("/sketch", { method: "POST", body: { name: "E2E late places step" } });
const late = await newPage(browser, { width: 1600, height: 1000 });
await late.route("**/api/map/*/places", async route => {
  await new Promise(resolve => setTimeout(resolve, 8000));
  await route.continue();
});
const asked = late.waitForRequest(request => request.url().endsWith(`/map/${lateSlug}/places`), { timeout: 60000 });
await late.goto(`${BASE}/maps/${lateSlug}/sketch`, { waitUntil: "domcontentloaded" });
await late.locator(".nav-rail .nav-step[aria-current]", { hasText: "Ground" }).waitFor({ timeout: 60000 });
await asked;
const drawn = await late.$$eval(".nav-rail .nav-step", els => els.map(e => e.textContent.trim()));
await late.click(".nav-rail .nav-step >> text=Layout");
const landed = await late.locator(".nav-rail .nav-step[aria-current]", { hasText: "Layout" }).waitFor({ timeout: 20000 })
  .then(() => true, () => false);
const now = await late.$$eval(".nav-rail .nav-step[aria-current]", els => els.map(e => e.textContent.trim()));
checks.add("Layout is the step it lands on", landed, `bar ${drawn.join(",")} → active ${now.join(",")}`);
checks.add("and the late step raised nothing", late.faults.length === 0, late.faults.slice(0, 3).join(" | "));
await api(`/map/${lateSlug}`, { method: "DELETE" });

checks.section("the address says where the author is");
await page.click(".nav-rail .nav-step >> text=Layout");
await page.locator(".nav-rail .nav-step[aria-current]", { hasText: "Layout" }).waitFor({ timeout: 30000 });
await page.waitForFunction(() => location.search.includes("step=layout"), null, { timeout: 10000 }).catch(() => {});
checks.add("Layout writes phase and step", new URL(page.url()).search === "?phase=draw&step=layout", new URL(page.url()).search);
await page.reload({ waitUntil: "networkidle" });
await page.locator(".nav-rail .nav-step[aria-current]", { hasText: "Layout" }).waitFor({ timeout: 60000 }).catch(() => {});
const reopened = await page.$$eval(".nav-rail .nav-step[aria-current]", els => els.map(e => e.textContent.trim()));
checks.add("a reload opens on Layout", reopened.join() === "Layout", reopened.join());
await page.click(".editor-bar-next");
await page.waitForFunction(() => location.search.includes("terraform"), null, { timeout: 10000 }).catch(() => {});
checks.add("Next names Terraform by its rail word", new URL(page.url()).search === "?phase=terraform", new URL(page.url()).search);

checks.add("sketch places is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
