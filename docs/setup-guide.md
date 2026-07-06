# Setup guide

PGM Studio is a web app for authoring and analysing PGM Capture-the-Wool Minecraft maps. It runs as a
local ASP.NET Core server that serves a Blazor UI in your browser and keeps its map data in MariaDB. This
guide gets it running on Windows, Linux, or macOS.

## What the tools do

Sketch is where you draw a new map's islands on a blank canvas and let the studio build a terrain world
from them. Configure is the guided wizard that turns a terrain-only world into a playable map by writing
its `map.xml`. Edit opens a map that already has a `map.xml` so you can refine its regions, teams, wools,
and objectives. The plan editor is a coarse grid layout tool for laying out a map at a higher level and
compiling it into a new draft.

## Prerequisites

You need two things installed: the .NET 10 SDK and a MariaDB server (10.11 or newer).

| Platform | .NET 10 SDK | MariaDB |
| --- | --- | --- |
| Windows | `winget install Microsoft.DotNet.SDK.10` | Installer from `https://mariadb.org/download/` |
| macOS | `brew install dotnet-sdk` | `brew install mariadb` then `brew services start mariadb` |
| Linux | `sudo apt-get install -y dotnet-sdk-10.0` | `sudo apt-get install -y mariadb-server` |

Confirm the SDK with `dotnet --version`, which should print a `10.0.x` version.

## Install and run

Clone the repository and move into it.

```bash
git clone https://github.com/rockymine/pgm-studio.git
cd pgm-studio
```

Create the database and the user the studio connects as. Open a MariaDB shell as an administrator
(`sudo mariadb` on Linux, `mysql -u root -p` on Windows and macOS) and run the following.

```sql
CREATE DATABASE IF NOT EXISTS pgm_studio;
CREATE DATABASE IF NOT EXISTS pgm_studio_test;
CREATE USER IF NOT EXISTS 'pgm'@'localhost' IDENTIFIED BY 'pgm_dev_pw';
GRANT ALL ON pgm_studio.*      TO 'pgm'@'localhost';
GRANT ALL ON pgm_studio_test.* TO 'pgm'@'localhost';
FLUSH PRIVILEGES;
```

Give the app the connection string. It is a secret, so it is stored with .NET user secrets rather than
committed. Set it once against the API project.

```bash
dotnet user-secrets set "ConnectionStrings:PgmStudio" \
  "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;" \
  --project src/PgmStudio.Api
```

Apply the database schema.

```bash
dotnet run --project src/PgmStudio.Import -- --migrate-only
```

Run the app. On Linux and macOS the helper script builds once and starts the server on port 7894 in the
background; on Windows run the API project directly.

```bash
./tools/dev.sh restart          # Linux / macOS
dotnet run --project src/PgmStudio.Api   # Windows
```

Open `http://localhost:7894/` in your browser.

## Loading existing maps

The public map corpus lives in the `OvercastCommunity/CommunityMaps` repository, with the Capture-the-Wool
maps under its `ctw` folder. Clone it next to the studio.

```bash
git clone https://github.com/OvercastCommunity/CommunityMaps.git
```

Point the studio at that folder so it can find each map's world, then run the app. On Linux and macOS:

```bash
export MapsRoots__0="/absolute/path/to/CommunityMaps/ctw"
```

To load a map into the studio, scan its world and import it into the database. For example, for the map
`acapulco`:

```bash
dotnet run --project tools/PgmStudio.RoundTrip -- --scan-out CommunityMaps/ctw/acapulco ./imports-out
dotnet run --project src/PgmStudio.Import -- ./imports-out
```

The map now appears in the Edit stage, ready to open.

## Sketches and plans

Sketches are created and saved inside the studio, so you resume one by reopening it from the Sketch stage.
Plans are shared as `*.plan.json` files: use the plan editor's Import and Export buttons to load and save
them, and try the examples in `tools/seeds` to see complete plans you can compile into a draft.
