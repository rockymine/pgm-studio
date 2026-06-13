using System.Globalization;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>
/// Builds a flat filter registry from a &lt;filters&gt; element (port of filter_parser.py),
/// pre-seeded with the PGM built-ins (never/always). Anonymous children get synthetic ids
/// <c>{parent_id}__anon_{index}</c>; a &lt;filter id="ref"/&gt; is a transient reference.
/// </summary>
internal sealed class FilterParser
{
    private readonly Dictionary<string, Filter> _registry = new()
    {
        ["never"] = new Filter { Id = "never", Type = "never" },
        ["always"] = new Filter { Id = "always", Type = "always" },
    };

    public Dictionary<string, Filter> Registry() => _registry;

    public Dictionary<string, Filter> ParseFiltersElem(XElement filtersElem)
    {
        foreach (var (child, i) in filtersElem.Elements().Select((c, i) => (c, i)))
        {
            var f = ParseFilterNode(child, parentId: "", index: i);
            if (f is not null && f.Type != "filter" && f.Id.Length > 0)
                _registry.TryAdd(f.Id, f);
        }
        return _registry;
    }

    private Filter? ParseFilterNode(XElement elem, string parentId, int index = 0)
    {
        var tag = elem.Name.LocalName;
        var id = Xml.Get(elem, "id", "");
        var text = Xml.Text(elem).Trim();

        Filter? f = tag switch
        {
            "all" or "any" or "one" => ParseComposite(elem, id, parentId, index, tag),
            "not" or "deny" or "allow" => ParseSingleChild(elem, id, parentId, index, tag),
            "team" => new Filter { Id = id, Type = "team", Team = text },
            "material" => new Filter { Id = id, Type = "material", Material = text },
            "void" => new Filter { Id = id, Type = "void" },
            "cause" => new Filter { Id = id, Type = "cause", Cause = text },
            "blocks" => ParseBlocks(elem, id, parentId, index),
            "carrying" => ParseItemFilter(elem, id, "carrying"),
            "wearing" => ParseItemFilter(elem, id, "wearing"),
            "holding" => ParseItemFilter(elem, id, "holding"),
            "alive" or "dead" or "participating" or "observing" or "match-running"
                or "match-started" or "grounded" or "never" or "always"
                => new Filter { Id = id, Type = tag },
            "time" => new Filter { Id = id, Type = "time", Duration = text },
            "after" => new Filter { Id = id, Type = "after", FilterRefId = Xml.Get(elem, "filter", ""), Duration = Xml.Get(elem, "duration", "") },
            "pulse" => new Filter { Id = id, Type = "pulse", Period = Xml.Get(elem, "period", ""), Duration = Xml.Get(elem, "duration", ""), FilterRefId = Xml.Get(elem, "filter", "") },
            "offset" => ParseOffset(elem, id, parentId, index),
            "variable" => new Filter { Id = id, Type = "variable", Var = Xml.Get(elem, "var", ""), Value = text, Team = Xml.Get(elem, "team", "") },
            "completed" => new Filter { Id = id, Type = "completed", Objective = text },
            "objective" => new Filter { Id = id, Type = "objective", Objective = text },
            "filter" => new Filter { Type = "filter", RefId = Xml.Get(elem, "id", "") },
            "kill-streak" => new Filter { Id = id, Type = "kill-streak", Min = SafeInt(Xml.GetOrNull(elem, "min")), Max = SafeInt(Xml.GetOrNull(elem, "max")), Count = SafeInt(Xml.GetOrNull(elem, "count")) },
            "class" => new Filter { Id = id, Type = "class", Name = text },
            "region" => new Filter { Id = id, Type = "region", RegionRef = Xml.Get(elem, "id", "") },
            "players" => new Filter { Id = id, Type = "players", Min = SafeInt(Xml.GetOrNull(elem, "min")), Max = SafeInt(Xml.GetOrNull(elem, "max")) },
            "spawn" => new Filter { Id = id, Type = "spawn", Mob = text },
            _ => null,
        };

        if (f is null) return null;
        if (f.Type == "filter") return f;  // FilterRef: never registered, no synthetic id

        if (f.Id.Length == 0)
        {
            if (parentId.Length > 0) f.Id = $"{parentId}__anon_{index}";
            else return f;  // top-level anonymous — skip registration
        }
        if (f.Id.Length > 0) _registry.TryAdd(f.Id, f);
        return f;
    }

