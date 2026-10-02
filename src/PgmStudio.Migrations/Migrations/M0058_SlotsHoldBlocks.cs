using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A slot holds one block or one pattern, and the library holds each pattern, roof, storey and porch once.
///
/// <para><b>A single block leaves the pattern library.</b> Every theme bucket and every course of a house, a roof
/// and a storey gains <c>block_id</c>, <c>block_data</c> and <c>block_laid</c>, and a course's <c>style_id</c> may be
/// null. Each slot bound to a <c>solid</c> or a <c>laidLog</c> row takes that row's block into its own columns, and
/// the row leaves: a block is named by its id, so it needs no row and no name.</para>
///
/// <para><b>Two rows holding one pattern become one.</b> Patterns whose params are equal, and the seeded patterns
/// the seed folder now states as one (<see cref="Renamed"/> maps each name the seed used to file a house's or a
/// theme's material under to the pattern it now is), merge into the row with the lowest id; every slot binding
/// another takes that row. Roofs, storeys and porches holding the same columns and courses merge the same way,
/// and every house wearing one takes the row kept.</para>
///
/// <para><b>A seeded pattern takes the name it describes itself by.</b> The row kept is renamed where the seed
/// folder names it, unless a row already carries that name; the next start's seed lays the folder's content over
/// it. Roofs, storeys and porches are renamed by the seed itself, which names them from their content.</para>
///
/// <para><b>Rolled back, a block takes a row again</b> — the one holding it where there is one — so every slot
/// still binds a style; a merged row stays merged, since the rows it replaced held what it holds.</para>
///
/// <para><b>The refinement each map last stated follows.</b> A material named from the library by a block's row
/// is replaced by that block, the fields stated beside the name laid over it; one named by a merged or renamed
/// pattern names the row kept. Only the current refinement is rewritten, and one whose JSON will not parse is left
/// exactly as it was found.</para>
/// </summary>
[Migration(58, "A slot holds one block or one pattern, and the library holds each pattern and part once")]
public sealed partial class M0058_SlotsHoldBlocks : Migration
{
    /// <summary>The tables whose rows fill a slot.</summary>
    private static readonly string[] SlotTables = ["theme_bucket", "room_style_course", "roof_style_course", "storey_style_course"];

