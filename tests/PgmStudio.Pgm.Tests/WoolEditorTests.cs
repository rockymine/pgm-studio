using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>Wool + monument editor tests (pure doc transformations).</summary>
public sealed class WoolEditorTests
{
    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>w</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red">Red</team><team id="blue-team" color="blue">Blue</team></teams>
        <regions><cuboid id="r" min="0,0,0" max="1,1,1"/></regions></map>
        """));

    [Test]
    public async Task Add_wool_uses_colour_slug_id_and_no_monuments()
    {
        var doc = Map();
        var r = WoolEditor.AddWool(doc, new Dict { ["color"] = "green" });
        var wool = (Dict)r["wool"]!;
        await Assert.That(wool["id"]).IsEqualTo("green");
        await Assert.That(((List<object?>)wool["monuments"]!).Count).IsEqualTo(0);
        await Assert.That(((List<object?>)doc["wools"]!).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Invalid_or_duplicate_colour_is_rejected()
    {
        var doc = Map();
        await Assert.That(() => WoolEditor.AddWool(doc, new Dict { ["color"] = "chartreuse" })).Throws<EditException>();
        WoolEditor.AddWool(doc, new Dict { ["color"] = "green" });
        await Assert.That(() => WoolEditor.AddWool(doc, new Dict { ["color"] = "green" })).Throws<EditException>();
    }

    [Test]
    public async Task Monument_id_is_colour_team_and_team_must_be_unique()
    {
        var doc = Map();
        WoolEditor.AddWool(doc, new Dict { ["color"] = "green" });
        var m = (Dict)WoolEditor.AddMonument(doc, "green", new Dict { ["team"] = "blue-team" })["monument"]!;
        await Assert.That(m["id"]).IsEqualTo("green-blue-team");
        await Assert.That(() => WoolEditor.AddMonument(doc, "green", new Dict { ["team"] = "blue-team" })).Throws<EditException>();

        WoolEditor.DeleteMonument(doc, "green", "green-blue-team");
        var wool = ((List<object?>)doc["wools"]!).Cast<Dict>().First();
        await Assert.That(((List<object?>)wool["monuments"]!).Count).IsEqualTo(0);
    }
}
