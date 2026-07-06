# Setup guide

This guide gets PGM Studio running on your own machine — Windows, Linux, or macOS — and shows you how
to load real maps, sketches, and plans once it is up. It is written for someone setting the project up
for the first time. If you are an AI agent working in the ephemeral cloud container instead, read
`docs/cloud-setup.md`, which is the runbook for that environment; this guide is for a normal desktop.

## What PGM Studio is

PGM Studio is an authoring and analysis studio for PGM (PvP Game Manager) Capture-the-Wool Minecraft
maps. A finished map is a Minecraft world folder plus a `map.xml` that tells PGM how the match is played:
where the teams spawn, which wools each team must capture, where the monuments are, and which regions and
filters gate the objectives. The studio is a web app — an ASP.NET Core backend that serves a Blazor
WebAssembly UI — and it keeps all of its map data in a MariaDB database rather than in loose files.

## The four tools, and what each is for

The studio is organised around a map's lifecycle, and you enter it through one of four tools depending on
where your map is in that lifecycle.

Sketch is where a map begins when you are starting from nothing. You draw the islands from a blank canvas
— their shapes, the symmetry that relates the teams, and the mirror that reflects one authored side onto
the others — and the studio rasterises what you drew and builds a terrain world out of it. You reach for
Sketch when you have an idea for a layout but no world yet.

Configure turns a world that has terrain but no finished `map.xml` into a playable map. It is the guided
wizard that walks you from the raw world through the world settings, the teams, the buildable areas, the
wools, and a final review that writes out a validated `map.xml`. Both sketched worlds and imported worlds
flow through Configure, so this is the stage that produces the game rules for a map that so far is only
terrain.

Edit opens a map that already has a finished `map.xml` and lets you refine it. This is where you adjust
the regions, teams, wools, and objectives of an existing map — either one you configured yourself or one
you imported from an existing map corpus. Editing works directly against the map data in the database.

The plan editor, also called the seed studio, is a coarse cell-grid layout tool that sits one level of
abstraction above the sketch canvas. Instead of drawing exact island outlines you place pieces on a grid
with a chosen symmetry, set the globals like cell size and player count, and then compile the plan into a
layout and an intent that become a new draft map. Plans are saved and shared as `*.plan.json` files, so
the plan editor is also where you load an existing plan to keep working on it.

## Prerequisites

You need the .NET 10 SDK and a MariaDB server. Everything else the projects depend on is restored
automatically the first time you build.

For the .NET 10 SDK, install version 10.0.x. The repository pins the SDK feature band in `global.json`
(`10.0.109` with `latestFeature` roll-forward), so any `10.0.1xx` SDK will satisfy it. On Windows, install
it with `winget install Microsoft.DotNet.SDK.10` or from the installer at `https://dotnet.microsoft.com/download`.
On macOS, use the installer from that same page, or `brew install dotnet-sdk` if you use Homebrew. On Linux,
install the `dotnet-sdk-10.0` package from your distribution (for Debian and Ubuntu that is
`sudo apt-get install -y dotnet-sdk-10.0`). Confirm it with `dotnet --version`, which should print a
`10.0.x` number.

For MariaDB, install a 10.11 or newer server. On Windows, download the MSI installer from
`https://mariadb.org/download/` and let it install MariaDB as a service. On macOS, run `brew install mariadb`
and then `brew services start mariadb`. On Linux, install `mariadb-server`
(`sudo apt-get install -y mariadb-server`) and start it with `sudo systemctl start mariadb`. Any of these
leaves a server listening on `localhost:3306`, which is what the studio expects.

## Get the code

Clone the repository and move into it.

```bash
git clone https://github.com/rockymine/pgm-studio.git
cd pgm-studio
```

## Create the database and a user

The studio connects as a dedicated `pgm` user to a database named `pgm_studio`, and the test suite uses a
second database named `pgm_studio_test`. Create both, plus the user, by opening a MariaDB shell as an
administrator (`sudo mariadb` on Linux, or `mysql -u root -p` on Windows and macOS) and running the
following.

```sql
CREATE DATABASE IF NOT EXISTS pgm_studio;
CREATE DATABASE IF NOT EXISTS pgm_studio_test;
CREATE USER IF NOT EXISTS 'pgm'@'localhost' IDENTIFIED BY 'pgm_dev_pw';
GRANT ALL ON pgm_studio.*      TO 'pgm'@'localhost';
GRANT ALL ON pgm_studio_test.* TO 'pgm'@'localhost';
FLUSH PRIVILEGES;
```

You can choose a different password; just use the same one in the connection string in the next step. The
`pgm_dev_pw` value here matches the examples throughout this guide and the rest of the repository.

## Tell the app about the database

The connection string is a secret, so it is never committed to the repository. In development you supply
it through .NET user secrets, which are stored per-user outside the source tree. Set it once against the
API project, which owns the secret store the whole solution reads.

```bash
dotnet user-secrets set "ConnectionStrings:PgmStudio" \
  "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;" \
  --project src/PgmStudio.Api
```

The API and the command-line tools all resolve the connection string the same way: an explicit
`PGM_STUDIO_DB` environment variable wins if it is set, otherwise the `ConnectionStrings:PgmStudio` value
from configuration, which is where your user secret lands. If you would rather not use user secrets, you
can instead export the connection string as an environment variable before running anything, and it will
be picked up with no further setup.

```bash
export ConnectionStrings__PgmStudio="Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;"
```

## Apply the database schema

The application does not create or migrate its own schema at startup; the database lifecycle is kept
explicit so nothing is altered behind your back. If the database is behind the schema the build expects,
the API refuses to start and tells you which migrations are pending. Bring the schema up to date by running
the migrator, which resolves the same connection string as the API and therefore lands on the same database.

