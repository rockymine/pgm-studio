using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// What a player may do to the blocks inside a spawn, stated once for whatever ore lives there.
///
/// <para>A spawn is protected by a blanket <c>block=never</c> (<see cref="TeamsGenerator"/>), which is the
/// right rule for a spawn holding nothing but its own floor and the wrong one the moment it holds ore: the
/// ore cannot be mined, so the <c>&lt;renewable&gt;</c> that would regrow it never fires and the resource is
/// scenery. The corpus pattern — and <c>docs/pgm/template.xml</c> — states it as a pair instead:
/// <b>break</b> admits only the ore, <b>place</b> admits only the ore placed by the <c>world</c>, which is
/// the renewable putting it back. Everything else in the spawn stays untouchable either way.</para>
///
/// <para>Ore reaches a spawn two ways and neither can see the other's: scanned out of an imported world
/// (<see cref="ResourceRenewables"/>) or stamped there by the plan as an iron cube
/// (<see cref="StructureRenewables"/>). Both name what they found and the rule is stated once over the
/// union, because two callers each replacing one apply rule is one of them silently winning.</para>
/// </summary>
public static class SpawnOreProtection
{
    /// <summary>One kind of ore living in a spawn: the slug its filter is named for and the material a
    /// filter matches it by.</summary>
    public readonly record struct Ore(string Slug, string Material);

    private const string Message = "You may not edit spawn!";

    /// <summary>The spawn-protection regions and the block boxes they cover. A protection region is one an
    /// apply rule admits a single team to — its <c>enter</c> filter is the team itself or a <c>&lt;team&gt;</c>
    /// filter; a wool room admits everyone <em>but</em> its owner, through a <c>not</c>. The id is the region
    /// the renewable and the block rule reference; the boxes are what an ore's position is tested against.</summary>
    public static List<(string Id, Rect Box)> Protections(MapXml m)
    {
        var teams = m.Teams.Select(team => team.Id).ToHashSet();
        bool AdmitsOneTeam(string filter) =>
            teams.Contains(filter) || m.Filters.TryGetValue(filter, out var f) && f.Type == "team";

        var seen = new HashSet<string>();
        var result = new List<(string, Rect)>();
        foreach (var rule in m.ApplyRules)
            if (rule.EnterFilter.Length > 0 && AdmitsOneTeam(rule.EnterFilter) && seen.Add(rule.RegionId)
                && m.Regions.TryGetValue(rule.RegionId, out var r))
                foreach (var box in RectBoxes(m, r))
                    result.Add((rule.RegionId, box));
        return result;
    }

    /// <summary>The one region every spawn protection is gathered in: <c>spawns</c> where the map has it, the
    /// single protection itself where there is one, and otherwise a <c>spawns</c> union written here. Null where
    /// the map protects no spawn.</summary>
    public static string? SpawnsRegion(MapXml m)
    {
        if (m.Regions.ContainsKey(SpawnsId)) return SpawnsId;
        var ids = Protections(m).Select(s => s.Id).Distinct().ToList();
        if (ids.Count == 0) return null;
        if (ids.Count == 1) return ids[0];
        m.Regions[SpawnsId] = new Region { Id = SpawnsId, Type = "union", Children = ids };
        return SpawnsId;
    }

    /// <summary>The id the shared spawn protection is written under when there is more than one spawn.</summary>
    public const string SpawnsId = "spawns";

    /// <summary>Restate the spawns' block protection for the ore that lives in them. Called with nothing,
    /// the blanket deny stands — a spawn with no ore has nothing anyone may break.</summary>
    public static void State(MapXml m, IReadOnlyList<Ore> ores)
    {
        if (ores.Count == 0) return;
        var spawns = Protections(m);
        if (SpawnsRegion(m) is not { } spawnsId) return;

        var seq = 0;
        string Synth(Filter f) { f.Id = $"__spawn-ore-{seq++}"; m.Filters[f.Id] = f; return f.Id; }
        string Match() => ores.Count == 1
            ? Synth(new Filter { Type = "material", Material = ores[0].Material })
            : Synth(new Filter { Type = "any", Children = ores.Select(o => Synth(new Filter { Type = "material", Material = o.Material })).ToList() });

        string breakId;
        if (ores.Count == 1) breakId = $"only-{ores[0].Slug}";   // the named material filter the renewable reads
        else
        {
            breakId = "spawn-resources";
            m.Filters[breakId] = new Filter
            {
                Id = breakId, Type = "any",
                Children = ores.Select(o => Synth(new Filter { Type = "material", Material = o.Material })).ToList(),
            };
        }
        var placeId = ores.Count == 1 ? $"only-{ores[0].Slug}-cause-world" : "spawn-resources-cause-world";
        m.Filters[placeId] = new Filter
        {
            Id = placeId, Type = "all", Children = [Match(), Synth(new Filter { Type = "cause", Cause = "world" })],
        };

        var spawnIds = spawns.Select(s => s.Id).ToHashSet();
        spawnIds.Add(spawnsId);   // the blanket deny sits on the shared spawns region (TeamsGenerator)
        m.ApplyRules.RemoveAll(r => r.BlockFilter == "never" && spawnIds.Contains(r.RegionId));
        m.ApplyRules.RemoveAll(r => r.RegionId == spawnsId && r.BlockBreakFilter.Length > 0);
        m.ApplyRules.Add(new ApplyRule
        {
            BlockBreakFilter = breakId, BlockPlaceFilter = placeId, RegionId = spawnsId, Message = Message,
        });
    }

    /// <summary>The block-footprint boxes of a protection region: a rectangle's own box, or a union's
    /// rectangle children.</summary>
    private static IEnumerable<Rect> RectBoxes(MapXml m, Region r)
    {
        if (r.Type == "rectangle")
        {
            if (r.MinX is { } a && r.MinZ is { } b && r.MaxX is { } c && r.MaxZ is { } d) yield return new Rect(a, b, c, d);
        }
        else if (r.Type == "union" && r.Children is { } children)
            foreach (var cid in children)
                if (m.Regions.TryGetValue(cid, out var cr) && cr.Type == "rectangle"
                    && cr.MinX is { } a && cr.MinZ is { } b && cr.MaxX is { } c && cr.MaxZ is { } d)
                    yield return new Rect(a, b, c, d);
    }
}
