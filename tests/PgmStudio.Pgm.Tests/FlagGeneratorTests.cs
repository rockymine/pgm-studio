using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The flag slice. King of the Flag: one unowned flag paying its rate to whoever carries it, over a post that
/// brings it back to one of its places at random, and the kits carrying it gives and dropping it undoes. Capture
/// the Flag: a team's flag, carried off by any other team and captured in that team's net round its own flag.
/// </summary>
public sealed class FlagGeneratorTests
{
    private static FlagIntent Hill(string carrier = FlagCarriers.Lowered, int hearts = ObjectiveDefaults.FlagCarrierHearts) => new()
    {
        Name = "Flag",
        Color = "yellow",
        Carrier = carrier,
        CarrierHearts = hearts,
        Posts =
        [
            new FlagPostIntent { Name = "Mid", At = new Pt(0.5, 9, 0.5) },
            new FlagPostIntent { Name = "East", At = new Pt(24.5, 14, 0.5), Yaw = -90 },
            new FlagPostIntent { Name = "West", At = new Pt(-23.5, 14, 0.5), Yaw = 90 },
        ],
    };

    private static MapXml Written(MapIntent intent)
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        return MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
    }

    private static Kit KitOf(MapXml map, string id) => map.Kits.Single(kit => kit.Id == id);

    [Test]
    public async Task A_king_of_the_flag_board_writes_one_unowned_flag_over_its_three_places()
    {
        var map = Written(new MapIntent { Meta = new MetaIntent { Name = "Flag Hill" }, Flags = [Hill()] });

        var flag = map.Flags.Single();
        await Assert.That(flag.Owner).IsEqualTo("");
        await Assert.That(flag.Color).IsEqualTo("yellow");
        await Assert.That(flag.PointsRate).IsEqualTo(ObjectiveDefaults.FlagPointsRate);
        await Assert.That(flag.Post!.Posts.Select(post => post.Name)).IsEquivalentTo(new[] { "Mid", "East", "West" });
        await Assert.That(flag.Post.Sequential).IsNull().Because("the next post is chosen at random");
        await Assert.That(flag.Post.Posts[1].Yaw).IsEqualTo("-90");
        await Assert.That(map.Score!.Limit).IsEqualTo(ObjectiveDefaults.FlagScoreLimit);
        await Assert.That(map.DeclaredGamemode).IsEquivalentTo(new[] { Gamemodes.Kotf });
        await Assert.That(map.Objective).IsEqualTo("Hold the flag!");
    }

    [Test]
    public async Task A_lowered_carrier_keeps_its_hearts_and_dropping_the_flag_restores_them()
    {
        var map = Written(new MapIntent { Flags = [Hill(FlagCarriers.Lowered, hearts: 7)] });

        var flag = map.Flags.Single();
        await Assert.That(KitOf(map, flag.PickupKitId).MaxHealth).IsEqualTo(14.0);
        await Assert.That(KitOf(map, flag.DropKitId).MaxHealth).IsEqualTo(20.0);
    }

    [Test]
    public async Task An_absorbed_carrier_is_shielded_for_three_seconds_and_needs_no_drop_kit()
    {
        var map = Written(new MapIntent { Flags = [Hill(FlagCarriers.Absorption)] });

        var flag = map.Flags.Single();
        var effect = KitOf(map, flag.PickupKitId).Effects.Single();
        await Assert.That((effect.Type, effect.Amplifier)).IsEqualTo(("absorption", 2));
        await Assert.That(flag.DropKitId).IsEqualTo("");
    }

    [Test]
    public async Task A_boosted_carrier_is_raised_to_its_hearts_and_a_forced_drop_kit_takes_the_boost_away()
    {
        var map = Written(new MapIntent { Flags = [Hill(FlagCarriers.Boost, hearts: 14)] });

        var flag = map.Flags.Single();
        var boost = KitOf(map, flag.PickupKitId).Effects.Single();
        await Assert.That((boost.Type, boost.Amplifier)).IsEqualTo(("health boost", 1)).Because("two levels: 28 health");
        var drop = KitOf(map, flag.DropKitId);
        await Assert.That(drop.Force).IsTrue();
        await Assert.That(drop.Effects.Single().Duration).IsEqualTo("0");
    }

    [Test]
    public async Task Applying_twice_writes_the_flag_and_its_kits_once()
    {
        var doc = new Dict();
        var intent = new MapIntent { Flags = [Hill()] };
        FlagGenerator.Apply(doc, intent);
        FlagGenerator.Apply(doc, intent);

        await Assert.That(((List<object?>)doc["flags"]!).Count).IsEqualTo(1);
        await Assert.That(((List<object?>)doc["kits"]!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_null_slice_leaves_the_map_flags_alone()
    {
        var doc = new Dict { ["flags"] = new List<object?> { new Dict { ["id"] = "theirs" } } };

        FlagGenerator.Apply(doc, new MapIntent());

        await Assert.That(((List<object?>)doc["flags"]!).Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_post_off_the_centre_gains_its_image_and_the_centre_post_stays_one()
    {
        var intent = new MapIntent
        {
            Symmetry = new SymmetryIntent { Mode = "rot_180" },
            Teams = [new TeamDef { Id = "red", Name = "Red", Color = "red" }, new TeamDef { Id = "blue", Name = "Blue", Color = "blue" }],
            // The centre of a rot_180 about (0, 0) is the point (0, 0) itself.
            Flags = [Hill() with { Posts = [new FlagPostIntent { Name = "Mid", At = new Pt(0, 9, 0) }, Hill().Posts[1]] }],
        };

        var posts = SymmetryExpander.Expand(intent).Flags!.Single().Posts;

        await Assert.That(posts.Count).IsEqualTo(3);
        var image = posts[2];
        await Assert.That((image.At.X, image.At.Z)).IsEqualTo((-24.5, -0.5));
        await Assert.That(image.Yaw).IsEqualTo(90.0);
    }

    private static readonly TeamDef[] Teams =
    [
        new() { Id = "red-team", Name = "Red", Color = "red" },
        new() { Id = "blue-team", Name = "Blue", Color = "blue" },
    ];

    private static FlagIntent Base(string team, double x) => new()
    {
        Team = team,
        Posts = [new FlagPostIntent { Name = "Base", At = new Pt(x + 0.5, 12, 0.5) }],
    };

    [Test]
    public async Task A_team_flag_is_owned_coloured_for_its_team_and_its_own_team_cannot_take_it()
    {
        var map = Written(new MapIntent
        {
            Meta = new MetaIntent { Name = "Two Bases" }, Teams = [.. Teams], Flags = [Base("red-team", -40), Base("blue-team", 40)],
        });

        var red = map.Flags.Single(flag => flag.Owner == "red-team");
        await Assert.That((red.Name, red.Color)).IsEqualTo(("Red Flag", "red"));
        await Assert.That(red.PointsRate).IsNull().Because("a team's flag scores on capture, not while carried");
        await Assert.That(red.PickupKitId).IsEmpty().Because("carrying a team's flag does nothing unless stated");
        await Assert.That(red.Post!.RecoverTime).IsEqualTo(ObjectiveDefaults.FlagRecoverTime);
        var takers = map.Filters[red.PickupFilterId];
        await Assert.That((takers.Type, takers.Child)).IsEqualTo(("not", "red-team"));
        await Assert.That(map.DeclaredGamemode).IsEquivalentTo(new[] { Gamemodes.Ctf });
        await Assert.That(map.Objective).IsEqualTo("Capture the enemy's flag!");
        await Assert.That(map.Score!.Limit).IsEqualTo(ObjectiveDefaults.CtfScoreLimit);
    }

    [Test]
    public async Task Each_team_captures_every_other_team_flag_in_a_net_round_its_own()
    {
        var map = Written(new MapIntent { Teams = [.. Teams], Flags = [Base("red-team", -40), Base("blue-team", 40)] });

        await Assert.That(map.FlagNets.Count).IsEqualTo(2);
        var redFlag = map.Flags.Single(flag => flag.Owner == "red-team").Id;
        var blueNet = map.FlagNets.Single(net => net.Flags.SequenceEqual([redFlag]));
        var region = map.Regions[blueNet.RegionId];
        await Assert.That((region.MinX, region.MaxX)).IsEqualTo(((double?)38, (double?)43))
            .Because("blue's net reaches two blocks either side of blue's own post, at x 40");
        await Assert.That((region.MinY, region.MaxY)).IsEqualTo(((double?)12, (double?)15));
        await Assert.That(blueNet.Points).IsEqualTo(ObjectiveDefaults.FlagNetPoints);
    }

    [Test]
    public async Task A_team_flag_fans_to_every_other_team_with_its_net()
    {
        var map = Written(new MapIntent
        {
            Teams = [.. Teams],
            Symmetry = new SymmetryIntent { Mode = "rot_180" },
            Flags = [Base("red-team", -40) with { Net = new BlockBox(-45, 12, -2, -41, 14, 2) }],
        });

        var blue = map.Flags.Single(flag => flag.Owner == "blue-team");
        await Assert.That((blue.Name, blue.Color)).IsEqualTo(("Blue Flag", "blue"));
        await Assert.That(blue.Post!.Point!.Value.X).IsEqualTo(39.5);
        var redNet = map.Regions[map.FlagNets.Single(net => net.Flags.SequenceEqual([blue.Id])).RegionId];
        await Assert.That((redNet.MinX, redNet.MaxX)).IsEqualTo(((double?)-45, (double?)-40));
        var blueNet = map.Regions[map.FlagNets.Single(net => net.Flags.SequenceEqual([map.Flags.Single(flag => flag.Owner == "red-team").Id])).RegionId];
        // Red's blocks -45..-41 turned about the grid line x = 0 are blocks 40..44, written to an exclusive max.
        await Assert.That((blueNet.MinX, blueNet.MaxX)).IsEqualTo(((double?)40, (double?)45));
    }

    [Test]
    public async Task A_rebuild_writes_the_nets_and_the_pickup_filters_once()
    {
        var doc = new Dict();
        var intent = new MapIntent { Teams = [.. Teams], Flags = [Base("red-team", -40), Base("blue-team", 40)] };
        IntentGenerator.Apply(doc, intent);
        IntentGenerator.Apply(doc, intent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        await Assert.That(map.FlagNets.Count).IsEqualTo(2);
        await Assert.That(map.Filters.Keys.Count(id => id.EndsWith("-takers", StringComparison.Ordinal))).IsEqualTo(2);
        await Assert.That(map.Regions.Keys.Count(id => id.EndsWith("-net", StringComparison.Ordinal))).IsEqualTo(2);
    }

    [Test]
    public async Task A_lone_team_flag_has_no_net_since_a_net_naming_no_flag_takes_every_flag()
    {
        var map = Written(new MapIntent { Teams = [.. Teams], Flags = [Base("red-team", -40)] });

        await Assert.That(map.FlagNets).IsEmpty();
    }

    [Test]
    public async Task A_team_capturing_only_at_home_captures_while_its_own_flag_stands_at_its_post()
    {
        var map = Written(new MapIntent
        {
            Teams = [.. Teams],
            Symmetry = new SymmetryIntent { Mode = "rot_180" },
            Flags = [Base("red-team", -40) with { Capture = FlagCaptures.Home }],
        });

        foreach (var flag in map.Flags)
        {
            var net = map.FlagNets.Single(net => !net.Flags.Contains(flag.Id));
            var home = map.Filters[net.CaptureFilterId];
            await Assert.That((home.Type, home.Objective)).IsEqualTo(("flag-returned", flag.Id))
                .Because("a team's net captures only while that team's own flag is home, and the rule fans with the flag");
        }
    }

    [Test]
    public async Task A_team_capturing_anytime_states_no_capture_filter()
    {
        var map = Written(new MapIntent { Teams = [.. Teams], Flags = [Base("red-team", -40), Base("blue-team", 40)] });

        await Assert.That(map.FlagNets.All(net => net.CaptureFilterId.Length == 0)).IsTrue();
    }

    [Test]
    public async Task A_team_with_two_flags_captures_while_both_are_home_and_a_rebuild_writes_the_filters_once()
    {
        var doc = new Dict();
        var intent = new MapIntent
        {
            Teams = [.. Teams],
            Flags =
            [
                Base("red-team", -40) with { Name = "Red North", Capture = FlagCaptures.Home },
                Base("red-team", -50) with { Name = "Red South" },
                Base("blue-team", 40),
            ],
        };
        IntentGenerator.Apply(doc, intent);
        IntentGenerator.Apply(doc, intent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        var redNet = map.FlagNets.Single(net => net.CaptureFilterId.Length > 0);
        var home = map.Filters[redNet.CaptureFilterId];
        await Assert.That(home.Type).IsEqualTo("all");
        await Assert.That(home.Children!.Count).IsEqualTo(2);
        await Assert.That(map.Filters.Values.Count(filter => filter.Type == "flag-returned")).IsEqualTo(2);
    }
}
