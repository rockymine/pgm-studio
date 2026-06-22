using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>The parser's supported-range gate: proto >= 1.4.0 (id-based regions/filters/kits) and no
/// modern (1.13+ palette) worlds. Below the floor or a modern min-server-version throws
/// <see cref="UnsupportedMapException"/> rather than silently mis-parsing.</summary>
public sealed class MapSupportTests
{
    private static string Map(string mapTag) =>
        $"""<?xml version="1.0"?>{mapTag}<name>m</name><version>1</version><objective>o</objective></map>""";

    [Test]
    [Arguments("1.4.0")]
    [Arguments("1.4.2")]
    [Arguments("1.5.0")]
    [Arguments("1.5.1")]
    public async Task Accepts_proto_at_or_above_the_floor(string proto)
    {
        var m = MapParser.ParseXmlString(Map($"<map proto=\"{proto}\">"));
        await Assert.That(m.Name).IsEqualTo("m");
    }

    [Test]
    public async Task Rejects_proto_below_the_floor()   // kytriak_te ships proto 1.3.0
    {
        await Assert.That(() => MapParser.ParseXmlString(Map("<map proto=\"1.3.0\">")))
            .Throws<UnsupportedMapException>();
    }

    [Test]
    public async Task Rejects_missing_or_unparseable_proto()
    {
        await Assert.That(() => MapParser.ParseXmlString(Map("<map>")))
            .Throws<UnsupportedMapException>();
        await Assert.That(() => MapParser.ParseXmlString(Map("<map proto=\"latest\">")))
            .Throws<UnsupportedMapException>();
    }

    [Test]
    public async Task Rejects_modern_min_server_version()   // allure: proto 1.5.0 but a 1.21.10 world
    {
        await Assert.That(() => MapParser.ParseXmlString(Map("<map proto=\"1.5.0\" min-server-version=\"1.21.10\">")))
            .Throws<UnsupportedMapException>();
    }

    [Test]
    public async Task Accepts_a_legacy_min_server_version()   // a declared pre-flattening server is fine
    {
        var m = MapParser.ParseXmlString(Map("<map proto=\"1.4.0\" min-server-version=\"1.8\">"));
        await Assert.That(m.Name).IsEqualTo("m");
    }
}
