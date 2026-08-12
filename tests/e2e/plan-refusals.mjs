/**
 * The refusal contract: a plan that is missing something a map cannot do without must be turned away
 * with a *reason*, at the earliest gate that can see the problem — never compiled into a half-map and
 * never silently finished.
 *
 * Written by corrupting a real composed plan rather than hand-building broken ones, so each case
 * differs from a known-good document in exactly one way and the refusal can be attributed to it.
 * A case that stops refusing is the interesting result: it means a gate moved.
 */

import { apiRaw, Checks, readSeed } from "./lib/harness.mjs";

const seed = await readSeed();
const good = JSON.parse(seed.planJson);
const clone = () => JSON.parse(JSON.stringify(good));

/**
 * Each case strips one slice out of a good plan. `refused: true` means the compile gate blocks it (422 +
 * findings); `refused: false` records that it compiles **today**, asserted as-is rather than left failing —
 * a green suite is a signal, a permanently red one is noise. A `refused: false` case that starts refusing
 * fails this spec loudly, which is the point: it means a gate moved.
 *
 * The gate asks two questions. Is the plan coherent (the structural validator: overlaps, a placement inside
 * its piece, reachability), and does it carry what a map cannot exist without (completeness: land, a spawn).
 * A missing *objective* is deliberately not a refusal — the goal is the author's, and it can still be set
 * when the map is configured — so it comes back as a warning on the 200 instead.
 */
const CASES = [
  { label: "no pieces at all", refused: true,
    mutate: () => { const p = clone(); p.pieces = []; p.zones = []; p.boxes = []; return p; } },
  { label: "no build zones", refused: true,
    mutate: () => { const p = clone(); p.zones = []; return p; } },
  { label: "a marker on a piece that isn't there", refused: true,
    mutate: () => { const p = clone(); if (p.placements.wools[0]) p.placements.wools[0].piece = "no-such-piece"; return p; } },
  { label: "no spawns", refused: true,
    mutate: () => { const p = clone(); p.placements.spawns = []; return p; } },

  // ── still compiles: incomplete but not un-buildable ──
  { label: "no wools", refused: false,
    mutate: () => { const p = clone(); p.placements.wools = []; return p; } },
  { label: "a piece with a blank id", refused: false,
    mutate: () => { const p = clone(); if (p.pieces[0]) p.pieces[0].id = ""; return p; } },
];

const checks = new Checks("plan refusals");

checks.section("a good plan still compiles");
const okCompile = await apiRaw("/plan/compile", { method: "POST", body: good });
checks.add("the composed plan compiles", okCompile.status === 200,
  okCompile.status === 200 ? "200" : `${okCompile.status}: ${okCompile.text.slice(0, 160)}`);

checks.section("a structurally broken plan is refused, with findings");
for (const c of CASES.filter(c => c.refused)) {
  const res = await apiRaw("/plan/compile", { method: "POST", body: c.mutate() });
  const refused = res.status === 422;
  const named = refused && Array.isArray(res.json?.findings) && res.json.findings.length > 0;
  checks.add(`refuses: ${c.label}`, refused, refused ? "422" : `got ${res.status}`);
  if (refused) {
    const why = (res.json.findings ?? []).map(f => f.message).filter(Boolean)[0] ?? "";
    checks.add(`  …and says why: ${c.label}`, named, why.slice(0, 90));
  }
}

checks.section("an incomplete-but-buildable plan still compiles");
for (const c of CASES.filter(c => !c.refused)) {
  const res = await apiRaw("/plan/compile", { method: "POST", body: c.mutate() });
  checks.add(`still compiles: ${c.label}`, res.status === 200,
    res.status === 200 ? "200" : `now ${res.status}: a gate landed, flip this case`);
}

checks.section("a goalless plan compiles, but says so");
{
  const p = clone();
  p.placements.wools = []; p.placements.destroyables = []; p.placements.cores = [];
  const res = await apiRaw("/plan/compile", { method: "POST", body: p });
  const warned = (res.json?.warnings ?? []).some(w => /objective/i.test(w.message ?? ""));
  checks.add("compiles", res.status === 200, `${res.status}`);
  checks.add("…and warns about the missing objective", warned,
    (res.json?.warnings ?? []).map(w => w.message).join("; ").slice(0, 100) || "no warnings");

  // The warning must not be noise on a plan that does have a goal.
  const good2 = await apiRaw("/plan/compile", { method: "POST", body: good });
  checks.add("no warning on a plan with an objective", (good2.json?.warnings ?? []).length === 0,
    JSON.stringify(good2.json?.warnings ?? []).slice(0, 100));
}

