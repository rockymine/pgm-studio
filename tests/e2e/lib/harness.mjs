/**
 * Shared plumbing for the end-to-end specs: a browser, a per-page fault collector, and a check list
 * that decides the exit code.
 *
 * The fault collector is the point of the smoke layer. A page is "healthy" only if it raises no
 * uncaught exception, logs no console error, and issues no failed/4xx-5xx request — the class of
 * break that unit tests cannot see and that hand-verification keeps missing (a boot failure, a route
 * that no longer resolves, a dead asset). Anything tolerated has to be named in ALLOWED_FAULTS with a
 * reason, so the allowance is a decision on the record rather than a silent filter.
 */

import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

/**
 * Playwright, however it happens to be installed. The repo carries no npm dependencies on purpose, so
 * the suite accepts a global install (`npm i -g playwright`) as readily as a local devDependency, and
 * says which to run if it finds neither. PLAYWRIGHT_PACKAGE overrides the lookup outright.
 */
async function loadPlaywright() {
  const tries = [];
  if (process.env.PLAYWRIGHT_PACKAGE) tries.push(process.env.PLAYWRIGHT_PACKAGE);
  tries.push("playwright");
  try {
    const root = execFileSync("npm", ["root", "-g"], { encoding: "utf8" }).trim();
    if (root) tries.push(`${root}/playwright`);
  } catch { /* npm not on PATH — the plain import may still work */ }

  for (const spec of tries) {
    try { return createRequire(import.meta.url)(spec); }
    catch { /* next */ }
  }
  throw new Error(
    "playwright is not installed. Either `npm i -g playwright` (the repo keeps no npm dependencies) "
    + "or `npm i -D playwright`, then re-run. Point PLAYWRIGHT_PACKAGE at it to override the lookup.");
}

const { chromium } = await loadPlaywright();

export const BASE = process.env.E2E_BASE ?? "http://localhost:7895";

/**
 * Faults that are known, explained, and not this suite's business. Keep this list short and justified —
 * every entry is coverage given up.
 */
const ALLOWED_FAULTS = [
  {
    // The icon library is fetched from a public CDN at runtime, so it fails in any offline or
    // egress-restricted environment (icons simply don't render). Tracked as C30 — vendoring it is a
    // decision, not a test fix, so the suite records it rather than pretending it passed.
    match: /cdn\.jsdelivr\.net/,
    why: "lucide is loaded from a CDN (C30) — unreachable offline",
  },
  {
    // ERR_ABORTED is the browser cancelling a request that was still in flight, which on a route sweep is
    // the sweep itself moving on — the page is judged and left while a background fetch is open. It says
    // nothing about the page's health and it fires nondeterministically, so tolerating it removes a flake
    // rather than coverage: a request that actually fails or answers 4xx/5xx is a different event and
    // still fails the sweep.
    match: /request failed \(net::ERR_ABORTED\)/,
    why: "a fetch cancelled by navigating on — an artifact of the sweep, not a page fault",
  },
];

const isAllowed = (text) => ALLOWED_FAULTS.some(a => a.match.test(text));

/** Launch a browser. Honours PW_CHROMIUM when Playwright can't resolve one itself. */
export async function openBrowser() {
  const executablePath = process.env.PW_CHROMIUM || undefined;
  return chromium.launch(executablePath ? { executablePath } : {});
}

/**
 * A page that records everything that went wrong on it. `page.faults` holds the un-allowed ones;
 * `page.allowed` holds the tolerated ones, so a run can still report what it chose to ignore.
 */
export async function newPage(browser, { width = 1600, height = 900 } = {}) {
  const page = await browser.newPage({ viewport: { width, height } });
  page.faults = [];
  page.allowed = [];
  const note = (text) => (isAllowed(text) ? page.allowed : page.faults).push(text);

  page.on("pageerror", (e) => note(`uncaught: ${e?.message ?? e}`));
  page.on("console", (m) => {
    if (m.type() !== "error") return;
    // The browser echoes every failed subresource to the console WITHOUT its URL ("Failed to load
    // resource: net::ERR_…"), which is both unattributable and a duplicate of the requestfailed /
    // 4xx events below — those carry the URL, so they are the ones that decide. Dropping the echo
    // sharpens the signal rather than hiding anything.
    if (/^Failed to load resource\b/.test(m.text())) return;
    note(`console: ${m.text()}`);
  });
  page.on("requestfailed", (r) => note(`request failed (${r.failure()?.errorText}): ${r.url()}`));
  page.on("response", (r) => { if (r.status() >= 400) note(`HTTP ${r.status()}: ${r.url()}`); });
  return page;
}

/** Drop everything recorded so far — call between routes so a fault is attributed to one page. */
export function clearFaults(page) { page.faults = []; page.allowed = []; }

/** A named list of assertions that prints as it goes and sets the process exit code at the end. */
export class Checks {
  #rows = [];
  #title;
  constructor(title) { this.#title = title; }

  add(name, ok, detail = "") {
    this.#rows.push({ name, ok, detail });
    console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}${detail ? `  — ${detail}` : ""}`);
    return ok;
  }

  section(name) { console.log(`\n── ${name} ──`); }

  /** Print the tally and exit non-zero if anything failed. */
  finish() {
    const failed = this.#rows.filter(r => !r.ok);
    console.log(`\n${this.#rows.length - failed.length}/${this.#rows.length} passed — ${this.#title}`);
    if (failed.length) {
      console.log("\nfailures:");
      for (const f of failed) console.log(`  ✗ ${f.name}${f.detail ? `  — ${f.detail}` : ""}`);
    }
    process.exitCode = failed.length ? 1 : 0;
    return failed.length === 0;
  }
}

/** The seed fixtures written by seed.mjs. */
export async function readSeed() {
  const { readFile } = await import("node:fs/promises");
  const path = process.env.E2E_SEED ?? new URL("../../../.tmp/e2e-seed.json", import.meta.url).pathname;
  try { return JSON.parse(await readFile(path, "utf8")); }
  catch { throw new Error(`no seed fixtures at ${path} — run tests/e2e/seed.mjs first (tools/e2e.sh does)`); }
}

/** GET/POST/PUT JSON against the app under test, throwing with the body on a non-2xx. */
export async function api(path, { method = "GET", body } = {}) {
  const res = await fetch(`${BASE}/api${path}`, {
    method,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : (typeof body === "string" ? body : JSON.stringify(body)),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} /api${path} -> ${res.status}: ${text.slice(0, 300)}`);
  return text ? JSON.parse(text) : null;
}

/** Like `api`, but hands back the status instead of throwing — for the refusal specs. */
export async function apiRaw(path, { method = "GET", body } = {}) {
  const res = await fetch(`${BASE}/api${path}`, {
    method,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : (typeof body === "string" ? body : JSON.stringify(body)),
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
  return { status: res.status, text, json };
}
