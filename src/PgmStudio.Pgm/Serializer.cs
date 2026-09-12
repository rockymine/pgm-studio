using PgmStudio.Domain;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>MapXml → JSON object tree, in the xml_data.json shape.</summary>
public static class Serializer
{
    public static Dict ToDict(MapXml m)
    {
        var d = Base(m);
        // Objective modules the reference contract has no key for. Emitted only when the map carries one,
        // so a map without them serialises to exactly the shape it always did.
        if (m.Destroyables.Count > 0) d["destroyables"] = m.Destroyables.Select(EncodeDestroyable).ToList<object?>();
        if (m.Cores.Count > 0) d["cores"] = m.Cores.Select(EncodeCore).ToList<object?>();
        if (m.ControlPoints.Count > 0) d["control_points"] = m.ControlPoints.Select(EncodeControlPoint).ToList<object?>();
        if (m.Shops.Count > 0) d["shops"] = m.Shops.Select(EncodeShop).ToList<object?>();
        if (m.Shopkeepers.Count > 0) d["shopkeepers"] = m.Shopkeepers.Select(EncodeShopkeeper).ToList<object?>();
        if (m.Score is { } score) d["score"] = EncodeScore(score);
        if (m.Modes.Count > 0) d["modes"] = m.Modes.Select(EncodeMode).ToList<object?>();
        // The derived truth beside the declared label — `gamemode` is what the author wrote (often
        // nothing), `gamemodes` is what the map's modules make it.
        if (m.Gamemodes.Count > 0) d["gamemodes"] = m.Gamemodes.ToList<object?>();
        return d;
    }

    private static Dict Base(MapXml m) => new()
    {
        ["name"] = m.Name,
        ["version"] = m.Version,
        ["gamemode"] = m.DeclaredGamemode.ToList<object?>(),
        ["objective"] = m.Objective,
        ["created"] = m.Created,
        ["phase"] = m.Phase,
        ["max_build_height"] = m.MaxBuildHeight,
        ["authors"] = m.Authors.Select(EncodeAuthor).ToList<object?>(),
        ["kits"] = m.Kits.Select(EncodeKit).ToList<object?>(),
        ["teams"] = m.Teams.Select(EncodeTeam).ToList<object?>(),
        ["spawns"] = m.Spawns.Select(EncodeSpawn).ToList<object?>(),
        ["observer_spawn"] = m.ObserverSpawn is null ? null : EncodeSpawn(m.ObserverSpawn),
        ["wools"] = EncodeWoolsGrouped(m.Wools, m.Teams),
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
        ["clear"] = k.Clear,
        ["items"] = k.Items.Select(EncodeKitItem).ToList<object?>(),
        ["armor"] = k.Armor.Select(EncodeKitArmor).ToList<object?>(),
        ["effects"] = k.Effects.Select(EncodeEffect).ToList<object?>(),
    };

    private static Dict EncodeEffect(PotionEffect e) => new()
    {
        ["type"] = e.Type, ["duration"] = e.Duration, ["amplifier"] = e.Amplifier,
    };

    /// <summary>
    /// The item stack, flat on the dict it is written into rather than nested under an <c>item</c> key: a
    /// kit item is a slot and a stack, and both read off one object. Only what the map stated is written, so
    /// an item that says nothing but its material encodes as exactly that.
    /// </summary>
    public static Dict ItemToDict(ItemSpec item) { var d = new Dict(); EncodeItemSpec(d, item); return d; }

    private static void EncodeItemSpec(Dict r, ItemSpec i)
    {
        r["material"] = i.Material;
        if (i.Amount != 1) r["amount"] = i.Amount;
        if (i.Damage != 0) r["damage"] = i.Damage;
        if (i.Name.Length > 0) r["name"] = i.Name;
        if (i.Lore.Length > 0) r["lore"] = i.Lore;
        if (i.Color.Length > 0) r["color"] = i.Color;
        if (i.Enchantments.Length > 0) r["enchantments"] = i.Enchantments;
        if (i.StoredEnchantments.Length > 0) r["stored_enchantments"] = i.StoredEnchantments;
        if (i.Unbreakable) r["unbreakable"] = true;
        if (i.TeamColor) r["team_color"] = true;
        if (i.PreventSharing) r["prevent_sharing"] = true;
        if (i.Locked) r["locked"] = true;
        if (i.Projectile.Length > 0) r["projectile"] = i.Projectile;
        if (i.Consumable.Length > 0) r["consumable"] = i.Consumable;
        if (i.Hidden.Count > 0) r["hidden"] = i.Hidden.Select(w => (object?)w).ToList();
        if (i.Effects.Count > 0) r["effects"] = i.Effects.Select(EncodeEffect).ToList<object?>();
        if (i.Attributes.Count > 0) r["attributes"] = i.Attributes
            .Select(a => (object?)new Dict { ["attribute"] = a.Attribute, ["operation"] = a.Operation, ["amount"] = a.Amount }).ToList();
        if (i.CanPlaceOn.Count > 0) r["can_place_on"] = i.CanPlaceOn.Select(w => (object?)w).ToList();
        if (i.CanDestroy.Count > 0) r["can_destroy"] = i.CanDestroy.Select(w => (object?)w).ToList();
    }

