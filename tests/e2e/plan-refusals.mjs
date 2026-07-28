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
 * Each case strips one slice out of a good plan. `refused: true` means the validator blocks the compile
 * (422 + findings); `refused: false` records that it compiles **today** — the validator gates structural
 * consistency (overlaps, a placement sitting inside its piece, core geometry) but has no *completeness*
 * gate, so an objective-less plan passes. Those are asserted as-is rather than left failing: a green
 * suite is a signal, a permanently red one is noise. If a completeness gate lands, the case flips and
 * this spec fails loudly — which is the point. Open question tracked as B38.
 */
const CASES = [
  { label: "no pieces at all", refused: true,
    mutate: () => { const p = clone(); p.pieces = []; p.zones = []; p.boxes = []; return p; } },
  { label: "no build zones", refused: true,
    mutate: () => { const p = clone(); p.zones = []; return p; } },
  { label: "a marker on a piece that isn't there", refused: true,
    mutate: () => { const p = clone(); if (p.placements.wools[0]) p.placements.wools[0].piece = "no-such-piece"; return p; } },

  // ── compiles today; see B38 ──
  { label: "no spawns", refused: false,
    mutate: () => { const p = clone(); p.placements.spawns = []; return p; } },
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

checks.section("an incomplete plan still compiles — no completeness gate (B38)");
for (const c of CASES.filter(c => !c.refused)) {
  const res = await apiRaw("/plan/compile", { method: "POST", body: c.mutate() });
  checks.add(`still compiles: ${c.label}`, res.status === 200,
    res.status === 200 ? "200 — documented gap, see B38" : `now ${res.status}: a gate landed, flip this case`);
}

checks.section("malformed input is answered, not 500");
for (const [label, body] of [["not json", "{{{"], ["empty", ""], ["an array", "[]"]]) {
  const res = await apiRaw("/plan/compile", { method: "POST", body });
  checks.add(`${label} → 4xx, never 5xx`, res.status >= 400 && res.status < 500, `${res.status}`);
}

checks.section("finish will not rasterize a map with too little land");
// A sketch draft whose layout holds a single small shape cannot make two islands; Finish must say so.
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
  checks.add("finish refuses a one-island layout", fin.status === 422, `${fin.status}: ${(fin.json?.error ?? fin.text).slice(0, 120)}`);
  await apiRaw(`/map/${slug}/sketch/discard-if-empty`, { method: "DELETE" });
} else {
  checks.add("finish refuses a one-island layout", false, `could not create a draft: ${draft.status}`);
}

checks.finish();
