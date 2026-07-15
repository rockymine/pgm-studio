using PgmStudio.Domain;

namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>JSON object tree → MapXml (port of deserializer.py; inverse of <see cref="Serializer"/>).</summary>
public static class Deserializer
{
    public static MapXml FromDict(Dict d)
    {
        var regions = DictOf(d, "regions").ToDictionary(kv => kv.Key, kv => DecodeRegion(AsDict(kv.Value)));
        var filters = DictOf(d, "filters").ToDictionary(kv => kv.Key, kv => DecodeFilter(AsDict(kv.Value)));

        var wools = new List<Wool>();
        foreach (var w in ListOf(d, "wools")) wools.AddRange(DecodeWoolsEntry(AsDict(w)));

        var obs = Val(d, "observer_spawn");
        return new MapXml
        {
            Name = Str(d, "name"),
            Version = Str(d, "version"),
            Gamemode = Str(d, "gamemode", "ctw"),
            Objective = Str(d, "objective"),
            MaxBuildHeight = AsIntN(Val(d, "max_build_height")),
            Authors = ListOf(d, "authors").Select(a => DecodeAuthor(AsDict(a))).ToList(),
            Kits = ListOf(d, "kits").Select(k => DecodeKit(AsDict(k))).ToList(),
            Teams = ListOf(d, "teams").Select(t => DecodeTeam(AsDict(t))).ToList(),
            Spawns = ListOf(d, "spawns").Select(s => DecodeSpawn(AsDict(s), regions)).ToList(),
            ObserverSpawn = obs is null ? null : DecodeSpawn(AsDict(obs), regions),
            Wools = wools,
            Destroyables = ListOf(d, "destroyables").Select(x => DecodeDestroyable(AsDict(x))).ToList(),
            Modes = ListOf(d, "modes").Select(x => DecodeMode(AsDict(x))).ToList(),
            Spawners = ListOf(d, "spawners").Select(s => DecodeSpawner(AsDict(s))).ToList(),
            Renewables = ListOf(d, "renewables").Select(r => DecodeRenewable(AsDict(r))).ToList(),
            BlockDropRules = ListOf(d, "block_drop_rules").Select(r => DecodeBlockDropRule(AsDict(r))).ToList(),
            Filters = filters,
            Regions = regions,
            ApplyRules = ListOf(d, "apply_rules").Select(r => DecodeApplyRule(AsDict(r))).ToList(),
        };
    }

    /// <summary>Decode a single region from its JSON dict (for the DB → MapXml reader).</summary>
    public static Region RegionFromDict(Dict d) => DecodeRegion(d);

    /// <summary>Decode a single filter from its JSON dict (for the DB → MapXml reader).</summary>
    public static Filter FilterFromDict(Dict d) => DecodeFilter(d);

    // ── tree access helpers ─────────────────────────────────────────────────────────
    private static object? Val(Dict d, string k) => d.GetValueOrDefault(k);
    private static object? Req(Dict d, string k) => d.TryGetValue(k, out var v) ? v : throw new KeyNotFoundException(k);
    private static Dict AsDict(object? v) => (Dict)v!;
    private static Dict DictOf(Dict d, string k) => d.GetValueOrDefault(k) as Dict ?? new Dict();
    private static List<object?> ListOf(Dict d, string k) => d.GetValueOrDefault(k) as List<object?> ?? [];
    private static string Str(Dict d, string k, string def = "") => d.GetValueOrDefault(k) as string ?? def;
    private static int AsInt(object? v, int def) => v is null ? def : Convert.ToInt32(v);
    private static int? AsIntN(object? v) => v is null ? null : Convert.ToInt32(v);
    private static double AsDouble(object? v, double def) => v is null ? def : Convert.ToDouble(v);
    private static bool AsBool(object? v, bool def) => v is bool b ? b : def;
    private static double Cd(object? v) => Coord.Decode(v);
    private static (double x, double y, double z) Xyz(object? v)
    {
        if (v is not Dict d) return (0, 0, 0);
        return (Convert.ToDouble(d["x"]), Convert.ToDouble(d["y"]), Convert.ToDouble(d["z"]));
    }

