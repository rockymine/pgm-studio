using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0082</c> states every room's doors in one shape: a spawn's door words become doors of that edge and no
/// point, a wool's entries become a door at each one's midpoint on the edge of its region's bounds it lies along,
/// an entry on no edge carries nothing, and an intent stating neither is left byte for byte as it was. Rolls the
/// schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class RoomDoorsMigrationTests
{
    private const string Doored = """
        {"spawns":[{"team":"red","protection":[{"minX":0,"minZ":0,"maxX":10,"maxZ":10}],"doors":["+x","-z"],"id":"spawn-red"}],
         "wools":[{"owner":"red","protection":[{"minX":20,"minZ":0,"maxX":30,"maxZ":10}],
                   "entries":[{"minX":30,"minZ":2,"maxX":30,"maxZ":6},{"minX":24,"minZ":5,"maxX":26,"maxZ":5}],"id":"wool-1"},
                  {"owner":"blue","protection":[{"minX":-30,"minZ":0,"maxX":-20,"maxZ":10}],"entries":[],"id":"wool-2"}]}
        """;

    private const string Untouched = """{"spawns":[{"team":"red","doors":[{"edge":"-z"}],"id":"spawn-red"}]}""";

    [Test]
    public async Task Every_door_word_and_entry_becomes_a_stated_door()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 81);
        try
        {
            long board, untouched;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                board = Intent(db, "board", Doored);
                untouched = Intent(db, "untouched", Untouched);
                recorded = Record(db, "board", Doored);
            }
            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 82);
            await using (var db = TestDb.Connect())
            {
                var intent = Read(db, board);
                await Assert.That(Same(intent["spawns"]![0]!["doors"], """[{"edge":"+x"},{"edge":"-z"}]""")).IsTrue()
                    .Because("a door word is a door centred on its wall");
                var wool = intent["wools"]![0]!.AsObject();
                await Assert.That(wool.ContainsKey("entries")).IsFalse();
                await Assert.That(Same(wool["doors"], """[{"edge":"+x","x":30,"z":4}]""")).IsTrue()
                    .Because("the entry on the +x edge is a door at its midpoint, and the one inside the region cut nothing");
                await Assert.That(intent["wools"]![1]!.AsObject().ContainsKey("doors")).IsFalse()
                    .Because("no entries state no doors");
                await Assert.That(intent["wools"]![1]!.AsObject().ContainsKey("entries")).IsFalse();

                await Assert.That(Encoding.UTF8.GetString(Data(db, untouched))).IsEqualTo(Untouched);

                var history = db.Execute<string>(
                    "SELECT blob_hash FROM map_change_document WHERE kind = 'map_intent_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded intent is stored under its new bytes");
                var kept = JsonNode.Parse(Gunzip(db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'")))!;
                await Assert.That(Same(kept["wools"]![0]!["doors"], """[{"edge":"+x","x":30,"z":4}]""")).IsTrue();
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    // The same JSON value, however its text escapes a character.
    private static bool Same(JsonNode? node, string expected) => JsonNode.DeepEquals(node, JsonNode.Parse(expected));

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
