using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The King of the Flag slice: one unowned flag paying its rate to whoever carries it, over a post that brings
/// it back to one of its places at random, and the kits carrying it gives and dropping it undoes.
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
}
