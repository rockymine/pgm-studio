using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>Resource-block renewables (<see cref="ResourceRenewables"/>) for iron/gold/diamond: a tight
/// renewable region per type, reusing the spawns when the ore lives there (with the relaxed spawn
/// protection) or a new per-cluster region otherwise.</summary>
public sealed class ResourceRenewablesTests
{
    private static Region Rect(string id, int minX, int minZ, int maxX, int maxZ) => new()
    {
        Id = id, Type = "rectangle", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
        Bounds2d = Bounds2d.Of(minX, minZ, maxX, maxZ),
    };

    private static MapXml WithSpawns()
    {
        var m = new MapXml();
        m.Regions["red-spawn"] = Rect("red-spawn", -10, -10, 10, 10);
        m.Regions["blue-spawn"] = Rect("blue-spawn", 20, -10, 40, 10);
        m.ApplyRules.Add(new ApplyRule { EnterFilter = "only-red", RegionId = "red-spawn", Message = "spawn" });
        m.ApplyRules.Add(new ApplyRule { BlockFilter = "never", RegionId = "red-spawn", Message = "spawn" });
        m.ApplyRules.Add(new ApplyRule { EnterFilter = "only-blue", RegionId = "blue-spawn", Message = "spawn" });
        m.ApplyRules.Add(new ApplyRule { BlockFilter = "never", RegionId = "blue-spawn", Message = "spawn" });
        return m;
    }

    // ── branch B: ore elsewhere → per-cluster rects + union ──
    [Test]
    public async Task Iron_outside_spawns_builds_tight_clustered_region()
    {
        var m = new MapXml();
        var iron = new List<(string, int, int, int)>
        {
            ("iron_block", -59, 8, 7), ("iron_block", -59, 9, 7),
            ("iron_block", 51, 10, 6), ("iron_block", 52, 10, 6), ("iron_block", 53, 10, 7),
        };
        ResourceRenewables.Apply(m, iron);

        await Assert.That(m.Regions["iron-renewable"].Type).IsEqualTo("union");
        await Assert.That(m.Regions["iron-renewable"].Children!.Count).IsEqualTo(2);   // two clusters
        await Assert.That((m.Regions["iron-renewable-1"].MinX, m.Regions["iron-renewable-1"].MaxX)).IsEqualTo(((double?)-59, (double?)-59));
        await Assert.That(m.Filters.ContainsKey("only-iron")).IsTrue();
        await Assert.That(m.Filters.ContainsKey("only-air")).IsTrue();
        await Assert.That(m.Renewables.Single().RegionId).IsEqualTo("iron-renewable");
        await Assert.That(m.Renewables.Single().AvoidPlayers).IsEqualTo(2);
        await Assert.That(m.Filters.ContainsKey("only-iron-cause-world")).IsFalse();   // no spawn relax
    }

    [Test]
    public async Task Single_cluster_is_a_plain_rectangle_no_union()
    {
        var m = new MapXml();
        ResourceRenewables.Apply(m, [("iron_block", 10, 8, 10), ("iron_block", 11, 8, 10), ("iron_block", 12, 8, 11)]);
        await Assert.That(m.Regions["iron-renewable"].Type).IsEqualTo("rectangle");
        await Assert.That(m.Renewables.Single().RegionId).IsEqualTo("iron-renewable");
    }

    // ── branch A: ore in spawns → reuse spawns + relax protection ──
    [Test]
    public async Task Iron_in_spawns_reuses_spawns_and_relaxes_protection()
    {
        var m = WithSpawns();
        ResourceRenewables.Apply(m, [("iron_block", 0, 8, 0), ("iron_block", 30, 8, 0)]);

        await Assert.That(m.Regions["spawns"].Children).IsEquivalentTo(new[] { "red-spawn", "blue-spawn" });
        await Assert.That(m.ApplyRules.Any(r => r.BlockFilter == "never")).IsFalse();
        var relaxed = m.ApplyRules.Single(r => r.RegionId == "spawns");
        await Assert.That(relaxed.BlockBreakFilter).IsEqualTo("only-iron");
        await Assert.That(relaxed.BlockPlaceFilter).IsEqualTo("only-iron-cause-world");
        await Assert.That(m.ApplyRules.Count(r => r.EnterFilter.StartsWith("only-"))).IsEqualTo(2);   // enter rules survive
        await Assert.That(m.Filters["only-iron-cause-world"].Type).IsEqualTo("all");
        await Assert.That(m.Renewables.Single().RegionId).IsEqualTo("spawns");
    }

    // ── generalized: iron in spawns, gold + diamond in the middle ──
    [Test]
    public async Task Mixed_resources_get_a_renewable_each_with_the_right_region()
    {
        var m = WithSpawns();
        ResourceRenewables.Apply(m,
        [
            ("iron_block", 0, 8, 0), ("iron_block", 30, 8, 0),         // both in spawns
            ("gold_block", 100, 8, 100), ("gold_block", 101, 8, 100),  // middle
            ("diamond_block", -100, 8, -100),                          // middle
        ]);

        // one renewable per ore type, in iron/gold/diamond order
        var byFilter = m.Renewables.ToDictionary(r => r.RenewFilter, r => r.RegionId);
        await Assert.That(byFilter["only-iron"]).IsEqualTo("spawns");      // iron reuses the spawns
        await Assert.That(byFilter["only-gold"]).IsEqualTo("gold-renewable");
        await Assert.That(byFilter["only-diamond"]).IsEqualTo("diamond-renewable");
        await Assert.That(m.Regions["gold-renewable"].Type).IsEqualTo("rectangle");
        await Assert.That(m.Regions["diamond-renewable"].Type).IsEqualTo("rectangle");

        // spawn relaxation only mentions the in-spawn ore (iron)
        var relaxed = m.ApplyRules.Single(r => r.RegionId == "spawns");
        await Assert.That(relaxed.BlockBreakFilter).IsEqualTo("only-iron");

        // serializes + re-parses with all three renewables
        m.Name = "T"; m.Version = "1.0.0";
        var reparsed = PgmStudio.Pgm.Serializer.ToDict(PgmStudio.Pgm.MapParser.ParseXmlString(PgmStudio.Pgm.XmlWriter.ToXml(m)));
        await Assert.That(((List<object?>)reparsed["renewables"]!).Count).IsEqualTo(3);
    }
}
