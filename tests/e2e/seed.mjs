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
import { api, BASE } from "./lib/harness.mjs";

const DESCRIPTOR = { players: 12, teams: 2, symmetry: "rot_180", seed: 0, cell: 5 };

/** Pin a composed board so it exists as a stored candidate, then commit it to authoring. */
async function composedPlanMap() {
  const pinned = await api("/compose/pin", { method: "POST", body: DESCRIPTOR });
  const { slug } = await api(`/plan/${pinned.id}/author`, { method: "POST" });
  return { slug, planJson: pinned.planJson, planId: pinned.id };
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
    descriptor: DESCRIPTOR,
    planSlug: plan.slug,
    sketchSlug: draft.slug,
    mapSlug: finished.slug,
    planJson: plan.planJson,
  };

  const dir = new URL("../../.tmp/", import.meta.url).pathname;
  await mkdir(dir, { recursive: true });
  await writeFile(`${dir}e2e-seed.json`, JSON.stringify(seed, null, 2));
  console.log(`  → ${dir}e2e-seed.json`);
}

await main();
