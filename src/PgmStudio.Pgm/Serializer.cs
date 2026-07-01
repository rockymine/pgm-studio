using PgmStudio.Domain;

namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>MapXml → JSON object tree (port of serializer.py). Mirrors the xml_data.json shape.</summary>
public static class Serializer
{
    public static Dict ToDict(MapXml m) => new()
    {
        ["name"] = m.Name,
        ["version"] = m.Version,
        ["gamemode"] = m.Gamemode,
        ["objective"] = m.Objective,
        ["max_build_height"] = m.MaxBuildHeight,
        ["authors"] = m.Authors.Select(EncodeAuthor).ToList<object?>(),
        ["kits"] = m.Kits.Select(EncodeKit).ToList<object?>(),
        ["teams"] = m.Teams.Select(EncodeTeam).ToList<object?>(),
        ["spawns"] = m.Spawns.Select(EncodeSpawn).ToList<object?>(),
        ["observer_spawn"] = m.ObserverSpawn is null ? null : EncodeSpawn(m.ObserverSpawn),
        ["wools"] = EncodeWoolsGrouped(m.Wools),
        ["spawners"] = m.Spawners.Select(EncodeSpawner).ToList<object?>(),
        ["renewables"] = m.Renewables.Select(EncodeRenewable).ToList<object?>(),
        ["block_drop_rules"] = m.BlockDropRules.Select(EncodeBlockDropRule).ToList<object?>(),
        ["filters"] = m.Filters.ToDictionary(kv => kv.Key, kv => (object?)EncodeFilter(kv.Value)),
        ["regions"] = m.Regions.ToDictionary(kv => kv.Key, kv => (object?)EncodeRegion(kv.Value)),
        ["apply_rules"] = m.ApplyRules.Select(EncodeApplyRule).ToList<object?>(),
    };

    /// <summary>Encode a single region to its JSON dict (for the importer's column split).</summary>
    public static Dict RegionToDict(Region r) => EncodeRegion(r);

    /// <summary>Encode a single filter to its JSON dict (for the importer's column split).</summary>
    public static Dict FilterToDict(Filter f) => EncodeFilter(f);

    /// <summary>Encode a single apply rule to its JSON event dict (for the importer).</summary>
    public static Dict ApplyRuleToDict(ApplyRule r) => EncodeApplyRule(r);

    private static object? C(double? v) => Coord.Encode(v);

    private static Dict EncodeBounds2d(Bounds2d b) => new()
    {
        ["min"] = new Dict { ["x"] = C(b.MinX), ["z"] = C(b.MinZ) },
        ["max"] = new Dict { ["x"] = C(b.MaxX), ["z"] = C(b.MaxZ) },
    };

    private static Dict EncodeRegion(Region r)
    {
        var b = new Dict { ["id"] = r.Id, ["type"] = r.Type };
        if (r.Bounds2d is not null) b["bounds_2d"] = EncodeBounds2d(r.Bounds2d);

        switch (r.Type)
        {
            case "rectangle": break;  // only bounds_2d
            case "cuboid":
                b["min"] = new Dict { ["x"] = C(r.MinX), ["y"] = C(r.MinY), ["z"] = C(r.MinZ) };
                b["max"] = new Dict { ["x"] = C(r.MaxX), ["y"] = C(r.MaxY), ["z"] = C(r.MaxZ) };
                break;
            case "cylinder":
                b["base"] = new Dict { ["x"] = C(r.BaseX), ["y"] = C(r.BaseY), ["z"] = C(r.BaseZ) };
                b["radius"] = C(r.Radius);
                if (r.Height is not null) b["height"] = C(r.Height);
                break;
            case "circle":
                b["center"] = new Dict { ["x"] = C(r.CenterX), ["z"] = C(r.CenterZ) };
                b["radius"] = C(r.Radius);
                break;
            case "sphere":
                b["origin"] = new Dict { ["x"] = C(r.OriginX), ["y"] = C(r.OriginY), ["z"] = C(r.OriginZ) };
                b["radius"] = C(r.Radius);
                break;
            case "block" or "point":
                b["position"] = new Dict { ["x"] = C(r.PosX), ["y"] = C(r.PosY), ["z"] = C(r.PosZ) };
                break;
            case "union" or "negative" or "complement" or "intersect":
                b["children"] = (r.Children ?? []).ToList<object?>();
                break;
            case "half":
                b["origin"] = new Dict { ["x"] = C(r.OriginX), ["y"] = C(r.OriginY), ["z"] = C(r.OriginZ) };
                b["normal"] = new Dict { ["x"] = C(r.NormalX), ["y"] = C(r.NormalY), ["z"] = C(r.NormalZ) };
                break;
            case "mirror":
                b["source_id"] = r.SourceId;
                b["origin"] = new Dict { ["x"] = C(r.OriginX), ["y"] = C(r.OriginY), ["z"] = C(r.OriginZ) };
                b["normal"] = new Dict { ["x"] = C(r.NormalX), ["y"] = C(r.NormalY), ["z"] = C(r.NormalZ) };
                break;
            case "translate":
                b["source_id"] = r.SourceId;
                b["offset"] = new Dict { ["x"] = C(r.OffsetX), ["y"] = C(r.OffsetY), ["z"] = C(r.OffsetZ) };
                break;
            case "reference":
                b["ref_id"] = r.RefId;
                break;
            case "above":
                b["y"] = C(r.AboveY);
                break;
        }
        return b;
    }

