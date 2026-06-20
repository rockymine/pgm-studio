using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Turns a freshly generated <see cref="SketchLayout"/> into one the sketch editor can show and edit. A
/// generator emits dense lane hulls (a curved or jittered lane is a ~100-point ring) in a board-corner
/// coordinate space (centre at <c>Width/2, Height/2</c>); the editor wants a handful of draggable vertices
/// per shape, around the symmetry origin, with the canvas framed to the board. This:
/// <list type="bullet">
/// <item>recentres every shape on the symmetry origin (the editor's convention — mirror centre at 0,0),</item>
/// <item>simplifies each polygon ring to its real bends (Douglas–Peucker), so a vertex is a handle, and</item>
/// <item>sets a square framing <c>bbox</c> so the canvas opens fitted to the board.</item>
/// </list>
/// The generator output is left untouched (headless rasterize/preview keep using it); this is the
/// presentation step on the way into the editor.
/// </summary>
public static class SketchLayoutPrep
{
    /// <summary>Recentre + simplify + frame, in place (and returned for chaining). <paramref name="tolerance"/>
    /// is the Douglas–Peucker deviation (blocks) used to thin each polygon ring; <paramref name="pad"/> is the
    /// empty margin kept around the board in the framing bbox.</summary>
    public static SketchLayout ForEditor(SketchLayout layout, double tolerance = 1.0, double pad = 8)
    {
        var setup = layout.Setup ??= new SketchSetup();
        double cx = setup.Center?.Cx ?? 0, cz = setup.Center?.Cz ?? 0;

        foreach (var s in layout.Layout?.Shapes ?? [])
        {
            Translate(s, -cx, -cz);
            if (s.Vertices is { Length: > 3 })
                s.Vertices = [.. PolygonSimplify.Ring(s.Vertices, tolerance)];
        }
        setup.Center = new SketchCenter { Cx = 0, Cz = 0 };

        var half = FrameHalf(layout, pad);
        setup.Bbox = new SketchBbox { MinX = -half, MaxX = half, MinZ = -half, MaxZ = half };
        return layout;
    }

    private static void Translate(SketchShape s, double dx, double dz)
    {
        if (s.Vertices is { } v) foreach (var p in v) { p[0] += dx; p[1] += dz; }
        if (s.MinX is not null) { s.MinX += dx; s.MaxX += dx; s.MinZ += dz; s.MaxZ += dz; }
        if (s.CenterX is not null) { s.CenterX += dx; s.CenterZ += dz; }
    }

    // Half-side of a square, origin-centred bbox that contains every shape plus a margin.
    private static double FrameHalf(SketchLayout layout, double pad)
    {
        double m = 0;
        foreach (var s in layout.Layout?.Shapes ?? [])
        {
            if (s.Vertices is { } v)
                foreach (var p in v) m = Math.Max(m, Math.Max(Math.Abs(p[0]), Math.Abs(p[1])));
            if (s.MinX is not null)
                m = Math.Max(m, Max4(s.MinX.Value, s.MaxX ?? 0, s.MinZ ?? 0, s.MaxZ ?? 0));
            if (s.CenterX is not null)
                m = Math.Max(m, Math.Max(Math.Abs(s.CenterX.Value), Math.Abs(s.CenterZ ?? 0)) + (s.Radius ?? 0));
        }
        return m + pad;
    }

    private static double Max4(double a, double b, double c, double d) =>
        Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), Math.Max(Math.Abs(c), Math.Abs(d)));
}
