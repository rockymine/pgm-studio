using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0088</c> rewrites every stored <c>"ending":"repeat"</c> to <c>"ending":"hold"</c> — in a library style, a
/// boulder's rock, a composed board, a map's JSON documents and the documents its changes recorded — replacing
/// the pair alone, so the rest of a document is byte for byte what was stored, and leaving a document that
/// states no such pair, or only states it inside a string, as it was. Rolls the schema back and forward, so it
/// runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class BandEndingHoldMigrationTests
{
    // Spacing the serializer would never write, and an ending nested a stack deep, are both carried.
    private const string NestedStyle = """
        {"kind":"layered", "stack": {"bands":[{"material":{"kind":"layered","stack":{"bands":[],"ending" : "repeat"}},"thickness":2}],
          "ending":"repeat"}, "axis":"slope","beyond":{"kind":"solid","id":1,"data":0}}
        """;

    private const string NestedStyleCarried = """
        {"kind":"layered", "stack": {"bands":[{"material":{"kind":"layered","stack":{"bands":[],"ending" : "hold"}},"thickness":2}],
          "ending":"hold"}, "axis":"slope","beyond":{"kind":"solid","id":1,"data":0}}
        """;

    // A stack that hands over, and a string that merely contains the text of a repeating one.
    private const string Untouched =
        """{"kind":"layered","label":"Grüne Wiese \"ending\":\"repeat\"","stack":{"ending":"handOver","bands":[]}}""";

    private const string Layout =
        """{"layers":[{"material":{"kind":"layered","stack":{"ending":"repeat","bands":[]}}}],"name":"Wiese ä"}""";

    private const string LayoutCarried =
        """{"layers":[{"material":{"kind":"layered","stack":{"ending":"hold","bands":[]}}}],"name":"Wiese ä"}""";

    [Test]
    public async Task Every_stored_repeat_ending_becomes_hold_and_nothing_else_changes()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 85);
        try
        {
            long styleId, untouchedId, boulderId, boardId, artifactId, otherArtifactId;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                styleId = Style(db, "nested", NestedStyle);
                untouchedId = Style(db, "untouched", Untouched);
                boulderId = Boulder(db, NestedStyle);
                boardId = Board(db, Layout);
                artifactId = Artifact(db, "board", "sketch_layout_json", Layout);
                otherArtifactId = Artifact(db, "other", "surface_parquet", Layout);
                recorded = Record(db, "board", "sketch_layout_json", Layout);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 88);

            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<string>($"SELECT params_json FROM style WHERE id = {styleId}"))
                    .IsEqualTo(NestedStyleCarried).Because("the pair at every depth becomes hold, spacing and all");
                await Assert.That(db.Execute<string>($"SELECT params_json FROM style WHERE id = {untouchedId}"))
                    .IsEqualTo(Untouched).Because("a handOver stack, and a string quoting a repeat, are not the pair");
                await Assert.That(db.Execute<string>($"SELECT rock FROM boulder_style WHERE id = {boulderId}"))
                    .IsEqualTo(NestedStyleCarried);
                await Assert.That(db.Execute<string>($"SELECT layout_json FROM composed_board WHERE id = {boardId}"))
                    .IsEqualTo(LayoutCarried);
                await Assert.That(Encoding.UTF8.GetString(Data(db, artifactId))).IsEqualTo(LayoutCarried);
                await Assert.That(Encoding.UTF8.GetString(Data(db, otherArtifactId))).IsEqualTo(Layout)
                    .Because("only a map's JSON documents are read");

                var history = db.Execute<string>("SELECT blob_hash FROM map_change_document WHERE kind = 'sketch_layout_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded document is kept under its new bytes");
                var kept = db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'");
                await Assert.That(Gunzip(kept)).IsEqualTo(LayoutCarried);
                await Assert.That(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(LayoutCarried))))
                    .IsEqualTo(history);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    [Test]
    public async Task A_document_that_is_not_json_is_left_alone()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 85);
        try
        {
            const string NotJson = "\"ending\":\"repeat\" {";
            long artifactId;
            await using (var db = TestDb.Connect())
                artifactId = Artifact(db, "broken", "sketch_layout_json", NotJson);

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 88);

            await using (var db = TestDb.Connect())
                await Assert.That(Encoding.UTF8.GetString(Data(db, artifactId))).IsEqualTo(NotJson);
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Style(DataConnection db, string name, string json)
    {
        db.Execute("INSERT INTO style (name, kind, params_json, created_at) VALUES (@name, 'layered', @json, NOW())",
            new DataParameter("name", name), new DataParameter("json", json));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Boulder(DataConnection db, string json)
    {
        db.Execute("INSERT INTO boulder_style (name, rock) VALUES ('rock', @json)", new DataParameter("json", json));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Board(DataConnection db, string layout)
    {
        db.Execute("INSERT INTO composed_board (composer_version, band, symmetry, cell, seed, score, wool_count, wools, "
                   + "hub, frontline, card_json, layout_json, intent_json, svg, created_at) VALUES ('v', 'b', 'rot_180', 5, "
                   + "1, 0, 2, ',i,l,', 'i', 'u', '{}', @layout, '{}', '', NOW())",
            new DataParameter("layout", layout));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Artifact(DataConnection db, string slug, string kind, string json)
    {
        db.Execute("INSERT INTO map (slug) VALUES (@slug)", new DataParameter("slug", slug));
        var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, @kind, @data)",
            new DataParameter("map", map), new DataParameter("kind", kind), new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static string Record(DataConnection db, string slug, string kind, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        db.Execute("INSERT INTO map_change (map_slug, number, created_at) VALUES (@slug, 1, UTC_TIMESTAMP())",
            new DataParameter("slug", slug));
        var change = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
            new DataParameter("hash", hash), new DataParameter("data", Gzip(bytes)), new DataParameter("length", (long)bytes.Length));
        db.Execute("INSERT INTO map_change_document (change_id, kind, blob_hash) VALUES (@change, @kind, @hash)",
            new DataParameter("change", change), new DataParameter("kind", kind), new DataParameter("hash", hash));
        return hash;
    }

    private static byte[] Data(DataConnection db, long artifact)
        => db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE id = {artifact}");

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
