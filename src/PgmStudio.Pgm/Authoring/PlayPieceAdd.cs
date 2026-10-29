using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Add one play piece to an intent, and every image the map's symmetry draws of it, or delete one and its
/// images. What the sketch's Layout step places arrives here.
///
/// <para><b>A piece is added with its whole orbit.</b> The authored image belongs to the named team, or the first
/// in orbit order — for a spawn the first with none, since a team enters at one — and the symmetry expander turns it onto every other team, so an added spawn has the same
/// images a compiled one has. Every image is stored and carries one stamp, which is what makes the orbit move,
/// turn and delete together. A build area or a water lane carries no stamp, and its images are the areas the
/// symmetry carries it onto.</para>
///
/// <para><b>A room or an area is the rectangle drawn; a goal, a control point, a cube, a chest and the observer are
/// a point, and a wall is a line through its corners (<see cref="Wall"/>).</b> A control point is a square pad of the
/// default size centred on the block clicked, nobody's, and its images are the pads the symmetry carries it onto
/// that do not overlap it. A chest fronts away from a wall beside it. A spawn
/// faces the middle of the board and opens its door on that side; a team's first wool takes its colour and
/// every later one the next dye no wool on the board has; a goal takes the default structure, centred on the
/// block the point is in. The symmetry is the intent's, or where it states none the board's, and a map stating no
/// teams takes the ones its symmetry gives, so the owner a piece names is a team the map has.</para>
/// </summary>
public static class PlayPieceAdd
{
    /// <summary>The intent with a <paramref name="kind"/> piece added at <paramref name="placed"/> — a room's or
    /// an area's rectangle, or the block a point-placed piece stands in — and the id of its authored image, or
    /// the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, string Id, Findings Refused) Apply(
        MapIntent intent, string kind, Rect placed, string? team = null, SymmetryIntent? board = null)
    {
        var symmetry = intent.Symmetry ?? board;
        var order = Symmetry.Order(symmetry?.Mode);
        var teams = SymmetryExpander.TeamsOf(intent, order);
        var owner = team ?? teams.FirstOrDefault()?.Id ?? "";
        if (team is not null && teams.All(listed => listed.Id != team))
            return Refuse(RequestRules.Unreadable, "team", $"the map has no team '{team}'");
        if (kind == PlayPieceKinds.Wall)
            return Refuse(RequestRules.Unreadable, "kind", "a wall is placed by its corners, not a rectangle");
        if (placed.MaxX <= placed.MinX || placed.MaxZ <= placed.MinZ)
            return Refuse(RequestRules.Unreadable, "placed", "the placed rectangle has no area");

        var (cx, cz) = ((placed.MinX + placed.MaxX) / 2, (placed.MinZ + placed.MaxZ) / 2);
        // A room's marker shares its parity across both axes, which is the only marker a pad can be squared on.
        var (markerX, markerZ) = SpawnPad.SameParity(cx, cz);
        MapIntent added;
        StampId stamp;
        switch (kind)
        {
            case PlayPieceKinds.Spawn:
            {
                // A team enters at one spawn, and the map's regions are named by it, so a spawn goes to the first
                // team in orbit order that has none.
                var spawned = new HashSet<string>(intent.Spawns.Select(spawn => spawn.Team));
                if (team is not null && spawned.Contains(team))
                    return Refuse(RequestRules.Unreadable, "team", $"the team '{team}' has a spawn already");
                owner = team ?? teams.FirstOrDefault(listed => !spawned.Contains(listed.Id))?.Id ?? "";
                if (owner.Length == 0)
                    return Refuse(RequestRules.Unreadable, "kind", "every team has a spawn already, and moving one is what places it");
                stamp = new StampId("spawn", Unit(intent.Spawns.Select(spawn => spawn.Stamp), "spawn"), 0);
                var door = Facing(cx, cz, symmetry);
                var authored = new SpawnIntent
                {
                    Team = owner, Stamp = stamp, Point = new Pt(markerX, 0, markerZ), Protection = [placed],
                    Yaw = YawOf(door), Doors = [StatedDoor.Centred(RoomEdges.Nearest(door, RoomEdges.All)!.Value)],
                };
                added = intent with { Spawns = [.. intent.Spawns, .. Orbit(new MapIntent { Spawns = [authored] }).Spawns] };
                break;
            }
            case PlayPieceKinds.WoolRoom:
            {
                stamp = new StampId("wool", Unit((intent.Wools ?? []).Select(wool => wool.Stamp), "wool"), 0);
                var authored = new WoolIntent { Owner = owner, Stamp = stamp, Spawn = new Pt(markerX, 0, markerZ), Protection = [placed] };
                var images = Orbit(new MapIntent { Wools = [authored] }).Wools ?? [];
                added = intent with { Wools = [.. intent.Wools ?? [], .. Coloured(intent.Wools ?? [], images, teams)] };
                break;
            }
            case PlayPieceKinds.Destroyable:
            {
                stamp = new StampId("destroyable", Unit((intent.Destroyables ?? []).Select(goal => goal.Stamp), "destroyable"), 0);
                var named = teams.FirstOrDefault(listed => listed.Id == owner)?.Name ?? owner;
                var authored = new DestroyableIntent
                {
                    Owner = owner, Stamp = stamp, Anchor = Block(placed),
                    Name = PieceDefaults.MonumentName(named, (intent.Destroyables ?? []).Count(goal => goal.Owner == owner)),
                    Style = DestroyableStyles.Slug(ObjectiveDefaults.Style), Materials = ObjectiveDefaults.Materials,
                    Float = ObjectiveDefaults.DestroyableFloat,
                };
                var images = Orbit(new MapIntent { Destroyables = [authored] }).Destroyables ?? [];
                added = intent with
                {
                    Destroyables = [.. intent.Destroyables ?? [], .. images.Select(image => image.Owner == owner ? image : image with
                    {
                        Name = PieceDefaults.MonumentName(teams.FirstOrDefault(listed => listed.Id == image.Owner)?.Name ?? image.Owner,
                            (intent.Destroyables ?? []).Count(goal => goal.Owner == image.Owner)),
                    })],
                };
                break;
            }
            case PlayPieceKinds.Core:
            {
                stamp = new StampId("core", Unit((intent.Cores ?? []).Select(core => core.Stamp), "core"), 0);
                var authored = new CoreIntent
                {
                    Owner = owner, Stamp = stamp, Anchor = Block(placed), Lava = ObjectiveDefaults.CoreLava,
                    LavaHeight = ObjectiveDefaults.CoreLavaHeight, Float = ObjectiveDefaults.CoreFloat,
                    Leak = ObjectiveDefaults.CoreLeak,
                };
                added = intent with { Cores = [.. intent.Cores ?? [], .. Orbit(new MapIntent { Cores = [authored] }).Cores ?? []] };
                break;
            }
            case PlayPieceKinds.ControlPoint:
            {
                var points = intent.ControlPoints ?? [];
                stamp = new StampId("controlpoint", Unit(points.Select(point => point.Stamp), "controlpoint"), 0);
                var authored = new ControlPointIntent
                {
                    Stamp = stamp, Anchor = new Pt(Math.Floor(cx), 0, Math.Floor(cz)),
                };
                var images = symmetry is null ? [authored] : SymmetryExpander.FillControlPoints([authored], symmetry)!;
                added = intent with { ControlPoints = [.. points, .. images] };
                return Named(added, named => named.ControlPoints!.First(point => point.Stamp == stamp).Id);
            }
            case PlayPieceKinds.BuildArea:
            {
                var build = intent.Build ?? new BuildIntent();
                var at = build.Areas.Count;
                added = intent with { Build = build with { Areas = [.. build.Areas, .. Areas(placed, symmetry)] } };
                return Named(added, named => named.Build!.Areas[at].Id);
            }
            case PlayPieceKinds.WaterLane:
            {
                var lanes = intent.WaterLanes ?? new WaterLaneIntent();
                var at = lanes.Rects.Count;
                added = intent with { WaterLanes = lanes with { Rects = [.. lanes.Rects, .. Areas(placed, symmetry)] } };
                return Named(added, named => named.WaterLanes!.Rects[at].Id);
            }
            case PlayPieceKinds.IronCube:
            {
                var structures = intent.Structures ?? new StructureIntent();
                stamp = new StampId("ironcube", Unit(structures.IronCubes.Select(cube => cube.Stamp), "ironcube"), 0);
                var corner = (X: RoomFrames.IronCorner(cx), Z: RoomFrames.IronCorner(cz));
                var cubes = Images(PlayPieces.Iron(corner.X, corner.Z), symmetry)
                    .Select((image, k) => new IronCube((int)image.MinX, (int)image.MinZ, true, stamp.At(k)));
                added = intent with { Structures = structures with { IronCubes = [.. structures.IronCubes, .. cubes] } };
                break;
            }
            case PlayPieceKinds.Chest:
            {
                var structures = intent.Structures ?? new StructureIntent();
                stamp = new StampId("chest", Unit(structures.Chests.Select(chest => chest.Stamp), "chest"), 0);
                var (x, z) = ((int)Math.Floor(cx), (int)Math.Floor(cz));
                var (fx, fz) = AwayFromWall(structures.Walls, x, z);
                var chests = Images(new Rect(x, z, x + 1, z + 1), symmetry).Select((image, k) =>
                {
                    var (ix, iz) = k == 0 || symmetry is null ? (fx, fz) : Symmetry.Point(fx, fz, symmetry.Mode, 0, 0, k);
                    var facing = RoomEdges.Nearest(((int)Math.Round(ix), (int)Math.Round(iz)), RoomEdges.All)!.Value.Word();
                    return new ChestMarker((int)image.MinX, (int)image.MinZ, facing, stamp.At(k));
                });
                added = intent with { Structures = structures with { Chests = [.. structures.Chests, .. chests] } };
                break;
            }
            case PlayPieceKinds.Observer:
                if (intent.Observer is not null)
                    return Refuse(RequestRules.Unreadable, "kind", "the map has an observer already, and moving it is what places it");
                added = intent with { Observer = new ObserverIntent { Point = new Pt(cx, 0, cz) } };
                return (PlayIds.Assign(added), PlayIds.Observer, Findings.None);
            default:
                return Refuse(RequestRules.Unreadable, "kind", $"'{kind}' is not a play piece the sketch places");
        }

        // A piece is owned by a team the map states, so a map stating none takes the teams the piece was given.
        if (intent.Teams is not { Count: > 0 }) added = added with { Teams = teams };
        return Named(added, named => Stamped(named, kind, stamp));

        MapIntent Orbit(MapIntent unit) => symmetry is null
            ? unit
            : SymmetryExpander.Expand(unit with { Symmetry = symmetry, Teams = teams });
    }

    /// <summary>The intent with the piece <paramref name="id"/> taken out, and unless <paramref name="alone"/>
    /// every image of its unit with it, or the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Delete(
        MapIntent intent, string id, bool alone = false, SymmetryIntent? board = null)
    {
        var symmetry = intent.Symmetry ?? board;
        List<T> Without<T>(List<T> pieces, Func<T, string> idOf, Func<T, StampId> stampOf)
        {
            var named = pieces.First(piece => idOf(piece) == id);
            var unit = stampOf(named);
            return [.. pieces.Where(piece => idOf(piece) != id
                && (alone || string.IsNullOrEmpty(unit.Kind) || stampOf(piece).Identity != unit.Identity))];
        }
        List<PieceArea> WithoutArea(List<PieceArea> areas)
        {
            var named = areas.First(area => area.Id == id);
            var images = alone ? [] : Images(named.Rect, symmetry).Skip(1).ToList();
            return [.. areas.Where(area => area.Id != id && !images.Contains(area.Rect))];
        }

        if (intent.Spawns.Any(spawn => spawn.Id == id))
            return (intent with { Spawns = Without(intent.Spawns, spawn => spawn.Id, spawn => spawn.Stamp) }, Findings.None);
        if (intent.Wools?.Any(wool => wool.Id == id) is true)
            return (intent with { Wools = Without(intent.Wools, wool => wool.Id, wool => wool.Stamp) }, Findings.None);
        if (intent.Destroyables?.Any(goal => goal.Id == id) is true)
            return (intent with { Destroyables = Without(intent.Destroyables, goal => goal.Id, goal => goal.Stamp) }, Findings.None);
        if (intent.Cores?.Any(core => core.Id == id) is true)
            return (intent with { Cores = Without(intent.Cores, core => core.Id, core => core.Stamp) }, Findings.None);
        if (intent.ControlPoints?.Any(point => point.Id == id) is true)
            return (intent with { ControlPoints = Without(intent.ControlPoints, point => point.Id, point => point.Stamp) },
                    Findings.None);
        if (intent.Build is { } build && build.Areas.Any(area => area.Id == id))
            return (intent with { Build = build with { Areas = WithoutArea(build.Areas) } }, Findings.None);
        if (intent.WaterLanes is { } lanes && lanes.Rects.Any(lane => lane.Id == id))
            return (intent with { WaterLanes = lanes with { Rects = WithoutArea(lanes.Rects) } }, Findings.None);
        if (intent.Structures is { } structures)
        {
            if (structures.IronCubes.Any(cube => cube.Id == id))
                return (intent with { Structures = structures with
                    { IronCubes = Without(structures.IronCubes, cube => cube.Id, cube => cube.Stamp) } }, Findings.None);
            if (structures.Walls.Any(wall => wall.Id == id))
                return (intent with { Structures = structures with
                    { Walls = Without(structures.Walls, wall => wall.Id, wall => wall.Stamp) } }, Findings.None);
            if (structures.Chests.Any(chest => chest.Id == id))
                return (intent with { Structures = structures with
                    { Chests = Without(structures.Chests, chest => chest.Id, chest => chest.Stamp) } }, Findings.None);
        }
        if (id == PlayIds.Observer && intent.Observer is not null)
            return (intent with { Observer = null }, Findings.None);
        if (PlayIds.RoomOfMarker(id) is { } room
            && (intent.Spawns.Any(spawn => spawn.Id == room) || intent.Wools?.Any(wool => wool.Id == room) is true))
            return (null, Findings.Of(new Finding(RequestRules.Unreadable,
                $"'{id}' is a room's marker, which goes with its room", Field: "id", Subjects: [id])));
        return (null, Findings.Of(new Finding(RequestRules.NoSuchSubject,
            $"the map has no play piece with the id '{id}'", Field: "id", Subjects: [id])));
    }

    /// <summary>The intent with an approach wall added through <paramref name="points"/>, block corners in order,
    /// and every image the symmetry draws of it, and the id of the authored image; or the findings that stopped it and
    /// a null intent.</summary>
    public static (MapIntent? Intent, string Id, Findings Refused) Wall(
        MapIntent intent, int[][] points, SymmetryIntent? board = null)
    {
        if (points.Length < 2 || points.All(point => point[0] == points[0][0] && point[1] == points[0][1]))
            return Refuse(RequestRules.Unreadable, "points", "the wall's corners are all one point");
        var symmetry = intent.Symmetry ?? board;
        var structures = intent.Structures ?? new StructureIntent();
        var stamp = new StampId("wall", Unit(structures.Walls.Select(wall => wall.Stamp), "wall"), 0);
        // The stamper levels a wall on the highest ground it crosses; the stated top answers only where no ground
        // does, so it is the one the walls already standing state, or a wall's courses over the nominal ground.
        var top = structures.Walls.Count > 0 ? structures.Walls[0].TopY : RoomFrames.WallCourses - 1;
        var walls = new List<WallStructure> { new(points, top, stamp) };
        for (var step = 1; symmetry is not null && step < Symmetry.Order(symmetry.Mode); step++)
        {
            var image = new WallStructure(ImageOf(points, symmetry, step), top, stamp.At(step));
            if (walls.All(standing => !standing.SameLine(image))) walls.Add(image);
        }
        var added = intent with { Structures = structures with { Walls = [.. structures.Walls, .. walls] } };
        return Named(added, named => named.Structures!.Walls.First(wall => wall.Stamp == stamp).Id);
    }

    /// <summary>The corners <paramref name="points"/> carried through the symmetry's step <paramref name="step"/>,
    /// each to the block corner it lands on.</summary>
    internal static int[][] ImageOf(int[][] points, SymmetryIntent symmetry, int step) =>
    [
        .. points.Select(point =>
        {
            var (x, z) = Symmetry.Point(point[0], point[1], symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step);
            return new[] { (int)Math.Round(x), (int)Math.Round(z) };
        }),
    ];

    /// <summary>The way a chest placed at (<paramref name="x"/>, <paramref name="z"/>) fronts: away from the wall
    /// whose columns lie next to it, or south where none does.</summary>
    private static (double X, double Z) AwayFromWall(IEnumerable<WallStructure> walls, int x, int z)
    {
        foreach (var wall in walls)
            foreach (var (wx, wz) in wall.Cells())
                if (Math.Abs(wx - x) + Math.Abs(wz - z) == 1) return (x - wx, z - wz);
        return (0, 1);
    }

    /// <summary>The rectangle and each image the symmetry carries it onto that is not the same rectangle, the
    /// authored one first.</summary>
    private static List<Rect> Images(Rect rect, SymmetryIntent? symmetry)
    {
        var images = new List<Rect> { rect };
        if (symmetry is null) return images;
        for (var step = 1; step < Symmetry.Order(symmetry.Mode); step++)
        {
            var (minX, minZ, maxX, maxZ) = Symmetry.Rect(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ,
                symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step);
            var image = new Rect(Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
            if (!images.Contains(image)) images.Add(image);
        }
        return images;
    }

    private static IEnumerable<PieceArea> Areas(Rect placed, SymmetryIntent? symmetry) =>
        Images(placed, symmetry).Select(image => (PieceArea)image);

    /// <summary>The wools with each image coloured: a team's first wool takes its own colour, every later one the
    /// first dye no wool on the board has.</summary>
    private static IEnumerable<WoolIntent> Coloured(List<WoolIntent> standing, List<WoolIntent> images, List<TeamDef> teams)
    {
        string ColorOf(WoolIntent wool) => wool.Color.Length > 0 ? wool.Color
            : teams.FirstOrDefault(team => team.Id == wool.Owner)?.Color ?? "";
        var taken = new HashSet<string>(standing.Select(ColorOf));
        var owners = new HashSet<string>(standing.Select(wool => wool.Owner));
        foreach (var image in images)
        {
            if (owners.Add(image.Owner)) { taken.Add(ColorOf(image)); yield return image with { Color = "" }; continue; }
            var dye = PieceDefaults.Dyes.FirstOrDefault(candidate => !taken.Contains(candidate)) ?? PieceDefaults.Dyes[0];
            taken.Add(dye);
            yield return image with { Color = dye };
        }
    }

    /// <summary>A unit name no stamp of the kind uses yet.</summary>
    private static string Unit(IEnumerable<StampId> stamps, string kind)
    {
        var used = new HashSet<string>(stamps.Where(stamp => stamp.Kind == kind).Select(stamp => stamp.Unit));
        var next = 1;
        while (used.Contains($"added-{next}")) next++;
        return $"added-{next}";
    }

    /// <summary>The block a point-placed piece stands on: the middle of the placed rectangle's block.</summary>
    private static Pt Block(Rect placed) =>
        new(Math.Floor((placed.MinX + placed.MaxX) / 2) + 0.5, 0, Math.Floor((placed.MinZ + placed.MaxZ) / 2) + 0.5);

    /// <summary>The side of a room at (<paramref name="x"/>, <paramref name="z"/>) that faces the middle of the
    /// board, as a unit step, south where the room stands on the middle itself.</summary>
    private static (int Dx, int Dz) Facing(double x, double z, SymmetryIntent? symmetry)
    {
        double dx = (symmetry?.CenterX ?? 0) - x, dz = (symmetry?.CenterZ ?? 0) - z;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dz) < 1e-9) return (0, 1);
        return Math.Abs(dx) >= Math.Abs(dz) ? (Math.Sign(dx), 0) : (0, Math.Sign(dz));
    }

    /// <summary>The yaw a player arriving faces along <paramref name="facing"/>, the way the compiler reads one.</summary>
    private static double YawOf((int Dx, int Dz) facing)
    {
        var yaw = Math.Round(Math.Atan2(-facing.Dx, facing.Dz) * 180 / Math.PI);
        return ((yaw % 360) + 360) % 360;
    }

    private static string Stamped(MapIntent intent, string kind, StampId stamp) => kind switch
    {
        PlayPieceKinds.Spawn => intent.Spawns.First(spawn => spawn.Stamp == stamp).Id,
        PlayPieceKinds.WoolRoom => intent.Wools!.First(wool => wool.Stamp == stamp).Id,
        PlayPieceKinds.Destroyable => intent.Destroyables!.First(goal => goal.Stamp == stamp).Id,
        PlayPieceKinds.Core => intent.Cores!.First(core => core.Stamp == stamp).Id,
        PlayPieceKinds.IronCube => intent.Structures!.IronCubes.First(cube => cube.Stamp == stamp).Id,
        _ => intent.Structures!.Chests.First(chest => chest.Stamp == stamp).Id,
    };

    private static (MapIntent? Intent, string Id, Findings Refused) Named(MapIntent added, Func<MapIntent, string> idOf)
    {
        var named = PlayIds.Assign(added);
        return (named, idOf(named), Findings.None);
    }

    private static (MapIntent? Intent, string Id, Findings Refused) Refuse(string rule, string field, string message) =>
        (null, "", Findings.Of(new Finding(rule, message, Field: field)));
}
