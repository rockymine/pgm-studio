using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A band stack whose last band claims everything past it states <c>"ending":"hold"</c>, where it stated
/// <c>"repeat"</c>. Every stored stack is rewritten: the library's styles (<c>style.params_json</c>), a boulder's
/// rock (<c>boulder_style.rock</c>), a composed board's layout and intent, every JSON document a map keeps
/// (<c>map_artifact</c>), and every document a change recorded — stored under the hash of its new bytes, so a
/// restore reads the same shape. The pair is replaced in place and nothing else in the text is touched; a
/// document that is not JSON, or states no such pair, is left byte for byte as it was. Forward-only.
/// </summary>
[Migration(88, "A band stack that holds states hold")]
public sealed partial class M0088_BandEndingHold : Migration
{
    private static readonly (string Table, string Column)[] TextColumns =
    [
        ("style", "params_json"),
        ("boulder_style", "rock"),
        ("composed_board", "layout_json"),
        ("composed_board", "intent_json"),
    ];

    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            foreach (var (table, column) in TextColumns)
            {
                var stored = new List<(long Id, string Text)>();
                using (var sel = conn.CreateCommand())
                {
                    sel.Transaction = tx;
                    sel.CommandText = $"SELECT id, {column} FROM {table} WHERE INSTR({column}, '\"ending\"') > 0";
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
                sel.CommandText = "SELECT id, data FROM map_artifact WHERE kind LIKE '%\\_json' AND INSTR(data, '\"ending\"') > 0";
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

    [GeneratedRegex("(\"ending\"\\s*:\\s*)\"repeat\"", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatEnding();

    // Returns the new text only when the document is JSON and states the old pair; the replacement is the
    // pair alone, so the rest of the text is exactly what was stored.
    internal static bool TryCarry(string text, out string updated)
    {
        updated = text;
        if (!RepeatEnding().IsMatch(text)) return false;
        try { using var _ = JsonDocument.Parse(text); }
        catch (JsonException) { return false; }
        updated = RepeatEnding().Replace(text, "$1\"hold\"");
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
