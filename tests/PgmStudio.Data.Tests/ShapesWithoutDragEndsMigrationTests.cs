using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0090</c> takes the drag ends off every stored sketch shape — in a map's layout and the layouts its changes
/// recorded — keeping every other member of the shape, and leaving a layout whose shapes state neither as it was.
/// Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class ShapesWithoutDragEndsMigrationTests
{
    private const string Ended =
        """{"layers":[{"id":"ground","layout":{"shapes":[{"type":"rectangle","operation":"add","min_x":-41,"max_x":-1,"min_z":-28,"max_z":21,"from":[-41,-28],"to":[-2,20],"id":"s1"}],"groups":[]}}]}""";

    private const string Plain =
        """{"layers":[{"id":"ground","layout":{"shapes":[{"type":"rectangle","operation":"add","min_x":-41,"max_x":-1,"min_z":-28,"max_z":21,"id":"s1"}],"groups":[]}}]}""";

    private const string Untouched =
        """{"layers":[{"id":"ground","layout":{"shapes":[{"type":"circle","center_x":0,"center_z":0,"radius":5,"id":"c1"}]}}]}""";

    [Test]
    public async Task Every_stored_shape_drops_its_drag_ends()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 89);
        try
        {
            long artifactId, untouchedId;
            string recorded;
            await using (var db = TestDb.Connect())
            {
                artifactId = Artifact(db, "drawn", "sketch_layout_json", Ended);
                untouchedId = Artifact(db, "round", "sketch_layout_json", Untouched);
                recorded = Record(db, "drawn", "sketch_layout_json", Ended);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 90);

            await using (var db = TestDb.Connect())
            {
                await Assert.That(Encoding.UTF8.GetString(Data(db, artifactId))).IsEqualTo(Plain);
                await Assert.That(Encoding.UTF8.GetString(Data(db, untouchedId))).IsEqualTo(Untouched)
                    .Because("a layout whose shapes state no drag end is left as it was");

                var history = db.Execute<string>("SELECT blob_hash FROM map_change_document WHERE kind = 'sketch_layout_json'");
                await Assert.That(history).IsNotEqualTo(recorded).Because("the recorded layout is kept under its new bytes");
                var kept = db.Execute<byte[]>($"SELECT data FROM document_blob WHERE hash = '{history}'");
                await Assert.That(Gunzip(kept)).IsEqualTo(Plain);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
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