    // ── region decode ───────────────────────────────────────────────────────────────
    private static Region DecodeRegion(Dict d)
    {
        var id = Str(d, "id");
        var type = Str(d, "type");
        var r = new Region { Id = id, Type = type };
        switch (type)
        {
            case "rectangle":
                var rb = AsDict(Req(d, "bounds_2d"));
                var mn = AsDict(rb["min"]); var mx = AsDict(rb["max"]);
                r.MinX = Cd(mn["x"]); r.MinZ = Cd(mn["z"]); r.MaxX = Cd(mx["x"]); r.MaxZ = Cd(mx["z"]);
                if (r.MinX is { } a && r.MinZ is { } b && r.MaxX is { } c && r.MaxZ is { } e) r.Bounds2d = Bounds2d.Of(a, b, c, e);
                break;
            case "cuboid":
                var cmn = AsDict(Req(d, "min")); var cmx = AsDict(Req(d, "max"));
                r.MinX = Cd(cmn["x"]); r.MinY = Cd(cmn["y"]); r.MinZ = Cd(cmn["z"]);
                r.MaxX = Cd(cmx["x"]); r.MaxY = Cd(cmx["y"]); r.MaxZ = Cd(cmx["z"]);
                if (r.MinX is { } a2 && r.MinZ is { } b2 && r.MaxX is { } c2 && r.MaxZ is { } e2) r.Bounds2d = Bounds2d.Of(a2, b2, c2, e2);
                break;
            case "cylinder":
                var cb = AsDict(Req(d, "base"));
                r.BaseX = Cd(cb["x"]); r.BaseY = Cd(cb["y"]); r.BaseZ = Cd(cb["z"]);
                r.Radius = Cd(Req(d, "radius"));
                if (d.ContainsKey("height")) r.Height = Cd(d["height"]);
                r.Bounds2d = Bounds2d.Of(Or0(r.BaseX) - Or0(r.Radius), Or0(r.BaseZ) - Or0(r.Radius), Or0(r.BaseX) + Or0(r.Radius), Or0(r.BaseZ) + Or0(r.Radius));
                break;
            case "circle":
                var cc = AsDict(Req(d, "center"));
                r.CenterX = Cd(cc["x"]); r.CenterZ = Cd(cc["z"]); r.Radius = Cd(Req(d, "radius"));
                r.Bounds2d = Bounds2d.Of(Or0(r.CenterX) - Or0(r.Radius), Or0(r.CenterZ) - Or0(r.Radius), Or0(r.CenterX) + Or0(r.Radius), Or0(r.CenterZ) + Or0(r.Radius));
                break;
            case "sphere":
                var so = AsDict(Req(d, "origin"));
                r.OriginX = Cd(so["x"]); r.OriginY = Cd(so["y"]); r.OriginZ = Cd(so["z"]); r.Radius = Cd(Req(d, "radius"));
                r.Bounds2d = Bounds2d.Of(Or0(r.OriginX) - Or0(r.Radius), Or0(r.OriginZ) - Or0(r.Radius), Or0(r.OriginX) + Or0(r.Radius), Or0(r.OriginZ) + Or0(r.Radius));
                break;
            case "block" or "point":
                var p = AsDict(Req(d, "position"));
                r.PosX = Cd(p["x"]); r.PosY = Cd(p["y"]); r.PosZ = Cd(p["z"]);
                r.Bounds2d = type == "block"
                    ? Bounds2d.Of(Or0(r.PosX), Or0(r.PosZ), Or0(r.PosX) + 1, Or0(r.PosZ) + 1)
                    : Bounds2d.Of(Or0(r.PosX) - 0.5, Or0(r.PosZ) - 0.5, Or0(r.PosX) + 0.5, Or0(r.PosZ) + 0.5);
                break;
            case "union" or "negative" or "complement" or "intersect":
                r.Children = ListOf(d, "children").Select(x => (string)x!).ToList();
                break;
            case "mirror":
                var moo = AsDict(Req(d, "origin")); var mno = AsDict(Req(d, "normal"));
                r.SourceId = Str(d, "source_id");
                r.OriginX = Cd(moo["x"]); r.OriginY = Cd(moo["y"]); r.OriginZ = Cd(moo["z"]);
                r.NormalX = Cd(mno["x"]); r.NormalY = Cd(mno["y"]); r.NormalZ = Cd(mno["z"]);
                break;
            case "translate":
                var off = AsDict(Req(d, "offset"));
                r.SourceId = Str(d, "source_id");
                r.OffsetX = Cd(off["x"]); r.OffsetY = Cd(off["y"]); r.OffsetZ = Cd(off["z"]);
                break;
            case "half":
                var ho = AsDict(Req(d, "origin")); var hn = AsDict(Req(d, "normal"));
                r.OriginX = Cd(ho["x"]); r.OriginY = Cd(ho["y"]); r.OriginZ = Cd(ho["z"]);
                r.NormalX = Cd(hn["x"]); r.NormalY = Cd(hn["y"]); r.NormalZ = Cd(hn["z"]);
                break;
            case "reference":
                r.RefId = Str(d, "ref_id");
                break;
            case "everywhere":
                break;
            case "above":
                r.AboveY = Cd(Req(d, "y"));
                break;
        }
        return r;
    }