    /// <summary>Each name the seed filed a house's or a theme's pattern under, and the pattern the seed folder
    /// states it as.</summary>
    private static readonly Dictionary<string, string> Renamed = new(StringComparer.Ordinal)
    {
        ["acacia-log-checkered-house · wall 1"] = "cobblestone-andesite-noise",
        ["acacia-log-checkered-house · wall 2"] = "acacia-log-checker",
        ["ashfall · fill"] = "cracked-stone-bricks-andesite-voronoi",
        ["ashfall · surface"] = "gravel-coarse-dirt-andesite-layers",
        ["ashfall · wall"] = "team-stained-clay-gray-neutral",
        ["black-wool-roofed-andesite-cottage · gable"] = "stone-andesite-noise",
        ["black-wool-roofed-andesite-cottage · wall 1"] = "cobblestone-polished-andesite-noise",
        ["brick-roofed-spruce-house · storey 1 wall 1"] = "cobblestone-andesite-cells",
        ["brick-roofed-stone-and-dark-oak-house · storey 1 wall 1"] = "cobblestone-andesite-noise",
        ["brick-townhouse · post"] = "andesite-polished-andesite-checker",
        ["brick-townhouse · storey 1 post"] = "andesite-polished-andesite-checker",
        ["brick-townhouse · storey 2 post"] = "andesite-polished-andesite-checker",
        ["brick-townhouse · storey 3 post"] = "andesite-polished-andesite-checker",
        ["clay grassland · fill"] = "green-wool-green-stained-clay-grass-block-mix-layers",
        ["clay mycelium · fill"] = "light-blue-stained-clay-light-gray-stained-clay-mycelium-mix-layers",
        ["claybed · fill"] = "granite-hardened-clay-voronoi",
        ["claybed · surface"] = "hardened-clay-brown-stained-clay-clay-layers",
        ["claybed · wall"] = "team-stained-clay-brown-neutral",
        ["cyan-roofed-white-clay-cottage · floor"] = "spruce-planks-dark-oak-planks-cells",
        ["cyan-roofed-white-clay-cottage · wall 1"] = "cobblestone-andesite-ringed-noise",
        ["cyan-roofed-white-clay-cottage · wall 3"] = "team-stained-clay-white-neutral",
        ["cyan-roofed-white-clay-house · floor"] = "spruce-planks-dark-oak-planks-cells",
        ["cyan-roofed-white-clay-house · storey 1 wall 1"] = "cobblestone-andesite-ringed-noise",
        ["cyan-roofed-white-clay-house · wall 1"] = "cobblestone-andesite-ringed-noise",
        ["dark-oak-roofed-quartz-house · field"] = "stone-bricks-andesite-slab-grid-cells",
        ["dark-oak-roofed-quartz-house · storey 1 field"] = "stone-bricks-andesite-slab-grid-cells",
        ["dark-oak-roofed-rubble-cottage · storey 1 wall 1"] = "cobblestone-andesite-broad-noise",
        ["dark-oak-roofed-rubble-cottage · wall"] = "cobblestone-andesite-broad-noise",
        ["dark-oak-roofed-spruce-house · field"] = "stone-bricks-andesite-stone-cells",
        ["dark-oak-roofed-stone-brick-house · storey 1 wall 1"] = "cobblestone-andesite-cells",
        ["dark-oak-roofed-stone-cottage · wall 1"] = "cobblestone-andesite-noise",
        ["dark-oak-roofed-stone-cottage · wall 2"] = "stone-polished-andesite-checker",
        ["dark-oak-stilt-hut · storey 1 wall 1"] = "dark-oak-log-air-stripes",
        ["dark-oak-stilt-hut · wall 1"] = "dark-oak-log-air-stripes",
        ["dunes · fill"] = "smooth-sandstone-sandstone-voronoi",
        ["dunes · surface"] = "sand-red-sand-sandstone-layers",
        ["dunes · wall"] = "team-stained-clay-brown-neutral",
        ["firnline · fill"] = "diorite-stone-voronoi",
        ["firnline · surface"] = "snow-block-white-stained-clay-diorite-layers",
        ["firnline · wall"] = "team-stained-clay-white-neutral",
        ["hay-gambrel-barn · storey 1 wall 1"] = "cobblestone-andesite-columnar-noise",
        ["hay-gambrel-barn · wall 1"] = "cobblestone-andesite-columnar-noise",
        ["hay-roofed-stone-and-dark-oak-house · storey 1 wall 1"] = "cobblestone-andesite-noise",
        ["jungle-framed-cottage · wall 1"] = "cobblestone-andesite-noise",
        ["jungle-framed-cottage · wall 2"] = "jungle-planks-oak-planks-checker",
        ["jungle-saltbox-cottage · storey 1 wall 1"] = "cobblestone-andesite-columnar-noise",
        ["jungle-saltbox-cottage · wall 1"] = "cobblestone-andesite-columnar-noise",
        ["log-roofed-clay-hut · gable"] = "jungle-planks-hardened-clay-light-gray-stained-clay-noise",
        ["log-roofed-clay-hut · wall"] = "jungle-planks-hardened-clay-light-gray-stained-clay-noise",
        ["meadow · fill"] = "andesite-stone-voronoi",
        ["meadow · surface"] = "grass-block-podzol-dirt-layers",
        ["meadow · wall"] = "team-stained-clay-light-gray-neutral",
        ["oak-and-spruce-timbered-house · storey 1 wall 1"] = "cobblestone-andesite-noise",
        ["oak-framed-rubble-house · storey 1 wall 1"] = "cobblestone-andesite-broad-noise",
        ["oak-framed-rubble-house · wall"] = "cobblestone-andesite-broad-noise",
        ["oldstone · fill"] = "mossy-stone-bricks-stone-bricks-voronoi",
        ["oldstone · surface"] = "grass-block-andesite-dirt-layers",
        ["oldstone · wall"] = "team-stained-clay-light-gray-neutral",
        ["rubble-and-spruce-house · storey 1 wall 1"] = "cobblestone-andesite-broad-noise",
        ["rubble-and-spruce-house · wall"] = "cobblestone-andesite-broad-noise",
        ["spruce-mountain-chalet · storey 1 wall 1"] = "cobblestone-andesite-columnar-noise",
        ["spruce-mountain-chalet · wall 1"] = "cobblestone-andesite-columnar-noise",
        ["spruce-roofed-checkered-plank-cottage · gable"] = "oak-planks-jungle-planks-checker",
        ["spruce-roofed-checkered-plank-cottage · wall 4"] = "oak-planks-jungle-planks-checker",
        ["spruce-roofed-oak-cottage · wall 1"] = "cobblestone-mossy-cobblestone-stone-bricks-noise",
        ["spruce-roofed-oak-cottage · wall 3"] = "team-stained-clay-light-gray-neutral",
        ["spruce-roofed-oak-house · floor"] = "stone-bricks-andesite-stone-cells",
        ["spruce-roofed-oak-house · storey 1 wall 1"] = "stone-bricks-andesite-polished-andesite-cells",
        ["spruce-roofed-oak-house · wall 1"] = "stone-bricks-andesite-polished-andesite-cells",
        ["spruce-roofed-quartz-house · field"] = "stone-bricks-andesite-stone-cells",
        ["spruce-roofed-quartz-house · storey 1 field"] = "stone-bricks-andesite-stone-cells",
        ["spruce-roofed-quartz-house · storey 1 wall 1"] = "cobblestone-andesite-stone-noise",
        ["spruce-roofed-quartz-house · wall 1"] = "cobblestone-andesite-stone-noise",
        ["spruce-roofed-stone-cottage · wall 1"] = "cobblestone-andesite-noise",
        ["spruce-roofed-stone-cottage · wall 2"] = "stone-polished-andesite-checker",
        ["spruce-roofed-stone-longhouse · wall 1"] = "cobblestone-andesite-noise",
        ["spruce-roofed-stone-longhouse · wall 2"] = "stone-polished-andesite-checker",
        ["stone-and-sandstone-townhouse · storey 1 wall 1"] = "cobblestone-andesite-noise",
        ["stone-and-sandstone-townhouse · storey 1 wall 2"] = "stone-polished-andesite-checker",
        ["stone-and-spruce-barn · storey 1 wall 2"] = "stone-andesite-noise",
    };

