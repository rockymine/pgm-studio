using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A doorway that states no width takes the default, and a stored width of 2 is that default. A room's door
/// follows its wall where its house states none (<c>WX7</c>) and a house's is 2 wide, so every 2 a doorway holds
/// becomes no width at all: <c>room_style.door_width</c> turns nullable and its 2s null, and every house style
/// snapshot a document keeps — a board's bound room styles, a room's own house, a house in a dressing registry —
/// has its <c>doorway.width</c> of 2 (or a pre-doorway style's <c>doorWidth</c>) written as null. The documents
/// are a composed board's layout and intent, every JSON document a map keeps (<c>map_artifact</c>), and every
/// document a change recorded, stored under the hash of its new bytes. A document that is not JSON, or holds no
/// such width, is left byte for byte as it was. Forward-only.
/// </summary>
[Migration(97, "A doorway's width of 2 is its default")]
public sealed class M0097_DoorwayWidthDefault : Migration
{
    private const int DefaultWidth = 2;

    private static readonly (string Table, string Column)[] TextColumns =
    [
        ("composed_board", "layout_json"),
        ("composed_board", "intent_json"),
    ];

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public override void Up()
    {
        Alter.Column("door_width").OnTable("room_style").AsInt32().Nullable();
        Execute.Sql($"UPDATE room_style SET door_width = NULL WHERE door_width = {DefaultWidth}");

        Execute.WithConnection((conn, tx) =>
        {
            foreach (var (table, column) in TextColumns)
            {
                var stored = new List<(long Id, string Text)>();
                using (var sel = conn.CreateCommand())
                {
                    sel.Transaction = tx;
                    sel.CommandText = $"SELECT id, {column} FROM {table} WHERE INSTR({column}, 'doorway') > 0 "
                                      + $"OR INSTR({column}, 'doorWidth') > 0";
                    using var reader = sel.ExecuteReader();
                    while (reader.Read()) stored.Add((reader.GetInt64(0), reader.GetString(1)));
                }
                foreach (var (id, text) in stored)
                    if (TryCarry(text, out var updated))
                        Run(conn, tx, $"UPDATE {table} SET {column} = @text WHERE id = @id", ("@text", updated), ("@id", id));
            }

            var documents = new List<(long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, data FROM map_artifact WHERE kind LIKE '%\\_json' "
                                  + "AND (INSTR(data, 'doorway') > 0 OR INSTR(data, 'doorWidth') > 0)";
                using var reader = sel.ExecuteReader();
                while (reader.Read()) documents.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            }
            foreach (var (id, data) in documents)
                if (TryCarry(data, out var updated))
                    Run(conn, tx, "UPDATE map_artifact SET data = @data WHERE id = @id", ("@data", updated), ("@id", id));

            var recorded = new List<(long ChangeId, string Kind, string Hash, byte[] Packed)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT d.change_id, d.kind, d.blob_hash, b.data FROM map_change_document d "
                                  + "JOIN document_blob b ON b.hash = d.blob_hash";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    recorded.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), (byte[])reader.GetValue(3)));
            }
            var carried = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (changeId, kind, hash, packed) in recorded)
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
                Run(conn, tx, "UPDATE map_change_document SET blob_hash = @next WHERE change_id = @change AND kind = @kind",
                    ("@next", next), ("@change", changeId), ("@kind", kind));
            }
        });
    }

    public override void Down() { }

    /// <summary>The document with every doorway width of 2 written as null, or false where it is not JSON or
    /// holds none.</summary>
    internal static bool TryCarry(string text, out string updated)
    {
        updated = text;
        JsonNode? root;
        try { root = JsonNode.Parse(text); }
        catch (JsonException) { return false; }
        if (root is null || !Carry(root)) return false;
        updated = root.ToJsonString(Compact);
        return true;
    }

    internal static bool TryCarry(byte[] data, out byte[] updated)
    {
        updated = data;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(data); }
        catch (DecoderFallbackException) { return false; }
        if (!TryCarry(text, out var carried)) return false;
        updated = Encoding.UTF8.GetBytes(carried);
        return true;
    }

    /// <summary>Every default width under <paramref name="node"/> written as null: a doorway's <c>width</c>, and a
    /// style written before its doorway was a part of its own, whose <c>doorWidth</c> stood beside its walls.</summary>
    private static bool Carry(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject item:
                if (item["doorway"] is JsonObject doorway) changed |= Unstate(doorway, "width");
                if (item.ContainsKey("doorWidth") && (item.ContainsKey("wall") || item.ContainsKey("doorHeight")))
                    changed |= Unstate(item, "doorWidth");
                foreach (var (_, child) in item.ToList())
                    if (child is not null) changed |= Carry(child);
                break;
            case JsonArray items:
                foreach (var child in items)
                    if (child is not null) changed |= Carry(child);
                break;
        }
        return changed;
    }

    private static bool Unstate(JsonObject owner, string key)
    {
        if (owner[key] is not JsonValue value || !value.TryGetValue<int>(out var width) || width != DefaultWidth)
            return false;
        owner[key] = null;
        return true;
    }

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
