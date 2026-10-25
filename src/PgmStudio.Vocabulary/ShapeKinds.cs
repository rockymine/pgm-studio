namespace PgmStudio.Vocabulary;

/// <summary>
/// What a sketch shape is drawn as. The word decides which of a shape's fields mean anything: a rectangle
/// reads its bounds, a circle its centre and radius, and the three vertex kinds their <c>vertices</c>. The
/// solids read the footprint of the kind they are drawn as and stand in the block grid.
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

    /// <summary>A cuboid over a rectangle's bounds: <c>base_height</c> tall, turned by <c>turn</c>, tilted by
    /// <c>tilt</c>, bedded <c>sunk</c> blocks under its <c>floor</c>.</summary>
    public const string Box = "box";

    /// <summary>A box cut by a plane falling from its full height at the near end to nothing at the far end;
    /// the fields of a <see cref="Box"/>.</summary>
    public const string Wedge = "wedge";

    /// <summary>An upright cylinder over a circle's centre and radius, <c>base_height</c> tall.</summary>
    public const string Cylinder = "cylinder";

    /// <summary>A sphere over a circle's centre and radius, standing on its <c>floor</c>.</summary>
    public const string Sphere = "sphere";

    /// <summary>A ring over a circle's centre and radius, which is its outer edge, with a tube of radius
    /// <c>tube</c>. It lies flat until <c>tilt</c> stands it on its edge.</summary>
    public const string Torus = "torus";

    /// <summary>A natural arch along an open chain of <c>vertices</c> and a half-width <c>radius</c>, its
    /// walkable deck at <c>deck</c> and the rock under it <c>thick</c>.</summary>
    public const string Arch = "arch";

    /// <summary>The five a canvas draws by tracing outline, in the order a picker offers them.</summary>
    public static readonly string[] Drawn = [Rectangle, Circle, Polygon, Lasso, Polyline];

    /// <summary>The six that stand in the block grid, in the order a picker offers them.</summary>
    public static readonly string[] Solids = [Box, Wedge, Cylinder, Sphere, Torus, Arch];

    /// <summary>Every kind a shape may name.</summary>
    public static readonly string[] All = [.. Drawn, .. Solids];

    /// <summary>The kind of outline a shape is drawn with: a solid's footprint, or the kind itself. A canvas
    /// draws, groups and hit-tests a solid as the footprint it reads.</summary>
    public static string Footprint(string kind) => kind switch
    {
        Box or Wedge => Rectangle,
        Cylinder or Sphere or Torus => Circle,
        Arch => Polyline,
        _ => kind,
    };
}
