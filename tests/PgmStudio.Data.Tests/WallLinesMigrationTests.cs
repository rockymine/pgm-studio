using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0079</c> carries every stored approach wall box into the line down its middle and its defence chests
/// into markers one block outside the face it named, in each map's current intent and in every intent a change
/// recorded, and leaves an intent with no wall box byte for byte as it was. Rolls the schema back and forward,
/// so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class WallLinesMigrationTests
{
    private const string Walled = """
        {"structures":{"walls":[{"minX":0,"minZ":0,"maxX":2,"maxZ":8,"topY":20,"chestOnMinFace":true,
                                 "stamp":{"kind":"wall","unit":"0","image":0},"id":"wall-1"},
                                {"minX":30,"minZ":0,"maxX":44,"maxZ":2,"topY":20,"chestOnMinFace":false,"id":"wall-2"}]}}
        """;

    private const string Untouched = """{"structures":{"walls":[{"x1":1,"z1":0,"x2":1,"z2":8,"topY":20,"id":"wall-1"}]}}""";

    [Test]
    public async Task Every_wall_box_becomes_a_line_and_its_chests_markers()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 78);
        try
        {
            long board, untouched;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                board = Intent(db, "board", Walled);
                untouched = Intent(db, "untouched", Untouched);
                recorded = Record(db, "board", Walled);
            }
            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 79);
            await using (var db = TestDb.Connect())
            {
                var structures = Read(db, board)["structures"]!;
                var walls = (JsonArray)structures["walls"]!;
                await Assert.That(Line(walls[0]!)).IsEqualTo((1, 0, 1, 8)).Because("the line down a box thin along x");
                await Assert.That(Line(walls[1]!)).IsEqualTo((30, 1, 44, 1)).Because("the line down a box thin along z");
                await Assert.That(walls[0]!.AsObject().ContainsKey("minX")).IsFalse();

                var chests = ((JsonArray)structures["chests"]!).Select(chest =>
                    (chest!["x"]!.GetValue<int>(), chest["z"]!.GetValue<int>(), chest["facing"]!.GetValue<string>())).ToList();
                await Assert.That(chests).IsEquivalentTo(new[] { (-1, 4, "-x"), (34, 2, "+z"), (39, 2, "+z") })
                    .Because("one chest on a lane of 8, two on a lane of 14, one block out from the face named");
                await Assert.That(((JsonArray)structures["chests"]!)[0]!["stamp"]!["unit"]!.GetValue<string>()).IsEqualTo("0-0");

                await Assert.That(Encoding.UTF8.GetString(Data(db, untouched))).IsEqualTo(Untouched);

                var history = db.Execute<string>(
                    "SELECT blob_hash FROM map_change_document WHERE kind = 'map_intent_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded intent is stored under its new bytes");
                var kept = JsonNode.Parse(Gunzip(db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'")))!;
                await Assert.That(Line(kept["structures"]!["walls"]![0]!)).IsEqualTo((1, 0, 1, 8));
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static (int, int, int, int) Line(JsonNode wall) =>
        (wall["x1"]!.GetValue<int>(), wall["z1"]!.GetValue<int>(), wall["x2"]!.GetValue<int>(), wall["z2"]!.GetValue<int>());

    private static long Intent(DataConnection db, string slug, string json)
    {
        db.Execute("INSERT INTO map (slug) VALUES (@slug)", new DataParameter("slug", slug));
        var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'map_intent_json', @data)",
            new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    // A change on the slug that wrote the intent, kept the way the change log keeps it.
    private static string Record(DataConnection db, string slug, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        db.Execute("INSERT INTO map_change (map_slug, number, created_at) VALUES (@slug, 1, UTC_TIMESTAMP())",
            new DataParameter("slug", slug));
        var change = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
            new DataParameter("hash", hash), new DataParameter("data", Gzip(bytes)), new DataParameter("length", (long)bytes.Length));
        db.Execute("INSERT INTO map_change_document (change_id, kind, blob_hash) VALUES (@change, 'map_intent_json', @hash)",
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