checks.section("an empty plan is refused, not answered with an empty map");
{
  const empty = { plan: 1, globals: { cell: 5 } };
  const compiled = await apiRaw("/plan/compile", { method: "POST", body: empty });
  checks.add("empty plan → 422", compiled.status === 422, `${compiled.status}`);
  const why = (compiled.json?.findings ?? []).map(f => f.message)[0] ?? "";
  checks.add("…naming the missing land", /no pieces/.test(why), why.slice(0, 90));

  // Its sibling endpoints must survive the same document — an empty plan is well-formed, just empty.
  const evaluated = await apiRaw("/plan/evaluate", { method: "POST", body: empty });
  checks.add("evaluate answers an empty plan with an empty score", evaluated.status === 200,
    `${evaluated.status}: ${evaluated.text.slice(0, 120)}`);
  const inspected = await apiRaw("/plan/inspect", { method: "POST", body: empty });
  checks.add("inspect answers an empty plan", inspected.status === 200, `${inspected.status}`);
}

checks.section("malformed input is answered, not 500");
for (const [label, body] of [["not json", "{{{"], ["empty", ""], ["an array", "[]"]]) {
  const res = await apiRaw("/plan/compile", { method: "POST", body });
  checks.add(`${label} → 4xx, never 5xx`, res.status >= 400 && res.status < 500, `${res.status}`);
}

checks.section("finish takes one island, and refuses only bare ground");
// One connected landmass is a map, not a half-drawn one: 17% of the destroy-the-monument corpus is a single
// island and 26% carries a single major one, and the layout generator's own boards compile to exactly one.
// Symmetry decides whether a board has two sides and is stated in the setup, so Finish must accept this and
// refuse only a layout that rasterizes to no ground at all.
const draft = await apiRaw("/sketch", { method: "POST", body: { name: "E2E refusal draft" } });
if (draft.status === 200 && draft.json?.slug) {
  const slug = draft.json.slug;
  const oneShape = {
    setup: { bbox: { min_x: -32, max_x: 32, min_z: -32, max_z: 32 }, center: { cx: 0, cz: 0 }, mirror_mode: "none" },
    layers: [{ id: "l1", name: "Ground", base_y: 0, layout: {
      shapes: [{ id: "s1", type: "rectangle", operation: "add", min_x: 0, max_x: 4, min_z: 0, max_z: 4, base_height: 1, floor: 0 }],
      islands: [],
    } }],
  };
  await apiRaw(`/map/${slug}/sketch`, { method: "PUT", body: oneShape });
  const fin = await apiRaw(`/map/${slug}/sketch/finish`, { method: "POST" });
  checks.add("finish accepts a one-island layout", fin.status === 200, `${fin.status}: ${(fin.json?.error ?? fin.text).slice(0, 120)}`);

  // Nothing drawn is the one thing left to refuse.
  const bare = await apiRaw("/sketch", { method: "POST", body: { name: "E2E bare draft" } });
  if (bare.status === 200 && bare.json?.slug) {
    const bareSlug = bare.json.slug;
    await apiRaw(`/map/${bareSlug}/sketch`, { method: "PUT", body: {
      setup: oneShape.setup,
      layers: [{ id: "l1", name: "Ground", base_y: 0, layout: { shapes: [], islands: [] } }],
    } });
    const bareFin = await apiRaw(`/map/${bareSlug}/sketch/finish`, { method: "POST" });
    checks.add("finish refuses a layout with no ground", bareFin.status === 422,
      `${bareFin.status}: ${(bareFin.json?.error ?? bareFin.text).slice(0, 120)}`);
    await apiRaw(`/map/${bareSlug}/sketch/discard-if-empty`, { method: "DELETE" });
  }
} else {
  checks.add("finish accepts a one-island layout", false, `could not create a draft: ${draft.status}`);
}

checks.finish();
