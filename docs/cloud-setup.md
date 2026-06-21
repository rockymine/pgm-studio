# Cloud setup (Claude Code on the web) — read first

This repo runs in an **ephemeral cloud container**: cloned fresh, **no database running, no connection
string set, and a firewalled network** by default. These are the steps that actually work to get the
project and DB up — and the traps that waste time if you don't know them. (Local/VM dev is covered by the
`CLAUDE.md` *Environment* section; this file is the cloud-specific runbook.)

## Gotchas that cost time (avoid these)
- **Don't install .NET from the Microsoft CDN / `dotnet-install.sh`** — it's firewalled (403). Use **apt**.
  The SDK is usually already present (`/usr/bin/dotnet`, **10.0.109**, matching the `global.json` pin). If a
  fresh box lacks it: `sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0`.
- **Always `apt-get update` before any apt install** — the MariaDB package 404s otherwise.
- **`dotnet test` does NOT work** on the .NET 10 SDK (the VSTest bridge was removed). Run each test project
  directly: `dotnet run --project tests/<Project>`.
- **No systemd** in the container — start MariaDB with `service mariadb start` (or `/etc/init.d/mariadb
  start`), not `systemctl`.
- The app's `appsettings.Development.json` points `MapsRoots`/`Import.Root` at `/media/sf_repos/...` (the
  reference VM) — **those paths don't exist here.** Only needed for map import / corpus features.

## Database (what works)
MariaDB 10.11 is installed (data dir already initialized). Then:
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
The API reads `ConnectionStrings:PgmStudio` — it is **not in appsettings**; supply it via env var (simplest)
or user-secrets. Set one before running the API:
```bash
export ConnectionStrings__PgmStudio="Server=localhost;Database=pgm_studio;Uid=pgm;Pwd=pgm_dev_pw;"
```

## Build / run / test
```bash
dotnet build                                   # whole solution
./tools/dev.sh restart                         # API + hosted Blazor WASM on :7894 (needs the conn string above)
dotnet run --project tests/PgmStudio.Pgm.Tests # tests — one project at a time, NOT `dotnet test`
```
`Api.Tests` can flake from a shared-schema race (non-deterministic 1/5/8 failures) — known, not your change.

## If you don't need the DB
Much of the generator work needs **no database**. `tools/PgmStudio.RoundTrip` runs DB-free:
`--gen-sketch`, `--gen-map-preview`, `--gen-catalog`, `--skeleton-study`, plus the parity/analysis harnesses.
The Python analysis/visualization lives in **`scripts/generator/`** (see its README); deps
(`numpy` / `scikit-image` / `scipy`) are usually already installed.

## Data (corpus maps + real output)
- **Real output data** lives in its own repo: **`rockymine/pgm-studio-output`** — clone it when you need the
  actual generated/derived output rather than synthetic fixtures.
- Corpus analysis also needs the source map worlds, which aren't in the container. Clone them locally and
  point the tool / `MapsRoots` at the clone (e.g. `OvercastCommunity/CommunityMaps`, the `ctw/` subset).

## Orientation
`CLAUDE.md` (rules) · `TODO.md` (open work) · `FEATURES.md` (shipped) ·
`docs/contracts/organic-lane-generation.md` (the lane-sketch generator).
