using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Move one room piece's rectangle — the region a spawn or wool room owns, or the building raised inside it.
/// The sketch draws both as locked annotations projected out of this intent, so a drag on the canvas has to
/// arrive back here or the two answers diverge: the picture would say one place and the world build another.
///
/// <para><b>It moves and never resizes.</b> A spawn or wool's <c>at</c> is a fractional offset into its
/// region, so a region that changed size would shift the marker inside it, and the marker's own rules — the
/// pad's parity, its clearance to the walls, where the monuments seat — are resolved against a frame that is
/// no longer the one they were resolved on. A resize is that whole question and this is not it, so a placed
/// rectangle whose span differs from the one it replaces is refused rather than accommodated.</para>
///
/// <para><b>Moving the region carries the room on it.</b> The region is the ground a room stands on, not a
/// zone drawn beside it, so sliding it slides everything seated there — the marker players or a wool arrive
/// at, the building raised on it, the iron beside it, the entry interfaces its doors are cut on. Leaving any
/// of them behind puts it off the ground it belongs to, which the footprint rule below would refuse a moment
/// later and the others would carry silently. Moving the <b>building</b> moves the building alone: it is the
/// house on the ground rather than the ground.</para>
///
/// <para><b>One image, not the orbit.</b> Each image of a mirrored board is its own entry with its own team,
/// so moving red's spawn is a statement about red's spawn; the other images are other teams' and are left
/// alone. Symmetry in the sketch is a preview and a mirror flag rather than a constraint, and the plan is
/// where a board's structure is made symmetric.</para>
///
/// <para>A plan rebuild replaces what this writes, which is the standing rule for structure rather than
/// anything about this edit: the plan owns a board's spawns, wools and build zones, and
/// <c>PUT /intent/from-plan</c> carries only the authored slices across.</para>
/// </summary>
public static class RoomPieceMove
{
    /// <summary>Which of a room's two rectangles is being moved — the region, or the building on it. They are
    /// the same two words the sketch tags its annotations with.</summary>
    public static bool IsPart(string part) =>
        part is StructuralRoles.Spawn or StructuralRoles.WoolRoom or StructuralRoles.Building;

    /// <summary>Put the room named by <paramref name="reference"/> — a team id for a spawn, <c>owner:colour</c>
    /// for a wool, the same <c>intentRef</c> the sketch annotation carries — at <paramref name="placed"/>.
    ///
    /// <para>Answers the intent to store, or the findings that stopped it and a null intent. Pure: it reads
    /// the intent it is given and returns another, so the caller owns the storing and the projection.</para>
    /// </summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string reference, string part, Rect placed)
    {
        if (!IsPart(part))
            return Refuse(RequestRules.Unreadable, "part",
                $"'{part}' is not a room's rectangle — it is one of "
                + $"{string.Join(", ", StructuralRoles.All)}.");

        // A wool's reference carries its owner and its dye; a spawn's is the team alone. The colon is what
        // tells them apart, and it is the shape the sketch already writes rather than a second encoding.
        var colon = reference.IndexOf(':');
        return colon >= 0
            ? MoveWool(intent, reference[..colon], reference[(colon + 1)..], part, placed)
            : MoveSpawn(intent, reference, part, placed);
    }

    private static (MapIntent? Intent, Findings Refused) MoveSpawn(
        MapIntent intent, string team, string part, Rect placed)
    {
        var index = intent.Spawns.FindIndex(s => s.Team == team);
        if (index < 0)
            return Refuse(RequestRules.NoSuchSubject, "reference",
                $"no spawn belongs to team '{team}'.", team);

        var spawn = intent.Spawns[index];
        if (Stopped(spawn.Protection, spawn.Footprint, part, placed) is { } refused)
            return (null, refused);

        SpawnIntent moved;
        if (part == StructuralRoles.Building) moved = spawn with { Footprint = placed };
        else
        {
            var (dx, dz) = Shift(spawn.Protection[0], placed);
            moved = spawn with
            {
                Protection = [placed],
                Point = Slide(spawn.Point, dx, dz),
                Footprint = spawn.Footprint is { } f ? Slide(f, dx, dz) : null,
                Iron = [.. spawn.Iron.Select(pt => Slide(pt, dx, dz))],
            };
        }
        var spawns = new List<SpawnIntent>(intent.Spawns) { [index] = moved };
        return (intent with { Spawns = spawns }, Findings.None);
    }

