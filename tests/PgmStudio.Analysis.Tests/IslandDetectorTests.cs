using PgmStudio.Analysis.Footprint;

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
    public async Task CleanedBaseFootprint_KeepsGroundCells_DropsFloatingOverVoid()
    {
        var cells = new List<(int, int, int)>();
        // Terrain floor: 10×10 at y=0.
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) cells.Add((x, z, 0));
        // Floating mass over void (no ground below): 5×5 at y=70, 8-adjacent to the floor.
        for (var x = 10; x < 15; x++) for (var z = 0; z < 5; z++) cells.Add((x, z, 70));

        var footprint = IslandDetector.CleanedBaseFootprint(cells);

        await Assert.That(footprint.Count).IsEqualTo(100);     // only the floor; no min-size filter
        await Assert.That(footprint.Contains((5, 5))).IsTrue();
        await Assert.That(footprint.Contains((12, 2))).IsFalse();   // floating-over-void can't pose as ground
    }

    // ── stair-aware detection ─────────────────────────────────────────────────────────────────────
    private static (int, int, int, IReadOnlyList<int>) Col(int x, int z, int baseY, params int[] surfaces)
        => (x, z, baseY, surfaces.Length > 0 ? surfaces : [baseY]);

    [Test]
    public async Task DetectStairAware_BridgesARaisedStructureToItsTerraceViaStairSurfaces()
    {
        // A terrace at base 6 (one row), then a staircase climbing 6→9→12→15→18 (each column carries the
        // terrace's base 6 plus its stair surface), then a structure floor at base 18. Height-aware on the
        // base alone would split the structure (Δbase 6→18); stair-aware follows the stair surfaces up.
        var cols = new List<(int, int, int, IReadOnlyList<int>)>();
        for (var x = 0; x < 12; x++) cols.Add(Col(x, 0, 6));                 // terrace
        cols.Add(Col(12, 0, 6, 6, 9)); cols.Add(Col(13, 0, 6, 6, 12));
        cols.Add(Col(14, 0, 6, 6, 15)); cols.Add(Col(15, 0, 6, 6, 18));      // staircase
        for (var x = 16; x < 28; x++) cols.Add(Col(x, 0, 18, 18));           // structure floor

        var islands = IslandDetector.DetectStairAware(cols);
        await Assert.That(islands.Count).IsEqualTo(1);                       // one connected landmass
    }

    [Test]
    public async Task DetectStairAware_KeepsAWallColumnConnectedToItsBaseNeighbour()
    {
        // A wall column solid base..base+6 (only standable top is 6) sitting next to ground at base 0 must
        // stay attached — connection is on the shared base level, not only the standable top (the green_gem
        // regression guard).
        var cols = new List<(int, int, int, IReadOnlyList<int>)>();
        for (var z = 0; z < 12; z++) cols.Add(Col(0, z, 0));                 // ground strip
        for (var z = 0; z < 12; z++) cols.Add(Col(1, z, 0, 6));              // wall on its edge (top at 6)

        var islands = IslandDetector.DetectStairAware(cols);
        await Assert.That(islands.Count).IsEqualTo(1);
        await Assert.That(islands[0].BlockCount).IsEqualTo(24);
    }

    [Test]
    public async Task DetectStairAware_StillSplitsAndPrunesADetachedFloat()
    {
        // Terrain at base 0; a float 8-adjacent in (x,z) whose surfaces sit far above with no stair between
        // → no surface within a step of the terrain → split off, then pruned as floating over void.
        var cols = new List<(int, int, int, IReadOnlyList<int>)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) cols.Add(Col(x, z, 0));
        for (var x = 10; x < 15; x++) for (var z = 0; z < 5; z++) cols.Add(Col(x, z, 70, 70));

        var islands = IslandDetector.DetectStairAware(cols);
        await Assert.That(islands.Count).IsEqualTo(1);
        await Assert.That(islands[0].BlockCount).IsEqualTo(100);            // only the terrain remains
    }

    [Test]
    public async Task DetectCleanedStairAware_FallsBackToY0WhenColumnsReadDegenerate()
    {
        var cols = new List<(int, int, int, IReadOnlyList<int>)>();
        for (var x = 0; x < 20; x++) for (var z = 0; z < 20; z++) cols.Add(Col(x, z, 0));  // one blob
        var y0 = new List<(int, int, int)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) y0.Add((x, z, 0));
        for (var x = 100; x < 110; x++) for (var z = 0; z < 10; z++) y0.Add((x, z, 0));

        await Assert.That(IslandDetector.DetectCleanedStairAware(cols).Count).IsEqualTo(1);
        await Assert.That(IslandDetector.DetectCleanedStairAware(cols, [y0]).Count).IsEqualTo(2);
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
