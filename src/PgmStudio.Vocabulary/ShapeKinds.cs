namespace PgmStudio.Vocabulary;

/// <summary>
/// What a sketch shape is drawn as. The word decides which of a shape's fields mean anything: a rectangle
/// reads its bounds, a circle its centre and radius, and the three vertex kinds their <c>vertices</c>.
///
/// <para>Three parties spell them. The gate judges a shape's kind against this set and reports an unknown one
/// as <c>SK3</c>; the rasterizer switches on it to build a ring; and the canvas reads it to pick an icon and
/// to know whether a shape's points may be edited. A kind nothing here names draws no ground at all, so a
/// fourth spelling is a board that quietly loses a shape.</para>
/// </summary>
public static class ShapeKinds
{
    /// <summary>Bounds: <c>min_x</c>, <c>min_z</c>, <c>max_x</c>, <c>max_z</c>.</summary>
    public const string Rectangle = "rectangle";

    /// <summary>A centre and a radius, rasterized as a 64-gon.</summary>
    public const string Circle = "circle";

    /// <summary>A closed ring of <c>vertices</c>, with optional Bézier <c>controls</c> per vertex.</summary>
    public const string Polygon = "polygon";

    /// <summary>A closed ring traced freehand, simplified on release. A polygon by the time it is stored.</summary>
    public const string Lasso = "lasso";

    /// <summary>An <b>open</b> chain of <c>vertices</c> and a <c>radius</c>: the band around the splined
    /// centreline is the footprint, and <c>stroke_edge</c> says how its two long edges vary along it.</summary>
    public const string Polyline = "polyline";

    /// <summary>The five, in the order a picker offers them.</summary>
    public static readonly string[] All = [Rectangle, Circle, Polygon, Lasso, Polyline];
}
