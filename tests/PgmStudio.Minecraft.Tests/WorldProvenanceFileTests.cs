namespace PgmStudio.Minecraft.Tests;

/// <summary>The sidecar that lets a recorded provenance survive a round trip through disk (B133) — written
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
}
