using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Map;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Reconstructs a <see cref="MapXml"/> from the relational hybrid rows — the inverse of
/// <c>PgmStudio.Import.MapImporter</c>. Polymorphic region/filter shapes are rebuilt through the
/// validated <see cref="Deserializer"/> decoders; everything else is mapped directly.
/// </summary>
public sealed class MapReader(PgmDb db)
{
    public async Task<MapXml?> ReadAsync(string slug, CancellationToken ct = default)
    {
        var map = await db.Maps.FirstOrDefaultAsync(m => m.Slug == slug, ct);
        return map is null ? null : await ReadAsync(map, ct);
    }

    /// <summary>
    /// Build the full map document (xml_data.json shape). Wools are built directly from the grouped
    /// wool+monument rows (not via the flat <see cref="MapXml"/>, which can't represent a wool with
    /// no monuments or wool-level fields) so editor states round-trip faithfully.
    /// </summary>
    public async Task<Dict?> ReadDocAsync(string slug, CancellationToken ct = default)
    {
        var map = await db.Maps.FirstOrDefaultAsync(m => m.Slug == slug, ct);
        return map is null ? null : await ReadDocAsync(map, ct);
    }

    public async Task<Dict> ReadDocAsync(MapRow map, CancellationToken ct = default)
    {
        var doc = Serializer.ToDict(await ReadAsync(map, ct));
        doc["wools"] = await GroupedWoolsAsync(map.Id, ct);
        return doc;
    }

    private async Task<List<object?>> GroupedWoolsAsync(long mapId, CancellationToken ct)
    {
        var outp = new List<object?>();
        foreach (var w in await db.Wools.Where(x => x.MapId == mapId).OrderBy(x => x.Id).ToListAsync(ct))
        {
            var group = new Dict
            {
                ["id"] = w.WoolKey, ["color"] = w.Color,
                ["location"] = w.LocationJson is { } lj ? JsonTree.FromJson(lj) : null,
                ["wool_room_region"] = w.WoolRoomRegionKey,
                ["monuments"] = new List<object?>(),
            };
            if (w.Team is { } t) group["team"] = t;
            foreach (var mon in await db.Monuments.Where(x => x.WoolId == w.Id).OrderBy(x => x.Id).ToListAsync(ct))
                ((List<object?>)group["monuments"]!).Add(new Dict
                {
                    ["id"] = mon.MonumentKey, ["team"] = mon.Team,
                    ["location"] = mon.LocationJson is { } ml ? JsonTree.FromJson(ml) : null,
                    ["monument_region"] = mon.MonumentRegionKey,
                });
            outp.Add(group);
        }
        return outp;
    }

