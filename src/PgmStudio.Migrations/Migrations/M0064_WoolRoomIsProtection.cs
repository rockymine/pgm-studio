using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A wool's room region is stated as <c>protection</c>, the field a spawn's carries under the same name, and
/// the intent reader keeps nothing under <c>room</c>. A stored intent whose wool still states <c>room</c> moves
/// it to <c>protection</c> — wrapped in an array where it is a single rectangle — unless the wool already
/// states a non-empty <c>protection</c>, which is the newer answer and is kept. Forward-only.
/// </summary>
[Migration(64, "A wool's room is its protection")]
public sealed class M0064_WoolRoomIsProtection : Migration
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
                if (!TryMove(data, out var updated)) continue;
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

    // Returns the re-serialized bytes only when a wool stated `room`, so every other blob is left as it is.
    private static bool TryMove(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent || intent["wools"] is not JsonArray wools) return false;

        var changed = false;
        foreach (var wool in wools.OfType<JsonObject>())
        {
            if (!wool.ContainsKey("room")) continue;
            var room = wool["room"];
            wool.Remove("room");
            changed = true;

            var stated = wool["protection"] is JsonArray existing && existing.Count > 0;
            if (stated) continue;
            wool["protection"] = room switch
            {
                JsonArray rects => rects.DeepClone(),
                JsonObject single => new JsonArray(single.DeepClone()),
                _ => new JsonArray(),
            };
        }

        if (!changed) return false;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
