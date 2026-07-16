using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A wool box: the axis-aligned cell region a single wool's approach is emitted into. The approach
/// enters at the <b>mouth</b> (the top edge, <c>z = Z</c> — the hub-side interface) and dead-ends at the
/// <see cref="WoolBoxEmitter"/>-placed room deep in the box.</summary>
public readonly record struct WoolBox(int X, int Z, int W, int H);

/// <summary>The terrain a wool box was filled with: the approach-lane <see cref="Terrain"/> pieces plus the
/// dead-end <see cref="WoolRoom"/>, with the wool marker at piece-relative offset <see cref="At"/> and the
/// fill's published <see cref="Vacancies"/> (box-local shape vacancies).</summary>
public sealed record EmittedApproach(
    IReadOnlyList<GrownPiece> Terrain, GrownPiece WoolRoom, double[] At,
    IReadOnlyList<ShapeVacancy> Vacancies);

/// <summary>
/// The wool binding over <see cref="ShapeEmitter"/> — the composer's mirror of the categorizer's shape
/// read. The emitter owns the family geometry; this binding stamps what is wool-specific: the terminal
/// becomes a <see cref="PlanRoles.WoolRoom"/> piece carrying the wool marker, every piece is wrapped as a
/// composer-native <see cref="GrownPiece"/> with its slot and (when filling a typed box) its
/// <see cref="BoxRef"/> ownership, and ids take the box's prefix. <see cref="AsPlan"/> wraps one emission
/// as a standalone plan for classification and rendering.
/// </summary>
public static class WoolBoxEmitter
{
    /// <summary>The dead-end room's depth along the final corridor, in cells — a two-cell (~10-block)
    /// plateau that clears the export stamp, matching <see cref="SpawnWoolRooms"/>.</summary>
    public const int RoomDepthCells = ShapeEmitter.RoomDepthCells;

    /// <summary>Emit <paramref name="family"/> into <paramref name="box"/> at the given
    /// <paramref name="corridorWidth"/> (cells), in the canonical frame (mouth per the family, top for
    /// lanes). Knobs pass through to <see cref="ShapeEmitter.Emit"/>; see there for their meaning.</summary>
    public static EmittedApproach Emit(ShapeFamily family, WoolBox box, int corridorWidth, bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false, bool woolExtend = false, int attachmentWidth = 0, string idPrefix = "wa", int entryShift = 0, int woolShift = 0, int attachmentOffset = 0)
    {
        EmittedShape shape;
        try
        {
            shape = ShapeEmitter.Emit(family, box.W, box.H, corridorWidth, flip, roomPlacement, attachments, woolAtEnd, woolExtend, attachmentWidth, entryShift, woolShift, attachmentOffset);
        }
        catch (ArgumentException e)
        {
            throw new ComposeException(e.Message.Contains("too small")
                ? $"wool box {box.W}x{box.H} is too small for family {family}."
                : e.Message);
        }
        return Wrap(shape, box.X, box.Z, idPrefix, $"{idPrefix}-wool", null);
    }

    /// <summary>Fill a typed <paramref name="box"/> whose mouth is <paramref name="mouth"/> — any of the four
    /// box edges — with one family, as a <see cref="FillResult"/> (the data-channel form: "no shape fits" is a
    /// signal, not a throw). The shape is emitted mouth-up (<see cref="ShapeEmitter.OrientMouthTop"/>) in the
    /// mouth's <b>along × depth</b> frame, then rotated/flipped so its entry lands on <paramref name="mouth"/>
    /// and it fills the box's dims; pieces carry the box's <see cref="BoxRef"/> and id prefix, the room takes
    /// <paramref name="roomId"/>, and vacancies are published in plan cell coordinates. A plan-cell box docks
    /// the host on whichever edge faces it, so the partitioner drives all four mouths.</summary>
    public static FillResult Fill(
        Box box, BoxEdge mouth, ShapeFamily family, int corridorWidth,
        bool flip = false, string? roomId = null, RoomPlacement roomPlacement = RoomPlacement.Inline)
    {
        // the mouth's frame: its along-edge length and the depth perpendicular to it. Top/Bottom run along the
        // box width; Left/Right run along its height (the shape is rotated a quarter turn onto them).
        var lateral = mouth is BoxEdge.Left or BoxEdge.Right;
        var (alongLen, depth) = lateral ? (box.Rect[3], box.Rect[2]) : (box.Rect[2], box.Rect[3]);

        // canonical dims: the mouth-up normalization transposes left/right-mouth families, so size the canonical
        // frame with the along/depth swapped back through the same map
        var famTransposes = ShapeEmitter.MouthEdge(family, flip) is BoxEdge.Left or BoxEdge.Right;
        var (canonW, canonH) = famTransposes ? (depth, alongLen) : (alongLen, depth);
        var (minW, minH) = ShapeEmitter.MinBox(family, corridorWidth, roomPlacement);
        if (canonW < minW || canonH < minH)
        {
            int minAlong = famTransposes ? minH : minW, minDepth = famTransposes ? minW : minH;
            return lateral ? new FillResult.TooSmall(family, minDepth, minAlong)
                           : new FillResult.TooSmall(family, minAlong, minDepth);
        }

        var raw = ShapeEmitter.Emit(family, canonW, canonH, corridorWidth, flip, roomPlacement);
        var (shape, w, h) = ShapeEmitter.OrientMouthTop(raw, family, flip, canonW, canonH);
        // orient the mouth-up shape onto the requested edge — Bottom mirrors it, Left/Right rotate a quarter turn
        shape = mouth switch
        {
            BoxEdge.Top => shape,
            BoxEdge.Bottom => FlipVertical(shape, h),
            BoxEdge.Right => Rotate(shape, h, cw: true),
            BoxEdge.Left => Rotate(shape, w, cw: false),
            _ => throw new ArgumentException($"unknown box mouth {mouth}."),
        };

        var a = Wrap(shape, box.Rect[0], box.Rect[1], box.Id, roomId ?? $"{box.Id}-room", box.Ref);
        var vacancies = shape.Vacancies
            .Select(v => new Vacancy(v.Kind,
                [box.Rect[0] + v.Rect[0], box.Rect[1] + v.Rect[1], v.Rect[2], v.Rect[3]],
                v.Mouth is { } e ? new BoxInterface(e, e is BoxEdge.Top or BoxEdge.Bottom ? v.Rect[0] : v.Rect[1],
                    e is BoxEdge.Top or BoxEdge.Bottom ? v.Rect[2] : v.Rect[3]) : null,
                v.Walls))
            .ToList();
        return new FillResult.Ok(a, vacancies);
    }

