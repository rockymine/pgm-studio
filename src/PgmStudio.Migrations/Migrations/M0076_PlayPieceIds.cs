using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every play piece of a stored intent carries an <c>id</c>: each spawn, wool, destroyable and core. A piece
/// stored without one is given the id <c>PlayIds</c> mints for it — <c>spawn-{team}</c>,
/// <c>wool-{owner}-{color}</c> (or <c>wool-{owner}</c> with no colour), <c>destroyable-{owner}</c>,
/// <c>core-{owner}</c>, numbered <c>-2</c>, <c>-3</c>… where two would share one. An id already stated is
/// kept. Only each map's current intent is rewritten; the documents a change recorded are kept as they were
/// written, and one restored is given its ids by the store it passes through. Forward-only.
/// </summary>
[Migration(76, "Play pieces carry an id")]
public sealed class M0076_PlayPieceIds : Migration
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

    private static readonly (string List, Func<JsonObject, string> Named)[] Pieces =
    [
        ("spawns", piece => $"spawn-{Text(piece, "team")}"),
        ("wools", piece => Text(piece, "color") is { Length: > 0 } color
            ? $"wool-{Text(piece, "owner")}-{color}" : $"wool-{Text(piece, "owner")}"),
        ("destroyables", piece => $"destroyable-{Text(piece, "owner")}"),
        ("cores", piece => $"core-{Text(piece, "owner")}"),
    ];

    // Returns the re-serialized bytes only when a piece had no id, so every other blob is left as it is.
    internal static bool TryAssign(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent) return false;

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (list, _) in Pieces)
            foreach (var piece in Entries(intent, list))
                if (Text(piece, "id") is { Length: > 0 } stated) taken.Add(stated);

        var changed = false;
        foreach (var (list, named) in Pieces)
            foreach (var piece in Entries(intent, list))
            {
                if (Text(piece, "id").Length > 0) continue;
                var name = named(piece);
                var id = name;
                for (var suffix = 2; !taken.Add(id); suffix++) id = $"{name}-{suffix}";
                piece["id"] = id;
                changed = true;
            }

        if (!changed) return false;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    private static IEnumerable<JsonObject> Entries(JsonObject intent, string list) =>
        intent[list] is JsonArray entries ? entries.OfType<JsonObject>() : [];

    private static string Text(JsonObject piece, string key) =>
        piece[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
