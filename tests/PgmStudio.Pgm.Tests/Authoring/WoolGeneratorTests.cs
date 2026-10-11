using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests.Authoring;

using Dict = Dictionary<string, object?>;

public sealed class WoolGeneratorTests
{
    private static MapIntent TwoTeams(params MonumentIntent[] monuments) => new()
    {
        Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" },
                 new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
        Wools = [new WoolIntent { Owner = "blue-team", Color = "blue", Spawn = new Pt(40, 8, 0), Monuments = [.. monuments] }],
    };

    [Test]
    public async Task One_monument_for_each_capturing_team_projects()
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, TwoTeams(new MonumentIntent { Team = "red-team", Location = new Pt(-40, 10, 0) }));

        await Assert.That((doc["wools"] as List<object?>)!.Count).IsEqualTo(1);
    }

    /// <summary>Changing a wool's defender changes who captures it, so the monument region the previous projection
    /// named for the old capturer goes, and with it the old defender's room filter; nothing is left behind for the
    /// export to write unreferenced.</summary>
    [Test]
    public async Task A_wool_whose_teams_change_leaves_no_region_of_its_old_teams_behind()
    {
        var doc = new Dict();
        var first = TwoTeams(new MonumentIntent { Team = "red-team", Location = new Pt(-40, 10, 0) });
        IntentGenerator.Apply(doc, first with { Wools = [first.Wools![0] with { Protection = [new(35, -5, 45, 5)] }] });
        await Assert.That(Ids(doc, "regions")).Contains("blue-red-monument");
        await Assert.That(Ids(doc, "filters")).Contains("blues-woolrooms-filter");

        IntentGenerator.Apply(doc, new MapIntent
        {
            Teams = TwoTeams().Teams,
            Wools = [new WoolIntent { Owner = "red-team", Color = "blue", Spawn = new Pt(40, 8, 0), Protection = [new(35, -5, 45, 5)],
                                      Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-40, 10, 0) }] }],
        });

        await Assert.That(Ids(doc, "regions")).DoesNotContain("blue-red-monument");
        await Assert.That(Ids(doc, "regions")).Contains("blue-blue-monument");
        await Assert.That(Ids(doc, "filters")).DoesNotContain("blues-woolrooms-filter");
        await Assert.That(Ids(doc, "filters")).Contains("reds-woolrooms-filter");
    }

    private static IEnumerable<string> Ids(Dict doc, string section) =>
        doc.GetValueOrDefault(section) is Dict ids ? ids.Keys : [];

    [Test]
    public async Task A_second_monument_for_one_team_is_refused_by_name()
    {
        // A monument two blocks tall detected as two: the second names the region the first already holds.
        var intent = TwoTeams(new MonumentIntent { Team = "red-team", Location = new Pt(9, 10, 256) },
                              new MonumentIntent { Team = "red-team", Location = new Pt(9, 9, 256) });

        var fault = Assert.Throws<EditException>(() => IntentGenerator.Apply(new Dict(), intent));

        await Assert.That(fault!.Finding.Rule).IsEqualTo(ObjectiveRules.OneMonumentPerTeam);
        await Assert.That(fault.Status).IsEqualTo(422);
        await Assert.That(fault.Finding.Message).Contains("(9, 10, 256) and (9, 9, 256)");
    }
}
