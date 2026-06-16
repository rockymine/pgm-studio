using PgmStudio.Analysis;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Synthetic island-detection checks. Corpus-level parity vs Python's islands.json (count +
/// block_count + bounds) is covered by the RoundTrip <c>--islands</c> harness (10/10 maps).
/// </summary>
public class IslandDetectorTests
{
    [Test]
    public async Task Detect_SplitsComponents_FiltersTiny_SortsByBlockCount()
    {
        // Blob A: 5×5 = 25 cells at origin. Blob B: 4×4 = 16 cells far away. Speck: 3 cells (< min 10).
        var coords = new List<(int, int)>();
        for (var x = 0; x < 5; x++) for (var z = 0; z < 5; z++) coords.Add((x, z));
        for (var x = 100; x < 104; x++) for (var z = 100; z < 104; z++) coords.Add((x, z));
        coords.Add((200, 200)); coords.Add((201, 200)); coords.Add((202, 200));

        var islands = IslandDetector.Detect(coords, minIslandSize: 10);

        await Assert.That(islands.Count).IsEqualTo(2);                 // speck filtered
        await Assert.That(islands[0].BlockCount).IsEqualTo(25);        // sorted desc
        await Assert.That(islands[1].BlockCount).IsEqualTo(16);
        await Assert.That(islands[0].Id).IsEqualTo(1);
        await Assert.That(islands[1].Id).IsEqualTo(2);
        await Assert.That(islands[0].Bounds).IsEqualTo((0, 0, 5, 5));  // max is exclusive (+1)
        await Assert.That(islands[1].Bounds).IsEqualTo((100, 100, 104, 104));
        // Each cell is a unit square → polygon area equals the block count.
        await Assert.That(islands[0].Polygon.Area).IsEqualTo(25.0);
    }

    [Test]
    public async Task DetectHeightAware_SplitsFloatingMassOnStarkYJump_AndPrunesIt()
    {
        var cells = new List<(int, int, int)>();
        // Terrain: 10×10 at y=0 (100 cells).
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) cells.Add((x, z, 0));
        // Floating build: 5×5 at y=70, footprint 8-adjacent to the terrain (x10..14) — plain 2D would
        // MERGE the two into one 125-cell island; height-aware must split + prune the floating mass.
        for (var x = 10; x < 15; x++) for (var z = 0; z < 5; z++) cells.Add((x, z, 70));

        // Sanity: the (X,Z)-only detector merges them into a single island.
        await Assert.That(IslandDetector.Detect(cells.Select(c => (c.Item1, c.Item2))).Count).IsEqualTo(1);

        var islands = IslandDetector.DetectHeightAware(cells);
        await Assert.That(islands.Count).IsEqualTo(1);            // floating mass split off + pruned
        await Assert.That(islands[0].BlockCount).IsEqualTo(100);  // only the terrain remains
    }

    [Test]
    public async Task DetectHeightAware_KeepsGentleSlopeConnected()
    {
        // A 4-wide ramp climbing 1 block per z-step (|ΔY| = 1 ≤ tol) stays one connected island.
        var cells = new List<(int, int, int)>();
        for (var z = 0; z < 15; z++) for (var x = 0; x < 4; x++) cells.Add((x, z, z));

        var islands = IslandDetector.DetectHeightAware(cells);
        await Assert.That(islands.Count).IsEqualTo(1);
        await Assert.That(islands[0].BlockCount).IsEqualTo(60);
    }

    [Test]
    public async Task DetectCleaned_FallsBackToY0WhenBaseReadsDegenerate()
    {
        // Base bridges everything into one blob (1 island) — degenerate.
        var baseCells = new List<(int, int, int)>();
        for (var x = 0; x < 20; x++) for (var z = 0; z < 20; z++) baseCells.Add((x, z, 0));
        // y0 fallback separates into two distinct islands.
        var y0 = new List<(int, int, int)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) y0.Add((x, z, 0));
        for (var x = 100; x < 110; x++) for (var z = 0; z < 10; z++) y0.Add((x, z, 0));

        await Assert.That(IslandDetector.DetectCleaned(baseCells).Count).IsEqualTo(1);       // no fallback
        await Assert.That(IslandDetector.DetectCleaned(baseCells, [y0]).Count).IsEqualTo(2); // fell back to y0
    }

    [Test]
    public async Task SerializeJson_EmitsGeoJsonPolygons()
    {
        var coords = new List<(int, int)>();
        for (var x = 0; x < 4; x++) for (var z = 0; z < 4; z++) coords.Add((x, z));
        var json = IslandDetector.SerializeJson(IslandDetector.Detect(coords, minIslandSize: 1));

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var first = doc.RootElement[0];
        await Assert.That(first.GetProperty("block_count").GetInt32()).IsEqualTo(16);
        await Assert.That(first.GetProperty("polygon").GetProperty("type").GetString()).IsEqualTo("Polygon");
        var ring = first.GetProperty("polygon").GetProperty("coordinates")[0];
        // A closed exterior ring: ≥ 4 points and the last repeats the first.
        await Assert.That(ring.GetArrayLength() >= 4).IsTrue();
        var p0 = ring[0]; var pn = ring[ring.GetArrayLength() - 1];
        await Assert.That(p0[0].GetDouble()).IsEqualTo(pn[0].GetDouble());
        await Assert.That(p0[1].GetDouble()).IsEqualTo(pn[1].GetDouble());
    }
}
