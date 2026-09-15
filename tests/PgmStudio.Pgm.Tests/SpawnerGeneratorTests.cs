using PgmStudio.Geom;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The spawner slice: what a board mints its currency with. A shop is priced in a material no spawn kit
/// carries — 764 of the corpus's 907 icons are — and a generator is where that material comes from.
///
/// <para>The part with a rule behind it is the regions. PGM's element references them by id rather than
/// taking coordinates, so the slice mints a point for the drop and one shared <c>everywhere</c> for who has
/// to be standing near, and a regeneration replaces exactly those.</para>
/// </summary>
public sealed class SpawnerGeneratorTests
{
    private static SpawnerIntent Mint(string id = "mid", double x = 0, double z = 0) => new()
    {
        Id = id, At = new Pt(x, 12, z), Drops = [new SpawnerDrop("emerald")],
    };

    private static Dict Generate(params SpawnerIntent[] spawners)
    {
        var doc = new Dict();
        SpawnerGenerator.Apply(doc, new MapIntent { Spawners = [.. spawners] });
        return doc;
    }

    private static List<object?> Spawners(Dict doc) => (List<object?>)doc["spawners"]!;
    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;
    private static Dict At(List<object?> list, int index) => (Dict)list[index]!;

    [Test]
    public async Task A_spawner_states_its_drop_its_rate_and_its_cap()
    {
        var spawner = At(Spawners(Generate(Mint())), 0);

        await Assert.That(spawner["delay"]).IsEqualTo(SpawnerIntent.MedianDelay);
        await Assert.That(spawner["max_entities"]).IsEqualTo(SpawnerIntent.TypicalMaxEntities);
        var drop = (Dict)((List<object?>)spawner["items"]!)[0]!;
        await Assert.That(drop["material"]).IsEqualTo("emerald");
        await Assert.That(drop["amount"]).IsEqualTo(1);
    }

    /// <summary>The drop lands on the centre of the block it was stated in, because PGM spawns the stack
    /// exactly where the point says and a whole number puts it on a corner.</summary>
    [Test]
    public async Task The_drop_is_a_point_region_on_the_blocks_centre()
    {
        var regions = Regions(Generate(Mint("iron", x: 30, z: -18)));
        var drop = (Dict)regions["iron" + SpawnerGenerator.DropSuffix]!;

        await Assert.That(drop["type"]).IsEqualTo("point");
        var position = (Dict)drop["position"]!;
        await Assert.That(position["x"]).IsEqualTo(30.5);
        await Assert.That(position["y"]).IsEqualTo(12d);
        await Assert.That(position["z"]).IsEqualTo(-17.5);
    }

    /// <summary>Every spawner takes one shared region as the ground a player has to be standing on, which is
    /// what 444 of the corpus's stated player-regions are.</summary>
    [Test]
    public async Task Every_spawner_takes_the_one_shared_reach_region()
    {
        var doc = Generate(Mint("a"), Mint("b", x: 40));

        await Assert.That(((Dict)Regions(doc)[SpawnerGenerator.ReachRegionId]!)["type"]).IsEqualTo("everywhere");
        await Assert.That(Spawners(doc).Select(entry => ((Dict)entry!)["player_region"]))
            .IsEquivalentTo(new object?[] { SpawnerGenerator.ReachRegionId, SpawnerGenerator.ReachRegionId });
    }

    /// <summary>A generator with nothing to drop produces nothing, so it is left out rather than written as
    /// an element that fires forever and hands over no item.</summary>
    [Test]
    public async Task A_spawner_with_nothing_to_drop_is_not_written()
    {
        var doc = Generate(Mint() with { Drops = [] });

        await Assert.That(doc.ContainsKey("spawners")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey(SpawnerGenerator.ReachRegionId)).IsFalse();
    }

    /// <summary>Regenerating replaces rather than appends — the entry and both of its regions.</summary>
    [Test]
    public async Task A_board_stating_no_spawner_leaves_none_behind()
    {
        var doc = Generate(Mint());
        SpawnerGenerator.Apply(doc, new MapIntent());

        await Assert.That(doc.ContainsKey("spawners")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey("mid" + SpawnerGenerator.DropSuffix)).IsFalse();
        await Assert.That(Regions(doc).ContainsKey(SpawnerGenerator.ReachRegionId)).IsFalse();
    }

    /// <summary>A wool room's own spawner shares the document's one <c>spawners</c> list and names a region of
    /// its own, so this slice leaves it exactly where it stands.</summary>
    [Test]
    public async Task A_wool_rooms_spawner_survives_a_regeneration()
    {
        var doc = Generate(Mint());
        Spawners(doc).Add(new Dict { ["spawn_region"] = "lime-wool-spawn", ["player_region"] = "lime-wool" });

        SpawnerGenerator.Apply(doc, new MapIntent { Spawners = [Mint("iron", x: 60)] });

        var kept = Spawners(doc).Select(entry => ((Dict)entry!)["spawn_region"]).ToList();
        await Assert.That(kept).Contains("lime-wool-spawn");
        await Assert.That(kept).Contains("iron" + SpawnerGenerator.DropSuffix);
        await Assert.That(kept).DoesNotContain("mid" + SpawnerGenerator.DropSuffix);
    }

    /// <summary>The whole slice through the document codec: what the generator writes is what the writer
    /// emits and the parser reads back, regions included.</summary>
    [Test]
    public async Task The_generated_document_emits_a_spawner_and_its_regions()
    {
        var doc = Generate(Mint("emeralds", x: 0, z: 0) with { Delay = "30s", MaxEntities = 8 });
        doc["name"] = "Mint";
        doc["version"] = "1.0.0";

        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
        var spawner = map.Spawners.Single();

        await Assert.That(spawner.SpawnRegion).IsEqualTo("emeralds" + SpawnerGenerator.DropSuffix);
        await Assert.That(spawner.PlayerRegion).IsEqualTo(SpawnerGenerator.ReachRegionId);
        await Assert.That(spawner.Delay).IsEqualTo("30s");
        await Assert.That(spawner.MaxEntities).IsEqualTo(8);
        await Assert.That(spawner.Items.Single().Material).IsEqualTo("emerald");
    }
}
