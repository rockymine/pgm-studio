using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Normalize stored authoring intents (<c>map_artifact.kind = 'map_intent_json'</c>) after rooms /
/// spawn-protection became a <b>union of rectangles</b>: a wool's <c>room</c> and a spawn's
/// <c>protection</c> used to be a single <c>{minX,minZ,maxX,maxZ}</c> object (or null) and are now an
/// array of them. Wrap each legacy single object in a one-element array and turn an explicit null into an
/// empty array, in place, so existing drafts keep loading without relying on the read-time tolerance
/// (<c>RectListJsonConverter</c>). Already-array blobs are left untouched. Forward-only — the data is a
/// strict superset of the old shape, so there is nothing to roll back.
/// </summary>
[Migration(5, "Intent room/protection → rect arrays")]
public sealed class M0005_IntentRectArrays : Migration
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
                using var r = sel.ExecuteReader();
                while (r.Read())
                    rows.Add((r.GetInt64(0), (byte[])r.GetValue(1)));
            }

            foreach (var (id, data) in rows)
            {
                if (!TryNormalize(data, out var updated)) continue;   // already arrays / unparseable → skip
                using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE map_artifact SET data = @data WHERE id = @id";
                AddParam(upd, "@data", updated);
                AddParam(upd, "@id", id);
                upd.ExecuteNonQuery();
            }
        });
    }

    public override void Down() { }   // forward-only: the new array shape supersets the old single-object one

    // Wrap spawns[].protection and wools[].room legacy single objects (and nulls) into arrays. Returns the
    // re-serialized bytes only when something changed, so untouched (already-array) blobs aren't rewritten.
    private static bool TryNormalize(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject obj) return false;

        var changed = false;
        if (obj["spawns"] is JsonArray spawns)
            foreach (var s in spawns.OfType<JsonObject>())
                changed |= WrapRectField(s, "protection");
        if (obj["wools"] is JsonArray wools)
            foreach (var w in wools.OfType<JsonObject>())
                changed |= WrapRectField(w, "room");

        if (!changed) return false;
        updated = Encoding.UTF8.GetBytes(obj.ToJsonString());
        return true;
    }

    // A legacy single rect object → [object]; an explicit null → []; an array or absent field → unchanged.
    private static bool WrapRectField(JsonObject parent, string field)
    {
        switch (parent[field])
        {
            case JsonObject single:
                parent[field] = new JsonArray(single.DeepClone());
                return true;
            case null when parent.ContainsKey(field):
                parent[field] = new JsonArray();
                return true;
            default:
                return false;
        }
    }

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
