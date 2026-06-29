import { test, expect, type Page } from '@playwright/test';

/**
 * The Configure wizard golden path: a configure-stage map walks the flow bar through to Export and
 * downloads a `map.xml`.
 *
 * This needs a seeded, **export-ready** configure-stage map: one whose intent is fully authored (teams,
 * spawns, wools, monuments) and passes the export gate. A freshly-rasterized sketch reaches the wizard
 * but has geometry only — its Next is gated at the first phase, so it is NOT export-ready. Point at a
 * complete map via env:
 *
 *   PGM_E2E_SEED_MAP=<slug> npm test
 *
 * Without the env it skips — so the suite stays green on a fresh DB while this remains the scaffold the
 * full happy-path grows into (see D6: a deterministic export-ready fixture).
 */
const seedSlug = process.env.PGM_E2E_SEED_MAP;

const primaryButton = (page: Page) => page.locator('.flow-bar-actions .action-btn--primary');

test.describe('Configure → Export', () => {
  test.skip(!seedSlug, 'Set PGM_E2E_SEED_MAP to an export-ready configure-stage map slug to run this flow.');

  test('the wizard loads for a configure-stage map', async ({ page }) => {
    await page.goto(`/maps/${seedSlug}/configure`);
    // The flow bar's primary action is the steady signal the wizard shell mounted past the WASM boot.
    await expect(primaryButton(page)).toBeVisible();
    await expect(page.locator('.configure-flow-bar')).toBeVisible();
  });

  test('walking the flow bar reaches Export and downloads map.xml', async ({ page }) => {
    await page.goto(`/maps/${seedSlug}/configure`);

    const next = primaryButton(page);
    await expect(next).toBeVisible();

    // Advance phase-by-phase until the primary action becomes "Export" (the final XML sub-step). Bounded
    // so a stuck gate fails loudly instead of looping forever.
    for (let i = 0; i < 20; i++) {
      const label = (await next.textContent())?.trim() ?? '';
      if (/export/i.test(label)) break;
      if (await next.isDisabled()) {
        throw new Error(`Flow stalled at step ${i}: "${label}" is disabled — the seed map cannot advance.`);
      }
      await next.click();
    }

    await expect(next).toHaveText(/export/i);

    const download = page.waitForEvent('download');
    await next.click();
    const file = await download;
    expect(file.suggestedFilename()).toMatch(/\.xml$/);
  });
});
