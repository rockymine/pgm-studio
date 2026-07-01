using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>MapXml → PGM map.xml string (port of xml_writer.py).</summary>
public static partial class XmlWriter
{
    private static readonly HashSet<string> BuiltinFilterIds = new() { "never", "always" };

    public static string ToXml(MapXml m)
    {
        var root = BuildMapElem(m);
        // Serialize with the corpus/`docs/template.xml` conventions: 4-space indentation and no `<?xml?>`
        // declaration (real PGM maps start straight at `<map>`).
        var settings = new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    " };
        var sb = new System.Text.StringBuilder();
        using (var xw = System.Xml.XmlWriter.Create(sb, settings)) root.Save(xw);
        // PGM convention: self-close as `/>` with no leading space, and end the file with a trailing newline.
        // The serializer emits empty elements as `<tag … />` (a space before the slash); strip it only at
        // element closes — always at line end, so a `" />"` inside an attribute value is left alone.
        var body = SelfCloseSpace().Replace(sb.ToString(), "/>$1");
        return body + "\n";
    }

    [GeneratedRegex(@" />(\r?\n|$)")]
    private static partial Regex SelfCloseSpace();

    // ── synthetic-id detection ──────────────────────────────────────────────────────
    private static bool IsSynthetic(string id) => id.Contains("__anon_") || id.StartsWith("__");

    // ── coordinate helpers ──────────────────────────────────────────────────────────
    private static string C(double? v)
    {
        if (v is null) return "0";
        var d = v.Value;
        if (double.IsPositiveInfinity(d)) return "oo";
        if (double.IsNegativeInfinity(d)) return "-oo";
        if (d == Math.Floor(d) && !double.IsInfinity(d))
            return ((long)d).ToString(CultureInfo.InvariantCulture);
        return d.ToString(CultureInfo.InvariantCulture);
    }

    private static string C3(double? x, double? y, double? z) => $"{C(x)},{C(y)},{C(z)}";
    private static string C2(double? x, double? z) => $"{C(x)},{C(z)}";

    private static void Set(XElement e, string name, string? value) { if (value is not null) e.SetAttributeValue(name, value); }

    // ── parent-count metadata ───────────────────────────────────────────────────────
    private static Dictionary<string, int> CountFilterParents(Dictionary<string, Filter> filters)
    {
        var counts = new Dictionary<string, int>();
        void Inc(string id) => counts[id] = counts.GetValueOrDefault(id) + 1;
        foreach (var f in filters.Values)
        {
            if (f.Children is not null) foreach (var cid in f.Children) Inc(cid);
            if ((f.Type is "not" or "deny" or "allow" or "blocks" or "offset") && !string.IsNullOrEmpty(f.Child)) Inc(f.Child!);
            if ((f.Type is "blocks" or "offset") && !string.IsNullOrEmpty(f.Child)) Inc(f.Child!);  // matches Python double-count
        }
        return counts;
    }

    private static Dictionary<string, int> CountRegionParents(Dictionary<string, Region> regions)
    {
        var counts = new Dictionary<string, int>();
        void Inc(string id) => counts[id] = counts.GetValueOrDefault(id) + 1;
        foreach (var r in regions.Values)
        {
            if (r.Children is not null) foreach (var cid in r.Children) Inc(cid);
            if (!string.IsNullOrEmpty(r.SourceId)) Inc(r.SourceId!);
        }
        return counts;
    }

    // Filter ids referenced from outside the filter tree (apply rules, renewables) must stay top-level so
    // the reference resolves — even when a parent filter also nests them (which would otherwise inline them).
    private static HashSet<string> ExternalFilterRefs(MapXml m)
    {
        var refs = new HashSet<string>();
        void Add(string? id) { if (!string.IsNullOrEmpty(id) && !BuiltinFilterIds.Contains(id)) refs.Add(id!); }
        foreach (var r in m.ApplyRules)
        {
            Add(r.EnterFilter); Add(r.LeaveFilter); Add(r.BlockFilter); Add(r.BlockPlaceFilter);
            Add(r.BlockBreakFilter); Add(r.BlockPhysicsFilter); Add(r.BlockPlaceAgainstFilter); Add(r.UseFilter); Add(r.FilterId);
        }
        foreach (var r in m.Renewables) { Add(r.RenewFilter); Add(r.ReplaceFilter); }
        return refs;
    }

