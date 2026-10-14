using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A flag survives the XML codec unchanged: its posts, single and composite, its nets, nested and declared
/// beside it, and the filters and regions each names or writes inline. Nothing the map left unset comes back
/// set, and the attributes a <c>&lt;flags&gt;</c> group hands down reach the flags under it.
/// </summary>
public sealed class FlagRoundTripTests
{
    internal const string KingOfTheFlag = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Flag Map</name>
          <version>1.0.0</version>
          <gamemode>kotf</gamemode>
          <objective>Hold the flag</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <kits>
            <kit id="flag-kit"><max-health>12</max-health></kit>
            <kit id="reset-health"><max-health>20</max-health></kit>
          </kits>
          <flags flag-proximity-metric="none">
            <flags shared="true">
              <flag id="flag" name="Flag" color="yellow" points-rate="1" pickup-kit="flag-kit" drop-kit="reset-health">
                <post return-time="0s" respawn-time="15s">
                  <post name="Mid Post" respawn-filter="mid-open">11.5,9,24.5</post>
                  <post name="East Post" yaw="-90">36.5,14,24.5</post>
                  <post name="West Post" yaw="90">-13.5,14,24.5</post>
                </post>
              </flag>
            </flags>
          </flags>
          <score><limit>300</limit></score>
          <filters>
            <time id="mid-open">1m</time>
          </filters>
        </map>
        """;

    internal const string CaptureTheFlag = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>CTF Map</name>
          <version>1.0.0</version>
          <objective>Take their flag home</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <flags>
            <post id="red-home" owner="red">-40.5,10,0.5</post>
            <flag id="red-flag" owner="red" color="red" post="red-home" pickup-filter="only-blue">
              <carry-message>Bring it home!</carry-message>
              <net points="1" respawn-together="true">
                <region><cuboid min="40,10,-2" max="43,13,3"/></region>
              </net>
            </flag>
            <flag id="blue-flag" owner="blue" color="blue">
              <post owner="blue" recover-time="15s"><block>40,10,0</block></post>
            </flag>
            <net region="red-net" flags="red-flag blue-flag" post="red-home">
              <capture-filter>
                <not><flag-carried post="red-home">red-flag</flag-carried></not>
              </capture-filter>
            </net>
          </flags>
          <filters>
            <team id="only-blue">blue</team>
          </filters>
          <regions>
            <cuboid id="red-net" min="-43,10,-2" max="-40,13,3"/>
          </regions>
        </map>
        """;

    private static MapXml Again(string xml) => MapParser.ParseXmlString(XmlWriter.ToXml(MapParser.ParseXmlString(xml)));

    [Test]
    public async Task A_king_of_the_flag_map_survives_with_its_three_posts()
    {
        var map = Again(KingOfTheFlag);

        var flag = map.Flags.Single();
        await Assert.That(flag.Id).IsEqualTo("flag");
        await Assert.That(flag.Owner).IsEqualTo("");
        await Assert.That(flag.PointsRate).IsEqualTo(1.0);
        await Assert.That(flag.Shared).IsTrue().Because("the inner group hands it down");
        await Assert.That(flag.FlagProximityMetric).IsEqualTo("none").Because("the outer group hands it down");
        await Assert.That(flag.PickupKitId).IsEqualTo("flag-kit");
        await Assert.That(flag.Required).IsNull();

        var post = flag.Post!;
        await Assert.That(post.IsComposite).IsTrue();
        await Assert.That(post.Posts.Select(child => child.Name)).IsEquivalentTo(new[] { "Mid Post", "East Post", "West Post" });
        await Assert.That(post.Posts.All(child => child.RespawnTime == "15s" && child.RecoverTime == "0s")).IsTrue()
            .Because("each place inherits the composite's times");
        await Assert.That(post.Posts[0].RespawnFilterId).IsEqualTo("mid-open");
        await Assert.That(post.Posts[0].Point).IsEqualTo(new PgmStudio.Geom.Vec3(11.5, 9, 24.5));
        await Assert.That(post.Posts[1].Yaw).IsEqualTo("-90");
        await Assert.That(post.Posts[0].Id).IsEqualTo("").Because("a child does not take its composite's id");
    }

    [Test]
    public async Task A_flag_map_is_ctf_by_its_module_and_kotf_by_what_it_states()
    {
        var map = Again(KingOfTheFlag);

        await Assert.That(map.Gamemodes).Contains(Gamemodes.Ctf);
        await Assert.That(map.DeclaredGamemode).IsEquivalentTo(new[] { "kotf" });
    }

    [Test]
    public async Task A_capture_the_flag_map_survives_with_its_nets_and_inline_parts()
    {
        var map = Again(CaptureTheFlag);

        await Assert.That(map.FlagPosts.Single().Id).IsEqualTo("red-home");
        var red = map.Flags.Single(flag => flag.Id == "red-flag");
        await Assert.That(red.PostId).IsEqualTo("red-home");
        await Assert.That(red.Post).IsNull();
        await Assert.That(red.PickupFilterId).IsEqualTo("only-blue");
        await Assert.That(red.CarryMessage).IsEqualTo("Bring it home!");
        var nested = red.Nets.Single();
        await Assert.That(nested.Points).IsEqualTo(1.0);
        await Assert.That(nested.RespawnTogether).IsTrue();
        await Assert.That(map.Regions[nested.RegionId].Type).IsEqualTo("cuboid");

        var blue = map.Flags.Single(flag => flag.Id == "blue-flag");
        await Assert.That(blue.Post!.RecoverTime).IsEqualTo("15s");
        await Assert.That(map.Regions[blue.Post.RegionIds.Single()].Type).IsEqualTo("block");

        var net = map.FlagNets.Single();
        await Assert.That(net.RegionId).IsEqualTo("red-net");
        await Assert.That(net.Flags).IsEquivalentTo(new[] { "red-flag", "blue-flag" });
        await Assert.That(net.PostId).IsEqualTo("red-home");
        var capture = map.Filters[net.CaptureFilterId];
        await Assert.That(capture.Type).IsEqualTo("not");
        var carried = map.Filters[capture.Child!];
        await Assert.That((carried.Type, carried.Objective, carried.Post)).IsEqualTo(("flag-carried", "red-flag", "red-home"));
    }

    [Test]
    public async Task A_flag_whose_kit_is_written_inside_it_is_refused()
    {
        var xml = KingOfTheFlag.Replace("""pickup-kit="flag-kit" """, "")
            .Replace("<post return-time", "<pickup-kit><max-health>12</max-health></pickup-kit><post return-time");

        await Assert.That(() => MapParser.ParseXmlString(xml)).Throws<UnsupportedMapException>();
    }

    [Test]
    [Arguments(KingOfTheFlag)]
    [Arguments(CaptureTheFlag)]
    public async Task A_flag_map_writes_the_same_xml_after_the_json_codec(string xml)
    {
        var parsed = MapParser.ParseXmlString(xml);

        var again = Deserializer.FromDict(Serializer.ToDict(parsed));

        await Assert.That(XmlWriter.ToXml(again)).IsEqualTo(XmlWriter.ToXml(parsed));
    }
}
