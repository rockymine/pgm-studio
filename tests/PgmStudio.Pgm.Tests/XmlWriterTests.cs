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
}