    public async Task<MapXml> ReadAsync(MapRow map, CancellationToken ct = default)
    {
        var id = map.Id;
        var m = new MapXml
        {
            Name = map.Name,
            Version = map.Version ?? "",
            Gamemode = map.Gamemode ?? "ctw",
            Objective = map.Objective ?? "",
            MaxBuildHeight = map.MaxBuildHeight is { } h ? (int)h : null,
        };

        m.Authors = (await db.Authors.Where(a => a.MapId == id).OrderBy(a => a.Id).ToListAsync(ct))
            .Select(a => new Author { Uuid = a.Uuid, Role = a.Role, Contribution = a.Contribution ?? "", Name = a.Name ?? "" }).ToList();

        m.Teams = (await db.Teams.Where(t => t.MapId == id).OrderBy(t => t.Id).ToListAsync(ct))
            .Select(t => new Team { Id = t.TeamKey, Color = t.Color, MaxPlayers = t.MaxPlayers, MinPlayers = t.MinPlayers, Name = t.Name, DyeColor = t.DyeColor }).ToList();

        foreach (var kit in await db.Kits.Where(k => k.MapId == id).OrderBy(k => k.Id).ToListAsync(ct))
        {
            var items = await db.KitItems.Where(i => i.KitId == kit.Id).OrderBy(i => i.Id).ToListAsync(ct);
            var armor = await db.KitArmor.Where(a => a.KitId == kit.Id).OrderBy(a => a.Id).ToListAsync(ct);
            m.Kits.Add(new Kit
            {
                Id = kit.KitKey,
                Items = items.Select(i => new KitItem { Slot = i.Slot ?? 0, Material = i.Material, Amount = i.Amount ?? 1, ItemDamage = i.Damage ?? 0, Unbreakable = i.Unbreakable ?? false, TeamColor = i.TeamColor ?? false, Enchantments = i.Enchantments ?? "" }).ToList(),
                Armor = armor.Select(a => new KitArmor { SlotName = a.SlotName, Material = a.Material, Unbreakable = a.Unbreakable ?? false, TeamColor = a.TeamColor ?? false, Enchantments = a.Enchantments ?? "" }).ToList(),
            });
        }

        foreach (var r in await db.Regions.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
            m.Regions[r.RegionKey] = Deserializer.RegionFromDict(RegionDict(r));
        // Storage persists only primitive bounds; recompute the derived compound/transform bounds_2d.
        RegionBoundsDeriver.Derive(m.Regions);

        foreach (var f in await db.Filters.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
            m.Filters[f.FilterKey] = Deserializer.FilterFromDict(FilterDict(f));

        foreach (var w in await db.Wools.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
        {
            var loc = Xyz(w.LocationJson);
            foreach (var mon in await db.Monuments.Where(x => x.WoolId == w.Id).OrderBy(x => x.Id).ToListAsync(ct))
                m.Wools.Add(new Wool
                {
                    Team = mon.Team, Color = w.Color, Location = loc, Monument = Xyz(mon.LocationJson),
                    MonumentRegionId = mon.MonumentRegionKey, WoolRoomRegion = w.WoolRoomRegionKey,
                });
        }

        foreach (var s in await db.Spawns.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
        {
            var spawn = new Spawn { Team = s.Team, Kit = s.Kit ?? "", Yaw = s.Yaw, Region = ResolveRegion(m, s.RegionKey) };
            if (s.IsObserver) m.ObserverSpawn = spawn; else m.Spawns.Add(spawn);
        }

        foreach (var sp in await db.MapSpawners.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
            m.Spawners.Add(new WoolSpawner
            {
                SpawnRegion = sp.SpawnRegionKey ?? "", PlayerRegion = sp.PlayerRegionKey ?? "",
                Delay = sp.Delay ?? "", MaxEntities = sp.MaxEntities,
                Items = ListOfDicts(sp.ItemsJson).Select(i => new SpawnerItem { Material = Str(i, "material"), Damage = Int(i, "damage"), Amount = Int(i, "amount", 1) }).ToList(),
            });

        foreach (var r in await db.Renewables.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
            m.Renewables.Add(new Renewable { RegionId = r.RegionKey ?? "", Rate = r.Rate ?? 1.0, RenewFilter = r.RenewFilter ?? "", ReplaceFilter = r.ReplaceFilter ?? "", Grow = r.Grow ?? false });

        foreach (var r in await db.BlockDropRules.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
            m.BlockDropRules.Add(new BlockDropRule
            {
                RegionId = r.RegionKey ?? "", FilterId = r.FilterKey ?? "", Replacement = r.Replacement ?? "", WrongTool = r.WrongTool ?? false,
                Items = ListOfDicts(r.ItemsJson).Select(i => new BlockDropItem { Material = Str(i, "material"), Damage = Int(i, "damage"), Amount = Int(i, "amount", 1), Chance = Dbl(i, "chance", 1.0) }).ToList(),
            });

        foreach (var ar in await db.ApplyRules.Where(x => x.MapId == id).OrderBy(x => x.Id).ToListAsync(ct))
        {
            var ev = ar.EventsJson is null ? new Dict() : (Dict)JsonTree.FromJson(ar.EventsJson)!;
            m.ApplyRules.Add(new ApplyRule
            {
                EnterFilter = Str(ev, "enter"), LeaveFilter = Str(ev, "leave"), BlockFilter = Str(ev, "block"),
                BlockPlaceFilter = Str(ev, "block_place"), BlockBreakFilter = Str(ev, "block_break"),
                BlockPhysicsFilter = Str(ev, "block_physics"), BlockPlaceAgainstFilter = Str(ev, "block_place_against"),
                UseFilter = Str(ev, "use"), FilterId = Str(ev, "filter"), RegionId = ar.RegionKey ?? "",
                Kit = Str(ev, "kit"), LendKit = Str(ev, "lend_kit"), Velocity = Str(ev, "velocity"), Message = Str(ev, "message"),
            });
        }

        return m;
    }

    // ── region/filter dict reconstruction ───────────────────────────────────────────
    private static Dict RegionDict(RegionRow r)
    {
        var d = new Dict { ["id"] = r.RegionKey, ["type"] = r.Type };
        if (r.BoundsJson is { } b) d["bounds_2d"] = JsonTree.FromJson(b);
        if (r.ChildRefIdsJson is { } c) d["children"] = JsonTree.FromJson(c);
        if (r.SourceId is { } s) d["source_id"] = s;
        Merge(d, r.CoordsJson);
        return d;
    }

    private static Dict FilterDict(FilterRow f)
    {
        var d = new Dict { ["id"] = f.FilterKey, ["type"] = f.Type };
        if (f.ChildRefIdsJson is { } c) d["children"] = JsonTree.FromJson(c);
        if (f.ChildKey is { } ck) d["child"] = ck;
        if (f.RegionKey is { } rk) d["region"] = rk;
        Merge(d, f.ParamsJson);
        return d;
    }

    private static void Merge(Dict d, string? json)
    {
        if (json is null) return;
        if (JsonTree.FromJson(json) is Dict extra)
            foreach (var (k, v) in extra) d[k] = v;
    }

    private static Region? ResolveRegion(MapXml m, string? key)
        => key is null ? null : m.Regions.GetValueOrDefault(key) ?? new Region { Id = key, Type = "reference" };

    // ── small JSON helpers ──────────────────────────────────────────────────────────
    private static Vec3 Xyz(string? json)
    {
        if (json is null || JsonTree.FromJson(json) is not Dict d) return new Vec3(0, 0, 0);
        return new Vec3(Dbl(d, "x"), Dbl(d, "y"), Dbl(d, "z"));
    }

    private static List<Dict> ListOfDicts(string? json)
        => json is not null && JsonTree.FromJson(json) is List<object?> list ? list.OfType<Dict>().ToList() : [];

    private static string Str(Dict d, string k) => d.GetValueOrDefault(k) as string ?? "";
    private static int Int(Dict d, string k, int def = 0) => d.GetValueOrDefault(k) is { } v ? Convert.ToInt32(v) : def;
    private static double Dbl(Dict d, string k, double def = 0) => d.GetValueOrDefault(k) is { } v ? Convert.ToDouble(v) : def;
}
