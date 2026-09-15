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
/// taking coordinates, so the slice mints three per spawner — where the stack lands, how near a player has to
/// be standing, and the ground nobody may take — and a regeneration replaces exactly those.</para>
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

    /// <summary><b>The drop is the centre of the pad the stack lands on</b> (<c>WX5</c>): the marker names a
    /// square of ground and the point follows it, rather than the two being derived apart. A whole number is
    /// the corner four blocks share, so the pad is those four and its centre is the corner itself.</summary>
    [Test]
    public async Task The_drop_is_the_centre_of_the_pad_the_stack_lands_on()
    {
        var regions = Regions(Generate(Mint("iron", x: 30, z: -18)));
        var drop = (Dict)regions["iron" + SpawnerGenerator.DropSuffix]!;

        await Assert.That(drop["type"]).IsEqualTo("point");
        var position = (Dict)drop["position"]!;
        await Assert.That(position["x"]).IsEqualTo(30d);
        await Assert.That(position["y"]).IsEqualTo(12d);
        await Assert.That(position["z"]).IsEqualTo(-18d);
    }

    /// <summary>And a marker at a block's own centre names that one block, so the drop stays on it.</summary>
    [Test]
    public async Task A_marker_at_a_blocks_centre_drops_onto_that_block()
    {
        var regions = Regions(Generate(Mint("iron", x: 30.5, z: -17.5)));
        var position = (Dict)((Dict)regions["iron" + SpawnerGenerator.DropSuffix]!)["position"]!;

        await Assert.That(position["x"]).IsEqualTo(30.5);
        await Assert.That(position["z"]).IsEqualTo(-17.5);
    }

    /// <summary>A marker whose axes disagree has no square pad, so it is nudged onto one parity first — the
    /// same half-block the composer moves a room's marker by.</summary>
    [Test]
    public async Task A_mixed_parity_marker_is_nudged_onto_one_parity()
    {
        await Assert.That(SpawnerGenerator.Drop(new Pt(30, 12, -17.5))).IsEqualTo((30d, -18d));
    }

    /// <summary>The clock runs while somebody is standing by rather than all match: the reach is a cylinder
    /// based on the drop, of the stated radius and the corpus's own height.</summary>
    [Test]
    public async Task The_reach_is_a_cylinder_standing_on_the_drop()
    {
        var regions = Regions(Generate(Mint("iron", x: 30, z: -18)));
        var reach = (Dict)regions["iron" + SpawnerGenerator.ReachSuffix]!;

        await Assert.That(reach["type"]).IsEqualTo("cylinder");
        await Assert.That(reach["radius"]).IsEqualTo(SpawnerIntent.TypicalReach);
        await Assert.That(reach["height"]).IsEqualTo((double)SpawnerIntent.ReachHeight);
        var at = (Dict)reach["base"]!;
        await Assert.That(at["x"]).IsEqualTo(30d);
        await Assert.That(at["z"]).IsEqualTo(-18d);
    }

    /// <summary>Each spawner names its own reach, so two generators on one board are two places to stand
    /// rather than one.</summary>
    [Test]
    public async Task Each_spawner_names_its_own_reach()
    {
        var doc = Generate(Mint("a"), Mint("b", x: 40));

        await Assert.That(Spawners(doc).Select(entry => ((Dict)entry!)["player_region"]))
            .IsEquivalentTo(new object?[] { "a" + SpawnerGenerator.ReachSuffix, "b" + SpawnerGenerator.ReachSuffix });
    }

    // ── the ground nobody may take ───────────────────────────────────────────────────
    /// <summary>A generator whose block can be mined out or walled in is one anyone can switch off, so the
    /// ground around the drop is kept: a square centred on it, under one rule.</summary>
    [Test]
    public async Task The_ground_around_a_drop_is_kept_under_one_rule()
    {
        var doc = Generate(Mint("mid", x: 0, z: 0));
        var keep = (Dict)Regions(doc)["mid" + SpawnerGenerator.KeepSuffix]!;

        await Assert.That(keep["type"]).IsEqualTo("cuboid");
        var low = (Dict)keep["min"]!;
        var high = (Dict)keep["max"]!;
        // Six blocks a side about the drop, leaning one further along +X the way every even-sided box on a
        // marker does, and five tall about its own y — both ends inside, which is the span
        // `mame_i_shrunk_the_pvpers` writes around its gold-nugget drops.
        await Assert.That(low["x"]).IsEqualTo(-2d);
        await Assert.That(high["x"]).IsEqualTo(3d);
        await Assert.That(low["y"]).IsEqualTo(10d);
        await Assert.That(high["y"]).IsEqualTo(14d);

        var rule = ((List<object?>)doc["apply_rules"]!).OfType<Dict>()
            .Single(applied => applied.GetValueOrDefault("region") as string == SpawnerGenerator.KeptRegionId);
        await Assert.That(rule["block"]).IsEqualTo("never");
    }

    /// <summary>Two generators are one rule over both, because the sentence a player reads is the same at
    /// either of them.</summary>
    [Test]
    public async Task Two_generators_are_kept_by_one_rule_over_both()
    {
        var doc = Generate(Mint("a"), Mint("b", x: 40));
        var union = (Dict)Regions(doc)[SpawnerGenerator.KeptRegionId]!;

        await Assert.That(union["type"]).IsEqualTo("union");
        await Assert.That((List<object?>)union["children"]!)
            .IsEquivalentTo(new object?[] { "a" + SpawnerGenerator.KeepSuffix, "b" + SpawnerGenerator.KeepSuffix });
        await Assert.That(((List<object?>)doc["apply_rules"]!).Count).IsEqualTo(1);
    }

    /// <summary>A board holding the ground some other way says so, and gets the generator without the
    /// rule.</summary>
    [Test]
    public async Task A_spawner_that_keeps_nothing_writes_no_rule()
    {
        var doc = Generate(Mint() with { Protect = 0 });

        await Assert.That(Regions(doc).ContainsKey("mid" + SpawnerGenerator.KeepSuffix)).IsFalse();
        await Assert.That(Regions(doc).ContainsKey(SpawnerGenerator.KeptRegionId)).IsFalse();
        await Assert.That(doc.ContainsKey("apply_rules")).IsFalse();
        await Assert.That(Spawners(doc).Count).IsEqualTo(1);
    }

    /// <summary>A generator with nothing to drop produces nothing, so it is left out rather than written as
    /// an element that fires forever and hands over no item.</summary>
    [Test]
    public async Task A_spawner_with_nothing_to_drop_is_not_written()
    {
        var doc = Generate(Mint() with { Drops = [] });

        await Assert.That(doc.ContainsKey("spawners")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey(SpawnerGenerator.KeptRegionId)).IsFalse();
    }

    /// <summary>Regenerating replaces rather than appends — the entry, all three of its regions, the union
    /// over them and the rule written on it.</summary>
    [Test]
    public async Task A_board_stating_no_spawner_leaves_none_behind()
    {
        var doc = Generate(Mint());
        SpawnerGenerator.Apply(doc, new MapIntent());

        await Assert.That(doc.ContainsKey("spawners")).IsFalse();
        foreach (var suffix in new[] { SpawnerGenerator.DropSuffix, SpawnerGenerator.ReachSuffix, SpawnerGenerator.KeepSuffix })
            await Assert.That(Regions(doc).ContainsKey("mid" + suffix)).IsFalse();
        await Assert.That(Regions(doc).ContainsKey(SpawnerGenerator.KeptRegionId)).IsFalse();
        await Assert.That(((List<object?>)doc["apply_rules"]!)).IsEmpty();
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
        await Assert.That(spawner.PlayerRegion).IsEqualTo("emeralds" + SpawnerGenerator.ReachSuffix);
        await Assert.That(map.ApplyRules.Single(rule => rule.RegionId == SpawnerGenerator.KeptRegionId)
            .BlockFilter).IsEqualTo("never");
        await Assert.That(spawner.Delay).IsEqualTo("30s");
        await Assert.That(spawner.MaxEntities).IsEqualTo(8);
        await Assert.That(spawner.Items.Single().Material).IsEqualTo("emerald");
    }
}
