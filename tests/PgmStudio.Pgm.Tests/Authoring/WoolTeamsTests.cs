using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>A wool is defended by its owner and captured by every other team at a monument of its own, on the
/// islands of those teams: one capturer on a two-team map, three on a four-team one.</summary>
public sealed class WoolTeamsTests
{
    private static readonly string[] Four = ["red-team", "yellow-team", "blue-team", "green-team"];

    private static TeamDef Team(string id) => new() { Id = id, Name = id[..^5], Color = id[..^5] };

    private static WoolIntent Wool(string owner, params string[] capturers) => new()
    {
        Owner = owner, Color = owner[..^5], Spawn = new Pt(0, 10, 0),
        Monuments = [.. capturers.Select((team, i) => new MonumentIntent { Team = team, Location = new Pt(10 * (i + 1), 10, 0) })],
    };

    private static MapIntent Map(string[] teams, params WoolIntent[] wools) =>
        new() { Teams = [.. teams.Select(Team)], Wools = [.. wools] };

    [Test]
    public async Task Each_wool_captured_by_every_team_but_its_defender_is_right_on_two_and_four_teams()
    {
        var two = Map(["red-team", "blue-team"], Wool("red-team", "blue-team"), Wool("blue-team", "red-team"));
        var four = Map(Four, [.. Four.Select(owner => Wool(owner, [.. Four.Where(team => team != owner)]))]);

        await Assert.That(WoolTeams.Check(two)).IsEmpty();
        await Assert.That(WoolTeams.Check(four)).IsEmpty();
        await Assert.That(WoolTeams.Capturers(four, four.Wools![0])).IsEquivalentTo(new[] { "yellow-team", "blue-team", "green-team" });
    }

    [Test]
    public async Task A_wool_missing_a_capturer_or_naming_its_defender_is_complained_of()
    {
        var swapped = Map(["red-team", "blue-team"], Wool("red-team", "red-team"));
        var short3 = Map(Four, Wool("red-team", "yellow-team", "blue-team"));

        var swappedFindings = WoolTeams.Check(swapped);
        await Assert.That(swappedFindings.Select(f => f.Rule).Distinct()).IsEquivalentTo(new[] { ObjectiveRules.WoolCapturers });
        await Assert.That(swappedFindings.Select(f => f.Message)).Contains("the red wool, which red defends, has no monument for blue");
        await Assert.That(swappedFindings.Select(f => f.Message)).Contains("the red wool has a monument for red, which defends it");
        await Assert.That(swappedFindings.All(f => f.Severity == Vocabulary.Severity.Complaint)).IsTrue();

        await Assert.That(WoolTeams.Check(short3).Select(f => f.Message))
            .IsEquivalentTo(new[] { "the red wool, which red defends, has no monument for green" });
    }

    /// <summary>A wool stating no monuments has them placed by the build, one per capturing team — the form a map
    /// built from a plan stores — so there is nothing to complain of.</summary>
    [Test]
    public async Task A_wool_whose_monuments_the_build_places_is_not_asked_for_them()
    {
        await Assert.That(WoolTeams.Check(Map(Four, [.. Four.Select(owner => Wool(owner))]))).IsEmpty();
    }

    [Test]
    public async Task A_wool_or_monument_on_another_teams_island_is_complained_of()
    {
        // West of x = 5 is red's island, east of it blue's.
        static string? Island(double x, double z) => x < 5 ? "red-team" : "blue-team";
        var right = Map(["red-team", "blue-team"], Wool("red-team", "blue-team"));
        var wrongRoom = Map(["red-team", "blue-team"], Wool("blue-team", "red-team"));

        await Assert.That(WoolTeams.Check(right, Island)).IsEmpty();
        var findings = WoolTeams.Check(wrongRoom, Island);
        await Assert.That(findings.Select(f => f.Rule).Distinct()).IsEquivalentTo(new[] { ObjectiveRules.WoolOnAnotherIsland });
        await Assert.That(findings.Select(f => f.Message)).IsEquivalentTo(new[]
        {
            "the blue wool, which blue defends, spawns at (0, 0) on red's island",
            "the blue wool's monument for red at (10, 10, 0) stands on blue's island",
        });
    }
}