    private static double Or0(double? v) => v ?? 0.0;

    // ── filter decode ───────────────────────────────────────────────────────────────
    private static Filter DecodeFilter(Dict d)
    {
        var id = Str(d, "id");
        var type = Str(d, "type");
        var f = new Filter { Id = id, Type = type };
        switch (type)
        {
            case "all" or "any" or "one":
                f.Children = ListOf(d, "children").Select(x => (string)x!).ToList(); break;
            case "not" or "deny" or "allow":
                f.Child = Str(d, "child"); break;
            case "team": f.Team = Str(d, "team"); break;
            case "material": f.Material = Str(d, "material"); break;
            case "void": break;
            case "cause": f.Cause = Str(d, "cause"); break;
            case "blocks": f.RegionRef = Str(d, "region"); f.Child = Str(d, "child"); break;
            case "carrying":
                f.Material = Str(d, "material"); f.Damage = AsIntN(Val(d, "damage"));
                f.Enchantments = Str(d, "enchantments");
                f.IgnoreMetadata = AsBool(Val(d, "ignore_metadata"), false);
                f.IgnoreDurability = AsBool(Val(d, "ignore_durability"), true);
                break;
            case "wearing":
                f.Material = Str(d, "material"); f.Damage = AsIntN(Val(d, "damage"));
                f.IgnoreMetadata = AsBool(Val(d, "ignore_metadata"), false); break;
            case "holding":
                f.Material = Str(d, "material"); f.Damage = AsIntN(Val(d, "damage")); break;
            case "time": f.Duration = Str(d, "duration"); break;
            case "after": f.FilterRefId = Str(d, "filter"); f.Duration = Str(d, "duration"); break;
            case "pulse": f.Period = Str(d, "period"); f.Duration = Str(d, "duration"); f.FilterRefId = Str(d, "filter"); break;
            case "offset": f.Vector = Str(d, "vector"); f.Child = Str(d, "child"); break;
            case "variable": f.Var = Str(d, "var"); f.Value = Str(d, "value"); f.Team = Str(d, "team"); break;
            case "completed" or "objective": f.Objective = Str(d, "objective"); break;
            case "kill-streak":
                f.Min = AsIntN(Val(d, "min")); f.Max = AsIntN(Val(d, "max")); f.Count = AsIntN(Val(d, "count")); break;
            case "class": f.Name = Str(d, "name"); break;
            case "region": f.RegionRef = Str(d, "region"); break;
            case "players": f.Min = AsIntN(Val(d, "min")); f.Max = AsIntN(Val(d, "max")); break;
            case "spawn": f.Mob = Str(d, "mob"); break;
        }
        return f;
    }