    private static Dict EncodeKitItem(KitItem i)
    {
        var r = new Dict { ["slot"] = i.Slot };
        EncodeItemSpec(r, i.Item);
        return r;
    }

    private static Dict EncodeKitArmor(KitArmor a)
    {
        var r = new Dict { ["slot_name"] = a.SlotName };
        EncodeItemSpec(r, a.Item);
        return r;
    }

    /// <summary>A shop and the tree under it. The categories nest rather than being keyed apart, because a
    /// category has no life outside its shop and an icon none outside its category.</summary>
    public static Dict EncodeShop(Shop shop)
    {
        var r = new Dict { ["id"] = shop.Id };
        if (shop.Name.Length > 0) r["name"] = shop.Name;
        r["categories"] = shop.Categories.Select(category =>
        {
            var c = new Dict { ["id"] = category.Id, ["icon"] = ItemToDict(category.Icon) };
            if (category.FilterId.Length > 0) c["filter"] = category.FilterId;
            c["icons"] = category.Icons.Select(icon =>
            {
                var i = new Dict { ["item"] = ItemToDict(icon.Item) };
                if (icon.Payments.Count > 0) i["payments"] = icon.Payments.Select(pay =>
                {
                    var p = new Dict { ["price"] = pay.Price };
                    if (pay.Currency.Length > 0) p["currency"] = pay.Currency;
                    if (pay.Color.Length > 0) p["color"] = pay.Color;
                    return (object?)p;
                }).ToList();
                if (icon.FilterId.Length > 0) i["filter"] = icon.FilterId;
                if (icon.ActionId.Length > 0) i["action"] = icon.ActionId;
                return (object?)i;
            }).ToList();
            return (object?)c;
        }).ToList();
        return r;

    }

    public static Dict EncodeShopkeeper(Shopkeeper k)
    {
        var r = new Dict { ["shop"] = k.ShopId };
        if (k.Name.Length > 0) r["name"] = k.Name;
        if (k.Mob.Length > 0) r["mob"] = k.Mob;
        if (k.Location is { } at) r["location"] = new Dict { ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z };
        if (k.RegionId.Length > 0) r["region"] = k.RegionId;
        if (k.Yaw is { } yaw) r["yaw"] = yaw;
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

    /// <summary>The wools as the document's grouped shape: one entry per colour, its capturing teams under
    /// <c>monuments</c> and the team that defends it under <c>team</c>. The defender is
    /// <see cref="WoolEditor.DefendingTeam"/>'s answer rather than anything the XML states — a
    /// <c>&lt;wool team&gt;</c> is a capturing team, one element per monument, so the room's own team survives
    /// the read only by being inferred here. Every reader of a wool's owner is a reader of this key.</summary>
    private static List<object?> EncodeWoolsGrouped(List<Wool> wools, List<Team> teams)
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
                    ["team"] = null,
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

        var teamIds = teams.Select(t => t.Id).Where(id => id.Length > 0).ToList();
        foreach (var group in byColor.Values)
            group["team"] = WoolEditor.DefendingTeam(
                teamIds, ((List<object?>)group["monuments"]!).OfType<Dict>().Select(m => m["team"] as string));

        return order.Select(c => (object?)byColor[c]).ToList();
    }

    private static Dict EncodeDestroyable(Destroyable d)
    {
        var r = new Dict
        {
            ["id"] = d.Id,
            ["name"] = d.Name,
            ["owner"] = d.Owner,
            ["region"] = d.RegionId,
            ["materials"] = d.Materials,
        };
        if (d.Completion is not null) r["completion"] = d.Completion;
        // A phantom is a scripted block-swap region, not a goal. Both keys go out so a reader never has to
        // rediscover that `show="false"` is the discriminator.
        if (!d.Show)
        {
            r["show"] = false;
            r["phantom"] = d.Phantom.ToString().ToLowerInvariant();
        }
        if (!d.Repairable) r["repairable"] = false;
        if (d.ModeChanges) r["mode_changes"] = true;
        if (d.Modes is { Count: > 0 }) r["modes"] = d.Modes.ToList<object?>();
        return r;
    }

