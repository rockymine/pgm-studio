using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Which of a map's places a column of ground falls in: inside a box or a ring, or within <see cref="Reach"/>
/// blocks of a point or a line; the last place listed first, as the canvas draws it on top. An area is in the
/// place that holds the most of its columns.
/// </summary>
public static class PlaceLookup
{
    /// <summary>How near a point or a line a column is to fall in it, in blocks.</summary>
    public const double Reach = 2;

    /// <summary>The id of the place the column (<paramref name="x"/>, <paramref name="z"/>) falls in, or null.</summary>
    public static string? At(IReadOnlyList<Place> places, double x, double z)
    {
        for (var at = places.Count - 1; at >= 0; at--)
            if (Holds(places[at], x + 0.5, z + 0.5)) return places[at].Id;
        return null;
    }

    /// <summary>The id of the place holding the most of <paramref name="columns"/>, or null where none holds any.</summary>
    public static string? Of(IReadOnlyList<Place> places, IEnumerable<(double X, double Z)> columns) =>
        columns.Select(column => At(places, column.X, column.Z)).OfType<string>()
            .GroupBy(id => id).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();

    private static bool Holds(Place place, double x, double z)
    {
        var points = place.Points ?? [];
        switch (place.Shape)
        {
            case PlaceShapes.Point when points.Length == 1:
                return Distance(x - points[0][0], z - points[0][1]) <= Reach;
            case PlaceShapes.Box when points.Length == 2:
                return x >= Math.Min(points[0][0], points[1][0]) && x <= Math.Max(points[0][0], points[1][0])
                       && z >= Math.Min(points[0][1], points[1][1]) && z <= Math.Max(points[0][1], points[1][1]);
            case PlaceShapes.Ring when points.Length >= 3:
                var inside = false;
                for (int j = 0, k = points.Length - 1; j < points.Length; k = j++)
                    if ((points[j][1] > z) != (points[k][1] > z)
                        && x < (points[k][0] - points[j][0]) * (z - points[j][1]) / (points[k][1] - points[j][1]) + points[j][0])
                        inside = !inside;
                return inside;
            case PlaceShapes.Line:
                for (var j = 1; j < points.Length; j++)
                    if (Near(points[j - 1], points[j], x, z)) return true;
                return false;
            default:
                return false;
        }
    }

    private static double Distance(double dx, double dz) => Math.Sqrt(dx * dx + dz * dz);

    private static bool Near(double[] a, double[] b, double x, double z)
    {
        double dx = b[0] - a[0], dz = b[1] - a[1], length = dx * dx + dz * dz;
        var t = length == 0 ? 0 : Math.Clamp(((x - a[0]) * dx + (z - a[1]) * dz) / length, 0, 1);
        return Distance(x - (a[0] + t * dx), z - (a[1] + t * dz)) <= Reach;
    }
}
