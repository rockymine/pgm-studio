using System.Globalization;
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
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>The synthesised world plus the world spawn and a <em>resolved</em> intent — the authored intent with
/// spawn/wool positions snapped to the structures the world actually places and each monument's location filled
/// in from its in-cube air cell, so the exported <c>map.xml</c> agrees with the world. <see cref="Provenance"/>
/// is which pass claimed each column, kept beside the voxels because a block cannot carry the answer itself — a
/// caller writing the world to disk persists it alongside (<see cref="WorldProvenanceFile"/>) so a render reading
/// the region back gets the same recorded answer a render taken right after the build already would.
/// <para><b>Declined</b> — Everything the build could not do as it was authored: a goal whose material its own
/// size is wrong for or whose structure reaches over the build ceiling (<c>DC3</c>, <c>OB23</c>), and every prop
/// the dressing pass did not place (a <c>DR-*</c> each), carrying the goal's or the prop's id as its subject.
/// None of them stops the work — the world was built — and the two kinds are told apart by the severity each
/// carries: the goals are complaints, since the goal stands and the remark is about how, and every prop is a <see
/// cref="Severity.Decline"/>, since it is not in the world at all. Either way the caller that asked for the build
/// has to be told rather than sent looking in a sidecar. <para>Null when nothing was declined, the same
/// convention the pass itself answers in — read <see cref="Declines"/>, which never is.</para></para>
/// <para><b>Columns</b> — The ground this build stood on, as the rasterizer read it — one solid span per cell.
/// Carried rather than re-derived: every gate that asks where the ground is has to ask the reading the world was
/// actually built from, and a second <c>SketchRasterizer.RasterizeColumns</c> over the same layout is a second
/// answer free to disagree with the first.</para>
/// <para><b>Dressing</b> — What the dressing pass placed and what it declined, as the pass itself reported it.
/// Carried rather than left inside the build because the claim a prop made — the cells a stroke's style, coverage
/// and seed actually decided on — is reachable no other way, and a keep-out computed against a guess at it is a
/// keep-out tuned to the wrong distances.</para></summary>
public sealed record BuiltWorld(
    VoxelWorld World, int SpawnX, int SpawnY, int SpawnZ, MapIntent ResolvedIntent, WorldProvenance Provenance,
    IReadOnlyList<Finding>? Declined = null,
    IReadOnlyList<ColumnSegment>? Columns = null,
    DressingPlacement Dressing = default,
    IReadOnlyDictionary<(int X, int Z), int>? Ground = null)
{
    /// <summary>The <b>terrain's</b> surface, cell by cell — the tops of everything on the board that is not
    /// a made thing. What a pass reading "where does the ground reach here" takes: deriving it from
    /// <see cref="Columns"/> answers with whatever flies over a cell, so a field a balloon stands off reads
    /// as ground thirty blocks up.</summary>
    public IReadOnlyDictionary<(int X, int Z), int> Surface => Ground ?? Empty;

    private static readonly IReadOnlyDictionary<(int X, int Z), int> Empty =
        new Dictionary<(int X, int Z), int>();

    /// <summary>Every prop that did not land, and why. Never null — a caller spreading this into a list of
    /// warnings must not have to tell an absent list from an empty one.</summary>
    public IReadOnlyList<Finding> Declines => Declined ?? [];
}

/// <summary>
/// Assembles a playable Anvil world for a sketch-originated map from its sketch layout + authoring intent
/// (docs/world-export/sketch-world-export.md): terrain from the rasterised columns, a wool cage at each wool,
/// a spawn cube + auto-wired monuments at each team spawn, and the observer platform. Pure — no DB, no IO —
/// so it unit-tests directly.
/// </summary>
public static class WorldBuilder
{

    /// <summary>
    /// Give every entry that will be stamped an id a reader can group on, where nothing upstream did.
    ///
    /// <para>An intent compiled from a plan carries one on every entry, minted as the compiler fanned it, so
    /// this leaves those alone. Everything else reaching a build lands here: an intent the Configure wizard
    /// stored — where the orbit copies are <em>real, hand-correctable entries</em> rather than derived ones, so
    /// nothing recorded says entry 1 is entry 0's mirror — and one assembled by hand in a fixture or a
    /// one-off driver. Both get their list index as the unit and image 0, which is the honest reading of a
    /// list that was never an orbit: separate things, not images of one thing.</para>
    ///
    /// <para>Note that <c>SymmetryExpander</c> also seeds an id as it fans, and that seeding never reaches a
    /// world: the expander runs inside <c>IntentGenerator.Apply</c>, which <c>MapExportComposer</c> calls
    /// <em>after</em> this build. It is the map.xml projection's own path, and the projection does not read the
    /// id at all.</para>
    /// </summary>
    private static MapIntent WithStampIds(MapIntent intent)
    {
        static StampId Seed(StampId stamp, string kind, int index)
            => !string.IsNullOrEmpty(stamp.Kind) ? stamp
             : new StampId(kind, index.ToString(CultureInfo.InvariantCulture), 0);

        return intent with
        {
            Spawns = [.. intent.Spawns.Select((s, i) => s with { Stamp = Seed(s.Stamp, "spawn", i) })],
            Wools = intent.Wools?.Select((w, i) => w with { Stamp = Seed(w.Stamp, "wool", i) }).ToList(),
            Destroyables = intent.Destroyables?.Select((d, i) => d with { Stamp = Seed(d.Stamp, "destroyable", i) }).ToList(),
            Cores = intent.Cores?.Select((c, i) => c with { Stamp = Seed(c.Stamp, "core", i) }).ToList(),
            Structures = intent.Structures is not { } structures ? null : new StructureIntent
            {
                RedstoneLines = [.. structures.RedstoneLines.Select((l, i) => l with { Stamp = Seed(l.Stamp, "redstoneline", i) })],
                IronCubes = [.. structures.IronCubes.Select((c, i) => c with { Stamp = Seed(c.Stamp, "ironcube", i) })],
                Walls = [.. structures.Walls.Select((w, i) => w with { Stamp = Seed(w.Stamp, "wall", i) })],
            },
        };
    }