    private static Dict EncodeAuthor(Author a)
    {
        var r = new Dict { ["uuid"] = a.Uuid, ["role"] = a.Role };
        if (a.Contribution.Length > 0) r["contribution"] = a.Contribution;
        // name is a studio-side display cache (not present in map.xml) — emit only when set so the
        // map.xml round-trip parity (no name) is preserved.
        if (a.Name.Length > 0) r["name"] = a.Name;
        return r;
    }

    private static Dict EncodeKit(Kit k) => new()
    {
        ["id"] = k.Id,
        ["force"] = k.Force,
        ["items"] = k.Items.Select(EncodeKitItem).ToList<object?>(),
        ["armor"] = k.Armor.Select(EncodeKitArmor).ToList<object?>(),
        ["effects"] = k.Effects.Select(EncodeKitEffect).ToList<object?>(),
    };

    private static Dict EncodeKitEffect(KitEffect e) => new()
    {
        ["type"] = e.Type, ["duration"] = e.Duration, ["amplifier"] = e.Amplifier,
    };

    private static Dict EncodeKitItem(KitItem i)
    {
        var r = new Dict { ["slot"] = i.Slot, ["material"] = i.Material };
        if (i.Amount != 1) r["amount"] = i.Amount;
        if (i.ItemDamage != 0) r["damage"] = i.ItemDamage;
        if (i.Unbreakable) r["unbreakable"] = true;
        if (i.TeamColor) r["team_color"] = true;
        if (i.Enchantments.Length > 0) r["enchantments"] = i.Enchantments;
        return r;
    }

    private static Dict EncodeKitArmor(KitArmor a)
    {
        var r = new Dict { ["slot_name"] = a.SlotName, ["material"] = a.Material };
        if (a.Unbreakable) r["unbreakable"] = true;
        if (a.TeamColor) r["team_color"] = true;
        if (a.Enchantments.Length > 0) r["enchantments"] = a.Enchantments;
        return r;
    }

    private static Dict EncodeTeam(Team t) => new()
    {
        ["id"] = t.Id, ["name"] = t.Name, ["color"] = t.Color,
        ["dye_color"] = t.DyeColor, ["max_players"] = t.MaxPlayers, ["min_players"] = t.MinPlayers,
    };

    private static Dict EncodeSpawn(Spawn s)
    {
        var r = new Dict { ["team"] = s.Team, ["kit"] = s.Kit, ["yaw"] = s.Yaw };
        if (s.Region is not null)
            r["region"] = s.Region.Id.Length > 0 ? s.Region.Id : EncodeRegion(s.Region);
        return r;
    }

    private static string WoolSlug(string value) => value.Trim().ToLowerInvariant().Replace(" ", "_");

    private static List<object?> EncodeWoolsGrouped(List<Wool> wools)
    {
        var order = new List<string>();
        var byColor = new Dictionary<string, Dict>();
        foreach (var w in wools)
        {
            var cslug = WoolSlug(w.Color);
            if (!byColor.TryGetValue(w.Color, out var group))
            {
                group = new Dict
                {
                    ["id"] = cslug,
                    ["color"] = w.Color,
                    ["location"] = new Dict { ["x"] = w.Location.X, ["y"] = w.Location.Y, ["z"] = w.Location.Z },
                    ["wool_room_region"] = w.WoolRoomRegion,
                    ["monuments"] = new List<object?>(),
                };
                byColor[w.Color] = group;
                order.Add(w.Color);
            }
            ((List<object?>)group["monuments"]!).Add(new Dict
            {
                ["id"] = $"{cslug}-{WoolSlug(w.Team)}",
                ["team"] = w.Team,
                ["location"] = new Dict { ["x"] = w.Monument.X, ["y"] = w.Monument.Y, ["z"] = w.Monument.Z },
                ["monument_region"] = w.MonumentRegionId,
            });
        }
        return order.Select(c => (object?)byColor[c]).ToList();
    }

    private static Dict EncodeSpawner(WoolSpawner s)
    {
        var r = new Dict { ["spawn_region"] = s.SpawnRegion, ["player_region"] = s.PlayerRegion };
        if (s.Delay.Length > 0) r["delay"] = s.Delay;
        if (s.MaxEntities is not null) r["max_entities"] = s.MaxEntities;
        if (s.Items.Count > 0) r["items"] = s.Items.Select(EncodeSpawnerItem).ToList<object?>();
        return r;
    }

    private static Dict EncodeSpawnerItem(SpawnerItem i)
    {
        var r = new Dict { ["material"] = i.Material };
        if (i.Damage != 0) r["damage"] = i.Damage;
        if (i.Amount != 1) r["amount"] = i.Amount;
        return r;
    }

