using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>Spawn + apply-rule editor tests (pure doc transformations).</summary>
public sealed class SpawnAndRuleEditorTests
{
    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>t</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red">Red</team></teams>
        <regions><cuboid id="zone" min="0,0,0" max="4,4,4"/><cuboid id="obs" min="9,0,9" max="10,1,10"/></regions></map>
        """));

    [Test]
    public async Task Add_spawn_for_unknown_region_is_not_found()
    {
        var doc = Map();
        await Assert.That(() => SpawnEditor.AddSpawnLink(doc, new Dict { ["region_id"] = "ghost", ["team"] = "red-team" }))
            .Throws<EditException>();
    }

    [Test]
    public async Task Add_then_delete_spawn_link()
    {
        var doc = Map();
        SpawnEditor.AddSpawnLink(doc, new Dict { ["region_id"] = "zone", ["team"] = "red-team", ["yaw"] = 90 });
        var spawns = (List<object?>)doc["spawns"]!;
        await Assert.That(spawns.Count).IsEqualTo(1);
        await Assert.That(Links((Dict)spawns[0]!).Single()["region"]).IsEqualTo("zone");

        SpawnEditor.DeleteSpawnLink(doc, "zone");
        await Assert.That(((List<object?>)doc["spawns"]!).Count).IsEqualTo(0);
    }

    private static List<Dict> Links(Dict spawn) => ((List<object?>)spawn["regions"]!).OfType<Dict>().ToList();

    private static Dict TwoRegionSpawn() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.5.0"><name>t</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red">Red</team></teams>
        <spawns><spawn team="red-team"><regions><region id="zone" yaw="90"/><region id="obs" yaw="180"/></regions></spawn></spawns>
        <regions><cuboid id="zone" min="0,0,0" max="4,4,4"/><cuboid id="obs" min="9,0,9" max="10,1,10"/></regions></map>
        """));

    [Test]
    public async Task A_link_of_a_spawn_with_several_regions_turns_and_goes_alone()
    {
        var doc = TwoRegionSpawn();
        SpawnEditor.UpdateSpawnLink(doc, "obs", new Dict { ["yaw"] = 270, ["kit"] = "rush" });
        var spawn = (Dict)((List<object?>)doc["spawns"]!).Single()!;
        await Assert.That(Links(spawn).Select(link => $"{link["region"]}@{link["yaw"]}"))
            .IsEquivalentTo(new[] { "zone@90", "obs@270" });
        await Assert.That(spawn["kit"]).IsEqualTo("rush").Because("a link's kit is its whole spawn's");

        SpawnEditor.DeleteSpawnLink(doc, "obs");
        spawn = (Dict)((List<object?>)doc["spawns"]!).Single()!;
        await Assert.That(Links(spawn).Select(link => link["region"])).IsEquivalentTo(new object?[] { "zone" });
    }

    [Test]
    public async Task Observer_spawn_set_and_clear()
    {
        var doc = Map();
        SpawnEditor.SetObserverSpawn(doc, new Dict { ["region_id"] = "obs" });
        await Assert.That(doc["observer_spawn"]).IsNotNull();
        SpawnEditor.DeleteObserverSpawn(doc);
        await Assert.That(doc["observer_spawn"]).IsNull();
    }

    [Test]
    public async Task Create_apply_rule_assigns_stable_id_and_validates_refs()
    {
        var doc = Map();
        var r = ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["region"] = "zone", ["block"] = "never" });
        await Assert.That(r["id"]).IsEqualTo("rule_1");
        await Assert.That(((List<object?>)doc["apply_rules"]!).Count).IsEqualTo(1);

        // a dangling plain-id region ref is rejected
        await Assert.That(() => ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["region"] = "ghost", ["block"] = "never" }))
            .Throws<EditException>();
    }
}
