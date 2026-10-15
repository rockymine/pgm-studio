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
/// <para><b>The orbit moves together.</b> Every piece of the same kind stating the same
/// <see cref="StampId.Identity"/> is an image of one authored unit, and image <c>j</c> of a unit whose image
/// <c>i</c> moved by <c>d</c> moves by <c>d</c> turned through the symmetry's step <c>j − i</c>, so a mirrored
/// board stays mirrored. A piece with no stamp is its own unit and moves alone.</para>
/// </summary>
public static class PlayPieceMove
{
    /// <summary>The intent with the piece <paramref name="id"/> and its images moved so that its
    /// <paramref name="part"/> — the room's own region word or absent for the piece itself, <c>building</c> for
    /// a room's building — stands at <paramref name="placed"/>, or the findings that stopped it and a null
    /// intent. Pure: the caller stores and projects.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(MapIntent intent, string id, string? part, Rect placed)
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
            var spawns = Orbit(intent.Spawns, spawnAt, entry => entry.Stamp, mode, dx, dz,
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
            var moved = Orbit(wools, woolAt, entry => entry.Stamp, mode, dx, dz,
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
            var moved = Orbit(destroyables, goalAt, entry => entry.Stamp, mode, dx, dz,
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
            var moved = Orbit(cores, coreAt, entry => entry.Stamp, mode, dx, dz,
                (entry, x, z) => entry with { Anchor = Slide(entry.Anchor, x, z), Box = null });
            return (intent with { Cores = moved }, Findings.None);
        }

        return Refuse(RequestRules.NoSuchSubject, "id", $"the map has no play piece with the id '{id}'", id);
    }

    /// <summary>The list with the piece at <paramref name="at"/> moved by (<paramref name="dx"/>,
    /// <paramref name="dz"/>) and each other image of its unit moved by that displacement turned through the
    /// step between the two images. A resolved goal box is dropped by the caller's slide, since the world
    /// build resolves it again over the ground the goal now stands on.</summary>
    private static List<T> Orbit<T>(
        List<T> pieces, int at, Func<T, StampId> stampOf, string? mode, double dx, double dz,
        Func<T, double, double, T> slide)
    {
        var moved = new List<T>(pieces) { [at] = slide(pieces[at], dx, dz) };
        var stamp = stampOf(pieces[at]);
        if (string.IsNullOrEmpty(stamp.Kind)) return moved;

        var order = Symmetry.Order(mode);
        for (var index = 0; index < pieces.Count; index++)
        {
            var image = stampOf(pieces[index]);
            if (index == at || image.Identity != stamp.Identity || image.Image == stamp.Image) continue;
            if (image.Image >= order || stamp.Image >= order) continue;
            var step = ((image.Image - stamp.Image) % order + order) % order;
            var (x, z) = Symmetry.Point(dx, dz, mode, 0, 0, step);
            moved[index] = slide(pieces[index], Math.Round(x), Math.Round(z));
        }
        return moved;
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
