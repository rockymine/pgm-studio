using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

/// <summary>The synthesised world plus the world spawn and a <em>resolved</em> intent — the authored intent
/// with spawn/wool positions snapped to the structures the world actually places and each monument's
/// location filled in from its in-cube air cell, so the exported <c>map.xml</c> agrees with the world.
/// <see cref="Provenance"/> is which pass claimed each column, kept beside the voxels because a block cannot
/// carry the answer itself (B133) — a caller writing the world to disk persists it alongside
/// (<see cref="WorldProvenanceFile"/>) so a render reading the region back gets the same recorded answer a
/// render taken right after the build already would.</summary>
public sealed record SketchWorld(
    VoxelWorld World, int SpawnX, int SpawnY, int SpawnZ, MapIntent ResolvedIntent, WorldProvenance Provenance);

/// <summary>
/// Assembles a playable Anvil world for a sketch-originated map from its sketch layout + authoring intent
/// (docs/world-export/sketch-world-export.md): terrain from the rasterised columns, a wool cage at each wool,
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

        // ── Provenance (B133) — which pass claimed each column, composited in placement order ────────
        // The rasterizer's own ground claims every column first; every stamp below claims its footprint as
        // Structure over it, so the final answer at a column is whichever pass touched it last. Claimed here
        // rather than derived from the finished blocks, which is exactly what a material-only read cannot do:
        // a plaza the painter finishes in a built-looking material is never reclaimed after this line, so it
        // stays Ground however it is painted.
        var provenance = new WorldProvenance();
        provenance.Claim(terrain.SurfaceTop.Keys, ProvenanceLayer.Ground);

        var teams = intent.Teams ?? [];
        var wools = intent.Wools ?? [];
        // The shells this map is finished with — one for every cage, one for every spawn (structures.md §9).
        var (woolStyle, spawnStyle) = RoomStyleScope.StylesOf(layoutJson);

        // ── The build ceiling, and the one altitude every goal marker hangs at ──────────────────────
        // Both are the author's rule, and both are derived here because here is the first place that knows
        // the answer: twenty blocks over the highest ground the map actually built (G6), and the markers five
        // over that (BuildCeiling). The measurement is the point — SurfaceTop is the terrain the rasterizer
        // laid, read before a single structure, house or tree is stamped on it, so nothing placed on the map
        // can push its own ceiling up. It is written back onto the intent so the <max-build-height> the XML
        // declares and the altitude these markers are stamped at are one number rather than two agreeing
        // by habit.
        var highestGround = terrain.SurfaceTop.Count > 0 ? terrain.SurfaceTop.Values.Max() : 0;
        var maxBuildHeight = Math.Min(BuildCeiling.Of(highestGround), VoxelWorld.MaxHeight - 1);
        intent = intent with { Build = (intent.Build ?? new BuildIntent()) with { MaxHeight = maxBuildHeight } };
        var markerFloor = Math.Clamp(
            maxBuildHeight + BuildCeiling.MarkerOver, 0, VoxelWorld.MaxHeight - GoalMarkerStamper.Size);

        // ── Wool-room bedrock floors (ST1) ──────────────────────────────────────────────────────────
        // Ground, not dressing — the plan fills each wool-room piece solid from y=0 to the surface so the room
        // cannot be tunnelled into from below, and the building is then stamped on top of that. Which is why
        // it goes first: the fill's top block IS the floor course now that a room's floor sinks one course into
        // its platform (WX17), so laid afterwards it buries the floor and the wool pad standing on it.
        foreach (var claim in StampRoomFloors(world, terrain.SurfaceTop, intent.Structures))
            provenance.Claim(claim.Cells, ProvenanceLayer.Structure, claim.Owner);

        // ── Wool cages (framed by their plan piece + entries, or the marker-anchored default) ────────
        var resolvedWools = new List<WoolIntent>(wools.Count);
        var woolFrame = new RoomFrame[wools.Count];
        var woolFloor = new int[wools.Count];
        for (var i = 0; i < wools.Count; i++)
        {
            var w = wools[i];
            var slug = ColorSlug(w, teams);
            var frame = WoolFrame(w);
            var fy = FrameFloor(frame, terrain.SurfaceTop, woolStyle);
            WoolStructureStamper.Stamp(world, new WoolStructure
            {
                Frame = frame, FloorY = fy, WoolSlug = slug, Ground = terrain.SurfaceTop, Shell = woolStyle,
            });
            var woolOwner = $"wool:{i}";
            provenance.ClaimRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ, ProvenanceLayer.Structure, woolOwner);
            // One marker per wool room — the room is already one entry per orbit image (PlanCompiler fans
            // team-outer), so no orbit math is needed here to keep a mirrored board's markers matching.
            GoalMarkerStamper.Stamp(world, (frame.MinX + frame.MaxX) / 2, (frame.MinZ + frame.MaxZ) / 2,
                markerFloor, BlockColors.BlockDamage(slug), GoalMarkerShape.Cube);
            provenance.Claim((frame.MinX + frame.MaxX) / 2, (frame.MinZ + frame.MaxZ) / 2, ProvenanceLayer.Structure, woolOwner);
            woolFrame[i] = frame;
            woolFloor[i] = fy;
            resolvedWools.Add(w);   // monuments filled in below, once spawn cubes place them
        }

        // ── Spawn cubes + monuments; capture each monument's world air-cell coord ────────────────────
        // monLoc[(woolIndex, team)] = the air cell where that team places that wool.
        var monLoc = new Dictionary<(int Wool, string Team), Pt>();
        var resolvedSpawns = new List<SpawnIntent>(intent.Spawns.Count);
        for (var spawnIndex = 0; spawnIndex < intent.Spawns.Count; spawnIndex++)
        {
            var s = intent.Spawns[spawnIndex];
            var room = SpawnRoom(s);
            var frame = room.Frame;
            var fy = FrameFloor(frame, terrain.SurfaceTop, spawnStyle);

            var captured = wools.Select((w, i) => (w, i))
                .Where(x => Capturers(x.w, teams).Contains(s.Team)).ToList();
            var placed = SpawnStructureStamper.Stamp(world, new SpawnStructure
            {
                Frame = frame, FloorY = fy, TeamColor = WoolDataForTeam(s.Team, teams),
                CapturedWools = [.. captured.Select(x => ColorSlug(x.w, teams))], Shell = spawnStyle,
            }).Monuments;
            provenance.ClaimRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ, ProvenanceLayer.Structure, $"spawn:{spawnIndex}");

            for (var k = 0; k < placed.Count && k < captured.Count; k++)
                monLoc[(captured[k].i, s.Team)] = new Pt(placed[k].X, placed[k].Y, placed[k].Z);

            // The spawn's renewable iron: each placeable cube beside the room (WX8); an unplaceable
            // marker stamps nothing — the validator already flagged it (WX9). Each cube is its own claim —
            // it stands apart from the room rather than being part of its shell.
            var ironIndex = 0;
            foreach (var iron in room.Iron)
                if (iron.Placeable)
                {
                    StructureStamper.StampIronCubeAt(world, terrain.SurfaceTop, iron.MinX, iron.MinZ, iron.Size);
                    provenance.ClaimRect(iron.MinX, iron.MinZ, iron.MinX + iron.Size - 1, iron.MinZ + iron.Size - 1,
                        ProvenanceLayer.Structure, $"spawn:{spawnIndex}:iron:{ironIndex++}");
                }

            resolvedSpawns.Add(new SpawnIntent
            {
                Team = s.Team,
                // The player stands on the pad — the exported point follows it (WX5).
                Point = new Pt(frame.Pad.CenterX, fy + 1, frame.Pad.CenterZ),
                // Encase the auto-placed spawn room (unless the author drew their own protection).
                Protection = s.Protection.Count > 0 ? s.Protection
                    : [new Rect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)],
                Yaw = s.Yaw,
                Piece = s.Piece,
                Iron = s.Iron,
            });
        }

        // Fill each wool's monuments with the derived world locations.
        for (var i = 0; i < wools.Count; i++)
        {
            var w = wools[i];
            var frame = woolFrame[i];
            resolvedWools[i] = new WoolIntent
            {
                Owner = w.Owner,
                Color = w.Color,
                // Encase the auto-placed wool cage (unless the author drew their own room).
                Room = w.Room.Count > 0 ? w.Room : [new Rect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)],
                // The wool dispenses from the pad — the exported point follows it (WX5).
                Spawn = new Pt(frame.Pad.CenterX, woolFloor[i], frame.Pad.CenterZ),
                Piece = w.Piece,
                Entries = w.Entries,
                // Only teams that actually got a spawn cube have a placement cell; a capturer without a
                // spawn has no world location, so skip it rather than emit a phantom monument at (0,0,0).
                Monuments = [.. Capturers(w, teams)
                    .Where(team => monLoc.ContainsKey((i, team)))
                    .Select(team => new MonumentIntent { Team = team, Location = monLoc[(i, team)] })],
            };
        }

        // ── Plan-derived structures (entrance redstone, iron cubes, approach walls) ─────────────────
        // Stamped after the cubes so an authoritative layout feature (an iron cube beside a spawn) wins any
        // footprint overlap. The room floors are not among them — they are the ground the rooms stand on and
        // were laid before them.
        StampStructures(world, terrain.SurfaceTop, intent.Structures);
        ClaimStructures(provenance, intent.Structures);

        // ── Build-region outline (ST5) — an unpowered redstone line in the void, one air block clear of the
        // region and of the terrain. Derived here rather than in the compiler because the clearance rule reads
        // the terrain the world actually placed; the areas arrive already fanned, so the marker is symmetric.
        if (intent.Build is { } build)
            BuildMarkerStamper.Stamp(world, BlockRects(build.Areas), BlockRects(build.Holes), terrain.SurfaceTop);

        // ── Destroyables (DTM) — the box is computed once here and carried on the resolved intent, so the
        // region the generator emits is the volume these blocks were stamped into (OB8).
        var resolvedDestroyables = StampDestroyables(world, terrain.SurfaceTop, intent.Destroyables, teams, markerFloor, provenance);
        var resolvedCores = StampCores(world, terrain.SurfaceTop, intent.Cores, teams, markerFloor, provenance);

        // ── Terrain finish — dress the raw stone: team-tinted clay walls, quartz rims, grass surface.
        // Runs last so it reads the finished world; touches only stone, so bedrock and every stamp above stay
        // untouched. The theme is resolved per cell through TerrainThemeScope (a shape override, else the map
        // default — docs/world-export/terrain-painting.md TP10); team ownership is read through TeamTerritory — the canonical
        // islands_json decomposition plus the stored/pre-filled IslandTeams — so the tint agrees with configure.
        TerrainPainter.Paint(world, terrain.SurfaceTop, TerrainThemeScope.ThemeAt(layoutJson), TeamTerritory.DamageAt(terrain.SurfaceTop.Keys, intent));

        // ── Dressing — the terrain's life on top of its finish: flora over the soil, boulders half-buried in
        // it, trees standing on it (docs/world-export/decoration.md). Runs after the painter because the one
        // fact it needs is what the surface now *is* — soil takes flora, a plaza's quartz does not — and the
        // painter has just decided that per cell. Everything here was placed by hand and every prop is fanned
        // across the symmetry orbit, so two teams face the same rock from the same side.
        // The goals handed to the pass are the RESOLVED ones: their boxes were computed above, so the ground
        // read against a goal is the ground its structure occupies rather than a second derivation of it
        // (OB8, and the reason this call sits after the two stamps).
        var goals = intent with { Destroyables = resolvedDestroyables, Cores = resolvedCores };
        var dressed = Decorator.Decorate(world, new DressingContext(
            terrain.SurfaceTop,
            DressingScope.PropsOf(layoutJson),
            DressingScope.ProtectedAt(world, terrain.SurfaceTop, goals),
            DressingScope.SymmetryOf(layoutJson),
            DressingScope.GoalGroundAt(goals)));
        // A dressing-placed building is a structure the author chose, not scenery the way a tree or a boulder
        // is (docs/world-export/decoration.md) — its footprint claims Structure last, over whatever ground
        // provenance the terrain under it carried, the same "later pass wins" rule every stamp above follows.
        // One claim per house per orbit image, so each keeps the owner that names it rather than all of them
        // collapsing into one call with no identity of their own.
        //
        // Claimed from what the pass reported placing, never re-derived from the layout: the pass drops a
        // building whole when any of its images overlaps something already standing, stands over no ground, or
        // fails its turn, and a claim rebuilt from the author's intent cannot see any of that (B202).
        foreach (var claim in dressed.Structures)
            provenance.Claim(claim.Cells, ProvenanceLayer.Structure, claim.Owner);

        // ── Observer platform (floating at the authored Y) ───────────────────────────────────────────
        int spawnX, spawnY, spawnZ;
        ObserverIntent? resolvedObserver = null;
        if (intent.Observer is { } obs)
        {
            var (ox, oz) = PositionSnap.SnapXZ(obs.Point.X, obs.Point.Z);
            var platformFloor = SafeFloor((int)Math.Round(obs.Point.Y, MidpointRounding.AwayFromZero));
            var authorNames = intent.Meta?.Authors.Select(a => a.Name).ToList() ?? [];
            ObserverPlatformStamper.Stamp(world, ox, oz, platformFloor, intent.Meta?.Name ?? "", authorNames);
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

        // `with`, never a fresh MapIntent, for the reason SymmetryExpander already records: a rebuild that
        // names its fields drops every slice added after it was written. This one had dropped WaterLanes —
        // the export re-projects from this copy and the lane generator clears the document before writing, so
        // a lane an author had stored was deleted and never rewritten, on every sketch-built map.
        var resolved = intent with
        {
            Spawns = resolvedSpawns,
            Observer = resolvedObserver ?? intent.Observer,
            Wools = resolvedWools,
            Destroyables = resolvedDestroyables,
            Cores = resolvedCores,
        };

        return new SketchWorld(world, spawnX, spawnY, spawnZ, resolved, provenance);
    }

    // The bedrock under every wool room, laid before the rooms themselves (see the call site).
    private static List<StructureClaim> StampRoomFloors(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, StructureIntent? s)
    {
        // The claim is the cells the foundation filled, walked from the same four integers the stamp took.
        // It was a rect carried across by hand, and the two conventions did not match: the stamp's footprint
        // is max-EXCLUSIVE and ClaimRect's is max-inclusive, so every room floor claimed a column past its own
        // bedrock on each axis — 121 columns for a 10x10 floor that fills 100. The min end disagreed too,
        // truncation against Math.Floor, which parts company on a negative fractional bound.
        var claims = new List<StructureClaim>();
        var floors = s?.RoomFloors ?? [];
        for (var i = 0; i < floors.Count; i++)
        {
            var f = floors[i];
            int minX = (int)f.MinX, minZ = (int)f.MinZ, maxX = (int)f.MaxX, maxZ = (int)f.MaxZ;
            StructureStamper.StampFoundation(world, surface, minX, minZ, maxX, maxZ);
            claims.Add(new StructureClaim($"roomfloor:{i}",
                [.. StructureStamper.FoundationCells(minX, minZ, maxX, maxZ)]));
        }
        return claims;
    }

    // Stamp the plan-compiled layout structures (already resolved + fanned to block coords) onto the world.
    private static void StampStructures(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, StructureIntent? s)
    {
        if (s is null) return;
        foreach (var w in s.Walls)
        {
            StructureStamper.StampWall(world, w.MinX, w.MinZ, w.MaxX, w.MaxZ, w.TopY);
            WallDefenseChest.Stamp(world, surface, w.MinX, w.MinZ, w.MaxX, w.MaxZ, w.ChestOnMinFace);
        }
        foreach (var ic in s.IronCubes)
            StructureStamper.StampIronCube(world, surface, ic.X, ic.Z);
        foreach (var line in s.RedstoneLines)
            StructureStamper.StampRedstoneLine(world, surface, line.X1, line.Z1, line.X2, line.Z2);
    }

    // Provenance for the same plan-derived structures StampStructures just stamped — a separate pass rather
    // than folded into it, so the claim sits beside the stamp call it describes (see the call site) instead
    // of inside a method whose job is placing blocks.
    private static void ClaimStructures(WorldProvenance provenance, StructureIntent? s)
    {
        if (s is null) return;
        for (var i = 0; i < s.Walls.Count; i++)
        {
            var w = s.Walls[i];
            provenance.ClaimRect(w.MinX, w.MinZ, w.MaxX, w.MaxZ, ProvenanceLayer.Structure, $"wall:{i}");
        }
        for (var i = 0; i < s.IronCubes.Count; i++)
        {
            var (minX, minZ, maxX, maxZ) = StructureStamper.IronCubeFootprint(s.IronCubes[i].X, s.IronCubes[i].Z);
            provenance.ClaimRect(minX, minZ, maxX, maxZ, ProvenanceLayer.Structure, $"ironcube:{i}");
        }
        for (var i = 0; i < s.RedstoneLines.Count; i++)
        {
            // The run itself, from the stamper's own walk, rather than the bounding rect of its two ends. The
            // two are the same set only while the run is axis-aligned — which is what EntranceRow produces and
            // nothing states as a rule, so a diagonal run would have claimed its whole box.
            var line = s.RedstoneLines[i];
            provenance.Claim(
                StructureStamper.RedstoneLineCells(line.X1, line.Z1, line.X2, line.Z2),
                ProvenanceLayer.Structure, $"redstoneline:{i}");
        }
    }

    // Intent footprints are a fractional corner pair over whole world blocks (max exclusive); the stampers
    // work in integers.
    private static IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> BlockRects(IEnumerable<Rect> rects)
        => rects.Select(r => ((int)Math.Floor(r.MinX), (int)Math.Floor(r.MinZ),
                              (int)Math.Ceiling(r.MaxX), (int)Math.Ceiling(r.MaxZ)));

    // Stamp each destroyable's structure and return the intent with every box resolved. One DestroyableBox
    // call per objective feeds both the blocks and (via the returned intent) the emitted region — the box is
    // never derived twice (OB8). An unknown style is dropped rather than defaulted: the plan validator
    // already errors on it, so reaching here means something upstream skipped the gate, and stamping the
    // wrong structure would hide that.
    private static List<DestroyableIntent>? StampDestroyables(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, List<DestroyableIntent>? destroyables,
        IReadOnlyList<TeamDef> teams, int markerFloor, WorldProvenance provenance)
    {
        if (destroyables is null) return null;
        var resolved = new List<DestroyableIntent>(destroyables.Count);
        for (var i = 0; i < destroyables.Count; i++)
        {
            var b = destroyables[i];
            if (!DestroyableStyles.TryParse(b.Style, out var style)) continue;
            var owner = $"destroyable:{i}";
            var (ax, az) = PositionSnap.SnapXZ(b.Anchor.X, b.Anchor.Z);
            var box = ObjectiveStamper.DestroyableBox(surface, ax, az, style, b.Float);
            ObjectiveStamper.StampDestroyable(world, box, style, DestroyableMaterials.BlockId(b.Materials));
            provenance.ClaimRect(box.MinX, box.MinZ, box.MaxX, box.MaxZ, ProvenanceLayer.Structure, owner);

            // A buried bedrock plate under the goal, one course beneath the ground's own surface, so the
            // monument cannot be undermined from below and the ground under it cannot be mined away.
            var (platformMinX, platformMinZ, platformMaxX, platformMaxZ) =
                ObjectiveFootprint.Centred(ax, az, StructureStamper.PlatformSize, StructureStamper.PlatformSize);
            StructureStamper.StampPlatform(world, surface, platformMinX, platformMinZ, platformMaxX, platformMaxZ);
            provenance.ClaimRect(platformMinX, platformMinZ, platformMaxX, platformMaxZ, ProvenanceLayer.Structure, owner);

            // One marker per destroyable — already one orbit image per entry (PlanCompiler fans team-outer).
            GoalMarkerStamper.Stamp(world, ax, az, markerFloor, WoolDataForTeam(b.Owner, teams), GoalMarkerShape.Cross);
            provenance.Claim(ax, az, ProvenanceLayer.Structure, owner);

            resolved.Add(new DestroyableIntent
            {
                Owner = b.Owner, Name = b.Name, Style = b.Style, Materials = b.Materials,
                Anchor = b.Anchor, Float = b.Float, Box = box,
            });
        }
        return resolved;
    }

    // Stamp each core's casing + lava and return the intent with every box resolved — the destroyable's
    // shape, and the same one-box rule (OB8). Obsidian is not a knob (DC1): PGM defaults to it and the
    // corpus is effectively unanimous.
    private static List<CoreIntent>? StampCores(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, List<CoreIntent>? cores,
        IReadOnlyList<TeamDef> teams, int markerFloor, WorldProvenance provenance)
    {
        if (cores is null) return null;
        var resolved = new List<CoreIntent>(cores.Count);
        for (var i = 0; i < cores.Count; i++)
        {
            var c = cores[i];
            var owner = $"core:{i}";
            var (ax, az) = PositionSnap.SnapXZ(c.Anchor.X, c.Anchor.Z);
            var box = ObjectiveStamper.CoreBox(surface, ax, az, c.Size, c.Height, c.Float);
            ObjectiveStamper.StampCore(world, box, Blocks.Obsidian, c.Shell, c.OpenTop);
            provenance.ClaimRect(box.MinX, box.MinZ, box.MaxX, box.MaxZ, ProvenanceLayer.Structure, owner);

            // One marker per core — same already-fanned-per-orbit-image reasoning as the destroyable's.
            GoalMarkerStamper.Stamp(world, ax, az, markerFloor, WoolDataForTeam(c.Owner, teams), GoalMarkerShape.Cross);
            provenance.Claim(ax, az, ProvenanceLayer.Structure, owner);

            resolved.Add(new CoreIntent
            {
                Owner = c.Owner, Name = c.Name, Anchor = c.Anchor,
                Size = c.Size, Height = c.Height, Shell = c.Shell, OpenTop = c.OpenTop,
                Float = c.Float, Leak = c.Leak, Box = box,
            });
        }
        return resolved;
    }

    /// <summary>The XZ footprints (min/max inclusive) of the renewable iron cubes — the regions the map.xml
    /// renewables wiring covers so the mined ore regrows (ST2): every placeable spawn-side cube (WX8) plus
    /// any legacy <see cref="IronCube.Renew"/> directives an older stored intent still carries. Empty when
    /// there are none.</summary>
    public static IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> RenewableCubeFootprints(MapIntent intent)
    {
        var footprints = new List<(int MinX, int MinZ, int MaxX, int MaxZ)>();
        foreach (var s in intent.Spawns)
            foreach (var iron in SpawnRoom(s).Iron)
                if (iron.Placeable)
                    footprints.Add((iron.MinX, iron.MinZ, iron.MinX + iron.Size - 1, iron.MinZ + iron.Size - 1));
        if (intent.Structures is { } structures)
            footprints.AddRange(structures.IronCubes.Where(c => c.Renew)
                .Select(c => StructureStamper.IronCubeFootprint(c.X, c.Z)));
        return footprints;
    }

    // A shell's roof sits at floorY + its style's top layer, so the floor must leave that much headroom below
    // the world ceiling — clamp every structure floor here so an author-elevated island can't push a stamp
    // past 255. A taller shell clamps lower, which is why the style is read rather than assumed.
    /// <summary>A floor low enough that the shell over it cannot run past the world ceiling. A gable's ridge
    /// climbs with the footprint it spans, so the headroom is reserved against that rather than against the
    /// flat answer — otherwise a tall roof near the top of the world is clipped instead of lowered.</summary>
    internal static int SafeFloor(
        int y, HouseStyle? style = null, int width = 0, int depth = 0, RoomEdge? front = null)
        => Math.Clamp(y, 1, VoxelWorld.MaxHeight - 1 - (style is null
            ? HouseStyle.MaxTopLayer
            : style.TopLayerOver(width, depth, front)));

    /// <summary>The floor a room shell rests on: the highest surface over the columns its footprint spans —
    /// not the one at its marker, which is a grid line whose side does not survive the symmetry orbit.
    ///
    /// <para><b>The floor course replaces the ground's top block rather than sitting on it.</b> A surface top is
    /// the first <em>air</em> cell over a column, so laying the floor there puts it a block proud of the terrain
    /// and a player steps up on the way in and drops on the way out. One block down is the ground's own top
    /// course, so the room's floor sinks into the platform and comes out flush — and a floor deeper than one
    /// course digs further in, which is what a stack claimed downward is for. It overwrites what it lands on,
    /// the bedrock foundation included, because the foundation is what the building stands on and the floor is
    /// what stands on the foundation.</para></summary>
    public static int FrameFloor(
        RoomFrame frame, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, HouseStyle? style = null)
        => SafeFloor(
            PositionSnap.SurfaceYOver(surfaceTop, frame.MinX, frame.MinZ, frame.MaxX - 1, frame.MaxZ - 1, 1) - 1,
            style, frame.Width, frame.Depth,
            // The frame's own door, because a shed and a saltbox climb with the span perpendicular to it: a
            // room whose entry contract puts the door on the long wall reserves more headroom than the same
            // room fronting the short one.
            frame.Doors.Count > 0 ? frame.Doors[0].Edge : null);

    /// <summary>The frame the export stamps for a wool: resolved from its plan piece + entry interfaces when
    /// it compiled from a plan (WX1/WX6), else the legacy marker-anchored default. Shared with the structure
    /// preview so the drawn box and the stamped shell cannot disagree.</summary>
    public static RoomFrame WoolFrame(WoolIntent w)
    {
        if (w.Piece is { } piece && w.Entries.Count > 0)
        {
            var (markerX, markerZ) = PositionSnap.SnapHalfXZ(w.Spawn.X, w.Spawn.Z);
            var frame = RoomFrames.Resolve(
                (int)piece.MinX, (int)piece.MinZ, (int)piece.MaxX, (int)piece.MaxZ, markerX, markerZ,
                [.. w.Entries.Select(e => (e.MinX, e.MinZ, e.MaxX, e.MaxZ))], null, out _);
            if (frame is not null) return frame;
        }
        return DefaultFrame(w.Spawn.X, w.Spawn.Z, null);
    }

    /// <inheritdoc cref="WoolFrame"/>
    /// <remarks>A spawn resolves its room together with the piece's iron markers: the shell may yield to a
    /// cube, and an unfittable marker comes back unplaceable (WX8/WX9) — nothing stamps for it.</remarks>
    public static ResolvedRoom SpawnRoom(SpawnIntent s)
    {
        var doorEdge = PositionSnap.FacingFromYaw(s.Yaw);
        if (s.Piece is { } piece)
        {
            var (markerX, markerZ) = PositionSnap.SnapHalfXZ(s.Point.X, s.Point.Z);
            var room = RoomFrames.ResolveRoom(
                (int)piece.MinX, (int)piece.MinZ, (int)piece.MaxX, (int)piece.MaxZ, markerX, markerZ,
                [], doorEdge,
                [.. s.Iron.Select(iron => PositionSnap.SnapHalfXZ(iron.X, iron.Z))], out _);
            if (room is not null) return room;
        }
        return new ResolvedRoom(DefaultFrame(s.Point.X, s.Point.Z, doorEdge), []);
    }

    // The legacy default: the room a 10×10 piece centred on the integer-snapped marker resolves to — the
    // original 8×8 shell, with a door per wall for a wool cage or the single yaw door for a spawn. Also the
    // fallback when an authored piece refuses to frame (the validator gates plan exports, so reaching that
    // fallback means a hand-edited intent — stamping the default beats failing the export).
    private static RoomFrame DefaultFrame(double x, double z, RoomEdge? spawnDoorEdge)
    {
        var (anchorX, anchorZ) = PositionSnap.SnapXZ(x, z);
        int minX = anchorX - 5, minZ = anchorZ - 5, maxX = anchorX + 5, maxZ = anchorZ + 5;
        List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = spawnDoorEdge is null
            ?
            [
                (minX, minZ, maxX, minZ), (minX, maxZ, maxX, maxZ),
                (minX, minZ, minX, maxZ), (maxX, minZ, maxX, maxZ),
            ]
            : [];
        return RoomFrames.Resolve(minX, minZ, maxX, maxZ, anchorX, anchorZ, entries, spawnDoorEdge, out _)!;
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
        return BlockColors.Normalize(raw);
    }

    /// <summary>The wool/clay data value for a team's display colour. <see cref="BlockColors.BlockDamage"/>
    /// resolves chat-colour team palettes (gold, aqua, dark aqua, …) to their nearest wool.</summary>
    private static int WoolDataForTeam(string teamId, IReadOnlyList<TeamDef> teams)
        => BlockColors.BlockDamage(teams.FirstOrDefault(t => t.Id == teamId)?.Color ?? "white");

}
