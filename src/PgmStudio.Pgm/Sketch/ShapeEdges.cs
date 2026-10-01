using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// A shape's outline as the edges a point edit names: each edge by the vertex it leaves, its two corners, its
/// length, and what its layer covers half a block outside it — another shape, an image of one that the board's
/// symmetry draws, or void. The read a caller takes before stating a pull or naming a bend's edges, so the coast
/// and the seams are found by reading the board rather than by copying its ring and counting.
/// </summary>
public static class ShapeEdges
{
    /// <summary>The edges of the shape at <paramref name="shapeId"/>, as text, or null where no shape carries that
    /// id.</summary>
    public static string? Text(SketchLayout? layout, string shapeId)
    {
        var layer = SketchLayout.Stack(layout).FirstOrDefault(drawn => drawn.Shapes.Any(shape => shape.Id == shapeId));
        if (layer?.Shapes.First(shape => shape.Id == shapeId) is not { } shape) return null;

        var mode = SketchLayout.MirrorModeOf(layout);
        var (centreX, centreZ) = (layout?.Setup?.Center?.Cx ?? 0, layout?.Setup?.Center?.Cz ?? 0);
        var fanned = layer.Groups.Where(group => group.Mirrors).SelectMany(group => group.ShapeIds).ToHashSet();
        var group = layer.Groups.FirstOrDefault(listed => listed.ShapeIds.Contains(shapeId));

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"EDGES  {shapeId}  layer {layer.Id}");
        if (group is not null) text.Append(CultureInfo.InvariantCulture, $", group {group.Id}");
        text.Append(group is { Mirrors: true } && Symmetry.Order(mode) > 1
            ? FormattableString.Invariant($", drawn again by {mode} about ({Number(centreX)}, {Number(centreZ)})\n")
            : "\n");

        if (shape.Vertices is not { Length: >= 3 } vertices || shape.Type is not (ShapeKinds.Polygon or ShapeKinds.Lasso))
        {
            text.Append(CultureInfo.InvariantCulture,
                $"a {shape.Type} states no outline of its own, so a point edit, a pull or a bend is refused; state it as a polygon to edit it\n");
            return text.ToString();
        }

        // What the layer covers: every add shape but this one, and every image the symmetry draws of a fanned
        // shape, this one's own included.
        var covers = new List<(string Name, List<double[]> Ring)>();
        foreach (var other in layer.Shapes.Where(other => other.Operation != "subtract"))
        {
            var ring = SketchRasterizer.RingOf(other);
            if (ring.Count < 3) continue;
            if (other.Id != shapeId) covers.Add((other.Id, ring));
            if (!fanned.Contains(other.Id)) continue;
            for (var k = 1; k < Symmetry.Order(mode); k++)
                covers.Add(($"{other.Id} (image)", [.. ring.Select(point =>
                {
                    var (x, z) = Symmetry.Point(point[0], point[1], mode, centreX, centreZ, k);
                    return new[] { x, z };
                })]));
        }

        var own = vertices.Select(point => new[] { point[0], point[1] }).ToList();
        text.Append(CultureInfo.InvariantCulture,
            $"{own.Count} corners. An edge is named by the vertex it leaves; across is what this layer covers half a block outside it.\n");
        text.Append("edge  from              to                length  across\n");
        for (var edge = 0; edge < own.Count; edge++)
        {
            var (from, to) = (own[edge], own[(edge + 1) % own.Count]);
            double alongX = to[0] - from[0], alongZ = to[1] - from[1];
            var length = Math.Sqrt(alongX * alongX + alongZ * alongZ);
            var curved = shape.Controls is { } controls
                         && (controls.GetValueOrDefault(edge.ToString(CultureInfo.InvariantCulture))?.Out is not null
                             || controls.GetValueOrDefault(((edge + 1) % own.Count).ToString(CultureInfo.InvariantCulture))?.In is not null);
            text.Append(CultureInfo.InvariantCulture,
                $"{edge,4}  {Corner(from),-16}  {Corner(to),-16}  {length,6:0.0}{(curved ? "~" : " ")} {Across(own, from, to, length, covers)}\n");
        }
        if (shape.Controls is { Count: > 0 }) text.Append("~ a handle curves the edge; its index is still the vertex it leaves\n");
        return text.ToString();
    }

    // What stands half a block outside the edge, a block at a time, run together where it does not change.
    private static string Across(List<double[]> own, double[] from, double[] to, double length,
                                 List<(string Name, List<double[]> Ring)> covers)
    {
        if (length < 1e-9) return "a corner drawn twice";
        double acrossX = -(to[1] - from[1]) / length, acrossZ = (to[0] - from[0]) / length;
        double midX = (from[0] + to[0]) / 2, midZ = (from[1] + to[1]) / 2;
        if (Polygon.PointInRing(midX + acrossX * 0.5, midZ + acrossZ * 0.5, own)) (acrossX, acrossZ) = (-acrossX, -acrossZ);

        var steps = Math.Max(1, (int)Math.Ceiling(length));
        var runs = new List<(string What, double From, double To)>();
        for (var step = 0; step < steps; step++)
        {
            var at = (step + 0.5) / steps;
            double x = from[0] + (to[0] - from[0]) * at + acrossX * 0.5, z = from[1] + (to[1] - from[1]) * at + acrossZ * 0.5;
            var what = covers.FirstOrDefault(cover => Polygon.PointInRing(x, z, cover.Ring)).Name ?? "void";
            if (runs.Count > 0 && runs[^1].What == what) runs[^1] = runs[^1] with { To = (step + 1.0) / steps };
            else runs.Add((what, (double)step / steps, (step + 1.0) / steps));
        }
        return runs.Count == 1
            ? runs[0].What
            : string.Join(" · ", runs.Select(run => FormattableString.Invariant($"{run.What} {run.From:0.##}–{run.To:0.##}")));
    }

    private static string Corner(double[] point) => $"({Number(point[0])}, {Number(point[1])})";

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
