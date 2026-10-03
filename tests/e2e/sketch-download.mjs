/**
 * The Sketch tool hands over a finished map: Download map builds the world the first time it is pressed and
 * saves the export from whichever phase is up, a map with no game settings says why it cannot be exported and
 * saves nothing, and Draw's Done goes on to Terraform rather than leaving the tool.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("sketch download");

// A board built from the seed's plan, carrying the plan's game settings and never built into a world.
const compiled = await api("/plan/compile", { method: "POST", body: seed.planJson });
const unbuilt = await api("/sketch", { method: "POST", body: { name: "E2E download" } });
await api(`/map/${unbuilt.slug}/sketch`, { method: "PUT", body: compiled.layout });
const intent = { ...compiled.intent, meta: { ...compiled.intent.meta, authors: [{ name: "E2E fixture" }] } };
await api(`/map/${unbuilt.slug}/intent`, { method: "PUT", body: intent });

const browser = await openBrowser();
const page = await newPage(browser);
const download = page.locator(".topbar button", { hasText: "Download map" });

checks.section("Download map builds the world and saves the export");
checks.add("the map starts without a world", !(await api(`/map/${unbuilt.slug}/state`)).artifacts.world);
await page.goto(`${BASE}/maps/${unbuilt.slug}/sketch?phase=report`, { waitUntil: "networkidle" });
await download.waitFor({ timeout: 60000 });
const [saved] = await Promise.all([page.waitForEvent("download", { timeout: 180000 }), download.click()]);
checks.add("the browser is handed the world ZIP", saved.suggestedFilename() === `${unbuilt.slug}.zip`,
  saved.suggestedFilename());
checks.add("the map now has its world", (await api(`/map/${unbuilt.slug}/state`)).artifacts.world);

checks.section("a map with no game settings says why and saves nothing");
let unexpected = null;
page.on("download", (file) => { unexpected = file.suggestedFilename(); });
await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
await download.waitFor({ timeout: 60000 });
await download.click();
const warning = page.locator(".topbar-crumb--warn");
await warning.waitFor({ timeout: 180000 });
const said = (await warning.textContent()) ?? "";
checks.add("the refusal is the export's own sentence", said.includes("spawn"), said.trim());
checks.add("nothing is saved", unexpected === null, unexpected ?? "");
const configure = page.locator(".topbar a", { hasText: "Open Configure" });
checks.add("it offers Configure, where the game is set up",
  (await configure.getAttribute("href"))?.endsWith(`maps/${seed.sketchSlug}/configure`) ?? false);
clearFaults(page);   // the export's 409 is the refusal this section asked for

checks.section("Draw's Done goes on to Terraform");
await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
await page.locator(".flow-bar", { hasText: "Draw" }).locator("button", { hasText: "Next" }).click();
await page.locator(".flow-bar", { hasText: "Terraform" }).waitFor({ timeout: 30000 });
checks.add("the next phase is Terraform", true);

await api(`/map/${unbuilt.slug}`, { method: "DELETE" });
checks.add("sketch download is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
