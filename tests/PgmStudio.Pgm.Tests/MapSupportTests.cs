using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>The parser's supported-range gate: proto >= 1.4.0 (id-based regions/filters/kits), no
/// modern (1.13+ palette) worlds, and no objective module the parser cannot read. Outside the range
/// throws <see cref="UnsupportedMapException"/> rather than silently mis-parsing.</summary>
public sealed class MapSupportTests
{
    private static string Map(string mapTag, string body = "") =>
        $"""<?xml version="1.0"?>{mapTag}<name>m</name><version>1</version><objective>o</objective>{body}</map>""";

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

    // An objective module the parser does not read would be dropped in silence — the map would export
    // without its goal. Each of these declares a non-auxiliary gamemode in PGM.
    [Test]
    [Arguments("<cores><core team=\"red\" region=\"r\"/></cores>")]
    [Arguments("<control-points><control-point id=\"hill\"/></control-points>")]
    [Arguments("<king><hills><hill id=\"h\"/></hills></king>")]
    [Arguments("<flags><flag id=\"f\"/></flags>")]
    [Arguments("<score><limit>50</limit></score>")]
    public async Task Rejects_an_objective_module_it_cannot_read(string module)
    {
        await Assert.That(() => MapParser.ParseXmlString(Map("<map proto=\"1.5.0\">", module)))
            .Throws<UnsupportedMapException>();
    }

    // Auxiliary modules modify how a map plays, not what its goal is — PGM tags them auxiliary and
    // dropping them costs no objective.
    [Test]
    [Arguments("<blitz><lives>1</lives></blitz>")]
    [Arguments("<rage/>")]
    public async Task Accepts_an_auxiliary_module(string module)
    {
        var m = MapParser.ParseXmlString(Map("<map proto=\"1.5.0\">", module));
        await Assert.That(m.Name).IsEqualTo("m");
    }
}
