using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every room's building becomes a house of one wing. A spawn's or a wool's <c>footprint</c>, the box
/// <c>[minX, maxX) × [minZ, maxZ)</c> its shell was stamped on, becomes <c>house</c> holding the one wing whose
/// corners are its first and last cells, <c>[[minX, minZ], [maxX − 1, maxZ − 1]]</c>, stating no style of its
/// own; a room stating no footprint states no house. Each map's current intent is rewritten, and so is every
/// intent a change recorded, under the hash of its new bytes, so a restore reads the same shape. Forward-only.
/// </summary>
[Migration(81, "A room's building is a house")]
public sealed class M0081_RoomHouses : Migration
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

    // Returns the re-serialized bytes only when a room stated a footprint, so every other blob is left as it is.
    internal static bool TryCarry(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent) return false;

        var carried = false;
        foreach (var list in new[] { "spawns", "wools" })
        {
            if (intent[list] is not JsonArray rooms) continue;
            foreach (var room in rooms.OfType<JsonObject>())
            {
                if (!room.ContainsKey("footprint")) continue;
                carried = true;
                var footprint = room["footprint"] as JsonObject;
                room.Remove("footprint");
                if (footprint is null) continue;
                int minX = Number(footprint, "minX"), minZ = Number(footprint, "minZ");
                int maxX = Number(footprint, "maxX"), maxZ = Number(footprint, "maxZ");
                room["house"] = new JsonObject
                {
                    ["wings"] = new JsonArray(new JsonObject
                    {
                        ["corners"] = new JsonArray(new JsonArray(minX, minZ), new JsonArray(maxX - 1, maxZ - 1)),
                    }),
                };
            }
        }
        if (!carried) return false;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    // Truncated, as the build read a footprint's edges.
    private static int Number(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<double>(out var number) ? (int)number : 0;

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
