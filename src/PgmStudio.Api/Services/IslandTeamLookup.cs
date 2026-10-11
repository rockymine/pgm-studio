using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

/// <summary>
/// Whose island a column is, on a map read from a world: the island <c>islands_json</c> holds under the column,
/// and the team the intent's <c>islandTeams</c> gives it. The same reading Configure's steps make in the browser
/// (<c>AuthoringContext.IslandTeamAt</c>), so a check on the server and a step in Configure agree on it.
/// </summary>
public static class IslandTeamLookup
{
    /// <summary>The lookup for the map, or null where it has no islands or none is given a team: a sketch map, or
    /// a world map whose islands are not assigned yet. An excluded island is nobody's.</summary>
    public static async Task<Func<double, double, string?>?> OfAsync(
        FeatureData features, long mapId, MapIntent intent, CancellationToken ct)
    {
        if (intent.IslandTeams.Count == 0 || await features.IslandsAsync(mapId, ct) is not { } json) return null;
        var islands = SymmetrySupport.ParseIslands(json, await features.ExcludedIslandsAsync(mapId, ct))
            .Where(island => intent.IslandTeams.ContainsKey(island.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Select(island => (Team: intent.IslandTeams[island.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                               Ring: island.Exterior.Select(point => (IReadOnlyList<double>)[point.x, point.z]).ToList(),
                               island.Bounds))
            .ToList();
        return (x, z) => islands.FirstOrDefault(island => island.Ring.Count >= 3
                ? Polygon.PointInRing(x, z, island.Ring)
                : x >= island.Bounds[0] && x <= island.Bounds[2] && z >= island.Bounds[1] && z <= island.Bounds[3]).Team;
    }
}
