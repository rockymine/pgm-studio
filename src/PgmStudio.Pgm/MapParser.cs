using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>Top-level PGM map.xml parser.</summary>
public sealed partial class MapParser
{
    private readonly XElement _root;
    private readonly IncludeLibrary? _includes;
    private readonly RegionParser _regionParser = new();
    private readonly FilterParser _filterParser = new();

    private MapParser(XElement root, IncludeLibrary? includes = null) => (_root, _includes) = (root, includes);

    /// <summary>
    /// Parse a <c>map.xml</c>. With no <paramref name="includes"/> library the map is read exactly as it is
    /// written, and whatever its <c>&lt;include&gt;</c> references define stays outside the document
    /// (<see cref="MapXml.Includes"/> records them; <see cref="MapValidity"/> warns).
    ///
    /// <para>With a library, the fragments are spliced in first, so the result describes the map <b>as the
    /// server plays it</b> rather than as its author wrote it. That is an <b>analysis</b> read and must not be
    /// re-exported: the include references are still recorded and still emitted, so writing a resolved map back
    /// out would emit the fragments' content inline <i>and</i> reference them again. Export parses without a
    /// library, which is the default.</para>
    /// </summary>
    public static MapXml Parse(string xmlPath, IncludeLibrary? includes = null)
    {
        var doc = XDocument.Load(xmlPath, LoadOptions.None);
        return new MapParser(doc.Root!, includes).ParseInternal();
    }

