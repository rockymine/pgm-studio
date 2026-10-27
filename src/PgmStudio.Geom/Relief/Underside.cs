using System.Text.Json.Serialization;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Relief;

/// <summary>How far the rock under a floating layer hangs below its top: <see cref="Edge"/> blocks at the rim and
/// <see cref="Taper"/> more per block inward, so an island narrows to a keel instead of ending in a flat slab.</summary>
public sealed record UndersideSpec
{
    /// <summary>Blocks of rock under the top at the rim: a cell on the footprint's edge hangs this far.</summary>
    [JsonPropertyName("edge")]  public double Edge { get; init; } = 4;

    /// <summary>Blocks of extra depth per block inward from the rim.</summary>
    [JsonPropertyName("taper")] public double Taper { get; init; } = 0.9;

    /// <summary>The amplitude, in blocks, of the noise added to the depth; 0 leaves the underside smooth.</summary>
    [JsonPropertyName("rough")] public double Rough { get; init; }

    /// <summary>The noise row, so the underside is the same on every build.</summary>
    [JsonPropertyName("seed")]  public int Seed { get; init; }

    /// <summary>Ids of shapes on the layer whose outline beside open air is a sheer side: the cells nearest it
    /// hang <see cref="Underside.SheerEdge"/> blocks and <see cref="Underside.SheerTaper"/> more per block
    /// inward, a face that drops far before it narrows. Absent, no side is sheer.</summary>
    [JsonPropertyName("sheer")] public List<string>? Sheer { get; init; }
}

/// <summary>
/// Where the rock of a floating layer ends under each of its columns: the column's top less a depth that grows
/// with the straight distance from the footprint's edge, except beside a sheer side, where the depth starts at
/// <see cref="SheerEdge"/> and grows at <see cref="SheerTaper"/>. The shallower of the two applies, so a cell
/// near both reads the nearer.
/// </summary>
public static class Underside
{
    /// <summary>Blocks of rock under the top at a sheer side's rim.</summary>
    public const double SheerEdge = 26;

    /// <summary>Extra blocks of depth per block inward from a sheer side.</summary>
    public const double SheerTaper = 1.2;

    /// <summary>Blocks of air an underside may leave over the ground below before it rests on that ground
    /// instead.</summary>
    public const int Clear = 4;

    /// <summary>The width of the noise's features, in blocks.</summary>
    private const double NoiseCell = 12;

    private const int NoiseOctaves = 2;

    /// <summary>The lowest block course each column of <paramref name="tops"/> may stand on, never above the
    /// course under its top and never below course 1. <paramref name="tops"/> is each column's top (exclusive)
    /// and its keys are the footprint. <paramref name="sheerSide"/> are the cells outside that footprint that
    /// are the sheer side's air. The noise is read at each cell's canonical member under
    /// <paramref name="mirrorMode"/> about (<paramref name="centreX"/>, <paramref name="centreZ"/>), so a layer
    /// that is mirrored hangs the same on every image.</summary>
    public static Dictionary<(int X, int Z), int> Floors(
        IReadOnlyDictionary<(int X, int Z), int> tops, IReadOnlySet<(int X, int Z)> sheerSide,
        UndersideSpec spec, string? mirrorMode, double centreX, double centreZ)
    {
        var floors = new Dictionary<(int X, int Z), int>(tops.Count);
        if (tops.Count == 0) return floors;

        // The sources are the air cells that touch the footprint, which is where the nearest air always is;
        // the one-cell margin holds the ones off the bounding box.
        var minX = tops.Keys.Min(cell => cell.X) - 1;
        var minZ = tops.Keys.Min(cell => cell.Z) - 1;
        var width = tops.Keys.Max(cell => cell.X) - minX + 2;
        var depth = tops.Keys.Max(cell => cell.Z) - minZ + 2;
        var openAir = new bool[width * depth];
        var sheerAir = new bool[width * depth];
        for (var cell = 0; cell < openAir.Length; cell++)
        {
            var at = (X: minX + cell % width, Z: minZ + cell / width);
            if (tops.ContainsKey(at)) continue;
            var touches = tops.ContainsKey((at.X + 1, at.Z)) || tops.ContainsKey((at.X - 1, at.Z))
                          || tops.ContainsKey((at.X, at.Z + 1)) || tops.ContainsKey((at.X, at.Z - 1));
            if (!touches) continue;
            if (sheerSide.Contains(at)) sheerAir[cell] = true;
            else openAir[cell] = true;
        }
        var fromOpen = DistanceTransform.ToNearest(openAir, width, depth);
        var fromSheer = DistanceTransform.ToNearest(sheerAir, width, depth);

        foreach (var (cell, top) in tops)
        {
            var index = (cell.Z - minZ) * width + (cell.X - minX);
            var plain = Math.Max(0, spec.Edge) + Math.Max(0, spec.Taper) * (fromOpen[index] - 1);
            var thickness = Math.Min(plain, SheerEdge + SheerTaper * (fromSheer[index] - 1));
            if (spec.Rough > 0)
            {
                var (noiseX, noiseZ) = Symmetry.Canonical(cell.X + 0.5, cell.Z + 0.5, mirrorMode, centreX, centreZ);
                thickness += spec.Rough * ValueNoise.Fractal(noiseX, noiseZ, NoiseCell, NoiseOctaves, spec.Seed);
            }
            var hang = Math.Max(1, (int)Math.Round(thickness, MidpointRounding.AwayFromZero));
            floors[cell] = Math.Max(1, top - hang);
        }
        return floors;
    }

    /// <summary>Whether an underside whose lowest course is <paramref name="floor"/> comes within
    /// <see cref="Clear"/> blocks of ground whose top is <paramref name="groundTop"/>, and so rests on it.</summary>
    public static bool Rests(int floor, int groundTop) => floor - groundTop < Clear;
}
