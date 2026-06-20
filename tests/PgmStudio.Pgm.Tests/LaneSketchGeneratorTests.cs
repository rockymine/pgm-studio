using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The lane-layout generator: a per-team 'H' of fixed-width lanes mirrored across the board mid, plus an
/// optional neutral mid island. Asserted through the rasterizer (generate → rasterize → cells), so the
/// layout the generator emits and the cells the finisher would persist are checked together. Island counts
/// use a local 8-connected flood fill (the Analysis IslandDetector isn't on this project's path).
/// </summary>
public sealed class LaneSketchGeneratorTests
{
    private static HashSet<(int, int)> Raster(SketchLayout l) => SketchRasterizer.Rasterize(l.ToJson()).ToHashSet();

    [Test]
    public async Task HLayout_yields_two_team_islands_plus_a_mid_island()
    {
        var cells = Raster(LaneSketchGenerator.HLayout().Layout);
        await Assert.That(ComponentSizes(cells).Count).IsEqualTo(3);
    }

    [Test]
    public async Task HLayout_stays_within_the_board_bounds()
    {
        var o = new LaneLayoutOptions();
        var cells = Raster(LaneSketchGenerator.HLayout(o).Layout);
        await Assert.That(cells.Min(c => c.Item1) >= 0).IsTrue();
        await Assert.That(cells.Max(c => c.Item1) < (int)o.Width).IsTrue();
        await Assert.That(cells.Min(c => c.Item2) >= 0).IsTrue();
        await Assert.That(cells.Max(c => c.Item2) < (int)o.Height).IsTrue();
    }

    [Test]
    public async Task Mirror_makes_the_two_team_islands_congruent()
    {
        var cells = Raster(LaneSketchGenerator.HLayout(new LaneLayoutOptions { MidIsland = false }).Layout);
        var sizes = ComponentSizes(cells);
        await Assert.That(sizes.Count).IsEqualTo(2);
        await Assert.That(sizes[0]).IsEqualTo(sizes[1]);   // exact mirror copy
    }

    [Test]
    public async Task Lane_width_matches_the_requested_width()
    {
        var o = new LaneLayoutOptions();
        var cells = Raster(LaneSketchGenerator.HLayout(o).Layout);
        // sample the wool leg (left third) at a z above the crossbar, where only the leg is present
        var z = (int)o.Margin + 6;
        var xs = cells.Where(c => c.Item2 == z && c.Item1 < o.Width / 3).Select(c => c.Item1).ToList();
        await Assert.That(xs.Max() - xs.Min() + 1).IsEqualTo((int)o.LaneWidth);
    }

    [Test]
    public async Task Objectives_are_one_wool_and_one_spawn_per_team_in_different_lanes()
    {
        var objectives = LaneSketchGenerator.HLayout().Objectives;
        await Assert.That(objectives.Count).IsEqualTo(4);
        var wools = objectives.Where(o => o.Kind == "wool").ToList();
        var spawns = objectives.Where(o => o.Kind == "spawn").ToList();
        await Assert.That(wools.Count).IsEqualTo(2);
        await Assert.That(spawns.Count).IsEqualTo(2);
        // a team's wool and spawn sit in different lanes (the spawn branches off, not in the wool lane)
        await Assert.That(wools.Single(w => w.Team == 0).X != spawns.Single(s => s.Team == 0).X).IsTrue();
    }

    [Test]
    public async Task Curved_crossbar_still_links_each_team_into_one_island()
    {
        var cells = Raster(LaneSketchGenerator.HLayout(new LaneLayoutOptions { CurvedCrossbar = true, MidIsland = false }).Layout);
        await Assert.That(ComponentSizes(cells).Count).IsEqualTo(2);
    }

    // 8-connected component sizes, descending.
    private static List<int> ComponentSizes(HashSet<(int, int)> cells)
    {
        var seen = new HashSet<(int, int)>();
        var sizes = new List<int>();
        foreach (var start in cells)
        {
            if (!seen.Add(start)) continue;
            var size = 0;
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var (x, z) = stack.Pop();
                size++;
                for (var dx = -1; dx <= 1; dx++)
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var nb = (x + dx, z + dz);
                        if (cells.Contains(nb) && seen.Add(nb)) stack.Push(nb);
                    }
            }
            sizes.Add(size);
        }
        sizes.Sort((a, b) => b.CompareTo(a));
        return sizes;
    }
}
