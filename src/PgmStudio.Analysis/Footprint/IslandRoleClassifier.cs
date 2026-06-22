using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using PgmStudio.Analysis.Region;

namespace PgmStudio.Analysis.Footprint;

using Dict = Dictionary<string, object?>;

/// <summary>Semantic gameplay role of a detected island, from the objective anchors it carries and its
/// relation to the buildable space (the lane-decomposition rubric, not size):
/// <list type="bullet">
/// <item><see cref="Team"/> — holds a team's spawn (a home island; gets dissected into lanes).</item>
/// <item><see cref="Objective"/> — holds a wool but no spawn (a forward wool island).</item>
/// <item><see cref="Neutral"/> — no objective anchor, but sits in the buildable space (stepping-stone / mid).</item>
/// <item><see cref="Decorative"/> — no anchor and outside the buildable space (e.g. an observer island).</item>
/// </list>
/// </summary>
public enum IslandGameplayRole { Team, Objective, Neutral, Decorative }

/// <summary>
/// Classifies detected islands by gameplay role from the map's objective anchors — the team spawn region,
/// wool location, wool-room region, and wool-dispensing spawner regions — and the
/// buildable region. A spawn anchor makes an island a <see cref="IslandGameplayRole.Team"/> island; a wool
/// anchor without a spawn makes it <see cref="IslandGameplayRole.Objective"/>; an anchorless island is
/// <see cref="IslandGameplayRole.Neutral"/> when it touches the build region and
/// <see cref="IslandGameplayRole.Decorative"/> otherwise. The <b>monument</b> is deliberately not an anchor
/// (it is the capture point on the enemy/contested side) and a spawner counts only when it dispenses wool
/// (an economy spawner, e.g. gold nuggets, is not an objective).
/// </summary>
public static class IslandRoleClassifier
{
    private static readonly GeometryFactory Gf = new();

    /// <summary>An objective anchor footprint, tagged spawn-type (home) vs wool-type (objective).</summary>
    public readonly record struct Anchor(bool IsSpawn, Geometry Geom);

    /// <summary>An island's role plus the objective anchors that fall on it — the lane targets the auto-cutter
    /// seeds from (a wool anchor is a wool-lane tip, a spawn anchor the spawn spur).</summary>
    public readonly record struct IslandAssessment(IslandGameplayRole Role, IReadOnlyList<Anchor> Anchors);

    /// <summary>Classify each island <em>and</em> report the anchors it carries, in one pass. The role follows
    /// the rubric (spawn → team, else wool → objective, else build-region → neutral, else decorative); the
    /// carried anchors are every spawn/wool anchor whose footprint intersects the island.</summary>
    public static IReadOnlyList<IslandAssessment> Assess(
        IReadOnlyList<Geometry> islands, IReadOnlyList<Anchor> anchors, Geometry? buildRegion)
    {
        var spawn = anchors.Where(a => a.IsSpawn).Select(a => a.Geom).ToList();
        var wool = anchors.Where(a => !a.IsSpawn).Select(a => a.Geom).ToList();
        return islands.Select(isl =>
        {
            var carried = anchors.Where(a => Hits(isl, a.Geom)).ToList();
            var role =
                spawn.Any(a => Hits(isl, a)) ? IslandGameplayRole.Team
                : wool.Any(a => Hits(isl, a)) ? IslandGameplayRole.Objective
                // No build region known → can't tell a stepping-stone from decor, so stay Neutral.
                : buildRegion is { IsEmpty: false } && Hits(isl, buildRegion) ? IslandGameplayRole.Neutral
                : buildRegion is { IsEmpty: false } ? IslandGameplayRole.Decorative
                : IslandGameplayRole.Neutral;
            return new IslandAssessment(role, carried);
        }).ToList();
    }

    public static IReadOnlyList<IslandGameplayRole> Classify(
        IReadOnlyList<Geometry> islands, IReadOnlyList<Anchor> anchors, Geometry? buildRegion)
        => Assess(islands, anchors, buildRegion).Select(a => a.Role).ToList();

    private static bool Hits(Geometry island, Geometry? anchor)
        => anchor is { IsEmpty: false } && island.Intersects(anchor);

