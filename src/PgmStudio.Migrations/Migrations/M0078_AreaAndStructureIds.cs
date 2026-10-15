using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every build area, water lane, iron cube and approach wall of a stored intent carries an <c>id</c>, as the
/// spawns, wools and objectives do. One stored without one is given the id <c>PlayIds</c> mints for it —
/// <c>build-{n}</c>, <c>water-lane-{n}</c>, <c>iron-{n}</c>, <c>wall-{n}</c> by its position in its list,
/// numbered <c>-2</c>, <c>-3</c>… where it would share an id another piece already states. An id already
/// stated is kept. Only each map's current intent is rewritten; the documents a change recorded are kept as
/// they were written, and one restored is given its ids by the store it passes through. Forward-only.
/// </summary>
[Migration(78, "Build areas, water lanes, iron cubes and walls carry an id")]
public sealed class M0078_AreaAndStructureIds : Migration
{
    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var rows = new List<(long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, data FROM map_artifact WHERE kind = 'map_intent_json'";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    rows.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            }

            foreach (var (id, data) in rows)
            {
                if (!TryAssign(data, out var updated)) continue;
                using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE map_artifact SET data = @data WHERE id = @id";
                AddParam(upd, "@data", updated);
                AddParam(upd, "@id", id);
                upd.ExecuteNonQuery();
            }
        });
    }

    public override void Down() { }

    // Every list whose entries state an id, so a minted one never repeats any of them.
    private static readonly (string Parent, string List)[] Named =
    [
        ("", "spawns"), ("", "wools"), ("", "destroyables"), ("", "cores"),
        ("build", "areas"), ("waterLanes", "rects"), ("structures", "ironCubes"), ("structures", "walls"),
    ];

    private static readonly (string Parent, string List, string Prefix)[] Minted =
    [
        ("build", "areas", "build"), ("waterLanes", "rects", "water-lane"),
        ("structures", "ironCubes", "iron"), ("structures", "walls", "wall"),
    ];

    // Returns the re-serialized bytes only when an entry had no id, so every other blob is left as it is.
    internal static bool TryAssign(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent) return false;

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parent, list) in Named)
            foreach (var entry in Entries(intent, parent, list))
                if (Text(entry, "id") is { Length: > 0 } stated) taken.Add(stated);

        var changed = false;
        foreach (var (parent, list, prefix) in Minted)
        {
            var position = 0;
            foreach (var entry in Entries(intent, parent, list))
            {
                position++;
                if (Text(entry, "id").Length > 0) continue;
                var name = $"{prefix}-{position}";
                var id = name;
                for (var suffix = 2; !taken.Add(id); suffix++) id = $"{name}-{suffix}";
                entry["id"] = id;
                changed = true;
            }
        }

        if (!changed) return false;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    private static IEnumerable<JsonObject> Entries(JsonObject intent, string parent, string list)
    {
        var holder = parent.Length == 0 ? intent : intent[parent] as JsonObject;
        return holder?[list] is JsonArray entries ? entries.OfType<JsonObject>() : [];
    }

    private static string Text(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
