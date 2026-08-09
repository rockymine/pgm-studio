/**
 * A compile finding points at what it is about.
 *
 * The validator names the subjects of every refusal — the piece, the zone, or (since markers gained ids) the
 * marker itself — and the canvas can pulse them. Until now nothing joined the two: the findings rendered as
 * static rows, their subjects were parsed and dropped, and an author told a core "overhangs the void" had to
 * find that core by eye.
 *
 * The fixture is a plan built to fail one specific way: a 15-block casing on the far edge of its piece, which
 * puts most of its footprint over nothing. That refusal names the core, so the row has something to point at.
 * The API assertion comes first on purpose — if the fixture ever stops being refused, the UI checks below it
 * would pass vacuously against a drawer with no rows in it.
 */

import { openBrowser, newPage, clearFaults, Checks, api, apiRaw, BASE } from "./lib/harness.mjs";

const checks = new Checks("plan · findings");

// One 6×6-cell island, and a core sitting on its right edge wearing a casing three cells across: centred on
// the marker, over half of that footprint hangs past the last block of land.
const plan = {
  plan: 1,
  globals: { cell: 5, symmetry: "rot_180", maxPlayers: 12, surface: 9, headroom: 11 },
  pieces: [{ id: "land", role: "piece", rect: [0, 0, 6, 6] }],
  placements: { cores: [{ id: "core-1", piece: "land", at: [6, 3], size: 15 }] },
};

checks.section("the plan is refused, and the refusal names the core");

const compiled = await apiRaw("/plan/compile", { method: "POST", body: plan });
checks.add("the overhanging core blocks the compile", compiled.status === 422, `${compiled.status}`);

const findings = compiled.json?.findings ?? [];
const overhang = findings.find(f => /overhangs the void/.test(f.message ?? ""));
checks.add("a finding says the goal overhangs the void", overhang !== undefined,
  findings.map(f => f.message).join(" | ") || "no findings");
checks.add("it names the marker, not only its piece", (overhang?.subjects ?? []).includes("core-1"),
  (overhang?.subjects ?? []).join(", ") || "no subjects");

// ── the row, and what clicking it does ────────────────────────────────────────────────────────────────
const browser = await openBrowser();
const page = await newPage(browser);

const slug = (await api("/plan", { method: "POST", body: { name: "e2e findings" } })).slug;
await api(`/map/${slug}/plan`, { method: "PUT", body: plan });

await page.goto(`${BASE}/maps/${slug}/plan`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector("canvas.world-canvas-2d", { timeout: 20000 });
await page.waitForTimeout(800);

checks.section("the finding is offered as something to click");

await page.click('button:has-text("Compile")');
await page.waitForSelector(".plan-compile-errors", { timeout: 20000 });

const row = page.locator('.plan-compile-errors .plan-lint-row', { hasText: "overhangs the void" }).first();
checks.add("the refusal is listed in the compile drawer", await row.count() > 0);
checks.add("and it reads as the marker's problem", /core 'core-1'/.test(await row.textContent()),
  (await row.textContent()).trim());
checks.add("the row is live, not inert",
  !(await row.getAttribute("class")).includes("plan-lint-row--static") && await row.isEnabled());

/**
 * An order-sensitive digest of the board. Pixel *count* carries no signal here — the surface paints an opaque
 * ground, so every pixel is painted before anything is drawn on it and a highlight recolours rather than adds.
 * What moves when the board changes is which colour sits at which offset, which is what this hashes.
 */
const survey = () => page.evaluate(() => {
  const canvas = document.querySelector("canvas.world-canvas-2d");
  const { width, height } = canvas;
  const data = canvas.getContext("2d").getImageData(0, 0, width, height).data;
  let signature = 0;
  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] === 0) continue;
    signature = (signature + data[i] * 3 + data[i + 1] * 5 + data[i + 2] * 7 + i) % 1_000_000_007;
  }
  return signature;
});

checks.section("clicking it shows the core on the board");

// The compile this fixture exists to trigger answers 422, which the page records as a fault. It is the
// expected answer, so it is cleared here — leaving the check below to catch anything the *click* raises.
clearFaults(page);

const before = await survey();
await row.click();
await page.waitForTimeout(200);

// The drawer is modal and dims the board behind it, so the click has to dismiss it — a pulse nobody can see
// is not an answer to "which core?".
checks.add("the drawer gets out of the way", await page.locator(".side-drawer").count() === 0);

const during = await survey();
checks.add("the board is repainted with the highlight", during !== before, `${before} → ${during}`);

// The pulse is transient by design (1.6s), and it leaves the board exactly as it found it — which also says
// the difference above was the highlight rather than some unrelated repaint that happened to land there.
await page.waitForTimeout(2200);
checks.add("the highlight clears itself", await survey() === before, `${before} → ${during} → ${await survey()}`);

checks.add("showing a finding raised nothing", page.faults.length === 0, page.faults.join(" | "));

await browser.close();
checks.finish();
