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
/// the floor.</para>
///
/// <para>A held column is never moved: one under a play room, under a shape stating <c>relief_scope: hold</c>, or
/// under a shape standing at a <c>level</c> height. A shape standing a stated amount over the ground rides the
/// stack: a <c>drape</c> moves each column by the change the stack made to the ground there, so a wall climbs the
/// hill raised under it, and a <c>raise</c>, <c>sink</c> or <c>relief_scope: ride</c> shape moves by the median
/// change under it, so it stays one flat thing standing proud (<see cref="SketchRasterizer.StackRelations"/>).
/// One modifier can leave named shapes out of its own run (<see cref="ModifierMaskJson.Leaves"/>).</para>
///
/// <para>A river and a lake wet the columns they cut. The stack answers those as <see cref="Run"/>'s result: each
/// wet cell and the height its water surface stands at, in the same unit as a column's top, so water fills the
/// blocks under it. A cell whose ground a later modifier raised to the surface or above is dry and not
/// listed.</para>
/// </summary>
internal static class SketchTerraform
{
    /// <summary>Applies every enabled modifier, in list order, to <paramref name="segments"/> in place, and
    /// answers the water the stack made: each wet cell and the height its surface stands at. A layout with no
    /// modifier leaves the segments untouched and makes no water. Given <paramref name="changed"/>, it is filled
    /// with one count per modifier in list order: the columns whose rounded top that modifier's own run changed,
    /// zero for one switched off or unknown.</summary>
    public static Dictionary<(int X, int Z), int> Run(SketchLayout? state, List<ColumnSegment> segments,
                                                      IReadOnlyList<string> axes, double centreX, double centreZ,
                                                      List<int>? changed = null)
    {
        var wet = new Dictionary<(int X, int Z), int>();
        changed?.Clear();
        changed?.AddRange(new int[state?.Modifiers?.Count ?? 0]);
        if (state?.Modifiers is not { Count: > 0 } modifiers) return wet;
        var steps = new List<(int At, SketchModifierJson Modifier, ModifierSpec Spec)>();
        for (var at = 0; at < modifiers.Count; at++)
            if (!modifiers[at].Off && SpecOf(modifiers[at], axes, centreX, centreZ) is { } spec)
                steps.Add((at, modifiers[at], spec));
        if (steps.Count == 0) return wet;

        var terrain = SketchLayout.Stack(state).Where(layer => !layer.IsMade && !layer.SeatsOnGround)
                                  .Select(layer => layer.Id!).ToHashSet(StringComparer.Ordinal);
        var topOf = new Dictionary<(int X, int Z), int>();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (!terrain.Contains(segment.Layer)) continue;
            if (!topOf.TryGetValue(segment.Cell, out var held) || Higher(segment, segments[held])) topOf[segment.Cell] = index;
        }

        var (holding, riders) = SketchRasterizer.StackRelations(state);
        var moving = topOf.Where(entry => !holding.Contains(entry.Key)).ToArray();
        if (moving.Length == 0) return wet;

        var xs = moving.Select(entry => entry.Key.X + 0.5).ToArray();
        var zs = moving.Select(entry => entry.Key.Z + 0.5).ToArray();
        var heights = moving.Select(entry => (double)segments[entry.Value].YTop).ToArray();
        var before = (double[])heights.Clone();
        var surfaces = new double[moving.Length];
        Array.Fill(surfaces, double.NaN);
        var previous = changed is null ? null : (double[])heights.Clone();
        var leftCells = SketchRasterizer.ShapeCells(state, steps.SelectMany(step => step.Modifier.Mask.Leaves ?? [])
                                                                  .ToHashSet(StringComparer.Ordinal));
        foreach (var step in steps)
        {
            Apply(step.Spec, heights, xs, zs, surfaces, LeftOut(step.Modifier.Mask.Leaves, leftCells, moving));
            if (changed is null) continue;
            changed[step.At] = Moved(previous!, heights);
            Array.Copy(heights, previous!, heights.Length);
        }

        var changeAt = new Dictionary<(int X, int Z), double>(moving.Length);
        for (var at = 0; at < moving.Length; at++)
            if (double.IsFinite(heights[at])) changeAt[moving[at].Key] = heights[at] - before[at];

        // A rider's columns take the ground's change rather than their own run through the stack.
        var shifted = new Dictionary<(int X, int Z), double>();
        foreach (var (mode, cells) in riders)
        {
            var under = cells.Where(changeAt.ContainsKey).ToList();
            if (under.Count == 0) continue;
            var median = under.Select(cell => changeAt[cell]).Order().ElementAt(under.Count / 2);
            foreach (var cell in under) shifted[cell] = mode == HeightModes.Drape ? changeAt[cell] : median;
        }

