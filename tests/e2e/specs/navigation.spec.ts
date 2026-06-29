import { test, expect } from '@playwright/test';
import { LandingPage } from '../pages/landing.page';
import { MapsPage } from '../pages/maps.page';

// Navigation flow: from the landing, each card lands on the right stage overview with its
// stage-appropriate start action. Runs against an empty database (an empty stage just lists no rows).
test.describe('Navigation', () => {
  test('Configure card → import-a-world overview', async ({ page }) => {
    const landing = new LandingPage(page);
    await landing.goto();
    await landing.openStage('Configure');

    await expect(page).toHaveURL(/stage=configure/);
    const maps = new MapsPage(page);
    await expect(maps.primaryAction('Import a world')).toBeVisible();
  });

  test('Sketch card → new-sketch overview', async ({ page }) => {
    const landing = new LandingPage(page);
    await landing.goto();
    await landing.openStage('Sketch');

    await expect(page).toHaveURL(/stage=sketch/);
    const maps = new MapsPage(page);
    await expect(maps.primaryAction('New sketch')).toBeVisible();
  });

  test('Edit card → maps overview, and Studio crumb returns home', async ({ page }) => {
    const landing = new LandingPage(page);
    await landing.goto();
    await landing.openStage('Edit');

    await expect(page).toHaveURL(/\/maps$/);

    // The overview topbar carries a Studio breadcrumb back to the landing.
    await page.getByRole('link', { name: 'Studio' }).first().click();
    await expect(new LandingPage(page).title).toBeVisible();
  });
});
