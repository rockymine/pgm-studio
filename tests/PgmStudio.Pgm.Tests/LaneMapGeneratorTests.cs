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
            var m = wool.Monuments[0].Location;
            var s = captorSpawn.Point;
            var dist = Math.Sqrt((m.X - s.X) * (m.X - s.X) + (m.Z - s.Z) * (m.Z - s.Z));
            await Assert.That(dist <= 6).IsTrue();   // monument sits a few blocks from the captor's spawn
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

    [Test]
    public async Task Pinwheel_generates_four_teams_one_wool_each_all_valid()
    {
        var (_, intent) = LaneMapGenerator.Generate(new LaneLayoutOptions { Archetype = LaneArchetype.Pinwheel });
        await Assert.That(intent.Teams!.Count).IsEqualTo(4);
        await Assert.That(intent.Spawns.Count).IsEqualTo(4);
        await Assert.That(intent.Wools!.Count).IsEqualTo(4);
        foreach (var w in intent.Wools!) await Assert.That(w.Monuments.Count).IsEqualTo(1);

        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        await Assert.That(MapValidity.Check(doc).Valid).IsTrue();
    }

    [Test]
    public async Task Trident_generates_two_teams_three_wools_each_all_valid()
    {
        var (_, intent) = LaneMapGenerator.Generate(new LaneLayoutOptions { Archetype = LaneArchetype.Trident });
        await Assert.That(intent.Teams!.Count).IsEqualTo(2);
        await Assert.That(intent.Wools!.Count).IsEqualTo(6);   // 3 per team
        foreach (var w in intent.Wools!) await Assert.That(w.Monuments.Count).IsEqualTo(1);

        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        await Assert.That(MapValidity.Check(doc).Valid).IsTrue();
    }
}