    private static Dict EncodeCore(Core c)
    {
        // `owner` is the field name everywhere but the XML, where PGM spells it `team` (OB1).
        var r = new Dict { ["id"] = c.Id, ["owner"] = c.Owner, ["region"] = c.RegionId };
        if (c.Name.Length > 0) r["name"] = c.Name;
        if (c.Material.Length > 0) r["material"] = c.Material;
        if (c.Leak is not null) r["leak"] = c.Leak;
        if (c.ModeChanges) r["mode_changes"] = true;
        if (c.Modes is { Count: > 0 }) r["modes"] = c.Modes.ToList<object?>();
        return r;
    }

    // Every knob is written only when the map stated one: the absent ones are the element's PGM default,
    // which differs between a hill and a point, so a materialised value would be a different map.
    private static Dict EncodeControlPoint(ControlPoint p)
    {
        var r = new Dict
        {
            ["id"] = p.Id,
            ["element"] = p.Element == ControlPointElement.King ? "king" : "control-points",
            ["capture_region"] = p.CaptureRegionId,
        };
        if (p.Name.Length > 0) r["name"] = p.Name;
        if (p.ProgressRegionId.Length > 0) r["progress_region"] = p.ProgressRegionId;
        if (p.OwnerRegionId.Length > 0) r["owner_region"] = p.OwnerRegionId;
        if (p.VisualMaterialsFilterId.Length > 0) r["visual_materials"] = p.VisualMaterialsFilterId;
        if (p.InitialOwner.Length > 0) r["initial_owner"] = p.InitialOwner;
        if (p.CaptureTime.Length > 0) r["capture_time"] = p.CaptureTime;
        if (p.CaptureRule.Length > 0) r["capture_rule"] = p.CaptureRule;
        if (p.CaptureFilterId.Length > 0) r["capture_filter"] = p.CaptureFilterId;
        if (p.PlayerFilterId.Length > 0) r["player_filter"] = p.PlayerFilterId;
        if (p.Incremental is { } incremental) r["incremental"] = incremental;
        if (p.Recovery is { } recovery) r["recovery"] = recovery;
        if (p.Decay is { } decay) r["decay"] = decay;
        if (p.OwnedDecay is { } ownedDecay) r["owned_decay"] = ownedDecay;
        if (p.Contested is { } contested) r["contested"] = contested;
        if (p.TimeMultiplier is { } timeMultiplier) r["time_multiplier"] = timeMultiplier;
        if (p.NeutralState is { } neutral) r["neutral_state"] = neutral;
        if (p.Permanent) r["permanent"] = true;
        if (p.Points is { } points) r["points"] = points;
        if (p.OwnerPoints is { } ownerPoints) r["owner_points"] = ownerPoints;
        if (p.PointsGrowth is { } growth) r["points_growth"] = growth;
        if (p.ShowProgress is { } showProgress) r["show_progress"] = showProgress;
        if (p.Required is { } required) r["required"] = required;
        if (!p.Show) r["show"] = false;
        return r;
    }

    private static Dict EncodeScore(ScoreConfig s)
    {
        var r = new Dict();
        if (s.Initial is { } initial) r["initial"] = initial;
        if (s.Limit is { } limit) r["limit"] = limit;
        if (s.EnforceLimit is { } enforce) r["enforce_limit"] = enforce;
        if (s.Kills is { } kills) r["kills"] = kills;
        if (s.Deaths is { } deaths) r["deaths"] = deaths;
        if (s.Mercy is { } mercy) r["mercy"] = mercy;
        if (s.MercyMin is { } mercyMin) r["mercy_min"] = mercyMin;
        if (s.Display.Length > 0) r["display"] = s.Display;
        if (s.ScoreboardFilterId.Length > 0) r["scoreboard_filter"] = s.ScoreboardFilterId;
        if (s.King) r["king"] = true;
        return r;
    }

    private static Dict EncodeMode(ObjectiveMode m)
    {
        var r = new Dict { ["id"] = m.Id, ["after"] = m.After };
        if (m.Name.Length > 0) r["name"] = m.Name;
        if (m.Material.Length > 0) r["material"] = m.Material;
        if (m.ShowBefore.Length > 0) r["show_before"] = m.ShowBefore;
        if (m.FilterId.Length > 0) r["filter"] = m.FilterId;
        if (m.ActionId.Length > 0) r["action"] = m.ActionId;
        return r;
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
