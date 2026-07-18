using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The body twin of <see cref="MouthOrient"/>: orient a canonical <b>spine-top</b> <see cref="ShapeBody"/> onto
/// any box edge — the one copy the terminal-free box kinds share (the hub's front-flip, the frontline's spine
/// dock). A body is emitted spine-up (its reference edge on <see cref="BoxEdge.Top"/>); <see cref="To"/> moves
/// that reference to <paramref name="spine"/>: <see cref="BoxEdge.Top"/> is the identity, <see cref="BoxEdge.Bottom"/>
/// a vertical mirror, <see cref="BoxEdge.Left"/>/<see cref="BoxEdge.Right"/> a quarter turn (transposing the box).
/// Piece and vacancy rects, and vacancy mouths, all follow the transform.
/// </summary>
internal static class BodyOrient
{
    /// <summary>Orient the spine-up <paramref name="body"/> (canonical extent <paramref name="w"/>×<paramref name="h"/>)
    /// so its reference edge docks <paramref name="spine"/>. Top/Bottom keep the w×h extent; Left/Right transpose
    /// it to h×w.</summary>
    public static ShapeBody To(ShapeBody body, BoxEdge spine, int w, int h)
    {
        Func<int[], int[]> map = spine switch
        {
            BoxEdge.Top => r => r,
            BoxEdge.Bottom => r => [r[0], h - r[1] - r[3], r[2], r[3]],
            BoxEdge.Right => r => [h - r[1] - r[3], r[0], r[3], r[2]],   // quarter turn: Top → Right (dim = h)
            BoxEdge.Left => r => [r[1], w - r[0] - r[2], r[3], r[2]],    // quarter turn: Top → Left (dim = w)
            _ => throw new ArgumentException($"unknown spine edge {spine}."),
        };
        return new ShapeBody(
            body.Pieces.Select(p => (map(p.Rect), p.Slot)).ToList(),
            body.Vacancies.Select(v => v with { Rect = map(v.Rect), Mouth = MapEdge(v.Mouth, spine) }).ToList());
    }

    // a vacancy mouth follows the same turn the reference edge does (Top → spine)
    private static BoxEdge? MapEdge(BoxEdge? e, BoxEdge spine) => e is not { } m ? null : spine switch
    {
        BoxEdge.Top => m,
        BoxEdge.Bottom => m switch { BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top, _ => m },
        BoxEdge.Right => m switch
        {
            BoxEdge.Top => BoxEdge.Right, BoxEdge.Right => BoxEdge.Bottom,
            BoxEdge.Bottom => BoxEdge.Left, _ => BoxEdge.Top,
        },
        _ => m switch                                                   // Left
        {
            BoxEdge.Top => BoxEdge.Left, BoxEdge.Left => BoxEdge.Bottom,
            BoxEdge.Bottom => BoxEdge.Right, _ => BoxEdge.Top,
        },
    };
}
