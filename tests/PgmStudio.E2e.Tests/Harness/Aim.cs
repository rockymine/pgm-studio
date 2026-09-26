using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>Turning a world block into the client point to click on the canvas that shows it.</summary>
public static partial class Aim
{
    /// <summary>
    /// World points a click stands a good chance of landing inside <paramref name="shape"/>, best first. A
    /// shape's middle is not always inside it — a board fused across its own symmetry is a dumbbell whose
    /// centroid sits in the gap — so a rectangle offers its centre and a polygon the centroid of each run of
    /// three consecutive vertices, which is inside the outline wherever that corner is convex.
    /// </summary>
    public static IReadOnlyList<(double X, double Z)> ShapePoints(JsonNode shape, int limit = 8)
    {
        if (shape["min_x"] is { } minX && shape["max_x"] is { } maxX)
        {
            return [((minX.GetValue<double>() + maxX.GetValue<double>()) / 2,
                     (shape["min_z"]!.GetValue<double>() + shape["max_z"]!.GetValue<double>()) / 2)];
        }

        var vertices = shape["vertices"]?.AsArray()
            .Select(vertex => (X: vertex![0]!.GetValue<double>(), Z: vertex[1]!.GetValue<double>()))
            .ToList() ?? [];
        if (vertices.Count < 3) return [];
        var points = new List<(double X, double Z)>();
        for (var i = 0; i < vertices.Count && points.Count < limit; i++)
        {
            var first = vertices[i];
            var second = vertices[(i + 1) % vertices.Count];
            var third = vertices[(i + 2) % vertices.Count];
            points.Add(((first.X + second.X + third.X) / 3, (first.Z + second.Z + third.Z) / 3));
        }
        return points;
    }

    /// <summary>
    /// A world → client mapper for the canvas as it currently sits, solved rather than guessed: two probes of
    /// the cursor readout fix the scale and the offset. Null when the readout says nothing (no canvas, or a
    /// view that reports no world position).
    /// </summary>
    public static async Task<Func<double, double, (double X, double Y)>?> WorldAimerAsync(StudioPage page)
    {
        var box = await page.Locator("canvas").First().BoundingBoxAsync();
        if (box is not { } canvas) return null;

        var cursor = page.Locator(".canvas-readout .canvas-readout-item").First();
        async Task<(double X, double Z)?> Probe(double clientX, double clientY)
        {
            await page.Mouse.MoveAsync((decimal)clientX, (decimal)clientY);
            await StudioPage.Pause(120);
            var text = await cursor.EvaluateAsync<string>("el => el.innerText");
            var match = Readout().Match(text);
            return match.Success
                ? (double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                   double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
                : null;
        }

        var first = (X: canvas.X + canvas.Width * 0.25, Y: canvas.Y + canvas.Height * 0.25);
        var second = (X: canvas.X + canvas.Width * 0.75, Y: canvas.Y + canvas.Height * 0.75);
        var atFirst = await Probe(first.X, first.Y);
        var atSecond = await Probe(second.X, second.Y);
        if (atFirst is not { } worldFirst || atSecond is not { } worldSecond
            || worldSecond.X == worldFirst.X || worldSecond.Z == worldFirst.Z)
        {
            return null;
        }

        var pixelsPerBlockX = (second.X - first.X) / (worldSecond.X - worldFirst.X);
        var pixelsPerBlockZ = (second.Y - first.Y) / (worldSecond.Z - worldFirst.Z);
        return (worldX, worldZ) => (first.X + (worldX - worldFirst.X) * pixelsPerBlockX,
                                    first.Y + (worldZ - worldFirst.Z) * pixelsPerBlockZ);
    }

    [GeneratedRegex(@"X\s*(-?[\d.]+)\s*Z\s*(-?[\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Readout();
}
