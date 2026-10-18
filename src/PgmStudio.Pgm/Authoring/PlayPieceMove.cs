using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Move one play piece — a spawn, a wool, a destroyable, a core, a build area, a water lane, an iron cube, an
/// approach wall, a defence chest or the observer — by its id, and every orbit image of it with it. The sketch draws the pieces out of the intent, so a drag on the canvas arrives here or the picture
/// and the world build answer differently.
///
/// <para><b>A room's region, a build area and a water lane also resize.</b> A placed rectangle of another span
/// is the piece's new size: a room keeps everything on its region — the marker, the building, the iron, the
/// doors — at the same fraction across it, and is refused under <see cref="RoomFrames.MinFootprintSpan"/> blocks
/// across (<c>WX2</c>) or over a room region's 20×30 (<c>ST10</c>). Every image takes the image of the new
/// rectangle. A goal's structure is centred on its anchor and a building is reshaped whole by its house, so
/// either refuses a span other than its own.</para>
///
/// <para><b>Moving a room's region carries the room on it</b> — the marker, the building, the iron, the
/// entries — because the region is the ground they stand on. Moving the <b>building</b> moves the building
/// alone, and moving the <b>marker</b> moves the point alone, by whole blocks and inside the building. A goal has one rectangle, its structure's footprint, and moving it moves the anchor.</para>
///
/// <para><b>A room also turns,</b> a quarter at a time about the middle of its region — the region, the marker, the
/// building, the iron, the entries and the doors with it — or its building alone about the building's middle. A
/// defence chest turns the way it fronts. An objective states no facing, so it does not turn.</para>
///
/// <para><b>The orbit moves together.</b> Every piece of the same kind stating the same
/// <see cref="StampId.Identity"/> is an image of one authored unit, and image <c>j</c> of a unit whose image
/// <c>i</c> moved by <c>d</c> moves by <c>d</c> turned through the symmetry's step <c>j − i</c>, so a mirrored
/// board stays mirrored; it turns the same way where the step is a rotation and the other way where it is a
/// reflection. A piece with no stamp is its own unit and moves alone, and <c>alone</c> moves or turns the named
/// image without its others. A build area or a water lane carries no stamp, so its images are the areas standing
/// where the symmetry carries it. The symmetry is the intent's, or where the intent states none the board's.</para>
/// </summary>
public static class PlayPieceMove
{
    /// <summary>The intent with the piece <paramref name="id"/> and its images moved so that its
    /// <paramref name="part"/> — the room's own region word or absent for the piece itself, <c>building</c> for
    /// a room's building — stands at <paramref name="placed"/>, or the findings that stopped it and a null
    /// intent. Pure: the caller stores and projects.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, string? part, Rect placed, bool alone = false, SymmetryIntent? board = null,
        int wing = 0)
    {
        var building = part == StructuralRoles.Building;
        var symmetry = intent.Symmetry ?? board;
        var mode = symmetry?.Mode;
        if (PlayIds.RoomOfMarker(id) is { } room && Marker(intent, room, part, placed, mode, alone) is { } marked)
            return marked;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            if (Unpartable(part, StructuralRoles.Spawn) is { } unpartable) return (null, unpartable);
            var spawn = intent.Spawns[spawnAt];
            var stopped = Stopped(spawn.Protection, WingOf(spawn.Raised(), wing), building, placed);
            if (stopped is not null) return (null, stopped);
            if (!building && SpanOf(spawn.Protection[0]) != SpanOf(placed))
            {
                if (RegionRefused(placed, id) is { } refused) return (null, refused);
                var stretched = Images(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone, (entry, step) =>
                {
                    var stretch = new Stretch(entry.Protection[0], ImageOf(placed, symmetry, step));
                    return entry with
                    {
                        Protection = [stretch.To],
                        Point = stretch.Point(entry.Point),
                        House = entry.House?.Mapped(stretch.Rect),
                        Doors = [.. entry.Doors.Select(stretch.Door)],
                        Iron = [.. entry.Iron.Select(stretch.Point)],
                    };
                });
                return Checked(intent with { Spawns = stretched }, Carried(intent.Spawns, stretched)
                    .Select(entry => (entry.Id, entry.Protection, entry.House, entry.Point)));
            }
            var (dx, dz) = Shift(building ? WingOf(spawn.Raised(), wing)!.Value : spawn.Protection[0], placed);
            var spawns = Orbit(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => building ? entry with { House = Slide(entry.Raised(), x, z), Doors = Slide(entry.Doors, x, z) } : entry with
                {
                    Protection = [.. entry.Protection.Select(r => Slide(r, x, z))],
                    Doors = Slide(entry.Doors, x, z),
                    Point = Slide(entry.Point, x, z),
                    House = Slide(entry.House, x, z),
                    Iron = [.. entry.Iron.Select(pt => Slide(pt, x, z))],
                });
            return Checked(intent with { Spawns = spawns }, Carried(intent.Spawns, spawns)
                .Select(entry => (entry.Id, entry.Protection, entry.House?.Bounds)));
        }

        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if (Unpartable(part, StructuralRoles.WoolRoom) is { } unpartable) return (null, unpartable);
            var wool = wools[woolAt];
            var stopped = Stopped(wool.Protection, WingOf(wool.Raised(), wing), building, placed);
            if (stopped is not null) return (null, stopped);
            if (!building && SpanOf(wool.Protection[0]) != SpanOf(placed))
            {
                if (RegionRefused(placed, id) is { } refused) return (null, refused);
                var stretched = Images(wools, woolAt, entry => entry.Stamp, mode, alone, (entry, step) =>
                {
                    var stretch = new Stretch(entry.Protection[0], ImageOf(placed, symmetry, step));
                    return entry with
                    {
                        Protection = [stretch.To],
                        Spawn = stretch.Point(entry.Spawn),
                        House = entry.House?.Mapped(stretch.Rect),
                        Doors = [.. entry.Doors.Select(stretch.Door)],
                    };
                });
                return Checked(intent with { Wools = stretched }, Carried(wools, stretched)
                    .Select(entry => (entry.Id, entry.Protection, entry.House, entry.Spawn)));
            }
            var (dx, dz) = Shift(building ? WingOf(wool.Raised(), wing)!.Value : wool.Protection[0], placed);
            var moved = Orbit(wools, woolAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => building ? entry with { House = Slide(entry.Raised(), x, z), Doors = Slide(entry.Doors, x, z) } : entry with
                {
                    Protection = [.. entry.Protection.Select(r => Slide(r, x, z))],
                    Spawn = Slide(entry.Spawn, x, z),
                    House = Slide(entry.House, x, z),
                    Doors = Slide(entry.Doors, x, z),
                });
            return Checked(intent with { Wools = moved }, Carried(wools, moved)
                .Select(entry => (entry.Id, entry.Protection, entry.House?.Bounds)));
        }

        var destroyables = intent.Destroyables ?? [];
        if (destroyables.FindIndex(goal => goal.Id == id) is var goalAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (BlockShift(PlayPieces.Destroyable(destroyables[goalAt]), placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var moved = Orbit(destroyables, goalAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Anchor = Slide(entry.Anchor, x, z), Box = null });
            return (intent with { Destroyables = moved }, Findings.None);
        }

        var cores = intent.Cores ?? [];
        if (cores.FindIndex(core => core.Id == id) is var coreAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (BlockShift(PlayPieces.Core(cores[coreAt]), placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var moved = Orbit(cores, coreAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Anchor = Slide(entry.Anchor, x, z), Box = null });
            return (intent with { Cores = moved }, Findings.None);
        }

        if (intent.Build is { } build && build.Areas.FindIndex(area => area.Id == id) is var areaAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (SpanOf(build.Areas[areaAt].Rect) != SpanOf(placed))
                return OffGrid(placed) is { } offGrid ? (null, offGrid)
                    : (intent with { Build = build with { Areas = StretchAreas(build.Areas, areaAt, placed, symmetry, alone) } },
                       Findings.None);
            var (dx, dz) = Shift(build.Areas[areaAt].Rect, placed);
            return (intent with { Build = build with { Areas = MoveAreas(build.Areas, areaAt, dx, dz, symmetry, alone) } },
                    Findings.None);
        }

        if (intent.WaterLanes is { } lanes && lanes.Rects.FindIndex(lane => lane.Id == id) is var laneAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (SpanOf(lanes.Rects[laneAt].Rect) != SpanOf(placed))
                return OffGrid(placed) is { } offGrid ? (null, offGrid)
                    : (intent with { WaterLanes = lanes with { Rects = StretchAreas(lanes.Rects, laneAt, placed, symmetry, alone) } },
                       Findings.None);
            var (dx, dz) = Shift(lanes.Rects[laneAt].Rect, placed);
            return (intent with { WaterLanes = lanes with { Rects = MoveAreas(lanes.Rects, laneAt, dx, dz, symmetry, alone) } },
                    Findings.None);
        }

        if (intent.Structures is { } structures)
        {
            if (structures.IronCubes.FindIndex(cube => cube.Id == id) is var cubeAt and >= 0)
            {
                if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
                var cube = structures.IronCubes[cubeAt];
                if (BlockShift(PlayPieces.Iron(cube.MinX, cube.MinZ), placed, out var dx, out var dz) is { } stopped)
                    return (null, stopped);
                var moved = Orbit(structures.IronCubes, cubeAt, entry => entry.Stamp, mode, alone, dx, dz,
                    (entry, x, z) => entry with { MinX = entry.MinX + (int)x, MinZ = entry.MinZ + (int)z });
                return (intent with { Structures = structures with { IronCubes = moved } }, Findings.None);
            }

            if (structures.Walls.FindIndex(wall => wall.Id == id) is var wallAt and >= 0)
            {
                if (part is PieceParts.Start or PieceParts.End)
                    return WallEnd(intent, structures, wallAt, part == PieceParts.End, placed, symmetry, alone);
                if (!string.IsNullOrEmpty(part))
                    return Refuse(RequestRules.Unreadable, "part",
                        $"the request's `part` is '{part}', and a wall takes '{PieceParts.Start}', '{PieceParts.End}' or none");
                if (BlockShift(PlayPieces.Wall(structures.Walls[wallAt]), placed, out var dx, out var dz) is { } stopped)
                    return (null, stopped);
                var moved = Orbit(structures.Walls, wallAt, entry => entry.Stamp, mode, alone, dx, dz,
                    (entry, x, z) => entry with
                    {
                        X1 = entry.X1 + (int)x, Z1 = entry.Z1 + (int)z, X2 = entry.X2 + (int)x, Z2 = entry.Z2 + (int)z,
                    });
                return (intent with { Structures = structures with { Walls = moved } }, Findings.None);
            }

            if (structures.Chests.FindIndex(chest => chest.Id == id) is var chestAt and >= 0)
            {
                if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
                if (BlockShift(PlayPieces.Chest(structures.Chests[chestAt]), placed, out var dx, out var dz) is { } stopped)
                    return (null, stopped);
                var moved = Orbit(structures.Chests, chestAt, entry => entry.Stamp, mode, alone, dx, dz,
                    (entry, x, z) => entry with { X = entry.X + (int)x, Z = entry.Z + (int)z });
                return (intent with { Structures = structures with { Chests = moved } }, Findings.None);
            }
        }

        if (id == PlayIds.Observer && intent.Observer is { } observer)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (BlockShift(PlayPieces.Observer(observer), placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            return (intent with { Observer = observer with { Point = Slide(observer.Point, dx, dz) } }, Findings.None);
        }

        return Refuse(RequestRules.NoSuchSubject, "id", $"the map has no play piece with the id '{id}'", id);
    }

    /// <summary>The intent with one end of the wall at <paramref name="at"/> — its second where
    /// <paramref name="second"/> — moved to the block corner <paramref name="placed"/> states, the other end left
    /// standing, and the matching end of every image moved by the displacement turned through the step to it.</summary>
    private static (MapIntent? Intent, Findings Refused) WallEnd(
        MapIntent intent, StructureIntent structures, int at, bool second, Rect placed, SymmetryIntent? symmetry,
        bool alone)
    {
        if (placed.MinX != placed.MaxX || placed.MinZ != placed.MaxZ)
            return Refuse(RequestRules.Unreadable, "placed",
                "a wall's end is one block corner, and the request's `placed` spans blocks");
        if (placed.MinX != Math.Floor(placed.MinX) || placed.MinZ != Math.Floor(placed.MinZ))
            return Refuse(RequestRules.Unreadable, "placed", "a wall's end stands on a block corner, and the request's `placed` is off the block grid");
        var wall = structures.Walls[at];
        var (oldX, oldZ) = second ? (wall.X2, wall.Z2) : (wall.X1, wall.Z1);
        var (otherX, otherZ) = second ? (wall.X1, wall.Z1) : (wall.X2, wall.Z2);
        if ((int)placed.MinX == otherX && (int)placed.MinZ == otherZ)
            return Refuse(RequestRules.Unreadable, "placed", "a wall's two ends would stand on one corner", wall.Id);
        double dx = placed.MinX - oldX, dz = placed.MinZ - oldZ;
        var mode = symmetry?.Mode;
        var walls = Images(structures.Walls, at, entry => entry.Stamp, mode, alone, (entry, step) =>
        {
            if (step == 0) return Ended(entry, second, (int)placed.MinX, (int)placed.MinZ);
            var (imageX, imageZ) = Symmetry.Point(oldX, oldZ, mode, symmetry!.CenterX, symmetry.CenterZ, step);
            var nearSecond = Distance(entry.X2, entry.Z2, imageX, imageZ) < Distance(entry.X1, entry.Z1, imageX, imageZ);
            var (x, z) = Symmetry.Point(dx, dz, mode, 0, 0, step);
            var (fromX, fromZ) = nearSecond ? (entry.X2, entry.Z2) : (entry.X1, entry.Z1);
            return Ended(entry, nearSecond, fromX + (int)Math.Round(x), fromZ + (int)Math.Round(z));
        });
        return (intent with { Structures = structures with { Walls = walls } }, Findings.None);
    }

    private static WallStructure Ended(WallStructure wall, bool second, int x, int z) =>
        second ? wall with { X2 = x, Z2 = z } : wall with { X1 = x, Z1 = z };

    private static double Distance(double x1, double z1, double x2, double z2) => Math.Abs(x1 - x2) + Math.Abs(z1 - z2);

    /// <summary>The intent with the marker of the room <paramref name="room"/> and its images moved by whole blocks
    /// to <paramref name="placed"/>, held inside the room's building where it states one and its region where
    /// not; the findings that stopped it; or null where no room has that id.</summary>
    private static (MapIntent? Intent, Findings Refused)? Marker(
        MapIntent intent, string room, string? part, Rect placed, string? mode, bool alone)
    {
        if (intent.Spawns.FindIndex(spawn => spawn.Id == room) is var spawnAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (BlockShift(PlayPieces.Marker(intent.Spawns[spawnAt].Point), placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var spawns = Orbit(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Point = Slide(entry.Point, x, z) });
            return Seated(intent with { Spawns = spawns }, Carried(intent.Spawns, spawns)
                .Select(entry => (entry.Id, entry.Point, entry.Protection, entry.House?.Room)));
        }

        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == room) is var woolAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            if (BlockShift(PlayPieces.Marker(wools[woolAt].Spawn), placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var moved = Orbit(wools, woolAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Spawn = Slide(entry.Spawn, x, z) });
            return Seated(intent with { Wools = moved }, Carried(wools, moved)
                .Select(entry => (entry.Id, entry.Spawn, entry.Protection, entry.House?.Room)));
        }
        return null;
    }

    /// <summary>The moved intent, unless a marker it moved now stands outside its room — on the image the
    /// request named or on one its orbit moved.</summary>
    private static (MapIntent? Intent, Findings Refused) Seated(
        MapIntent intent, IEnumerable<(string Id, Pt Point, List<Rect> Protection, Rect? Footprint)> rooms)
    {
        foreach (var (id, point, protection, footprint) in rooms)
        {
            Rect? bound = footprint ?? (protection.Count == 1 ? protection[0] : null);
            if (bound is { } inside && !Holds(inside, PlayPieces.Marker(point)))
                return (null, Findings.Of(new Finding(RequestRules.Unreadable,
                    $"the marker of '{id}' would stand outside its {(footprint is null ? "region" : "building")}",
                    Field: "placed", Subjects: [PlayIds.Marker(id)])));
        }
        return (intent, Findings.None);
    }

    /// <summary>The intent with the room <paramref name="id"/> — or, with <paramref name="part"/>
    /// <c>building</c>, its building alone — turned <paramref name="quarters"/> quarter turns clockwise seen from
    /// above, and its orbit images with it, or the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Turn(
        MapIntent intent, string id, string? part, int quarters, bool alone = false, SymmetryIntent? board = null)
    {
        var building = part == StructuralRoles.Building;
        var symmetry = intent.Symmetry ?? board;
        var mode = symmetry?.Mode;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            if ((Unpartable(part, StructuralRoles.Spawn) ?? Unturnable(intent.Spawns[spawnAt].Protection,
                    intent.Spawns[spawnAt].Raised()?.Bounds, building)) is { } stopped) return (null, stopped);
            var spawns = Images(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone,
                (entry, step) => TurnSpawn(entry, building, Handed(quarters, mode, step)));
            return Checked(intent with { Spawns = spawns }, Carried(intent.Spawns, spawns)
                .Select(entry => (entry.Id, entry.Protection, entry.House?.Bounds)));
        }

        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if ((Unpartable(part, StructuralRoles.WoolRoom) ?? Unturnable(wools[woolAt].Protection,
                    wools[woolAt].Raised()?.Bounds, building)) is { } stopped) return (null, stopped);
            var turned = Images(wools, woolAt, entry => entry.Stamp, mode, alone,
                (entry, step) => TurnWool(entry, building, Handed(quarters, mode, step)));
            return Checked(intent with { Wools = turned }, Carried(wools, turned)
                .Select(entry => (entry.Id, entry.Protection, entry.House?.Bounds)));
        }

        if (intent.Build is { } build && build.Areas.FindIndex(area => area.Id == id) is var areaAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            return (intent with { Build = build with { Areas = TurnAreas(build.Areas, areaAt, quarters, symmetry, alone) } },
                    Findings.None);
        }

        if (intent.WaterLanes is { } lanes && lanes.Rects.FindIndex(lane => lane.Id == id) is var laneAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            return (intent with { WaterLanes = lanes with { Rects = TurnAreas(lanes.Rects, laneAt, quarters, symmetry, alone) } },
                    Findings.None);
        }

        if ((intent.Destroyables ?? []).Any(goal => goal.Id == id) || (intent.Cores ?? []).Any(core => core.Id == id))
            return Refuse(RequestRules.Unreadable, "turn", $"'{id}' is an objective, which states no facing to turn");
        if (PlayIds.RoomOfMarker(id) is { } room
            && (intent.Spawns.Any(spawn => spawn.Id == room) || (intent.Wools ?? []).Any(wool => wool.Id == room)))
            return Refuse(RequestRules.Unreadable, "turn", $"'{id}' is a room's marker, which turns with its room");
        if (intent.Structures is { } chested
            && chested.Chests.FindIndex(chest => chest.Id == id) is var chestAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            var chests = Images(chested.Chests, chestAt, entry => entry.Stamp, mode, alone, (entry, step) =>
                entry with { Facing = TurnedEdge(entry.Facing, Handed(quarters, mode, step)) });
            return (intent with { Structures = chested with { Chests = chests } }, Findings.None);
        }
        if (intent.Structures is { } structures
            && (structures.IronCubes.Any(cube => cube.Id == id) || structures.Walls.Any(wall => wall.Id == id))
            || id == PlayIds.Observer && intent.Observer is not null)
            return Refuse(RequestRules.Unreadable, "turn", $"'{id}' is not a room or an area, which are what turns");
        return Refuse(RequestRules.NoSuchSubject, "id", $"the map has no play piece with the id '{id}'", id);
    }

    /// <summary>The list with the piece at <paramref name="at"/> moved by (<paramref name="dx"/>,
    /// <paramref name="dz"/>) and each other image of its unit moved by that displacement turned through the
    /// step between the two images. A resolved goal box is dropped by the caller's slide, since the world
    /// build resolves it again over the ground the goal now stands on.</summary>
    private static List<T> Orbit<T>(
        List<T> pieces, int at, Func<T, StampId> stampOf, string? mode, bool alone, double dx, double dz,
        Func<T, double, double, T> slide) =>
        Images(pieces, at, stampOf, mode, alone, (piece, step) =>
        {
            if (step == 0) return slide(piece, dx, dz);
            var (x, z) = Symmetry.Point(dx, dz, mode, 0, 0, step);
            return slide(piece, Math.Round(x), Math.Round(z));
        });

    /// <summary>The list with <paramref name="change"/> applied to the piece at <paramref name="at"/> (step 0)
    /// and, unless <paramref name="alone"/>, to every other image of its unit, given the symmetry step from the
    /// named image to it.</summary>
    internal static List<T> Images<T>(
        List<T> pieces, int at, Func<T, StampId> stampOf, string? mode, bool alone, Func<T, int, T> change)
    {
        var changed = new List<T>(pieces) { [at] = change(pieces[at], 0) };
        var stamp = stampOf(pieces[at]);
        if (alone || string.IsNullOrEmpty(stamp.Kind)) return changed;

        var order = Symmetry.Order(mode);
        for (var index = 0; index < pieces.Count; index++)
        {
            var image = stampOf(pieces[index]);
            if (index == at || image.Identity != stamp.Identity || image.Image == stamp.Image) continue;
            if (image.Image >= order || stamp.Image >= order) continue;
            changed[index] = change(pieces[index], ((image.Image - stamp.Image) % order + order) % order);
        }
        return changed;
    }

    /// <summary>The areas with the one at <paramref name="at"/> moved by (<paramref name="dx"/>,
    /// <paramref name="dz"/>), and unless <paramref name="alone"/> each area standing where the symmetry carries
    /// it moved by that displacement turned through the same step.</summary>
    private static List<PieceArea> MoveAreas(
        List<PieceArea> areas, int at, double dx, double dz, SymmetryIntent? symmetry, bool alone) =>
        AreaImages(areas, at, symmetry, alone, (area, step) =>
        {
            var (x, z) = step == 0 ? (dx, dz) : Symmetry.Point(dx, dz, symmetry!.Mode, 0, 0, step);
            return area with { MinX = area.MinX + x, MinZ = area.MinZ + z, MaxX = area.MaxX + x, MaxZ = area.MaxZ + z };
        });

    /// <summary>The areas with the one at <paramref name="at"/> turned a quarter at a time about its middle, and
    /// its images turned the same way under a rotation and the other way under a reflection.</summary>
    private static List<PieceArea> TurnAreas(
        List<PieceArea> areas, int at, int quarters, SymmetryIntent? symmetry, bool alone) =>
        AreaImages(areas, at, symmetry, alone, (area, step) =>
        {
            var turned = Pivot.Of(area.Rect, Handed(quarters, symmetry?.Mode, step)).Rect(area.Rect);
            return area with { MinX = turned.MinX, MinZ = turned.MinZ, MaxX = turned.MaxX, MaxZ = turned.MaxZ };
        });

    /// <summary>The list with <paramref name="change"/> applied to the area at <paramref name="at"/> (step 0)
    /// and, unless <paramref name="alone"/>, to each area whose bounds are its image under a step of the
    /// symmetry, read before any of them changed.</summary>
    private static List<PieceArea> AreaImages(
        List<PieceArea> areas, int at, SymmetryIntent? symmetry, bool alone, Func<PieceArea, int, PieceArea> change)
    {
        var changed = new List<PieceArea>(areas) { [at] = change(areas[at], 0) };
        if (alone || symmetry is null) return changed;
        var named = areas[at];
        for (var step = 1; step < Symmetry.Order(symmetry.Mode); step++)
        {
            var (minX, minZ, maxX, maxZ) = Symmetry.Rect(named.MinX, named.MinZ, named.MaxX, named.MaxZ,
                symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step);
            var image = areas.FindIndex(area => Near(area.MinX, minX) && Near(area.MinZ, minZ)
                                                && Near(area.MaxX, maxX) && Near(area.MaxZ, maxZ));
            if (image >= 0 && image != at) changed[image] = change(areas[image], step);
        }
        return changed;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

    /// <summary>The intent with the spawn <paramref name="id"/>'s facing — the way its players face on arriving,
    /// and the door that opens that way — turned <paramref name="quarters"/> quarter turns clockwise, its region
    /// left where it stands, and its images turned the same way under a rotation and the other way under a
    /// reflection; or the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Face(
        MapIntent intent, string id, int quarters, bool alone = false, SymmetryIntent? board = null)
    {
        var mode = (intent.Symmetry ?? board)?.Mode;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is not (var spawnAt and >= 0))
            return Refuse(RequestRules.NoSuchSubject, "id", $"the map has no spawn with the id '{id}'", id);
        var spawns = Images(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone, (entry, step) =>
        {
            var pivot = Pivot.Of(entry.Protection.Count > 0 ? entry.Protection[0] : new Rect(0, 0, 1, 1),
                                 Handed(quarters, mode, step));
            return entry with
            {
                Doors = [.. entry.Doors.Select(pivot.Door)],
                Yaw = ((entry.Yaw + 90 * pivot.Quarters) % 360 + 360) % 360,
            };
        });
        return (intent with { Spawns = spawns }, Findings.None);
    }

    /// <summary>An edge word turned <paramref name="quarters"/> quarter turns clockwise seen from above.</summary>
    private static string TurnedEdge(string word, int quarters) =>
        RoomEdges.OfWord(word) is { } edge ? new Pivot(0, 0, ((quarters % 4) + 4) % 4, 0, 0).Edge(edge).Word() : word;

    /// <summary>The quarter turns an image takes for the named one's <paramref name="quarters"/>: the same where
    /// the step between them is a rotation, the other way where it is a reflection.</summary>
    private static int Handed(int quarters, string? mode, int step) =>
        Symmetry.Reflects(mode, step) ? -quarters : quarters;

    private static SpawnIntent TurnSpawn(SpawnIntent spawn, bool building, int quarters)
    {
        if (building && spawn.Raised() is { } spawnHouse) return spawn with { House = Turned(spawnHouse, quarters), Doors = Turned(spawn.Doors, spawnHouse, quarters) };
        var pivot = Pivot.Of(spawn.Protection[0], quarters);
        return spawn with
        {
            Protection = [pivot.Rect(spawn.Protection[0])],
            Point = pivot.Point(spawn.Point),
            House = spawn.House?.Mapped(pivot.Rect),
            Iron = [.. spawn.Iron.Select(pivot.Point)],
            Doors = [.. spawn.Doors.Select(pivot.Door)],
            Yaw = ((spawn.Yaw + 90 * pivot.Quarters) % 360 + 360) % 360,
        };
    }

    private static WoolIntent TurnWool(WoolIntent wool, bool building, int quarters)
    {
        if (building && wool.Raised() is { } woolHouse) return wool with { House = Turned(woolHouse, quarters), Doors = Turned(wool.Doors, woolHouse, quarters) };
        var pivot = Pivot.Of(wool.Protection[0], quarters);
        return wool with
        {
            Protection = [pivot.Rect(wool.Protection[0])],
            Spawn = pivot.Point(wool.Spawn),
            House = wool.House?.Mapped(pivot.Rect),
            Doors = [.. wool.Doors.Select(pivot.Door)],
        };
    }

    /// <summary>What stops a turn: a region of several rectangles, or a building to turn that is not stated.</summary>
    private static Findings? Unturnable(IReadOnlyList<Rect> protection, Rect? footprint, bool building)
    {
        if (protection.Count != 1)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"the room's `protection` has {protection.Count} rectangles, not 1", Field: "protection"));
        if (building && footprint is null)
            return Findings.Of(new Finding(RequestRules.Unreadable, "the room states no `house`", Field: "part"));
        return null;
    }

    /// <summary>A quarter turn clockwise seen from above about the middle of a rectangle, then the half-block
    /// shift that keeps the turned rectangle on the block grid, applied alike to everything the rectangle
    /// carries.</summary>
    private readonly record struct Pivot(double X, double Z, int Quarters, double ShiftX, double ShiftZ)
    {
        public static Pivot Of(Rect around, int quarters)
        {
            var turn = ((quarters % 4) + 4) % 4;
            var pivot = new Pivot((around.MinX + around.MaxX) / 2, (around.MinZ + around.MaxZ) / 2, turn, 0, 0);
            var turned = pivot.Rect(around);
            return pivot with { ShiftX = Math.Floor(turned.MinX) - turned.MinX, ShiftZ = Math.Floor(turned.MinZ) - turned.MinZ };
        }

        private (double X, double Z) Turned(double x, double z)
        {
            double dx = x - X, dz = z - Z;
            for (var k = 0; k < Quarters; k++) (dx, dz) = (-dz, dx);
            return (X + dx + ShiftX, Z + dz + ShiftZ);
        }

        public Rect Rect(Rect r)
        {
            var (ax, az) = Turned(r.MinX, r.MinZ);
            var (bx, bz) = Turned(r.MaxX, r.MaxZ);
            return new Rect(Math.Min(ax, bx), Math.Min(az, bz), Math.Max(ax, bx), Math.Max(az, bz));
        }

        public Pt Point(Pt p)
        {
            var (x, z) = Turned(p.X, p.Z);
            return p with { X = x, Z = z };
        }

        public RoomEdge Edge(RoomEdge edge)
        {
            var (dx, dz) = edge.Outward();
            for (var k = 0; k < Quarters; k++) (dx, dz) = (-dz, dx);
            return RoomEdges.Nearest((dx, dz), RoomEdges.All) ?? edge;
        }

        public StatedDoor Door(StatedDoor door) => door.Mapped(Turned, Edge);
    }

    /// <summary>The entries the move replaced — every other entry is the same object it was.</summary>
    private static IEnumerable<T> Carried<T>(List<T> was, List<T> moved) where T : class =>
        moved.Where((entry, index) => !ReferenceEquals(entry, was[index]));

    /// <summary>What stops a room's region being resized to <paramref name="placed"/>: a rectangle off the block
    /// grid, one too small to hold a room (<c>WX2</c>), or one larger than a room's region (<c>ST10</c>).</summary>
    private static Findings? RegionRefused(Rect placed, string id)
    {
        if (OffGrid(placed) is { } offGrid) return offGrid;
        var (width, depth) = SpanOf(placed);
        if (Math.Min(width, depth) < RoomFrames.MinFootprintSpan)
            return Findings.Of(new Finding(RoomFrameRules.FootprintTooSmall,
                $"the region of '{id}' would be {Span(placed)} blocks, less than {RoomFrames.MinFootprintSpan} blocks "
                + "across its shorter side", Field: "placed", Subjects: [id]));
        if (Math.Min(width, depth) > RoomFrames.RegionCapAcross || Math.Max(width, depth) > RoomFrames.RegionCapAlong)
            return Findings.Of(new Finding(LayoutRules.RoomRegionSize,
                $"the region of '{id}' would be {Span(placed)} blocks, more than {RoomFrames.RegionCapAcross} by "
                + $"{RoomFrames.RegionCapAlong} blocks", Field: "placed", Subjects: [id]));
        return null;
    }

    private static Findings? OffGrid(Rect placed) =>
        placed.MinX != Math.Floor(placed.MinX) || placed.MinZ != Math.Floor(placed.MinZ)
        || placed.MaxX != Math.Floor(placed.MaxX) || placed.MaxZ != Math.Floor(placed.MaxZ)
            ? Findings.Of(new Finding(RequestRules.Unreadable,
                "the placed rectangle is off the block grid, and a piece is resized by whole blocks", Field: "placed"))
            : placed.MaxX <= placed.MinX || placed.MaxZ <= placed.MinZ
                ? Findings.Of(new Finding(RequestRules.Unreadable, "the placed rectangle has no area", Field: "placed"))
                : null;

    /// <summary>The rectangle <paramref name="placed"/> carried <paramref name="step"/> images round the orbit,
    /// on the block grid.</summary>
    private static Rect ImageOf(Rect placed, SymmetryIntent? symmetry, int step)
    {
        if (step == 0 || symmetry is null) return placed;
        var (minX, minZ, maxX, maxZ) = Symmetry.Rect(placed.MinX, placed.MinZ, placed.MaxX, placed.MaxZ,
            symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step);
        return new Rect(Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
    }

    /// <summary>The areas with the one at <paramref name="at"/> made <paramref name="placed"/>, and unless
    /// <paramref name="alone"/> each area standing where the symmetry carries it made the image of it.</summary>
    private static List<PieceArea> StretchAreas(
        List<PieceArea> areas, int at, Rect placed, SymmetryIntent? symmetry, bool alone) =>
        AreaImages(areas, at, symmetry, alone, (area, step) =>
        {
            var to = ImageOf(placed, symmetry, step);
            return area with { MinX = to.MinX, MinZ = to.MinZ, MaxX = to.MaxX, MaxZ = to.MaxZ };
        });

    /// <summary>The map a region resized from <see cref="From"/> to <see cref="To"/> carries what stands on it by:
    /// each coordinate kept at the same fraction across the region, a rectangle's on the block grid and a point's
    /// on the half-block it stood on, so a grid-line marker stays a grid line and a block-centre one a centre.</summary>
    private readonly record struct Stretch(Rect From, Rect To)
    {
        private double X(double x) => To.MinX + (x - From.MinX) * (To.MaxX - To.MinX) / (From.MaxX - From.MinX);
        private double Z(double z) => To.MinZ + (z - From.MinZ) * (To.MaxZ - To.MinZ) / (From.MaxZ - From.MinZ);

        private static double OnLattice(double stretched, double was)
        {
            var half = was - Math.Floor(was);
            return Math.Round(stretched - half, MidpointRounding.AwayFromZero) + half;
        }

        public Rect Rect(Rect r) => new(Math.Round(X(r.MinX)), Math.Round(Z(r.MinZ)), Math.Round(X(r.MaxX)), Math.Round(Z(r.MaxZ)));

        public Pt Point(Pt p)
        {
            var (x, z) = Stretched(p.X, p.Z);
            return p with { X = x, Z = z };
        }

        private (double X, double Z) Stretched(double x, double z) => (OnLattice(X(x), x), OnLattice(Z(z), z));

        public StatedDoor Door(StatedDoor door) => door.Mapped(Stretched, edge => edge);
    }

    /// <summary>The resized intent, unless a room it carried now has a wing too small to hold a room
    /// (<c>WX2</c>) or its marker outside its room's wing — on the image the request named or on one its orbit
    /// resized.</summary>
    private static (MapIntent? Intent, Findings Refused) Checked(
        MapIntent intent, IEnumerable<(string Id, List<Rect> Protection, RoomHouse? House, Pt Marker)> rooms)
    {
        foreach (var (id, _, house, marker) in rooms)
        {
            if (house is null) continue;
            if (house.Rects().Any(wing => Math.Min(wing.MaxX - wing.MinX, wing.MaxZ - wing.MinZ) < RoomFrames.MinFootprintSpan))
                return (null, Findings.Of(new Finding(RoomFrameRules.FootprintTooSmall,
                    $"a wing of the house of '{id}' would be less than {RoomFrames.MinFootprintSpan} blocks across",
                    Field: "placed", Subjects: [id])));
            if (house.Room is { } room && (Math.Floor(marker.X) < room.MinX || Math.Floor(marker.X) >= room.MaxX
                                           || Math.Floor(marker.Z) < room.MinZ || Math.Floor(marker.Z) >= room.MaxZ))
                return (null, Findings.Of(new Finding(RequestRules.Unreadable,
                    $"the marker of '{id}' would stand outside its room's wing", Field: "placed",
                    Subjects: [PlayIds.Marker(id)])));
        }
        return Checked(intent, rooms.Select(room => (room.Id, room.Protection, room.House?.Bounds)));
    }

    /// <summary>What stops a room's move: a part it does not have, a region of several rectangles, a span
    /// that changed, or a building carried off its region.</summary>
    private static Findings? Stopped(IReadOnlyList<Rect> protection, Rect? footprint, bool building, Rect placed)
    {
        if (protection.Count == 0)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                "the room's `protection` has no region", Field: "protection"));
        // A region drawn as a union of several has no single rectangle a drag could mean, and moving the
        // first alone would tear the union apart.
        if (protection.Count > 1)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"the room's `protection` has {protection.Count} rectangles, more than 1", Field: "protection"));

        if (!building) return null;

        if (footprint is not { } standing)
            return Findings.Of(new Finding(RequestRules.Unreadable, "the room states no `house`", Field: "part"));
        if (SpanOf(standing) != SpanOf(placed)) return Resized(standing, placed);
        return null;
    }

    /// <summary>The moved intent, unless a building it carried now stands outside its region — on the image
    /// the request named or on one its orbit moved.</summary>
    private static (MapIntent? Intent, Findings Refused) Checked(
        MapIntent intent, IEnumerable<(string Id, List<Rect> Protection, Rect? Footprint)> rooms)
    {
        foreach (var (id, protection, footprint) in rooms)
            if (footprint is { } house && protection.Count == 1 && !Holds(protection[0], house))
                return (null, Findings.Of(new Finding(RoomFrameRules.FootprintOffPiece,
                    $"the building of '{id}' would reach outside the region of its room", Field: "part",
                    Subjects: [id])));
        return (intent, Findings.None);
    }

    /// <summary>A part the piece does not have: a room takes its own region word or <c>building</c>, and an
    /// objective, whose one rectangle is its structure, takes none. Absent is always the piece itself.</summary>
    private static Findings? Unpartable(string? part, string? region)
    {
        if (string.IsNullOrEmpty(part) || part == region || (region is not null && part == StructuralRoles.Building))
            return null;
        var takes = region is null ? "no `part`" : $"'{region}' or '{StructuralRoles.Building}'";
        return Findings.Of(new Finding(RequestRules.Unreadable,
            $"the request's `part` is '{part}', and the piece takes {takes}", Field: "part"));
    }

    /// <summary>The displacement from <paramref name="standing"/> to <paramref name="placed"/> for a piece
    /// that moves by whole blocks, or what stops it: a span that changed, or a move off the block grid.</summary>
    private static Findings? BlockShift(Rect standing, Rect placed, out double dx, out double dz)
    {
        dx = dz = 0;
        if (SameSpan(standing, placed) is { } resized) return resized;
        (dx, dz) = Shift(standing, placed);
        if (dx != Math.Floor(dx) || dz != Math.Floor(dz))
            return Findings.Of(new Finding(RequestRules.Unreadable,
                "the placed rectangle is off the block grid, and the piece moves by whole blocks", Field: "placed"));
        return null;
    }

    private static Findings? SameSpan(Rect standing, Rect placed) =>
        SpanOf(standing) != SpanOf(placed) ? Resized(standing, placed) : null;

    private static Findings Resized(Rect was, Rect placed) =>
        Findings.Of(new Finding(RequestRules.Unreadable,
            $"the request's `placed` is {Span(placed)} blocks, not the {Span(was)} blocks of the rectangle it replaces",
            Field: "placed"));

    /// <summary>How far the rectangle travelled, read off its corner, which is exact because the spans match.</summary>
    private static (double X, double Z) Shift(Rect was, Rect placed) =>
        (placed.MinX - was.MinX, placed.MinZ - was.MinZ);

    private static Rect Slide(Rect r, double dx, double dz) =>
        new(r.MinX + dx, r.MinZ + dz, r.MaxX + dx, r.MaxZ + dz);

    private static RoomHouse? Slide(RoomHouse? house, double dx, double dz) => house?.Mapped(rect => Slide(rect, dx, dz));

    /// <summary>The cells of wing <paramref name="index"/> of a house, or null where it has none by that number.</summary>
    private static Rect? WingOf(RoomHouse? house, int index) =>
        house is not null && index >= 0 && index < house.Wings.Count ? RoomHouse.RectOf(house.Wings[index]) : null;

    /// <summary>A house turned about the middle of the rectangle round its wings.</summary>
    private static RoomHouse Turned(RoomHouse house, int quarters) =>
        house.Mapped(Pivot.Of(house.Bounds!.Value, quarters).Rect);

    /// <summary>A room's doors turned with its house, about the same pivot.</summary>
    private static List<StatedDoor> Turned(List<StatedDoor> doors, RoomHouse house, int quarters) =>
        [.. doors.Select(Pivot.Of(house.Bounds!.Value, quarters).Door)];

    private static List<StatedDoor> Slide(List<StatedDoor> doors, double dx, double dz) =>
        [.. doors.Select(door => door.Slid(dx, dz))];

    /// <summary>A point on the moved ground. Its Y is the nominal surface it was stated at and is left alone:
    /// what it stands on is measured by the world build against the terrain it laid.</summary>
    private static Pt Slide(Pt p, double dx, double dz) => p with { X = p.X + dx, Z = p.Z + dz };

    private static (double X, double Z) SpanOf(Rect r) => (r.MaxX - r.MinX, r.MaxZ - r.MinZ);
    private static string Span(Rect r) => $"{r.MaxX - r.MinX:0.#} by {r.MaxZ - r.MinZ:0.#}";

    private static bool Holds(Rect outer, Rect inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX
        && inner.MinZ >= outer.MinZ && inner.MaxZ <= outer.MaxZ;

    private static (MapIntent? Intent, Findings Refused) Refuse(
        string rule, string field, string message, string? subject = null) =>
        (null, Findings.Of(new Finding(rule, message, Field: field, Subjects: subject is null ? null : [subject])));
}
