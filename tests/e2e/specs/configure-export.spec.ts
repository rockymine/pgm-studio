import { test, expect, type Page } from '@playwright/test';
import { ensureExportReadyMap } from '../fixtures/seed';

/**
 * The Configure wizard golden path: an export-ready configure-stage map walks the flow bar through to
 * Export and downloads a `map.xml`.
 *
 * Self-seeding: `beforeAll` ensures an export-ready fixture exists (idempotent — reused across runs) and
 * captures its slug, so this runs unconditionally against a writable DB. Set `PGM_E2E_SEED_MAP=<slug>` to
 * point at a hand-made map instead.
 *
 * "Export-ready" means a fully-authored intent that passes the export gate (round-trip + traversability).
 * A freshly-rasterized sketch reaches the wizard but is geometry-only — its Next is gated at phase 1 —
 * which is why the fixture PUTs a complete intent over the geometry (see fixtures/seed.ts).
 */
let slug: string;

test.beforeAll(async () => {
  slug = await ensureExportReadyMap();
});

const primaryButton = (page: Page) => page.locator('.flow-bar-actions .action-btn--primary');

test.describe('Configure → Export', () => {
  test('the wizard loads for a configure-stage map', async ({ page }) => {
    await page.goto(`/maps/${slug}/configure`);
    // The flow bar's primary action is the steady signal the wizard shell mounted past the WASM boot.
    await expect(primaryButton(page)).toBeVisible();
    await expect(page.locator('.configure-flow-bar')).toBeVisible();
  });

  test('walking the flow bar reaches Export and downloads map.xml', async ({ page }) => {
    await page.goto(`/maps/${slug}/configure`);

    const next = primaryButton(page);
    await expect(next).toBeVisible();

    // Advance phase-by-phase until the primary action becomes "Export" (the final XML sub-step). Each
    // step waits for Next to be enabled — riding out the WASM-load gate and the brief save between phases
    // — so a genuinely stuck gate surfaces as a clear toBeEnabled timeout. Bounded against an infinite loop.
    for (let i = 0; i < 20; i++) {
      await expect(next, `flow stalled at step ${i} — Next never enabled`).toBeEnabled();
      const label = (await next.textContent())?.trim() ?? '';
      if (/export/i.test(label)) break;
      await next.click();
    }

    await expect(next).toHaveText(/export/i);

    const download = page.waitForEvent('download');
    await next.click();
    const file = await download;
    expect(file.suggestedFilename()).toMatch(/\.xml$/);
  });
});
