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

        // the comment sits on its own line under the element at the same indentation (the corpus convention)
        await Assert.That(xml).Contains("<author uuid=\"fe3608b7-d105-4029-8800-34b3147065b6\"/>\n    <!-- rockymine -->");
        await Assert.That(xml).Contains("<contributor uuid=\"00000000-0000-0000-0000-000000000001\"/>\n    <!-- Helper -->");

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
}
