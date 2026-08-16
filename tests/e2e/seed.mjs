/**
 * Seed the fixtures the specs need, and write them to `.tmp/e2e-seed.json`.
 *
 * The geometry comes from the **composer**, not from hand-drawn boxes: a composed board is a real
 * layout — spawns, wools, a hub, a frontline, connected build zones — so the pages under test render
 * something representative instead of two rectangles. It is also reproducible: a descriptor
 * (players, teams, symmetry, seed, cell) composes the same plan every time, so the fixtures are stable
 * run to run without committing a fixture file.
 *
 * Three maps, one per stage the routes need:
 *   plan      — a composed candidate committed to authoring    → /maps/{slug}/plan
 *   sketch    — a draft carrying a compiled layout             → /maps/{slug}/sketch
 *   configure — that layout finished into world geometry       → /maps/{slug}/configure and /edit
 */

import { mkdir, writeFile } from "node:fs/promises";
import { api, BASE, TMP_DIR } from "./lib/harness.mjs";

const REQUEST = "players=12&symmetry=rot_180&cell=5&count=1";

/**
 * Pin a composed board so it exists as a stored candidate, then commit it to authoring.
 *
 * The descriptor is taken from a browsed card rather than written out here. A descriptor carries the
 * composer's own version and schema, and a hand-built one goes stale the next time either moves — which is
 * what a pin refuses on (`RQ1`), leaving the whole gate unable to seed. Browsing first is also what the
 * studio does, so the seed exercises the path a user takes.
 */
async function composedPlanMap() {
  const { cards } = await api(`/compose?${REQUEST}`);
  if (!cards?.length) throw new Error(`/compose?${REQUEST} returned no cards to pin`);
  const descriptor = cards[0].descriptor;
  const pinned = await api("/compose/pin", { method: "POST", body: descriptor });
  const { slug } = await api(`/plan/${pinned.id}/author`, { method: "POST" });
  return { slug, planJson: pinned.planJson, planId: pinned.id, descriptor };
}

async function main() {
  console.log(`seeding against ${BASE}`);

  // 1. a plan-stage map, straight from the composer
  const plan = await composedPlanMap();
  console.log(`  plan       ${plan.slug}`);

  // 2. a second composed board, carried all the way to world geometry
  const carried = await composedPlanMap();
  const compiled = await api("/plan/compile", { method: "POST", body: plan.planJson });
  await api(`/map/${carried.slug}/sketch`, { method: "PUT", body: compiled.layout });
  await api(`/map/${carried.slug}/intent`, { method: "PUT", body: compiled.intent });
  const finished = await api(`/map/${carried.slug}/sketch/finish`, { method: "POST" });
  console.log(`  configure  ${finished.slug} (finished to world geometry)`);

  // 3. a sketch-stage draft that still holds its layout, so the Sketch tool opens on real content
  const draft = await api("/sketch", { method: "POST", body: { name: "E2E sketch draft" } });
  await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: compiled.layout });
  console.log(`  sketch     ${draft.slug}`);

  const seed = {
    base: BASE,
    descriptor: plan.descriptor,
    planSlug: plan.slug,
    sketchSlug: draft.slug,
    mapSlug: finished.slug,
    planJson: plan.planJson,
  };

  const dir = TMP_DIR;
  await mkdir(dir, { recursive: true });
  await writeFile(`${dir}e2e-seed.json`, JSON.stringify(seed, null, 2));
  console.log(`  → ${dir}e2e-seed.json`);
}

await main();
