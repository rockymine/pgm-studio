using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Who a wool belongs to and who captures it. A wool's <c>owner</c> is the team that defends it, and every other
/// team on the map captures it, each at a monument of its own: one capturer on a two-team map, three on a
/// four-team one. The defender is the one decision; the capturers, their monuments and the room rules follow from
/// it (<c>docs/pgm/destroyables-and-cores.md</c>).
/// </summary>
public static class WoolTeams
{
    /// <summary>The teams that capture <paramref name="wool"/>: every team on the map but its defender.</summary>
    public static IReadOnlyList<string> Capturers(MapIntent intent, WoolIntent wool) =>
        [.. (intent.Teams ?? []).Select(team => team.Id).Where(team => !Same(team, wool.Owner))];

    /// <summary>
    /// What is wrong with the teams of the map's wools, as complaints: a wool without exactly one monument for each
    /// team but its defender (<see cref="ObjectiveRules.WoolCapturers"/>) — a wool stating none has them placed by
    /// the build, one per capturing team, and is not asked — and, where <paramref name="islandTeamAt"/>
    /// answers whose island a column is, a wool standing on another team's island than its defender's, or a
    /// monument on another team's island than the team placing the wool there
    /// (<see cref="ObjectiveRules.WoolOnAnotherIsland"/>). Read over the expanded intent, so every wool a symmetric
    /// map fans out to is checked as it will be exported.
    /// </summary>
    public static IReadOnlyList<Finding> Check(MapIntent intent, Func<double, double, string?>? islandTeamAt = null)
    {
        var expanded = SymmetryExpander.Expand(intent);
        var teams = (expanded.Teams ?? []).Select(team => team.Id).ToList();
        var findings = new List<Finding>();
        foreach (var wool in expanded.Wools ?? [])
        {
            if (!teams.Any(team => Same(team, wool.Owner))) continue;
            var color = wool.Color ?? Name(wool.Owner);
            var stated = wool.Monuments.Select(monument => monument.Team).ToList();
            foreach (var missing in Capturers(expanded, wool).Where(team => stated.Count > 0 && !stated.Any(s => Same(s, team))))
                findings.Add(Complaint(ObjectiveRules.WoolCapturers,
                    $"the {color} wool, which {Name(wool.Owner)} defends, has no monument for {Name(missing)}", color));
            foreach (var team in stated.Where(team => Same(team, wool.Owner)).Distinct())
                findings.Add(Complaint(ObjectiveRules.WoolCapturers,
                    $"the {color} wool has a monument for {Name(team)}, which defends it", color));
            foreach (var team in stated.Where(team => !teams.Any(t => Same(t, team))).Distinct())
                findings.Add(Complaint(ObjectiveRules.WoolCapturers,
                    $"the {color} wool has a monument for '{team}', which is not a team on the map", color));

            if (islandTeamAt is null) continue;
            if (islandTeamAt(wool.Spawn.X, wool.Spawn.Z) is { } spawnIsland && !Same(spawnIsland, wool.Owner))
                findings.Add(Complaint(ObjectiveRules.WoolOnAnotherIsland,
                    $"the {color} wool, which {Name(wool.Owner)} defends, spawns at ({Block(wool.Spawn.X)}, "
                    + $"{Block(wool.Spawn.Z)}) on {Name(spawnIsland)}'s island", color));
            foreach (var monument in wool.Monuments)
                if (islandTeamAt(monument.Location.X, monument.Location.Z) is { } island && !Same(island, monument.Team))
                    findings.Add(Complaint(ObjectiveRules.WoolOnAnotherIsland,
                        $"the {color} wool's monument for {Name(monument.Team)} at ({Block(monument.Location.X)}, "
                        + $"{Block(monument.Location.Y)}, {Block(monument.Location.Z)}) stands on {Name(island)}'s island",
                        color));
        }
        return findings;
    }

    private static Finding Complaint(string rule, string message, string color) =>
        new(rule, message, Severity.Complaint, Field: "wools", Subjects: [color]);

    private static bool Same(string a, string b) => IntentNaming.Slug(a) == IntentNaming.Slug(b);

    private static string Name(string team) => IntentNaming.Slug(team);

    private static int Block(double coordinate) => (int)Math.Floor(coordinate);
}
