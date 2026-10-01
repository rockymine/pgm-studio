using System.IO.Compression;
using System.Text;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0052</c> carries every stored map's plan, layout and intent in as its first change: numbered at the
/// highest revision any of them, or the map's revision floor, had reached; each kept document set to that
/// number; the derived artifacts left counting their own. Rolls the schema back one migration and forward again,
/// so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class MapChangesMigrationTests
{
    [Test]
    public async Task A_stored_maps_documents_become_its_first_change_and_roll_back()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 51);
        try
        {
            long mapId;
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO map (slug, name, stage, created_at, updated_at, revision, artifact_revision_floor) "
                           + "VALUES ('weir', 'Weir', 'configure', NOW(), NOW(), 1, 9)");
                mapId = db.Execute<long>("SELECT id FROM map WHERE slug = 'weir'");
                foreach (var (kind, data, revision) in new[]
                         {
                             ("plan_json", """{"cell":9}""", 3L), ("sketch_layout_json", """{"layers":[]}""", 7L),
                             ("map_intent_json", """{"meta":{}}""", 2L), ("islands_json", "[]", 4L),
                         })
                    db.Execute("INSERT INTO map_artifact (map_id, kind, data, revision) VALUES (@map, @kind, @data, @revision)",
                        new DataParameter("map", mapId), new DataParameter("kind", kind),
                        new DataParameter("data", Encoding.UTF8.GetBytes(data)), new DataParameter("revision", revision));
                db.Execute("INSERT INTO map (slug, name, stage, created_at, updated_at, revision) VALUES ('bare', 'Bare', 'edit', NOW(), NOW(), 1)");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<long>("SELECT last_number FROM map_change_sequence WHERE map_slug = 'weir'"))
                    .IsEqualTo(9L).Because("the floor was above every document's revision");
                await Assert.That(db.Execute<long>("SELECT number FROM map_change WHERE map_slug = 'weir'")).IsEqualTo(9L);
                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM map_change_document")).IsEqualTo(3L);
                await Assert.That(Revision(db, mapId, "sketch_layout_json")).IsEqualTo(9L);
                await Assert.That(Revision(db, mapId, "plan_json")).IsEqualTo(9L);
                await Assert.That(Revision(db, mapId, "islands_json")).IsEqualTo(4L)
                    .Because("a derived artifact is not a kept document and keeps its own count");
                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM map_change WHERE map_slug = 'bare'"))
                    .IsEqualTo(0L).Because("a map holding no kept document has nothing to carry in");

                var kept = db.Execute<byte[]>(
                    "SELECT b.data FROM document_blob b JOIN map_change_document d ON d.blob_hash = b.hash "
                    + "WHERE d.kind = 'sketch_layout_json'");
                await Assert.That(Encoding.UTF8.GetString(Gunzip(kept))).IsEqualTo("""{"layers":[]}""");
            }

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 51);
            await using (var db = TestDb.Connect())
                await Assert.That(db.Execute<long>("SELECT artifact_revision_floor FROM map WHERE slug = 'weir'"))
                    .IsEqualTo(9L).Because("rolling back gives the floor the slug's last number");
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Revision(DataConnection db, long mapId, string kind) =>
        db.Execute<long>($"SELECT revision FROM map_artifact WHERE map_id = {mapId} AND kind = '{kind}'");

    private static byte[] Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var unpacked = new MemoryStream();
        gzip.CopyTo(unpacked);
        return unpacked.ToArray();
    }
}
