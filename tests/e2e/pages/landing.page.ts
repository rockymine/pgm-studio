import { type Page, type Locator, expect } from '@playwright/test';

/**
 * The studio landing at `/` — a hero over three lifecycle cards (Sketch · Configure · Edit),
 * each deep-linking into the matching `/maps?stage=…` overview.
 */
export class LandingPage {
  readonly page: Page;
  readonly title: Locator;

  constructor(page: Page) {
    this.page = page;
    this.title = page.locator('h1.landing-title');
  }

  async goto() {
    await this.page.goto('/');
    // Wait past the Blazor WASM boot ("Loading") for the rendered hero.
    await expect(this.title).toBeVisible();
  }

  /** A landing card by its visible title. */
  card(name: 'Sketch' | 'Configure' | 'Edit'): Locator {
    return this.page
      .locator('.landing-cards .card', { has: this.page.locator('.card-title', { hasText: name }) });
  }

  async openStage(name: 'Sketch' | 'Configure' | 'Edit') {
    await this.card(name).click();
  }
}