    private static Dict EncodeRenewable(Renewable r0)
    {
        var r = new Dict { ["region_id"] = r0.RegionId };
        if (r0.Rate != 1.0) r["rate"] = r0.Rate;
        if (r0.RenewFilter.Length > 0) r["renew_filter"] = r0.RenewFilter;
        if (r0.ReplaceFilter.Length > 0) r["replace_filter"] = r0.ReplaceFilter;
        if (r0.Grow) r["grow"] = true;
        return r;
    }

    private static Dict EncodeBlockDropRule(BlockDropRule r0)
    {
        var r = new Dict();
        if (r0.RegionId.Length > 0) r["region_id"] = r0.RegionId;
        if (r0.FilterId.Length > 0) r["filter_id"] = r0.FilterId;
        if (r0.Replacement.Length > 0) r["replacement"] = r0.Replacement;
        if (r0.WrongTool) r["wrong_tool"] = true;
        if (r0.Items.Count > 0) r["items"] = r0.Items.Select(EncodeBlockDropItem).ToList<object?>();
        return r;
    }

    private static Dict EncodeBlockDropItem(BlockDropItem i)
    {
        var r = new Dict { ["material"] = i.Material };
        if (i.Damage != 0) r["damage"] = i.Damage;
        if (i.Amount != 1) r["amount"] = i.Amount;
        if (i.Chance != 1.0) r["chance"] = i.Chance;
        return r;
    }

    private static Dict EncodeApplyRule(ApplyRule r0)
    {
        var r = new Dict();
        void Put(string k, string v) { if (v.Length > 0) r[k] = v; }
        Put("enter", r0.EnterFilter); Put("leave", r0.LeaveFilter); Put("block", r0.BlockFilter);
        Put("block_place", r0.BlockPlaceFilter); Put("block_break", r0.BlockBreakFilter);
        Put("block_physics", r0.BlockPhysicsFilter); Put("block_place_against", r0.BlockPlaceAgainstFilter);
        Put("use", r0.UseFilter); Put("filter", r0.FilterId); Put("region", r0.RegionId);
        Put("kit", r0.Kit); Put("lend_kit", r0.LendKit); Put("velocity", r0.Velocity); Put("message", r0.Message);
        return r;
    }

    private static Dict EncodeFilter(Filter f)
    {
        var b = new Dict { ["id"] = f.Id, ["type"] = f.Type };
        switch (f.Type)
        {
            case "all" or "any" or "one":
                b["children"] = (f.Children ?? []).ToList<object?>(); break;
            case "not" or "deny" or "allow":
                b["child"] = f.Child ?? ""; break;
            case "team":
                b["team"] = f.Team ?? ""; break;
            case "material":
                b["material"] = f.Material ?? ""; break;
            case "cause":
                b["cause"] = f.Cause ?? ""; break;
            case "blocks":
                b["region"] = f.RegionRef ?? ""; b["child"] = f.Child ?? ""; break;
            case "carrying":
                b["material"] = f.Material ?? "";
                if (f.Damage is not null) b["damage"] = f.Damage;
                if (!string.IsNullOrEmpty(f.Enchantments)) b["enchantments"] = f.Enchantments;
                if (f.IgnoreMetadata) b["ignore_metadata"] = true;
                if (!f.IgnoreDurability) b["ignore_durability"] = false;
                break;
            case "wearing":
                b["material"] = f.Material ?? "";
                if (f.Damage is not null) b["damage"] = f.Damage;
                if (f.IgnoreMetadata) b["ignore_metadata"] = true;
                break;
            case "holding":
                b["material"] = f.Material ?? "";
                if (f.Damage is not null) b["damage"] = f.Damage;
                break;
            case "time":
                b["duration"] = f.Duration ?? ""; break;
            case "after":
                if (!string.IsNullOrEmpty(f.FilterRefId)) b["filter"] = f.FilterRefId;
                b["duration"] = f.Duration ?? ""; break;
            case "pulse":
                b["period"] = f.Period ?? ""; b["duration"] = f.Duration ?? "";
                if (!string.IsNullOrEmpty(f.FilterRefId)) b["filter"] = f.FilterRefId; break;
            case "offset":
                b["vector"] = f.Vector ?? ""; b["child"] = f.Child ?? ""; break;
            case "variable":
                b["var"] = f.Var ?? ""; b["value"] = f.Value ?? "";
                if (!string.IsNullOrEmpty(f.Team)) b["team"] = f.Team; break;
            case "completed" or "objective":
                b["objective"] = f.Objective ?? ""; break;
            case "kill-streak":
                if (f.Min is not null) b["min"] = f.Min;
                if (f.Max is not null) b["max"] = f.Max;
                if (f.Count is not null) b["count"] = f.Count; break;
            case "class":
                b["name"] = f.Name ?? ""; break;
            case "region":
                b["region"] = f.RegionRef ?? ""; break;
            case "players":
                if (f.Min is not null) b["min"] = f.Min;
                if (f.Max is not null) b["max"] = f.Max; break;
            case "spawn":
                if (!string.IsNullOrEmpty(f.Mob)) b["mob"] = f.Mob; break;
        }
        return b;
    }
}
