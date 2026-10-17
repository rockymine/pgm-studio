using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0081</c> carries every room's stored footprint into a house of one wing whose corners are the
/// footprint's first and last cells, in each map's current intent and in every intent a change recorded, drops
/// a null footprint, and leaves an intent stating none byte for byte as it was. Rolls the schema back and
/// forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class RoomHousesMigrationTests
{
    private const string Housed = """
        {"spawns":[{"team":"red","protection":[{"minX":0,"minZ":0,"maxX":10,"maxZ":10}],
                    "footprint":{"minX":2,"minZ":2,"maxX":8,"maxZ":8},"id":"spawn-red"}],
         "wools":[{"owner":"red","protection":[],"footprint":null,"id":"wool-1"},
                  {"owner":"blue","footprint":{"minX":-6,"minZ":4,"maxX":0,"maxZ":10},"id":"wool-2"}]}
        """;

    private const string Untouched = """{"spawns":[{"team":"red","house":{"wings":[{"corners":[[2,2],[7,7]]}]},"id":"spawn-red"}]}""";

    [Test]
    public async Task Every_footprint_becomes_a_house_of_one_wing()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 80);
        try
        {
            long board, untouched;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                board = Intent(db, "board", Housed);
                untouched = Intent(db, "untouched", Untouched);
                recorded = Record(db, "board", Housed);
            }
            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 81);
            await using (var db = TestDb.Connect())
            {
                var intent = Read(db, board);
                await Assert.That(Corners(intent["spawns"]![0]!)).IsEqualTo((2, 2, 7, 7))
                    .Because("the footprint's first and last cells");
                await Assert.That(intent["spawns"]![0]!.AsObject().ContainsKey("footprint")).IsFalse();
                await Assert.That(intent["wools"]![0]!.AsObject().ContainsKey("house")).IsFalse()
                    .Because("a null footprint states no house");
                await Assert.That(intent["wools"]![0]!.AsObject().ContainsKey("footprint")).IsFalse();
                await Assert.That(Corners(intent["wools"]![1]!)).IsEqualTo((-6, 4, -1, 9));

                await Assert.That(Encoding.UTF8.GetString(Data(db, untouched))).IsEqualTo(Untouched);

                var history = db.Execute<string>(
                    "SELECT blob_hash FROM map_change_document WHERE kind = 'map_intent_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded intent is stored under its new bytes");
                var kept = JsonNode.Parse(Gunzip(db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'")))!;
                await Assert.That(Corners(kept["spawns"]![0]!)).IsEqualTo((2, 2, 7, 7));
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static (int, int, int, int) Corners(JsonNode room)
    {
        var corners = room["house"]!["wings"]![0]!["corners"]!;
        return (corners[0]![0]!.GetValue<int>(), corners[0]![1]!.GetValue<int>(),
                corners[1]![0]!.GetValue<int>(), corners[1]![1]!.GetValue<int>());
    }

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
