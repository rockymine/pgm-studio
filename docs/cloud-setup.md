# Cloud setup (Claude Code on the web) — read first

This repo runs in an **ephemeral cloud container**: cloned fresh, **no database running, no connection
string set, and a firewalled network** by default. These are the steps that actually work to get the
project and DB up — and the traps that waste time if you don't know them. (Local/VM dev is covered by the
`CLAUDE.md` *Environment* section; this file is the cloud-specific runbook.)

## Gotchas that cost time (avoid these)
- **Foreground shell commands are sandboxed** (no outbound network beyond the local git proxy, restricted
  PATH). Anything needing the network — `apt`, `dotnet restore`/`build`/`run` on a cold cache — must run as a
  **background** command (`run_in_background`), which is initialized from the login profile and unsandboxed.
  `git` commit/push works either way (it goes through a local proxy). A bare `dotnet`/`apt` in a foreground
  call can fail with "command not found" or a network error even though it works in the background.
- **Beware `cmd 2>&1 | tail` masking failures** — the pipe's exit is `tail`'s (0). Capture the real exit code
  separately (`cmd; echo "exit=$?"`), or the run *looks* green when the tool wasn't even found.
- **Don't install .NET from the Microsoft CDN / `dotnet-install.sh`** — it's firewalled (403). Use **apt**.
  The SDK may already be present (`/usr/bin/dotnet`, **10.0.109**, matching the `global.json` pin); if a fresh
  box lacks it: `sudo apt-get install -y dotnet-sdk-10.0` (SDK, runtime, templates all pull in).
- **Dead PPAs break `apt-get update`.** The box may carry `deadsnakes` / `ondrej-php` PPA sources that 403,
  making `apt-get update` exit non-zero — which silently kills an `apt-get update && apt-get install …` chain
  before the install runs. Remove them first: `sudo rm -f /etc/apt/sources.list.d/deadsnakes-* /etc/apt/sources.list.d/ondrej-*`.
  The main Ubuntu archive (`archive.ubuntu.com`) is reachable and carries `dotnet-sdk-10.0` + `mariadb-server`.
- **`dotnet test` does NOT work** on the .NET 10 SDK (the VSTest bridge was removed). Run each test project
  directly: `dotnet run --project tests/<Project>`.
- **No systemd** in the container — start MariaDB with `sudo service mariadb start` (or `/etc/init.d/mariadb
  start`), not `systemctl`.
- The app's `appsettings.Development.json` points `MapsRoots`/`Import.Root` at `/media/sf_repos/...` (the
  reference VM) — **those paths don't exist here.** Only needed for map import / corpus features.

## Database (what works)
Install MariaDB if absent (`sudo apt-get install -y mariadb-server`), then:
```bash
sudo service mariadb start
sudo mariadb <<'SQL'
CREATE DATABASE IF NOT EXISTS pgm_studio;
CREATE DATABASE IF NOT EXISTS pgm_studio_test;     -- the Api test project needs this
CREATE USER IF NOT EXISTS 'pgm'@'localhost' IDENTIFIED BY 'pgm_dev_pw';
GRANT ALL ON pgm_studio.*      TO 'pgm'@'localhost';
GRANT ALL ON pgm_studio_test.* TO 'pgm'@'localhost';
FLUSH PRIVILEGES;
SQL
```

## Telling the app about the DB
Both the API and the `PgmStudio.Import` migrator resolve the **same** connection string, in this order:
1. **`PGM_STUDIO_DB`** env var — the explicit override, highest precedence.
2. **`ConnectionStrings:PgmStudio`** from configuration — appsettings (not committed), the API project's
   **User Secrets** (`dotnet user-secrets ... --project src/PgmStudio.Api`), or the
   **`ConnectionStrings__PgmStudio`** env var (which overrides file config).

The connection string is **not in appsettings**; supply it via env var (simplest) or user-secrets. Set one
before running the API or the importer:
```bash
export ConnectionStrings__PgmStudio="Server=localhost;Database=pgm_studio;Uid=pgm;Pwd=pgm_dev_pw;"
```
The importer prints which source it used, so a mismatch is visible up front.

