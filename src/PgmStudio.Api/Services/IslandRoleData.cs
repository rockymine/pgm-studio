using System.Text.Json;
using NetTopologySuite.Geometries;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Shared island-roles plumbing for the <c>island-health</c> and <c>island-roles</c> endpoints: parse the
/// stored <c>islands_json</c> into footprints, and derive the objective anchors + buildable region from the
/// map doc (reusing <see cref="IslandRoleClassifier"/> + <see cref="RegionCategorizer"/>).
/// </summary>
internal static class IslandRoleData
{
    private static readonly GeometryFactory Gf = new();

    /// <summary>islands_json (<c>IslandDetector.SerializeJson</c>): <c>[{block_count, polygon:{coordinates:[[[x,z],…],…]}}]</c>,
    /// in detection (island-sketch) order. Malformed/missing → empty.</summary>
    public static List<(int Blocks, Geometry Geom)> ParseIslands(byte[]? data)
    {
        var result = new List<(int, Geometry)>();
        if (data is null || data.Length == 0) return result;
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (!e.TryGetProperty("block_count", out var bc)) continue;
                var rings = e.GetProperty("polygon").GetProperty("coordinates");
                if (rings.GetArrayLength() == 0) continue;
                var shell = Ring(rings[0]);
                var holes = Enumerable.Range(1, rings.GetArrayLength() - 1).Select(i => Ring(rings[i])).ToArray();
                result.Add((bc.GetInt32(), Gf.CreatePolygon(Gf.CreateLinearRing(shell), holes.Select(Gf.CreateLinearRing).ToArray())));
            }
        }
        catch { /* malformed islands_json → no geometry */ }
        return result;
    }

    /// <summary>Objective anchors (spawn + wool, never the monument) and the union of the declared build
    /// regions, in the islands' combined extent (enough for the concrete spawn/wool regions and the
    /// void-spanning build complement). Caller ensures <paramref name="islands"/> is non-empty.</summary>
    public static (List<IslandRoleClassifier.Anchor> Anchors, Geometry? BuildRegion) Context(
        Dict doc, IReadOnlyList<Geometry> islands)
    {
        var env = new Envelope();
        foreach (var g in islands) env.ExpandToInclude(g.EnvelopeInternal);
        var bounds = (env.MinX, env.MinY, env.MaxX, env.MaxY);

        var buildIds = RegionCategorizer.Categorize(doc).Where(kv => kv.Value == "build").Select(kv => kv.Key);
        return (IslandRoleClassifier.ExtractAnchors(doc, bounds),
                IslandRoleClassifier.BuildRegion(doc, buildIds, bounds));
    }

    private static Coordinate[] Ring(JsonElement ring) =>
        ring.EnumerateArray().Select(p => new Coordinate(p[0].GetDouble(), p[1].GetDouble())).ToArray();
}
