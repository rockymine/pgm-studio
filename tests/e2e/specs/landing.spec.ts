import { test, expect } from '@playwright/test';
import { LandingPage } from '../pages/landing.page';

// Smoke: the landing renders past the WASM boot and presents the three lifecycle cards. This is the
// cheapest end-to-end signal that the host serves the app, the framework boots, and the client mounts
// — it needs no seeded data.
test.describe('Landing', () => {
  test('boots and shows the three lifecycle cards', async ({ page }) => {
    const landing = new LandingPage(page);
    await landing.goto();

    await expect(landing.title).toHaveText(/Capture-the-Wool map/i);

    for (const name of ['Sketch', 'Configure', 'Edit'] as const) {
      await expect(landing.card(name)).toBeVisible();
    }
  });

  test('cards deep-link into their stage overviews', async ({ page }) => {
    const landing = new LandingPage(page);
    await landing.goto();

    await expect(landing.card('Sketch')).toHaveAttribute('href', /stage=sketch/);
    await expect(landing.card('Configure')).toHaveAttribute('href', /stage=configure/);
    await expect(landing.card('Edit')).toHaveAttribute('href', /maps$/);
  });
});
