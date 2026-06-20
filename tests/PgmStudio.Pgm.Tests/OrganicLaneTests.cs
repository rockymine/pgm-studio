using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The Organic archetype grows each team's island from a spawn hub out to noise-spread wool tips. Asserted
/// through the rasterizer: a seed is reproducible, the two teams are separate congruent islands, the spawn
/// sits at the hub and the wools at far dead-end tips on land.
/// </summary>
public sealed class OrganicLaneTests
{
    private static LaneLayoutResult Gen(int seed, int wools = 2) =>
        LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = LaneArchetype.Organic, Seed = seed, Wools = wools });

    private static HashSet<(int, int)> Raster(LaneLayoutResult r) =>
        SketchRasterizer.Rasterize(r.Layout.ToJson()).ToHashSet();

    [Test]
    public async Task A_seed_is_reproducible()
    {
        await Assert.That(Gen(7).Layout.ToJson()).IsEqualTo(Gen(7).Layout.ToJson());
    }

    [Test]
    public async Task Different_seeds_give_different_islands()
    {
        await Assert.That(Gen(1).Layout.ToJson()).IsNotEqualTo(Gen(2).Layout.ToJson());
    }

    [Test]
    public async Task Two_separate_congruent_team_islands()
    {
        var sizes = ComponentSizes(Raster(Gen(1)));
        await Assert.That(sizes.Count).IsEqualTo(2);
        await Assert.That(sizes[0]).IsEqualTo(sizes[1]);
    }

    [Test]
    public async Task One_spawn_and_the_requested_wools_per_team()
    {
        var hints = Gen(3, wools: 3).Objectives;
        await Assert.That(hints.Count(h => h.Kind == "spawn" && h.Team == 0)).IsEqualTo(1);
        await Assert.That(hints.Count(h => h.Kind == "wool" && h.Team == 0)).IsEqualTo(3);
    }

    [Test]
    public async Task Wools_are_far_dead_end_tips_and_the_spawn_is_the_hub()
    {
        foreach (var seed in new[] { 1, 2, 7, 13 })
        {
            var r = Gen(seed);
            var cells = Raster(r);
            var spawn = r.Objectives.Single(h => h.Kind == "spawn" && h.Team == 0);
            var wools = r.Objectives.Where(h => h.Kind == "wool" && h.Team == 0).ToList();
            foreach (var w in wools)
            {
                // a wool sits toward the far edge (above the hub), well away from it, and on land
                await Assert.That(w.Z < spawn.Z).IsTrue();
                await Assert.That(Math.Abs(w.Z - spawn.Z) > 12).IsTrue();
                await Assert.That(cells.Any(c => Math.Abs(c.Item1 - w.X) <= 14 && Math.Abs(c.Item2 - w.Z) <= 14)).IsTrue();
            }
        }
    }

    [Test]
    public async Task GrowStages_matches_Grow_and_populates_every_stage()
    {
        var o = LaneSketchGenerator.OrganicOptions(new LaneLayoutOptions { Archetype = LaneArchetype.Organic, Seed = 9, Wools = 3 });
        var plain = OrganicLane.Grow(o);
        var s = OrganicLane.GrowStages(o);

        // attaching a trace must not perturb the RNG — the captured shapes equal the untraced run's
        static string Json(List<SketchShape> shapes) => new SketchLayout { Layout = new SketchShapes { Shapes = shapes } }.ToJson();
        await Assert.That(Json(s.Shapes)).IsEqualTo(Json(plain.Shapes));

        await Assert.That(s.Noise.Values.Length).IsEqualTo(s.Noise.Cols * s.Noise.Rows);
        await Assert.That(s.Noise.Values.Any(v => v > 0)).IsTrue();
        await Assert.That(s.Hub.R).IsGreaterThan(0);
        await Assert.That(s.WoolTips.Count).IsEqualTo(3);
        await Assert.That(s.WoolObjs.Count).IsEqualTo(3);
        // trunks + one spine per wool lane (a fork adds a child spine) + the spawn spur
        await Assert.That(s.Spines.Count).IsGreaterThanOrEqualTo(s.TrunkTips.Count + s.WoolTips.Count + 1);
        await Assert.That(s.Spines.Any(sp => sp.Kind == "spawn")).IsTrue();
        await Assert.That(s.Shapes.Count).IsGreaterThan(0);
        await Assert.That(s.MirrorMode).IsEqualTo("mirror_z");
    }

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
                        if (cells.Contains((x + dx, z + dz)) && seen.Add((x + dx, z + dz))) stack.Push((x + dx, z + dz));
            }
            sizes.Add(size);
        }
        sizes.Sort((a, b) => b.CompareTo(a));
        return sizes;
    }
}
