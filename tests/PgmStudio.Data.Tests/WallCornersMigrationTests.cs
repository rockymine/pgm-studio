using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0089</c> carries every stored wall from its two ends to its corners — in a map's intent, the intents its
/// changes recorded and a composed board's intent — keeping every other member of the wall, and leaving an intent
/// that states no wall by its ends as it was. Rolls the schema back and forward, so it runs apart from every other
/// database test.
/// </summary>
[NotInParallel]
public sealed class WallCornersMigrationTests
{
    private const string Ended =
        """{"structures":{"walls":[{"x1":-40,"z1":-10,"x2":-40,"z2":10,"topY":20,"stamp":{"kind":"wall","unit":"0","image":0},"id":"wall-1"}],"chests":[]}}""";

    private const string Cornered =
        """{"structures":{"walls":[{"topY":20,"stamp":{"kind":"wall","unit":"0","image":0},"id":"wall-1","points":[[-40,-10],[-40,10]]}],"chests":[]}}""";

    private const string NoWalls = """{"spawns":[],"structures":{"walls":[],"chests":[]}}""";

    [Test]
    public async Task Every_stored_wall_runs_through_its_two_ends()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 88);
        try
        {
            long boardId, artifactId, untouchedId;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                boardId = Board(db, Ended);
                artifactId = Artifact(db, "walled", "map_intent_json", Ended);
                untouchedId = Artifact(db, "open", "map_intent_json", NoWalls);
                recorded = Record(db, "walled", "map_intent_json", Ended);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 89);

            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<string>($"SELECT intent_json FROM composed_board WHERE id = {boardId}"))
                    .IsEqualTo(Cornered);
                await Assert.That(Encoding.UTF8.GetString(Data(db, artifactId))).IsEqualTo(Cornered);
                await Assert.That(Encoding.UTF8.GetString(Data(db, untouchedId))).IsEqualTo(NoWalls)
                    .Because("an intent stating no wall by its ends is left as it was");

                var history = db.Execute<string>("SELECT blob_hash FROM map_change_document WHERE kind = 'map_intent_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded intent is kept under its new bytes");
                var kept = db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'");
                await Assert.That(Gunzip(kept)).IsEqualTo(Cornered);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Board(DataConnection db, string intent)
    {
        db.Execute("INSERT INTO composed_board (composer_version, band, symmetry, cell, seed, score, wool_count, wools, "
                   + "hub, frontline, card_json, layout_json, intent_json, svg, created_at) VALUES ('v', 'b', 'rot_180', 5, "
                   + "1, 0, 2, ',i,l,', 'i', 'u', '{}', '{}', @intent, '', NOW())",
            new DataParameter("intent", intent));
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
