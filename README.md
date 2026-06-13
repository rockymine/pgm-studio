# pgm-studio

An ASP.NET Core rewrite of `pgm-map-studio` — an authoring/analysis studio for PGM
(PvP Game Manager) Capture-the-Wool Minecraft maps. Map data lives in **MariaDB**
(relational), not files.

## Stack
- **Backend:** ASP.NET Core + [FastEndpoints](https://fast-endpoints.com/) (`/api/...`)
- **Frontend:** Blazor WebAssembly, hosted by the backend
- **DB:** MariaDB · **Migrations:** FluentMigrator · **DAL:** linq2db (MySqlConnector)
- **Tests:** TUnit (Microsoft.Testing.Platform)
- **Parquet ingest:** Parquet.Net

## Layout
```
src/
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

DB: `pgm_studio` on localhost; dev user `pgm` / `pgm_dev_pw`.

See `CLAUDE.md` for the milestone tracker and the reference Python app.
