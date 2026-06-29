import { request, type APIRequestContext } from '@playwright/test';

const port = process.env.PGM_STUDIO_PORT ?? '7894';
const baseURL = process.env.PGM_E2E_BASE_URL ?? `http://localhost:${port}`;

/** The fixture map's display name — also how an existing one is found for reuse (no delete-map API). */
export const EXPORT_FIXTURE_NAME = 'E2E Export Fixture';

/**
 * A complete, **export-ready** authoring intent. The trick that makes it terrain-independent: every
 * spawn/wool/monument nav point sits inside one big build area, so traversability (which treats a build
 * area as navigable) connects them without any real world terrain. Symmetry is set with a single orbit
 * unit (red team) — the generator mirrors it onto blue — and all five phase slices (meta · symmetry ·
 * teams · build · wools) are present, so the wizard unlocks the rail through Review.
 */
export const EXPORT_FIXTURE_INTENT = {
  meta: { name: EXPORT_FIXTURE_NAME, authors: ['e2e'] },
  symmetry: { mode: 'rot_180', centerX: 0, centerZ: 0 },
  teams: [
    { id: 'red-team', name: 'Red', color: 'red' },
    { id: 'blue-team', name: 'Blue', color: 'blue' },
  ],
  maxPlayers: 12,
  observer: { point: { x: 0, y: 60, z: 0 }, yaw: 180 },
  build: { maxHeight: 30, areas: [{ minX: -100, minZ: -100, maxX: 100, maxZ: 100 }], holes: [] },
  spawns: [
    { team: 'red-team', point: { x: -20, y: 12, z: -20 }, protection: [{ minX: -30, minZ: -30, maxX: -10, maxZ: -10 }], yaw: 0 },
  ],
  wools: [
    {
      owner: 'red-team', color: 'red',
      room: [{ minX: -35, minZ: -35, maxX: -15, maxZ: -15 }],
      spawn: { x: -20.5, y: 13, z: -18.5 },
      monuments: [{ team: 'blue-team', location: { x: 20, y: 13, z: 20 } }],
    },
  ],
  islandTeams: {},
};

async function isExportReady(ctx: APIRequestContext, slug: string): Promise<boolean> {
  const res = await ctx.get(`/api/map/${slug}/preflight`);
  if (!res.ok()) return false;
  const body = await res.json();
  return body?.exportReady === true;
}

/** Find a reusable fixture map by name (there is no delete-map API, so reuse rather than re-create). */
async function findFixture(ctx: APIRequestContext): Promise<string | null> {
  const res = await ctx.get('/api/maps?stage=configure');
  if (!res.ok()) return null;
  const maps = (await res.json()) as Array<{ slug: string; name: string }>;
  const hit = maps.find((m) => m.name === EXPORT_FIXTURE_NAME);
  return hit && (await isExportReady(ctx, hit.slug)) ? hit.slug : null;
}

/**
 * Ensure an export-ready configure-stage map exists and return its slug. Idempotent: reuses a prior
 * fixture when one is present and still export-ready, else builds a fresh one — a generated sketch (for
 * the world geometry / `configure` stage) with the export-ready intent PUT over it. Honors
 * `PGM_E2E_SEED_MAP` as an explicit override (point at a hand-made map instead).
 */
export async function ensureExportReadyMap(): Promise<string> {
  if (process.env.PGM_E2E_SEED_MAP) return process.env.PGM_E2E_SEED_MAP;

  const ctx = await request.newContext({ baseURL });
  try {
    const existing = await findFixture(ctx);
    if (existing) return existing;

    // Geometry: a generated sketch (H archetype → 2 islands), advanced sketch → configure on finish.
    const gen = await ctx.post('/api/sketch/generate', { data: { name: EXPORT_FIXTURE_NAME, archetype: 'H', seed: 7 } });
    if (!gen.ok()) throw new Error(`sketch/generate failed: ${gen.status()} ${await gen.text()}`);
    const slug = (await gen.json()).slug as string;

    const finish = await ctx.post(`/api/map/${slug}/sketch/finish`, { data: {} });
    if (!finish.ok()) throw new Error(`sketch/finish failed: ${finish.status()} ${await finish.text()}`);

    // Configuration: the export-ready intent (generator regenerates the doc, opening the export gate).
    const put = await ctx.put(`/api/map/${slug}/intent`, { data: EXPORT_FIXTURE_INTENT });
    if (!put.ok()) throw new Error(`PUT intent failed: ${put.status()} ${await put.text()}`);

    if (!(await isExportReady(ctx, slug))) {
      throw new Error(`seeded map ${slug} is not export-ready — check /api/map/${slug}/preflight`);
    }
    return slug;
  } finally {
    await ctx.dispose();
  }
}
