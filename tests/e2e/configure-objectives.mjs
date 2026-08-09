/**
 * The Configure wizard's objective phases, which are a group rather than a choice.
 *
 * A PGM map may carry wools, destroyables and cores at once, so Cores is its own phase beside Wools and
 * both are offered on every map. The two claims worth holding down in a browser are that the Cores phase
 * renders what the intent states, and that the objective phases share ONE completeness gate — a map whose
 * only objective is a core must not be held behind an empty wool slice, which is exactly what a per-phase
 * "your slice is present" rule did.
 *
 * The seed's configure-stage map arrives with teams, build and wools already authored, so it starts from a
 * real map rather than a hand-built stub. The plan compiler leaves identity and symmetry empty (it does not
 * own either), and the rail unlocks off those, so the spec fills them in as arrangement.
 */

import { openBrowser, newPage, clearFaults, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("configure · objectives");
const browser = await openBrowser();
const page = await newPage(browser);

const CASING = { minX: 40, minY: 20, minZ: 40, maxX: 44, maxY: 24, maxZ: 44 };

/** The core the Cores phase writes: a confirmed suggestion, casing volume and all. */
function core(owner) {
  return {
    owner, name: "Probe Core",
    anchor: { x: 42.5, y: 14, z: 42.5 },
    size: 5, height: 5, shell: 1, float: 6, leak: 9, openTop: false,
    box: CASING,
  };
}

/**
 * Store the map's objectives, with the identity and symmetry the rail unlocks off filled in — the plan
 * compiler owns neither, so a compiled map arrives without them and every phase past World stays locked.
 */
async function arrange(objectives) {
  await api(`/map/${seed.mapSlug}/intent`, {
    method: "PUT",
    body: {
      ...intent,
      meta: { ...intent.meta, authors: [{ name: "Notch" }] },
      symmetry: { mode: "rot_180", centerX: 0, centerZ: 0 },
      ...objectives,
    },
  });
}

/**
 * The faults this spec is responsible for. The author it invents to complete Identity sends the topbar off
 * to resolve a Minecraft username, which answers 404 with no network — an artifact of the arrangement, and
 * nothing to do with the objective phases.
 */
const featureFaults = () => page.faults.filter(f => !f.includes("/api/minecraft/"));

/** Open the wizard and click a phase on the activity rail by its title. */
async function openPhase(title) {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${seed.mapSlug}/configure`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".nav-btn", { timeout: 20000 });
  await page.click(`.nav-btn[title="${title}"]`);
  await page.waitForSelector(".flow-bar-phase", { timeout: 20000 });
}

const intent = await api(`/map/${seed.mapSlug}/intent`);
const owner = intent.teams?.[0]?.id ?? "";

// ── the Cores phase exists beside Wools, and renders the map's cores ────────────────────────────────
checks.section("a map carrying wools AND a core shows both phases");

await arrange({ cores: [core(owner)] });

await openPhase("Cores");
const phaseTitle = await page.textContent(".flow-bar-phase span");
checks.add("the Cores phase opens", phaseTitle?.trim() === "Cores", phaseTitle?.trim());

const railTitles = await page.$$eval(".nav-btn", els => els.map(e => e.getAttribute("title")));
checks.add("Wools and Cores are both on the rail", railTitles.includes("Wools") && railTitles.includes("Cores"),
           railTitles.join(" · "));

const steps = await page.$$eval(".flow-steps .flow-step", els => els.map(e => e.textContent.trim()));
checks.add("the phase walks Objectives → Casing", steps.join(",") === "Objectives,Casing", steps.join(" · "));

const listed = await page.textContent(".workspace");
checks.add("the stored core is listed", listed.includes("Probe Core"));

// The casing volume is the core's region (OB8), so the step must show the box it will export, not a guess.
await page.click(".flow-steps .flow-step:nth-child(2)");
await page.waitForSelector(".workspace", { timeout: 20000 });
const casing = await page.textContent(".workspace");
checks.add("the Casing step shows the region it will export",
           casing.includes(`${CASING.minX}, ${CASING.minY}, ${CASING.minZ}`));
// leak 9 over float 6 — three blocks of digging (DC2), stated rather than left to arithmetic.
checks.add("the dig depth is spelled out", /digging 3 blocks/.test(casing));

checks.add("the Cores phase raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

// ── one gate across the objective phases ───────────────────────────────────────────────────────────
checks.section("a core-only map is not held behind an empty wool slice");

await arrange({ wools: [], cores: [core(owner)] });

await openPhase("Review & Export");
const reviewTitle = await page.textContent(".flow-bar-phase span");
checks.add("Review is reachable with no wools at all", reviewTitle?.trim() === "Review & Export", reviewTitle?.trim());

const locked = await page.$$eval(".nav-btn", els =>
  els.filter(e => e.disabled).map(e => e.getAttribute("title")));
checks.add("no phase is locked", locked.length === 0, locked.join(" · "));

checks.add("the core-only map raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

// Put the seed map back the way the other specs expect it.
await api(`/map/${seed.mapSlug}/intent`, { method: "PUT", body: intent });

await browser.close();
checks.finish();