    // ── other decoders ──────────────────────────────────────────────────────────────
    private static Author DecodeAuthor(Dict d) => new() { Uuid = Str(d, "uuid"), Role = Str(d, "role", "author"), Contribution = Str(d, "contribution"), Name = Str(d, "name") };

    private static Kit DecodeKit(Dict d) => new()
    {
        Id = Str(d, "id"),
        Force = AsBool(Val(d, "force"), false),
        Items = ListOf(d, "items").Select(i => DecodeKitItem(AsDict(i))).ToList(),
        Armor = ListOf(d, "armor").Select(a => DecodeKitArmor(AsDict(a))).ToList(),
        Effects = ListOf(d, "effects").Select(e => DecodeKitEffect(AsDict(e))).ToList(),
    };

    private static KitEffect DecodeKitEffect(Dict d) => new()
    {
        Type = Str(d, "type"), Duration = Str(d, "duration"), Amplifier = AsInt(Val(d, "amplifier"), 0),
    };

    private static KitItem DecodeKitItem(Dict d) => new()
    {
        Slot = AsInt(Val(d, "slot"), 0), Material = Str(d, "material"),
        Amount = AsInt(Val(d, "amount"), 1), ItemDamage = AsInt(Val(d, "damage"), 0),
        Unbreakable = AsBool(Val(d, "unbreakable"), false), TeamColor = AsBool(Val(d, "team_color"), false),
        Enchantments = Str(d, "enchantments"),
    };

    private static KitArmor DecodeKitArmor(Dict d) => new()
    {
        SlotName = Str(d, "slot_name"), Material = Str(d, "material"),
        Unbreakable = AsBool(Val(d, "unbreakable"), false), TeamColor = AsBool(Val(d, "team_color"), false),
        Enchantments = Str(d, "enchantments"),
    };

    private static Team DecodeTeam(Dict d) => new()
    {
        Id = Str(d, "id"), Color = Str(d, "color"),
        MaxPlayers = AsInt(Val(d, "max_players"), 0), MinPlayers = AsInt(Val(d, "min_players"), 0),
        Name = Str(d, "name"), DyeColor = Str(d, "dye_color"),
    };

    private static Spawn DecodeSpawn(Dict d, Dictionary<string, Region> regions)
    {
        Region? region = null;
        var raw = Val(d, "region");
        if (raw is string s) region = regions.GetValueOrDefault(s);
        else if (raw is Dict rd) region = DecodeRegion(rd);
        return new Spawn { Team = Str(d, "team"), Kit = Str(d, "kit"), Yaw = AsDouble(Val(d, "yaw"), 0.0), Region = region };
    }

    private static List<Wool> DecodeWoolsEntry(Dict d)
    {
        if (d.ContainsKey("monuments"))
        {
            var color = Str(d, "color");
            var (lx, ly, lz) = Xyz(Val(d, "location"));
            var room = Val(d, "wool_room_region") as string;
            return ListOf(d, "monuments").Select(mObj =>
            {
                var mon = AsDict(mObj);
                var (mxx, myy, mzz) = Xyz(Val(mon, "location"));
                return new Wool
                {
                    Team = Str(mon, "team"), Color = color, Location = new Vec3(lx, ly, lz),
                    Monument = new Vec3(mxx, myy, mzz), MonumentRegionId = Val(mon, "monument_region") as string,
                    WoolRoomRegion = room,
                };
            }).ToList();
        }
        // legacy flat entry
        var monD = Val(d, "monument") as Dict ?? new Dict();
        var (lx2, ly2, lz2) = Xyz(Val(d, "location"));
        var (mx2, my2, mz2) = Xyz(monD);
        return [new Wool
        {
            Team = Str(d, "team"), Color = Str(d, "color"), Location = new Vec3(lx2, ly2, lz2),
            Monument = new Vec3(mx2, my2, mz2), MonumentRegionId = Val(monD, "region_id") as string,
            WoolRoomRegion = Val(d, "wool_room_region") as string,
        }];
    }

