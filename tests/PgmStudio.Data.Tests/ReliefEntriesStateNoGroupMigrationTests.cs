using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0085</c> drops <c>groupId</c> from every relief mark and push of a stored sketch layout, in the current
/// layout and in the one a change recorded, keeps every other field, and leaves a layout stating none byte for
/// byte as it was. Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class ReliefEntriesStateNoGroupMigrationTests
{
    private const string Tagged = """
        {"relief":{"isl_1":{"base":10,
          "marks":[{"id":"r2","kind":"point","at":[4,4],"h":6,"r":5,"groupId":"isl_1"},{"id":"r4","kind":"area","h":3,"ring":[[0,0],[4,0],[4,4]]}],
          "pushes":[{"id":"r5","ring":[[0,0],[6,0],[6,6]],"amount":5,"groupId":"isl_1"}]}},
         "layers":[]}
        """;

    private const string Clean = """{"relief":{"isl_1":{"marks":[{"id":"r1","kind":"point","at":[0,0],"h":4}],"pushes":[]}},"layers":[]}""";

    private const string Expected = """
        {"isl_1":{"base":10,
          "marks":[{"id":"r2","kind":"point","at":[4,4],"h":6,"r":5},{"id":"r4","kind":"area","h":3,"ring":[[0,0],[4,0],[4,4]]}],
          "pushes":[{"id":"r5","ring":[[0,0],[6,0],[6,6]],"amount":5}]}}
        """;

    [Test]
    public async Task Every_relief_entry_loses_the_group_it_was_tagged_with_and_nothing_else()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 84);
        try
        {
            long board, clean;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                board = Layout(db, "board", Tagged);
                clean = Layout(db, "clean", Clean);
                recorded = Record(db, "board", Tagged);
            }
            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 85);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(Same(Read(db, board)["relief"], Expected)).IsTrue()
                    .Because("a mark and a push lose their groupId and keep every field they state");
                await Assert.That(Encoding.UTF8.GetString(Data(db, clean))).IsEqualTo(Clean);

                var history = db.Execute<string>(
                    "SELECT blob_hash FROM map_change_document WHERE kind = 'sketch_layout_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded layout is stored under its new bytes");
                var kept = JsonNode.Parse(Gunzip(db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'")))!;
                await Assert.That(Same(kept["relief"], Expected)).IsTrue();
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    // The same JSON value, however its text escapes a character.
    private static bool Same(JsonNode? node, string expected) => JsonNode.DeepEquals(node, JsonNode.Parse(expected));

    private static long Layout(DataConnection db, string slug, string json)
    {
        db.Execute("INSERT INTO map (slug) VALUES (@slug)", new DataParameter("slug", slug));
        var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'sketch_layout_json', @data)",
            new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    // A change on the slug that wrote the layout, kept the way the change log keeps it.
    private static string Record(DataConnection db, string slug, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        db.Execute("INSERT INTO map_change (map_slug, number, created_at) VALUES (@slug, 1, UTC_TIMESTAMP())",
            new DataParameter("slug", slug));
        var change = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
            new DataParameter("hash", hash), new DataParameter("data", Gzip(bytes)), new DataParameter("length", (long)bytes.Length));
        db.Execute("INSERT INTO map_change_document (change_id, kind, blob_hash) VALUES (@change, 'sketch_layout_json', @hash)",
            new DataParameter("change", change), new DataParameter("hash", hash));
        return hash;
    }

    private static byte[] Data(DataConnection db, long artifact)
        => db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE id = {artifact}");

    private static JsonObject Read(DataConnection db, long artifact)
        => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(Data(db, artifact)))!;

    private static byte[] Gzip(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return packed.ToArray();
    }

    private static string Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