    private static HashSet<string> ExternalRegionRefs(MapXml m)
    {
        var refs = new HashSet<string>();
        foreach (var s in m.Spawners) { refs.Add(s.SpawnRegion); refs.Add(s.PlayerRegion); }
        foreach (var r in m.Renewables) if (r.RegionId.Length > 0) refs.Add(r.RegionId);
        foreach (var r in m.BlockDropRules) if (r.RegionId.Length > 0) refs.Add(r.RegionId);
        foreach (var rule in m.ApplyRules) if (rule.RegionId.Length > 0 && !IsSynthetic(rule.RegionId)) refs.Add(rule.RegionId);
        foreach (var w in m.Wools) if (w.MonumentRegionId is { Length: > 0 }) refs.Add(w.MonumentRegionId);
        return refs;
    }

    // ── top-level builder ───────────────────────────────────────────────────────────
    private static XElement BuildMapElem(MapXml m)
    {
        var root = new XElement("map", new XAttribute("proto", "1.5.0"));
        root.Add(new XElement("name", m.Name));
        root.Add(new XElement("version", m.Version));
        if (m.Gamemode.Length > 0 && m.Gamemode != "ctw") root.Add(new XElement("gamemode", m.Gamemode));
        root.Add(new XElement("objective", m.Objective));

        foreach (var inc in m.Includes) root.Add(new XElement("include", new XAttribute("id", inc)));

        WriteAuthors(root, m.Authors);
        WriteTeams(root, m.Teams);
        WriteKits(root, m.Kits);
        WriteSpawns(root, m.Spawns, m.ObserverSpawn);
        WriteWools(root, m.Wools);

        if (m.Filters.Count > 0) WriteFiltersBlock(root, m.Filters, ExternalFilterRefs(m));
        if (m.Regions.Count > 0 || m.ApplyRules.Count > 0)
            WriteRegionsBlock(root, m.Regions, m.ApplyRules, ExternalRegionRefs(m));

        if (m.Spawners.Count > 0) WriteSpawners(root, m.Spawners);
        if (m.Renewables.Count > 0) WriteRenewables(root, m.Renewables);
        if (m.BlockDropRules.Count > 0) WriteBlockDrops(root, m.BlockDropRules);

        WriteMaterialList(root, "itemkeep", "item", m.ItemKeep);
        WriteMaterialList(root, "itemremove", "item", m.ItemRemove);
        WriteMaterialList(root, "toolrepair", "tool", m.ToolRepair);
        WriteKillRewards(root, m.KillRewards);
        if (m.HungerDepletion is { Length: > 0 } hd)
            root.Add(new XElement("hunger", new XElement("depletion", hd)));

        if (m.MaxBuildHeight is not null) root.Add(new XElement("maxbuildheight", m.MaxBuildHeight.Value.ToString(CultureInfo.InvariantCulture)));
        return root;
    }

    // <itemkeep>/<itemremove> hold <item> children; <toolrepair> holds <tool> children — each a plain material.
    private static void WriteMaterialList(XElement parent, string blockTag, string itemTag, List<string> materials)
    {
        if (materials.Count == 0) return;
        var block = new XElement(blockTag);
        foreach (var mat in materials) block.Add(new XElement(itemTag, mat));
        parent.Add(block);
    }