    public override void Up()
    {
        foreach (var table in SlotTables)
            Alter.Table(table)
                .AddColumn("block_id").AsInt32().Nullable()
                .AddColumn("block_data").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("block_laid").AsBoolean().NotNullable().WithDefaultValue(false);
        foreach (var table in SlotTables.Skip(1))
            Alter.Column("style_id").OnTable(table).AsInt64().Nullable();
        Execute.WithConnection((connection, transaction) => Carry(new Db(connection, transaction)));
    }

    private static void Carry(Db db)
    {
        var styles = db.Rows("SELECT id, name, kind, params_json FROM style ORDER BY id")
            .Select(row => new Style(Convert.ToInt64(row["id"], CultureInfo.InvariantCulture), (string)row["name"]!,
                (string)row["kind"]!, (string)row["params_json"]!))
            .ToList();

        // Blocks into their slots.
        var blocks = new Dictionary<long, JsonObject>();
        foreach (var style in styles)
        {
            if (BlockOf(style) is not { } block) continue;
            blocks[style.Id] = block;
            foreach (var table in SlotTables)
                db.Run($"UPDATE {table} SET block_id = @block, block_data = @data, block_laid = @laid, style_id = NULL "
                       + "WHERE style_id = @id",
                    ("@block", block["id"]!.GetValue<int>()), ("@data", block["data"]?.GetValue<int>() ?? 0),
                    ("@laid", style.Kind == "laidLog"), ("@id", style.Id));
        }

        // Patterns holding one thing, into one row each.
        var patterns = styles.Where(style => !blocks.ContainsKey(style.Id)).ToList();
        var group = patterns.ToDictionary(style => style.Id, style => style.Id);
        long Root(long id) => group[id] == id ? id : group[id] = Root(group[id]);
        void Join(long one, long other)
        {
            var (a, b) = (Root(one), Root(other));
            if (a != b) group[Math.Max(a, b)] = Math.Min(a, b);
        }
        foreach (var same in patterns.GroupBy(style => Canonical(style.Params)).Where(same => same.Count() > 1))
            foreach (var style in same.Skip(1)) Join(same.First().Id, style.Id);
        foreach (var same in patterns.Where(style => Renamed.ContainsKey(style.Name))
                     .GroupBy(style => Renamed[style.Name]).Where(same => same.Count() > 1))
            foreach (var style in same.Skip(1)) Join(same.First().Id, style.Id);

        var named = new Dictionary<string, string>(StringComparer.Ordinal);   // a name or id a refinement may hold → the row kept's
        var keptIds = new Dictionary<long, long>();
        var byId = patterns.ToDictionary(style => style.Id);
        var taken = styles.Where(style => !blocks.ContainsKey(style.Id)).Select(style => style.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var members in patterns.GroupBy(style => Root(style.Id)))
        {
            var kept = byId[members.Key];
            var name = members.Select(style => Renamed.GetValueOrDefault(style.Name)).FirstOrDefault(target => target is not null)
                       ?? kept.Name;
            foreach (var style in members.Where(style => style.Id != kept.Id))
            {
                foreach (var table in SlotTables)
                    db.Run($"UPDATE {table} SET style_id = @kept WHERE style_id = @id", ("@kept", kept.Id), ("@id", style.Id));
                db.Run("DELETE FROM style WHERE id = @id", ("@id", style.Id));
                taken.Remove(style.Name);
            }
            if (name != kept.Name && !taken.Contains(name))
            {
                db.Run("UPDATE style SET name = @name WHERE id = @id", ("@name", name), ("@id", kept.Id));
                taken.Remove(kept.Name);
                taken.Add(name);
            }
            else name = kept.Name;
            foreach (var style in members)
            {
                named[style.Name] = name;
                named[style.Id.ToString(CultureInfo.InvariantCulture)] = kept.Id.ToString(CultureInfo.InvariantCulture);
                keptIds[style.Id] = kept.Id;
            }
        }

        foreach (var id in blocks.Keys) db.Run("DELETE FROM style WHERE id = @id", ("@id", id));

        MergeParts(db, "roof_style", "roof_style_course", "roof_style_id", [("room_style", "roof_style_id")]);
        MergeParts(db, "storey_style", "storey_style_course", "storey_style_id", [("room_style_storey", "storey_style_id")]);
        MergeParts(db, "porch_style", null, null, [("room_style", "porch_style_id")]);

        var blockByName = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var style in styles.Where(style => blocks.ContainsKey(style.Id)))
        {
            blockByName.TryAdd(style.Name, blocks[style.Id]);
            blockByName.TryAdd(style.Id.ToString(CultureInfo.InvariantCulture), blocks[style.Id]);
        }
        RewriteRefinements(db, blockByName, named, keptIds);
    }

    private sealed record Style(long Id, string Name, string Kind, string Params);

    /// <summary>The one block a <c>solid</c> or <c>laidLog</c> row lays, as its material — or null for a pattern,
    /// and for params that will not parse, which stay a row exactly as found.</summary>
    private static JsonObject? BlockOf(Style style)
    {
        if (style.Kind is not ("solid" or "laidLog")) return null;
        try
        {
            return JsonNode.Parse(style.Params) is JsonObject block && block["id"] is JsonValue id
                   && id.TryGetValue<int>(out _)
                ? block
                : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Params in one form two equal ones share: every object's members in order of name. Params that
    /// will not parse are their own form.</summary>
    private static string Canonical(string json)
    {
        try { return Sorted(JsonNode.Parse(json))?.ToJsonString() ?? json; }
        catch (JsonException) { return json; }
    }

    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject members => new JsonObject(members.OrderBy(member => member.Key, StringComparer.Ordinal)
            .Select(member => KeyValuePair.Create(member.Key, Sorted(member.Value)))),
        JsonArray items => new JsonArray([.. items.Select(Sorted)]),
        _ => node?.DeepClone(),
    };

    /// <summary>A part library's rows holding the same columns and courses, merged into the lowest id, with every
    /// row binding another taking that one.</summary>
    private static void MergeParts(
        Db db, string table, string? courseTable, string? owner, (string Table, string Column)[] boundBy)
    {
        var courses = (courseTable is null ? [] : db.Rows($"SELECT * FROM {courseTable}"))
            .ToLookup(row => Convert.ToInt64(row[owner!], CultureInfo.InvariantCulture));
        var parts = db.Rows($"SELECT * FROM {table} ORDER BY id");
        string Key(Dictionary<string, object?> row)
        {
            var id = Convert.ToInt64(row["id"], CultureInfo.InvariantCulture);
            var stack = courseTable is null
                ? []
                : courses[id].Select(course => Columns(course, "id", owner!))
                    .OrderBy(course => course, StringComparer.Ordinal).ToList();
            return Columns(row, "id", "name", "created_at") + "|" + string.Join(";", stack);
        }
        foreach (var same in parts.GroupBy(Key).Where(same => same.Count() > 1))
        {
            var kept = Convert.ToInt64(same.First()["id"], CultureInfo.InvariantCulture);
            foreach (var row in same.Skip(1))
            {
                var id = Convert.ToInt64(row["id"], CultureInfo.InvariantCulture);
                foreach (var (bound, column) in boundBy)
                    db.Run($"UPDATE {bound} SET {column} = @kept WHERE {column} = @id", ("@kept", kept), ("@id", id));
                db.Run($"DELETE FROM {table} WHERE id = @id", ("@id", id));
            }
        }
    }

    private static string Columns(Dictionary<string, object?> row, params string[] skipped)
        => string.Join(",", row.Where(column => !skipped.Contains(column.Key))
            .OrderBy(column => column.Key, StringComparer.Ordinal)
            .Select(column => $"{column.Key}={Convert.ToString(column.Value, CultureInfo.InvariantCulture)}"));

    /// <summary>Every map's current refinement, each material it names from the library following the row it
    /// named.</summary>
    private static void RewriteRefinements(
        Db db, IReadOnlyDictionary<string, JsonObject> blocks, IReadOnlyDictionary<string, string> named,
        IReadOnlyDictionary<long, long> keptIds)
    {
        foreach (var (id, data) in db.Blobs("SELECT id, data FROM map_artifact WHERE kind = 'refinement_json'"))
        {
            if (Parsed(Encoding.UTF8.GetString(data)) is not JsonObject refinement) continue;
            if (!Follow(refinement, "", blocks, named, keptIds, replace: null)) continue;
            db.Run("UPDATE map_artifact SET data = @data WHERE id = @id",
                ("@data", Encoding.UTF8.GetBytes(refinement.ToJsonString(Written))), ("@id", id));
        }
    }

    /// <summary>A refinement written back as it was written: a name keeps its own letters rather than escapes.</summary>
    private static readonly JsonSerializerOptions Written = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static JsonNode? Parsed(string json)
    {
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }

    private static bool Follow(
        JsonNode node, string path, IReadOnlyDictionary<string, JsonObject> blocks,
        IReadOnlyDictionary<string, string> named, IReadOnlyDictionary<long, long> keptIds, Action<JsonNode>? replace)
    {
        var changed = false;
        if (node is JsonObject stated && stated["library"] is JsonValue value && IsMaterial(path, stated))
        {
            var called = value.TryGetValue<string>(out var name) ? name
                : value.TryGetValue<long>(out var id) ? id.ToString(CultureInfo.InvariantCulture)
                : "";
            if (blocks.TryGetValue(called, out var block) && replace is not null)
            {
                var material = block.DeepClone().AsObject();
                foreach (var (key, field) in stated)
                    if (key is not ("library" or "row" or "hash")) material[key] = field?.DeepClone();
                replace(material);
                return true;
            }
            if (named.TryGetValue(called, out var kept) && kept != called)
            {
                stated["library"] = value.TryGetValue<long>(out _) ? long.Parse(kept, CultureInfo.InvariantCulture) : kept;
                changed = true;
            }
            if (stated["row"] is JsonValue row && row.TryGetValue<long>(out var resolved)
                && keptIds.TryGetValue(resolved, out var keptRow) && keptRow != resolved)
            {
                stated["row"] = keptRow;
                changed = true;
            }
        }
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, child) in members.ToList())
                    if (child is not null)
                        changed |= Follow(child, path.Length == 0 ? key : $"{path}.{key}", blocks, named, keptIds,
                            replacement => members[key] = replacement);
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                {
                    var index = at;
                    if (items[at] is { } child)
                        changed |= Follow(child, $"{path}[{at}]", blocks, named, keptIds,
                            replacement => items[index] = replacement);
                }
                break;
        }
        return changed;
    }

    /// <summary>Whether a name at this path stands for a material: anything but a theme, a room style, a prop
    /// style or the biome.</summary>
    private static bool IsMaterial(string path, JsonObject stated) =>
        !(Theme().IsMatch(path) || RoomShell().IsMatch(path) || HouseOwnStyle().IsMatch(path)
          || DressingStyle().IsMatch(path) || path == "biome");

    [GeneratedRegex(@"^themes\.[^.\[]+$")]
    private static partial Regex Theme();

    [GeneratedRegex(@"^roomStyles\.[^.\[]+$")]
    private static partial Regex RoomShell();

    [GeneratedRegex(@"^dressing\.props\[\d+\]\.style$")]
    private static partial Regex HouseOwnStyle();

    [GeneratedRegex(@"^dressing\.styles\.[^.\[]+$")]
    private static partial Regex DressingStyle();

    /// <summary>Every block a slot holds back into a row of its own — the <c>solid</c> or <c>laidLog</c> row
    /// already holding it, else a new one named for the block — and the columns gone. A merged row stays merged:
    /// the rows it replaced held what it holds.</summary>
    public override void Down()
    {
        Execute.WithConnection((connection, transaction) =>
        {
            var db = new Db(connection, transaction);
            var rows = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var style in db.Rows("SELECT id, kind, params_json FROM style WHERE kind IN ('solid', 'laidLog')"))
                rows.TryAdd(Canonical((string)style["params_json"]!), Convert.ToInt64(style["id"], CultureInfo.InvariantCulture));
            foreach (var table in SlotTables)
                foreach (var slot in db.Rows($"SELECT DISTINCT block_id, block_data, block_laid FROM {table} WHERE block_id IS NOT NULL"))
                {
                    var (block, data) = (Convert.ToInt32(slot["block_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(slot["block_data"], CultureInfo.InvariantCulture));
                    var laid = Convert.ToBoolean(slot["block_laid"], CultureInfo.InvariantCulture);
                    var kind = laid ? "laidLog" : "solid";
                    var json = $"{{\"kind\":\"{kind}\",\"id\":{block},\"data\":{data}}}";
                    if (!rows.TryGetValue(Canonical(json), out var id))
                    {
                        db.Run("INSERT INTO style (name, kind, params_json, created_at) VALUES (@name, @kind, @json, NOW())",
                            ("@name", $"{kind} {block}:{data}"), ("@kind", kind), ("@json", json));
                        id = Convert.ToInt64(db.Rows("SELECT LAST_INSERT_ID() AS id")[0]["id"], CultureInfo.InvariantCulture);
                        rows[Canonical(json)] = id;
                    }
                    db.Run($"UPDATE {table} SET style_id = @id WHERE block_id = @block AND block_data = @data AND block_laid = @laid",
                        ("@id", id), ("@block", block), ("@data", data), ("@laid", laid));
                }
        });
        foreach (var table in SlotTables)
        {
            Delete.Column("block_id").FromTable(table);
            Delete.Column("block_data").FromTable(table);
            Delete.Column("block_laid").FromTable(table);
        }
        foreach (var table in SlotTables.Skip(1))
            Alter.Column("style_id").OnTable(table).AsInt64().NotNullable();
    }

    /// <summary>The handful of statements the migration needs, over the runner's own connection and
    /// transaction.</summary>
    private sealed class Db(IDbConnection connection, IDbTransaction transaction)
    {
        public void Run(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            command.ExecuteNonQuery();
        }

        public List<Dictionary<string, object?>> Rows(string sql)
        {
            using var command = Command(sql, []);
            using var reader = command.ExecuteReader();
            var rows = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var column = 0; column < reader.FieldCount; column++)
                    row[reader.GetName(column)] = reader.IsDBNull(column) ? null : reader.GetValue(column);
                rows.Add(row);
            }
            return rows;
        }

        public List<(long Id, byte[] Data)> Blobs(string sql)
        {
            using var command = Command(sql, []);
            using var reader = command.ExecuteReader();
            var rows = new List<(long, byte[])>();
            while (reader.Read()) rows.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            return rows;
        }

        private IDbCommand Command(string sql, (string Name, object Value)[] parameters)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            return command;
        }
    }
}
