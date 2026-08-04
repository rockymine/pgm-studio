namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Terrain-paint pattern materials (docs/world-export/terrain-painting.md TP13): the three pattern specs at the
/// material seam — voronoi and noise (area), wall-runs (perimeter) — and the outer-perimeter arc the profile
/// computes for them. Patterns are pure and deterministic, so the assertions pin exact selection where the
/// input is known (wall-runs over an arc) and determinism / palette-membership where the field is noise.
/// </summary>
public sealed class TerrainPatternsTests
{
    private static BucketContext At(int x, int z, int arc = -1, int team = -1)
        => new(x, 0, z, TerrainBucket.Wall, 0, team, arc);

    // ── wall-runs: N materials, each its own width, wrapping the perimeter arc ────────────────────────────

    [Test]
    public async Task Wall_run_cycles_more_than_two_materials_by_width_around_the_loop()
    {
        // three runs — widths 2 / 3 / 1, total 6 — so arc 0,1→A · 2,3,4→B · 5→C · 6 wraps back to A.
        var run = new WallRunMaterial([
            new WallStripe(new SolidMaterial(10), 2),
            new WallStripe(new SolidMaterial(11), 3),
            new WallStripe(new SolidMaterial(12), 1),
        ]);
        int[] expected = [10, 10, 11, 11, 11, 12, 10, 10];   // arc 0..7
        for (var s = 0; s < expected.Length; s++)
            await Assert.That(run.Resolve(At(0, 0, arc: s))).IsEqualTo((expected[s], 0));
    }

    [Test]
    public async Task Wall_run_off_the_perimeter_takes_the_first_run()
    {
        var run = new WallRunMaterial([new WallStripe(new SolidMaterial(10), 2), new WallStripe(new SolidMaterial(11), 2)]);
        await Assert.That(run.Resolve(At(0, 0, arc: -1))).IsEqualTo((10, 0));
    }

    [Test]
    public async Task Wall_run_stripe_can_nest_a_team_tint()
    {
        var run = new WallRunMaterial([new WallStripe(new TeamTintedMaterial(Blocks.Wool, new SolidMaterial(Blocks.QuartzBlock)), 4)]);
        await Assert.That(run.Resolve(At(0, 0, arc: 1, team: 5))).IsEqualTo((Blocks.Wool, 5));       // team → wool 5
        await Assert.That(run.Resolve(At(0, 0, arc: 1, team: -1))).IsEqualTo((Blocks.QuartzBlock, 0)); // neutral fallback
    }

    // ── noise (value + fractal) → an N-stop ramp ─────────────────────────────────────────────────────────

    [Test]
    public async Task Noise_resolves_to_one_of_its_stops_and_is_deterministic()
    {
        var stops = new TerrainMaterial[] { new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3) };
        var noise = new NoiseMaterial(42u, 8, 4, stops);
        var ids = new HashSet<int>();
        for (var x = 0; x < 40; x++)
        for (var z = 0; z < 40; z++)
        {
            var (id, _) = noise.Resolve(At(x, z));
            await Assert.That(id is 1 or 2 or 3).IsTrue();
            await Assert.That(noise.Resolve(At(x, z))).IsEqualTo((id, 0));   // stable
            ids.Add(id);
        }
        await Assert.That(ids.Count).IsGreaterThan(1);                       // the field actually varies
    }

    [Test]
    public async Task Single_stop_noise_is_that_stop_everywhere()
    {
        var noise = new NoiseMaterial(1u, 8, 1, [new SolidMaterial(7)]);
        await Assert.That(noise.Resolve(At(3, 9))).IsEqualTo((7, 0));
        await Assert.That(noise.Resolve(At(31, 4))).IsEqualTo((7, 0));
    }

    // ── voronoi → an N-material palette ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Voronoi_resolves_to_one_of_its_palette_and_is_deterministic()
    {
        var pal = new TerrainMaterial[] { new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3), new SolidMaterial(4) };
        var vor = new VoronoiMaterial(7u, 5, pal);
        var ids = new HashSet<int>();
        for (var x = 0; x < 40; x++)
        for (var z = 0; z < 40; z++)
        {
            var (id, _) = vor.Resolve(At(x, z));
            await Assert.That(id is >= 1 and <= 4).IsTrue();
            await Assert.That(vor.Resolve(At(x, z))).IsEqualTo((id, 0));
            ids.Add(id);
        }
        await Assert.That(ids.Count).IsGreaterThan(1);
    }

    // ── the new geometry: the outer void-facing perimeter arc ────────────────────────────────────────────

    [Test]
    public async Task Perimeter_arc_rings_a_plateau_boundary_and_leaves_the_interior_unset()
    {
        // a solid 5×5 plateau: the 16 boundary cells get a contiguous arc 0..15, the inner 3×3 stay -1.
        var columns = new List<(int, int, int, int)>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add((x, z, 1, 9));
        var terrain = SketchTerrainBuilder.Build(columns);
        var profile = new TerrainProfile(terrain.World, terrain.SurfaceTop);
        var arc = profile.PaintableColumns().ToDictionary(p => p.Cell, p => p.Profile.PerimeterArc);

        var onPerimeter = arc.Where(kv => kv.Value >= 0).Select(kv => kv.Value).OrderBy(v => v).ToList();
        await Assert.That(onPerimeter.Count).IsEqualTo(16);                 // the boundary ring
        await Assert.That(onPerimeter).IsEquivalentTo(Enumerable.Range(0, 16).ToList());
        await Assert.That(arc[(2, 2)]).IsEqualTo(-1);                       // interior centre, off the wall
    }

    [Test]
    public async Task A_wall_run_paints_the_perimeter_and_leaves_the_interior_to_its_bucket()
    {
        // a plateau themed with a 2-material wall-run: edge risers cycle the runs; the interior surface is untouched.
        var columns = new List<(int, int, int, int)>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add((x, z, 1, 9));
        var terrain = SketchTerrainBuilder.Build(columns);
        var theme = TerrainTheme.Default with
        {
            Wall = new WallRunMaterial([new WallStripe(new SolidMaterial(Blocks.Wool, 0), 1), new WallStripe(new SolidMaterial(Blocks.Wool, 15), 1)]),
        };
        TerrainPainter.Paint(terrain.World, terrain.SurfaceTop, theme);

        // an edge column's riser is one of the two run blocks (white or black wool), never left as stone.
        var (wid, wdata) = terrain.World.GetBlock(0, 5, 2);
        await Assert.That(wid).IsEqualTo(Blocks.Wool);
        await Assert.That(wdata is 0 or 15).IsTrue();
        // the interior surface is still the default grass — the wall pattern touched only the wall bucket.
        await Assert.That(terrain.World.GetBlock(2, 8, 2)).IsEqualTo((Blocks.Grass, 0));
    }

    [Test]
    public async Task A_pattern_with_no_entries_paints_stone_rather_than_throwing()
    {
        // The theme JSON is a hand-editable leaf, so a pattern whose entry list is simply absent is a thing an
        // author can write — and a preview or an export that reads one must answer, not fall over.
        var context = new BucketContext(3, 4, 5, TerrainBucket.Surface, DepthFromTop: 0);
        await Assert.That(new VoronoiMaterial(1, 8, null!).Resolve(in context)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(new NoiseMaterial(1, 16, 3, null!).Resolve(in context)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(new WallRunMaterial(null!).Resolve(in context)).IsEqualTo((Blocks.Stone, 0));
    }
}
