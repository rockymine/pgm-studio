import { type Page, type Locator, expect } from '@playwright/test';

export type Stage = 'sketch' | 'configure' | 'edit';

/**
 * The staged map collection at `/maps?stage=…` (Home.razor): one overview scoped by stage, with a
 * stage-appropriate "start" action (Configure → Import a world, Sketch → New sketch) and a list of
 * that stage's maps. Default stage is `edit`.
 */
export class MapsPage {
  readonly page: Page;
  readonly sectionTitle: Locator;
  readonly searchBox: Locator;
  readonly rows: Locator;

  constructor(page: Page) {
    this.page = page;
    this.sectionTitle = page.locator('section.panel-section .section-title');
    this.searchBox = page.getByPlaceholder('Search…');
    this.rows = page.locator('.panel-list .list-row');
  }

  async goto(stage: Stage = 'edit') {
    const q = stage === 'edit' ? '' : `?stage=${stage}`;
    await this.page.goto(`/maps${q}`);
    await expect(this.sectionTitle).toBeVisible();
  }

  /** The stage's primary action ("Import a world" for configure, "New sketch" for sketch). */
  primaryAction(label: string): Locator {
    return this.page.getByRole('button', { name: label })
      .or(this.page.getByRole('link', { name: label }));
  }

  /** A map row by its slug (the mono label). */
  row(slug: string): Locator {
    return this.rows.filter({ has: this.page.locator('.list-label--mono', { hasText: slug }) });
  }

  async open(slug: string) {
    await this.row(slug).click();
  }
}