    /// <inheritdoc cref="Parse(string, IncludeLibrary?)"/>
    public static MapXml ParseXmlString(string xml, IncludeLibrary? includes = null)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        return new MapParser(doc.Root!, includes).ParseInternal();
    }

    // The studio targets PGM's id-based regions/filters/kits, introduced in proto 1.4.0, and reads
    // pre-"flattening" (pre-1.13) numeric-block worlds.
    private static readonly Version MinProto = new(1, 4, 0);
    private static readonly Version FirstModernServer = new(1, 13, 0);   // the 1.13 block-id "flattening"

    // PGM's objective modules: the root elements whose module contributes a non-auxiliary gamemode when it
    // parses anything. The gamemode falls out of which of these are present — PGM never reads the
    // <gamemode> element itself to decide (Domain.Gamemodes). Auxiliary modules (blitz, ffa, rage) modify
    // how a map plays rather than what its goal is, so they are not objectives and are not listed.
    private static readonly Dictionary<string, string> ObjectiveModules = new()
    {
        ["wools"] = "CTW",
        ["destroyables"] = "DTM",
        ["cores"] = "DTC",
        ["control-points"] = "CP/KOTH",
        ["king"] = "KOTH",
        ["payloads"] = "Payload",
        ["flags"] = "CTF",
        ["score"] = "TDM",
    };

    // The subset we actually read. A listed-but-unread module is an objective the map would lose on
    // round-trip with no error, so its presence rejects the map instead.
    private static readonly HashSet<string> ParsedObjectiveModules = ["wools", "destroyables", "cores"];

    // Reject maps outside the supported range up front rather than silently mis-parsing them: the old
    // positional format below proto 1.4.0 (anonymous teams, no region/filter ids), modern worlds whose
    // 1.13+ palette chunks the Anvil reader cannot decode, and maps whose objective we cannot represent.
    private void EnsureSupported()
    {
        var protoText = _root.Attribute("proto")?.Value;
        if (protoText is null || !Version.TryParse(protoText, out var proto))
            throw new UnsupportedMapException(
                $"map.xml declares no parseable proto; the studio supports proto >= {MinProto} (id-based regions/filters/kits).");
        if (proto < MinProto)
            throw new UnsupportedMapException(
                $"map proto {protoText} is below the supported floor {MinProto} (pre id-based regions/filters/kits).");

        var serverText = _root.Attribute("min-server-version")?.Value;
        if (serverText is not null && Version.TryParse(serverText, out var server) && server >= FirstModernServer)
            throw new UnsupportedMapException(
                $"map requires server {serverText}: modern (>= {FirstModernServer}) worlds use the palette block format the Anvil reader does not support yet.");

        EnsureObjectivesReadable();
    }

    // An objective module we do not read would be dropped in silence: the map parses, exports, and plays
    // without its goal. Reject it instead — the objective is the map.
    private void EnsureObjectivesReadable()
    {
        var unread = _root.Elements()
            .Select(e => e.Name.LocalName)
            .Where(t => ObjectiveModules.ContainsKey(t) && !ParsedObjectiveModules.Contains(t))
            .Distinct()
            .ToList();
        if (unread.Count == 0) return;

        var described = string.Join(", ", unread.Select(t => $"<{t}> ({ObjectiveModules[t]})"));
        throw new UnsupportedMapException(
            $"map declares an objective the studio cannot read: {described}. Parsing it would drop the objective silently on round-trip.");
    }

    private MapXml ParseInternal()
    {
        // The supported-range gates read the map's OWN body, before any fragment is spliced. A module arriving
        // from an include is not at risk of being lost on round-trip — the export re-emits the reference, and
        // the server resolves it again — so the unread-objective gate, which exists to stop exactly that silent
        // loss, has nothing to catch there. Gating after the splice would reject 82 corpus maps that today
        // parse and re-export perfectly.
        EnsureSupported();

        // The referenced ids are recorded before splicing removes the elements, so a resolved map still knows
        // (and still re-emits) what it pulled in.
        var referenced = ParseIncludes();
        var resolved = _includes?.Splice(_root) ?? [];

        ResolveVariants(_root);
        ResolveConstants(_root);

        var data = new MapXml
        {
            Name = GetText("name", ""),
            Version = GetText("version", ""),
            DeclaredGamemode = GetTextList("gamemode"),
            Objective = GetText("objective", ""),
            Authors = ParseAuthors(),
            Teams = ParseTeams(),
            Kits = ParseKits(),
        };
        (data.Spawns, data.ObserverSpawn) = ParseSpawns();

        // EVERY <filters>/<regions> block, not the first. A map written by hand has one of each, but a
        // resolved one has the fragments' blocks beside its own, and reading only the first would silently
        // drop whichever lost the race — the map's own, when a fragment was spliced ahead of it. Both parsers
        // accumulate into one registry, so repeated blocks merge the way PGM merges them, and apply rules
        // concatenate in document order because PGM stops at the first rule that decides.
        data.Filters = _filterParser.Registry();
        foreach (var filtersElem in _root.Elements("filters"))
            data.Filters = _filterParser.ParseFiltersElem(filtersElem);

        data.Regions = _regionParser.Registry();
        foreach (var regionsElem in _root.Elements("regions"))
        {
            var (regions, applyRules) = _regionParser.ParseRegionsElem(regionsElem);
            data.Regions = regions;
            data.ApplyRules.AddRange(applyRules);
        }

        ResolveSpawnRegions(data);

        data.Wools = ParseWools(data.Regions);
        data.Modes = ParseModes();
        data.Destroyables = ParseDestroyables();
        data.Cores = ParseCores();
        data.Spawners = ParseSpawners();
        data.Renewables = ParseRenewables();
        data.BlockDropRules = ParseBlockDropRules();
        data.MaxBuildHeight = ParseMaxBuildHeight();
        data.Includes = referenced;
        data.ResolvedIncludes = [.. resolved];
        data.Fills = ParseFills();
        data.Constants = _constants;
        return data;
    }

    // <include id="…"/> — the id only. The fragment's body lives in the server's includes directory, so
    // recording the reference is the most that can be known here; what the map means by it stays unread.
    // PGM also splices a `global` include into every map that no map.xml mentions, and that one is invisible
    // from the document too.
    private List<string> ParseIncludes() =>
        [.. _root.Descendants("include")
              .Select(e => Xml.Get(e, "id"))
              .Where(id => id.Length > 0)];

    // <fill> leaves anywhere under <actions>, which nests them under <action> or lists them directly.
    private List<FillAction> ParseFills()
    {
        var actionBlocks = _root.Elements("actions").ToList();
        // <trigger action="id" filter="…"/> fires an action declared elsewhere, so the condition has to be
        // carried back to the fills inside that action rather than read from where they sit.
        var triggerByAction = actionBlocks
            .SelectMany(a => a.Descendants("trigger"))
            .Where(t => Xml.Get(t, "action").Length > 0)
            .GroupBy(t => Xml.Get(t, "action"))
            .ToDictionary(g => g.Key, g => TriggerCondition(g.First()));

        return [.. actionBlocks
            .SelectMany(a => a.Descendants("fill"))
            .Select(f => new FillAction
            {
                Id = Xml.Get(f, "id"),
                RegionId = Xml.Get(f, "region"),
                Material = Xml.Get(f, "material"),
                FilterId = Xml.Get(f, "filter"),
                Trigger = FillTrigger(f, triggerByAction),
            })];
    }

    // The condition a fill runs under, looked up from the outside in: the trigger that encloses it, else the
    // trigger that names the action it sits in. Empty when neither states one in a form read here.
    private static string FillTrigger(XElement fill, IReadOnlyDictionary<string, string> triggerByAction)
    {
        foreach (var ancestor in fill.Ancestors())
        {
            if (ancestor.Name.LocalName == "trigger") return TriggerCondition(ancestor);
            if (ancestor.Name.LocalName == "action" && Xml.Get(ancestor, "id") is { Length: > 0 } actionId)
                return triggerByAction.GetValueOrDefault(actionId, "");
        }
        return "";
    }

    // A trigger states its condition either as a filter reference or as an inline <after duration="…">
    // countdown, and the duration is the more useful of the two when both are present.
    private static string TriggerCondition(XElement trigger)
    {
        if (trigger.Descendants("after").FirstOrDefault() is { } after
            && Xml.Get(after, "duration") is { Length: > 0 } duration) return duration;
        return Xml.Get(trigger, "filter");
    }

    // ── variant / constant preprocessing ────────────────────────────────────────────
    private static void ResolveVariants(XElement element)
    {
        foreach (var child in element.Elements().ToList()) ResolveVariants(child);

        var newChildren = new List<XElement>();
        var changed = false;
        foreach (var child in element.Elements().ToList())
        {
            var tag = child.Name.LocalName;
            if (tag is "if" or "unless")
            {
                changed = true;
                var variants = Xml.Get(child, "variant", "").Split(',').Select(v => v.Trim()).ToHashSet();
                var include = (tag == "if" && variants.Contains("default")) || (tag == "unless" && !variants.Contains("default"));
                if (include) newChildren.AddRange(child.Elements());
            }
            else newChildren.Add(child);
        }
        if (changed)
        {
            foreach (var c in newChildren) c.Remove();
            element.RemoveNodes();
            foreach (var c in newChildren) element.Add(c);
        }
    }

    // The constants declared in the document, id → value, in the order the resolver read them. Retained past
    // substitution: a map that declares a constant it never interpolates is tuning a rule an include owns.
    private Dictionary<string, string> _constants = new(StringComparer.Ordinal);

    private void ResolveConstants(XElement root)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        _constants = constants;
        foreach (var elem in root.Descendants("constant"))
        {
            var cid = Xml.Get(elem, "id", "").Trim();
            if (cid.Length > 0) constants[cid] = Xml.Text(elem).Trim();
        }
        if (constants.Count == 0) return;

        string Sub(string value) => ConstantPattern().Replace(value,
            m => constants.GetValueOrDefault(m.Groups[1].Value, m.Value));

        foreach (var elem in root.DescendantsAndSelf())
            foreach (var attr in elem.Attributes().ToList())
                if (attr.Value.Contains("${"))
                    attr.Value = Sub(attr.Value);
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex ConstantPattern();

    // ── simple helpers ──────────────────────────────────────────────────────────────
    private string GetText(string tag, string def = "")
    {
        var elem = _root.Elements(tag).FirstOrDefault();
        if (elem is null) return def;
        var t = Xml.Text(elem);
        return t.Length > 0 ? t : def;
    }

    // Every occurrence of a repeated element, not the first — PGM reads <gamemode> this way
    // (MapInfoImpl.parseGamemodes loops root.getChildren("gamemode")), so a corpus map declaring several
    // must keep all of them rather than silently dropping every one after the first.
    private List<string> GetTextList(string tag) =>
        [.. _root.Elements(tag).Select(Xml.Text).Where(t => t.Length > 0)];

    private static string NonEmpty(string s, string def) => string.IsNullOrEmpty(s) ? def : s;

    private static Vec3 Coords3OrZero(string s)
    {
        var c = Xml.Coords3(s);
        return new Vec3(Xml.Or0(c[0]), Xml.Or0(c[1]), Xml.Or0(c[2]));
    }

    // ── sections ────────────────────────────────────────────────────────────────────
    /// <summary>The authors and contributors. A person is an account (<c>uuid</c>) <b>or</b> a pseudonym (the
    /// element's own text) — PGM takes either and requires one, so reading only the uuid dropped every
    /// name-only author a map declared and lost them on round-trip.</summary>
    private List<Author> ParseAuthors()
    {
        var authors = new List<Author>();
        void Read(string blockTag, string itemTag, string role)
        {
            foreach (var elem in (_root.Elements(blockTag).FirstOrDefault()?.Elements(itemTag) ?? []))
            {
                var (uuid, name) = (Xml.Get(elem, "uuid", ""), (elem.Value ?? "").Trim());
                if (uuid.Length == 0 && name.Length == 0) continue;
                authors.Add(new Author
                {
                    Uuid = uuid,
                    Name = name,
                    Role = role,
                    Contribution = Xml.Get(elem, "contribution", ""),
                });
            }
        }
        Read("authors", "author", "author");
        Read("contributors", "contributor", "contributor");
        return authors;
    }

    private List<Team> ParseTeams()
    {
        var teams = new List<Team>();
        var teamsElem = _root.Elements("teams").FirstOrDefault();
        if (teamsElem is null) return teams;
        foreach (var t in teamsElem.Elements("team"))
            teams.Add(new Team
            {
                Id = Xml.Get(t, "id", ""),
                Color = Xml.Get(t, "color", ""),
                MaxPlayers = Xml.IntAttr(t, "max", 0),
                MinPlayers = Xml.IntAttr(t, "min", 0),
                Name = Xml.Text(t),
                DyeColor = Xml.Get(t, "dye-color", ""),
            });
        return teams;
    }

    private List<Kit> ParseKits()
    {
        var kits = new List<Kit>();
        foreach (var kitElem in _root.Elements("kits").SelectMany(block => block.Elements("kit")))
        {
            var kitId = Xml.Get(kitElem, "id", "");
            if (kitId.Length == 0) continue;

            var items = new List<KitItem>();
            foreach (var itemElem in kitElem.Elements("item"))
            {
                var material = Xml.Get(itemElem, "material", "").Trim();
                if (material.Length == 0) continue;
                if (!int.TryParse(Xml.Get(itemElem, "slot", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot)) continue;
                items.Add(new KitItem
                {
                    Slot = slot, Material = material,
                    Amount = Xml.IntAttr(itemElem, "amount", 1),
                    ItemDamage = Xml.IntAttr(itemElem, "damage", 0),
                    Unbreakable = Xml.BoolAttr(itemElem, "unbreakable"),
                    TeamColor = Xml.BoolAttr(itemElem, "team-color"),
                    Enchantments = CollectEnchantments(itemElem),
                });
            }

            var armor = new List<KitArmor>();
            foreach (var slotName in new[] { "helmet", "chestplate", "leggings", "boots" })
            {
                var armorElem = kitElem.Elements(slotName).FirstOrDefault();
                if (armorElem is null) continue;
                var material = Xml.Get(armorElem, "material", "").Trim();
                if (material.Length == 0) continue;
                armor.Add(new KitArmor
                {
                    SlotName = slotName, Material = material,
                    Unbreakable = Xml.BoolAttr(armorElem, "unbreakable"),
                    TeamColor = Xml.BoolAttr(armorElem, "team-color"),
                    Enchantments = CollectEnchantments(armorElem),
                });
            }

            var effects = new List<KitEffect>();
            foreach (var effElem in kitElem.Elements("effect"))
            {
                var type = effElem.Value.Trim();
                if (type.Length == 0) continue;
                effects.Add(new KitEffect { Type = type, Duration = Xml.Get(effElem, "duration", ""), Amplifier = Xml.IntAttr(effElem, "amplifier", 0) });
            }

            if (items.Count > 0 || armor.Count > 0 || effects.Count > 0)
                kits.Add(new Kit { Id = kitId, Force = Xml.BoolAttr(kitElem, "force"), Items = items, Armor = armor, Effects = effects });
        }
        return kits;
    }

    private static string CollectEnchantments(XElement elem)
    {
        var parts = new List<string>();
        var attr = Xml.Get(elem, "enchantment", "").Trim();
        if (attr.Length > 0)
            foreach (var rawToken in attr.Split(';'))
            {
                var token = rawToken.Trim();
                if (token.Length == 0) continue;
                string name; int level;
                var idx = token.LastIndexOf(':');
                if (idx >= 0)
                {
                    name = token[..idx].Trim().Replace(' ', '_');
                    level = int.TryParse(token[(idx + 1)..].Trim(), out var lv) ? lv : 1;
                }
                else { name = token.Replace(' ', '_'); level = 1; }
                parts.Add($"{name}:{level}");
            }
        foreach (var child in elem.Elements("enchantment"))
        {
            var name = Xml.Text(child).Trim().Replace(' ', '_');
            var level = int.TryParse(Xml.Get(child, "level", "1"), out var lv) ? lv : 1;
            if (name.Length > 0) parts.Add($"{name}:{level}");
        }
        return string.Join(",", parts);
    }

    private (List<Spawn>, Spawn?) ParseSpawns()
    {
        var spawns = new List<Spawn>();
        Spawn? observer = null;
        var spawnsElem = _root.Elements("spawns").FirstOrDefault();
        if (spawnsElem is null) return (spawns, observer);

        var (spawnElems, defaultElem) = CollectSpawnElements(spawnsElem, "");
        foreach (var (spawnElem, inheritedKit) in spawnElems)
            spawns.Add(ParseSpawnElement(spawnElem, inheritedKit));
        if (defaultElem is not null) observer = ParseSpawnElement(defaultElem, "");
        return (spawns, observer);
    }

    private (List<(XElement, string)>, XElement?) CollectSpawnElements(XElement parent, string inheritedKit)
    {
        var results = new List<(XElement, string)>();
        XElement? defaultElem = null;
        foreach (var child in parent.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "spawn": results.Add((child, inheritedKit)); break;
                case "default": defaultElem ??= child; break;
                case "spawns":
                    var kit = NonEmpty(Xml.Get(child, "kit", ""), inheritedKit);
                    var (nested, nestedDefault) = CollectSpawnElements(child, kit);
                    results.AddRange(nested);
                    defaultElem ??= nestedDefault;
                    break;
            }
        }
        return (results, defaultElem);
    }

    private Spawn ParseSpawnElement(XElement elem, string inheritedKit)
    {
        var team = Xml.Get(elem, "team", "");
        Region? region = null;
        var regionAttr = Xml.Get(elem, "region", "");
        if (regionAttr.Length > 0)
        {
            region = new Region { Id = "", Type = "reference", RefId = regionAttr };  // resolved later
        }
        else
        {
            var regionElem = elem.Elements("region").FirstOrDefault() ?? elem.Elements("regions").FirstOrDefault();
            if (regionElem is not null)
            {
                var syntheticId = team.Length > 0 ? $"__spawn_{team}" : "__observer_spawn";
                region = _regionParser.ParseSpawnRegion(regionElem, syntheticId);
            }
        }

        var yaw = double.TryParse(Xml.Get(elem, "yaw", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : 0.0;
        return new Spawn { Team = team, Kit = NonEmpty(Xml.Get(elem, "kit", ""), inheritedKit), Yaw = yaw, Region = region };
    }

    private void ResolveSpawnRegions(MapXml data)
    {
        foreach (var spawn in data.Spawns)
            if (spawn.Region is { Type: "reference" } r)
                spawn.Region = _regionParser.ResolveReference(r.RefId ?? "") ?? spawn.Region;
        if (data.ObserverSpawn?.Region is { Type: "reference" } or_)
            data.ObserverSpawn.Region = _regionParser.ResolveReference(or_.RefId ?? "") ?? data.ObserverSpawn.Region;
    }

    private List<Wool> ParseWools(Dictionary<string, Region> regions)
    {
        var wools = new List<Wool>();
        foreach (var wool in Xml.Flatten(_root, "wools", "wool"))
        {
            var (monument, monumentRegionId) = ResolveMonument(wool, regions);
            wools.Add(new Wool
            {
                Team = wool.Get("team"),
                Color = wool.Get("color"),
                Location = Coords3OrZero(wool.Get("location", "0,0,0")),
                Monument = monument,
                MonumentRegionId = monumentRegionId,
            });
        }
        return wools;
    }

    private (Vec3, string?) ResolveMonument(InheritedElement wool, Dictionary<string, Region> regions)
    {
        foreach (var path in new[] { ("monument", "block"), ("monument", "point") })
        {
            var child = wool.Element.Elements(path.Item1).FirstOrDefault()?.Elements(path.Item2).FirstOrDefault();
            if (child is not null && Xml.Text(child).Length > 0)
                return (Coords3OrZero(Xml.Text(child)), null);
        }
        var monumentRef = wool.GetOrNull("monument");
        if (monumentRef is not null && monumentRef.Length > 0)
        {
            var region = regions.GetValueOrDefault(monumentRef);
            if (region is { Type: "block" or "point" })
                return (new Vec3(Xml.Or0(region.PosX), Xml.Or0(region.PosY), Xml.Or0(region.PosZ)), monumentRef);
        }
        return (new Vec3(0, 0, 0), null);
    }

    // ── destroyables (DTM) + objective modes ────────────────────────────────────────
    // Both are id-keyed features referenced by `modes="a b"`, so modes parse first.
    private List<ObjectiveMode> ParseModes()
    {
        var modes = new List<ObjectiveMode>();
        var used = new HashSet<string>();
        foreach (var mode in Xml.Flatten(_root, "modes", "mode"))
        {
            var name = mode.Get("name");
            var material = mode.Get("material");
            // PGM auto-generates an id from the name (or the material, when unnamed) whenever the XML omits
            // one. Generate the same way so `modes="…"` always resolves against a key we hold.
            var id = mode.Get("id");
            if (id.Length == 0) id = UniqueId($"mode-{Slug(name.Length > 0 ? name : material)}", used);
            used.Add(id);

            modes.Add(new ObjectiveMode
            {
                Id = id,
                Name = name,
                After = mode.Get("after"),
                Material = material,
                // `boss-bar="false"` is PGM's way of spelling "no countdown", i.e. show-before = 0.
                ShowBefore = mode.Bool("boss-bar", true) ? mode.Get("show-before") : "0s",
                FilterId = mode.Get("filter"),
                ActionId = mode.Get("action"),
            });
        }
        return modes;
    }

    private List<Destroyable> ParseDestroyables()
    {
        var destroyables = new List<Destroyable>();
        var used = new HashSet<string>();
        foreach (var d in Xml.Flatten(_root, "destroyables", "destroyable"))
        {
            var name = d.Get("name");
            var owner = d.Get("owner");
            var id = d.Get("id");
            if (id.Length == 0) id = UniqueId(Slug($"{owner}-{name}"), used);
            used.Add(id);

            var (modeChanges, modes) = ParseModeMembership(d);
            destroyables.Add(new Destroyable
            {
                Id = id,
                Name = name,
                Owner = owner,
                RegionId = ResolveObjectiveRegion(d, $"__destroyable_{id}") ?? "",
                // PGM accepts either spelling, preferring the plural.
                Materials = NonEmpty(d.Get("materials"), d.Get("material")),
                Completion = ParsePercent(d.GetOrNull("completion")),
                Show = d.Bool("show", true),
                ModeChanges = modeChanges,
                Modes = modes,
            });
        }
        return destroyables;
    }

    private List<Core> ParseCores()
    {
        var cores = new List<Core>();
        var used = new HashSet<string>();
        foreach (var c in Xml.Flatten(_root, "cores", "core"))
        {
            var name = c.Get("name");
            var owner = c.Get("team");   // PGM spells the owner `team` here (OB1)
            var id = c.Get("id");
            if (id.Length == 0) id = UniqueId(Slug($"{owner}-{NonEmpty(name, "core")}"), used);
            used.Add(id);

            var (modeChanges, modes) = ParseModeMembership(c);
            cores.Add(new Core
            {
                Id = id,
                Name = name,
                Owner = owner,
                RegionId = ResolveObjectiveRegion(c, $"__core_{id}") ?? "",
                Material = c.Get("material"),
                Leak = int.TryParse(c.Get("leak"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var leak) ? leak : null,
                ModeChanges = modeChanges,
                Modes = modes,
            });
        }
        return cores;
    }

    // Mode membership is a tri-state, not a list: `modes="a b"` is a specific set, `mode-changes="true"`
    // means every mode (modelled as no set rather than an enumerated one), and neither means no modes.
    // Declaring both is contradictory and PGM rejects it.
    private static (bool modeChanges, List<string>? modes) ParseModeMembership(InheritedElement e)
    {
        var modeChanges = e.Bool("mode-changes");
        var listed = e.Get("modes").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (modeChanges && listed.Count > 0)
            throw new UnsupportedMapException(
                $"<{e.Element.Name.LocalName}> combines modes=\"{e.Get("modes")}\" with mode-changes=\"true\"; they are mutually exclusive (mode-changes already means every mode).");
        return (modeChanges, listed.Count > 0 ? listed : null);
    }

    /// <summary>
    /// Resolve an objective's region property, which PGM spells two ways at our proto floor: a
    /// <c>region="id"</c> attribute, or a <c>&lt;region&gt;</c> child wrapping the geometry. The wrapper is
    /// the union of its own <c>region=</c> reference and every nested region, so a multi-shape wrapper
    /// registers a synthetic union rather than silently keeping only the first shape. The bare-geometry
    /// form (a <c>&lt;cuboid&gt;</c> straight under the leaf) is legacy and cannot occur above proto 1.3.6.
    /// </summary>
    private string? ResolveObjectiveRegion(InheritedElement e, string syntheticId)
    {
        var wrapper = e.Element.Elements("region").FirstOrDefault();
        if (wrapper is null) return e.GetOrNull("region");
        return _regionParser.ParseRegionProperty(wrapper, syntheticId)?.Id;
    }

    // PGM's parsePercent strips any '%' and divides by 100 — so `completion="90"` and `completion="90%"`
    // both mean 0.9, and `completion="0.8"` means 0.8%, not 80%. Store the fraction.
    private static double? ParsePercent(string? raw)
    {
        if (raw is null) return null;
        var text = raw.Replace("%", "").Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 100.0 : null;
    }

    private static string Slug(string value)
    {
        // Strip PGM's `-prefixed colour codes, then lowercase and hyphenate runs of whitespace. Trailing
        // separators are trimmed because a name is routinely absent or explicitly empty, which would
        // otherwise leave a dangling hyphen in the generated key.
        var stripped = ColorCode().Replace(value, "");
        var slug = WhitespaceRun().Replace(stripped.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 0 ? slug : "unnamed";
    }

    private static string UniqueId(string baseId, HashSet<string> used)
    {
        if (!used.Contains(baseId)) return baseId;
        for (var i = 2; ; i++)
            if (!used.Contains($"{baseId}-{i}")) return $"{baseId}-{i}";
    }

    [GeneratedRegex(@"[`§].")]
    private static partial Regex ColorCode();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    private List<WoolSpawner> ParseSpawners()
    {
        var spawners = new List<WoolSpawner>();
        foreach (var elem in _root.Descendants("spawners").SelectMany(s => s.Elements("spawner")))
        {
            var spawnRegion = Xml.Get(elem, "spawn-region", "").Trim();
            var playerRegion = Xml.Get(elem, "player-region", "").Trim();
            if (spawnRegion.Length == 0 || playerRegion.Length == 0) continue;

            var maxEntitiesStr = Xml.Get(elem, "max-entities", "");
            int? maxEntities = maxEntitiesStr.All(char.IsDigit) && maxEntitiesStr.Length > 0 ? int.Parse(maxEntitiesStr) : null;

            var items = new List<SpawnerItem>();
            foreach (var itemElem in elem.Elements("item"))
            {
                var material = Xml.Get(itemElem, "material", "").Trim();
                var dmgOk = int.TryParse(Xml.Get(itemElem, "damage", "0"), out var damage);
                var amtOk = int.TryParse(Xml.Get(itemElem, "amount", "1"), out var amount);
                items.Add(new SpawnerItem { Material = material, Damage = dmgOk ? damage : 0, Amount = amtOk ? amount : 1 });
            }
            spawners.Add(new WoolSpawner { SpawnRegion = spawnRegion, PlayerRegion = playerRegion, Delay = Xml.Get(elem, "delay", ""), MaxEntities = maxEntities, Items = items });
        }
        return spawners;
    }

    private List<Renewable> ParseRenewables()
    {
        var renewables = new List<Renewable>();
        foreach (var elem in _root.Descendants("renewables").SelectMany(s => s.Elements("renewable")))
        {
            var regionId = Xml.Get(elem, "region", "").Trim();
            if (regionId.Length == 0) continue;
            var rate = double.TryParse(Xml.Get(elem, "rate", "1.0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rt) ? rt : 1.0;
            renewables.Add(new Renewable
            {
                RegionId = regionId, Rate = rate,
                RenewFilter = Xml.Get(elem, "renew-filter", "").Trim(),
                ReplaceFilter = Xml.Get(elem, "replace-filter", "").Trim(),
                Grow = Xml.Get(elem, "grow", "false").Trim().ToLowerInvariant() == "true",
            });
        }
        return renewables;
    }

    private List<BlockDropRule> ParseBlockDropRules()
    {
        var rules = new List<BlockDropRule>();
        foreach (var elem in _root.Descendants("block-drops").SelectMany(s => s.Elements("rule")))
        {
            var replacementElem = elem.Elements("replacement").FirstOrDefault();
            var replacement = replacementElem is not null ? Xml.Text(replacementElem).Trim() : "";

            var items = new List<BlockDropItem>();
            var dropsElem = elem.Elements("drops").FirstOrDefault();
            if (dropsElem is not null)
                foreach (var itemElem in dropsElem.Elements("item"))
                {
                    var material = Xml.Get(itemElem, "material", "").Trim();
                    if (material.Length == 0) continue;
                    var dmgOk = int.TryParse(Xml.Get(itemElem, "damage", "0"), out var damage);
                    var amtOk = int.TryParse(Xml.Get(itemElem, "amount", "1"), out var amount);
                    var chOk = double.TryParse(Xml.Get(itemElem, "chance", "1.0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var chance);
                    var allOk = dmgOk && amtOk && chOk;
                    items.Add(new BlockDropItem { Material = material, Damage = allOk ? damage : 0, Amount = allOk ? amount : 1, Chance = allOk ? chance : 1.0 });
                }

            rules.Add(new BlockDropRule
            {
                RegionId = Xml.Get(elem, "region", "").Trim(),
                FilterId = Xml.Get(elem, "filter", "").Trim(),
                Replacement = replacement,
                WrongTool = Xml.Get(elem, "wrong-tool", "false").Trim().ToLowerInvariant() == "true",
                Items = items,
            });
        }
        return rules;
    }

    private int? ParseMaxBuildHeight()
    {
        var elem = _root.Elements("maxbuildheight").FirstOrDefault();
        if (elem is null) return null;
        var t = Xml.Text(elem).Trim();
        return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