    private static void WriteKillRewards(XElement parent, List<KillReward> rewards)
    {
        if (rewards.Count == 0) return;
        var block = new XElement("kill-rewards");
        foreach (var r in rewards)
        {
            var re = new XElement("kill-reward");
            foreach (var item in r.Items)
            {
                var ie = new XElement("item"); Set(ie, "material", item.Material);
                if (item.Damage != 0) Set(ie, "damage", item.Damage.ToString());
                if (item.Amount != 1) Set(ie, "amount", item.Amount.ToString());
                if (item.TeamColor) Set(ie, "team-color", "true");
                re.Add(ie);
            }
            block.Add(re);
        }
        parent.Add(block);
    }

    private static void WriteAuthors(XElement parent, List<Author> authors)
    {
        void Block(string blockTag, string itemTag, List<Author> people)
        {
            if (people.Count == 0) return;
            var block = new XElement(blockTag); parent.Add(block);
            foreach (var a in people)
            {
                var e = new XElement(itemTag); Set(e, "uuid", a.Uuid);
                if (a.Contribution.Length > 0) Set(e, "contribution", a.Contribution);
                block.Add(e);
                // Resolve uuid → username as a sibling comment (its own line at the same indent, the corpus
                // convention) so the human name is visible next to the uuid. Skipped when unresolved.
                if (a.Name.Length > 0) block.Add(new XComment($" {a.Name} "));
            }
        }
        Block("authors", "author", authors.Where(a => a.Role == "author").ToList());
        Block("contributors", "contributor", authors.Where(a => a.Role == "contributor").ToList());
    }

    private static void WriteTeams(XElement parent, List<Team> teams)
    {
        if (teams.Count == 0) return;
        var block = new XElement("teams"); parent.Add(block);
        foreach (var t in teams)
        {
            var e = new XElement("team", t.Name);
            Set(e, "id", t.Id); Set(e, "color", t.Color);
            if (t.DyeColor.Length > 0) Set(e, "dye-color", t.DyeColor);
            if (t.MaxPlayers != 0) Set(e, "max", t.MaxPlayers.ToString());
            if (t.MinPlayers != 0) Set(e, "min", t.MinPlayers.ToString());
            block.Add(e);
        }
    }

    private static void WriteKits(XElement parent, List<Kit> kits)
    {
        if (kits.Count == 0) return;
        var block = new XElement("kits"); parent.Add(block);
        foreach (var kit in kits)
        {
            var ke = new XElement("kit"); Set(ke, "id", kit.Id); if (kit.Force) Set(ke, "force", "true"); block.Add(ke);
            foreach (var item in kit.Items)
            {
                var e = new XElement("item"); Set(e, "slot", item.Slot.ToString()); Set(e, "material", item.Material);
                if (item.Amount != 1) Set(e, "amount", item.Amount.ToString());
                if (item.ItemDamage != 0) Set(e, "damage", item.ItemDamage.ToString());
                if (item.Unbreakable) Set(e, "unbreakable", "true");
                if (item.TeamColor) Set(e, "team-color", "true");
                WriteEnchantments(e, item.Enchantments); ke.Add(e);
            }
            foreach (var armor in kit.Armor)
            {
                var e = new XElement(armor.SlotName); Set(e, "material", armor.Material);
                if (armor.Unbreakable) Set(e, "unbreakable", "true");
                if (armor.TeamColor) Set(e, "team-color", "true");
                WriteEnchantments(e, armor.Enchantments); ke.Add(e);
            }
            foreach (var eff in kit.Effects)
            {
                var e = new XElement("effect", eff.Type);
                if (eff.Duration.Length > 0) Set(e, "duration", eff.Duration);
                Set(e, "amplifier", eff.Amplifier.ToString());
                ke.Add(e);
            }
        }
    }