    private static string ChildRef(Filter child) => child.Type == "filter" ? child.RefId ?? "" : child.Id;

    private Filter ParseComposite(XElement elem, string id, string parentId, int parentIndex, string tag)
    {
        var effectiveId = id.Length > 0 ? id : (parentId.Length > 0 ? $"{parentId}__anon_{parentIndex}" : "");
        var childIds = new List<string>();
        foreach (var (childElem, i) in elem.Elements().Select((c, i) => (c, i)))
        {
            var child = ParseFilterNode(childElem, effectiveId, i);
            if (child is null) continue;
            if (child.Type == "filter") childIds.Add(child.RefId ?? "");
            else if (child.Id.Length > 0) childIds.Add(child.Id);
        }
        return new Filter { Id = id, Type = tag, Children = childIds };
    }

    private Filter ParseSingleChild(XElement elem, string id, string parentId, int parentIndex, string tag)
    {
        var effectiveId = id.Length > 0 ? id : (parentId.Length > 0 ? $"{parentId}__anon_{parentIndex}" : "");
        var childId = "";
        foreach (var (childElem, i) in elem.Elements().Select((c, i) => (c, i)))
        {
            var child = ParseFilterNode(childElem, effectiveId, i);
            if (child is not null) { childId = ChildRef(child); break; }
        }
        return new Filter { Id = id, Type = tag, Child = childId };
    }

    private Filter ParseBlocks(XElement elem, string id, string parentId, int parentIndex)
    {
        var effectiveId = id.Length > 0 ? id : (parentId.Length > 0 ? $"{parentId}__anon_{parentIndex}" : "");
        var region = Xml.Get(elem, "region", "");
        var childId = "";
        foreach (var (childElem, i) in elem.Elements().Select((c, i) => (c, i)))
        {
            var child = ParseFilterNode(childElem, effectiveId, i);
            if (child is not null) { childId = ChildRef(child); break; }
        }
        return new Filter { Id = id, Type = "blocks", RegionRef = region, Child = childId };
    }

    private static Filter ParseItemFilter(XElement elem, string id, string kind)
    {
        var ignoreMetadata = Xml.Get(elem, "ignore-metadata", "false").ToLowerInvariant() == "true";
        var ignoreDurability = Xml.Get(elem, "ignore-durability", "true").ToLowerInvariant() != "false";

        var material = "";
        int? damage = null;
        var enchantments = "";
        var item = elem.Elements("item").FirstOrDefault();
        if (item is not null)
        {
            material = Xml.Get(item, "material", "").Trim();
            var dmg = Xml.Get(item, "damage", "");
            if (dmg.Length > 0 && int.TryParse(dmg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dv)) damage = dv;
            enchantments = Xml.Get(item, "enchantment", "").Trim();
        }
        else
        {
            var text = Xml.Text(elem).Trim();
            if (text.Length > 0) material = text;
        }

        return kind switch
        {
            "carrying" => new Filter { Id = id, Type = "carrying", Material = material, Damage = damage, Enchantments = enchantments, IgnoreMetadata = ignoreMetadata, IgnoreDurability = ignoreDurability },
            "wearing" => new Filter { Id = id, Type = "wearing", Material = material, Damage = damage, IgnoreMetadata = ignoreMetadata },
            _ => new Filter { Id = id, Type = "holding", Material = material, Damage = damage },
        };
    }

    private Filter ParseOffset(XElement elem, string id, string parentId, int parentIndex)
    {
        var effectiveId = id.Length > 0 ? id : (parentId.Length > 0 ? $"{parentId}__anon_{parentIndex}" : "");
        var vector = Xml.Get(elem, "vector", "");
        var childId = "";
        foreach (var (childElem, i) in elem.Elements().Select((c, i) => (c, i)))
        {
            var child = ParseFilterNode(childElem, effectiveId, i);
            if (child is not null) { childId = ChildRef(child); break; }
        }
        return new Filter { Id = id, Type = "offset", Vector = vector, Child = childId };
    }

    private static int? SafeInt(string? value)
    {
        if (value is null) return null;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
