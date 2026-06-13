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
        await Assert.That(((Dict)spawns[0]!)["region"]).IsEqualTo("zone");

        SpawnEditor.DeleteSpawnLink(doc, "zone");
        await Assert.That(((List<object?>)doc["spawns"]!).Count).IsEqualTo(0);
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

        ApplyRuleEditor.DeleteApplyRule(doc, "rule_1");
        await Assert.That(((List<object?>)doc["apply_rules"]!).Count).IsEqualTo(0);
    }
}