```bash
dotnet run --project src/PgmStudio.Import -- --migrate-only
```

On a fresh database this applies every migration and prints a summary such as
`applied 6 migration(s): M0001..M0006`. Run this command again after pulling changes that add new
migrations; when there is nothing to do it says so plainly rather than pretending it worked.

## Build and run

Build the whole solution once to restore packages and compile everything.

```bash
dotnet build
```

On Linux and macOS the convenient way to run the app is the helper script, which builds once and then runs
the compiled binary directly on port 7894 in the background, avoiding a slow cold start on every launch.

```bash
./tools/dev.sh restart
```

The script waits for the health endpoint before returning, and `./tools/dev.sh status`, `logs`, and `stop`
manage the background process. On Windows, where the shell script does not apply, run the API host directly.

```bash
dotnet run --project src/PgmStudio.Api
```

Either way, once it is up you can confirm the backend is healthy and then open the studio in a browser.

```bash
curl http://localhost:7894/api/health
```

A response of `{"status":"ok","service":"pgm-studio"}` means the app is running. Open
`http://localhost:7894/` to reach the landing page with the Sketch, Configure, and Edit cards, and the Plan
link in the footer.

## Load real maps

New sketches and plans are created inside the studio, but to edit or analyse existing community maps you
first bring their worlds onto your machine and then load them into the database. The public corpus lives in
the `OvercastCommunity/CommunityMaps` repository, and its Capture-the-Wool maps are in the `ctw`
subdirectory. That repository holds around 190 maps and is large, so clone it sparsely and check out only
the one map you want; a single map is enough to prove the flow works.

Clone with no blobs and no working tree, then narrow the checkout to one map folder before materialising it.

```bash
git clone --filter=blob:none --no-checkout --depth 1 \
  https://github.com/OvercastCommunity/CommunityMaps.git
cd CommunityMaps
git sparse-checkout init --cone
git sparse-checkout set ctw/acapulco
git checkout
```

You now have `ctw/acapulco` on disk, containing the map's `map.xml`, its `region/*.mca` world files, and a
`level.dat` — a little over a megabyte, rather than the whole corpus. To load more maps later, add their
folders to the same `git sparse-checkout set` command.

Loading a map into the studio is a two-step pipeline. First scan the world and parse its `map.xml` into an
importer-ready output directory, which produces an `xml_data.json` plus the feature parquet files. Point the
scanner at the map folder you checked out and at an output directory of your choosing.

```bash
dotnet run --project tools/PgmStudio.RoundTrip -- \
  --scan-out CommunityMaps/ctw/acapulco /path/to/imports-out
```

Then import that output directory into MariaDB. The importer reuses the same connection string as the API,
so the map lands in the database the studio reads.

```bash
dotnet run --project src/PgmStudio.Import -- /path/to/imports-out
```

The importer prints a per-map summary of what it loaded — regions, filters, wools, spawns, and block
counts — and verifies that the database rows round-trip back to the source data. When it finishes, refresh
the Edit stage in the studio and the map appears there, ready to open and refine. You can confirm from the
command line too.

```bash
curl http://localhost:7894/api/maps
```

If you want the studio to be able to re-scan a map's Minecraft world on demand while you edit it — or to
run the scan step across a whole folder of maps at once with `--scan-out-all` — tell the app where those
worlds live by pointing its maps roots at your checkout. The app looks for a map's world at
`<root>/<slug>/region`, so a root of `.../CommunityMaps/ctw` resolves `acapulco` to
`.../CommunityMaps/ctw/acapulco/region`. Set it with an environment variable before launching the app, where
the trailing `__0` selects the first entry of the roots list.

```bash
export MapsRoots__0="/absolute/path/to/CommunityMaps/ctw"
```

You can list several roots as `MapsRoots__0`, `MapsRoots__1`, and so on, or edit the `MapsRoots` array in
`src/PgmStudio.Api/appsettings.Development.json` directly if you prefer keeping the paths in a file.

## Load sketches and plans

Sketches and plans are not loaded from files the way finished maps are; they live inside the studio. A
sketch is a draft that you create with the New sketch action and that is saved in the database as you work,
so you load one by opening it again from the Sketch stage and continuing to draw. There is no separate
sketch file to import.

Plans are the exception, because the plan editor exchanges them as `*.plan.json` files. Open the plan
editor from the Plan link in the landing footer or at `http://localhost:7894/plan-editor`, and use its
Import button to load a plan file from disk; Export writes the current plan back out as `*.plan.json`, and
your in-progress edits also autosave to the browser. The repository ships example plans under `tools/seeds`
— for instance `tools/seeds/base-4team.plan.json` — that you can import to see a complete plan and then
Compile into a draft map, which hands off to the rest of the lifecycle.

## Troubleshooting

If the API refuses to start with a message about the schema being out of date, run the
`--migrate-only` command shown above; the message names the pending migrations and the exact command to
apply them. If instead you see a connection error, check that the MariaDB server is running and that the
connection string you set with user secrets or the environment variable matches the database and password
you created.

The test suite runs one project at a time rather than through `dotnet test`, because the .NET 10 SDK
removed the runner that command relied on. Run a given test project directly, for example
`dotnet run --project tests/PgmStudio.Pgm.Tests`.

Two kinds of map are rejected by design rather than loaded incorrectly. Very old maps that predate PGM's
id-based regions and filters (proto below 1.4.0) and modern post-flattening worlds (a map declaring a
minimum server version of 1.13.0 or newer) both fail the scan with an unsupported-map error. Nearly all of
the Capture-the-Wool corpus is in the supported range, so this only affects a small handful of maps.
