using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Move one play piece — a spawn, a wool, a destroyable or a core — by its id, and every orbit image of it
/// with it. The sketch draws the pieces out of the intent, so a drag on the canvas arrives here or the picture
/// and the world build answer differently.
///
/// <para><b>It moves and never resizes.</b> A room's marker is a fraction of its region and a goal's structure
/// is centred on its anchor, so a placed rectangle whose span differs from the standing one is refused rather
/// than accommodated.</para>
///
/// <para><b>Moving a room's region carries the room on it</b> — the marker, the building, the iron, the
/// entries — because the region is the ground they stand on. Moving the <b>building</b> moves the building
/// alone. A goal has one rectangle, its structure's footprint, and moving it moves the anchor.</para>
///
/// <para><b>A room also turns,</b> a quarter at a time about the middle of its region — the region, the marker, the
/// building, the iron, the entries and the doors with it — or its building alone about the building's middle. An
/// objective states no facing, so it does not turn.</para>
///
/// <para><b>The orbit moves together.</b> Every piece of the same kind stating the same
/// <see cref="StampId.Identity"/> is an image of one authored unit, and image <c>j</c> of a unit whose image
/// <c>i</c> moved by <c>d</c> moves by <c>d</c> turned through the symmetry's step <c>j − i</c>, so a mirrored
/// board stays mirrored; it turns the same way where the step is a rotation and the other way where it is a
/// reflection. A piece with no stamp is its own unit and moves alone, and <c>alone</c> moves or turns the named
/// image without its others.</para>
/// </summary>
public static class PlayPieceMove
{
    /// <summary>The intent with the piece <paramref name="id"/> and its images moved so that its
    /// <paramref name="part"/> — the room's own region word or absent for the piece itself, <c>building</c> for
    /// a room's building — stands at <paramref name="placed"/>, or the findings that stopped it and a null
    /// intent. Pure: the caller stores and projects.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, string? part, Rect placed, bool alone = false)
    {
        var building = part == StructuralRoles.Building;
        var mode = intent.Symmetry?.Mode;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            if (Unpartable(part, StructuralRoles.Spawn) is { } unpartable) return (null, unpartable);
            var spawn = intent.Spawns[spawnAt];
            var stopped = Stopped(spawn.Protection, spawn.Footprint, building, placed);
            if (stopped is not null) return (null, stopped);
            var (dx, dz) = Shift(building ? spawn.Footprint!.Value : spawn.Protection[0], placed);
            var spawns = Orbit(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => building ? entry with { Footprint = Slide(entry.Footprint, x, z) } : entry with
                {
                    Protection = [.. entry.Protection.Select(r => Slide(r, x, z))],
                    Point = Slide(entry.Point, x, z),
                    Footprint = Slide(entry.Footprint, x, z),
                    Iron = [.. entry.Iron.Select(pt => Slide(pt, x, z))],
                });
            return Checked(intent with { Spawns = spawns }, Carried(intent.Spawns, spawns)
                .Select(entry => (entry.Id, entry.Protection, entry.Footprint)));
        }

        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if (Unpartable(part, StructuralRoles.WoolRoom) is { } unpartable) return (null, unpartable);
            var wool = wools[woolAt];
            var stopped = Stopped(wool.Protection, wool.Footprint, building, placed);
            if (stopped is not null) return (null, stopped);
            var (dx, dz) = Shift(building ? wool.Footprint!.Value : wool.Protection[0], placed);
            var moved = Orbit(wools, woolAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => building ? entry with { Footprint = Slide(entry.Footprint, x, z) } : entry with
                {
                    Protection = [.. entry.Protection.Select(r => Slide(r, x, z))],
                    Spawn = Slide(entry.Spawn, x, z),
                    Footprint = Slide(entry.Footprint, x, z),
                    Entries = [.. entry.Entries.Select(r => Slide(r, x, z))],
                });
            return Checked(intent with { Wools = moved }, Carried(wools, moved)
                .Select(entry => (entry.Id, entry.Protection, entry.Footprint)));
        }

        var destroyables = intent.Destroyables ?? [];
        if (destroyables.FindIndex(goal => goal.Id == id) is var goalAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            var goal = destroyables[goalAt];
            var (width, _, depth) = ObjectiveFootprint.Destroyable(
                DestroyableStyles.TryParse(goal.Style, out var style) ? style : ObjectiveDefaults.Style);
            if (GoalShift(goal.Anchor, width, depth, placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var moved = Orbit(destroyables, goalAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Anchor = Slide(entry.Anchor, x, z), Box = null });
            return (intent with { Destroyables = moved }, Findings.None);
        }

        var cores = intent.Cores ?? [];
        if (cores.FindIndex(core => core.Id == id) is var coreAt and >= 0)
        {
            if (Unpartable(part, null) is { } unpartable) return (null, unpartable);
            var core = cores[coreAt];
            var (width, depth) = ObjectiveFootprint.Core(core.Size);
            if (GoalShift(core.Anchor, width, depth, placed, out var dx, out var dz) is { } stopped)
                return (null, stopped);
            var moved = Orbit(cores, coreAt, entry => entry.Stamp, mode, alone, dx, dz,
                (entry, x, z) => entry with { Anchor = Slide(entry.Anchor, x, z), Box = null });
            return (intent with { Cores = moved }, Findings.None);
        }

        return Refuse(RequestRules.NoSuchSubject, "id", $"the map has no play piece with the id '{id}'", id);
    }

    /// <summary>The intent with the room <paramref name="id"/> — or, with <paramref name="part"/>
    /// <c>building</c>, its building alone — turned <paramref name="quarters"/> quarter turns clockwise seen from
    /// above, and its orbit images with it, or the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Turn(
        MapIntent intent, string id, string? part, int quarters, bool alone = false)
    {
        var building = part == StructuralRoles.Building;
        var mode = intent.Symmetry?.Mode;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            if ((Unpartable(part, StructuralRoles.Spawn) ?? Unturnable(intent.Spawns[spawnAt].Protection,
                    intent.Spawns[spawnAt].Footprint, building)) is { } stopped) return (null, stopped);
            var spawns = Images(intent.Spawns, spawnAt, entry => entry.Stamp, mode, alone,
                (entry, step) => TurnSpawn(entry, building, Handed(quarters, mode, step)));
            return Checked(intent with { Spawns = spawns }, Carried(intent.Spawns, spawns)
                .Select(entry => (entry.Id, entry.Protection, entry.Footprint)));
        }

        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if ((Unpartable(part, StructuralRoles.WoolRoom) ?? Unturnable(wools[woolAt].Protection,
                    wools[woolAt].Footprint, building)) is { } stopped) return (null, stopped);
            var turned = Images(wools, woolAt, entry => entry.Stamp, mode, alone,
                (entry, step) => TurnWool(entry, building, Handed(quarters, mode, step)));
            return Checked(intent with { Wools = turned }, Carried(wools, turned)
                .Select(entry => (entry.Id, entry.Protection, entry.Footprint)));
        }

        if ((intent.Destroyables ?? []).Any(goal => goal.Id == id) || (intent.Cores ?? []).Any(core => core.Id == id))
            return Refuse(RequestRules.Unreadable, "turn", $"'{id}' is an objective, which states no facing to turn");
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
    private static List<T> Images<T>(
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

    /// <summary>The quarter turns an image takes for the named one's <paramref name="quarters"/>: the same where
    /// the step between them is a rotation, the other way where it is a reflection.</summary>
    private static int Handed(int quarters, string? mode, int step) =>
        Symmetry.Reflects(mode, step) ? -quarters : quarters;

    private static SpawnIntent TurnSpawn(SpawnIntent spawn, bool building, int quarters)
    {
        if (building) return spawn with { Footprint = Pivot.Of(spawn.Footprint!.Value, quarters).Rect(spawn.Footprint!.Value) };
        var pivot = Pivot.Of(spawn.Protection[0], quarters);
        return spawn with
        {
            Protection = [pivot.Rect(spawn.Protection[0])],
            Point = pivot.Point(spawn.Point),
            Footprint = spawn.Footprint is { } house ? pivot.Rect(house) : null,
            Iron = [.. spawn.Iron.Select(pivot.Point)],
            Doors = [.. spawn.Doors.Select(pivot.Door)],
            Yaw = ((spawn.Yaw + 90 * pivot.Quarters) % 360 + 360) % 360,
        };
    }

    private static WoolIntent TurnWool(WoolIntent wool, bool building, int quarters)
    {
        if (building) return wool with { Footprint = Pivot.Of(wool.Footprint!.Value, quarters).Rect(wool.Footprint!.Value) };
        var pivot = Pivot.Of(wool.Protection[0], quarters);
        return wool with
        {
            Protection = [pivot.Rect(wool.Protection[0])],
            Spawn = pivot.Point(wool.Spawn),
            Footprint = wool.Footprint is { } house ? pivot.Rect(house) : null,
            Entries = [.. wool.Entries.Select(pivot.Rect)],
        };
    }

    /// <summary>What stops a turn: a region of several rectangles, or a building to turn that is not stated.</summary>
    private static Findings? Unturnable(IReadOnlyList<Rect> protection, Rect? footprint, bool building)
    {
        if (protection.Count != 1)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"the room's `protection` has {protection.Count} rectangles, not 1", Field: "protection"));
        if (building && footprint is null)
            return Findings.Of(new Finding(RequestRules.Unreadable, "the room states no `footprint`", Field: "part"));
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

        public string Door(string word)
        {
            if (RoomEdges.OfWord(word) is not { } edge) return word;
            var (dx, dz) = edge.Outward();
            for (var k = 0; k < Quarters; k++) (dx, dz) = (-dz, dx);
            return RoomEdges.Nearest((dx, dz), RoomEdges.All) is { } image ? image.Word() : word;
        }
    }

    /// <summary>The entries the move replaced — every other entry is the same object it was.</summary>
    private static IEnumerable<T> Carried<T>(List<T> was, List<T> moved) where T : class =>
        moved.Where((entry, index) => !ReferenceEquals(entry, was[index]));

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

        if (!building) return SpanOf(protection[0]) != SpanOf(placed) ? Resized(protection[0], placed) : null;

        if (footprint is not { } standing)
            return Findings.Of(new Finding(RequestRules.Unreadable, "the room states no `footprint`", Field: "part"));
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

    /// <summary>An objective's displacement: its footprint is the structure centred on the anchor, in whole
    /// blocks, and the placed rectangle has to be that footprint somewhere else.</summary>
    private static Findings? GoalShift(Pt anchor, int width, int depth, Rect placed, out double dx, out double dz)
    {
        dx = dz = 0;
        var (minX, minZ, maxX, maxZ) = ObjectiveFootprint.Centred(anchor.X, anchor.Z, width, depth);
        var standing = new Rect(minX, minZ, maxX + 1, maxZ + 1);
        if (SpanOf(standing) != SpanOf(placed)) return Resized(standing, placed);
        (dx, dz) = Shift(standing, placed);
        if (dx != Math.Floor(dx) || dz != Math.Floor(dz))
            return Findings.Of(new Finding(RequestRules.Unreadable,
                "the placed rectangle is off the block grid, and an objective moves by whole blocks", Field: "placed"));
        return null;
    }

    private static Findings Resized(Rect was, Rect placed) =>
        Findings.Of(new Finding(RequestRules.Unreadable,
            $"the request's `placed` is {Span(placed)} blocks, not the {Span(was)} blocks of the rectangle it replaces",
            Field: "placed"));

    /// <summary>How far the rectangle travelled, read off its corner, which is exact because the spans match.</summary>
    private static (double X, double Z) Shift(Rect was, Rect placed) =>
        (placed.MinX - was.MinX, placed.MinZ - was.MinZ);

    private static Rect Slide(Rect r, double dx, double dz) =>
        new(r.MinX + dx, r.MinZ + dz, r.MaxX + dx, r.MaxZ + dz);

    private static Rect? Slide(Rect? r, double dx, double dz) => r is { } rect ? Slide(rect, dx, dz) : null;

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
