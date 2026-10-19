/**
 * Seed the fixtures the specs need, and write them to `.tmp/e2e-seed.json`.
 *
 * The plan fixtures come from `fixtures/composed.plan.json`, a plan the composer laid (twelve players, `rot_180`,
 * seed 0, its boxes written in) — a real board with a spawn, a wool, a hub and a frontline at one height, so the
 * pages under test render something representative instead of two rectangles, and the same every run. The composed fixture comes from the
 * **composer**: `e2e.sh` composes the board library the feed reads, and a map is made of its first card the
 * way the generator makes one.
 *
 * Three maps, one per stage the routes need, and a composed one:
 *   sketch    — a draft carrying its compiled layout             → /maps/{slug}/sketch
 *   configure — that layout finished into world geometry         → /maps/{slug}/configure
 *   composed  — a map the generator made of a library board      → /maps/{slug}/sketch
 */

import { mkdir, readFile, writeFile } from "node:fs/promises";
import { api, BASE, TMP_DIR } from "./lib/harness.mjs";

const REQUEST = "players=12&symmetry=rot_180&count=1";
const SEED_PLAN = new URL("./fixtures/composed.plan.json", import.meta.url);

/**
 * A map made of the first card the feed shows. The descriptor is taken from a browsed card rather than written
 * out here: it carries the composer's own version and schema, and a hand-built one goes stale the next time
 * either moves. Browsing first is also what the studio does, so the seed exercises the path a user takes.
 */
async function composedMap() {
  const { cards } = await api(`/compose?${REQUEST}`);
  if (!cards?.length) throw new Error(`/compose?${REQUEST} returned no cards to make a map of`);
  const { slug } = await api("/compose/map", { method: "POST", body: cards[0].descriptor });
  return { slug, descriptor: cards[0].descriptor };
}

async function main() {
  console.log(`seeding against ${BASE}`);

  const planJson = await readFile(SEED_PLAN, "utf8");

  // 1. the seed plan compiled and carried all the way to world geometry
  const compiled = await api("/plan/compile", { method: "POST", body: planJson });
  const { slug: carried } = await api("/sketch", { method: "POST", body: { name: "E2E configure" } });
  await api(`/map/${carried}/sketch`, { method: "PUT", body: compiled.layout });
  // A finished map names its author: Configure unlocks every phase past Identity only once one is stated,
  // and a name with a space in it is a pseudonym, so no Mojang lookup is asked.
  const intent = { ...compiled.intent, meta: { ...compiled.intent.meta, authors: [{ name: "E2E fixture" }] } };
  await api(`/map/${carried}/intent`, { method: "PUT", body: intent });
  const finished = await api(`/map/${carried}/sketch/finish`, { method: "POST" });
  console.log(`  configure  ${finished.slug} (finished to world geometry)`);

  // 2. a sketch-stage draft that still holds its layout, so the Sketch tool opens on real content — and its
  //    intent, which is where its rooms are drawn from
  const draft = await api("/sketch", { method: "POST", body: { name: "E2E sketch draft" } });
  await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: compiled.layout });
  await api(`/map/${draft.slug}/intent`, { method: "PUT", body: intent });
  console.log(`  sketch     ${draft.slug}`);

  // 3. a map the generator made of a library board
  const composed = await composedMap();
  console.log(`  composed   ${composed.slug}`);

  const seed = {
    base: BASE,
    descriptor: composed.descriptor,
    sketchSlug: draft.slug,
    mapSlug: finished.slug,
    composedSlug: composed.slug,
    planJson,
  };

  const dir = TMP_DIR;
  await mkdir(dir, { recursive: true });
  await writeFile(`${dir}e2e-seed.json`, JSON.stringify(seed, null, 2));
  console.log(`  → ${dir}e2e-seed.json`);
}

await main();