## Migrations & the schema guard (avoid "Unknown column …")
The API **does not auto-apply migrations** — the DB lifecycle stays explicit. At startup it **fails fast**
if the database is behind the migrations this build needs, naming the pending versions:
```
SchemaOutOfDateException: Database schema is out of date: applied version 5, latest known M0006.
Pending migration(s): M0006. Apply them before starting the API:
  dotnet run --project src/PgmStudio.Import -- --migrate-only
```
Run that command (it resolves the same connection string as the API, so it lands on the same DB) to bring
the schema up to date. `--migrate-only` prints an explicit summary — either
`applied N migration(s): M000x..M000y` or `no pending migrations - schema is up to date at version X` — so a
no-op can't be mistaken for success.

## Build / run / test
```bash
dotnet build                                   # whole solution
./tools/dev.sh restart                         # API + hosted Blazor WASM on :7894 (needs the conn string above)
dotnet run --project tests/PgmStudio.Pgm.Tests # tests — one project at a time, NOT `dotnet test`
```
`Api.Tests` is deterministic — every DB-touching class boots the shared `ApiTestFactory`, which pins the
connection to `pgm_studio_test` (a module initializer forces `ConnectionStrings__PgmStudio` at that schema, so
an ambient dev-server value can't redirect the tests at the live DB) and shares one `[NotInParallel("api-db")]`
group so no per-test schema reset overlaps another test.

## End-to-end (it runs here — don't assume otherwise)
`./tools/e2e.sh all` works in the container, and it is the only check that exercises the Blazor surfaces at
all. Nothing extra needs installing: **Playwright is already global** (`/opt/node22`, resolved by
`tests/e2e/lib/harness.mjs`) and **Chromium is pre-installed** at `PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers`
— never run `playwright install`. The only prerequisite is the database section above, since the script
resets its own schema through `sudo -n mariadb` (its default admin path, which works once
`sudo service mariadb start` has run). It uses its own port (7895) and database (`pgm_studio_e2e`), so a run
cannot touch dev data. A full sweep takes roughly forty minutes here, most of it the WASM build and the
per-page boot.

**One check fails where the container cannot reach Mojang.** The smoke sweep's *edit tool is clean* reads a
404 from `/api/minecraft/player?name=Notch`, which is the studio answering honestly: `Notch` is shaped like a
Minecraft account, so the question is worth asking, and where `api.mojang.com` is unreachable there is no
account to answer with. It is not tolerated — `ALLOWED_FAULTS` in `tests/e2e/lib/harness.mjs` holds one
entry, for a fetch the sweep itself cancelled, and a 404 from the studio's own API is exactly what that list
refuses to wave through. Check `curl -m 10 https://api.mojang.com/users/profiles/minecraft/Notch` before
reading it as a defect: no answer at all means the run is on a network without egress, and every other spec
passing is the real result.

Nothing else on those pages wants the network. The author rows draw their own marks, and a name no lookup can
answer is stored as a pseudonym, so a container with no egress renders and saves the Identity form intact.

The suite drives **Chromium only**, so a rendering defect that exists in another engine cannot appear in it
— which is why the browser-specific canvas artifact recorded in `BACKLOG.md` was found by hand and stays
un-gated. **Firefox cannot be added here**, and the three routes are all closed, so there is no point
spending the attempt again: `playwright install firefox` is refused by the network policy (403 on the
CONNECT to `cdn.playwright.dev` and `playwright.download.prss.microsoft.com`), every Mozilla download host
is refused the same way, and Ubuntu noble's `firefox` package is a snap transition stub with no snap in the
container. Playwright needs its own patched build regardless, so a distro Firefox would not have driven
anyway. Anything engine-specific has to be checked on a real machine; `tools/painter-probe.mjs` takes a
`--browser` switch for exactly that.

## If you don't need the DB
Much of the analysis work needs **no database**. `tools/PgmStudio.RoundTrip` runs DB-free:
`--island-sketch`, `--skeleton-study`, `--island-study`, plus the parity/analysis harnesses.
Analysis beyond that is a scratch pass rather than a checked-in harness (`CLAUDE.md`, *Investigation stays
local*) — write it against the JSON `RoundTrip` emits, keep the finding, drop the script.

## Data (corpus maps + real output)
- **Real output data** lives in its own repo: **`rockymine/pgm-studio-output`** — clone it when you need the
  actual generated/derived output rather than synthetic fixtures.
- Corpus analysis also needs the source map worlds, which aren't in the container. Clone them locally and
  point the tool / `MapsRoots` at the clone (e.g. `OvercastCommunity/CommunityMaps`, the `ctw/` subset).

## Orientation
`CLAUDE.md` (rules) · `TODO.md` (open work) · `FEATURES.md` (shipped) ·
`docs/generator/model.md` (the map-generation model).
