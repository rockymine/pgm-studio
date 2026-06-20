using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Simplifies a real island's outline into an editable <see cref="SketchLayout"/>: the <b>Douglas-Peucker
/// simplified exterior</b> (a right-angle map collapses to a handful of nodes) as one "add" polygon, plus
/// each <b>simplified interior ring</b> as a "subtract" hole. This is only simplification — the faithful
/// outline of the real island. Cutting the body into lanes is later work, built forward from this shape.
/// </summary>
public static class IslandSimplifier
{
    public sealed record Result(SketchLayout Layout, int ExteriorVertices, int Holes);

    /// <summary>Simplify one island outline (exterior + holes) into a single-island sketch layout.</summary>
    public static Result Simplify(IReadOnlyList<double[]> exterior,
        IReadOnlyList<IReadOnlyList<double[]>>? holes = null, double tolerance = 2.0)
    {
        holes ??= [];
        if (exterior.Count < 3)
            return new Result(new SketchLayout
            {
                Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter() },
                Layout = new SketchShapes(),
            }, 0, 0);

        var simp = PolygonSimplify.Simplify(exterior, holes, tolerance, minHoleArea: 8);
        var shapes = new List<SketchShape> { Poly("island", simp.Exterior, "add") };
        var hid = 0;
        foreach (var h in simp.Holes)
            shapes.Add(Poly($"hole{hid++}", h, "subtract"));

        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var p in simp.Exterior)
        {
            minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]);
            minZ = Math.Min(minZ, p[1]); maxZ = Math.Max(maxZ, p[1]);
        }

        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = (minX + maxX) / 2, Cz = (minZ + maxZ) / 2 } },
            Layout = new SketchShapes
            {
                Shapes = shapes,
                Islands = [new SketchIsland { Id = "island", Name = "Island", Mirrors = false, ShapeIds = [.. shapes.Select(s => s.Id)] }],
            },
        };
        return new Result(layout, simp.Exterior.Count, hid);
    }

    /// <summary>Simplify every island of a map into one editable <see cref="SketchLayout"/>: each island's
    /// outline + holes (shape ids prefixed by the island id), mirror "none" (all islands are present).</summary>
    public static SketchLayout SimplifyMap(
        IReadOnlyList<(string Id, IReadOnlyList<double[]> Exterior, IReadOnlyList<IReadOnlyList<double[]>> Holes)> islands,
        double tolerance = 2.0)
    {
        var shapes = new List<SketchShape>();
        var groups = new List<SketchIsland>();
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var (id, ext, holes) in islands)
        {
            var islandShapes = Simplify(ext, holes, tolerance).Layout.Layout!.Shapes;
            if (islandShapes.Count == 0) continue;
            foreach (var s in islandShapes)
            {
                s.Id = $"i{id}_{s.Id}";
                shapes.Add(s);
                foreach (var v in s.Vertices ?? []) { minX = Math.Min(minX, v[0]); maxX = Math.Max(maxX, v[0]); minZ = Math.Min(minZ, v[1]); maxZ = Math.Max(maxZ, v[1]); }
            }
            groups.Add(new SketchIsland { Id = $"i{id}", Name = $"Island {id}", Mirrors = false, ShapeIds = [.. islandShapes.Select(s => s.Id)] });
        }
        return new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = shapes.Count > 0 ? (minX + maxX) / 2 : 0, Cz = shapes.Count > 0 ? (minZ + maxZ) / 2 : 0 } },
            Layout = new SketchShapes { Shapes = shapes, Islands = groups },
        };
    }

    private static SketchShape Poly(string id, IReadOnlyList<double[]> ring, string op) =>
        new() { Id = id, Type = "polygon", Operation = op, Vertices = [.. ring] };
}
