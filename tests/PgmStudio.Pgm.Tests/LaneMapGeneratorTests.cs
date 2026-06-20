using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The full-map generator: a lane layout projected into a complete MapIntent (teams, spawns, wools,
/// monuments, build bridges). Each wool is captured by the opponent, whose monument sits near their spawn;
/// the inferred bridges connect the islands. Projecting the intent into a document and checking it with
/// <see cref="MapValidity"/> proves a generated map clears the monument gate on its own.
/// </summary>
public sealed class LaneMapGeneratorTests
{
    [Test]
    public async Task Generates_two_teams_two_spawns_two_wools()
    {
        var (_, intent) = LaneMapGenerator.Generate();
        await Assert.That(intent.Teams!.Count).IsEqualTo(2);
        await Assert.That(intent.Spawns.Count).IsEqualTo(2);
        await Assert.That(intent.Wools!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Every_wool_has_a_monument_owned_by_the_opponent()
    {
        var (_, intent) = LaneMapGenerator.Generate();
        foreach (var wool in intent.Wools!)
        {
            await Assert.That(wool.Monuments.Count).IsEqualTo(1);
            await Assert.That(wool.Monuments[0].Team).IsNotEqualTo(wool.Owner);
        }
    }

    [Test]
    public async Task Monument_sits_near_the_capturing_teams_spawn()
    {
        var (_, intent) = LaneMapGenerator.Generate();
        foreach (var wool in intent.Wools!)
        {
            var captor = wool.Monuments[0].Team;
            var captorSpawn = intent.Spawns.Single(s => s.Team == captor);
            // same lane (x) as the captor's spawn, and within a few blocks of it
            await Assert.That(wool.Monuments[0].Location.X).IsEqualTo(captorSpawn.Point.X);
            await Assert.That(Math.Abs(wool.Monuments[0].Location.Z - captorSpawn.Point.Z) <= 6).IsTrue();
        }
    }

    [Test]
    public async Task Build_areas_bridge_the_islands()
    {
        var (_, intent) = LaneMapGenerator.Generate();
        await Assert.That(intent.Build!.Areas.Count > 0).IsTrue();
    }

    [Test]
    public async Task Projected_document_passes_the_monument_validity_gate()
    {
        var (_, intent) = LaneMapGenerator.Generate();
        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        var validity = MapValidity.Check(doc);
        await Assert.That(validity.Valid).IsTrue();
    }
}
