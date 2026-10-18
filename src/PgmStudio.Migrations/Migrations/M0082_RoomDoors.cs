using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A spawn's and a wool's doors are stated in one shape: <c>{edge, x?, z?}</c>, the way the wall faces and the
/// point on it the door is centred on, a door stating no point being centred on its wall. A spawn's
/// <c>doors</c>, edge words, become doors of that edge and no point, which is where the stamper centred them. A
/// wool's <c>entries</c>, degenerate rects on its region's boundary, become a door at each one's midpoint on the
/// edge of the region's bounds it lies along, which is where the stamper cut it; an entry on no edge cut nothing
/// and carries nothing. Each map's current intent is rewritten, and so is every intent a change recorded, under
/// the hash of its new bytes. Forward-only.
/// </summary>
[Migration(82, "A room's doors are stated in one shape")]
public sealed class M0082_RoomDoors : Migration
{
    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var current = new List<(long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, data FROM map_artifact WHERE kind = 'map_intent_json'";
                using var reader = sel.ExecuteReader();
                while (reader.Read()) current.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            }
            foreach (var (id, data) in current)
            {
                if (!TryCarry(data, out var updated)) continue;
                Run(conn, tx, "UPDATE map_artifact SET data = @data WHERE id = @id", ("@data", updated), ("@id", id));
            }

            var recorded = new List<(long ChangeId, string Hash, byte[] Packed)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT d.change_id, d.blob_hash, b.data FROM map_change_document d "
                                  + "JOIN document_blob b ON b.hash = d.blob_hash WHERE d.kind = 'map_intent_json'";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    recorded.Add((reader.GetInt64(0), reader.GetString(1), (byte[])reader.GetValue(2)));
            }
            var carried = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (changeId, hash, packed) in recorded)
            {
                if (!carried.TryGetValue(hash, out var next))
                {
                    next = null;
                    if (TryCarry(Gunzip(packed), out var updated))
                    {
                        next = Convert.ToHexStringLower(SHA256.HashData(updated));
                        if (Count(conn, tx, "SELECT COUNT(*) FROM document_blob WHERE hash = @hash", ("@hash", next)) == 0)
                            Run(conn, tx, "INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
                                ("@hash", next), ("@data", Gzip(updated)), ("@length", (long)updated.Length));
                    }
                    carried[hash] = next;
                }
                if (next is null) continue;
                Run(conn, tx, "UPDATE map_change_document SET blob_hash = @next "
                              + "WHERE change_id = @change AND kind = 'map_intent_json'",
                    ("@next", next), ("@change", changeId));
            }
        });
    }

    public override void Down() { }

    // Returns the re-serialized bytes only when a room stated door words or entries, so every other blob is left
    // as it is.
    internal static bool TryCarry(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent) return false;

        var carried = false;
        if (intent["spawns"] is JsonArray spawns)
            foreach (var spawn in spawns.OfType<JsonObject>())
            {
                if (spawn["doors"] is not JsonArray words || !words.Any(word => word is JsonValue)) continue;
                carried = true;
                spawn["doors"] = new JsonArray([.. words.OfType<JsonValue>()
                    .Select(word => (JsonNode)new JsonObject { ["edge"] = word.GetValue<string>() })]);
            }
        if (intent["wools"] is JsonArray wools)
            foreach (var wool in wools.OfType<JsonObject>())
            {
                if (!wool.ContainsKey("entries")) continue;
                carried = true;
                var entries = wool["entries"] as JsonArray;
                wool.Remove("entries");
                if (Bounds(wool["protection"]) is not { } bounds) continue;
                var doors = new JsonArray();
                foreach (var entry in (entries ?? []).OfType<JsonObject>())
                    if (Door(entry, bounds) is { } door) doors.Add(door);
                if (doors.Count > 0) wool["doors"] = doors;
            }
        if (!carried) return false;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    // The block bounds of a region, truncated as the stamper reads them; null for a region of no rects.
    private static (int MinX, int MinZ, int MaxX, int MaxZ)? Bounds(JsonNode? protection)
    {
        var rects = protection switch
        {
            JsonArray list => list.OfType<JsonObject>().ToList(),
            JsonObject one => [one],
            _ => [],
        };
        if (rects.Count == 0) return null;
        return ((int)rects.Min(rect => Number(rect, "minX")), (int)rects.Min(rect => Number(rect, "minZ")),
                (int)rects.Max(rect => Number(rect, "maxX")), (int)rects.Max(rect => Number(rect, "maxZ")));
    }

    // The door an entry cut: on the edge of the bounds its line lies along, at its midpoint; null for one on none.
    private static JsonObject? Door(JsonObject entry, (int MinX, int MinZ, int MaxX, int MaxZ) bounds)
    {
        const double tolerance = 0.01;
        static bool On(double a, double b) => Math.Abs(a - b) < tolerance;
        double minX = Number(entry, "minX"), minZ = Number(entry, "minZ"), maxX = Number(entry, "maxX"), maxZ = Number(entry, "maxZ");
        string? edge = null;
        if (On(minX, maxX)) edge = On(minX, bounds.MinX) ? "-x" : On(minX, bounds.MaxX) ? "+x" : null;
        else if (On(minZ, maxZ)) edge = On(minZ, bounds.MinZ) ? "-z" : On(minZ, bounds.MaxZ) ? "+z" : null;
        if (edge is null) return null;
        return new JsonObject { ["edge"] = edge, ["x"] = (minX + maxX) / 2, ["z"] = (minZ + maxZ) / 2 };
    }

    private static double Number(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<double>(out var number) ? number : 0;

    private static byte[] Gzip(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return packed.ToArray();
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var unpacked = new MemoryStream();
        gzip.CopyTo(unpacked);
        return unpacked.ToArray();
    }

    private static void Run(IDbConnection conn, IDbTransaction tx, string sql, params (string Name, object Value)[] values)
    {
        using var cmd = Command(conn, tx, sql, values);
        cmd.ExecuteNonQuery();
    }

    private static long Count(IDbConnection conn, IDbTransaction tx, string sql, params (string Name, object Value)[] values)
    {
        using var cmd = Command(conn, tx, sql, values);
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IDbCommand Command(IDbConnection conn, IDbTransaction tx, string sql, (string Name, object Value)[] values)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }
}