    private static void WriteEnchantments(XElement parent, string enchantments)
    {
        if (enchantments.Length == 0) return;
        foreach (var raw in enchantments.Split(','))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            var idx = token.LastIndexOf(':');
            if (idx >= 0) { var e = new XElement("enchantment", token[..idx]); Set(e, "level", token[(idx + 1)..]); parent.Add(e); }
            else parent.Add(new XElement("enchantment", token));
        }
    }

    private static void WriteSpawns(XElement parent, List<Spawn> spawns, Spawn? observer)
    {
        if (spawns.Count == 0 && observer is null) return;
        var block = new XElement("spawns"); parent.Add(block);
        if (observer is not null) WriteSpawnElem(block, observer, "default");
        foreach (var s in spawns) WriteSpawnElem(block, s, "spawn");
    }

    private static void WriteSpawnElem(XElement parent, Spawn spawn, string tag)
    {
        var e = new XElement(tag);
        if (spawn.Team.Length > 0) Set(e, "team", spawn.Team);
        if (spawn.Kit.Length > 0) Set(e, "kit", spawn.Kit);
        if (spawn.Yaw != 0) Set(e, "yaw", C(spawn.Yaw));
        var region = spawn.Region;
        if (region is not null && !IsSynthetic(region.Id))
            Set(e, "region", region.Id);
        else if (region is not null)
            e.Add(new XElement("region", RegionElemInline(region)));
        parent.Add(e);
    }

    private static void WriteWools(XElement parent, List<Wool> wools)
    {
        if (wools.Count == 0) return;
        var block = new XElement("wools"); parent.Add(block);
        foreach (var w in wools)
        {
            var e = new XElement("wool");
            Set(e, "team", w.Team); Set(e, "color", w.Color);
            Set(e, "location", C3(w.Location.X, w.Location.Y, w.Location.Z));
            if (w.MonumentRegionId is { Length: > 0 }) Set(e, "monument", w.MonumentRegionId);
            else e.Add(new XElement("monument", new XElement("block", C3(w.Monument.X, w.Monument.Y, w.Monument.Z))));
            block.Add(e);
        }
    }

    // ── filters block ───────────────────────────────────────────────────────────────
    private static void WriteFiltersBlock(XElement parent, Dictionary<string, Filter> filters, HashSet<string> external)
    {
        var counts = CountFilterParents(filters);
        var topLevel = new HashSet<string>();
        foreach (var fid in filters.Keys)
        {
            if (BuiltinFilterIds.Contains(fid) || IsSynthetic(fid)) continue;
            var c = counts.GetValueOrDefault(fid);
            if (c == 0 || c >= 2 || external.Contains(fid)) topLevel.Add(fid);
        }
        if (topLevel.Count == 0) return;
        var block = new XElement("filters"); parent.Add(block);
        foreach (var fid in filters.Keys)
            if (topLevel.Contains(fid) && FilterElem(fid, filters, counts, topLevel, withId: true) is { } e)
                block.Add(e);
    }

    private static XElement? FilterChildElem(string childId, Dictionary<string, Filter> filters, Dictionary<string, int> counts, HashSet<string> topLevel)
    {
        if (topLevel.Contains(childId)) return new XElement("filter", new XAttribute("id", childId));
        if (!filters.ContainsKey(childId)) return new XElement("filter", new XAttribute("id", childId));
        return FilterElem(childId, filters, counts, topLevel, withId: !IsSynthetic(childId));
    }

    private static XElement? FilterElem(string fid, Dictionary<string, Filter> filters, Dictionary<string, int> counts, HashSet<string> topLevel, bool withId)
    {
        if (!filters.TryGetValue(fid, out var f)) return null;
        XElement? Child(string cid) => FilterChildElem(cid, filters, counts, topLevel);

        var e = new XElement(MapTag(f.Type));
        // A `void` filter is trivial and always inlined — `<void/>` is enough, it never needs an id (B15).
        if (withId && fid.Length > 0 && !IsSynthetic(fid) && f.Type != "void") e.SetAttributeValue("id", fid);

        switch (f.Type)
        {
            case "all" or "any" or "one":
                foreach (var cid in f.Children ?? []) if (Child(cid) is { } ce) e.Add(ce);
                break;
            case "not" or "deny" or "allow":
                if (!string.IsNullOrEmpty(f.Child) && Child(f.Child) is { } sce) e.Add(sce);
                break;
            case "team": e.Value = f.Team ?? ""; break;
            case "material": e.Value = f.Material ?? ""; break;
            case "void": break;
            case "cause": e.Value = f.Cause ?? ""; break;
            case "blocks":
                if (!string.IsNullOrEmpty(f.RegionRef)) Set(e, "region", f.RegionRef);
                if (!string.IsNullOrEmpty(f.Child) && Child(f.Child) is { } bce) e.Add(bce);
                break;
            case "carrying" or "wearing" or "holding":
                if (f.Type == "carrying") { if (f.IgnoreMetadata) Set(e, "ignore-metadata", "true"); if (!f.IgnoreDurability) Set(e, "ignore-durability", "false"); }
                else if (f.Type == "wearing") { if (f.IgnoreMetadata) Set(e, "ignore-metadata", "true"); }
                var item = new XElement("item"); Set(item, "material", f.Material ?? "");
                if (f.Damage is not null) Set(item, "damage", f.Damage.Value.ToString());
                if (!string.IsNullOrEmpty(f.Enchantments)) Set(item, "enchantment", f.Enchantments);
                e.Add(item);
                break;
            case "alive" or "dead" or "participating" or "observing" or "match-running" or "match-started" or "grounded" or "never" or "always":
                break;
            case "time": e.Value = f.Duration ?? ""; break;
            case "after":
                if (!string.IsNullOrEmpty(f.FilterRefId)) Set(e, "filter", f.FilterRefId);
                if (!string.IsNullOrEmpty(f.Duration)) Set(e, "duration", f.Duration);
                break;
            case "pulse":
                if (!string.IsNullOrEmpty(f.Period)) Set(e, "period", f.Period);
                if (!string.IsNullOrEmpty(f.Duration)) Set(e, "duration", f.Duration);
                if (!string.IsNullOrEmpty(f.FilterRefId)) Set(e, "filter", f.FilterRefId);
                break;
            case "offset":
                Set(e, "vector", f.Vector ?? "");
                if (!string.IsNullOrEmpty(f.Child) && Child(f.Child) is { } oce) e.Add(oce);
                break;
            case "variable":
                Set(e, "var", f.Var ?? "");
                if (!string.IsNullOrEmpty(f.Team)) Set(e, "team", f.Team);
                e.Value = f.Value ?? "";
                break;
            case "completed" or "objective": e.Value = f.Objective ?? ""; break;
            case "kill-streak":
                if (f.Min is not null) Set(e, "min", f.Min.Value.ToString());
                if (f.Max is not null) Set(e, "max", f.Max.Value.ToString());
                if (f.Count is not null) Set(e, "count", f.Count.Value.ToString());
                break;
            case "class": e.Value = f.Name ?? ""; break;
            case "region": if (!string.IsNullOrEmpty(f.RegionRef)) Set(e, "id", f.RegionRef); break;
            case "players":
                if (f.Min is not null) Set(e, "min", f.Min.Value.ToString());
                if (f.Max is not null) Set(e, "max", f.Max.Value.ToString());
                break;
            case "spawn": e.Value = f.Mob ?? ""; break;
            default: return null;  // FilterRef / unknown — not written
        }
        return e;
    }

    private static string MapTag(string filterType) => filterType;  // filter types already match element tags

    // ── regions block ───────────────────────────────────────────────────────────────
    private static void WriteRegionsBlock(XElement parent, Dictionary<string, Region> regions, List<ApplyRule> applyRules, HashSet<string> external)
    {
        var counts = CountRegionParents(regions);
        var topLevel = new HashSet<string>();
        foreach (var rid in regions.Keys)
        {
            if (IsSynthetic(rid)) continue;
            var c = counts.GetValueOrDefault(rid);
            if (c == 0 || c >= 2 || external.Contains(rid)) topLevel.Add(rid);
        }
        // named transform sources must be top-level so the region="<id>" reference resolves
        foreach (var r in regions.Values)
            if (!string.IsNullOrEmpty(r.SourceId) && !IsSynthetic(r.SourceId!) && regions.ContainsKey(r.SourceId!)) topLevel.Add(r.SourceId!);
        // named targets referenced by synthetic (inline) regions must be top-level too
        foreach (var (rid, r) in regions)
        {
            if (!IsSynthetic(rid)) continue;
            var refs = new List<string>(r.Children ?? []);
            if (!string.IsNullOrEmpty(r.SourceId)) refs.Add(r.SourceId!);
            foreach (var rf in refs) if (!IsSynthetic(rf) && regions.ContainsKey(rf)) topLevel.Add(rf);
        }

        var block = new XElement("regions"); parent.Add(block);
        // Order to match the corpus convention: regions grouped by type (primitives, then compounds), then
        // by semantic role within a type (spawn points · spawn regions · wool spawns/rooms · monuments · build),
        // then the <apply> rules last. OrderBy/ThenBy are stable, so same type+role keeps its relative order.
        var ordered = regions.Keys.Where(topLevel.Contains)
            .OrderBy(rid => TypeRank(regions[rid].Type))
            .ThenBy(rid => RoleRank(rid));
        foreach (var rid in ordered)
            if (RegionElem(rid, regions, counts, topLevel, withId: true) is { } e)
                block.Add(e);
        for (var i = 0; i < applyRules.Count; i++)
            block.Add(ApplyElem(applyRules[i], regions));
    }

    // Region type ordering inside <regions>: primitives first (by shape), then compounds. Applicators
    // (<apply>) are emitted after all regions regardless.
    private static int TypeRank(string type) => type switch
    {
        "point" => 0, "circle" => 1, "cylinder" => 2, "sphere" => 3, "block" => 4, "rectangle" => 5, "cuboid" => 6,
        "union" => 7, "negative" => 8, "complement" => 9, "intersect" => 10, "mirror" => 11, "translate" => 12,
        _ => 20,
    };

    // Secondary ordering inside <regions> (B16): group same-type regions by semantic role (from the id
    // pattern — `red-spawn-point`, `red-spawn`, `red-wool-spawn`, `red-wool`, `red-blue-team-monument`, …) so
    // the roles cluster instead of interleaving. Most-specific patterns first.
    private static int RoleRank(string id) =>
        id.Contains("spawn-point") ? 0 :
        id.Contains("wool-spawn")  ? 1 :
        id.Contains("spawn")       ? 2 :   // spawn regions / protection (incl observer-spawn, *-spawn-N)
        id.Contains("monument")    ? 3 :
        id.Contains("wool")        ? 4 :   // wool rooms / unions
        (id.Contains("build") || id.Contains("bridge") || id.Contains("area") || id.Contains("hole")) ? 5 :
        6;

    private static XElement RegionChildElem(string childId, Dictionary<string, Region> regions, Dictionary<string, int> counts, HashSet<string> topLevel)
    {
        if (topLevel.Contains(childId)) return new XElement("region", new XAttribute("id", childId));
        if (!regions.ContainsKey(childId)) return new XElement("region", new XAttribute("id", childId));
        return RegionElem(childId, regions, counts, topLevel, withId: !IsSynthetic(childId))!;
    }

    private static XElement? RegionElem(string rid, Dictionary<string, Region> regions, Dictionary<string, int> counts, HashSet<string> topLevel, bool withId)
    {
        if (!regions.TryGetValue(rid, out var r)) return null;
        XElement Child(string cid) => RegionChildElem(cid, regions, counts, topLevel);

        XElement e;
        string? idAttr = (withId && rid.Length > 0 && !IsSynthetic(rid)) ? rid : null;

        switch (r.Type)
        {
            case "rectangle":
                e = new XElement("rectangle"); Set(e, "min", C2(r.MinX, r.MinZ)); Set(e, "max", C2(r.MaxX, r.MaxZ)); break;
            case "cuboid":
                e = new XElement("cuboid"); Set(e, "min", C3(r.MinX, r.MinY, r.MinZ)); Set(e, "max", C3(r.MaxX, r.MaxY, r.MaxZ)); break;
            case "cylinder":
                e = new XElement("cylinder"); Set(e, "base", C3(r.BaseX, r.BaseY, r.BaseZ)); Set(e, "radius", C(r.Radius));
                if (r.Height is not null) Set(e, "height", C(r.Height)); break;
            case "circle":
                e = new XElement("circle"); Set(e, "center", C2(r.CenterX, r.CenterZ)); Set(e, "radius", C(r.Radius)); break;
            case "sphere":
                e = new XElement("sphere"); Set(e, "origin", C3(r.OriginX, r.OriginY, r.OriginZ)); Set(e, "radius", C(r.Radius)); break;
            case "block":
                e = new XElement("block", C3(r.PosX, r.PosY, r.PosZ)); break;
            case "point":
                e = new XElement("point", C3(r.PosX, r.PosY, r.PosZ)); break;
            case "union" or "negative" or "complement" or "intersect":
                e = new XElement(r.Type); foreach (var cid in r.Children ?? []) e.Add(Child(cid)); break;
            case "mirror":
                e = new XElement("mirror"); Set(e, "origin", C3(r.OriginX, r.OriginY, r.OriginZ)); Set(e, "normal", C3(r.NormalX, r.NormalY, r.NormalZ));
                if (!string.IsNullOrEmpty(r.SourceId) && !IsSynthetic(r.SourceId!)) Set(e, "region", r.SourceId);
                else if (!string.IsNullOrEmpty(r.SourceId) && regions.TryGetValue(r.SourceId!, out var ms)) e.Add(RegionElemInline(ms));
                break;
            case "translate":
                e = new XElement("translate"); Set(e, "offset", C3(r.OffsetX, r.OffsetY, r.OffsetZ));
                if (!string.IsNullOrEmpty(r.SourceId) && !IsSynthetic(r.SourceId!)) Set(e, "region", r.SourceId);
                else if (!string.IsNullOrEmpty(r.SourceId) && regions.TryGetValue(r.SourceId!, out var ts)) e.Add(RegionElemInline(ts));
                break;
            case "everywhere":
                e = new XElement("everywhere"); break;
            case "above":
                e = new XElement("above"); Set(e, "y", C(r.AboveY)); break;
            case "half":
                e = new XElement("half"); Set(e, "origin", C3(r.OriginX, r.OriginY, r.OriginZ)); Set(e, "normal", C3(r.NormalX, r.NormalY, r.NormalZ)); break;
            default:
                return null;  // reference / unknown — not written
        }
        // id is the first attribute (matches the corpus: <rectangle id="…" min="…" max="…"/>). The geometry
        // attributes are set above, so rebuild the order with id in front.
        if (idAttr is not null)
        {
            var attrs = e.Attributes().Select(a => new XAttribute(a.Name, a.Value)).ToList();
            e.RemoveAttributes();
            e.Add(new XAttribute("id", idAttr));
            foreach (var a in attrs) e.Add(a);
        }
        return e;
    }

    private static XElement RegionElemInline(Region r)
    {
        var single = new Dictionary<string, Region> { [r.Id] = r };
        return RegionElem(r.Id, single, new Dictionary<string, int>(), new HashSet<string>(), withId: false)
               ?? new XElement("region");
    }

    private static XElement ApplyElem(ApplyRule rule, Dictionary<string, Region> regions)
    {
        var e = new XElement("apply");
        void A(string k, string v) { if (v.Length > 0) Set(e, k, v); }
        A("enter", rule.EnterFilter); A("leave", rule.LeaveFilter); A("block", rule.BlockFilter);
        A("block-place", rule.BlockPlaceFilter); A("block-break", rule.BlockBreakFilter);
        A("block-physics", rule.BlockPhysicsFilter); A("block-place-against", rule.BlockPlaceAgainstFilter);
        A("use", rule.UseFilter); A("filter", rule.FilterId);
        A("kit", rule.Kit); A("lend-kit", rule.LendKit); A("velocity", rule.Velocity);

        if (rule.RegionId.Length > 0 && !IsSynthetic(rule.RegionId))
            Set(e, "region", rule.RegionId);
        else if (rule.RegionId.Length > 0 && regions.TryGetValue(rule.RegionId, out var region))
            e.Add(new XElement("region", RegionElemInline(region)));

        A("message", rule.Message);   // message is always the last attribute
        return e;
    }

    private static void WriteSpawners(XElement parent, List<WoolSpawner> spawners)
    {
        var block = new XElement("spawners"); parent.Add(block);
        foreach (var s in spawners)
        {
            var e = new XElement("spawner"); Set(e, "spawn-region", s.SpawnRegion); Set(e, "player-region", s.PlayerRegion);
            if (s.Delay.Length > 0) Set(e, "delay", s.Delay);
            if (s.MaxEntities is not null) Set(e, "max-entities", s.MaxEntities.Value.ToString());
            foreach (var item in s.Items)
            {
                var ie = new XElement("item"); Set(ie, "material", item.Material);
                if (item.Damage != 0) Set(ie, "damage", item.Damage.ToString());
                if (item.Amount != 1) Set(ie, "amount", item.Amount.ToString());
                e.Add(ie);
            }
            block.Add(e);
        }
    }

    private static void WriteRenewables(XElement parent, List<Renewable> renewables)
    {
        var block = new XElement("renewables"); parent.Add(block);
        foreach (var r in renewables)
        {
            var e = new XElement("renewable"); Set(e, "region", r.RegionId);
            if (r.Rate != 1.0) Set(e, "rate", r.Rate.ToString(CultureInfo.InvariantCulture));
            if (r.RenewFilter.Length > 0) Set(e, "renew-filter", r.RenewFilter);
            if (r.ReplaceFilter.Length > 0) Set(e, "replace-filter", r.ReplaceFilter);
            if (r.AvoidPlayers is { } ap) Set(e, "avoid-players", ap.ToString());
            if (r.Grow) Set(e, "grow", "true");
            block.Add(e);
        }
    }

    private static void WriteBlockDrops(XElement parent, List<BlockDropRule> rules)
    {
        var block = new XElement("block-drops"); parent.Add(block);
        foreach (var rule in rules)
        {
            var re = new XElement("rule");
            if (rule.RegionId.Length > 0) Set(re, "region", rule.RegionId);
            if (rule.FilterMaterials.Count > 0)
            {
                var filter = new XElement("filter");
                if (rule.FilterMaterials.Count == 1)
                    filter.Add(new XElement("material", rule.FilterMaterials[0]));
                else
                {
                    var any = new XElement("any");
                    foreach (var mat in rule.FilterMaterials) any.Add(new XElement("material", mat));
                    filter.Add(any);
                }
                re.Add(filter);
            }
            else if (rule.FilterId.Length > 0) Set(re, "filter", rule.FilterId);
            if (rule.WrongTool) Set(re, "wrong-tool", "true");
            if (rule.Replacement.Length > 0) re.Add(new XElement("replacement", rule.Replacement));
            if (rule.Items.Count > 0)
            {
                var drops = new XElement("drops");
                foreach (var item in rule.Items)
                {
                    var ie = new XElement("item"); Set(ie, "material", item.Material);
                    if (item.Damage != 0) Set(ie, "damage", item.Damage.ToString());
                    if (item.Amount != 1) Set(ie, "amount", item.Amount.ToString());
                    if (item.Chance != 1.0) Set(ie, "chance", item.Chance.ToString(CultureInfo.InvariantCulture));
                    drops.Add(ie);
                }
                re.Add(drops);
            }
            block.Add(re);
        }
    }
}
