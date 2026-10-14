/**
 * The Configure wizard's objective phases, which are a group rather than a choice.
 *
 * A PGM map may carry wools, destroyables, cores and flags at once, so each is its own phase and every one
 * is offered on every map. The two claims worth holding down in a browser are that the Cores phase
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
 * The faults this spec is responsible for. The author it invents to complete Identity sends the Identity step off
 * to resolve a Minecraft username, which answers 404 with no network — an artifact of the arrangement, and
 * nothing to do with the objective phases.
 */
const featureFaults = () => page.faults.filter(f => !f.includes("/api/minecraft/"));

/** Open the wizard and click a phase on the activity rail by its title. A phase drawing the world canvas is
 * waited on until the canvas has read the map's regions: leaving before then aborts that fetch, and the next
 * phase would be blamed for it. */
async function openPhase(title, { canvas = false } = {}) {
  clearFaults(page);
  await page.goto(`${BASE}/maps/${seed.mapSlug}/configure`, { waitUntil: "networkidle", timeout: 30000 });
  await page.waitForSelector(".nav-btn", { timeout: 20000 });
  const regions = canvas ? page.waitForResponse(r => r.url().includes("/regions/tree"), { timeout: 30000 }) : null;
  await page.click(`.nav-btn[title="${title}"]`);
  await page.waitForSelector(".editor-bar-phase", { timeout: 20000 });
  if (regions) {
    await regions;
    await page.waitForLoadState("networkidle", { timeout: 30000 });
  }
}

const intent = await api(`/map/${seed.mapSlug}/intent`);
const owner = intent.teams?.[0]?.id ?? "";

// ── the Cores phase exists beside Wools, and renders the map's cores ────────────────────────────────
checks.section("a map carrying wools AND a core shows both phases");

await arrange({ cores: [core(owner)] });

await openPhase("Cores", { canvas: true });
const phaseTitle = await page.textContent(".editor-bar-phase span");
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
// leak 9 over float 6 — four blocks of digging (DC2), stated rather than left to arithmetic. The lava
// free-falls to the first air cell over the terrain and PGM tests its centre, so a block resting exactly at
// the leak level is half a block too high and the core leaks one course lower than the number reads.
checks.add("the dig depth is spelled out", /digging 4 blocks/.test(casing));

checks.add("the Cores phase raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

// ── one gate across the objective phases ───────────────────────────────────────────────────────────
checks.section("a core-only map is not held behind an empty wool slice");

await arrange({ wools: [], cores: [core(owner)] });

await openPhase("Review and export");
const reviewTitle = await page.textContent(".editor-bar-phase span");
checks.add("Review is reachable with no wools at all", reviewTitle?.trim() === "Review and export", reviewTitle?.trim());

const locked = await page.$$eval(".nav-btn", els =>
  els.filter(e => e.disabled).map(e => e.getAttribute("title")));
checks.add("no phase is locked", locked.length === 0, locked.join(" · "));

checks.add("the core-only map raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

// ── the Flags phase: a King of the Flag flag shares the same gate ────────────────────────────────────
checks.section("a flag-only map shows its flag and reaches Review");

// No banner stands at the seed world's (0, 9, 0), so the flag's start reads as one PGM would not load.
const flag = {
  name: "Probe Flag", color: "yellow", pointsRate: 1, carrier: "lowered", carrierHearts: 6,
  posts: [{ name: "Mid", at: { x: 0.5, y: 9, z: 0.5 } }],
};
await arrange({ wools: [], flags: [flag] });

await openPhase("Flags", { canvas: true });
await page.waitForLoadState("networkidle", { timeout: 30000 });
const flagTitle = await page.textContent(".editor-bar-phase span");
checks.add("the Flags phase opens", flagTitle?.trim() === "Flags", flagTitle?.trim());
const flagPhase = await page.textContent(".workspace");
checks.add("the stored flag is listed", flagPhase.includes("Probe Flag"));
checks.add("a start with no banner under it says so", flagPhase.includes("Start · no banner"));
checks.add("the Flags phase raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

await openPhase("Review and export");
const flagReview = await page.textContent(".editor-bar-phase span");
checks.add("Review is reachable with a flag as the only objective", flagReview?.trim() === "Review and export",
           flagReview?.trim());

// ── a team's flag, and the Match phase ───────────────────────────────────────────────────────────────
checks.section("a team's flag is defended, and the Match phase states the match");

await arrange({ wools: [], flags: [{ ...flag, team: owner, color: undefined, pointsRate: undefined, carrier: "none" }],
                timeLimit: { duration: "12m", result: "score" }, lives: 2 });

await openPhase("Flags", { canvas: true });
const teamFlag = await page.textContent(".workspace");
const defending = await page.inputValue(".field:has-text('Defending team') select");
checks.add("a team's flag names its defending team", defending === owner, defending);
checks.add("a team's flag states no points rate", !teamFlag.includes("Points per second"));
checks.add("a team's flag says when its team captures", await page.locator(".field:has-text('Capture') select").count() > 0);

await openPhase("Match");
const matchTitle = await page.textContent(".editor-bar-phase span");
checks.add("the Match phase opens", matchTitle?.trim() === "Match", matchTitle?.trim());
const timeBox = await page.inputValue(".workspace input[placeholder='e.g. 10m']");
checks.add("the stored time limit is shown", timeBox === "12m", timeBox);
checks.add("the Match phase raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

await openPhase("Review and export");
const matchReview = await page.textContent(".editor-bar-phase span");
checks.add("Review is reachable past the Match phase", matchReview?.trim() === "Review and export", matchReview?.trim());

// ── the Score phase: a deathmatch is an objective, and a score box is listed ─────────────────────────
checks.section("a board whose kills pay reaches Review, and its score box is listed");

await arrange({ wools: [], flags: [], killPoints: 1 });
await openPhase("Review and export");
const killReview = await page.textContent(".editor-bar-phase span");
checks.add("Review is reachable with kills as the only objective", killReview?.trim() === "Review and export",
           killReview?.trim());

await arrange({
  wools: [], killPoints: 1,
  scoreBoxes: [{ team: owner, box: { minX: 30, minY: 12, minZ: -2, maxX: 34, maxY: 14, maxZ: 2 }, points: 1, home: true }],
});
await openPhase("Score", { canvas: true });
const scorePhase = await page.textContent(".workspace");
checks.add("the stored score box is listed", scorePhase.includes("1 box"), scorePhase.slice(0, 200));
checks.add("the Score phase raised nothing", featureFaults().length === 0, featureFaults().join(" | "));

// Put the seed map back the way the other specs expect it.
await api(`/map/${seed.mapSlug}/intent`, { method: "PUT", body: intent });

await browser.close();
checks.finish();
