namespace PgmStudio.Analysis.Playability;

using PgmStudio.Geom;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Which ground a team may not set foot on — the map's <c>enter</c> apply rules read per team, so a spawn's
/// own protection reads as the wall it is.
///
/// <para>One reader, because two would disagree. The traversability verdict subtracts a team's denied cells
/// before asking whether it can reach the goals it must contest, and a walk measured for that team has to
/// subtract the same ones or it prices a route through ground the player is thrown out of. What they share is
/// the answer — the cells — rather than what either does with it.</para>
///
/// <para>A denial has to be <b>provable</b> to count: a rule with no region, geometry that will not resolve,
/// or a filter this reader cannot follow denies nobody. So an exotic wiring can only ever fail to subtract
/// ground, never invent a barred region that is not there.</para>
/// </summary>
public static class EntryDenials
{
    /// <summary>The teams a map spawns, in declaration order and without the observer.</summary>
    public static List<string> Teams(Dict data)
    {
        var teams = new List<string>();
        foreach (var spawn in MapDoc.AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            if (MapDoc.Truthy(spawn.GetValueOrDefault("observer"))) continue;
            if (spawn.GetValueOrDefault("team") is not string team || team.Length == 0) continue;
            if (!teams.Contains(team)) teams.Add(team);
        }
        return teams;
    }

    /// <summary>Each team's barred cells over the grid given, as a row-major mask. A team no rule bars is
    /// absent from the result rather than present with an empty mask, which is the difference between "walks
    /// its own map" and "walks everyone's".</summary>
    public static Dictionary<string, bool[]> Masks(Dict data, IReadOnlyList<string> teams,
        int minX, int minZ, int nx, int nz)
    {
        var denials = new Dictionary<string, bool[]>();
        if (teams.Count == 0) return denials;

        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var filters = MapDoc.AsDict(data.GetValueOrDefault("filters"));
        var bounds = ((double)minX, (double)minZ, (double)(minX + nx), (double)(minZ + nz));

        foreach (var rule in MapDoc.AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            if (rule.GetValueOrDefault("enter") is not string enter || enter.Length == 0) continue;
            if (rule.GetValueOrDefault("region") is not { } regionRef) continue;

            bool[]? mask = null;
            foreach (var team in teams)
            {
                if (Allows(enter, filters, team)) continue;
                mask ??= Buildability.RegionMask(regionRef, regions, bounds, minX, minZ, nx, nz);
                if (mask is null) break;
                if (!denials.TryGetValue(team, out var denied)) denials[team] = denied = new bool[nx * nz];
                for (var i = 0; i < mask.Length; i++) denied[i] |= mask[i];
            }
        }
        return denials;
    }

    /// <summary>One team's barred cells as coordinates, or null where nothing bars it — the shape a walk
    /// wants, which holds sets rather than a grid.</summary>
    public static HashSet<(int X, int Z)>? Cells(Dict data, string team, CellRect over)
    {
        var masks = Masks(data, [team], over.X, over.Z, over.Width, over.Height);
        if (!masks.TryGetValue(team, out var denied)) return null;

        var cells = new HashSet<(int X, int Z)>();
        for (var i = 0; i < denied.Length; i++)
            if (denied[i]) cells.Add((over.X + i % over.Width, over.Z + i / over.Width));
        return cells;
    }

    /// <summary>Whether an <c>enter</c> filter lets <paramref name="team"/> in. Deliberately permissive: a
    /// team filter answers by its team, the boolean wrappers compose, and anything unresolvable answers yes.
    /// </summary>
    public static bool Allows(string value, Dict filters, string team, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (value.Length == 0 || !seen.Add(value)) return true;
        if (value is "always" or "allow") return true;
        if (value == "never") return false;
        if (filters.GetValueOrDefault(value) is not Dict filter) return true;

        return (filter.GetValueOrDefault("type") as string) switch
        {
            "team" => filter.GetValueOrDefault("team") as string == team,
            "not" => !Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "allow" => Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "deny" => !Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "any" => MapDoc.AsList(filter.GetValueOrDefault("children"))
                .Any(child => Allows(child as string ?? "", filters, team, seen)),
            "all" => MapDoc.AsList(filter.GetValueOrDefault("children"))
                .All(child => Allows(child as string ?? "", filters, team, seen)),
            "always" => true,
            "never" => false,
            _ => true,
        };
    }
}
