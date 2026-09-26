/**
 * Who may write, as the browser shows it (docs/access.md).
 *
 * The suite's own server is open, so every page there must stay fully editable and the whitelist page must
 * answer the local admin. A second server over the same database runs invited, where this browser is signed
 * out: every map page must say it is read-only, grey its fields, keep the tools that only look and drop the
 * ones that draw, and the pages that start a map or keep the whitelist must offer nothing to press.
 */

import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { openBrowser, newPage, clearFaults, Checks, readSeed, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("access");
const browser = await openBrowser();
const page = await newPage(browser);

async function visit(base, path) {
  clearFaults(page);
  await page.goto(`${base}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".topbar-right .account", { timeout: 20000 });
}

const readState = () => page.evaluate(() => ({
  banner: document.querySelector(".readonly-banner")?.textContent?.trim() ?? null,
  signIn: !!document.querySelector('.readonly-banner a[href*="api/auth/discord"]'),
  greyed: document.querySelectorAll("fieldset.readonly-fieldset[disabled]").length,
  select: !!document.querySelector('.canvas-dock button[aria-label="Select"]'),
  rectangle: !!document.querySelector('.canvas-dock button[aria-label="Rectangle"]'),
  inspector: document.querySelectorAll(".workspace-inspector, .workspace-scroll").length,
  account: document.querySelector(".topbar-right .account")?.textContent?.replace(/\s+/g, " ").trim() ?? "",
}));

// ── the suite's own server: open, the local admin ──────────────────────────────────────────────────
checks.section("an open studio stays editable");
await visit(BASE, `/maps/${seed.sketchSlug}/sketch`);
await page.waitForSelector(".canvas-dock", { timeout: 20000 }).catch(() => {});
let state = await readState();
checks.add("no read-only banner", state.banner === null, state.banner ?? "");
checks.add("no field is greyed", state.greyed === 0, `${state.greyed} disabled fieldset(s)`);
checks.add("the drawing tools are there", state.rectangle);
checks.add("the top bar names the local admin", /local/.test(state.account), state.account);

await visit(BASE, "/admin/users");
const adminPage = await page.evaluate(() => ({
  add: !!document.querySelector('input[placeholder="Minecraft name or uuid"]'),
  refused: /Only an admin/.test(document.body.textContent),
}));
checks.add("the whitelist page answers the local admin", adminPage.add && !adminPage.refused);
checks.add("no page fault", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

// ── a second server over the same database, invited, with this browser signed out ─────────────────────
const port = Number(new URL(BASE).port) + 1;
const invited = `http://localhost:${port}`;
const dll = fileURLToPath(new URL("../../src/PgmStudio.Api/bin/Debug/net10.0/PgmStudio.Api.dll", import.meta.url));
const server = spawn("dotnet", [dll, "--urls", `http://0.0.0.0:${port}`],
  { env: { ...process.env, Access__Mode: "invited" }, stdio: "ignore" });
try {
  let up = false;
  for (let i = 0; i < 60 && !up; i++) {
    up = await fetch(`${invited}/api/health`).then(r => r.ok).catch(() => false);
    if (!up) await new Promise(resolve => setTimeout(resolve, 1000));
  }
  checks.add("the invited server came up", up, invited);

  // The sketch opens on its canvas; Configure opens on its info page, which has no canvas to keep a dock on.
  for (const [name, path, canvas] of [["sketch", `/maps/${seed.sketchSlug}/sketch`, true],
                                      ["configure", `/maps/${seed.mapSlug}/configure`, false]]) {
    checks.section(`a signed-out visitor sees the ${name} tool read-only`);
    await visit(invited, path);
    await page.waitForSelector(".readonly-banner", { timeout: 20000 }).catch(() => {});
    if (canvas) await page.waitForSelector(".canvas-dock", { timeout: 20000 }).catch(() => {});
    await page.waitForSelector("fieldset.readonly-fieldset", { timeout: 10000 }).catch(() => {});
    state = await readState();
    checks.add("the banner says why", /not signed in/.test(state.banner ?? ""), state.banner ?? "no banner");
    checks.add("the banner offers the sign-in", state.signIn);
    checks.add("the panels are greyed", state.greyed > 0,
      `${state.greyed} disabled fieldset(s) over ${state.inspector} panel(s)`);
    if (canvas) checks.add("the tools that only look stay", state.select);
    checks.add("the tools that draw are gone", !state.rectangle);
    checks.add("the top bar offers the sign-in", /Sign in/.test(state.account), state.account);
    checks.add("opening it writes nothing and faults nothing", page.faults.length === 0,
      page.faults.slice(0, 3).join(" | "));
  }

  checks.section("a signed-out visitor cannot start a map or keep the whitelist");
  await visit(invited, "/maps?stage=plan");
  await page.waitForSelector("button.action-btn--primary", { timeout: 20000 }).catch(() => {});
  const newPlan = await page.evaluate(() =>
    [...document.querySelectorAll("button")].find(b => /New plan/.test(b.textContent))?.disabled ?? null);
  checks.add("New plan is greyed", newPlan === true, String(newPlan));

  await visit(invited, "/admin/users");
  await page.waitForFunction(() => /Only an admin|Minecraft name/.test(document.body.textContent), null,
    { timeout: 20000 }).catch(() => {});
  const refused = await page.evaluate(() => /Only an admin/.test(document.body.textContent)
    && !document.querySelector('input[placeholder="Minecraft name or uuid"]'));
  checks.add("the whitelist page shows nothing of the list", refused);
} finally {
  server.kill();
}

checks.finish();
await browser.close();
