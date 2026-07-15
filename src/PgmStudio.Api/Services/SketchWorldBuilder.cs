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
        // A cube rests on the columns its shell spans, not on the one column at its anchor — the anchor is a
        // grid line, and picking a side of it resolves different heights across the symmetry orbit.
        int CubeFloor(int ax, int az)
        {
            var (minX, minZ, maxX, maxZ) = CubeStamper.Footprint(ax, az);
            return SafeFloor(PositionSnap.SurfaceYOver(terrain.SurfaceTop, minX, minZ, maxX, maxZ, 1));
        }

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
            var fy = CubeFloor(sx, sz);
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
            var fy = CubeFloor(sx, sz);
            var facing = PositionSnap.FacingFromYaw(s.Yaw);

            var captured = wools.Select((w, i) => (w, i))
                .Where(x => Capturers(x.w, teams).Contains(s.Team)).ToList();
            var placed = SpawnCubeStamper.Stamp(world, sx, sz, fy, WoolDataForTeam(s.Team, teams), facing,
                [.. captured.Select(x => ColorSlug(x.w, teams))]);

            for (var k = 0; k < placed.Count && k < captured.Count; k++)
                monLoc[(captured[k].i, s.Team)] = new Pt(placed[k].X, placed[k].Y, placed[k].Z);

            resolvedSpawns.Add(new SpawnIntent
            {
                Team = s.Team,
                Point = new Pt(sx, fy + 1, sz),   // player stands on the cube floor
                // Encase the auto-placed spawn cube (unless the author drew their own protection).
                Protection = s.Protection.Count > 0 ? s.Protection : [CubeRect(sx, sz)],
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
                // Encase the auto-placed wool cage (unless the author drew their own room).
                Room = w.Room.Count > 0 ? w.Room : [CubeRect(woolCell[i].X, woolCell[i].Z)],
                Spawn = new Pt(woolCell[i].X, woolFloor[i], woolCell[i].Z),
                // Only teams that actually got a spawn cube have a placement cell; a capturer without a
                // spawn has no world location, so skip it rather than emit a phantom monument at (0,0,0).
                Monuments = [.. Capturers(w, teams)
                    .Where(team => monLoc.ContainsKey((i, team)))
                    .Select(team => new MonumentIntent { Team = team, Location = monLoc[(i, team)] })],
            };
        }

        // ── Plan-derived structures (bedrock room floors, entrance redstone, iron cubes, approach walls) ──
        // Stamped after the cubes so an authoritative layout feature (an iron cube beside a spawn) wins any
        // footprint overlap; room floors sit below the cage floor and the rest are clear of the cubes.
        StampStructures(world, terrain.SurfaceTop, intent.Structures);

        // ── Destroyables (DTM) — the box is computed once here and carried on the resolved intent, so the
        // region the generator emits is the volume these blocks were stamped into (OB8).
        var resolvedDestroyables = StampDestroyables(world, terrain.SurfaceTop, intent.Destroyables);

        // ── Observer platform (floating at the authored Y) ───────────────────────────────────────────
        int spawnX, spawnY, spawnZ;
        ObserverIntent? resolvedObserver = null;
        if (intent.Observer is { } obs)
        {
            var (ox, oz) = PositionSnap.SnapXZ(obs.Point.X, obs.Point.Z);
            var platformFloor = SafeFloor((int)Math.Round(obs.Point.Y, MidpointRounding.AwayFromZero));
            ObserverPlatformStamper.Stamp(world, ox, oz, platformFloor, intent.Meta?.Name ?? "", intent.Meta?.Authors ?? []);
            (spawnX, spawnY, spawnZ) = (ox, platformFloor + 1, oz);
            resolvedObserver = new ObserverIntent { Point = new Pt(ox, platformFloor + 1, oz), Yaw = obs.Yaw };
        }
        else
        {
            // No observer authored: stand the world spawn on a real terrain column (the one nearest origin)
            // rather than at (0, fallback, 0), which would float over the void when nothing is drawn there.
            var (gx, gz) = terrain.SurfaceTop.Count > 0
                ? terrain.SurfaceTop.Keys.OrderBy(k => (long)k.X * k.X + (long)k.Z * k.Z).ThenBy(k => k.X).ThenBy(k => k.Z).First()
                : (0, 0);
            (spawnX, spawnY, spawnZ) = (gx, Surface(gx, gz) + 1, gz);
        }

        var resolved = new MapIntent
        {
            Teams = intent.Teams,
            MaxPlayers = intent.MaxPlayers,
            Spawns = resolvedSpawns,
            Observer = resolvedObserver ?? intent.Observer,
            Build = intent.Build,
            Wools = resolvedWools,
            Destroyables = resolvedDestroyables,
            Meta = intent.Meta,
            Symmetry = intent.Symmetry,
            IslandTeams = intent.IslandTeams,
            Structures = intent.Structures,
        };

        return new SketchWorld(world, spawnX, spawnY, spawnZ, resolved);
    }

    // Stamp the plan-compiled layout structures (already resolved + fanned to block coords) onto the world.
    private static void StampStructures(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, StructureIntent? s)
    {
        if (s is null) return;
        foreach (var f in s.RoomFloors)
            StructureStamper.StampRoomFloor(world, surface, (int)f.MinX, (int)f.MinZ, (int)f.MaxX, (int)f.MaxZ);
        foreach (var w in s.Walls)
            StructureStamper.StampWall(world, w.MinX, w.MinZ, w.MaxX, w.MaxZ, w.TopY);
        foreach (var ic in s.IronCubes)
            StructureStamper.StampIronCube(world, surface, ic.X, ic.Z);
        foreach (var line in s.RedstoneLines)
            StructureStamper.StampRedstoneLine(world, surface, line.X1, line.Z1, line.X2, line.Z2);
    }

    // Stamp each destroyable's structure and return the intent with every box resolved. One DestroyableBox
    // call per objective feeds both the blocks and (via the returned intent) the emitted region — the box is
    // never derived twice (OB8). An unknown style is dropped rather than defaulted: the plan validator
    // already errors on it, so reaching here means something upstream skipped the gate, and stamping the
    // wrong structure would hide that.
    private static List<DestroyableIntent>? StampDestroyables(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, List<DestroyableIntent>? destroyables)
    {
        if (destroyables is null) return null;
        var resolved = new List<DestroyableIntent>(destroyables.Count);
        foreach (var b in destroyables)
        {
            if (!DestroyableStyles.TryParse(b.Style, out var style)) continue;
            var (ax, az) = PositionSnap.SnapXZ(b.Anchor.X, b.Anchor.Z);
            var box = ObjectiveStamper.DestroyableBox(surface, ax, az, style, b.Float);
            ObjectiveStamper.StampDestroyable(world, box, style, MaterialId(b.Materials));
            resolved.Add(new DestroyableIntent
            {
                Owner = b.Owner, Name = b.Name, Style = b.Style, Materials = b.Materials,
                Anchor = b.Anchor, Float = b.Float, Box = box,
            });
        }
        return resolved;
    }

    // The block a destroyable's `materials` names. The closed four-material vocabulary the corpus uses for
    // DTM goals; anything else falls back to obsidian, which is over half of them.
    private static int MaterialId(string materials) => materials.Trim().ToLowerInvariant() switch
    {
        "emerald block" or "emerald_block" => Blocks.EmeraldBlock,
        "gold block" or "gold_block" => Blocks.GoldBlock,
        "ender stone" or "end_stone" or "ender_stone" => Blocks.EndStone,
        _ => Blocks.Obsidian,
    };

    /// <summary>The XZ footprints of the renewable iron cubes (<see cref="IronCube.Renew"/>) — the regions the
    /// map.xml renewables wiring covers so the mined ore regrows (ST2). Empty when there are none.</summary>
    public static IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> RenewableCubeFootprints(MapIntent intent)
        => intent.Structures is { } s
            ? [.. s.IronCubes.Where(c => c.Renew).Select(c => StructureStamper.IronCubeFootprint(c.X, c.Z))]
            : [];

    // A cube's roof sits at floorY + RoofLayer, so the floor must leave that much headroom below the world
    // ceiling — clamp every structure floor here so an author-elevated island can't push a stamp past 255.
    private const int MaxCubeFloor = VoxelWorld.MaxHeight - CubeStamper.RoofLayer - 1;
    internal static int SafeFloor(int y) => Math.Clamp(y, 1, MaxCubeFloor);

    /// <summary>The XZ footprint of the cube anchored on <paramref name="cx"/>/<paramref name="cz"/>
    /// (the integer 2×2 centre) — its blocks span <c>[anchor-Half, anchor+Half-1]</c>, so the rect is
    /// anchor ± Half.</summary>
    private static Rect CubeRect(int cx, int cz)
    {
        const int half = CubeStamper.Half;
        return new Rect(cx - half, cz - half, cx + half, cz + half);
    }

    /// <summary>The teams that capture a wool: its authored monument teams, or — when none were authored
    /// (the monument step is auto-wired away for sketch maps) — every team except the owner.</summary>
    private static IReadOnlyList<string> Capturers(WoolIntent w, IReadOnlyList<TeamDef> teams)
        => w.Monuments.Count > 0
            ? [.. w.Monuments.Select(m => m.Team)]
            : [.. teams.Where(t => t.Id != w.Owner).Select(t => t.Id)];

    /// <summary>The wool colour slug: the wool's own colour, else its owner team's colour, else white.</summary>
    private static string ColorSlug(WoolIntent w, IReadOnlyList<TeamDef> teams)
    {
        var raw = !string.IsNullOrWhiteSpace(w.Color)
            ? w.Color
            : teams.FirstOrDefault(t => t.Id == w.Owner)?.Color ?? "white";
        return WoolColors.Normalize(raw);
    }

    /// <summary>The wool/clay data value for a team's display colour. <see cref="WoolColors.WoolDamage"/>
    /// resolves chat-colour team palettes (gold, aqua, dark aqua, …) to their nearest wool.</summary>
    private static int WoolDataForTeam(string teamId, IReadOnlyList<TeamDef> teams)
        => WoolColors.WoolDamage(teams.FirstOrDefault(t => t.Id == teamId)?.Color ?? "white");
}
