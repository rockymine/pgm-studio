using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The box-local <b>orientation</b> of a mouth-up shape onto any of the four box edges — the one copy every box
/// binding (wool, spawn, later hub/frontline) shares. A shape is always emitted mouth-up
/// (<see cref="ShapeEmitter.OrientMouthTop"/>) in the mouth's <c>along × depth</c> frame; <see cref="To"/> then
/// places its entry on the requested edge: Top is the identity, Bottom a vertical mirror, and Left/Right a
/// quarter turn (so a plan-cell box can dock the host on whichever edge faces it). Rects, the marker offset
/// (recomputed against the room's rotated dims), and vacancy mouths all follow the transform.
/// </summary>
internal static class MouthOrient
{
    /// <summary>Orient the mouth-up <paramref name="shape"/> (extent <paramref name="w"/>×<paramref name="h"/>)
    /// onto <paramref name="mouth"/>. Top/Bottom keep the w×h extent; Left/Right transpose it to h×w.</summary>
    public static EmittedShape To(EmittedShape shape, BoxEdge mouth, int w, int h) => mouth switch
    {
        BoxEdge.Top => shape,
        BoxEdge.Bottom => FlipVertical(shape, h),
        BoxEdge.Right => Rotate(shape, h, cw: true),
        BoxEdge.Left => Rotate(shape, w, cw: false),
        _ => throw new ArgumentException($"unknown box mouth {mouth}."),
    };

    // box-local vertical mirror (docking the box's bottom edge instead of its top)
    private static EmittedShape FlipVertical(EmittedShape s, int h)
    {
        CellRect Map(CellRect r) => new(r.X, h - r.Z - r.Height, r.Width, r.Height);
        BoxEdge? Mouth(BoxEdge? e) => e switch
        {
            BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top, _ => e,
        };
        return new EmittedShape(
            s.Terrain.Select(p => (Map(p.Rect), p.Slot)).ToList(),
            Map(s.Room),
            [s.At[0], s.Room.Height - s.At[1]],
            s.Vacancies.Select(v => v with { Rect = Map(v.Rect), Mouth = Mouth(v.Mouth) }).ToList());
    }

    // box-local quarter turn — dock the box's left or right edge. cw rotates the mouth from Top to Right
    // (dim is the mouth-up height); else Top to Left (dim is the mouth-up width). Rects and the marker offset
    // (recomputed against the room's rotated dims) and vacancy mouths all follow the turn.
    private static EmittedShape Rotate(EmittedShape s, int dim, bool cw)
    {
        CellRect Map(CellRect r) => cw
            ? new(dim - r.Z - r.Height, r.X, r.Height, r.Width)
            : new(r.Z, dim - r.X - r.Width, r.Height, r.Width);
        BoxEdge? Mouth(BoxEdge? e) => e switch
        {
            BoxEdge.Top => cw ? BoxEdge.Right : BoxEdge.Left,
            BoxEdge.Right => cw ? BoxEdge.Bottom : BoxEdge.Top,
            BoxEdge.Bottom => cw ? BoxEdge.Left : BoxEdge.Right,
            BoxEdge.Left => cw ? BoxEdge.Top : BoxEdge.Bottom,
            _ => e,
        };
        double[] at = cw ? [s.Room.Height - s.At[1], s.At[0]] : [s.At[1], s.Room.Width - s.At[0]];
        return new EmittedShape(
            s.Terrain.Select(p => (Map(p.Rect), p.Slot)).ToList(),
            Map(s.Room),
            at,
            s.Vacancies.Select(v => v with { Rect = Map(v.Rect), Mouth = Mouth(v.Mouth) }).ToList());
    }
}
