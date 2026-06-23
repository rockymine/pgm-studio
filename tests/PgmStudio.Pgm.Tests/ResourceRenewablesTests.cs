using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>Resource-block renewables (<see cref="ResourceRenewables"/>): only ore that sits inside a team
/// spawn earns a renewable — the spawns region is reused with relaxed protection (only that ore breakable,
/// only the world replaces it). Ore scanned elsewhere is left as-is (ambiguous intent).</summary>
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

    // ── ore outside every spawn → no renewable, spawn protection untouched ──
    [Test]
    public async Task Ore_outside_spawns_gets_no_renewable()
    {
        var m = WithSpawns();
        ResourceRenewables.Apply(m, [("iron_block", 100, 8, 100), ("iron_block", 101, 8, 100)]);
        await Assert.That(m.Renewables.Count).IsEqualTo(0);
        await Assert.That(m.Filters.ContainsKey("only-iron")).IsFalse();
        await Assert.That(m.ApplyRules.Count(r => r.BlockFilter == "never")).IsEqualTo(2);   // spawn protection intact
    }

    // ── no spawns in the map → nothing to anchor a renewable to ──
    [Test]
    public async Task No_spawns_means_no_renewables()
    {
        var m = new MapXml();
        ResourceRenewables.Apply(m, [("iron_block", 0, 8, 0), ("iron_block", 1, 8, 0)]);
        await Assert.That(m.Renewables.Count).IsEqualTo(0);
    }

    // ── ore in spawns → reuse spawns + relax protection ──
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

    // ── ore straddles spawn + elsewhere → only the in-spawn part renews (on spawns); the loose part is ignored ──
    [Test]
    public async Task Iron_partly_in_spawn_renews_only_the_spawn_part()
    {
        var m = WithSpawns();
        ResourceRenewables.Apply(m, [("iron_block", 0, 8, 0), ("iron_block", 100, 8, 100)]);   // one in red-spawn, one out

        await Assert.That(m.Renewables.Single().RegionId).IsEqualTo("spawns");
        await Assert.That(m.Regions.ContainsKey("iron-renewable")).IsFalse();   // no cluster region for the loose block
        await Assert.That(m.ApplyRules.Any(r => r.BlockFilter == "never")).IsFalse();
        await Assert.That(m.ApplyRules.Single(r => r.RegionId == "spawns").BlockBreakFilter).IsEqualTo("only-iron");
    }

    // ── multiple types: only the in-spawn ones renew; off-spawn types are ignored ──
    [Test]
    public async Task Only_in_spawn_resources_get_a_renewable_each()
    {
        var m = WithSpawns();
        ResourceRenewables.Apply(m,
        [
            ("iron_block", 0, 8, 0), ("iron_block", 30, 8, 0),         // both in spawns
            ("gold_block", 100, 8, 100),                               // middle → ignored
            ("diamond_block", -100, 8, -100),                          // middle → ignored
        ]);

        await Assert.That(m.Renewables.Count).IsEqualTo(1);
        await Assert.That(m.Renewables.Single().RenewFilter).IsEqualTo("only-iron");
        await Assert.That(m.Renewables.Single().RegionId).IsEqualTo("spawns");
        await Assert.That(m.Filters.ContainsKey("only-gold")).IsFalse();
        await Assert.That(m.Filters.ContainsKey("only-diamond")).IsFalse();
        // the relax mentions only the in-spawn ore (iron)
        await Assert.That(m.ApplyRules.Single(r => r.RegionId == "spawns").BlockBreakFilter).IsEqualTo("only-iron");

        // serializes + re-parses with the single renewable intact
        m.Name = "T"; m.Version = "1.0.0";
        var reparsed = PgmStudio.Pgm.Serializer.ToDict(PgmStudio.Pgm.MapParser.ParseXmlString(PgmStudio.Pgm.XmlWriter.ToXml(m)));
        await Assert.That(((List<object?>)reparsed["renewables"]!).Count).IsEqualTo(1);
    }
}