    private static Destroyable DecodeDestroyable(Dict d) => new()
    {
        Id = Str(d, "id"),
        Name = Str(d, "name"),
        Owner = Str(d, "owner"),
        RegionId = Str(d, "region"),
        Materials = Str(d, "materials"),
        Completion = Val(d, "completion") is { } c ? AsDouble(c, 1.0) : null,
        Show = Val(d, "show") is not false,
        ModeChanges = Val(d, "mode_changes") is true,
        Modes = d.ContainsKey("modes") ? ListOf(d, "modes").Select(m => m as string ?? "").ToList() : null,
    };

    private static ObjectiveMode DecodeMode(Dict d) => new()
    {
        Id = Str(d, "id"),
        Name = Str(d, "name"),
        After = Str(d, "after"),
        Material = Str(d, "material"),
        ShowBefore = Str(d, "show_before"),
        FilterId = Str(d, "filter"),
        ActionId = Str(d, "action"),
    };

    private static WoolSpawner DecodeSpawner(Dict d) => new()
    {
        SpawnRegion = Str(d, "spawn_region"), PlayerRegion = Str(d, "player_region"),
        Delay = Str(d, "delay"), MaxEntities = AsIntN(Val(d, "max_entities")),
        Items = ListOf(d, "items").Select(i => DecodeSpawnerItem(AsDict(i))).ToList(),
    };

    private static SpawnerItem DecodeSpawnerItem(Dict d) => new() { Material = Str(d, "material"), Damage = AsInt(Val(d, "damage"), 0), Amount = AsInt(Val(d, "amount"), 1) };

    private static Renewable DecodeRenewable(Dict d) => new()
    {
        RegionId = Str(d, "region_id"), Rate = AsDouble(Val(d, "rate"), 1.0),
        RenewFilter = Str(d, "renew_filter"), ReplaceFilter = Str(d, "replace_filter"), Grow = AsBool(Val(d, "grow"), false),
    };

    private static BlockDropRule DecodeBlockDropRule(Dict d) => new()
    {
        RegionId = Str(d, "region_id"), FilterId = Str(d, "filter_id"), Replacement = Str(d, "replacement"),
        WrongTool = AsBool(Val(d, "wrong_tool"), false),
        Items = ListOf(d, "items").Select(i => DecodeBlockDropItem(AsDict(i))).ToList(),
    };

    private static BlockDropItem DecodeBlockDropItem(Dict d) => new()
    {
        Material = Str(d, "material"), Damage = AsInt(Val(d, "damage"), 0), Amount = AsInt(Val(d, "amount"), 1), Chance = AsDouble(Val(d, "chance"), 1.0),
    };

    private static ApplyRule DecodeApplyRule(Dict d) => new()
    {
        EnterFilter = Str(d, "enter"), LeaveFilter = Str(d, "leave"), BlockFilter = Str(d, "block"),
        BlockPlaceFilter = Str(d, "block_place"), BlockBreakFilter = Str(d, "block_break"),
        BlockPhysicsFilter = Str(d, "block_physics"), BlockPlaceAgainstFilter = Str(d, "block_place_against"),
        UseFilter = Str(d, "use"), FilterId = Str(d, "filter"), RegionId = Str(d, "region"),
        Kit = Str(d, "kit"), LendKit = Str(d, "lend_kit"), Velocity = Str(d, "velocity"), Message = Str(d, "message"),
    };
}
