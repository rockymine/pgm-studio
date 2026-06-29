# End-to-end tests (Playwright)

Browser-driven end-to-end tests for the hosted Blazor WASM app — the layer the C# (TUnit) and pure-JS
(`node --test`) unit suites can't reach: a real browser booting the WASM client, clicking through the
UI, and asserting rendered behaviour.

## Why this is isolated

The repo deliberately keeps the hand-written studio JS **dependency-free** so the unit tests run from
the VirtualBox shared folder with no `node_modules` (`tools/js-test.sh` → `node --test`). Playwright
needs `node_modules` + a browser binary, so it is quarantined here:

- its own `package.json` (the only one with dependencies),
- `node_modules/` and Playwright reports **gitignored** (`.gitignore`),
- **not** on the `npm test` / `tools/js-test.sh` path — the fast zero-dep loop is untouched.

## Running

From the repo root:

```bash
./tools/e2e.sh              # installs deps + Chromium on first run, boots the app, runs the suite
./tools/e2e.sh --headed     # extra args pass through to `playwright test`
./tools/e2e.sh --ui         # Playwright's interactive UI mode
```

The Playwright `webServer` block boots the app for you via `tools/dev.sh start` (which builds once,
runs the binary, and waits on `/api/health`) and **reuses** an already-running local server. So this
needs the same prerequisites as the app: the **.NET 10 SDK** and **MariaDB** (the `pgm_studio` DB).

Manage the server yourself instead:

```bash
PGM_E2E_NO_WEBSERVER=1 ./tools/dev.sh restart
PGM_E2E_NO_WEBSERVER=1 ./tools/e2e.sh
```

### Environment knobs

| Var                    | Default                 | Purpose                                              |
| ---------------------- | ----------------------- | ---------------------------------------------------- |
| `PGM_STUDIO_PORT`      | `7894`                  | App port (mirrors `tools/dev.sh`).                   |
| `PGM_E2E_BASE_URL`     | `http://localhost:7894` | Override the base URL wholesale.                     |
| `PGM_E2E_NO_WEBSERVER` | unset                   | `1` = don't let Playwright start/stop the app.       |
| `PGM_E2E_SEED_MAP`     | unset                   | Slug of an **export-ready** configure-stage map → enables the export flow. |
| `PGM_E2E_CHROMIUM`     | unset                   | Path to a specific Chromium binary (e.g. a sandbox with browsers pre-staged); else Playwright's managed browser. |
| `PLAYWRIGHT_BROWSERS_PATH` | unset               | If set (pre-staged browsers), the runner skips `playwright install`. |

## Layout

```
tests/e2e/
  playwright.config.ts   # baseURL, webServer (dev.sh), reporters
  pages/                 # page objects — one method per meaningful interaction
  specs/                 # the tests
```

- `specs/landing.spec.ts` — the landing boots past the WASM load and shows the three lifecycle cards.
- `specs/navigation.spec.ts` — each landing card lands on its stage overview with the right start action.
- `specs/configure-export.spec.ts` — the Configure wizard golden path (walk the flow bar → download
  `map.xml`). **Seed-gated:** skips unless `PGM_E2E_SEED_MAP` points at an **export-ready** configure-stage
  map (fully-authored intent that passes the export gate). A freshly-rasterized sketch reaches the wizard
  but is geometry-only, so its Next is gated — not export-ready. A deterministic fixture for this is D6.

The landing + navigation specs run against an **empty database** (an empty stage just lists no maps), so
they're the always-on smoke. The export flow grows as seed fixtures are added.

## Selectors

There are no `data-testid`s in the app today, so specs lean on **roles, visible text, and stable
structural classes** (`.card-title`, `.flow-bar-actions .action-btn--primary`, `.list-row`). When a
flow needs a more durable hook than a styling class, prefer adding a `data-testid` in the Razor over
matching brittle CSS.

## BDD / Gherkin

These are plain Playwright tests, organised behind page objects so a `playwright-bdd` (Gherkin
`.feature`) layer could be added on top later without rewriting the specs — adopt it only if you want
the spec-as-documentation artifact for a non-developer audience.
```