    // box-local vertical mirror (docking the box's bottom edge instead of its top)
    private static EmittedShape FlipVertical(EmittedShape s, int h)
    {
        int[] Map(int[] r) => [r[0], h - r[1] - r[3], r[2], r[3]];
        BoxEdge? Mouth(BoxEdge? e) => e switch
        {
            BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top, _ => e,
        };
        return new EmittedShape(
            s.Terrain.Select(p => (Map(p.Rect), p.Slot)).ToList(),
            Map(s.Room),
            [s.At[0], s.Room[3] - s.At[1]],
            s.Vacancies.Select(v => v with { Rect = Map(v.Rect), Mouth = Mouth(v.Mouth) }).ToList());
    }

    // box-local quarter turn — dock the box's left or right edge. cw rotates the mouth from Top to Right
    // (<paramref name="dim"/> is the mouth-up height); else Top to Left (dim is the mouth-up width). Rects and
    // the marker offset (recomputed against the room's rotated dims) and vacancy mouths all follow the turn.
    private static EmittedShape Rotate(EmittedShape s, int dim, bool cw)
    {
        int[] Map(int[] r) => cw
            ? [dim - r[1] - r[3], r[0], r[3], r[2]]
            : [r[1], dim - r[0] - r[2], r[3], r[2]];
        BoxEdge? Mouth(BoxEdge? e) => e switch
        {
            BoxEdge.Top => cw ? BoxEdge.Right : BoxEdge.Left,
            BoxEdge.Right => cw ? BoxEdge.Bottom : BoxEdge.Top,
            BoxEdge.Bottom => cw ? BoxEdge.Left : BoxEdge.Right,
            BoxEdge.Left => cw ? BoxEdge.Top : BoxEdge.Bottom,
            _ => e,
        };
        double[] at = cw ? [s.Room[3] - s.At[1], s.At[0]] : [s.At[1], s.Room[2] - s.At[0]];
        return new EmittedShape(
            s.Terrain.Select(p => (Map(p.Rect), p.Slot)).ToList(),
            Map(s.Room),
            at,
            s.Vacancies.Select(v => v with { Rect = Map(v.Rect), Mouth = Mouth(v.Mouth) }).ToList());
    }

    // translate box-local -> absolute and wrap as pieces, each carrying its slot (and box ownership when given)
    private static EmittedApproach Wrap(EmittedShape s, int x, int z, string idPrefix, string roomId, BoxRef? box)
    {
        var terrain = new List<GrownPiece>(s.Terrain.Count);
        for (var i = 0; i < s.Terrain.Count; i++)
        {
            var (r, slot) = s.Terrain[i];
            terrain.Add(new GrownPiece($"{idPrefix}-t{i + 1}", [x + r[0], z + r[1], r[2], r[3]], PlanRoles.Piece, slot, box));
        }
        var woolRoom = new GrownPiece(roomId, [x + s.Room[0], z + s.Room[1], s.Room[2], s.Room[3]], PlanRoles.WoolRoom, ApproachSlots.Room, box);
        return new EmittedApproach(terrain, woolRoom, s.At, s.Vacancies);
    }

    /// <summary>Wrap a single emission as a standalone <c>symmetry:none</c> plan — the form the categorizer and
    /// the shape gallery consume.</summary>
    public static PlanModel AsPlan(EmittedApproach a, string name = "wool-box")
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = name },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 },
        };
        foreach (var p in a.Terrain) plan.Pieces.Add(new PlanPiece { Id = p.Id, Role = p.Role, Rect = p.Rect });
        plan.Pieces.Add(new PlanPiece { Id = a.WoolRoom.Id, Role = a.WoolRoom.Role, Rect = a.WoolRoom.Rect });
        plan.Placements.Wools.Add(new WoolPlacement { Piece = a.WoolRoom.Id, At = a.At });
        return plan;
    }
}
