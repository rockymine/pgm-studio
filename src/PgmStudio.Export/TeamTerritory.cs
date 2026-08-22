using PgmStudio.Analysis.Footprint;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export;

/// <summary>
/// Shared terrain-ownership decomposition for the team tint (docs/world-export/terrain-painting.md §3). It
/// splits a terrain footprint into the <b>canonical</b> islands (<see cref="IslandDetector"/> — the same
/// decomposition <c>islands_json</c> and the configure canvas use) and gives each island its owning team: a
/// stored <see cref="MapIntent.IslandTeams"/> entry when present (the plan-compile pre-fill, or the configure
/// step's manual assignment), else a spawn's team on the island, else a wool's owner, else <see cref="Neutral"/>.
/// Consumed by the plan-compile endpoint (to pre-fill ownership once, when the plan is created) and by
/// <see cref="WorldBuilder"/> (to paint), so the id space the painter reads is exactly the one the
/// configure UI writes — nothing re-derives on a different decomposition.
/// </summary>
public static class TeamTerritory
{
    /// <summary>The sentinel for land owned by no team. Neutral is the same as unassigned — the better word.</summary>
    public const string Neutral = "neutral";

    /// <summary>Per canonical island id (<see cref="IslandDetector"/>'s 1-based id, string-keyed to match
    /// <see cref="MapIntent.IslandTeams"/>), the owning team id or <see cref="Neutral"/>. A stored value wins;
    /// otherwise a spawn's team on the island, else a wool's owner, else neutral.</summary>
    public static Dictionary<string, string> Assign(IEnumerable<(int X, int Z)> footprint, MapIntent intent)
        => Ownership(footprint, intent).IslandTeams;

    /// <summary>A <c>cell → team-colour damage</c> function (0–15 nibble, -1 = neutral) for the terrain
    /// painter, from the resolved ownership — one decomposition, read straight through.</summary>
    public static Func<int, int, int> DamageAt(IEnumerable<(int X, int Z)> footprint, MapIntent intent)
    {
        var (cellToId, islandTeams) = Ownership(footprint, intent);
        var damageByTeam = (intent.Teams ?? []).ToDictionary(t => t.Id, t => BlockColors.BlockDamage(t.Color));
        return (x, z) => cellToId.TryGetValue((x, z), out var id)
            && islandTeams.TryGetValue(id.ToString(), out var team)
            && damageByTeam.TryGetValue(team, out var dmg) ? dmg : -1;
    }

    // Decompose once and resolve each island's owner: stored IslandTeams first, then anchors, else neutral.
    private static (IReadOnlyDictionary<(int X, int Z), int> CellToId, Dictionary<string, string> IslandTeams) Ownership(
        IEnumerable<(int X, int Z)> footprint, MapIntent intent)
    {
        // minIslandSize 1 + 8-connectivity match SketchEndpoints' finish, so ids equal the islands_json ids.
        var (islands, cellToId) = IslandDetector.DetectWithCells(footprint, minIslandSize: 1, connectivity: 8);
        var stored = intent.IslandTeams ?? new();
        var teamIds = (intent.Teams ?? []).Select(t => t.Id).ToHashSet();

        int? IslandAt(double x, double z)
            => cellToId.TryGetValue(((int)Math.Floor(x), (int)Math.Floor(z)), out var id) ? id : null;

        // anchor team per island — a spawn wins over a wool.
        var anchor = new Dictionary<int, string>();
        foreach (var s in intent.Spawns)
            if (IslandAt(s.Point.X, s.Point.Z) is { } id) anchor.TryAdd(id, s.Team);
        foreach (var w in intent.Wools ?? [])
            if (IslandAt(w.Spawn.X, w.Spawn.Z) is { } id) anchor.TryAdd(id, w.Owner);

        var islandTeams = new Dictionary<string, string>(islands.Count);
        foreach (var isl in islands)
        {
            var key = isl.Id.ToString();
            var team = stored.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v
                     : anchor.GetValueOrDefault(isl.Id, Neutral);
            islandTeams[key] = teamIds.Contains(team) ? team : Neutral;   // unknown/blank/sentinel → neutral
        }
        return (cellToId, islandTeams);
    }
}
