using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The DTC XML surface. A core is <c>team + region + material</c> — structurally the destroyable with a
/// different owning attribute and a leak level, so it reuses the same group flatten, region property and
/// mode membership. The corpus leans on the inheritance harder than DTM does: <c>leak</c> is declared on
/// the group in 318 of 320 cases and <c>modes</c> in all of them.
/// </summary>
public sealed class CoreParsingTests
{
    private static PgmStudio.Domain.MapXml Parse(string body) => MapParser.ParseXmlString(
        "<?xml version=\"1.0\"?><map proto=\"1.5.0\"><name>m</name><version>1</version><objective>o</objective>"
        + body + "</map>");

    // PGM spells the owning attribute `team` on a core and `owner` on a destroyable — their own
    // inconsistency, with a standing TODO in their source. We mirror the XML and call the field Owner.
    [Test]
    public async Task The_owner_comes_from_the_team_attribute()
    {
        var m = Parse("""<cores><core team="red" region="r"/></cores>""");
        await Assert.That(m.Cores.Single().Owner).IsEqualTo("red");
    }

    [Test]
    public async Task Group_attributes_cascade_to_every_core()
    {
        var m = Parse("""
            <cores material="obsidian" leak="4" mode-changes="true">
                <core team="red" region="red-core"/>
                <core team="blue" region="blue-core"/>
            </cores>
            """);
        await Assert.That(m.Cores.Count).IsEqualTo(2);
        foreach (var c in m.Cores)
        {
            await Assert.That(c.Material).IsEqualTo("obsidian");
            await Assert.That(c.Leak).IsEqualTo(4);
            await Assert.That(c.ModeChanges).IsTrue();
        }
        await Assert.That(m.Cores.Select(c => c.Owner)).IsEquivalentTo(new[] { "red", "blue" });
    }

    // Both default in PGM, so an unauthored value stays unauthored rather than being materialised here.
    [Test]
    public async Task An_unauthored_material_and_leak_stay_null_meaning_obsidian_and_five()
    {
        var c = Parse("""<cores><core team="red" region="r"/></cores>""").Cores.Single();
        await Assert.That(c.Material).IsEmpty();
        await Assert.That(c.Leak).IsNull();
    }

    // PGM auto-names a nameless core per team ("Core", "Core 2", …), so leaving it absent round-trips.
    [Test]
    public async Task An_unauthored_name_stays_absent_and_is_not_written()
    {
        var m = Parse("""<cores><core team="red" region="r"/></cores>""");
        await Assert.That(m.Cores.Single().Name).IsEmpty();
        await Assert.That(XmlWriter.ToXml(m)).DoesNotContain("name=");
    }

    [Test]
    public async Task An_unauthored_id_is_generated_from_the_owner_and_stays_unique()
    {
        var m = Parse("""
            <cores>
                <core team="red" region="a"/>
                <core team="red" region="b"/>
                <core team="blue" region="c" name="Sky Core"/>
            </cores>
            """);
        await Assert.That(m.Cores.Select(c => c.Id)).IsEquivalentTo(new[] { "red-core", "red-core-2", "blue-sky-core" });
    }

    [Test]
    public async Task The_region_resolves_from_an_attribute_or_a_child()
    {
        var byAttr = Parse("""
            <regions><cuboid id="core-box" min="0,10,0" max="5,15,5"/></regions>
            <cores><core team="red" region="core-box"/></cores>
            """);
        await Assert.That(byAttr.Cores.Single().RegionId).IsEqualTo("core-box");

        var byChild = Parse("""<cores><core team="red"><region><cuboid min="0,10,0" max="5,15,5"/></region></core></cores>""");
        var region = byChild.Regions[byChild.Cores.Single().RegionId];
        await Assert.That(region.Type).IsEqualTo("cuboid");
        await Assert.That(region.MinY).IsEqualTo(10);
    }

    [Test]
    public async Task Combining_modes_with_mode_changes_is_rejected()
    {
        await Assert.That(() => Parse("""
            <modes><mode id="a" after="10m" material="lava"/></modes>
            <cores><core team="red" region="r" modes="a" mode-changes="true"/></cores>
            """)).Throws<UnsupportedMapException>();
    }

    // ── gamemode ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Cores_make_a_map_dtc()
        => await Assert.That(Parse("""<cores><core team="red" region="r"/></cores>""").Gamemodes)
            .IsEquivalentTo(new[] { "dtc" });

    // ── writer ──────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Cores_are_written_flat_and_re_parse()
    {
        var m = Parse("""
            <cores material="obsidian" leak="3">
                <core team="red" region="red-core" name="Red Core"/>
                <core team="blue" region="blue-core"/>
            </cores>
            <regions><cuboid id="red-core" min="0,10,0" max="5,15,5"/><cuboid id="blue-core" min="20,10,0" max="25,15,5"/></regions>
            """);
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("<core id=\"red-red-core\" name=\"Red Core\" team=\"red\" material=\"obsidian\" leak=\"3\" region=\"red-core\"/>");

        var back = MapParser.ParseXmlString(xml);
        await Assert.That(back.Cores.Count).IsEqualTo(2);
        await Assert.That(back.Cores[1].Owner).IsEqualTo("blue");
        await Assert.That(back.Cores[1].Leak).IsEqualTo(3);       // was inherited; now explicit on the leaf
        await Assert.That(back.Cores[1].Name).IsEmpty();
        await Assert.That(back.Gamemodes).IsEquivalentTo(new[] { "dtc" });
    }
}
