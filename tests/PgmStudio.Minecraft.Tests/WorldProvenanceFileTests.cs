using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
namespace PgmStudio.Minecraft.Tests;

/// <summary>The sidecar that lets a recorded provenance survive a round trip through disk — written
/// beside the region files a build writes, read back by anything reading that same directory later.</summary>
public sealed class WorldProvenanceFileTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"provenance-{Guid.NewGuid():N}");

    [Test]
    public async Task A_region_with_no_sidecar_reads_back_null()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        try { await Assert.That(WorldProvenanceFile.TryRead(dir)).IsNull(); }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_written_record_reads_back_every_claim()
    {
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.ClaimRect(-2, -2, 2, 2, ProvenanceLayer.Ground);
            written.ClaimRect(0, 0, 1, 1, ProvenanceLayer.Structure);   // overwrites the middle of the rect above
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir);
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.LayerAt(-2, -2)).IsEqualTo(ProvenanceLayer.Ground);
            await Assert.That(read.LayerAt(0, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(1, 1)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(2, 2)).IsEqualTo(ProvenanceLayer.Ground);
            await Assert.That(read.LayerAt(9, 9)).IsNull();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task Non_contiguous_runs_on_one_row_read_back_distinctly()
    {
        // Two separate Structure runs on the same Z with a Ground gap between them — the encoder has to
        // start a new run rather than bridging the gap.
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.ClaimRect(0, 0, 10, 0, ProvenanceLayer.Ground);
            written.ClaimRect(0, 0, 1, 0, ProvenanceLayer.Structure);
            written.ClaimRect(7, 0, 8, 0, ProvenanceLayer.Structure);
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir)!;
            await Assert.That(read.LayerAt(0, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(1, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(4, 0)).IsEqualTo(ProvenanceLayer.Ground);
            await Assert.That(read.LayerAt(7, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(8, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(10, 0)).IsEqualTo(ProvenanceLayer.Ground);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task Write_creates_the_region_directory_if_it_does_not_exist_yet()
    {
        var dir = TempDir();
        try
        {
            WorldProvenanceFile.Write(new WorldProvenance(), dir);
            await Assert.That(Directory.Exists(dir)).IsTrue();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_written_owner_reads_back_unchanged()
    {
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.ClaimRect(0, 0, 3, 0, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir)!;
            await Assert.That(read.OwnerAt(0, 0)).IsEqualTo(new StampId("house", "d-h1", 0));
            await Assert.That(read.OwnerAt(3, 0)).IsEqualTo(new StampId("house", "d-h1", 0));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task Two_owners_touching_on_one_row_read_back_as_two_distinct_runs()
    {
        // Adjacent columns, both Structure, different owners — the encoder has to break the run on the
        // owner change even though the layer never changes, or the two claims read back fused.
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.ClaimRect(0, 0, 4, 0, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
            written.ClaimRect(5, 0, 9, 0, ProvenanceLayer.Structure, new StampId("house", "d-h2", 0));
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir)!;
            await Assert.That(read.OwnerAt(4, 0)).IsEqualTo(new StampId("house", "d-h1", 0));
            await Assert.That(read.OwnerAt(5, 0)).IsEqualTo(new StampId("house", "d-h2", 0));
            await Assert.That(read.LayerAt(4, 0)).IsEqualTo(ProvenanceLayer.Structure);
            await Assert.That(read.LayerAt(5, 0)).IsEqualTo(ProvenanceLayer.Structure);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_column_with_no_owner_reads_back_with_no_owner()
    {
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.Claim(0, 0, ProvenanceLayer.Ground);
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir)!;
            await Assert.That(read.OwnerAt(0, 0)).IsEqualTo(null);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_sidecar_from_before_the_owner_table_falls_back_to_null_instead_of_throwing()
    {
        // An earlier shape: a bare JSON array of runs, with no {owners, runs} wrapper at all. Deserializing
        // it as the current object shape fails to convert rather than returning null on its own, so TryRead has
        // to catch that itself — the file's own doc comment promises exactly this fallback.
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "provenance.json"),
                """[{"Z":0,"MinX":0,"MaxX":5,"structure":true}]""");

            await Assert.That(WorldProvenanceFile.TryRead(dir)).IsNull();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_sidecar_missing_the_runs_field_falls_back_to_null_instead_of_throwing()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "provenance.json"), "{}");

            await Assert.That(WorldProvenanceFile.TryRead(dir)).IsNull();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task A_sidecar_that_is_not_json_at_all_falls_back_to_null_instead_of_throwing()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "provenance.json"), "not json");

            await Assert.That(WorldProvenanceFile.TryRead(dir)).IsNull();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public async Task Every_owner_string_is_written_once_regardless_of_how_many_runs_repeat_it()
    {
        // A id-table encoding: the same owner claimed on two separate rows costs the string once, not once
        // per run — the sidecar's own size discipline.
        var dir = TempDir();
        try
        {
            var written = new WorldProvenance();
            written.ClaimRect(0, 0, 2, 0, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
            written.ClaimRect(0, 5, 2, 5, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));   // same owner, a distant row
            WorldProvenanceFile.Write(written, dir);

            var json = File.ReadAllText(Path.Combine(dir, "provenance.json"));
            // The unit is written once into the id table however many runs carry it, which is the whole
            // point of the table — two distant rows of one building cost one entry, not two.
            var occurrences = System.Text.RegularExpressions.Regex.Matches(json, "d-h1").Count;
            await Assert.That(occurrences).IsEqualTo(1);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
