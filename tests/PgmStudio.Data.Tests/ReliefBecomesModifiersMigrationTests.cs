using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0086</c> turns a stored relief into modifiers: area and point marks into Levels, pushes into Hills, grain
/// into a Roughness over the board, the rest dropped; the <c>relief</c> key is gone from the current layout, the
/// current refinement and every recorded one, and the map's history gains a change saying what happened. Rolls the
/// schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class ReliefBecomesModifiersMigrationTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180"},
         "layers":[{"id":"ground","base_y":0,"layout":{"shapes":[{"id":"a","relief_scope":"exclude"},{"id":"b","relief_scope":"follow","floor":3},{"id":"c","relief_scope":"hold"},{"id":"d"}],"groups":[
           {"id":"isl_1","mirrors":false,"shapeIds":["a"]},{"id":"isl_2","mirrors":true,"shapeIds":["b"]}]}}],
         "relief":{
           "isl_1":{"base":10,"reach":8,"step":2,"landform":"hills",
             "grain":{"amplitude":1.5,"scale":6,"seed":7},
             "marks":[
               {"id":"top","kind":"area","h":[12,14],"ring":[[0,0],[10,0],[10,10]]},
               {"kind":"point","at":[4,5],"r":1,"h":9},
               {"id":"ridge","kind":"line","points":[[0,0],[5,5]],"h":[3,4]},
               {"kind":"scarp","points":[[0,0],[5,5]],"high":9,"low":3},
               {"kind":"rim","h":5,"depth":2},
               {"id":"loose","kind":"area","h":4}],
             "pushes":[
               {"id":"bump","ring":[[1,1],[6,1],[6,6]],"amount":5,"amounts":[4,6,8],"falloff":9,"seed":3},
               {"ring":[[2,2],[8,2],[8,8]],"amount":-2}]},
           "isl_2":{"marks":[{"id":"top","kind":"point","at":[40,40],"r":6,"h":20}]}},
         "rooms":{"spawn":{"relief_scope":"exclude","floor":5}},
         "modifiers":[{"id":"isl_2-top","op":"noise","mask":{"areas":[{"kind":"board"}],"mirror":true}}]}
        """;

    private const string Refinement = """
        {"themeById":{"a":"grass"},"shapePropsById":{"a":{"relief_scope":"follow"}},"relief":{"*":{"marks":[{"id":"mesa","kind":"area","h":8,"ring":[[0,0],[4,0],[4,4]]}]}}}
        """;

    private const string Plain = """{"layers":[],"modifiers":[{"id":"hill","op":"mound","mask":{"areas":[],"mirror":true}}]}""";

    private const string Bare = """{"layers":[],"relief":{"isl_1":{"base":4,"marks":[],"pushes":[]}}}""";

    [Test]
    public async Task A_relief_becomes_modifiers_in_the_layout_the_refinement_and_the_history()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 85);
        try
        {
            long board, source, plain, bare;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO map (slug) VALUES ('board')");
                var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
                board = Artifact(db, map, "sketch_layout_json", Layout);
                source = Artifact(db, map, "refinement_json", Refinement);
                db.Execute("INSERT INTO map (slug) VALUES ('plain')");
                var other = db.Execute<long>("SELECT LAST_INSERT_ID()");
                plain = Artifact(db, other, "sketch_layout_json", Plain);
                db.Execute("INSERT INTO map (slug) VALUES ('bare')");
                bare = Artifact(db, db.Execute<long>("SELECT LAST_INSERT_ID()"), "sketch_layout_json", Bare);
                recorded = Record(db, "board", Layout);
            }
            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 86);
            await using (var db = TestDb.Connect())
            {
                var layout = Read(db, board);
                await Assert.That(layout.ContainsKey("relief")).IsFalse();
                var modifiers = ((JsonArray)layout["modifiers"]!).OfType<JsonObject>().ToList();
                await Assert.That(modifiers.Select(modifier => (string)modifier["id"]!)).IsEquivalentTo(
                    ["isl_2-top", "isl_1-top", "isl_1-1", "isl_1-bump", "isl_1-1-2", "isl_1-grain", "isl_2-top-2"]);

                var area = modifiers.Single(modifier => (string)modifier["id"]! == "isl_1-top");
                await Assert.That(area["op"]!.GetValue<string>()).IsEqualTo("level");
                await Assert.That(area["top"]!.GetValue<double>()).IsEqualTo(12.0);
                await Assert.That(area["mask"]!["mirror"]!.GetValue<bool>()).IsFalse()
                    .Because("the group does not copy onto its images");
                await Assert.That(JsonNode.DeepEquals(area["mask"]!["areas"],
                    JsonNode.Parse("""[{"kind":"polygon","points":[[0,0],[10,0],[10,10]]}]"""))).IsTrue();

                var point = modifiers.Single(modifier => (string)modifier["id"]! == "isl_1-1");
                await Assert.That(JsonNode.DeepEquals(point["mask"]!["areas"],
                    JsonNode.Parse("""[{"kind":"ellipse","at":[4,5],"rx":2,"rz":2}]"""))).IsTrue()
                    .Because("a radius under two is two");
                await Assert.That(point["top"]!.GetValue<double>()).IsEqualTo(9.0);

                var mound = modifiers.Single(modifier => (string)modifier["id"]! == "isl_1-bump");
                await Assert.That(mound["op"]!.GetValue<string>()).IsEqualTo("mound");
                await Assert.That(mound["rise"]!.GetValue<double>()).IsEqualTo(6.0).Because("the mean of the amounts");
                await Assert.That(mound["soft"]!.GetValue<double>()).IsEqualTo(0.35);
                await Assert.That(mound["power"]!.GetValue<double>()).IsEqualTo(4.0);
                await Assert.That(mound["seed"]!.GetValue<int>()).IsEqualTo(3);

                var unnamed = modifiers.Single(modifier => (string)modifier["id"]! == "isl_1-1-2");
                await Assert.That(unnamed["rise"]!.GetValue<double>()).IsEqualTo(-2.0);

                var grain = modifiers.Single(modifier => (string)modifier["id"]! == "isl_1-grain");
                await Assert.That(JsonNode.DeepEquals(grain["sizes"],
                    JsonNode.Parse("""[{"cell":6,"octaves":2,"rise":1.5}]"""))).IsTrue();
                await Assert.That(grain["seed"]!.GetValue<int>()).IsEqualTo(7);
                await Assert.That(JsonNode.DeepEquals(grain["mask"]!["areas"], JsonNode.Parse("""[{"kind":"board"}]"""))).IsTrue();

                var onFar = modifiers.Single(modifier => (string)modifier["id"]! == "isl_2-top-2");
                await Assert.That(onFar["mask"]!["mirror"]!.GetValue<bool>()).IsTrue();
                await Assert.That(onFar["mask"]!["areas"]![0]!["rx"]!.GetValue<double>()).IsEqualTo(6.0);

                var scopes = ((JsonArray)layout["layers"]![0]!["layout"]!["shapes"]!).OfType<JsonObject>().ToList();
                await Assert.That(scopes[0]["relief_scope"]!.GetValue<string>()).IsEqualTo("hold").Because("exclude is hold");
                await Assert.That(scopes[1].ContainsKey("relief_scope")).IsFalse().Because("follow is the absence");
                await Assert.That(scopes[1]["floor"]!.GetValue<int>()).IsEqualTo(3);
                await Assert.That(scopes[2]["relief_scope"]!.GetValue<string>()).IsEqualTo("hold");
                await Assert.That(scopes[3].ContainsKey("relief_scope")).IsFalse();
                await Assert.That(layout["rooms"]!["spawn"]!["relief_scope"]!.GetValue<string>()).IsEqualTo("hold");

                var refinement = Read(db, source);
                await Assert.That(refinement["shapePropsById"]!["a"]!.AsObject().ContainsKey("relief_scope")).IsFalse();
                await Assert.That(refinement.ContainsKey("relief")).IsFalse();
                await Assert.That(refinement["themeById"]!["a"]!.GetValue<string>()).IsEqualTo("grass");
                await Assert.That(((JsonArray)refinement["modifiers"]!)[0]!["id"]!.GetValue<string>()).IsEqualTo("*-mesa");

                await Assert.That(Encoding.UTF8.GetString(Data(db, plain))).IsEqualTo(Plain);
                var emptied = Read(db, bare);
                await Assert.That(emptied.ContainsKey("relief")).IsFalse();
                await Assert.That(emptied.ContainsKey("modifiers")).IsFalse();

                var history = db.Execute<string>(
                    "SELECT blob_hash FROM map_change_document d JOIN map_change c ON c.id = d.change_id "
                    + "WHERE c.map_slug = 'board' AND c.number = 1 AND d.kind = 'sketch_layout_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded layout is stored under its new bytes");
                var kept = JsonNode.Parse(Gunzip(db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'")))!;
                await Assert.That(kept["relief"]).IsNull();
                await Assert.That(((JsonArray)kept["modifiers"]!).Count).IsEqualTo(7);

                var changes = db.Execute<long>("SELECT COUNT(*) FROM map_change WHERE map_slug = 'board'");
                await Assert.That(changes).IsEqualTo(2L).Because("the migration records one change of its own for the map");
                var note = db.Execute<string>("SELECT note FROM map_change WHERE map_slug = 'board' AND number = 2");
                await Assert.That(note).Contains("4 Level").And.Contains("2 Hill").And.Contains("1 Roughness")
                    .And.Contains("1 line").And.Contains("1 scarp").And.Contains("1 rim").And.Contains("1 unreadable");
                var revision = db.Execute<long>($"SELECT revision FROM map_artifact WHERE id = {board}");
                await Assert.That(revision).IsEqualTo(2L).Because("a document's revision is the change that last wrote it");
                var kinds = db.Execute<long>(
                    "SELECT COUNT(*) FROM map_change_document d JOIN map_change c ON c.id = d.change_id "
                    + "WHERE c.map_slug = 'board' AND c.number = 2");
                await Assert.That(kinds).IsEqualTo(2L);

                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM map_change WHERE map_slug = 'plain'"))
                    .IsEqualTo(0L).Because("a layout stating no relief gets no change");
                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM map_change WHERE map_slug = 'bare'"))
                    .IsEqualTo(0L).Because("a relief stating nothing converts to nothing worth a note");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Artifact(DataConnection db, long map, string kind, string json)
    {
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, @kind, @data)",
            new DataParameter("map", map), new DataParameter("kind", kind), new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    // A change on the slug that wrote the layout, kept the way the change log keeps it.
    private static string Record(DataConnection db, string slug, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        db.Execute("INSERT INTO map_change_sequence (map_slug, last_number) VALUES (@slug, 1)", new DataParameter("slug", slug));
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
