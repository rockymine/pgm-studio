using System.Text.Json;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>Shared helpers for the plan tests: locating the <c>tools/seeds</c> corpus, loading the
/// checked-in seed pairs, and normalising a polygon ring so it compares winding- and start-independent.</summary>
internal static class PlanTestSupport
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Walk up from the test binary to the repo's <c>tools/seeds</c> directory.</summary>
    public static string SeedDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("tools/seeds not found above the test binary");
    }

    public static string ReadSeed(string file) => File.ReadAllText(Path.Combine(SeedDir(), file));

    public static MapIntent LoadIntent(string file) =>
        JsonSerializer.Deserialize<MapIntent>(ReadSeed(file), Web)!;

    public static SketchLayout LoadLayout(string file) => SketchLayout.Parse(ReadSeed(file))!;

    /// <summary>Canonicalise a ring (list of <c>[x,z]</c>) to a CCW cycle starting at its smallest vertex, so
    /// two rings describing the same polygon compare equal regardless of start index or winding.</summary>
    public static string NormRing(IReadOnlyList<double[]> ring)
    {
        var pts = ring.Select(v => ((int)Math.Round(v[0]), (int)Math.Round(v[1]))).ToList();
        if (pts.Count == 0) return "";
        double area = 0;
        for (int i = 0, n = pts.Count; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            area += (double)p.Item1 * q.Item2 - (double)q.Item1 * p.Item2;
        }
        if (area < 0) pts.Reverse();
        var start = pts.IndexOf(pts.Min());
        return string.Join(";", Enumerable.Range(0, pts.Count).Select(i => pts[(start + i) % pts.Count]));
    }

    /// <summary>The normalised ring for every shape in a layout, as a set of strings.</summary>
    public static HashSet<string> ShapeRings(SketchLayout layout) =>
        (layout.Layout?.Shapes ?? []).Select(s => NormRing(s.Vertices ?? [])).ToHashSet();

    /// <summary>Map each shape's normalised ring to its base height, for structural comparison.</summary>
    public static Dictionary<string, double> RingHeights(SketchLayout layout) =>
        (layout.Layout?.Shapes ?? []).ToDictionary(s => NormRing(s.Vertices ?? []), s => s.BaseHeight ?? 0);

    public static (double X, double Y, double Z) T(Pt p) => (Math.Round(p.X), Math.Round(p.Y), Math.Round(p.Z));

    public static (double, double, double, double) R(Rect r) => (r.MinX, r.MinZ, r.MaxX, r.MaxZ);
}