    /// <summary>
    /// Extract the objective anchors from a map doc (xml_data shape): the team <c>spawns[].region</c> as
    /// spawn-type; wool location + wool-room region + wool-dispensing spawner regions as wool-type. Spawn
    /// protection is deliberately <b>not</b> a source — an <c>only-&lt;team&gt;</c> <c>enter</c> filter also
    /// guards wool rooms (and other team-restricted areas), so it dropped spawn markers onto the wool rooms;
    /// the spawn region itself is the ground truth and sits on the island, so the point anchor suffices.
    /// </summary>
    public static List<Anchor> ExtractAnchors(Dict doc, (double minX, double minZ, double maxX, double maxZ) bounds)
    {
        var registry = doc.GetValueOrDefault("regions") as Dict ?? new Dict();
        var anchors = new List<Anchor>();

        Geometry? Region(object? id) =>
            id is string s && s.Length > 0 && registry.GetValueOrDefault(s) is Dict r
                ? RegionGeometry2d.ToGeometry(r, bounds, registry) : null;
        void Add(bool spawn, Geometry? g) { if (g is { IsEmpty: false }) anchors.Add(new Anchor(spawn, g)); }

        foreach (var s in List(doc, "spawns"))
        {
            if (s is not Dict sd) continue;
            var rid = sd.GetValueOrDefault("region");
            if (Region(rid) is { IsEmpty: false } g) { Add(true, g); continue; }
            // A spawn region whose footprint is degenerate (e.g. 803's radius-0 cylinder spawn points) → anchor
            // a point at its centre, so the team island is still detected (the spawn sits on it).
            if (rid is string sid && registry.GetValueOrDefault(sid) is Dict sr && RegionCenter(sr) is ({ } cx, { } cz))
                anchors.Add(new Anchor(true, Gf.CreatePoint(new Coordinate(cx, cz))));
        }

        foreach (var w in List(doc, "wools"))
        {
            if (w is not Dict wd) continue;
            if (wd.GetValueOrDefault("location") is Dict loc && Num(loc, "x") is { } wx && Num(loc, "z") is { } wz)
                Add(false, Gf.CreatePoint(new Coordinate(wx, wz)));
            Add(false, Region(wd.GetValueOrDefault("wool_room_region")));
        }

        foreach (var sp in List(doc, "spawners"))
        {
            if (sp is not Dict spd || !DispensesWool(spd)) continue;   // skip economy spawners (gold, etc.)
            Add(false, Region(spd.GetValueOrDefault("spawn_region")));
            Add(false, Region(spd.GetValueOrDefault("player_region")));
        }
        return anchors;
    }

    /// <summary>True when a spawner dispenses wool (vs an economy item like gold nuggets).</summary>
    public static bool DispensesWool(Dict spawner) =>
        List(spawner, "items").OfType<Dict>().Any(it =>
            it.GetValueOrDefault("material") is string m && m.Contains("wool", StringComparison.OrdinalIgnoreCase));

    /// <summary>Union the cleaned-base footprints of the build-category regions into one buildable geometry,
    /// or null when there are none. <paramref name="buildIds"/> comes from the region categorizer (the caller
    /// supplies it, keeping this geometry leaf free of the categorizer dependency).</summary>
    public static Geometry? BuildRegion(Dict doc, IEnumerable<string> buildIds,
        (double minX, double minZ, double maxX, double maxZ) bounds)
    {
        var registry = doc.GetValueOrDefault("regions") as Dict ?? new Dict();
        var geoms = buildIds
            .Select(id => registry.GetValueOrDefault(id) is Dict r ? RegionGeometry2d.ToGeometry(r, bounds, registry) : null)
            .Where(g => g is { IsEmpty: false }).Cast<Geometry>().ToList();
        return geoms.Count == 0 ? null : UnaryUnionOp.Union(geoms);
    }

    // A representative (x,z) for a region whose footprint came back degenerate — its 2D AABB centre, else a
    // declared centre coordinate (cylinder base, point/block position, …).
    private static (double? X, double? Z) RegionCenter(Dict r)
    {
        if (r.GetValueOrDefault("bounds_2d") is Dict b && b.GetValueOrDefault("min") is Dict mn && b.GetValueOrDefault("max") is Dict mx
            && Num(mn, "x") is { } x0 && Num(mx, "x") is { } x1 && Num(mn, "z") is { } z0 && Num(mx, "z") is { } z1)
            return ((x0 + x1) / 2, (z0 + z1) / 2);
        foreach (var k in (string[])["base", "position", "center", "origin"])
            if (r.GetValueOrDefault(k) is Dict p && Num(p, "x") is { } px && Num(p, "z") is { } pz)
                return (px, pz);
        return (null, null);
    }

    private static List<object?> List(Dict d, string k) => d.GetValueOrDefault(k) as List<object?> ?? [];
    private static double? Num(Dict d, string k) => d.GetValueOrDefault(k) switch
    { double x => x, long l => l, int i => i, float f => f, _ => null };
}
