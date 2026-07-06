# pgm-studio

An ASP.NET Core rewrite of `pgm-map-studio` — an authoring/analysis studio for PGM
(PvP Game Manager) Capture-the-Wool Minecraft maps. Map data lives in **MariaDB**
(relational), not files.

Setting it up for the first time? Read [`docs/setup-guide.md`](docs/setup-guide.md) — a
step-by-step guide for Windows, Linux, and macOS that covers the .NET 10 and MariaDB
prerequisites, the connection string, migrations, running the app, and loading real maps,
sketches, and plans. The rest of this README is a terse reference for people already set up.

## Stack
- **Backend:** ASP.NET Core + [FastEndpoints](https://fast-endpoints.com/) (`/api/...`)
- **Frontend:** Blazor WebAssembly, hosted by the backend
- **DB:** MariaDB · **Migrations:** FluentMigrator · **DAL:** linq2db (MySqlConnector)
- **Tests:** TUnit (Microsoft.Testing.Platform)
- **Parquet ingest:** Parquet.Net

## Layout
```
src/
  PgmStudio.Geom        dependency-free 2-D geometry leaf (symmetry transforms, polygon primitives)
  PgmStudio.Domain      domain POCOs + value types (the PGM map contract)
  PgmStudio.Pgm         map.xml ⇄ domain ⇄ persisted-JSON codec
  PgmStudio.Minecraft   NBT/Anvil world reading + feature extractors
  PgmStudio.Analysis    buildability / traversability / symmetry / categorization
  PgmStudio.Data        linq2db models + repositories
  PgmStudio.Migrations  FluentMigrator migrations (the schema)
  PgmStudio.Contracts   request/response DTOs shared by Api + Client
  PgmStudio.Api         FastEndpoints host; serves the Blazor client
  PgmStudio.Client      Blazor WebAssembly UI
  PgmStudio.Import      one-off: existing file outputs → MariaDB
tests/                  TUnit, one class per source unit, mirroring the tree
```

## Dev
```bash
./tools/dev.sh restart        # build once + run the API host on :7894 (background)
./tools/dev.sh status|logs|stop
curl localhost:7894/api/health
```
Run a test project (TUnit native runner): `dotnet run --project tests/<Proj>`.

## Configuration
Environment-specific values are **config, not code**.

- **DB connection (secret)** — never committed. In dev it comes from **User Secrets**; set it once per
  machine:
  ```bash
  dotnet user-secrets set "ConnectionStrings:PgmStudio" \
    "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;" \
    --project src/PgmStudio.Api
  ```
  In prod set the `ConnectionStrings__PgmStudio` (or `PGM_STUDIO_DB`) environment variable instead.
- **Non-secret paths** — corpus/import roots live in `src/PgmStudio.Api/appsettings.Development.json`
  (`MapsRoots`, `Import:Root`); override per environment via `appsettings.{Environment}.json` or env vars
  (`MapsRoots__0`, `Import__Root`).
- **Tools** (`PgmStudio.Import`, `PgmStudio.RoundTrip`) read env vars: `PGM_STUDIO_DB` (connection),
  `PGM_STUDIO_MAPS_ROOTS` (semicolon/comma-separated corpus roots), `PGM_STUDIO_OUTPUT_ROOT`. e.g.
  ```bash
  PGM_STUDIO_DB="Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;" \
    dotnet run --project src/PgmStudio.Import -- --migrate-only
  ```

See `CLAUDE.md` for the milestone tracker and the reference Python app.
