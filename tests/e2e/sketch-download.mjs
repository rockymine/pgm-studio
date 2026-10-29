/**
 * The Sketch tool hands over a finished map: Download (an icon in the editor bar) builds the world the first time it is pressed and
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
const download = page.locator('.editor-bar button[aria-label^="Download map"]');

checks.section("Download builds the world and saves the export");
checks.add("the map starts without a world", !(await api(`/map/${unbuilt.slug}/state`)).artifacts.world);
await page.goto(`${BASE}/maps/${unbuilt.slug}/sketch?phase=history`, { waitUntil: "networkidle" });
await download.waitFor({ timeout: 60000 });
const [saved] = await Promise.all([page.waitForEvent("download", { timeout: 180000 }), download.click()]);
checks.add("the browser is handed the world ZIP", saved.suggestedFilename() === `${unbuilt.slug}.zip`,
  saved.suggestedFilename());
checks.add("the map now has its world", (await api(`/map/${unbuilt.slug}/state`)).artifacts.world);

checks.section("a map with no game settings says why and saves nothing");
// A draft of its own: the layout alone, with no intent beside it.
const unset = await api("/sketch", { method: "POST", body: { name: "E2E no settings" } });
await api(`/map/${unset.slug}/sketch`, { method: "PUT", body: compiled.layout });
let unexpected = null;
page.on("download", (file) => { unexpected = file.suggestedFilename(); });
await page.goto(`${BASE}/maps/${unset.slug}/sketch`, { waitUntil: "networkidle" });
await download.waitFor({ timeout: 60000 });
await download.click();
const problems = page.locator(".problems-pop");
await problems.waitFor({ timeout: 180000 });
const button = (await page.locator(".problems-btn").first().textContent()) ?? "";
checks.add("the problems button says the map can't be downloaded", button.includes("Download blocked"), button.trim());
const said = (await problems.textContent()) ?? "";
checks.add("the refusal is the export's own sentence", said.includes("spawn"), said.trim());
checks.add("nothing is saved", unexpected === null, unexpected ?? "");
const configure = problems.locator("a", { hasText: "Open Configure" }).first();
checks.add("it offers Configure, where the game is set up",
  (await configure.getAttribute("href"))?.endsWith(`maps/${unset.slug}/configure`) ?? false);
clearFaults(page);   // the export's 409 is the refusal this section asked for

checks.section("a board whose sides no walk joins says so before Download, and the export refuses it");
// Two plates two blocks apart with nothing drawn or buildable between them: a jump clears the gap, a walk does not.
const cut = await api("/sketch", { method: "POST", body: { name: "E2E cut board" } });
await api(`/map/${cut.slug}/sketch`, { method: "PUT", body: {
  setup: { mirror_mode: "none", center: { cx: 0, cz: 0 } },
  layers: [{ id: "ground", name: "Ground", base_y: 0, layout: {
    shapes: [
      { id: "west", type: "rectangle", operation: "add", min_x: -40, min_z: -20, max_x: -1, max_z: 20, floor: 0, base_height: 1 },
      { id: "east", type: "rectangle", operation: "add", min_x: 1, min_z: -20, max_x: 40, max_z: 20, floor: 0, base_height: 1 }],
    groups: [{ id: "west-g", name: "West", mirrors: false, shapeIds: ["west"] },
             { id: "east-g", name: "East", mirrors: false, shapeIds: ["east"] }] } }] } });
await api(`/map/${cut.slug}/intent`, { method: "PUT", body: {
  meta: { authors: [{ name: "E2E fixture" }] },
  teams: [{ id: "red-team", name: "Red", color: "red" }, { id: "blue-team", name: "Blue", color: "blue" }],
  spawns: [
    { team: "red-team", point: { x: -30.5, y: 2, z: 0.5 }, yaw: 270, protection: [{ minX: -36, minZ: -5, maxX: -25, maxZ: 6 }] },
    { team: "blue-team", point: { x: 30.5, y: 2, z: 0.5 }, yaw: 90, protection: [{ minX: 25, minZ: -5, maxX: 36, maxZ: 6 }] }],
  wools: [
    { owner: "red-team", color: "red", spawn: { x: -15.5, y: 2, z: 0.5 }, protection: [{ minX: -20, minZ: -4, maxX: -11, maxZ: 5 }] },
    { owner: "blue-team", color: "blue", spawn: { x: 15.5, y: 2, z: 0.5 }, protection: [{ minX: 11, minZ: -4, maxX: 20, maxZ: 5 }] }],
} });
const findings = await api(`/map/${cut.slug}/findings`);
checks.add("the studio's rules find no route to either wool",
  findings.findings.filter((finding) => finding.rule === "PL9").length === 2,
  findings.findings.map((finding) => finding.rule).join(", "));
unexpected = null;
await page.goto(`${BASE}/maps/${cut.slug}/sketch`, { waitUntil: "networkidle" });
const blocked = page.locator(".problems-btn", { hasText: "Download blocked" });
await blocked.waitFor({ timeout: 60000 });
checks.add("the problems button says so before Download is pressed", true);
await blocked.click();
const listed = (await page.locator(".problems-pop").textContent()) ?? "";
checks.add("and the list carries the rule", listed.includes("PL9"), listed.trim().slice(0, 200));
await page.locator(".problems-pop button[title='Close']").click();
await download.click();
await page.locator(".problems-pop").waitFor({ timeout: 180000 });
checks.add("Download is refused and nothing is saved", unexpected === null, unexpected ?? "");
checks.add("a play rule is not sent to Configure",
  await page.locator(".problems-pop a", { hasText: "Open Configure" }).count() === 0);
clearFaults(page);   // the export's 409 is the refusal this section asked for

checks.section("Draw's Next goes to Layout, and Layout's on to Terraform");
await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch`, { waitUntil: "networkidle" });
const next = page.locator(".editor-bar", { hasText: "Draw" }).locator("button", { hasText: "Next" });
await next.click();
await page.locator(".nav-rail .nav-step[aria-current]", { hasText: "Layout" }).waitFor({ timeout: 30000 });
checks.add("Ground's next step is Layout", true);
await next.click();
await page.locator(".editor-bar", { hasText: "Terraform" }).waitFor({ timeout: 30000 });
checks.add("Layout's next phase is Terraform", true);

await api(`/map/${unbuilt.slug}`, { method: "DELETE" });
await api(`/map/${unset.slug}`, { method: "DELETE" });
await api(`/map/${cut.slug}`, { method: "DELETE" });
checks.add("sketch download is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
