using PgmStudio.Geom;
using PgmStudio.Geom.Relief;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Runs a layout's modifier stack (<see cref="SketchModifierJson"/>) over the ground its layers built.
///
/// <para>The ground of a column is its highest segment among the layers that are terrain: a made thing stands on
/// the ground and a layer seated on it follows it, so neither is moved. The stack carries the heights as doubles
/// from one modifier to the next and rounds once at the end; a top lowered below its segment's floor stops at
/// the floor. A column under a play room is never moved.</para>
/// </summary>
internal static class SketchTerraform
{
    /// <summary>Applies every enabled modifier, in list order, to <paramref name="segments"/> in place. A layout
    /// with no modifier leaves them untouched.</summary>
    public static void Run(SketchLayout? state, List<ColumnSegment> segments,
                           IReadOnlyList<string> axes, double centreX, double centreZ)
    {
        if (state?.Modifiers is not { Count: > 0 } modifiers) return;
        var steps = modifiers.Where(modifier => !modifier.Off)
                             .Select(modifier => SpecOf(modifier, axes, centreX, centreZ))
                             .OfType<ModifierSpec>().ToList();
        if (steps.Count == 0) return;

        var terrain = SketchLayout.Stack(state).Where(layer => !layer.IsMade && !layer.SeatsOnGround)
                                  .Select(layer => layer.Id!).ToHashSet(StringComparer.Ordinal);
        var topOf = new Dictionary<(int X, int Z), int>();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (!terrain.Contains(segment.Layer)) continue;
            if (!topOf.TryGetValue(segment.Cell, out var held) || Higher(segment, segments[held])) topOf[segment.Cell] = index;
        }

        var rooms = SketchRasterizer.RoomCells(state);
        var free = topOf.Where(entry => !rooms.Contains(entry.Key)).Select(entry => entry.Value).ToArray();
        if (free.Length == 0) return;

        var xs = free.Select(index => segments[index].X + 0.5).ToArray();
        var zs = free.Select(index => segments[index].Z + 0.5).ToArray();
        var heights = free.Select(index => (double)segments[index].YTop).ToArray();
        var before = (double[])heights.Clone();
        foreach (var step in steps) Modifiers.Apply(step, heights, xs, zs);

        for (var at = 0; at < free.Length; at++)
        {
            if (heights[at] == before[at] || !double.IsFinite(heights[at])) continue;
            var segment = segments[free[at]];
            segments[free[at]] = segment with { YTop = Math.Max(segment.YFloor, (int)Math.Floor(heights[at] + 0.5)) };
        }
    }

    private static bool Higher(ColumnSegment segment, ColumnSegment other) =>
        segment.YTop > other.YTop || (segment.YTop == other.YTop && segment.YFloor < other.YFloor);

    /// <summary>The modifier as the operations read it, or null for a word this build does not know.</summary>
    private static ModifierSpec? SpecOf(SketchModifierJson modifier, IReadOnlyList<string> axes, double centreX, double centreZ)
    {
        ModifierOp? op = modifier.Op switch
        {
            ModifierOps.Mound => ModifierOp.Mound,
            ModifierOps.Crater => ModifierOp.Crater,
            ModifierOps.Level => ModifierOp.Level,
            ModifierOps.Noise => ModifierOp.Noise,
            _ => null,
        };
        if (op is null) return null;

        var areas = (modifier.Mask?.Areas ?? []).Select(AreaOf).OfType<MaskArea>().ToList();
        if (modifier.Mask?.Mirror == true) areas = MaskArea.Fanned(areas, axes, centreX, centreZ);

        return new ModifierSpec
        {
            Op = op.Value,
            Mode = modifier.Mode switch
            {
                ModifierModes.Lift => ModifierMode.Lift,
                ModifierModes.Cut => ModifierMode.Cut,
                _ => ModifierMode.Unrestricted,
            },
            Areas = areas,
            Rise = modifier.Rise, Power = modifier.Power, Soft = modifier.Soft, Join = modifier.Join,
            Floor = modifier.Floor, Flat = modifier.Flat, Slope = modifier.Slope, Melt = modifier.Melt,
            Top = modifier.Top, Inner = modifier.Inner, Outer = modifier.Outer,
            Sizes = modifier.Sizes?.Select(size => new NoiseSize(size.Cell, size.Octaves, size.Rise)).ToList(),
            Seed = modifier.Seed,
        };
    }

    /// <summary>One outline as the operations measure it, or null where the document states too little to draw
    /// one: an ellipse without a centre or a positive radius, a polygon under three points.</summary>
    private static MaskArea? AreaOf(MaskAreaJson area)
    {
        switch (area.Kind)
        {
            case MaskKinds.Board:
                return MaskArea.Board;
            case MaskKinds.Ellipse:
                if (area.At is not { Length: >= 2 } at) return null;
                var (radiusX, radiusZ) = (area.Rx ?? area.Rz ?? 0, area.Rz ?? area.Rx ?? 0);
                if (!(radiusX > 0) || !(radiusZ > 0) || !double.IsFinite(at[0]) || !double.IsFinite(at[1])) return null;
                return MaskArea.Ellipse(at[0], at[1], radiusX, radiusZ, area.Angle ?? 0);
            case MaskKinds.Polygon:
                if (area.Points is not { Length: >= 3 } points || points.Any(point => point is not { Length: >= 2 })) return null;
                return MaskArea.Polygon(points.Select(point => (point[0], point[1])));
            default:
                return null;
        }
    }
}