    private static (MapIntent? Intent, Findings Refused) MoveWool(
        MapIntent intent, string owner, string color, string part, Rect placed)
    {
        var wools = new List<WoolIntent>(intent.Wools ?? []);
        var index = wools.FindIndex(w => w.Owner == owner && w.Color == color);
        if (index < 0)
            return Refuse(RequestRules.NoSuchSubject, "reference",
                $"no {color} wool belongs to team '{owner}'.", $"{owner}:{color}");

        var wool = wools[index];
        if (Stopped(wool.Protection, wool.Footprint, part, placed) is { } refused)
            return (null, refused);

        if (part == StructuralRoles.Building) wools[index] = wool with { Footprint = placed };
        else
        {
            var (dx, dz) = Shift(wool.Protection[0], placed);
            wools[index] = wool with
            {
                Protection = [placed],
                Spawn = Slide(wool.Spawn, dx, dz),
                Footprint = wool.Footprint is { } f ? Slide(f, dx, dz) : null,
                Entries = [.. wool.Entries.Select(e => Slide(e, dx, dz))],
            };
        }
        return (intent with { Wools = wools }, Findings.None);
    }

    /// <summary>What stops a move, for either kind of room: the two rules are about a region and a footprint
    /// rather than about a spawn or a wool, so they are asked once.</summary>
    private static Findings? Stopped(
        IReadOnlyList<Rect> protection, Rect? footprint, string part, Rect placed)
    {
        var region = protection.Count > 0 ? protection[0] : (Rect?)null;

        if (part == StructuralRoles.Building)
        {
            if (footprint is not { } standing)
                return Findings.Of(new Finding(RequestRules.Unreadable,
                    "this room states no footprint, so there is no building rectangle to move. The shell "
                    + "stands on the region inset a block on every side until one is stated.", Field: "part"));
            if (SpanOf(standing) != SpanOf(placed)) return Resized(standing, placed);
            // The building is the house raised on the region's ground; carried outside it, it stands over
            // whatever the neighbour happens to be, or over the void.
            if (region is { } ground && !Holds(ground, placed))
                return Findings.Of(new Finding(RoomFrameRules.FootprintOffPiece,
                    "the building would stand outside the region that is its ground.", Field: "part"));
            return null;
        }

        if (region is not { } standingRegion)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                "this room states no region, so there is no rectangle to move.", Field: "part"));

        // The sketch draws one rectangle per region. A region an author drew as a union of several has no
        // single rectangle a drag could mean, and moving the first alone would tear the union apart.
        if (protection.Count > 1)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"this room's region is {protection.Count} rectangles and the sketch draws one, so there is "
                + "no single rectangle to move. Edit the region in Configure.", Field: "protection"));

        if (SpanOf(standingRegion) != SpanOf(placed)) return Resized(standingRegion, placed);
        return null;
    }

    private static Findings Resized(Rect was, Rect placed) =>
        Findings.Of(new Finding(RequestRules.Unreadable,
            $"a room piece moves and does not resize: it was {Span(was)} and was placed as {Span(placed)}. "
            + "A room's marker is a fraction of its own rectangle, so resizing one moves the marker inside it.",
            Field: "placed"));

    /// <summary>How far the region travelled — read off its corner, which is exact because the spans match.
    /// Everything seated on that ground is carried by the same pair.</summary>
    private static (double X, double Z) Shift(Rect was, Rect placed) =>
        (placed.MinX - was.MinX, placed.MinZ - was.MinZ);

    private static Rect Slide(Rect r, double dx, double dz) =>
        new(r.MinX + dx, r.MinZ + dz, r.MaxX + dx, r.MaxZ + dz);

    /// <summary>A point on the moved ground. Its Y is the nominal surface the room was stated at and is left
    /// alone: what the marker actually stands on is measured by the world build against the terrain it laid,
    /// so a Y carried here would be a second answer to a question already settled elsewhere.</summary>
    private static Pt Slide(Pt p, double dx, double dz) => p with { X = p.X + dx, Z = p.Z + dz };

    private static (double X, double Z) SpanOf(Rect r) => (r.MaxX - r.MinX, r.MaxZ - r.MinZ);
    private static string Span(Rect r) => $"{r.MaxX - r.MinX:0.#}×{r.MaxZ - r.MinZ:0.#}";

    private static bool Holds(Rect outer, Rect inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX
        && inner.MinZ >= outer.MinZ && inner.MaxZ <= outer.MaxZ;

    private static (MapIntent? Intent, Findings Refused) Refuse(
        string rule, string field, string message, string? subject = null) =>
        (null, Findings.Of(new Finding(rule, message, Field: field,
                                       Subjects: subject is null ? null : [subject])));
}
