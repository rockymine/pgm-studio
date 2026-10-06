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
