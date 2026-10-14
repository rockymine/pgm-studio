using System.Xml.Linq;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The match rules survive both codecs: the time limit, the map's own respawn and the filtered ones, a blitz's
/// lives, rage, and every kill reward with its items, filter and kit.
/// </summary>
public sealed class MatchRulesRoundTripTests
{
    internal const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Rules Map</name>
          <version>1.0.0</version>
          <objective>Fight</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <kits>
            <kit id="streak"><item slot="3" material="golden apple"/></kit>
          </kits>
          <time result="score" overtime="1m" max-overtime="5m">10m</time>
          <respawn delay="3s" auto="true"/>
          <respawn spectate="true"/>
          <respawns delay="5s">
            <respawn filter="only-red"/>
            <respawn delay="7s"><filter><team>blue</team></filter></respawn>
          </respawns>
          <blitz join-filter="never">
            <lives>2</lives>
            <broadcastLives>false</broadcastLives>
          </blitz>
          <rage/>
          <kill-rewards>
            <kill-reward>
              <item material="arrow" amount="8"/>
            </kill-reward>
            <kill-reward kit="streak">
              <filter><kill-streak count="3"/></filter>
            </kill-reward>
          </kill-rewards>
          <killreward>
            <kit><item slot="0" material="iron sword"/></kit>
          </killreward>
          <filters>
            <team id="only-red">red</team>
          </filters>
        </map>
        """;

    private static readonly string[] Tags = ["time", "respawn", "respawns", "blitz", "rage", "kill-rewards", "kits"];

    private static string RulesOf(MapXml map)
    {
        var root = XDocument.Parse(XmlWriter.ToXml(map)).Root!;
        return string.Join("\n", Tags.SelectMany(tag => root.Elements(tag)).Select(e => e.ToString()));
    }

    [Test]
    public async Task Every_rule_is_read_as_pgm_reads_it()
    {
        var map = MapParser.ParseXmlString(Xml);

        await Assert.That((map.TimeLimit!.Duration, map.TimeLimit.Result, map.TimeLimit.Overtime)).IsEqualTo(("10m", "score", "1m"));
        await Assert.That((map.Respawn!.Delay, map.Respawn.Auto, map.Respawn.Spectate)).IsEqualTo(("3s", true, true))
            .Because("every root respawn merges into the map's own, the later winning");
        await Assert.That(map.Respawns.Select(rule => rule.Delay)).IsEquivalentTo(["5s", "7s"])
            .Because("a group's delay reaches every respawn under it that states none");
        await Assert.That(map.Respawns[0].FilterId).IsEqualTo("only-red");
        await Assert.That((map.Blitz!.Lives, map.Blitz.BroadcastLives, map.Blitz.JoinFilterId)).IsEqualTo(((int?)2, (bool?)false, "never"));
        await Assert.That(map.Rage).IsTrue();
        await Assert.That(map.KillRewards.Count).IsEqualTo(3).Because("both spellings, grouped or not, are rewards");
        await Assert.That(map.KillRewards[0].Items.Single().Amount).IsEqualTo(8);
        await Assert.That(map.KillRewards[1].KitId).IsEqualTo("streak");
        await Assert.That(map.Kits.Any(kit => kit.Id == map.KillRewards[2].KitId)).IsTrue()
            .Because("a kit written inside a reward is one of the map's kits");
    }

    [Test]
    public async Task A_map_writes_the_same_rules_after_the_xml_codec()
    {
        var first = MapParser.ParseXmlString(Xml);

        await Assert.That(RulesOf(MapParser.ParseXmlString(XmlWriter.ToXml(first)))).IsEqualTo(RulesOf(first));
    }

    [Test]
    public async Task A_map_writes_the_same_rules_after_the_json_codec()
    {
        var first = MapParser.ParseXmlString(Xml);

        await Assert.That(RulesOf(Deserializer.FromDict(Serializer.ToDict(first)))).IsEqualTo(RulesOf(first));
    }

    [Test]
    public async Task A_kit_written_inside_a_reward_is_written_there_again()
    {
        var root = XDocument.Parse(XmlWriter.ToXml(MapParser.ParseXmlString(Xml))).Root!;

        await Assert.That(root.Element("kits")!.Elements("kit").Count()).IsEqualTo(1);
        await Assert.That(root.Element("kill-rewards")!.Elements("kill-reward").Last().Element("kit")!.Element("item")).IsNotNull();
    }

    [Test]
    public async Task A_legacy_time_limit_is_read_and_written_at_the_root()
    {
        var map = MapParser.ParseXmlString("""
            <?xml version="1.0"?>
            <map proto="1.4.0"><name>Old</name><version>1.0.0</version><objective>o</objective>
              <score><limit>50</limit><time>5m</time></score>
            </map>
            """);
        var root = XDocument.Parse(XmlWriter.ToXml(map)).Root!;

        await Assert.That(map.TimeLimit!.Duration).IsEqualTo("5m");
        await Assert.That(root.Element("time")!.Value).IsEqualTo("5m");
        await Assert.That(root.Element("score")!.Element("time")).IsNull();
    }

    [Test]
    public async Task A_map_with_no_rules_writes_none()
    {
        var map = MapParser.ParseXmlString("""
            <?xml version="1.0"?>
            <map proto="1.5.0"><name>Plain</name><version>1.0.0</version><objective>o</objective></map>
            """);

        await Assert.That(Serializer.EncodeMatchRules(map)).IsEmpty();
        await Assert.That(RulesOf(map)).IsEqualTo("");
    }
}
