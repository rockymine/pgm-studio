using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every approach wall of a stored intent runs through a list of corners: a wall stating its two ends as
/// <c>x1, z1, x2, z2</c> states them as <c>"points": [[x1, z1], [x2, z2]]</c>, and every other member of it is
/// kept. Rewritten wherever an intent is stored: a map's current intent (<c>map_artifact</c>), every intent a
/// change recorded, under the hash of its new bytes so a restore reads the same shape, and a composed board's
/// intent. An intent that is not JSON, or states no wall by its ends, is left byte for byte as it was.
/// Forward-only.
/// </summary>
[Migration(89, "An approach wall runs through its corners")]
public sealed class M0089_WallCorners : Migration
{
    private const string Kind = "map_intent_json";

    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var boards = new List<(long Id, string Text)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, intent_json FROM composed_board WHERE INSTR(intent_json, '\"x1\"') > 0";
                using var reader = sel.ExecuteReader();
                while (reader.Read()) boards.Add((reader.GetInt64(0), reader.GetString(1)));
            }
            foreach (var (id, text) in boards)
                if (TryCarry(Encoding.UTF8.GetBytes(text), out var updated))
                    Run(conn, tx, "UPDATE composed_board SET intent_json = @text WHERE id = @id",
                        ("@text", Encoding.UTF8.GetString(updated)), ("@id", id));

            var current = new List<(long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = $"SELECT id, data FROM map_artifact WHERE kind = '{Kind}'";
                using var reader = sel.ExecuteReader();
                while (reader.Read()) current.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            }
            foreach (var (id, data) in current)
                if (TryCarry(data, out var updated))
                    Run(conn, tx, "UPDATE map_artifact SET data = @data WHERE id = @id", ("@data", updated), ("@id", id));

            var recorded = new List<(long ChangeId, string Hash, byte[] Packed)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT d.change_id, d.blob_hash, b.data FROM map_change_document d "
                                  + $"JOIN document_blob b ON b.hash = d.blob_hash WHERE d.kind = '{Kind}'";
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
                Run(conn, tx, $"UPDATE map_change_document SET blob_hash = @next WHERE change_id = @change AND kind = '{Kind}'",
                    ("@next", next), ("@change", changeId));
            }
        });
    }

    public override void Down() { }

    // Returns the re-serialized bytes only when a wall stated its ends, so every other document is left as it is.
    internal static bool TryCarry(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent || intent["structures"] is not JsonObject structures
            || structures["walls"] is not JsonArray walls) return false;
        var ended = walls.OfType<JsonObject>().Where(wall => wall.ContainsKey("x1")).ToList();
        if (ended.Count == 0) return false;

        foreach (var wall in ended)
        {
            var points = new JsonArray(
                new JsonArray(Number(wall, "x1"), Number(wall, "z1")),
                new JsonArray(Number(wall, "x2"), Number(wall, "z2")));
            foreach (var key in new[] { "x1", "z1", "x2", "z2" }) wall.Remove(key);
            wall["points"] = points;
        }
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    private static int Number(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<double>(out var number) ? (int)Math.Round(number) : 0;

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
