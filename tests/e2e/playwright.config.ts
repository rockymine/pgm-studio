import { defineConfig, devices } from '@playwright/test';

// The app is the hosted Blazor WASM client served by the API host. Port mirrors tools/dev.sh
// (PGM_STUDIO_PORT, default 7894). Override the base URL wholesale with PGM_E2E_BASE_URL.
const port = process.env.PGM_STUDIO_PORT ?? '7894';
const baseURL = process.env.PGM_E2E_BASE_URL ?? `http://localhost:${port}`;

// In CI (or any one-shot run) let Playwright boot the app itself via tools/dev.sh. Locally, if the
// dev server is already up, it is reused rather than restarted. Set PGM_E2E_NO_WEBSERVER=1 to manage
// the server yourself.
const manageServer = process.env.PGM_E2E_NO_WEBSERVER !== '1';

export default defineConfig({
  testDir: './specs',
  // Blazor WASM cold boot + the wizard's per-phase work are not instant; give assertions room.
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }], ['list']]
    : [['html', { open: 'never' }], ['list']],

  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],

  webServer: manageServer
    ? {
        // dev.sh `start` is idempotent: builds once, launches the binary, waits for /api/health,
        // and returns. reuseExistingServer means a warm local server is left alone.
        command: '../../tools/dev.sh start',
        cwd: __dirname,
        url: `${baseURL}/api/health`,
        timeout: 180_000,
        reuseExistingServer: !process.env.CI,
        stdout: 'pipe',
        stderr: 'pipe',
      }
    : undefined,
});
