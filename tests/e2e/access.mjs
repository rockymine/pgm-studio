/**
 * Who may write, as the browser shows it (docs/access.md).
 *
 * The suite's own server is open, so every page there must stay fully editable and the whitelist page must
 * answer the local admin. A second server over the same database runs invited, where this browser is signed
 * out: every map page must say it is read-only, grey its fields, keep the tools that only look and drop the
 * ones that draw, and the pages that start a map or keep the whitelist must offer nothing to press. Every
 * action that writes (a Button marked Writes, docs/client/ui-conventions.md) is open to the admin and closed to
 * the visitor with the reason on hover, and what only reads stays open to both.
 */

import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { openBrowser, newPage, clearFaults, Checks, readSeed, worldAimer, shapeAimPoints, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("access");
const browser = await openBrowser();
const page = await newPage(browser);

async function visit(base, path) {
  clearFaults(page);
  await page.goto(`${base}${path}`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".app-nav-right .account-local, .app-nav-right .account-signin, .app-nav-right .account", { timeout: 20000 });
}

const readState = () => page.evaluate(() => ({
  banner: document.querySelector(".editor-bar .readonly-tag, .topbar .readonly-tag")?.getAttribute("title") ?? null,
  signIn: !!document.querySelector('.app-nav a.account-signin[href*="api/auth/discord"]'),
  greyed: document.querySelectorAll("fieldset.readonly-fieldset[disabled]").length,
  select: !!document.querySelector('.canvas-dock button[aria-label="Select"]'),
  rectangle: !!document.querySelector('.canvas-dock button[aria-label="Rectangle"]'),
  inspector: document.querySelectorAll(".workspace-inspector, .workspace-scroll").length,
  account: document.querySelector(".app-nav-right")?.textContent?.replace(/\s+/g, " ").trim() ?? "",
  users: !!document.querySelector('.app-nav a[href="admin/users"]'),
}));

/** The first button or action link whose text matches, and whether it is closed: disabled, inside a disabled
 *  fieldset, or a link with no href. */
const control = pattern => page.evaluate(source => {
  const labelOf = e => e.textContent.replace(/\s+/g, " ").trim() || e.getAttribute("aria-label") || "";
  const match = new RegExp(source);
  const el = [...document.querySelectorAll("button, a.action-btn, label.action-btn")]
    .find(e => match.test(labelOf(e)));
  if (!el) return null;
  const closed = el.disabled === true || el.getAttribute("aria-disabled") === "true"
    || !!el.closest("fieldset[disabled]") || (el.tagName === "A" && !el.hasAttribute("href"));
  return { closed, title: el.getAttribute("title") ?? "" };
}, pattern.source);

/** The control once it has settled into the state expected of it, or as it stands after the wait. */
async function settle(pattern, closed) {
  await page.waitForFunction(([source, want]) => {
    const labelOf = e => e.textContent.replace(/\s+/g, " ").trim() || e.getAttribute("aria-label") || "";
    const match = new RegExp(source);
    const el = [...document.querySelectorAll("button, a.action-btn, label.action-btn")]
      .find(e => match.test(labelOf(e)));
    if (!el) return false;
    const shut = el.disabled === true || el.getAttribute("aria-disabled") === "true"
      || !!el.closest("fieldset[disabled]") || (el.tagName === "A" && !el.hasAttribute("href"));
    return shut === want;
  }, [pattern.source, closed], { timeout: 15000 }).catch(() => {});
  return control(pattern);
}

/** Open the first layout's details on the generator, where its pin and its plan editor are offered. */
async function openFirstLayout() {
  await page.waitForSelector(".gen-card-fig", { timeout: 20000 }).catch(() => {});
  await page.click(".gen-card-fig").catch(() => {});
  await page.waitForSelector(".side-drawer, .drawer", { timeout: 10000 }).catch(() => {});
}

// Every page whose actions write, the actions, and what to do on the page before they are read. A library
// entry is saved only once it has a name, so the admin types one; the visitor's name field is greyed.
const WRITE_PAGES = [
  ["/library/themes", null, [["the library's New button", /^New palette$/]]],
  ["/library/themes/new", async open => {
    await page.waitForSelector(".lib-section", { timeout: 20000 }).catch(() => {});
    if (open) await page.fill("#lib-entry-name", "E2E access palette");
  },
    [["a library editor's save", /^Add to library$/]]],
  [`/plans/${seed.planId}`, null, [["the plan editor's New", /^New$/], ["the plan editor's Import", /^Import$/],
    ["the plan editor's Save", /^Save$/], ["the plan editor's Compile", /^Compile$/]]],
  ["/generator", openFirstLayout, [["the generator's Pin", /^(Pin|Unpin)$/],
    ["the generator's Start a map", /^Start a map$/]]],
];

async function checkWrites(base, open) {
  for (const [path, prepare, controls] of WRITE_PAGES) {
    await visit(base, path);
    await prepare?.(open);
    for (const [name, pattern] of controls) {
      const state = await settle(pattern, !open);
      if (open) checks.add(`${name} is open`, state?.closed === false, JSON.stringify(state));
      else checks.add(`${name} is closed, and says why`, state?.closed === true && /not signed in/.test(state.title),
        JSON.stringify(state));
    }
  }
}

// ── the suite's own server: open, the local admin ──────────────────────────────────────────────────
checks.section("an open studio stays editable");
await visit(BASE, `/maps/${seed.sketchSlug}/sketch`);
await page.waitForSelector(".canvas-dock", { timeout: 20000 }).catch(() => {});
let state = await readState();
checks.add("no view-only tag", state.banner === null, state.banner ?? "");
checks.add("no field is greyed", state.greyed === 0, `${state.greyed} disabled fieldset(s)`);
checks.add("the drawing tools are there", state.rectangle);
checks.add("the studio bar names the local admin", /local/.test(state.account), state.account);
await page.click(".app-nav-right .account-local");
state = await readState();
checks.add("the account menu links the whitelist for an admin", state.users);
await page.click(".account-scrim");
const openDownload = await settle(/^Download map/, false);
checks.add("Download is open", openDownload?.closed === false, JSON.stringify(openDownload));
const openIso = await settle(/^2D$/, false);
checks.add("the 3-D switch is open", openIso?.closed === false, JSON.stringify(openIso));

await visit(BASE, "/admin/users");
// The page says "Loading…" until it has asked who is signed in, and only then draws the form or the refusal.
await page.waitForFunction(() => !!document.querySelector('input[placeholder="Minecraft name or uuid"]')
  || /Only admins/.test(document.body.textContent), null, { timeout: 15000 }).catch(() => {});
const adminPage = await page.evaluate(() => ({
  add: !!document.querySelector('input[placeholder="Minecraft name or uuid"]'),
  refused: /Only admins/.test(document.body.textContent),
}));
checks.add("the whitelist page answers the local admin", adminPage.add && !adminPage.refused);
checks.add("no page fault", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

checks.section("an open studio offers every action that writes");
await checkWrites(BASE, true);

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
    await page.waitForSelector(".editor-bar .readonly-tag, .topbar .readonly-tag", { timeout: 20000 }).catch(() => {});
    if (canvas) await page.waitForSelector(".canvas-dock", { timeout: 20000 }).catch(() => {});
    await page.waitForSelector("fieldset.readonly-fieldset", { timeout: 10000 }).catch(() => {});
    state = await readState();
    checks.add("the editor bar says view only, and why", /not signed in/.test(state.banner ?? ""), state.banner ?? "no tag");
    checks.add("the studio bar offers the sign-in", state.signIn);
    checks.add("the panels are greyed", state.greyed > 0,
      `${state.greyed} disabled fieldset(s) over ${state.inspector} panel(s)`);
    if (canvas) checks.add("the tools that only look stay", state.select);
    checks.add("the tools that draw are gone", !state.rectangle);
    checks.add("the studio bar offers a visitor no whitelist", !state.users);
    checks.add("opening it writes nothing and faults nothing", page.faults.length === 0,
      page.faults.slice(0, 3).join(" | "));
  }

  checks.section("a signed-out visitor cannot start a map or keep the whitelist");
  await visit(invited, "/maps?stage=plan");
  await page.waitForSelector("button.action-btn--primary", { timeout: 20000 }).catch(() => {});
  const newPlan = await page.evaluate(() =>
    [...document.querySelectorAll("button")].find(b => /New plan/.test(b.textContent))?.disabled ?? null);
  checks.add("New plan is greyed", newPlan === true, String(newPlan));
  await visit(invited, "/maps?stage=configure");
  const importWorld = await settle(/^Import a world$/, true);
  checks.add("Import a world goes nowhere, and says why", importWorld?.closed === true
    && /Sign in/.test(importWorld.title), JSON.stringify(importWorld));

  checks.section("a signed-out visitor cannot download a map or build its 3-D preview");
  await visit(invited, `/maps/${seed.sketchSlug}/sketch`);
  const download = await settle(/^Download map/, true);
  checks.add("Download is closed, and says why", download?.closed === true && /Sign in/.test(download.title),
    JSON.stringify(download));
  const exported = await fetch(`${invited}/api/map/${seed.sketchSlug}/export`);
  checks.add("the export itself is refused", exported.status === 401, String(exported.status));
  // The 3-D preview is a build too, so the switch is greyed before it is pressed rather than after it fails.
  const iso = await settle(/^2D$/, true);
  checks.add("the 3-D switch is closed, and says why", iso?.closed === true && /Sign in/.test(iso.title),
    JSON.stringify(iso));

  checks.section("a signed-out visitor's sidebar and canvas change nothing");
  await visit(invited, `/maps/${seed.sketchSlug}/sketch`);
  await page.waitForSelector(".canvas-dock", { timeout: 20000 }).catch(() => {});
  await page.waitForFunction(() => !!document.querySelector(".workspace-sidebar input.field-input"), null,
    { timeout: 15000 }).catch(() => {});
  const layerName = await page.evaluate(() => {
    const input = document.querySelector(".workspace-sidebar input.field-input");
    return input ? { disabled: input.matches(":disabled"), title: input.closest("fieldset")?.getAttribute("title") ?? "" } : null;
  });
  checks.add("the layer's name is greyed in the sidebar, and says why", layerName?.disabled === true
    && /not signed in/.test(layerName.title), JSON.stringify(layerName));

  // Pick a group from the sidebar, then drag it on the canvas with the select tool, from a point inside it. The
  // canvas and its chrome are compared with the pointer resting where the drag ends both times, so a hover
  // cannot be read as a move.
  await page.click('.canvas-dock button[aria-label="Select"]').catch(() => {});
  const stored = await fetch(`${invited}/api/map/${seed.sketchSlug}/sketch`).then(r => r.json());
  const shapes = (stored?.layers?.[0]?.layout?.shapes ?? stored?.layout?.shapes ?? [])
    .filter(shape => !shape.role && shape.operation !== "subtract");
  const aim = await worldAimer(page);
  const target = aim && shapes.flatMap(shape => shapeAimPoints(shape))[0];
  const writes = [];
  const onRequest = request => {
    if (/^(PUT|PATCH|DELETE)$/.test(request.method()) && request.url().includes("/api/")) writes.push(`${request.method()} ${request.url()}`);
  };
  let picked = false, still = false;
  if (target) {
    const from = aim(target.x, target.z);
    const to = { x: from.x + 90, y: from.y + 60 };
    const chrome = () => page.evaluate(() =>
      [...document.querySelectorAll('.svg-area svg [stroke-dasharray="5 3"]')]
        .map(el => JSON.stringify(el.getBoundingClientRect())).join("|"));
    await page.click(".workspace-sidebar .geo-row").catch(() => {});
    await page.waitForTimeout(600);
    await page.mouse.move(to.x, to.y);
    await page.waitForTimeout(400);
    const boxBefore = await chrome();
    picked = boxBefore.length > 0;
    const before = await page.locator(".svg-area").screenshot();
    page.on("request", onRequest);
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(to.x, to.y, { steps: 8 });
    await page.mouse.up();
    await page.waitForTimeout(1500);   // longer than the save's debounce
    const after = await page.locator(".svg-area").screenshot();
    still = before.equals(after) && (await chrome()) === boxBefore;
    page.off("request", onRequest);
  }
  checks.add("a group is still picked", picked, target ? "" : "no shape to aim at");
  checks.add("dragging it moves nothing", picked && still);
  checks.add("and sends nothing", writes.length === 0, writes.slice(0, 3).join(" | "));

  checks.section("a signed-out visitor is offered nothing that writes");
  await checkWrites(invited, false);
  await visit(invited, `/plans/${seed.planId}`);
  const openPlan = await settle(/^Open$/, false);
  checks.add("the plan editor's Open still reads", openPlan?.closed === false, JSON.stringify(openPlan));

  await visit(invited, "/admin/users");
  await page.waitForFunction(() => /Only admins|Minecraft name/.test(document.body.textContent), null,
    { timeout: 20000 }).catch(() => {});
  const refused = await page.evaluate(() => /Only admins/.test(document.body.textContent)
    && !document.querySelector('input[placeholder="Minecraft name or uuid"]'));
  checks.add("the whitelist page shows nothing of the list", refused);
} finally {
  server.kill();
}

checks.finish();
await browser.close();
