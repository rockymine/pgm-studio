using System.Text.Json;
using LinqToDB;
using LinqToDB.Data;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Repositories;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Writes a <see cref="MapXml"/> into the relational entity rows. Used both by the importer
/// (fresh insert) and by editor writes (replace a map's entities, keeping feature rows/artifacts).
/// Regions/filters are split into the bounds/coords/params JSON columns via the public serializer
/// encoders, mirroring the read-side <see cref="MapReader"/>.
/// </summary>
public sealed class MapWriter(PgmDb db)
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    /// <summary>
    /// Replace an existing map's entity rows from the document dict (features/artifacts kept).
    /// Non-wool entities go through the flat <see cref="MapXml"/>; wools are written from the grouped
    /// doc so a monument-less wool / wool-level fields survive.
    /// </summary>
    public async Task SaveDocAsync(long mapId, Dict doc, CancellationToken ct = default)
    {
        var m = Deserializer.FromDict(doc);
        await using var tx = await db.BeginTransactionAsync(ct);
        await DeleteEntitiesAsync(mapId, ct);
        await db.Maps.Where(x => x.Id == mapId).Set(x => x.Name, m.Name)
            .Set(x => x.Version, NullIfEmpty(m.Version)).Set(x => x.Gamemode, NullIfEmpty(m.Gamemode))
            .Set(x => x.Objective, NullIfEmpty(m.Objective)).Set(x => x.MaxBuildHeight, (double?)m.MaxBuildHeight)
            .Set(x => x.UpdatedAt, DateTime.UtcNow).UpdateAsync(ct);
        await WriteEntitiesAsync(mapId, m, ct);
        await WriteWoolsFromDocAsync(mapId, doc, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Insert wool + monument rows from the grouped <c>doc["wools"]</c> (handles monument-less wools).</summary>
    public async Task WriteWoolsFromDocAsync(long mapId, Dict doc, CancellationToken ct = default)
    {
        foreach (var groupObj in doc.GetValueOrDefault("wools") as List<object?> ?? [])
        {
            if (groupObj is not Dict group) continue;
            var woolId = await db.InsertWithInt64IdentityAsync(new WoolRow
            {
                MapId = mapId,
                WoolKey = group.GetValueOrDefault("id") as string ?? Slug(group.GetValueOrDefault("color") as string ?? ""),
                Color = group.GetValueOrDefault("color") as string ?? "",
                LocationJson = group.GetValueOrDefault("location") is { } loc ? Json(loc) : null,
                WoolRoomRegionKey = group.GetValueOrDefault("wool_room_region") as string,
                Team = group.GetValueOrDefault("team") as string,
            });
            foreach (var monObj in group.GetValueOrDefault("monuments") as List<object?> ?? [])
            {
                if (monObj is not Dict mon) continue;
                await db.InsertAsync(new MonumentRow
                {
                    WoolId = woolId,
                    MonumentKey = mon.GetValueOrDefault("id") as string ?? "",
                    Team = mon.GetValueOrDefault("team") as string ?? "",
                    LocationJson = mon.GetValueOrDefault("location") is { } ml ? Json(ml) : null,
                    MonumentRegionKey = mon.GetValueOrDefault("monument_region") as string,
                }, token: ct);
            }
        }
    }

    /// <summary>Delete a map's entity rows (kits/wools cascade to their children); leaves features/artifacts.</summary>
    public async Task DeleteEntitiesAsync(long mapId, CancellationToken ct = default)
    {
        await db.Authors.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.Teams.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.Kits.Where(x => x.MapId == mapId).DeleteAsync(ct);            // cascades kit_item/kit_armor
        await db.Regions.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.Filters.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.Wools.Where(x => x.MapId == mapId).DeleteAsync(ct);           // cascades monument
        await db.Spawns.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.MapSpawners.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.Renewables.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.BlockDropRules.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.ApplyRules.Where(x => x.MapId == mapId).DeleteAsync(ct);
    }

    /// <summary>Insert all entity rows for a map (no delete — caller ensures a clean slate).</summary>
    public async Task WriteEntitiesAsync(long mapId, MapXml m, CancellationToken ct = default)
    {
        foreach (var a in m.Authors)
            await db.InsertAsync(new AuthorRow { MapId = mapId, Uuid = a.Uuid, Role = a.Role, Contribution = NullIfEmpty(a.Contribution), Name = NullIfEmpty(a.Name) }, token: ct);

        foreach (var t in m.Teams)
            await db.InsertAsync(new TeamRow { MapId = mapId, TeamKey = t.Id, Name = t.Name, Color = t.Color, DyeColor = t.DyeColor, MaxPlayers = t.MaxPlayers, MinPlayers = t.MinPlayers }, token: ct);

        foreach (var kit in m.Kits)
        {
            var kitId = await db.InsertWithInt64IdentityAsync(new KitRow { MapId = mapId, KitKey = kit.Id });
            foreach (var it in kit.Items)
                await db.InsertAsync(new KitItemRow { KitId = kitId, Slot = it.Slot, Material = it.Material, Amount = it.Amount, Damage = it.ItemDamage, Unbreakable = it.Unbreakable, TeamColor = it.TeamColor, Enchantments = NullIfEmpty(it.Enchantments) }, token: ct);
            foreach (var ar in kit.Armor)
                await db.InsertAsync(new KitArmorRow { KitId = kitId, SlotName = ar.SlotName, Material = ar.Material, Unbreakable = ar.Unbreakable, TeamColor = ar.TeamColor, Enchantments = NullIfEmpty(ar.Enchantments) }, token: ct);
        }

        foreach (var (rid, region) in m.Regions)
        {
            var rd = Serializer.RegionToDict(region);
            await db.InsertAsync(new RegionRow
            {
                MapId = mapId, RegionKey = rid, Type = region.Type,
                BoundsJson = JsonOrNull(rd, "bounds_2d"),
                ChildRefIdsJson = JsonOrNull(rd, "children"),
                SourceId = rd.GetValueOrDefault("source_id") as string,
                CoordsJson = SubsetJson(rd, "id", "type", "bounds_2d", "children", "source_id"),
            }, token: ct);
        }

        foreach (var (fid, filter) in m.Filters)
        {
            var fd = Serializer.FilterToDict(filter);
            await db.InsertAsync(new FilterRow
            {
                MapId = mapId, FilterKey = fid, Type = filter.Type,
                ChildRefIdsJson = JsonOrNull(fd, "children"),
                ChildKey = fd.GetValueOrDefault("child") as string,
                RegionKey = fd.GetValueOrDefault("region") as string,
                ParamsJson = SubsetJson(fd, "id", "type", "children", "child", "region"),
            }, token: ct);
        }

        // wools are written separately from the grouped doc (WriteWoolsFromDocAsync) — the flat
        // MapXml can't represent a monument-less wool or wool-level fields.

        foreach (var s in m.Spawns)
            await db.InsertAsync(new SpawnRow { MapId = mapId, IsObserver = false, Team = s.Team, Kit = NullIfEmpty(s.Kit), Yaw = s.Yaw, RegionKey = s.Region?.Id }, token: ct);
        if (m.ObserverSpawn is { } obs)
            await db.InsertAsync(new SpawnRow { MapId = mapId, IsObserver = true, Team = obs.Team, Kit = NullIfEmpty(obs.Kit), Yaw = obs.Yaw, RegionKey = obs.Region?.Id }, token: ct);

        foreach (var sp in m.Spawners)
            await db.InsertAsync(new MapSpawnerRow
            {
                MapId = mapId, SpawnRegionKey = NullIfEmpty(sp.SpawnRegion), PlayerRegionKey = NullIfEmpty(sp.PlayerRegion),
                Delay = NullIfEmpty(sp.Delay), MaxEntities = sp.MaxEntities,
                ItemsJson = sp.Items.Count == 0 ? null : Json(sp.Items.Select(i => (object?)new Dict { ["material"] = i.Material, ["damage"] = i.Damage, ["amount"] = i.Amount }).ToList()),
            }, token: ct);

        foreach (var r in m.Renewables)
            await db.InsertAsync(new RenewableRow { MapId = mapId, RegionKey = NullIfEmpty(r.RegionId), Rate = r.Rate, RenewFilter = NullIfEmpty(r.RenewFilter), ReplaceFilter = NullIfEmpty(r.ReplaceFilter), Grow = r.Grow }, token: ct);

        foreach (var r in m.BlockDropRules)
            await db.InsertAsync(new BlockDropRuleRow
            {
                MapId = mapId, RegionKey = NullIfEmpty(r.RegionId), FilterKey = NullIfEmpty(r.FilterId),
                Replacement = NullIfEmpty(r.Replacement), WrongTool = r.WrongTool,
                ItemsJson = r.Items.Count == 0 ? null : Json(r.Items.Select(i => (object?)new Dict { ["material"] = i.Material, ["damage"] = i.Damage, ["amount"] = i.Amount, ["chance"] = i.Chance }).ToList()),
            }, token: ct);

        foreach (var rule in m.ApplyRules)
        {
            var ad = Serializer.ApplyRuleToDict(rule);
            await db.InsertAsync(new ApplyRuleRow { MapId = mapId, RuleKey = null, RegionKey = NullIfEmpty(rule.RegionId), EventsJson = SubsetJson(ad, "region") }, token: ct);
        }
    }

    // ── JSON / value helpers ──────────────────────────────────────────────────────
    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    private static string Slug(string v) => v.Trim().ToLowerInvariant().Replace(" ", "_");
    private static Dict Xyz(Vec3 v) => new() { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
    private static string Json(object? v) => JsonSerializer.Serialize(v, JsonOpts);
    private static string? JsonOrNull(Dict d, string key) => d.ContainsKey(key) ? JsonSerializer.Serialize(d[key], JsonOpts) : null;

    private static string? SubsetJson(Dict d, params string[] exclude)
    {
        var copy = new Dict(d);
        foreach (var k in exclude) copy.Remove(k);
        return copy.Count == 0 ? null : JsonSerializer.Serialize(copy, JsonOpts);
    }
}
