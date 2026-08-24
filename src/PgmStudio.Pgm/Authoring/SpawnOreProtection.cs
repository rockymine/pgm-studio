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
    /// apply rule keeps other teams out of with an <c>only-&lt;team&gt;</c> enter filter — a wool room uses
    /// <c>not-&lt;team&gt;</c>, so the prefix is what tells the two apart. The id is the region the renewable
    /// and the block rule reference; the boxes are what an ore's position is tested against.</summary>
    public static List<(string Id, Rect Box)> Protections(MapXml m)
    {
        var seen = new HashSet<string>();
        var result = new List<(string, Rect)>();
        foreach (var rule in m.ApplyRules)
            if (rule.EnterFilter.StartsWith("only-") && seen.Add(rule.RegionId)
                && m.Regions.TryGetValue(rule.RegionId, out var r))
                foreach (var box in RectBoxes(m, r))
                    result.Add((rule.RegionId, box));
        return result;
    }

    /// <summary>Restate the spawns' block protection for the ore that lives in them. Called with nothing,
    /// the blanket deny stands — a spawn with no ore has nothing anyone may break.</summary>
    public static void State(MapXml m, IReadOnlyList<Ore> ores)
    {
        if (ores.Count == 0) return;
        var spawns = Protections(m);
        if (spawns.Count == 0) return;

        if (!m.Regions.ContainsKey("spawns"))
            m.Regions["spawns"] = new Region { Id = "spawns", Type = "union", Children = spawns.Select(s => s.Id).ToList() };

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
        spawnIds.Add("spawns");   // the blanket deny sits on the shared spawns union (TeamsGenerator)
        m.ApplyRules.RemoveAll(r => r.BlockFilter == "never" && spawnIds.Contains(r.RegionId));
        m.ApplyRules.RemoveAll(r => r.RegionId == "spawns" && r.BlockBreakFilter.Length > 0);
        m.ApplyRules.Add(new ApplyRule
        {
            BlockBreakFilter = breakId, BlockPlaceFilter = placeId, RegionId = "spawns", Message = Message,
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
