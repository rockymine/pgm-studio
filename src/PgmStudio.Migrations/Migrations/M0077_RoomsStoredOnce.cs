using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A play room is stored once, in the intent. Each map's sketch layout loses its room shapes — every shape
/// carrying a <c>role</c> — and what each stated about the ground under it (<c>floor</c>, <c>base_height</c>,
/// <c>anchor_heights</c>, <c>relief_scope</c>, <c>height_authored</c>, <c>doors</c>, <c>theme</c>,
/// <c>material</c>) is kept in the layout's <c>rooms</c> record, keyed by the id the room is now drawn as:
/// its piece's id for a region, that id with <c>-building</c> after it for a building.
///
/// <para>A region is matched to its piece by the <c>intentRef</c> it carried — a spawn's team, a wool's
/// <c>owner:colour</c> — and a building by the region its id was derived from. The rectangle is the
/// intent's, which is what the world was built from, so the intent is not rewritten: a shape whose
/// rectangle the intent states differently, or does not state, was a picture of nothing the build made. A
/// shape matching no piece keeps its record under its own id. Only each map's current layout is rewritten;
/// the documents a change recorded are kept as written, and one restored is folded by the store it passes
/// through. Forward-only.</para>
/// </summary>
[Migration(77, "A play room is stored once, in the intent")]
public sealed class M0077_RoomsStoredOnce : Migration
{
    private static readonly string[] GroundFields =
        ["floor", "base_height", "anchor_heights", "relief_scope", "height_authored", "doors", "theme", "material"];

    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var rows = new List<(long MapId, string Kind, long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT map_id, kind, id, data FROM map_artifact "
                                  + "WHERE kind IN ('sketch_layout_json', 'map_intent_json')";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2), (byte[])reader.GetValue(3)));
            }

            foreach (var layout in rows.Where(row => row.Kind == "sketch_layout_json"))
            {
                var intent = rows.FirstOrDefault(row => row.MapId == layout.MapId && row.Kind == "map_intent_json");
                if (TryCarry(layout.Data, intent.Data, out var layoutAfter)) Rewrite(conn, tx, layout.Id, layoutAfter);
            }
        });
    }

    public override void Down() { }

    /// <summary>The layout with its room shapes folded into <c>rooms</c>, keyed by the ids the map's intent
    /// gives its rooms. False where the layout carries no room shape, so every other blob is left as it
    /// is.</summary>
    internal static bool TryCarry(byte[] layoutData, byte[]? intentData, out byte[] layoutAfter)
    {
        layoutAfter = layoutData;
        if (Parse(layoutData) is not JsonObject layout || layout["layers"] is not JsonArray layers) return false;
        var intent = intentData is null ? null : Parse(intentData) as JsonObject;

        var rooms = new List<JsonObject>();
        foreach (var layer in layers)
        {
            if (layer?["layout"]?["shapes"] is not JsonArray shapes) continue;
            for (var at = shapes.Count - 1; at >= 0; at--)
            {
                if (shapes[at] is not JsonObject shape || shape["role"] is not JsonValue) continue;
                rooms.Insert(0, shape);
                shapes.RemoveAt(at);
            }
        }
        if (rooms.Count == 0) return false;

        var record = layout["rooms"] as JsonObject ?? new JsonObject();
        var pieceOfRegion = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var shape in rooms.Where(shape => Text(shape, "role") != "building"))
        {
            var piece = intent is null ? null : PieceOf(intent, Text(shape, "role"), Text(shape, "intentRef"));
            if (piece is not null) pieceOfRegion[Text(shape, "id")] = piece;
            Keep(record, piece is not null && Text(piece, "id") is { Length: > 0 } id ? id : Text(shape, "id"), shape);
        }

        foreach (var shape in rooms.Where(shape => Text(shape, "role") == "building"))
        {
            var shapeId = Text(shape, "id");
            var region = shapeId.EndsWith("-building", StringComparison.Ordinal) ? shapeId[..^"-building".Length] : "";
            if (pieceOfRegion.TryGetValue(region, out var piece) && Text(piece, "id") is { Length: > 0 } id)
                shapeId = $"{id}-building";
            Keep(record, shapeId, shape);
        }

        if (record.Count > 0) layout["rooms"] = record;
        layoutAfter = Encoding.UTF8.GetBytes(layout.ToJsonString());
        return true;
    }

    /// <summary>The spawn whose team, or the wool whose owner and dye, the shape's <c>intentRef</c> names. A
    /// wool stating no dye is the owner's own colour and matches any.</summary>
    private static JsonObject? PieceOf(JsonObject intent, string role, string reference)
    {
        if (reference.Length == 0) return null;
        if (role == "spawn")
            return Entries(intent, "spawns").FirstOrDefault(spawn => Text(spawn, "team") == reference);
        var colon = reference.IndexOf(':');
        if (colon < 0) return null;
        var (owner, color) = (reference[..colon], reference[(colon + 1)..]);
        return Entries(intent, "wools").FirstOrDefault(wool => Text(wool, "owner") == owner && Text(wool, "color") == color)
            ?? Entries(intent, "wools").FirstOrDefault(wool => Text(wool, "owner") == owner && Text(wool, "color").Length == 0);
    }

    private static void Keep(JsonObject record, string id, JsonObject shape)
    {
        var ground = new JsonObject();
        foreach (var field in GroundFields)
            if (shape[field] is { } value) ground[field] = value.DeepClone();
        if (ground.Count > 0) record[id] = ground;
    }

    private static JsonNode? Parse(byte[] data)
    {
        try { return JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return null; }
    }

    private static IEnumerable<JsonObject> Entries(JsonObject intent, string list) =>
        intent[list] is JsonArray entries ? entries.OfType<JsonObject>() : [];

    private static string Text(JsonObject node, string key) =>
        node[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static void Rewrite(IDbConnection conn, IDbTransaction tx, long id, byte[] data)
    {
        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE map_artifact SET data = @data WHERE id = @id";
        AddParam(upd, "@data", data);
        AddParam(upd, "@id", id);
        upd.ExecuteNonQuery();
    }

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
