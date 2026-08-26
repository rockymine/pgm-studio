using PgmStudio.Minecraft.Anvil;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

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
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));
        var terrain = TerrainBuilder.Build(columns);
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
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));
        var terrain = TerrainBuilder.Build(columns);
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

    // ── diagonal wall stripes: the same cycle, sheared by height ─────────────────────────────────────────

    [Test]
    public async Task Diagonal_shifts_its_stripes_one_cell_along_for_every_course_up()
    {
        // Two runs two wide, so the cycle is 4. At slope 1 the read is arc + y, which walks the same
        // sequence one step further along for each course — a stripe leaning 45 degrees.
        var diagonal = new WallDiagonalMaterial([
            new WallStripe(new SolidMaterial(10), 2),
            new WallStripe(new SolidMaterial(11), 2),
        ]);
        for (var y = 0; y < 6; y++)
            for (var arc = 0; arc < 8; arc++)
            {
                var expected = ((arc + y) % 4) < 2 ? 10 : 11;
                var cell = new BucketContext(0, y, 0, TerrainBucket.Wall, 0, -1, arc);
                await Assert.That(diagonal.Resolve(cell)).IsEqualTo((expected, 0));
            }
    }

    [Test]
    public async Task Diagonal_at_slope_zero_is_a_plain_vertical_run()
    {
        IReadOnlyList<WallStripe> runs = [
            new WallStripe(new SolidMaterial(10), 2), new WallStripe(new SolidMaterial(11), 3)];
        var flat = new WallDiagonalMaterial(runs, 0);
        var run = new WallRunMaterial(runs);
        for (var y = 0; y < 4; y++)
            for (var arc = 0; arc < 10; arc++)
            {
                var cell = new BucketContext(0, y, 0, TerrainBucket.Wall, 0, -1, arc);
                await Assert.That(flat.Resolve(cell)).IsEqualTo(run.Resolve(cell));
            }
    }

    // ── checkerboard: squares in the face it paints ──────────────────────────────────────────────────────

    [Test]
    public async Task Checker_alternates_along_the_wall_and_up_it()
    {
        var board = new CheckerMaterial(1, new SolidMaterial(10), new SolidMaterial(11));
        for (var y = 0; y < 4; y++)
            for (var arc = 0; arc < 4; arc++)
            {
                var cell = new BucketContext(0, y, 0, TerrainBucket.Wall, 0, -1, arc);
                await Assert.That(board.Resolve(cell)).IsEqualTo(((arc + y) % 2 == 0 ? 10 : 11, 0));
            }
    }

    [Test]
    public async Task Checker_off_the_perimeter_lays_its_board_on_the_ground_instead()
    {
        // No arc, so the two axes are x and z: the board tiles the plane rather than a face, and height
        // stops mattering — the same square whatever course it is read at.
        var board = new CheckerMaterial(1, new SolidMaterial(10), new SolidMaterial(11));
        foreach (var y in new[] { 0, 5 })
            for (var x = 0; x < 4; x++)
                for (var z = 0; z < 4; z++)
                {
                    var cell = new BucketContext(x, y, z, TerrainBucket.Surface, 0);
                    await Assert.That(board.Resolve(cell)).IsEqualTo(((x + z) % 2 == 0 ? 10 : 11, 0));
                }
    }

    [Test]
    public async Task Checker_squares_keep_their_size_across_the_origin()
    {
        // A truncating divide folds at zero and puts two squares of a colour together there; the floor the
        // pattern uses does not. Walking x across the origin must alternate every two blocks throughout.
        var board = new CheckerMaterial(2, new SolidMaterial(10), new SolidMaterial(11));
        var seen = new List<int>();
        for (var x = -6; x < 6; x++)
            seen.Add(board.Resolve(new BucketContext(x, 0, 0, TerrainBucket.Surface, 0)).Id);

        for (var i = 0; i < seen.Count; i += 2)
            await Assert.That(seen[i]).IsEqualTo(seen[i + 1]);          // squares are two wide
        for (var i = 0; i + 2 < seen.Count; i += 2)
            await Assert.That(seen[i]).IsNotEqualTo(seen[i + 2]);       // and neighbouring squares differ
    }

    // ── the wall frame: top, bottom and corners inked, panel between ─────────────────────────────────────

    private static BucketContext Wall(int depthFromTop, int heightFromBottom, int turn) =>
        new(0, 0, 0, TerrainBucket.Wall, depthFromTop, -1, 0, heightFromBottom, turn);

    [Test]
    public async Task A_frame_inks_its_top_and_bottom_courses_and_fills_between()
    {
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11));
        await Assert.That(frame.Resolve(Wall(0, 9, 0))).IsEqualTo((10, 0));   // top course
        await Assert.That(frame.Resolve(Wall(9, 0, 0))).IsEqualTo((10, 0));   // bottom course
        await Assert.That(frame.Resolve(Wall(4, 4, 0))).IsEqualTo((11, 0));   // the panel between
    }

    [Test]
    public async Task A_frame_inks_a_turn_at_or_past_its_angle_and_no_shallower_one()
    {
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11), Angle: 45);
        await Assert.That(frame.Resolve(Wall(4, 4, 90))).IsEqualTo((10, 0));  // a right angle
        await Assert.That(frame.Resolve(Wall(4, 4, 45))).IsEqualTo((10, 0));  // exactly the threshold
        await Assert.That(frame.Resolve(Wall(4, 4, 44))).IsEqualTo((11, 0));  // a shallow bend is not a corner
        await Assert.That(frame.Resolve(Wall(4, 4, 17))).IsEqualTo((11, 0));  // a wide arc still less so
    }

    [Test]
    public async Task A_shape_with_no_corner_leaves_a_frame_reading_as_a_layer_stack()
    {
        // What a disc gives it: nothing ever reaches the threshold, so only the top and bottom are inked and
        // the pattern degenerates to horizontal bands rather than drawing something arbitrary.
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11), Angle: 45);
        for (var course = 1; course < 9; course++)
            await Assert.That(frame.Resolve(Wall(course, 9 - course, 18))).IsEqualTo((11, 0));
        await Assert.That(frame.Resolve(Wall(0, 9, 18))).IsEqualTo((10, 0));
        await Assert.That(frame.Resolve(Wall(9, 0, 18))).IsEqualTo((10, 0));
    }

    [Test]
    public async Task A_thicker_frame_claims_more_courses_at_both_ends()
    {
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11), Angle: 45, Thickness: 2);
        await Assert.That(frame.Resolve(Wall(1, 8, 0))).IsEqualTo((10, 0));
        await Assert.That(frame.Resolve(Wall(8, 1, 0))).IsEqualTo((10, 0));
        await Assert.That(frame.Resolve(Wall(2, 7, 0))).IsEqualTo((11, 0));
    }

    [Test]
    public async Task A_wall_too_short_to_hold_two_courses_is_all_frame()
    {
        // One course is both the top and the bottom of its own wall, which should read as a sill rather than
        // as a panel with no edge.
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11));
        await Assert.That(frame.Resolve(Wall(0, 0, 0))).IsEqualTo((10, 0));
    }

    // ── the turn reaching a material through the real profile ────────────────────────────────────────────

    private static TerrainProfile ProfileOf(IEnumerable<(int X, int Z)> footprint)
    {
        var columns = footprint.Select(cell => Seg(cell.X, cell.Z, 1, 9)).ToList();
        var terrain = TerrainBuilder.Build(columns);
        return new TerrainProfile(terrain.World, terrain.SurfaceTop);
    }

    [Test]
    public async Task A_rectangles_profile_carries_a_right_angle_at_its_corners_and_nothing_along_its_edges()
    {
        var footprint = new List<(int X, int Z)>();
        for (var x = 0; x < 16; x++)
            for (var z = 0; z < 12; z++) footprint.Add((x, z));

        var turn = ProfileOf(footprint).PaintableColumns().ToDictionary(p => p.Cell, p => p.Profile.PerimeterTurn);

        foreach (var corner in new[] { (0, 0), (15, 0), (0, 11), (15, 11) })
            await Assert.That(turn[corner]).IsEqualTo(90);
        await Assert.That(turn[(8, 0)]).IsEqualTo(0);       // mid-run of the long edge
        await Assert.That(turn[(0, 6)]).IsEqualTo(0);       // mid-run of the short edge
        await Assert.That(turn[(8, 6)]).IsEqualTo(0);       // interior, off the perimeter entirely
    }

    [Test]
    public async Task A_discs_profile_never_reaches_a_corner_so_a_frame_falls_back_to_its_courses()
    {
        const int radius = 14;
        var footprint = new List<(int X, int Z)>();
        for (var x = -radius; x <= radius; x++)
            for (var z = -radius; z <= radius; z++)
                if (x * x + z * z <= radius * radius) footprint.Add((x, z));

        var turn = ProfileOf(footprint).PaintableColumns().ToDictionary(p => p.Cell, p => p.Profile.PerimeterTurn);
        await Assert.That(turn.Values.Max()).IsLessThan(45);

        // So on this shape no cell of the wall's middle is ever inked — the frame is its top and bottom only.
        var frame = new WallFrameMaterial(new SolidMaterial(10), new SolidMaterial(11), Angle: 45);
        foreach (var bend in turn.Values.Distinct())
            await Assert.That(frame.Resolve(Wall(4, 4, bend))).IsEqualTo((11, 0));
    }

    // ── the log checkerboard ────────────────────────────────────────────────────────────────────────
    private const int Acacia = 162, Upright = 0, AlongX = 4, AlongZ = 8;

    [Test]
    public async Task A_log_checker_alternates_the_turn_of_one_log_rather_than_two_blocks()
    {
        // The whole point: one block id everywhere, and what changes square to square is its axis.
        var board = new LogCheckerMaterial(1, Acacia);
        for (var y = 0; y < 4; y++)
            for (var arc = 0; arc < 4; arc++)
            {
                var (id, data) = board.Resolve(
                    new BucketContext(0, y, 0, TerrainBucket.Wall, 0, -1, arc,
                        PerimeterRun: GridBoundary.RunAlongX));
                await Assert.That(id).IsEqualTo(Acacia);
                await Assert.That(data).IsEqualTo((arc + y) % 2 == 0 ? Upright : AlongX);
            }
    }

    [Test]
    [Arguments(GridBoundary.RunAlongX, AlongX)]
    [Arguments(GridBoundary.RunAlongZ, AlongZ)]
    public async Task A_log_on_its_side_lies_along_the_wall_so_its_bark_faces_out(int run, int expected)
    {
        // The rule read off alpine_mining_ii: on a z-facing wall every horizontal log is axis-x and never
        // axis-z. A log laid across the wall instead points a sawn end straight at the viewer.
        var board = new LogCheckerMaterial(1, Acacia);
        var odd = new BucketContext(0, 0, 0, TerrainBucket.Wall, 0, -1, PerimeterArc: 1, PerimeterRun: run);
        await Assert.That(board.Resolve(odd)).IsEqualTo((Acacia, expected));
    }

    [Test]
    public async Task A_log_checker_keeps_the_species_it_was_given_and_supplies_only_the_axis()
    {
        // Data is the wood in the low two bits; the two above it are the axis and belong to the pattern.
        const int DarkOak = 1;
        var board = new LogCheckerMaterial(1, Acacia, DarkOak);
        var even = new BucketContext(0, 0, 0, TerrainBucket.Wall, 0, -1, PerimeterArc: 0,
                                     PerimeterRun: GridBoundary.RunAlongX);
        var odd = even with { PerimeterArc = 1 };
        await Assert.That(board.Resolve(even)).IsEqualTo((Acacia, DarkOak | Upright));
        await Assert.That(board.Resolve(odd)).IsEqualTo((Acacia, DarkOak | AlongX));
    }

    [Test]
    public async Task A_log_at_a_corner_stands_up_whichever_square_it_lands_on()
    {
        // A corner is on two faces at right angles, and no log lying down shows bark to both. So it stands —
        // and it stands on the odd square too, where the board would otherwise have laid it down.
        var board = new LogCheckerMaterial(1, Acacia);
        foreach (var arc in new[] { 0, 1 })
        {
            var corner = new BucketContext(0, 0, 0, TerrainBucket.Wall, 0, -1, arc,
                                           PerimeterRun: GridBoundary.RunsBothWays);
            await Assert.That(board.Resolve(corner)).IsEqualTo((Acacia, Upright));
        }
    }

    [Test]
    public async Task A_log_checker_off_a_wall_lays_its_board_on_the_ground()
    {
        // No face to protect, so the squares read as bark against sawn end — what a log floor is.
        var board = new LogCheckerMaterial(1, Acacia);
        for (var x = 0; x < 4; x++)
            for (var z = 0; z < 4; z++)
            {
                var (id, data) = board.Resolve(new BucketContext(x, 0, z, TerrainBucket.Surface, 0));
                await Assert.That(id).IsEqualTo(Acacia);
                await Assert.That(data).IsEqualTo((x + z) % 2 == 0 ? Upright : AlongX);
            }
    }

    [Test]
    public async Task A_laid_log_follows_the_wall_and_stands_up_only_at_a_corner()
    {
        // The beam course: a log lying along the wall everywhere, so the sawn ends are buried in its
        // neighbours and only bark shows. At a corner there are faces on both axes and no lying log can show
        // bark to both, so it stands — the same answer the checkerboard gives.
        var beam = new LaidLogMaterial(Blocks.Log, 0);
        await Assert.That(beam.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0,
            PerimeterRun: GridBoundary.RunAlongX))).IsEqualTo((Blocks.Log, AlongX));
        await Assert.That(beam.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0,
            PerimeterRun: GridBoundary.RunAlongZ))).IsEqualTo((Blocks.Log, AlongZ));
        await Assert.That(beam.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0,
            PerimeterRun: GridBoundary.RunsBothWays))).IsEqualTo((Blocks.Log, Upright));

        // The species is the author's and the axis is the pattern's, as everywhere a block carries geometry.
        var spruce = new LaidLogMaterial(Blocks.Log, 1);
        await Assert.That(spruce.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0,
            PerimeterRun: GridBoundary.RunAlongX))).IsEqualTo((Blocks.Log, 1 | AlongX));
    }

    // ── the symmetry fold: a pattern samples the orbit's representative, not the cell ────────────────────

    /// <summary>A 12x12 board painted with a four-colour cell pattern on its surface, mirrored about z = 6 —
    /// so cell z pairs with cell 11 - z.</summary>
    private static (VoxelWorld World, IReadOnlyDictionary<(int X, int Z), int> Tops) Board()
    {
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 12; x++)
        for (var z = 0; z < 12; z++)
            columns.Add(Seg(x, z, 1, 9));
        var terrain = TerrainBuilder.Build(columns);
        return (terrain.World, terrain.SurfaceTop);
    }

    private static TerrainTheme Patterned() => TerrainTheme.Default with
    {
        Surface = TerrainTheme.Default.Surface with
        {
            Material = new CellMaterial(7, 3, 40, 2, [
                new SolidMaterial(Blocks.Wool, 0), new SolidMaterial(Blocks.Wool, 4),
                new SolidMaterial(Blocks.Wool, 5), new SolidMaterial(Blocks.Wool, 14)]),
        },
    };

    [Test]
    public async Task A_pattern_painted_through_the_fold_matches_across_the_mirror()
    {
        var (world, tops) = Board();
        var fold = OrbitScatter.CanonicalizerFor("mirror_z", 6, 6);
        TerrainPainter.Paint(world, tops, Patterned(), foldAt: fold);

        for (var x = 0; x < 12; x++)
        for (var z = 0; z < 6; z++)
            await Assert.That(world.GetBlock(x, 8, z)).IsEqualTo(world.GetBlock(x, 8, 11 - z));
    }

    [Test]
    public async Task The_same_pattern_painted_without_the_fold_does_not()
    {
        // The other half of the previous test: a pattern is a function of position, so left to the world
        // coordinate it falls where its noise falls and the two halves disagree.
        var (world, tops) = Board();
        TerrainPainter.Paint(world, tops, Patterned());

        var mismatched = 0;
        for (var x = 0; x < 12; x++)
        for (var z = 0; z < 6; z++)
            if (world.GetBlock(x, 8, z) != world.GetBlock(x, 8, 11 - z)) mismatched++;
        await Assert.That(mismatched).IsGreaterThan(0);
    }

    [Test]
    public async Task A_context_with_no_fold_samples_its_own_cell()
    {
        var plain = new BucketContext(3, 4, 5, TerrainBucket.Surface, 0);
        await Assert.That(plain.Sample).IsEqualTo((3, 5));

        var folded = plain with { Sample = (9, 1) };
        await Assert.That(folded.Sample).IsEqualTo((9, 1));
        await Assert.That((folded.X, folded.Z)).IsEqualTo((3, 5));   // the cell it paints is still its own
    }

    /// <summary>A ground-layer segment, for a test whose subject is the fill rather than the stack.</summary>
    private static ColumnSegment Seg(int x, int z, int floor, int top) => new(x, z, floor, top, "ground");
}
