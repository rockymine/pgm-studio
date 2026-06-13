using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>Region editor tests (canonical shapes, grouping, rename cascade, delete/restore).</summary>
public sealed class RegionEditorTests
{
    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>r</name><version>1</version><objective>o</objective>
        <regions><cuboid id="seed" min="0,0,0" max="1,1,1"/></regions></map>
        """));

    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;

    [Test]
    public async Task Create_rectangle_uses_canonical_bounds_shape()
    {
        var doc = Map();
        var r = RegionEditor.CreateRegion(doc, new Dict { ["type"] = "rectangle", ["id"] = "rect", ["min_x"] = 0, ["min_z"] = 0, ["max_x"] = 10, ["max_z"] = 10 });
        await Assert.That(r["id"]).IsEqualTo("rect");
        var rect = (Dict)Regions(doc)["rect"]!;
        await Assert.That(rect["type"]).IsEqualTo("rectangle");
        var max = (Dict)((Dict)rect["bounds_2d"]!)["max"]!;
        await Assert.That(System.Convert.ToDouble(max["x"])).IsEqualTo(10d);
    }

    [Test]
    public async Task Group_then_rename_cascades_to_compound_children()
    {
        var doc = Map();
        RegionEditor.CreateRegion(doc, new Dict { ["type"] = "rectangle", ["id"] = "a", ["min_x"] = 0, ["min_z"] = 0, ["max_x"] = 4, ["max_z"] = 4 });
        RegionEditor.GroupRegions(doc, new Dict { ["type"] = "union", ["id"] = "grp", ["child_ids"] = new List<object?> { "a", "seed" } });

        RegionEditor.PatchRegion(doc, "a", new Dict { ["id"] = "a2" });
        var children = ((Dict)Regions(doc)["grp"]!)["children"] as List<object?>;
        await Assert.That(children!.Select(c => (string)c!)).Contains("a2");
        await Assert.That(Regions(doc).ContainsKey("a")).IsFalse();
    }

    [Test]
    public async Task Delete_compound_removes_subtree_and_restore_brings_it_back()
    {
        var doc = Map();
        RegionEditor.CreateRegion(doc, new Dict { ["type"] = "rectangle", ["id"] = "a", ["min_x"] = 0, ["min_z"] = 0, ["max_x"] = 4, ["max_z"] = 4 });
        RegionEditor.GroupRegions(doc, new Dict { ["type"] = "union", ["id"] = "grp", ["child_ids"] = new List<object?> { "a", "seed" } });

        var snapshot = (Dict)RegionEditor.DeleteRegion(doc, "grp")["snapshot"]!;
        await Assert.That(Regions(doc).ContainsKey("grp")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey("a")).IsFalse();   // subtree cascade

        RegionEditor.RestoreRegion(doc, snapshot);
        await Assert.That(Regions(doc).ContainsKey("grp")).IsTrue();
        await Assert.That(Regions(doc).ContainsKey("a")).IsTrue();
    }
}
