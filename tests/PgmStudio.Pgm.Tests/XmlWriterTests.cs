using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>Focused <see cref="XmlWriter"/> serialization details (the broad round-trip is covered by
/// IntentXmlExportTests): the uuid → username comment next to authors/contributors.</summary>
public sealed class XmlWriterTests
{
    [Test]
    public async Task Author_and_contributor_get_a_name_comment_when_resolved()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors =
            [
                new Author { Uuid = "fe3608b7-d105-4029-8800-34b3147065b6", Role = "author", Name = "rockymine" },
                new Author { Uuid = "00000000-0000-0000-0000-000000000001", Role = "contributor", Name = "Helper" },
            ],
        };
        var xml = XmlWriter.ToXml(m);

        // the comment sits on its own line under the element at the same indentation (the corpus convention);
        // authors/contributors nest two levels deep, so 4-space indentation puts them at 8 spaces.
        await Assert.That(xml).Contains("<author uuid=\"fe3608b7-d105-4029-8800-34b3147065b6\"/>\n        <!-- rockymine -->");
        await Assert.That(xml).Contains("<contributor uuid=\"00000000-0000-0000-0000-000000000001\"/>\n        <!-- Helper -->");

        // comments don't break the re-parse
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        await Assert.That(((List<object?>)reparsed["authors"]!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task No_comment_when_the_name_is_unresolved()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors = [new Author { Uuid = "fe3608b7-d105-4029-8800-34b3147065b6", Role = "author" }],   // no Name
        };
        var xml = XmlWriter.ToXml(m);
        await Assert.That(xml).DoesNotContain("<!--");
    }

    /// <summary>PGM reads the <c>uuid</c> attribute whenever it is present and refuses the map if the value
    /// is not a UUID, so an author who is only a name must be written as the element's text with no uuid
    /// attribute at all. Writing an empty one fails the map to load rather than degrading.</summary>
    [Test]
    public async Task An_author_without_an_account_is_written_as_a_name_not_an_empty_uuid()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors = [new Author { Role = "author", Name = "mapgen" }],   // no Uuid
        };
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).DoesNotContain("uuid=\"\"")
            .Because("PGM parses a present uuid attribute and rejects a map whose value is not a UUID");
        await Assert.That(xml).Contains("<author>mapgen</author>");

        // and it survives the read back, as the same pseudonym
        var reparsed = MapParser.ParseXmlString(xml);
        await Assert.That(reparsed.Authors.Count).IsEqualTo(1);
        await Assert.That(reparsed.Authors[0].Name).IsEqualTo("mapgen");
        await Assert.That(reparsed.Authors[0].Uuid).IsEqualTo("");
    }

    /// <summary>A pseudonym may still say what it did. PGM's own documentation writes the case as
    /// <c>&lt;contributor contribution="A contribution"&gt;aHelper&lt;/contributor&gt;</c> — the name is the
    /// element's text and the contribution is an attribute beside it, so the two are independent and neither
    /// implies an account.</summary>
    [Test]
    public async Task A_pseudonym_keeps_its_contribution()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors =
            [
                new Author { Role = "contributor", Name = "aHelper", Contribution = "A contribution" },
            ],
        };
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("<contributor contribution=\"A contribution\">aHelper</contributor>");
        await Assert.That(xml).DoesNotContain("uuid");

        var reparsed = MapParser.ParseXmlString(xml);
        await Assert.That(reparsed.Authors.Single().Name).IsEqualTo("aHelper");
        await Assert.That(reparsed.Authors.Single().Contribution).IsEqualTo("A contribution");
        await Assert.That(reparsed.Authors.Single().Uuid).IsEqualTo("");
    }

    /// <summary>A person carrying neither an account nor a name is something PGM refuses outright, so the
    /// writer drops them rather than emitting an element that fails the map.</summary>
    [Test]
    public async Task An_author_with_neither_a_name_nor_an_account_is_not_written()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors = [new Author { Role = "author" }],
        };
        await Assert.That(XmlWriter.ToXml(m)).DoesNotContain("<authors>");
    }

    [Test]
    public async Task Kit_force_and_potion_effects_round_trip()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Kits =
            [
                new Kit
                {
                    Id = "spawn-kit",
                    Items = [new KitItem { Slot = 0, Material = "iron sword" }],
                    Effects = [new KitEffect { Type = "damage resistance", Duration = "oo", Amplifier = 100 }],
                },
                new Kit
                {
                    Id = "reset-resistance-kit",
                    Force = true,
                    Effects = [new KitEffect { Type = "damage resistance", Duration = "0", Amplifier = 0 }],
                },
            ],
        };
        var xml = XmlWriter.ToXml(m);

        // force is emitted; the effect carries its duration/amplifier with the type as element text
        await Assert.That(xml).Contains("<kit id=\"reset-resistance-kit\" force=\"true\">");
        await Assert.That(xml).Contains("<effect duration=\"oo\" amplifier=\"100\">damage resistance</effect>");
        await Assert.That(xml).Contains("<effect duration=\"0\" amplifier=\"0\">damage resistance</effect>");

        // and it survives a parse back through the domain model (force + effect-only reset kit both kept)
        var back = MapParser.ParseXmlString(xml);
        var reset = back.Kits.Single(k => k.Id == "reset-resistance-kit");
        await Assert.That(reset.Force).IsTrue();
        await Assert.That(reset.Effects.Single().Type).IsEqualTo("damage resistance");
        await Assert.That(reset.Effects.Single().Duration).IsEqualTo("0");
        var spawn = back.Kits.Single(k => k.Id == "spawn-kit");
        await Assert.That(spawn.Force).IsFalse();
        await Assert.That(spawn.Effects.Single().Amplifier).IsEqualTo(100);
    }

    [Test]
    public async Task Kit_clear_leads_the_kit_and_appears_only_where_it_is_set()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Kits =
            [
                new Kit
                {
                    Id = "spawn-kit",
                    Clear = true,
                    Items = [new KitItem { Slot = 0, Material = "iron sword" }],
                    Armor = [new KitArmor { SlotName = "helmet", Material = "leather helmet" }],
                },
                new Kit
                {
                    Id = "reset-resistance-kit",
                    Force = true,
                    Effects = [new KitEffect { Type = "damage resistance", Duration = "0", Amplifier = 0 }],
                },
            ],
        };
        var xml = XmlWriter.ToXml(m);

        // exactly one <clear/>, and it precedes the items it makes room for
        await Assert.That(xml.Split("<clear/>").Length - 1).IsEqualTo(1);
        await Assert.That(xml.IndexOf("<clear/>", StringComparison.Ordinal))
            .IsLessThan(xml.IndexOf("<item ", StringComparison.Ordinal));

        var back = MapParser.ParseXmlString(xml);
        await Assert.That(back.Kits.Single(k => k.Id == "spawn-kit").Clear).IsTrue();
        await Assert.That(back.Kits.Single(k => k.Id == "reset-resistance-kit").Clear).IsFalse();
    }

    // A kit whose whole job is to empty an inventory carries no item, armour or effect of its own. The parser
    // drops an empty kit, so <clear/> has to be enough on its own to keep one.
    [Test]
    public async Task A_kit_that_only_clears_survives_a_parse()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Kits = [new Kit { Id = "strip-kit", Clear = true }],
        };
        var back = MapParser.ParseXmlString(XmlWriter.ToXml(m));
        await Assert.That(back.Kits.Single().Id).IsEqualTo("strip-kit");
        await Assert.That(back.Kits.Single().Clear).IsTrue();
    }

    // Authors nest <destroyables> groups to share attributes; a writer has nothing to share, so it emits
    // one flat block with every leaf carrying its own attributes. Round-trips are semantic, not textual.
    [Test]
    public async Task Destroyables_are_written_flat_with_explicit_attributes()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Regions = { ["mon"] = new Region { Id = "mon", Type = "block", PosX = 20, PosY = 43, PosZ = 146 } },
            Destroyables =
            [
                new Destroyable { Id = "green-hill", Name = "Hill Monument", Owner = "green", RegionId = "mon",
                                  Materials = "obsidian", ModeChanges = true },
            ],
        };
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains(
            "<destroyable id=\"green-hill\" name=\"Hill Monument\" owner=\"green\" materials=\"obsidian\" mode-changes=\"true\" region=\"mon\"/>");
        await Assert.That(xml).DoesNotContain("<destroyables>\n        <destroyables");

        var back = MapParser.ParseXmlString(xml);
        var d = back.Destroyables.Single();
        await Assert.That(d.Owner).IsEqualTo("green");
        await Assert.That(d.Materials).IsEqualTo("obsidian");
        await Assert.That(d.RegionId).IsEqualTo("mon");
        await Assert.That(d.ModeChanges).IsTrue();
    }

    // A bare `0.8` means 0.8% to PGM, so completion always goes out with its '%'.
    [Test]
    public async Task Completion_is_written_as_a_percentage()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Destroyables = [new Destroyable { Id = "d", Name = "n", Owner = "red", Materials = "ender stone", Completion = 0.8 }],
        };
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("completion=\"80%\"");
        await Assert.That(MapParser.ParseXmlString(xml).Destroyables.Single().Completion).IsEqualTo(0.8);
    }

    // A destroyable whose region was authored inline has no id to reference, so the geometry goes back inline.
    [Test]
    public async Task An_inline_region_is_written_back_inline()
    {
        var source = """
            <?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>
            <destroyables><destroyable owner="red" name="a" materials="obsidian"><region><cuboid min="20,43,146" max="23,46,149"/></region></destroyable></destroyables>
            </map>
            """;
        var xml = XmlWriter.ToXml(MapParser.ParseXmlString(source));

        await Assert.That(xml).Contains("<region>");
        await Assert.That(xml).Contains("<cuboid min=\"20,43,146\" max=\"23,46,149\"/>");

        var back = MapParser.ParseXmlString(xml);
        var region = back.Regions[back.Destroyables.Single().RegionId];
        await Assert.That(region.Type).IsEqualTo("cuboid");
        await Assert.That(region.MinY).IsEqualTo(43);
    }

    [Test]
    public async Task Modes_are_written_with_a_resolvable_id()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Modes = [new ObjectiveMode { Id = "mode-beacon", Name = "`bBEACON", After = "25m", Material = "beacon" }],
            Destroyables = [new Destroyable { Id = "d", Name = "n", Owner = "red", Materials = "obsidian", Modes = ["mode-beacon"] }],
        };
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("<mode id=\"mode-beacon\" after=\"25m\" material=\"beacon\" name=\"`bBEACON\"/>");
        await Assert.That(xml).Contains("modes=\"mode-beacon\"");

        var back = MapParser.ParseXmlString(xml);
        await Assert.That(back.Modes.Single().Id).IsEqualTo("mode-beacon");
        await Assert.That(back.Destroyables.Single().Modes).IsEquivalentTo(new[] { "mode-beacon" });
    }
    /// <summary><b>A pseudonym is the element's own text.</b> PGM takes a person as an account — a
    /// <c>uuid</c> it resolves to a player — or as a pseudonym, and either alone is a whole author. A name no
    /// Minecraft account carries is the second kind, so it is written as text and never as an empty
    /// <c>uuid</c>, which PGM would refuse to resolve. This is what makes a name the editor could not look up
    /// storable rather than droppable (<c>TC2</c>).</summary>
    [Test]
    public async Task An_author_with_no_account_is_written_as_the_elements_text()
    {
        var m = new MapXml
        {
            Name = "Test", Version = "1.0.0",
            Authors =
            [
                new Author { Uuid = "", Role = "author", Name = "Opus 5" },
                new Author { Uuid = "fe3608b7-d105-4029-8800-34b3147065b6", Role = "author", Name = "rockymine" },
                new Author { Uuid = "", Role = "contributor", Name = "Haiku 4.5" },
            ],
        };

        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("<author>Opus 5</author>");
        await Assert.That(xml).Contains("<contributor>Haiku 4.5</contributor>");
        await Assert.That(xml).Contains("uuid=\"fe3608b7-d105-4029-8800-34b3147065b6\"")
            .Because("an account is still written as its uuid");
        await Assert.That(xml).DoesNotContain("uuid=\"\"")
            .Because("an empty uuid is a player PGM cannot resolve");
    }
}
