using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The shapes that stand in the block grid — box, wedge, cylinder, sphere, torus and arch — read off a
/// <see cref="SketchShape"/>: the body each describes, the runs of blocks a column meets of it, the outline and
/// bounds the rest of the sketch measures, and the image of one under a symmetry axis.
///
/// <para>A solid reads the footprint of the kind it is drawn as — a box and a wedge a rectangle's bounds, a
/// cylinder, sphere and torus a circle's centre and radius, an arch a polyline's vertices and radius — and its
/// height from <c>floor</c> and <c>base_height</c>, so the drawing tools place one with the gesture that places
/// the footprint.</para>
/// </summary>
internal static class SketchSolids
{
    /// <summary>Whether the shape stands in the block grid.</summary>
    public static bool Is(SketchShape shape) => shape.Type is { } kind && ShapeKinds.Solids.Contains(kind);

    /// <summary>The body a box, wedge, cylinder, sphere or torus describes, or null for any other shape.</summary>
    public static Solid? BodyOf(SketchShape shape)
    {
        var height = shape.BaseHeight ?? 1;
        var radius = Math.Abs(shape.Radius ?? 0);
        var floor = shape.Floor ?? 0;
        double turn = shape.Turn ?? 0, tilt = shape.Tilt ?? 0, sunk = shape.Sunk ?? 0;
        switch (shape.Type)
        {
            case ShapeKinds.Box or ShapeKinds.Wedge:
            {
                var (centreX, centreZ, width, depth) = Footprint(shape);
                var form = shape.Type == ShapeKinds.Box ? SolidForm.Box : SolidForm.Wedge;
                return new Solid(form, centreX, centreZ, width, height, depth, turn, tilt, floor, sunk);
            }
            case ShapeKinds.Cylinder:
                return new Solid(SolidForm.Cylinder, shape.CenterX ?? 0, shape.CenterZ ?? 0, radius * 2, height, radius * 2,
                                 turn, tilt, floor, sunk);
            case ShapeKinds.Sphere:
                return new Solid(SolidForm.Sphere, shape.CenterX ?? 0, shape.CenterZ ?? 0, radius * 2, radius * 2, radius * 2,
                                 turn, tilt, floor, sunk);
            case ShapeKinds.Torus:
                return new Solid(SolidForm.Torus, shape.CenterX ?? 0, shape.CenterZ ?? 0, radius * 2, radius * 2, radius * 2,
                                 turn, tilt, floor, sunk, shape.Tube ?? Math.Max(1, radius / 4));
            default:
                return null;
        }
    }

    /// <summary>The arch an <c>arch</c> shape describes, or null for any other shape.</summary>
    public static NaturalArch? ArchOf(SketchShape shape) => shape.Type != ShapeKinds.Arch ? null
        : NaturalArch.Of(Math.Abs(shape.Radius ?? 0), shape.Deck, shape.Thick, shape.Camber ?? 0,
                         (shape.Floor ?? 0) + (shape.BaseHeight ?? 1));

    /// <summary>Every run of blocks the shape makes, lowest first in a column, cut at the layer's own floor:
    /// nothing below 0 is built, and a run that ends there is dropped.</summary>
    public static IEnumerable<(int X, int Z, int Floor, int Top)> Runs(SketchShape shape)
    {
        if (BodyOf(shape) is { } body)
        {
            foreach (var run in body.Columns())
                if (Clipped(run) is { } kept) yield return kept;
        }
        else if (ArchOf(shape) is { } arch && ArchCentreline(shape) is { Count: >= 2 } centreline)
        {
            foreach (var (x, z) in SketchRasterizer.CellsOfRing(ArchRing(shape)))
            {
                var hit = Polyline.Nearest(centreline, x + 0.5, z + 0.5);
                var (floor, top) = arch.Run(hit.Along, hit.Distance);
                if (Clipped((x, z, floor, top)) is { } kept) yield return kept;
            }
        }
    }

    /// <summary>The top run of each column the shape meets: its span in the layer.</summary>
    public static IEnumerable<(int X, int Z, int Floor, int Top)> Surface(SketchShape shape) => ByColumn(shape).Select(column => column[^1]);

    // Runs arrive column by column, lowest first, so a column is the stretch of runs sharing a cell.
    private static IEnumerable<List<(int X, int Z, int Floor, int Top)>> ByColumn(SketchShape shape)
    {
        List<(int X, int Z, int Floor, int Top)> column = [];
        foreach (var run in Runs(shape))
        {
            if (column.Count > 0 && (column[0].X != run.X || column[0].Z != run.Z))
            {
                yield return column;
                column = [];
            }
            column.Add(run);
        }
        if (column.Count > 0) yield return column;
    }

    private static (int X, int Z, int Floor, int Top)? Clipped((int X, int Z, int Floor, int Top) run)
        => run.Top <= 0 ? null : (run.X, run.Z, Math.Max(0, run.Floor), run.Top);

    private static List<double[]> ArchCentreline(SketchShape shape) => Centerline.Of(shape.Vertices ?? []);

