using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0095</c> makes a doorway width of 2 the unstated default everywhere one is stored: a library row's
/// <c>door_width</c> turns null, and so does every snapshot's <c>doorway.width</c> of 2 — in a composed board, a
/// map's JSON documents and the documents its changes recorded — while any other width, and a document that is
/// not JSON, stay as they were. Rolls the schema back and forward, so it runs apart from every other database
/// test.
/// </summary>
[NotInParallel]
public sealed class DoorwayWidthDefaultMigrationTests
{
    // A bound wool shell at the default, a spawn shell stating 3, and a room's own house in the shape a style had
    // before its doorway was a part.
    private const string Layout =
        """{"roomStyles":{"wool":{"wall":{},"doorway":{"door":"air","head":{"form":"none"},"width":2,"height":3}},"spawn":{"doorway":{"width":3,"height":3}}},"name":"Grüne Wiese","house":{"wall":{},"doorWidth":2,"doorHeight":3}}""";

    private const string LayoutCarried =
        """{"roomStyles":{"wool":{"wall":{},"doorway":{"door":"air","head":{"form":"none"},"width":null,"height":3}},"spawn":{"doorway":{"width":3,"height":3}}},"name":"Grüne Wiese","house":{"wall":{},"doorWidth":null,"doorHeight":3}}""";

    // A window's width of 2 is not a doorway's.
    private const string Untouched = """{"windows":{"form":"pane","width":2},"doorway":{"width":4}}""";

    [Test]
    public async Task Every_stored_doorway_width_of_two_becomes_the_default_and_nothing_else_changes()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 94);
        try
        {
            long defaultRow, statedRow, boardId, artifactId, untouchedId, brokenId;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                defaultRow = Row(db, "cottage", 2);
                statedRow = Row(db, "manor", 3);
                boardId = Board(db, Layout);
                artifactId = Artifact(db, "board", "sketch_layout_json", Layout);
                untouchedId = Artifact(db, "other", "sketch_layout_json", Untouched);
                brokenId = Artifact(db, "broken", "sketch_layout_json", "\"doorway\":{\"width\":2 {");
                recorded = Record(db, "board", "sketch_layout_json", Layout);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 95);

            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<int?>($"SELECT door_width FROM room_style WHERE id = {defaultRow}")).IsNull();
                await Assert.That(db.Execute<int?>($"SELECT door_width FROM room_style WHERE id = {statedRow}")).IsEqualTo(3);
                await Assert.That(db.Execute<string>($"SELECT layout_json FROM composed_board WHERE id = {boardId}"))
                    .IsEqualTo(LayoutCarried);
                await Assert.That(Encoding.UTF8.GetString(Data(db, artifactId))).IsEqualTo(LayoutCarried);
                await Assert.That(Encoding.UTF8.GetString(Data(db, untouchedId))).IsEqualTo(Untouched);
                await Assert.That(Encoding.UTF8.GetString(Data(db, brokenId))).IsEqualTo("\"doorway\":{\"width\":2 {");

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

    private static long Row(DataConnection db, string name, int doorWidth)
    {
        db.Execute("INSERT INTO room_style (name, door_width, created_at) VALUES (@name, @width, NOW())",
            new DataParameter("name", name), new DataParameter("width", doorWidth));
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