        for (var at = 0; at < moving.Length; at++)
        {
            var (cell, index) = moving[at];
            var change = shifted.TryGetValue(cell, out var ride) ? ride : changeAt.GetValueOrDefault(cell);
            if (change != 0)
            {
                var segment = segments[index];
                segments[index] = segment with
                {
                    YTop = Math.Max(segment.YFloor, (int)Math.Floor(segment.YTop + change + 0.5)),
                };
            }
            if (double.IsFinite(surfaces[at]) && segments[index].YTop < surfaces[at])
                wet[cell] = (int)Math.Floor(surfaces[at]);
        }
        return wet;
    }

    /// <summary>The columns, by position in the moving set, that a modifier naming <paramref name="leaves"/>
    /// leaves alone; null where it names none that stand on moving ground.</summary>
    private static bool[]? LeftOut(List<string>? leaves, Dictionary<string, HashSet<(int X, int Z)>> cellsOf,
                                   KeyValuePair<(int X, int Z), int>[] moving)
    {
        if (leaves is not { Count: > 0 }) return null;
        var cells = leaves.Where(cellsOf.ContainsKey).SelectMany(id => cellsOf[id]).ToHashSet();
        if (cells.Count == 0) return null;
        var left = moving.Select(entry => cells.Contains(entry.Key)).ToArray();
        return left.Any(flag => flag) ? left : null;
    }

    /// <summary>Runs one step over the heights, with the columns flagged in <paramref name="left"/> taken out of
    /// the run altogether: the operation neither moves them nor reads them.</summary>
    private static void Apply(ModifierSpec spec, double[] heights, double[] xs, double[] zs, double[] surfaces,
                              bool[]? left)
    {
        if (left is null) { Modifiers.Apply(spec, heights, xs, zs, surfaces); return; }
        var kept = Enumerable.Range(0, heights.Length).Where(at => !left[at]).ToArray();
        var keptHeights = kept.Select(at => heights[at]).ToArray();
        var keptSurfaces = kept.Select(at => surfaces[at]).ToArray();
        Modifiers.Apply(spec, keptHeights, kept.Select(at => xs[at]).ToArray(), kept.Select(at => zs[at]).ToArray(),
                        keptSurfaces);
        for (var at = 0; at < kept.Length; at++) { heights[kept[at]] = keptHeights[at]; surfaces[kept[at]] = keptSurfaces[at]; }
    }

    /// <summary>How many columns a step changed by a whole block once rounded.</summary>
    private static int Moved(double[] before, double[] after)
    {
        var moved = 0;
        for (var at = 0; at < before.Length; at++)
            if (double.IsFinite(before[at]) && double.IsFinite(after[at])
                && Math.Floor(before[at] + 0.5) != Math.Floor(after[at] + 0.5)) moved++;
        return moved;
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
            ModifierOps.Ridge => ModifierOp.Ridge,
            ModifierOps.Butte => ModifierOp.Butte,
            ModifierOps.Spire => ModifierOp.Spire,
            ModifierOps.Scarp => ModifierOp.Scarp,
            ModifierOps.Canyon => ModifierOp.Canyon,
            ModifierOps.River => ModifierOp.River,
            ModifierOps.Lake => ModifierOp.Lake,
            ModifierOps.Road => ModifierOp.Road,
            ModifierOps.Gullies => ModifierOp.Gullies,
            ModifierOps.Warp => ModifierOp.Warp,
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
            Wander = modifier.Wander, Cliff = modifier.Cliff, Talus = modifier.Talus, Apron = modifier.Apron,
            Jag = modifier.Jag, Taper = modifier.Taper, Height = modifier.Height, Side = modifier.Side,
            Depth = modifier.Depth, Bed = modifier.Bed, Wall = modifier.Wall, Ledges = modifier.Ledges,
            Water = modifier.Water, Bank = modifier.Bank, Level = modifier.Level, Shore = modifier.Shore,
            Grade = modifier.Grade, Shoulder = modifier.Shoulder, Count = modifier.Count,
            Sharpness = modifier.Sharpness, Amount = modifier.Amount, Cell = modifier.Cell,
            Sizes = modifier.Sizes?.Select(size => new NoiseSize(size.Cell, size.Octaves, size.Rise)).ToList(),
            Seed = modifier.Seed,
        };
    }

    /// <summary>One outline as the operations measure it, or null where the document states too little to draw
    /// one: an ellipse without a centre or a positive radius, a polygon under three points, a line under two
    /// points or without a positive width.</summary>
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
            case MaskKinds.Line:
                if (area.Points is not { Length: >= 2 } path || path.Any(point => point is not { Length: >= 2 })
                    || !(area.Width > 0) || path.Any(point => !double.IsFinite(point[0]) || !double.IsFinite(point[1])))
                    return null;
                return MaskArea.Line(path.Select(point => (point[0], point[1])), area.Width.Value);
            default:
                return null;
        }
    }
}
