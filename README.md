# pgm-studio

An authoring and analysis studio for PGM (PvP Game Manager) **Capture-the-Wool** Minecraft maps.
Import a Minecraft world, lay out its teams, spawns, wools, and monuments through a guided editor, and
export a ready-to-play `map.xml` — with all map data kept in a **MariaDB** database. It's built on
ASP.NET Core with a Blazor WebAssembly front-end.

## Requirements

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the exact version is pinned in
`global.json`) and a **MariaDB 10.11** database — or a compatible build — reachable on `localhost`.

## Getting started

Start by creating the database and a user for it. The credentials here match the sample connection
string in the next step, so you can paste them as-is to get going:

```sql
CREATE DATABASE pgm_studio CHARACTER SET utf8mb4;
CREATE USER 'pgm'@'localhost' IDENTIFIED BY 'pgm_dev_pw';
GRANT ALL PRIVILEGES ON pgm_studio.* TO 'pgm'@'localhost';
FLUSH PRIVILEGES;
```

Point the app at that database with a connection string. In development it's read from .NET user
secrets, so it never ends up in source control:

```bash
dotnet user-secrets set "ConnectionStrings:PgmStudio" \
  "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;" \
  --project src/PgmStudio.Api
```

Create the schema by applying the migrations once. The API refuses to start against an out-of-date
database, so this comes before the first run:

```bash
dotnet run --project src/PgmStudio.Import -- --migrate-only
```

Then run the API host, which also serves the Blazor client, and open <http://localhost:5189>:

```bash
dotnet run --project src/PgmStudio.Api
```

## Configuration

Environment-specific values live in configuration rather than code. The database connection is a secret
and comes from .NET user secrets in development (set during [getting started](#getting-started)); in
production you'd set the `ConnectionStrings__PgmStudio` — or `PGM_STUDIO_DB` — environment variable
instead.

The map folders are defined in `src/PgmStudio.Api/appsettings.Development.json`, overridable per
environment through `appsettings.{Environment}.json` or environment variables, and should be absolute
paths:

```json
{
  "MapsRoots": [
    "/path/to/community-maps/ctw",
    "/path/to/public-maps/ctw"
  ],
  "Import": { "Root": "/path/to/pgm-studio-imports" }
}
```

`MapsRoots` lists the folders holding authored maps — each a `<slug>/map.xml` — which the map editor
reads and re-scans worlds from. `Import:Root` is the single drop folder the Configure page watches for
new, un-authored worlds, and defaults to a temporary directory when unset. `Import:AllowedHosts`
restricts which hosts the download-URL import will fetch from. The command-line tools accept the same
settings as environment variables (`PGM_STUDIO_DB`, `PGM_STUDIO_MAPS_ROOTS`, and
`PGM_STUDIO_OUTPUT_ROOT`).

## Importing maps

There are two ways to get maps into the studio, depending on whether you're starting from a raw world
or a finished map.

### A new world, in the app

The main flow turns a raw Minecraft world — one that has terrain but no `map.xml` — into a new map you
author in the studio. Put the world in the imports folder (`Import:Root`, from
[configuration](#configuration)) so its region files sit at `<Import:Root>/<world-name>/region/*.mca`,
with no `map.xml` alongside them. Then start **Configure** in the app: the world shows up under "Open a
world folder", and picking it scans the world into the database and opens the authoring wizard. Instead
of a local folder you can also paste a download URL — provided its host is on the `Import:AllowedHosts`
allow-list — and the world is fetched, extracted, and scanned for you.

### An existing map library, from the command line

To bulk-load maps that already have a `map.xml`, use the command-line tools instead. First scan a world
(a folder containing `region/` and `map.xml`) into importer-ready files under `<outRoot>/<slug>/`, or
scan a whole tree at once with `--scan-out-all`:

```bash
dotnet run --project tools/PgmStudio.RoundTrip -- --scan-out <mapDir> <outRoot>
dotnet run --project tools/PgmStudio.RoundTrip -- --scan-out-all <mapsRoot> <outRoot>
```

Then ingest every map found under `<outRoot>` into the database:

```bash
dotnet run --project src/PgmStudio.Import <outRoot>
```

The importer verifies each map after loading. If you re-run it against an older output that predates a
schema change, the round-trip check flags the affected field — for example `ROUND-TRIP DRIFT <slug>:
[kits]` — which means the cached output is stale, not that data is corrupt. Regenerate it with a fresh
scan, or re-derive map entities in place from the current `map.xml` without a re-scan:

```bash
PGM_STUDIO_MAPS_ROOTS=<map root(s)> dotnet run --project src/PgmStudio.Import -- --refresh-xml
```

## Development

pgm-studio is built on ASP.NET Core with [FastEndpoints](https://fast-endpoints.com/) serving the
`/api`, a Blazor WebAssembly UI hosted by the backend, and MariaDB for storage with FluentMigrator
migrations and linq2db (MySqlConnector) as the data layer; block data is ingested with Parquet.Net, and
the tests use TUnit.

Because TUnit uses its own runner, `dotnet test` isn't supported — run a test project directly instead,
for example `dotnet run --project tests/PgmStudio.Api.Tests`. If you'd rather run the host as a managed
background process than a foreground `dotnet run`, `./tools/dev.sh` builds once and runs it in the
background on :7894, with `restart`, `status`, `logs`, and `stop` sub-commands (override the port with
`PGM_STUDIO_PORT`).

The solution is organized as a set of projects under `src/`, with tests mirroring them under `tests/`:

```
PgmStudio.Geom        dependency-free 2-D geometry (symmetry transforms, polygon primitives)
PgmStudio.Domain      domain types (the PGM map contract)
PgmStudio.Pgm         map.xml ⇄ domain ⇄ persisted-JSON codec
PgmStudio.Minecraft   NBT/Anvil world reading + feature extractors
PgmStudio.Analysis    buildability / traversability / symmetry / categorization
PgmStudio.Data        linq2db models + repositories
PgmStudio.Migrations  FluentMigrator migrations (the schema)
PgmStudio.Contracts   request/response DTOs shared by Api + Client
PgmStudio.Api         FastEndpoints host; serves the Blazor client
PgmStudio.Client      Blazor WebAssembly UI
PgmStudio.Import      command-line importer (worlds / scanned output → MariaDB)
```
