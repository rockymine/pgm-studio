using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>Team editor tests (pure doc transformations; the DB write path is exercised by the API).</summary>
public sealed class TeamEditorTests
{
    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>t</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red">Red</team></teams>
        <spawns><spawn team="red-team" region="r"/></spawns>
        <regions><cuboid id="r" min="0,0,0" max="1,1,1"/></regions></map>
        """));

    [Test]
    public async Task Add_team_appends_with_defaults()
    {
        var doc = Map();
        TeamEditor.AddTeam(doc, new Dict { ["id"] = "blue-team", ["color"] = "blue", ["max_players"] = 12 });
        var teams = ((List<object?>)doc["teams"]!).Cast<Dict>().ToList();
        await Assert.That(teams.Count).IsEqualTo(2);
        await Assert.That(teams[1]["id"]).IsEqualTo("blue-team");
        await Assert.That(teams[1]["max_players"]).IsEqualTo(12);
        await Assert.That(teams[1]["min_players"]).IsEqualTo(0);   // defaulted
    }

    [Test]
    public async Task Add_duplicate_id_conflicts()
    {
        var doc = Map();
        await Assert.That(() => TeamEditor.AddTeam(doc, new Dict { ["id"] = "red-team" }))
            .Throws<EditException>();
    }

    [Test]
    public async Task Rename_team_cascades_to_spawns()
    {
        var doc = Map();
        TeamEditor.UpdateTeam(doc, "red-team", new Dict { ["id"] = "crimson" });
        var team = ((List<object?>)doc["teams"]!).Cast<Dict>().First();
        await Assert.That(team["id"]).IsEqualTo("crimson");
        var spawn = ((List<object?>)doc["spawns"]!).Cast<Dict>().First();
        await Assert.That(spawn["team"]).IsEqualTo("crimson");      // spawn re-pointed
    }

    [Test]
    public async Task Delete_team_removes_its_spawns()
    {
        var doc = Map();
        TeamEditor.DeleteTeam(doc, "red-team");
        await Assert.That(((List<object?>)doc["teams"]!).Count).IsEqualTo(0);
        await Assert.That(((List<object?>)doc["spawns"]!).Count).IsEqualTo(0);
    }
}
