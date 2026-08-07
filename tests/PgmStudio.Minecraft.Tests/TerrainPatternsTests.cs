using PgmStudio.Geom.Algorithms;

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

    // ── voronoi → a ramp inward from the cell boundary ───────────────────────────────────────────────────

    [Test]
    public async Task A_voronoi_draws_its_first_band_as_one_connected_grid()
    {
        // The band that sits on the boundary IS the pattern's structure: cells are read off the line between
        // them, so a line that comes apart into fragments is not a grid, it is speckle. The old single-threshold
        // rim did exactly that at a one-block width.
        var vor = new VoronoiMaterial(7u, 10, [Band(50, 1), Band(1, 1)]);
        const int size = 80;
        var grid = new bool[size, size];
        for (var x = 0; x < size; x++)
        for (var z = 0; z < size; z++)
            grid[x, z] = vor.Resolve(At(x, z)).Id == 50;

        var (components, largest, total) = Components(grid, size);
        await Assert.That(total).IsGreaterThan(0);
        // One network, give or take the pieces the sampled window clips off at its own edges.
        await Assert.That((double)largest / total).IsGreaterThan(0.9);
        await Assert.That(components).IsLessThan(6);
    }

    [Test]
    public async Task A_voronoi_band_never_touches_one_two_steps_further_in()
    {
        // The ramp claim: bands are concentric, so only neighbours share a boundary. If the grid line ever
        // touched the middle the pattern would not be a ramp at all, it would be patches with an outline.
        var vor = new VoronoiMaterial(11u, 12, [Band(50, 1), Band(51, 2), Band(52, 1)]);
        const int size = 96;
        var id = new int[size, size];
        for (var x = 0; x < size; x++)
        for (var z = 0; z < size; z++)
            id[x, z] = vor.Resolve(At(x, z)).Id;

        var seen = new HashSet<(int, int)>();
        for (var x = 0; x < size - 1; x++)
        for (var z = 0; z < size - 1; z++)
        {
            foreach (var other in new[] { id[x + 1, z], id[x, z + 1] })
                if (other != id[x, z]) seen.Add((Math.Min(id[x, z], other), Math.Max(id[x, z], other)));
        }
        await Assert.That(seen).Contains((50, 51));
        await Assert.That(seen).Contains((51, 52));
        await Assert.That(seen.Contains((50, 52))).IsFalse();   // the grid line never reaches the middle
    }

    [Test]
    public async Task Shrinking_the_cells_starves_the_innermost_band()
    {
        // What gives cell size a meaning: the deepest band is what a roomy cell has and a cramped one mostly
        // does not. Not an absolute cut-off — an elongated small cell can still run deep along its long axis —
        // so the claim is measured as a share, which is what the difference actually looks like.
        static double MiddleShare(int cellSize)
        {
            var vor = new VoronoiMaterial(5u, cellSize, [Band(50, 1), Band(51, 2), Band(52, 1)]);
            int middle = 0, total = 0;
            for (var x = 0; x < 120; x++)
            for (var z = 0; z < 120; z++)
            {
                total++;
                if (vor.Resolve(At(x, z)).Id == 52) middle++;
            }
            return middle / (double)total;
        }

        var roomy = MiddleShare(20);
        var cramped = MiddleShare(4);
        await Assert.That(roomy).IsGreaterThan(0.3);        // a big cell is mostly its middle
        await Assert.That(cramped).IsLessThan(roomy / 3);   // a small one barely reaches it
    }

    [Test]
    public async Task A_one_band_voronoi_is_a_flat_fill_and_the_ramp_is_deterministic()
    {
        var flat = new VoronoiMaterial(2u, 7, [Band(9, 1)]);
        var ramp = new VoronoiMaterial(2u, 7, [Band(50, 1), Band(9, 1)]);
        for (var x = 0; x < 30; x++)
        for (var z = 0; z < 30; z++)
        {
            await Assert.That(flat.Resolve(At(x, z)).Id).IsEqualTo(9);
            await Assert.That(ramp.Resolve(At(x, z))).IsEqualTo(ramp.Resolve(At(x, z)));
        }
    }

    // ── cell → one material per warped region ────────────────────────────────────────────────────────────

    [Test]
    public async Task A_cell_pattern_fills_whole_regions_and_any_two_may_meet()
    {
        // The fabric read, and the thing that separates it from a voronoi: no band ordering, so every material
        // can border every other. Deterministic, like everything else here.
        var cell = new CellMaterial(3u, 8, Jitter: 50, Warp: 3,
            [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3)]);
        var seen = new HashSet<(int, int)>();
        var ids = new HashSet<int>();
        for (var x = 0; x < 90; x++)
        for (var z = 0; z < 90; z++)
        {
            var here = cell.Resolve(At(x, z)).Id;
            ids.Add(here);
            await Assert.That(cell.Resolve(At(x, z)).Id).IsEqualTo(here);
            foreach (var other in new[] { cell.Resolve(At(x + 1, z)).Id, cell.Resolve(At(x, z + 1)).Id })
                if (other != here) seen.Add((Math.Min(here, other), Math.Max(here, other)));
        }
        await Assert.That(ids.Count).IsEqualTo(3);
        await Assert.That(seen.Count).IsEqualTo(3);   // every pair of the three meets somewhere
    }

    [Test]
    public async Task Cell_warp_is_what_bends_the_boundaries_off_the_voronoi_diagram()
    {
        // Warp 0 is the plain diagram; any warp displaces the lookup, so the same seed paints a different map.
        var straight = new CellMaterial(3u, 8, Jitter: 50, Warp: 0, [new SolidMaterial(1), new SolidMaterial(2)]);
        var wobbly = new CellMaterial(3u, 8, Jitter: 50, Warp: 4, [new SolidMaterial(1), new SolidMaterial(2)]);
        var moved = 0;
        for (var x = 0; x < 60; x++)
        for (var z = 0; z < 60; z++)
            if (straight.Resolve(At(x, z)).Id != wobbly.Resolve(At(x, z)).Id) moved++;
        await Assert.That(moved).IsGreaterThan(0);
    }

    [Test]
    public async Task Zero_jitter_collapses_a_cell_pattern_onto_its_own_grid()
    {
        // The knob's far end, which is what says it is a knob: with no jitter every site sits in the middle of
        // its grid square, so the regions are the squares and every block of one takes the same material.
        var cell = new CellMaterial(4u, 8, Jitter: 0, Warp: 0,
            [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3)]);
        for (var gx = 0; gx < 4; gx++)
        for (var gz = 0; gz < 4; gz++)
        {
            var corner = cell.Resolve(At(gx * 8 + 1, gz * 8 + 1)).Id;
            for (var dx = 1; dx < 7; dx++)
            for (var dz = 1; dz < 7; dz++)
                await Assert.That(cell.Resolve(At(gx * 8 + dx, gz * 8 + dz)).Id).IsEqualTo(corner);
        }
    }

    // ── the field patterns: a ramp whose spread does not depend on the octave count ───────────────────────

    [Test]
    public async Task Adding_octaves_no_longer_starves_the_outer_stops()
    {
        // The defect this replaced: summing octaves and dividing by the amplitude total averages samples, so the
        // field crowded towards its middle and the first and last material an author named all but vanished —
        // measured at 1.0% each with five stops and three octaves. The share now barely moves with the octaves.
        TerrainMaterial[] stops = [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3),
                                   new SolidMaterial(4), new SolidMaterial(5)];
        foreach (var octaves in new[] { 1, 3, 5 })
        {
            var field = new NoiseMaterial(1u, 16, octaves, stops);
            var counts = new int[6];
            for (var x = 0; x < 120; x++)
            for (var z = 0; z < 120; z++)
                counts[field.Resolve(At(x, z)).Id]++;
            var total = 120 * 120;
            await Assert.That(counts[1] / (double)total).IsGreaterThan(0.03);   // the first stop is visible
            await Assert.That(counts[5] / (double)total).IsGreaterThan(0.03);   // and so is the last
            await Assert.That(counts[3] / (double)total).IsLessThan(0.6);       // no band swallows the field
        }
    }

    [Test]
    public async Task A_field_pattern_steps_from_one_stop_to_the_next_and_almost_never_skips()
    {
        // What makes a stop list a ramp rather than a set of patches: the field is continuous, so it steps from
        // one band to its neighbour. It is sampled at whole blocks, though, so where the field is steepest a
        // single block can cross two bands at once — that is discretisation, and it has to stay a rounding
        // error rather than become the pattern. A first stop bordering a third at any real rate would mean the
        // ramp was not banding anything.
        TerrainMaterial[] stops = [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3), new SolidMaterial(4)];
        foreach (TerrainMaterial pattern in new TerrainMaterial[]
                 { new NoiseMaterial(2u, 14, 3, stops), new TurbulenceMaterial(2u, 14, 3, stops), new ElectricMaterial(2u, 14, 3, stops) })
        {
            int adjacent = 0, skipped = 0;
            for (var x = 0; x < 100; x++)
            for (var z = 0; z < 100; z++)
            {
                var here = pattern.Resolve(At(x, z)).Id;
                foreach (var other in new[] { pattern.Resolve(At(x + 1, z)).Id, pattern.Resolve(At(x, z + 1)).Id })
                {
                    var step = Math.Abs(other - here);
                    if (step == 1) adjacent++;
                    else if (step > 1) skipped++;
                }
            }
            await Assert.That(adjacent).IsGreaterThan(0);
            await Assert.That(skipped / (double)(adjacent + skipped)).IsLessThan(0.05);
        }
    }

    [Test]
    public async Task The_three_field_patterns_bend_the_same_field_differently()
    {
        // Same seed, same scale, same stops — three different pictures, which is the only reason they are three
        // kinds. And a turbulence never equals an electric that happens to carry identical numbers.
        TerrainMaterial[] stops = [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3)];
        var plain = new NoiseMaterial(9u, 12, 3, stops);
        var billow = new TurbulenceMaterial(9u, 12, 3, stops);
        var ridge = new ElectricMaterial(9u, 12, 3, stops);
        int fromPlain = 0, fromBillow = 0;
        for (var x = 0; x < 80; x++)
        for (var z = 0; z < 80; z++)
        {
            if (plain.Resolve(At(x, z)).Id != billow.Resolve(At(x, z)).Id) fromPlain++;
            if (billow.Resolve(At(x, z)).Id != ridge.Resolve(At(x, z)).Id) fromBillow++;
        }
        await Assert.That(fromPlain).IsGreaterThan(500);
        await Assert.That(fromBillow).IsGreaterThan(500);
        await Assert.That(billow.Equals((object)ridge)).IsFalse();
    }

    // ── rise: the same patterns over a volume rather than a plane ─────────────────────────────────────────
    /// <summary>The defect the rise exists to fix. A pattern of the plane answers a whole column at once, so it
    /// decides the ground and leaves every wall face as vertical stripes — which is what a wall-run draws on
    /// purpose and what a pattern drew by accident. Given a vertical period, the same pattern varies with depth
    /// and a wall carries the fabric its surface does.</summary>
    [Test]
    public async Task A_flat_pattern_answers_a_whole_column_at_once_and_a_risen_one_does_not()
    {
        TerrainMaterial[] palette = [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3)];
        var flatCell = new CellMaterial(4u, 8, 60, 3, palette);
        var risenCell = flatCell with { Rise = 4 };
        var flatField = new NoiseMaterial(4u, 12, 3, palette);
        var risenField = flatField with { Rise = 4 };
        var flatVoronoi = new VoronoiMaterial(4u, 8, [Band(155, 1), Band(3, 2), Band(1, 1)]);
        var risenVoronoi = flatVoronoi with { Rise = 4 };

        // How many of a column's twenty courses differ from its top one.
        int Varies(TerrainMaterial material)
        {
            var varied = 0;
            for (var x = 0; x < 24; x++)
            for (var z = 0; z < 24; z++)
            {
                var top = material.Resolve(Deep(x, 20, z));
                for (var y = 0; y < 20; y++) if (material.Resolve(Deep(x, y, z)) != top) varied++;
            }
            return varied;
        }

        await Assert.That(Varies(flatCell)).IsEqualTo(0);
        await Assert.That(Varies(flatField)).IsEqualTo(0);
        await Assert.That(Varies(flatVoronoi)).IsEqualTo(0);
        await Assert.That(Varies(risenCell)).IsGreaterThan(1000);
        await Assert.That(Varies(risenField)).IsGreaterThan(1000);
        await Assert.That(Varies(risenVoronoi)).IsGreaterThan(1000);
    }

    /// <summary>A risen field is still a ramp. The band shares are what the field's normalisation exists to
    /// hold, and a volume octave is measurably narrower than a plane one — so reading a volume through the
    /// plane's numbers would crowd it towards the middle and starve the first and last stop, which is exactly
    /// the collapse the flat field was fixed for.</summary>
    [Test]
    public async Task A_risen_field_keeps_every_stop_it_was_given()
    {
        TerrainMaterial[] stops = [new SolidMaterial(1), new SolidMaterial(2), new SolidMaterial(3), new SolidMaterial(4)];
        foreach (var octaves in (int[])[1, 5])
        {
            var counts = new int[4];
            var field = new NoiseMaterial(7u, 16, octaves, stops, Rise: 5);
            for (var x = 0; x < 60; x++)
            for (var y = 0; y < 20; y++)
            for (var z = 0; z < 60; z++)
                counts[field.Resolve(Deep(x, y, z)).Id - 1]++;

            var total = (double)counts.Sum();
            foreach (var count in counts) await Assert.That(count / total).IsGreaterThan(0.04);
        }
    }

    /// <summary>A risen voronoi is still a voronoi: cut it at any height and the grid band is one connected
    /// network of lines, the property that makes the pattern read as cells rather than as speckle.</summary>
    [Test]
    public async Task A_risen_voronoi_still_draws_a_grid_on_the_slice_it_is_cut_at()
    {
        const int size = 90;
        var voronoi = new VoronoiMaterial(3u, 12, [Band(155, 1), Band(1, 1)], Rise: 6);
        foreach (var y in (int[])[0, 7])
        {
            var grid = new bool[size, size];
            for (var x = 0; x < size; x++)
            for (var z = 0; z < size; z++)
                grid[x, z] = voronoi.Resolve(Deep(x, y, z)).Id == 155;

            var (components, largest, total) = Components(grid, size);
            await Assert.That(total).IsGreaterThan(0);
            await Assert.That(largest / (double)total).IsGreaterThan(0.9);
            await Assert.That(components).IsLessThan(6);
        }
    }

    [Test]
    public async Task A_pattern_and_the_same_pattern_risen_are_two_materials()
    {
        // The rise changes what a theme paints, so it has to change what a theme *is* — otherwise a cached
        // preview or a deduplicated style would answer for the wrong one.
        var flat = new VoronoiMaterial(1u, 8, [Band(1, 1), Band(2, 1)]);
        await Assert.That(flat.Equals(flat with { Rise = 4 })).IsFalse();
        await Assert.That(new NoiseMaterial(1u, 8, 2, [new SolidMaterial(1)])
            .Equals(new NoiseMaterial(1u, 8, 2, [new SolidMaterial(1)], Rise: 3))).IsFalse();
    }

    /// <summary>A block at a real height, which is what every writer passes and what a risen pattern reads.</summary>
    private static BucketContext Deep(int x, int y, int z)
        => new(x, y, z, TerrainBucket.Fill, DepthFromTop: 0);

    private static VoronoiBand Band(int id, int depth) => new(new SolidMaterial(id), depth);

    /// <summary>Connected components of a boolean mask (8-connected, since a diagonal grid line is still one
    /// line): how many pieces, the largest, and the total set — what "is this a network?" asks.</summary>
    private static (int Components, int Largest, int Total) Components(bool[,] mask, int size)
    {
        var seen = new bool[size, size];
        int components = 0, largest = 0, total = 0;
        for (var x = 0; x < size; x++)
        for (var z = 0; z < size; z++)
        {
            if (mask[x, z]) total++;
            if (!mask[x, z] || seen[x, z]) continue;
            var stack = new Stack<(int X, int Z)>();
            stack.Push((x, z)); seen[x, z] = true; var count = 0;
            while (stack.Count > 0)
            {
                var (cx, cz) = stack.Pop(); count++;
                for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size || seen[nx, nz] || !mask[nx, nz]) continue;
                    seen[nx, nz] = true; stack.Push((nx, nz));
                }
            }
            components++; largest = Math.Max(largest, count);
        }
        return (components, largest, total);
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
