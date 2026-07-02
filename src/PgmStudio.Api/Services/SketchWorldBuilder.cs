using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>The synthesised world plus the world spawn and a <em>resolved</em> intent — the authored intent
/// with spawn/wool positions snapped to the structures the world actually places and each monument's
/// location filled in from its in-cube air cell, so the exported <c>map.xml</c> agrees with the world.</summary>
public sealed record SketchWorld(VoxelWorld World, int SpawnX, int SpawnY, int SpawnZ, MapIntent ResolvedIntent);

/// <summary>
/// Assembles a playable Anvil world for a sketch-originated map from its sketch layout + authoring intent
/// (docs/contracts/sketch-world-export.md): terrain from the rasterised columns, a wool cage at each wool,
/// a spawn cube + auto-wired monuments at each team spawn, and the observer platform. Pure — no DB, no IO —
/// so it unit-tests directly.
/// </summary>
public static class SketchWorldBuilder
{
    public static SketchWorld Build(string layoutJson, MapIntent intent)
    {
        var columns = SketchRasterizer.RasterizeColumns(layoutJson);
        var terrain = SketchTerrainBuilder.Build(columns);
        var world = terrain.World;
        int Surface(int x, int z) => PositionSnap.SurfaceY((x, z), terrain.SurfaceTop, 1);

        var teams = intent.Teams ?? [];
        var wools = intent.Wools ?? [];

        // ── Wool cages (anchored on the snapped wool spawn) ──────────────────────────────────────────
        var resolvedWools = new List<WoolIntent>(wools.Count);
        var woolFloor = new int[wools.Count];
        var woolCell = new (int X, int Z)[wools.Count];
        for (var i = 0; i < wools.Count; i++)
        {
            var w = wools[i];
            var slug = ColorSlug(w, teams);
            var (sx, sz) = PositionSnap.SnapXZ(w.Spawn.X, w.Spawn.Z);
            var fy = Surface(sx, sz);
            WoolCageStamper.Stamp(world, sx, sz, fy, WoolColors.WoolDamage(slug));
            woolFloor[i] = fy;
            woolCell[i] = (sx, sz);
            resolvedWools.Add(w);   // monuments filled in below, once spawn cubes place them
        }

        // ── Spawn cubes + monuments; capture each monument's world air-cell coord ────────────────────
        // monLoc[(woolIndex, team)] = the air cell where that team places that wool.
        var monLoc = new Dictionary<(int Wool, string Team), Pt>();
        var resolvedSpawns = new List<SpawnIntent>(intent.Spawns.Count);
        foreach (var s in intent.Spawns)
        {
            var (sx, sz) = PositionSnap.SnapXZ(s.Point.X, s.Point.Z);
            var fy = Surface(sx, sz);
            var facing = PositionSnap.FacingFromYaw(s.Yaw);

            var captured = wools.Select((w, i) => (w, i))
                .Where(x => x.w.Monuments.Any(m => m.Team == s.Team)).ToList();
            var placed = SpawnCubeStamper.Stamp(world, sx, sz, fy, WoolDataForTeam(s.Team, teams), facing,
                [.. captured.Select(x => ColorSlug(x.w, teams))]);

            for (var k = 0; k < placed.Count && k < captured.Count; k++)
                monLoc[(captured[k].i, s.Team)] = new Pt(placed[k].X, placed[k].Y, placed[k].Z);

            resolvedSpawns.Add(new SpawnIntent
            {
                Team = s.Team,
                Point = new Pt(sx, fy + 1, sz),   // player stands on the cube floor
                Protection = s.Protection,
                Yaw = s.Yaw,
            });
        }

        // Fill each wool's monuments with the derived world locations.
        for (var i = 0; i < wools.Count; i++)
        {
            var w = wools[i];
            resolvedWools[i] = new WoolIntent
            {
                Owner = w.Owner,
                Color = w.Color,
                Room = w.Room,
                Spawn = new Pt(woolCell[i].X, woolFloor[i], woolCell[i].Z),
                Monuments = [.. w.Monuments.Select(m => new MonumentIntent
                {
                    Team = m.Team,
                    Location = monLoc.GetValueOrDefault((i, m.Team), m.Location),
                })],
            };
        }

        // ── Observer platform (floating at the authored Y) ───────────────────────────────────────────
        int spawnX, spawnY, spawnZ;
        ObserverIntent? resolvedObserver = null;
        if (intent.Observer is { } obs)
        {
            var (ox, oz) = PositionSnap.SnapXZ(obs.Point.X, obs.Point.Z);
            var platformFloor = Math.Max(1, (int)Math.Round(obs.Point.Y, MidpointRounding.AwayFromZero));
            ObserverPlatformStamper.Stamp(world, ox, oz, platformFloor, intent.Meta?.Name ?? "", intent.Meta?.Authors ?? []);
            (spawnX, spawnY, spawnZ) = (ox, platformFloor + 1, oz);
            resolvedObserver = new ObserverIntent { Point = new Pt(ox, platformFloor + 1, oz), Yaw = obs.Yaw };
        }
        else
        {
            (spawnX, spawnY, spawnZ) = (0, Surface(0, 0) + 1, 0);
        }

        var resolved = new MapIntent
        {
            Teams = intent.Teams,
            MaxPlayers = intent.MaxPlayers,
            Spawns = resolvedSpawns,
            Observer = resolvedObserver ?? intent.Observer,
            Build = intent.Build,
            Wools = resolvedWools,
            Meta = intent.Meta,
            Symmetry = intent.Symmetry,
            IslandTeams = intent.IslandTeams,
        };

        return new SketchWorld(world, spawnX, spawnY, spawnZ, resolved);
    }

    /// <summary>The wool colour slug: the wool's own colour, else its owner team's colour, else white.</summary>
    private static string ColorSlug(WoolIntent w, IReadOnlyList<TeamDef> teams)
    {
        var raw = !string.IsNullOrWhiteSpace(w.Color)
            ? w.Color
            : teams.FirstOrDefault(t => t.Id == w.Owner)?.Color ?? "white";
        return WoolColors.Normalize(raw);
    }

    /// <summary>The wool/clay data value for a team's display colour, tolerating <c>dark </c>/<c>light </c>
    /// prefixes that aren't wool slugs (e.g. "dark red" → red).</summary>
    private static int WoolDataForTeam(string teamId, IReadOnlyList<TeamDef> teams)
    {
        var norm = WoolColors.Normalize(teams.FirstOrDefault(t => t.Id == teamId)?.Color ?? "white");
        if (WoolColors.WoolDamageToColor.Values.Contains(norm)) return WoolColors.WoolDamage(norm);
        foreach (var p in (string[])["dark_", "light_"])
            if (norm.StartsWith(p) && WoolColors.WoolDamageToColor.Values.Contains(norm[p.Length..]))
                return WoolColors.WoolDamage(norm[p.Length..]);
        return WoolColors.WoolDamage(norm);
    }
}