    public static BuiltWorld Build(string layoutJson, MapIntent intent)
    {
        intent = WithStampIds(intent);
        var columns = SketchRasterizer.RasterizeColumns(layoutJson);
        // Which layers are made things, read before the terrain so the terrain can answer where the GROUND
        // is rather than what is highest. Everything below that rests on the board asks that question.
        var madeLayers = SketchRasterizer.MadeLayers(SketchLayout.Stated(layoutJson));
        var terrain = TerrainBuilder.Build(columns, madeLayers);
        var world = terrain.World;
        int Surface(int x, int z) => PositionSnap.SurfaceY((x, z), terrain.Ground, 1);

        // ── Provenance — which pass claimed each column, composited in placement order ────────
        // The rasterizer's own ground claims every column first; every stamp below claims its footprint as
        // Structure over it, so the final answer at a column is whichever pass touched it last. Claimed here
        // rather than derived from the finished blocks, which is exactly what a material-only read cannot do:
        // a plaza the painter finishes in a built-looking material is never reclaimed after this line, so it
        // stays Ground however it is painted.
        var provenance = new WorldProvenance();
        provenance.Claim(terrain.SurfaceTop.Keys, ProvenancePass.Ground);

        var teams = intent.Teams ?? [];
        var wools = intent.Wools ?? [];
        // The shells this map is finished with — one for every cage, one for every spawn (structures.md §9).
        var (woolStyle, spawnStyle) = RoomStyleScope.StylesOf(layoutJson);

        // ── The build ceiling, and the one altitude every goal marker hangs at ──────────────────────
        // Both are the author's rule (BuildCeiling): twenty blocks over the highest thing the map builds and
        // a player meets, and the markers five over that. **The answer is not known here.** The ceiling
        // clears the buildings as well as the terrain, and the last of them is a house the dressing pass has
        // not placed yet — so the markers are collected as they are decided and stamped once the world is
        // finished, and the goals that top out over the ceiling are complained about at the same point.
        // Every marker on a board hangs at one altitude, so collecting them costs nothing but the order.
        var pendingMarkers = new List<(int X, int Z, int Data, GoalMarkerShape Shape)>();
        var pendingCeiling = new List<(string Kind, string Name, StampId Owner, BlockBox Box)>();

        // ── Wool cages (framed by their plan piece + entries, or the marker-anchored default) ────────
        var resolvedWools = new List<WoolIntent>(wools.Count);
        var woolFrame = new RoomFrame[wools.Count];
        var woolFloor = new int[wools.Count];
        for (var i = 0; i < wools.Count; i++)
        {
            var w = wools[i];
            var slug = ColorSlug(w, teams);
            var frame = WoolFrame(w, woolStyle is not null);
            // The storey this room was stated for, or the top one where it named none.
            var woolGround = terrain.SurfaceFor(w.Layer);
            var fy = FrameFloor(frame, woolGround, woolStyle);
            WoolStructureStamper.Stamp(world, new WoolStructure
            {
                Frame = frame, FloorY = fy, WoolSlug = slug, Ground = woolGround, Shell = woolStyle,
            });
            // The cells the shell actually filled, walked from the stamper's own function. A RoomFrame's
            // bounds are grid lines — its Width is MaxX − MinX — so carrying them into a max-inclusive
            // provenance rect claims a row and a column of ground the room never touched, on the +x/+z side
            // of every image alike, which no rotation maps onto its partner.
            provenance.Claim(StructureStamper.FoundationCells(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ),
                             ProvenancePass.Structure, w.Stamp);
            // One marker per wool room — the room is already one entry per orbit image (PlanCompiler fans
            // team-outer), so no orbit math is needed here to keep a mirrored board's markers matching.
            pendingMarkers.Add(((frame.MinX + frame.MaxX) / 2, (frame.MinZ + frame.MaxZ) / 2,
                BlockColors.BlockDamage(slug), GoalMarkerShape.Cube));
            provenance.Claim((frame.MinX + frame.MaxX) / 2, (frame.MinZ + frame.MaxZ) / 2, ProvenancePass.Structure, w.Stamp);
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
            var room = SpawnRoom(s, spawnStyle is not null);
            var frame = room.Frame;
            var spawnGround = terrain.SurfaceFor(s.Layer);
            var fy = FrameFloor(frame, spawnGround, spawnStyle);

            var captured = wools.Select((w, i) => (w, i))
                .Where(x => Capturers(x.w, teams).Contains(s.Team)).ToList();
            var placed = SpawnStructureStamper.Stamp(world, new SpawnStructure
            {
                Frame = frame, FloorY = fy, TeamColor = WoolDataForTeam(s.Team, teams),
                CapturedWools = [.. captured.Select(x => ColorSlug(x.w, teams))], Shell = spawnStyle,
            }).Monuments;
            provenance.Claim(StructureStamper.FoundationCells(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ),
                             ProvenancePass.Structure, s.Stamp);

            for (var k = 0; k < placed.Count && k < captured.Count; k++)
                monLoc[(captured[k].i, s.Team)] = new Pt(placed[k].X, placed[k].Y, placed[k].Z);

            // The spawn's renewable iron: each placeable cube beside the room (WX8); an unplaceable
            // marker stamps nothing — the validator already flagged it (WX9). Each cube is its own claim —
            // it stands apart from the room rather than being part of its shell.
            var ironIndex = 0;
            foreach (var iron in room.Iron)
                if (iron.Placeable)
                {
                    StructureStamper.StampIronCubeAt(world, spawnGround, iron.MinX, iron.MinZ, iron.Size);
                    provenance.ClaimRect(iron.MinX, iron.MinZ, iron.MinX + iron.Size - 1, iron.MinZ + iron.Size - 1,
                        ProvenancePass.Structure,
                        // The spawn's own unit, qualified by which of its cubes this is: one room's iron is
                        // one thing per marker, and both images of it share the unit the room's stamp names.
                        new StampId("ironcube", $"{s.Stamp.Unit}:{ironIndex++}", s.Stamp.Image));
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
                Footprint = s.Footprint,
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
                // Encase the auto-placed wool cage (unless the author drew their own region).
                Protection = w.Protection.Count > 0
                    ? w.Protection : [new Rect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)],
                // The wool dispenses from the pad — the exported point follows it (WX5).
                Spawn = new Pt(frame.Pad.CenterX, woolFloor[i], frame.Pad.CenterZ),
                Footprint = w.Footprint,
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
        StampStructures(world, terrain.Ground, intent.Structures);
        ClaimStructures(provenance, intent.Structures);

        // ── Build-region outline (ST5) — an unpowered redstone line in the void, one air block clear of the
        // region and of the terrain. Derived here rather than in the compiler because the clearance rule reads
        // the terrain the world actually placed; the areas arrive already fanned, so the marker is symmetric.
        if (intent.Build is { } build)
            BuildMarkerStamper.Stamp(world, BlockRects(build.Areas), BlockRects(build.Holes), terrain.Ground);

        // ── Destroyables (DTM) — the box is computed once here and carried on the resolved intent, so the
        // region the generator emits is the volume these blocks were stamped into (OB8).
        // What a goal could not be built as it was authored. Complaints, never refusals: the world is built
        // and the goal stands, in a material the author did not name or over the height players may build to,
        // and that is a thing the caller has to be told rather than a reason to stop.
        var built = new List<Finding>();
        var resolvedDestroyables = StampDestroyables(
            world, terrain, intent.Destroyables, teams, pendingMarkers, pendingCeiling, provenance,
            built);
        var resolvedCores = StampCores(
            world, terrain, intent.Cores, teams, pendingMarkers, pendingCeiling, provenance,
            built);

        // ── Terrain finish — dress the raw stone: team-tinted clay walls, quartz rims, grass surface.
        // Runs last so it reads the finished world; touches only stone, so bedrock and every stamp above stay
        // untouched. One pass per layer, each storey against its own surface, so a gallery floor takes its own
        // finish rather than whatever roofs it. The theme is resolved per layer and cell through
        // TerrainThemeScope (a shape override, else the map default — docs/world-export/terrain-painting.md
        // TP10); team ownership is read through TeamTerritory — the canonical islands_json decomposition plus
        // the stored/pre-filled IslandTeams — so the tint agrees with configure.
        // The board's symmetry, read once and used twice: the painter folds every cell into the primary image
        // before a pattern samples it (TP21), and the dressing pass below fans each prop across the same orbit.
        var symmetry = DressingScope.SymmetryOf(layoutJson);
        // A made thing is painted over its own span. Ground's bands run from the bedrock course whatever its
        // floor, so only a prop layer hands its floors over; the painter takes what it is given and asks
        // nothing about kinds.
        var madeFloors = madeLayers
            .Where(terrain.FloorByLayer.ContainsKey)
            .ToDictionary(layer => layer, layer => terrain.FloorByLayer[layer]);
        TerrainPainter.Paint(world, terrain.SurfaceByLayer, TerrainThemeScope.ThemeAt(layoutJson),
                             TeamTerritory.DamageAt(terrain.SurfaceTop.Keys, intent), symmetry.Canonical,
                             madeFloors);

        // ── Dressing — the terrain's life on top of its finish: flora over the soil, boulders bedded into
        // it, trees standing on it (docs/world-export/decoration.md). Runs after the painter because the one
        // fact it needs is what the surface now *is* — soil takes flora, a plaza's quartz does not — and the
        // painter has just decided that per cell. Everything here was placed by hand and every prop is fanned
        // across the symmetry orbit, so two teams face the same rock from the same side.
        // The goals handed to the pass are the RESOLVED ones: their boxes were computed above, so the ground
        // read against a goal is the ground its structure occupies rather than a second derivation of it
        // (OB8, and the reason this call sits after the two stamps).
        // The surface the pass reads is the terrain's, not the board's highest: a made thing is not ground.
        // A balloon flying over a field is the top of every column under it, so reading the whole-board tops
        // seats a tree at its envelope and marks the field it flies over as built — and the ground beneath a
        // floating thing is exactly the ground an author decorates.
        var groundTop = terrain.Ground;
        var goals = intent with { Destroyables = resolvedDestroyables, Cores = resolvedCores };
        var dressed = Decorator.Decorate(world, new DressingContext(
            groundTop,
            DressingScope.PropsOf(layoutJson),
            DressingScope.KeptClearAt(world, groundTop, goals, layoutJson),
            symmetry,
            DressingScope.GoalGroundAt(goals),
            DressingScope.GoalClearanceAt(goals),
            terrain.SurfaceByLayer,
            DressingScope.WaypointsOf(goals)));
        // A dressing-placed building is a structure the author chose, not scenery the way a tree or a boulder
        // is (docs/world-export/decoration.md) — its footprint claims Structure last, over whatever ground
        // provenance the terrain under it carried, the same "later pass wins" rule every stamp above follows.
        // One claim per house per orbit image, so each keeps the owner that names it rather than all of them
        // collapsing into one call with no identity of their own.
        //
        // Claimed from what the pass reported placing, never re-derived from the layout: the pass drops a
        // prop whole when any of its images overlaps something already standing, stands over no ground, or
        // fails its turn, and a claim rebuilt from the author's intent cannot see any of that. Every
        // prop, not only the buildings — each carries the layer that says which pass it was.
        foreach (var claim in dressed.Placements)
            provenance.Claim(claim.Cells, claim.Pass, claim.Owner);

        // ── A made thing's own columns, claimed last over what only ever touched the ground round them ──
        // The rasterizer laid a made thing, so without a claim of its own it reads as the ground under it: a
        // balloon flying over a field draws as that field's surface and a house beside one as a house
        // standing on it. It is claimed after the dressing rather than before because the passes between
        // work on the terrain, not on the thing — the harbour fills round a hull and claims every column it
        // filled, which is a true statement about the water and a false one about the ship floating in it.
        // A column a stamp built on is left alone: a room stamped on a deck is a room, and the deck is what
        // it stands on.
        // A column a stamp built on is where the two are in each other's way, and SK18 is raised off exactly
        // this branch: it is the one place both halves have registered, the rasterizer having laid the thing
        // and every stamper having claimed what it wrote. Grouped by the pair so one gantry through one shed
        // is one sentence rather than a sentence per column.
        var shared = new Dictionary<(string Layer, string Built, string Unit), (int Cells, int X, int Z)>();
        foreach (var layer in madeLayers)
            if (terrain.SurfaceByLayer.TryGetValue(layer, out var madeCells))
                foreach (var cell in madeCells.Keys)
                    if (provenance.PassAt(cell.X, cell.Z) != ProvenancePass.Structure)
                        provenance.Claim(cell.X, cell.Z, ProvenancePass.Made);
                    else
                    {
                        var owner = provenance.OwnerAt(cell.X, cell.Z);
                        var standing = owner is { Kind.Length: > 0 } stamp
                            ? stamp.Unit is { Length: > 0 } unit ? $"{stamp.Kind} '{unit}'" : stamp.Kind
                            : "structure";
                        var key = (layer, Built: standing, Unit: owner?.Unit ?? "");
                        shared[key] = shared.TryGetValue(key, out var seen)
                            ? (seen.Cells + 1, seen.X, seen.Z)
                            : (1, cell.X, cell.Z);
                    }

        foreach (var (key, seen) in shared.OrderByDescending(entry => entry.Value.Cells))
            built.Add(new Finding(SketchRules.MadeThingInBuilt,
                $"the made thing '{key.Layer}' and the {key.Built} stand in {seen.Cells} of the same "
                + $"column(s) — first at ({seen.X}, {seen.Z}). Neither pass reads the other: the thing is "
                + "drawn at the floor it states, and what is built seats on the terrain under it with the "
                + "made things taken out, so their blocks interleave and what stands there is one inside the "
                + "other. Raise or move the made thing, or move what it is standing in",
                Severity.Complaint, Subjects: key.Unit.Length > 0 ? [key.Layer, key.Unit] : [key.Layer]));

        // ── The build ceiling, now that everything a player meets is standing ───────────────────────
        // Read here because here is the first place the answer exists: the terrain was laid at the top of
        // this method, the rooms and the goals were stamped in the middle, and the last building on the
        // board is a house the dressing pass has just placed. The number is written back onto the intent, so
        // the <max-build-height> the XML declares and the altitude the markers are stamped at are one number
        // rather than two agreeing by habit.
        var maxBuildHeight = Math.Min(BuildCeiling.Of(HighestBuilt(world, groundTop, provenance, columns, madeLayers)),
                                      VoxelWorld.MaxHeight - 1);
        intent = intent with { Build = (intent.Build ?? new BuildIntent()) with { MaxHeight = maxBuildHeight } };
        var markerFloor = Math.Clamp(
            maxBuildHeight + BuildCeiling.MarkerOver, 0, VoxelWorld.MaxHeight - GoalMarkerStamper.Size);
        foreach (var (mx, mz, data, shape) in pendingMarkers)
            GoalMarkerStamper.Stamp(world, mx, mz, markerFloor, data, shape);
        foreach (var (kind, name, owner, box) in pendingCeiling)
            OverCeiling(built, kind, name, owner, box, maxBuildHeight);

        // ── Biome — the one colour that costs no block. Every chunk the world holds takes its byte from the
        // map's field, folded through the same symmetry the painter uses so a mirrored board answers one
        // biome on both halves. It runs after every pass that could add a chunk, because a chunk that arrives
        // later would otherwise keep the plains it was created with; it writes no blocks, so nothing above
        // cares that it ran at all.
        BiomeScope.Paint(world, BiomeScope.FieldOf(layoutJson), symmetry.Canonical);

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
            var (gx, gz) = terrain.Ground.Count > 0
                ? terrain.Ground.Keys.OrderBy(k => (long)k.X * k.X + (long)k.Z * k.Z).ThenBy(k => k.X).ThenBy(k => k.Z).First()
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

        // One list, in build order: what the build could not raise as authored — a goal over the ceiling, a
        // made thing standing in something stamped — then what the dressing pass did not place. All of them
        // are complaints on a world that exists, so a caller reads one channel.
        List<Finding>? complaints = built.Count > 0 || dressed.Declines.Count > 0
            ? [.. built, .. dressed.Declines]
            : null;
        return new BuiltWorld(world, spawnX, spawnY, spawnZ, resolved, provenance, complaints, columns, dressed,
                              groundTop);
    }

    /// <summary>The highest block the map built that a player meets — what the ceiling clears
    /// (<see cref="BuildCeiling"/>). The terrain answers for itself: <paramref name="groundTop"/> is already
    /// every column's top with the made things taken out. A building answers by its own column, which is why
    /// the provenance is read rather than the world — a stamp is exactly the pass that claimed a column, so
    /// the buildings are the <see cref="ProvenancePass.Structure"/> claims less the ones
    /// <see cref="BuildCeiling.Floating"/> names.
    ///
    /// <para>A column is read top-down and a course inside a made thing is stepped over, because the two can
    /// share one: a house standing under a balloon is claimed Structure and carries the envelope's blocks
    /// over its own roof. Stepping over them finds the ridge, which is the building's answer and the one
    /// wanted.</para></summary>
    private static int HighestBuilt(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> groundTop, WorldProvenance provenance,
        IReadOnlyList<ColumnSegment> columns, IReadOnlySet<string> madeLayers)
    {
        var highest = groundTop.Count > 0 ? groundTop.Values.Max() : 0;

        var made = new Dictionary<(int X, int Z), List<(int Floor, int Top)>>();
        foreach (var segment in columns)
        {
            if (!madeLayers.Contains(segment.Layer)) continue;
            if (!made.TryGetValue(segment.Cell, out var spans)) made[segment.Cell] = spans = [];
            spans.Add((segment.YFloor, segment.YTop));
        }

        foreach (var (cell, pass, owner) in provenance.Claims)
        {
            if (pass != ProvenancePass.Structure) continue;
            if (owner is { } stamp && BuildCeiling.Floating.Contains(stamp.Kind)) continue;
            made.TryGetValue(cell, out var spans);
            for (var y = VoxelWorld.MaxHeight - 1; y > highest; y--)
            {
                if (world.GetBlock(cell.X, y, cell.Z).Id == 0) continue;
                if (spans is not null && spans.Any(span => y >= span.Floor && y < span.Top)) continue;
                highest = y;
                break;
            }
        }
        return highest;
    }

    // Stamp the plan-compiled layout structures (already resolved + fanned to block coords) onto the world.
    private static void StampStructures(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surface, StructureIntent? s)
    {
        if (s is null) return;
        foreach (var w in s.Walls)
        {
            StructureStamper.StampWall(world, w.MinX, w.MinZ, w.MaxX, w.MaxZ, w.TopY);
            DefenseChest.Stamp(world, surface, w.MinX, w.MinZ, w.MaxX, w.MaxZ, w.ChestOnMinFace);
        }
        foreach (var ic in s.IronCubes)
            StructureStamper.StampIronCubeAt(world, surface, ic.MinX, ic.MinZ, RoomFrames.IronSpan);
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
            // The columns the stamp filled, from the stamper's own walk. Its footprint is max-EXCLUSIVE and
            // ClaimRect's is max-inclusive, so a rect carried across by hand records a bedrock line a column
            // thicker on each axis than the one a player meets — 26x3 in the sidecar for a 25x2 wall — and a
            // wall's thickness is exactly what decides whether it can be built over.
            var w = s.Walls[i];
            provenance.Claim(
                StructureStamper.WallCells(w.MinX, w.MinZ, w.MaxX, w.MaxZ),
                ProvenancePass.Structure, w.Stamp);
        }
        for (var i = 0; i < s.IronCubes.Count; i++)
        {
            var cube = s.IronCubes[i];
            provenance.ClaimRect(cube.MinX, cube.MinZ, cube.MinX + RoomFrames.IronSpan - 1,
                cube.MinZ + RoomFrames.IronSpan - 1, ProvenancePass.Structure, cube.Stamp);
        }
        for (var i = 0; i < s.RedstoneLines.Count; i++)
        {
            // The run itself, from the stamper's own walk, rather than the bounding rect of its two ends. The
            // two are the same set only while the run is axis-aligned — which is what EntranceRow produces and
            // nothing states as a rule, so a diagonal run would have claimed its whole box.
            var line = s.RedstoneLines[i];
            provenance.Claim(
                StructureStamper.RedstoneLineCells(line.X1, line.Z1, line.X2, line.Z2),
                ProvenancePass.Structure, s.RedstoneLines[i].Stamp);
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
        VoxelWorld world, BuiltTerrain terrain, List<DestroyableIntent>? destroyables,
        IReadOnlyList<TeamDef> teams, List<(int X, int Z, int Data, GoalMarkerShape Shape)> markers,
        List<(string Kind, string Name, StampId Owner, BlockBox Box)> ceiling, WorldProvenance provenance,
        List<Finding> complaints)
    {
        if (destroyables is null) return null;
        var resolved = new List<DestroyableIntent>(destroyables.Count);
        for (var i = 0; i < destroyables.Count; i++)
        {
            var b = destroyables[i];
            if (!DestroyableStyles.TryParse(b.Style, out var style)) continue;
            var owner = b.Stamp;
            // The same anchor→cell rule the compiler fanned this anchor with, so the stamp lands on the
            // block the plan validator measured and on the mirror of its own orbit image.
            var (ax, az) = ObjectiveFootprint.AnchorCell(b.Anchor.X, b.Anchor.Z);
            var box = ObjectiveStamper.DestroyableBox(terrain.SurfaceFor(b.Layer), ax, az, style, b.Float);

            // What the goal is actually built in. A material its size is wrong for, or one this studio does
            // not build at all, is corrected rather than refused — and the correction rides into the resolved
            // intent, so the map.xml declares what was laid instead of a name matching nothing in its own
            // region (OB3).
            var (materials, correction) = DestroyableMaterials.Resolve(style, b.Materials);
            if (correction is { } why)
                complaints.Add(new Finding(ObjectiveRules.StyleMaterial,
                    $"destroyable '{GoalName(b.Name, b.Owner)}': {why}",
                    Severity.Complaint, Subjects: [owner.Unit]));

            ObjectiveStamper.StampDestroyable(world, box, style, DestroyableMaterials.BlockId(materials));
            provenance.ClaimRect(box.MinX, box.MinZ, box.MaxX, box.MaxZ, ProvenancePass.Structure, owner);
            ceiling.Add(("destroyable", GoalName(b.Name, b.Owner), owner, box));

            // A buried bedrock plate under the goal, so the monument cannot be undermined from below and the
            // ground under it cannot be mined away, and the defence chest set into the ground beside it.
            var (platformMinX, platformMinZ, platformMaxX, platformMaxZ) =
                ObjectiveFootprint.Centred(ax, az, StructureStamper.PlatformSize, StructureStamper.PlatformSize);
            StructureStamper.StampPlatform(world, terrain.SurfaceFor(b.Layer), platformMinX, platformMinZ, platformMaxX, platformMaxZ);
            StructureStamper.StampDefenseChest(world, terrain.SurfaceFor(b.Layer), platformMinX, platformMinZ, platformMaxX, platformMaxZ);
            provenance.ClaimRect(platformMinX, platformMinZ, platformMaxX, platformMaxZ, ProvenancePass.Structure, owner);

            // One marker per destroyable — already one orbit image per entry (PlanCompiler fans team-outer).
            markers.Add((ax, az, WoolDataForTeam(b.Owner, teams), GoalMarkerShape.Cross));
            provenance.Claim(ax, az, ProvenancePass.Structure, owner);

            resolved.Add(new DestroyableIntent
            {
                Owner = b.Owner, Name = b.Name, Style = b.Style, Materials = materials,
                Anchor = b.Anchor, Float = b.Float, Box = box,
            });
        }
        return resolved;
    }

    // Stamp each core's casing + lava and return the intent with every box resolved — the destroyable's
    // shape, and the same one-box rule (OB8). Obsidian is not a knob (DC1): PGM defaults to it and the
    // corpus is effectively unanimous.
    private static List<CoreIntent>? StampCores(
        VoxelWorld world, BuiltTerrain terrain, List<CoreIntent>? cores,
        IReadOnlyList<TeamDef> teams, List<(int X, int Z, int Data, GoalMarkerShape Shape)> markers,
        List<(string Kind, string Name, StampId Owner, BlockBox Box)> ceiling, WorldProvenance provenance,
        List<Finding> complaints)
    {
        if (cores is null) return null;
        var resolved = new List<CoreIntent>(cores.Count);
        for (var i = 0; i < cores.Count; i++)
        {
            var c = cores[i];
            var owner = c.Stamp;
            var (ax, az) = ObjectiveFootprint.AnchorCell(c.Anchor.X, c.Anchor.Z);
            var box = ObjectiveStamper.CoreBox(terrain.SurfaceFor(c.Layer), ax, az, c.Size, c.Height, c.Float);
            ObjectiveStamper.StampCore(world, box, Blocks.Obsidian, c.Shell, c.OpenTop);
            provenance.ClaimRect(box.MinX, box.MinZ, box.MaxX, box.MaxZ, ProvenancePass.Structure, owner);
            ceiling.Add(("core", GoalName(c.Name, c.Owner), owner, box));

            // The defence chest a destroyable stands over, and no plate: a core is won by digging under it
            // until the lava leaks, so bedrock at a fixed depth is a floor across the objective's own rules
            // (the author's ruling). The dig is bounded by the terrain the board actually has.
            var (plateMinX, plateMinZ, plateMaxX, plateMaxZ) =
                ObjectiveFootprint.Centred(ax, az, StructureStamper.PlatformSize, StructureStamper.PlatformSize);
            StructureStamper.StampDefenseChest(world, terrain.SurfaceFor(c.Layer), plateMinX, plateMinZ, plateMaxX, plateMaxZ);
            provenance.ClaimRect(plateMinX, plateMinZ, plateMaxX, plateMaxZ, ProvenancePass.Structure, owner);

            // One marker per core — same already-fanned-per-orbit-image reasoning as the destroyable's.
            markers.Add((ax, az, WoolDataForTeam(c.Owner, teams), GoalMarkerShape.Cross));
            provenance.Claim(ax, az, ProvenancePass.Structure, owner);

            resolved.Add(new CoreIntent
            {
                Owner = c.Owner, Name = c.Name, Anchor = c.Anchor,
                Lava = c.Lava, LavaHeight = c.LavaHeight, OpenTop = c.OpenTop,
                Float = c.Float, Leak = c.Leak, Box = box,
            });
        }
        return resolved;
    }

    /// <summary>A goal standing over the height players may build to (OB23). The blocks above the line can
    /// still be broken, so nothing is unwinnable; what is wrong is that a goal is contested from ground
    /// nobody may build up to reach.</summary>
    private static void OverCeiling(
        List<Finding> complaints, string kind, string name, StampId owner, BlockBox box, int maxBuildHeight)
    {
        if (box.MaxY <= maxBuildHeight) return;
        complaints.Add(new Finding(ObjectiveRules.OverBuildCeiling,
            $"{kind} '{name}' tops out at y{box.MaxY}, over the map's build ceiling of y{maxBuildHeight}",
            Severity.Complaint, Subjects: [owner.Unit]));
    }

    /// <summary>What a goal is called in a complaint: its own name where it has one, else its owning team's,
    /// so the sentence points at something the author can find rather than at a list index.</summary>
    private static string GoalName(string? name, string? owner)
        => !string.IsNullOrWhiteSpace(name) ? name : owner ?? "?";

    /// <summary>The XZ footprints (min/max inclusive) of the renewable iron cubes — the regions the map.xml
    /// renewables wiring covers so the mined ore regrows (ST2): every placeable spawn-side cube (WX8) plus
    /// any legacy <see cref="IronCube.Renew"/> directives an older stored intent still carries. Empty when
    /// there are none.</summary>
    public static IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> RenewableCubeFootprints(MapIntent intent)
    {
        var footprints = new List<(int MinX, int MinZ, int MaxX, int MaxZ)>();
        foreach (var s in intent.Spawns)
            foreach (var iron in SpawnRoom(s, shellBound: true).Iron)
                if (iron.Placeable)
                    footprints.Add((iron.MinX, iron.MinZ, iron.MinX + iron.Size - 1, iron.MinZ + iron.Size - 1));
        if (intent.Structures is { } structures)
            footprints.AddRange(structures.IronCubes.Where(c => c.Renew).Select(c =>
                (c.MinX, c.MinZ, c.MinX + RoomFrames.IronSpan - 1, c.MinZ + RoomFrames.IronSpan - 1)));
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

    /// <summary>The frame the export stamps for a wool: resolved on the region it owns, with its entry
    /// interfaces cutting the doors (WX1/WX6). Shared with the structure preview so the drawn box and the
    /// stamped shell cannot disagree. A wool with no region at all — a partial intent — falls back to the
    /// marker-anchored default.</summary>
    public static RoomFrame WoolFrame(WoolIntent w, bool shellBound)
    {
        if (Ground(w.Protection) is { } ground)
        {
            var (markerX, markerZ) = PositionSnap.SnapHalfXZ(w.Spawn.X, w.Spawn.Z);
            var frame = RoomFrames.Resolve(
                ground, StatedFootprint(w.Footprint), shellBound, markerX, markerZ,
                [.. w.Entries.Select(e => (e.MinX, e.MinZ, e.MaxX, e.MaxZ))], [], out _);
            if (frame is not null) return frame;
        }
        return DefaultFrame(w.Spawn.X, w.Spawn.Z, [], shellBound);
    }

    /// <inheritdoc cref="WoolFrame"/>
    /// <remarks>A spawn resolves its room together with the region's iron markers: each cube stands clear of
    /// the shell in the ring around it, and an unfittable marker comes back unplaceable (WX8/WX9) — nothing
    /// stamps for it.</remarks>
    public static ResolvedRoom SpawnRoom(SpawnIntent s, bool shellBound)
    {
        var doorEdges = SpawnDoors(s);
        if (Ground(s.Protection) is { } ground)
        {
            var (markerX, markerZ) = PositionSnap.SnapHalfXZ(s.Point.X, s.Point.Z);
            var room = RoomFrames.ResolveRoom(
                ground, StatedFootprint(s.Footprint), shellBound, markerX, markerZ, [], doorEdges,
                [.. s.Iron.Select(iron => PositionSnap.SnapHalfXZ(iron.X, iron.Z))], out _);
            if (room is not null) return room;
        }
        return new ResolvedRoom(DefaultFrame(s.Point.X, s.Point.Z, doorEdges, shellBound), []);
    }

    /// <summary>The walls a spawn hall opens through: the ones the intent names, in the order it names them.
    /// A hand-authored intent names none and gets the wall its yaw leans into — a yaw is one angle and a
    /// board is not obliged to have put a door there, so it answers one door rather than a door per
    /// wall.</summary>
    private static IReadOnlyList<RoomEdge> SpawnDoors(SpawnIntent s)
    {
        var named = s.Doors.Select(RoomEdges.OfWord).OfType<RoomEdge>().ToList();
        if (named.Count > 0) return named;
        var yaw = ((s.Yaw % 360) + 360) % 360 * Math.PI / 180;
        return RoomEdges.Nearest(
            ((int)Math.Round(-Math.Sin(yaw)), (int)Math.Round(Math.Cos(yaw))), RoomEdges.All)
            is { } edge ? [edge] : [];
    }

    /// <summary>The ground a room stands on: what the region encloses, as one block rect, or null where the
    /// intent states no region. A plan-compiled room owns exactly its piece and this is that rectangle; an
    /// author who drew a complex zone gets the rectangle around it, since a room is framed on one.</summary>
    private static BlockRect? Ground(IReadOnlyList<Rect> region) => region.Count == 0 ? null
        : new BlockRect(
            (int)region.Min(r => r.MinX), (int)region.Min(r => r.MinZ),
            (int)region.Max(r => r.MaxX), (int)region.Max(r => r.MaxZ));

    /// <summary>An intent rect as the block rect the resolver takes, or null where none was stated.</summary>
    private static BlockRect? StatedFootprint(Rect? rect) => rect is { } r
        ? new BlockRect((int)r.MinX, (int)r.MinZ, (int)r.MaxX, (int)r.MaxZ)
        : null;

    // The legacy default: the room a 10×10 piece centred on the integer-snapped marker resolves to — the
    // original 8×8 shell, with a door per wall for a wool cage or the single yaw door for a spawn. Also the
    // fallback when an authored piece refuses to frame (the validator gates plan exports, so reaching that
    // fallback means a hand-edited intent — stamping the default beats failing the export).
    private static RoomFrame DefaultFrame(
        double x, double z, IReadOnlyList<RoomEdge> spawnDoorEdges, bool shellBound)
    {
        var (anchorX, anchorZ) = PositionSnap.SnapXZ(x, z);
        int minX = anchorX - 5, minZ = anchorZ - 5, maxX = anchorX + 5, maxZ = anchorZ + 5;
        List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = spawnDoorEdges.Count == 0
            ?
            [
                (minX, minZ, maxX, minZ), (minX, maxZ, maxX, maxZ),
                (minX, minZ, minX, maxZ), (maxX, minZ, maxX, maxZ),
            ]
            : [];
        // The footprint is stated rather than defaulted: this synthetic rect is the room a piece-less marker
        // has always resolved to, so the shell is its own one-block inset and no door gap is taken out of it.
        return RoomFrames.Resolve(new BlockRect(minX, minZ, maxX, maxZ),
            new BlockRect(minX + 1, minZ + 1, maxX - 1, maxZ - 1), shellBound,
            anchorX, anchorZ, entries, spawnDoorEdges, out _)!;
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