    private static List<double[]> ArchRing(SketchShape shape)
        => StrokeOutline.Ring(shape.Vertices ?? [], Math.Abs(shape.Radius ?? 0), StrokeEdge.Solid, 0);

    /// <summary>The outline of the shape's footprint on the plan: the plan box of a body, the band of an arch.</summary>
    public static List<double[]> Ring(SketchShape shape)
    {
        if (BodyOf(shape) is { IsEmpty: false } body)
        {
            var (minX, minZ, maxX, maxZ) = body.PlanBounds();
            return [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]];
        }
        return shape.Type == ShapeKinds.Arch ? ArchRing(shape) : [];
    }

    /// <summary>The plan box the shape fits in, or null where it has no volume.</summary>
    public static (double MinX, double MinZ, double MaxX, double MaxZ)? Bounds(SketchShape shape)
    {
        if (BodyOf(shape) is { IsEmpty: false } body) return body.PlanBounds();
        if (shape.Type != ShapeKinds.Arch || shape.Vertices is not { Length: > 0 } vertices) return null;
        var reach = Math.Abs(shape.Radius ?? 0);
        return (vertices.Min(point => point[0]) - reach, vertices.Min(point => point[1]) - reach,
                vertices.Max(point => point[0]) + reach, vertices.Max(point => point[1]) + reach);
    }

    /// <summary>The highest course the shape builds to: where its top stands, one over the highest block.</summary>
    public static double Top(SketchShape shape)
        => BodyOf(shape) is { IsEmpty: false } body
            ? body.Highest
            : Runs(shape).Select(run => (double)run.Top).DefaultIfEmpty(0).Max();

    /// <summary>The shape's image under <paramref name="axis"/> about (<paramref name="cx"/>, <paramref name="cz"/>).
    /// Every form is symmetric across its own width axis, so an image is the same form at the mirrored
    /// centre with its depth axis carried to where the symmetry takes the original's: the turn is read off that
    /// direction and the tilt, which rises towards it, is unchanged.</summary>
    public static SketchShape Mirror(SketchShape shape, string axis, double cx, double cz)
    {
        var image = Copy(shape);
        if (shape.Type == ShapeKinds.Arch)
        {
            image.Vertices = [.. (shape.Vertices ?? []).Select(point =>
            {
                var (x, z) = Symmetry.Apply(point[0], point[1], axis, cx, cz);
                return new[] { x, z };
            })];
            return image;
        }

        var radians = (shape.Turn ?? 0) * Math.PI / 180;
        var (depthX, depthZ) = Symmetry.Apply(-Math.Sin(radians), Math.Cos(radians), axis, 0, 0);
        var turn = Math.Round(Math.Atan2(-depthX, depthZ) * 180 / Math.PI, 9);
        image.Turn = turn < 0 ? turn + 360 : turn;

        if (shape.Type is ShapeKinds.Box or ShapeKinds.Wedge)
        {
            var (centreX, centreZ, width, depth) = Footprint(shape);
            var (imageX, imageZ) = Symmetry.Apply(centreX, centreZ, axis, cx, cz);
            image.MinX = imageX - width / 2; image.MaxX = imageX + width / 2;
            image.MinZ = imageZ - depth / 2; image.MaxZ = imageZ + depth / 2;
        }
        else
        {
            (image.CenterX, image.CenterZ) = Symmetry.Apply(shape.CenterX ?? 0, shape.CenterZ ?? 0, axis, cx, cz);
        }
        return image;
    }

    // A rectangle's middle and its two extents, whichever way round its corners are stated.
    private static (double CentreX, double CentreZ, double Width, double Depth) Footprint(SketchShape shape)
    {
        double minX = Math.Min(shape.MinX ?? 0, shape.MaxX ?? 0), maxX = Math.Max(shape.MinX ?? 0, shape.MaxX ?? 0);
        double minZ = Math.Min(shape.MinZ ?? 0, shape.MaxZ ?? 0), maxZ = Math.Max(shape.MinZ ?? 0, shape.MaxZ ?? 0);
        return ((minX + maxX) / 2, (minZ + maxZ) / 2, maxX - minX, maxZ - minZ);
    }

    private static SketchShape Copy(SketchShape shape) => new()
    {
        Id = shape.Id, Type = shape.Type, Operation = shape.Operation, Override = shape.Override,
        MinX = shape.MinX, MinZ = shape.MinZ, MaxX = shape.MaxX, MaxZ = shape.MaxZ,
        CenterX = shape.CenterX, CenterZ = shape.CenterZ, Radius = shape.Radius, Vertices = shape.Vertices,
        BaseHeight = shape.BaseHeight, Floor = shape.Floor, ReliefScope = shape.ReliefScope,
        Theme = shape.Theme, Material = shape.Material, Turn = shape.Turn, Tilt = shape.Tilt, Sunk = shape.Sunk,
        Tube = shape.Tube, Deck = shape.Deck, Thick = shape.Thick, Camber = shape.Camber,
    };
}
