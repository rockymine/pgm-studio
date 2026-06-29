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

The Playwright `webServer` block **reuses** an already-running dev server (e.g. `./tools/dev.sh`) via
the `/api/health` URL; if none is up it starts one in the foreground with `dotnet run` and tears it down
after. So this needs the same prerequisites as the app: the **.NET 10 SDK** and **MariaDB** (the
`pgm_studio` DB, schema migrated — `dotnet run --project src/PgmStudio.Import -- --migrate-only`).

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
| `PGM_E2E_SEED_MAP`     | unset                   | Override: slug of an **export-ready** configure-stage map for the export flow (else it self-seeds one). |
| `PGM_E2E_CHROMIUM`     | unset                   | Path to a specific Chromium binary (e.g. a sandbox with browsers pre-staged); else Playwright's managed browser. |
| `PLAYWRIGHT_BROWSERS_PATH` | unset               | If set (pre-staged browsers), the runner skips `playwright install`. |

## Layout

```
tests/e2e/
  playwright.config.ts   # baseURL, webServer, reporters
  fixtures/seed.ts       # idempotent export-ready map seeding (API calls + the intent)
  pages/                 # page objects — one method per meaningful interaction
  specs/                 # the tests
```

- `specs/landing.spec.ts` — the landing boots past the WASM load and shows the three lifecycle cards.
- `specs/navigation.spec.ts` — each landing card lands on its stage overview with the right start action.
- `specs/configure-export.spec.ts` — the Configure wizard golden path (walk the flow bar → download
  `map.xml`). **Self-seeding:** `beforeAll` ensures an **export-ready** configure-stage map (a generated
  sketch for the geometry + a fully-authored intent PUT over it, see `fixtures/seed.ts`) and is idempotent
  (reused across runs — there is no delete-map API). `PGM_E2E_SEED_MAP` overrides it with a hand-made map.

The landing + navigation specs run against an empty database (an empty stage lists no maps); the export
spec seeds its own fixture. All run unconditionally against a writable `pgm_studio` DB.

### How the fixture passes the export gate

The gate has two playability requirements: every spawn/wool point must be **grounded** (on real terrain,
not floating over a build area) and the spawn↔wool chain must be **connected**. The fixture rides on the
generated H-sketch's geometry: its two islands are large slabs (≈ x[-24,23], split along z), so the
authored spawn/wool coordinates (red near z=-20, blue near z=+20) land on actual terrain. The build area
bridges the middle for connectivity. Both teams are authored explicitly, and all five phase slices (meta ·
symmetry · teams · build · wools) are present so the wizard unlocks the rail through Review.
`ensureExportReadyMap` asserts export-readiness, so a generator change that moved the islands off these
coords would fail seeding loudly rather than produce a silently-unplayable map.

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
