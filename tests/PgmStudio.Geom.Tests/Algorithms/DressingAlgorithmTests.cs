using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// The pure leaves the dressing stage is grown from (G161). Each asserts the property the stage actually
/// depends on rather than a transcript of one output: that a scatter really is spaced, that asking on the
/// orbit's representative makes it symmetric, that a blob is a closed volume, that a grown tree has a
/// continuous trunk, and that a crown keeps its seams.
/// </summary>
public sealed class DressingAlgorithmTests
{
    // ── blue noise ─────────────────────────────────────────────────────────────────────────────────
    private static List<(int X, int Z)> Grid(int size)
        => [.. Enumerable.Range(0, size).SelectMany(z => Enumerable.Range(0, size).Select(x => (x, z)))];

    [Test]
    public async Task No_two_scatter_sites_land_within_the_spacing_radius()
    {
        // The point of a local-maximum test over a white-noise roll: sites cannot pile up.
        const int radius = 3;
        var sites = BlueNoise.Sites(Grid(60), seed: 7, radius).ToList();

        await Assert.That(sites.Count).IsGreaterThan(10);   // and it does place some
        foreach (var a in sites)
            foreach (var b in sites)
            {
                if (a == b) continue;
                var (dx, dz) = (a.X - b.X, a.Z - b.Z);
                await Assert.That(dx * dx + dz * dz).IsGreaterThan(radius * radius);
            }
    }

    [Test]
    public async Task A_wider_radius_scatters_fewer_sites()
    {
        var tight = BlueNoise.Sites(Grid(60), seed: 3, radius: 2).Count();
        var loose = BlueNoise.Sites(Grid(60), seed: 3, radius: 5).Count();
        await Assert.That(loose).IsLessThan(tight);
    }

    // ── the orbit representative ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task Every_image_of_an_orbit_resolves_to_the_same_representative()
    {
        var canonical = OrbitScatter.CanonicalizerFor("rot_180", 0, 0);
        for (var z = -20; z <= 20; z++)
        for (var x = -20; x <= 20; x++)
        {
            var (mx, mz) = Symmetry.Point(x + 0.5, z + 0.5, "rot_180", 0, 0, 1);
            var mirror = ((int)Math.Floor(mx), (int)Math.Floor(mz));
            await Assert.That(canonical(x, z)).IsEqualTo(canonical(mirror.Item1, mirror.Item2));
        }
    }

    [Test]
    public async Task Asking_on_the_representative_is_what_makes_a_scatter_symmetric()
    {
        // The correctness bug G162 names: a free scatter gives one team cover the other lacks. Count the sites
        // whose mirror image is bare — free scatter racks them up, the fanned pass is zero by construction.
        var cells = Enumerable.Range(-25, 50).SelectMany(z => Enumerable.Range(-25, 50).Select(x => (x, z))).ToList();
        var canonical = OrbitScatter.CanonicalizerFor("rot_180", 0, 0);

        int Unmirrored(Func<int, int, (int X, int Z)>? representative)
        {
            var sites = BlueNoise.Sites(cells, seed: 11, radius: 3, representative).ToHashSet();
            return sites.Count(site =>
            {
                var (mx, mz) = Symmetry.Point(site.X + 0.5, site.Z + 0.5, "rot_180", 0, 0, 1);
                return !sites.Contains(((int)Math.Floor(mx), (int)Math.Floor(mz)));
            });
        }

        await Assert.That(Unmirrored(null)).IsGreaterThan(0);
        await Assert.That(Unmirrored(canonical)).IsEqualTo(0);
    }

    [Test]
    public async Task Without_symmetry_a_cell_is_its_own_representative()
    {
        var canonical = OrbitScatter.CanonicalizerFor("none", 0, 0);
        await Assert.That(canonical(4, -9)).IsEqualTo((4, -9));
    }

    // ── blobs ──────────────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_blob_holds_its_centre_and_stops_at_its_radii()
    {
        var lobe = new BlobLobe(new Vec3(0, 0, 0), new Vec3(5, 3, 5), Erosion: 0);
        await Assert.That(Blob.Contains(lobe, new Vec3(0, 0, 0), 1)).IsTrue();
        await Assert.That(Blob.Contains(lobe, new Vec3(4.5, 0, 0), 1)).IsTrue();
        await Assert.That(Blob.Contains(lobe, new Vec3(6, 0, 0), 1)).IsFalse();
        await Assert.That(Blob.Contains(lobe, new Vec3(0, 4, 0), 1)).IsFalse();   // flatter than it is wide
    }

    [Test]
    public async Task Erosion_breaks_a_blobs_outline_without_moving_its_middle()
    {
        var clean = new BlobLobe(new Vec3(0, 0, 0), new Vec3(6, 6, 6), Erosion: 0);
        var eroded = clean with { Erosion = 0.6 };

        // The surface differs somewhere — that is the whole point of eroding it …
        var shell = Cells(-8, 8).Where(p => { var q = Quadric(p, 6); return q is > 0.8 and < 1.2; }).ToList();
        await Assert.That(shell.Any(p => Blob.Contains(clean, p, 5) != Blob.Contains(eroded, p, 5))).IsTrue();

        // … while the interior is untouched, so a rock never erodes into a shell or a hole.
        foreach (var point in Cells(-3, 3))
            if (Quadric(point, 6) < 0.4)
                await Assert.That(Blob.Contains(eroded, point, 5)).IsTrue();
    }

    [Test]
    public async Task A_blobs_bounds_contain_every_cell_it_fills()
    {
        List<BlobLobe> cairn =
        [
            new(new Vec3(0, 0, 0), new Vec3(5, 3.5, 5), 0.2),
            new(new Vec3(-1, 4, 0), new Vec3(3.5, 2.5, 3.5), 0.2),
        ];
        var (min, max) = Blob.Bounds(cairn);
        foreach (var point in Cells(-15, 15))
        {
            if (!Blob.Contains(cairn, point, 3)) continue;
            await Assert.That(point.X >= min.X && point.X <= max.X).IsTrue();
            await Assert.That(point.Y >= min.Y && point.Y <= max.Y).IsTrue();
            await Assert.That(point.Z >= min.Z && point.Z <= max.Z).IsTrue();
        }
    }

    private static IEnumerable<Vec3> Cells(int from, int to)
    {
        for (var y = from; y <= to; y++)
        for (var z = from; z <= to; z++)
        for (var x = from; x <= to; x++)
            yield return new Vec3(x, y, z);
    }

    private static double Quadric(Vec3 p, double r) => (p.X * p.X + p.Y * p.Y + p.Z * p.Z) / (r * r);

    // ── polylines ──────────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_band_is_every_cell_within_a_radius_of_the_line()
    {
        double[][] line = [[0, 0], [20, 0]];
        var band = Polyline.Band(line, radius: 2.5).ToHashSet();

        await Assert.That(band).Contains((10, 0));
        await Assert.That(band).Contains((10, 2));
        await Assert.That(band).DoesNotContain((10, 5));
        // The ends are capped by a disc, not cut square — a swept disc, not a rectangle.
        await Assert.That(band).Contains((-2, 0));
    }

    [Test]
    public async Task A_hit_reports_how_far_along_the_line_it_landed()
    {
        double[][] line = [[0, 0], [10, 0], [10, 10]];
        await Assert.That(Polyline.Nearest(line, 0, 0).Along).IsEqualTo(0).Within(0.001);
        await Assert.That(Polyline.Nearest(line, 10, 10).Along).IsEqualTo(1).Within(0.001);
        await Assert.That(Polyline.Nearest(line, 10, 0).Along).IsEqualTo(0.5).Within(0.001);
        await Assert.That(Polyline.Length(line)).IsEqualTo(20).Within(0.001);
    }

    [Test]
    public async Task A_per_cell_radius_is_how_a_band_tapers_or_wanders()
    {
        double[][] line = [[0, 0], [40, 0]];
        // Fat in the middle, pinched at both ends — the taper gate, as one function.
        var tapered = Polyline.Band(line, 4, (_, _, hit) => 4 * Math.Sin(hit.Along * Math.PI)).ToHashSet();
        await Assert.That(tapered).Contains((20, 3));
        await Assert.That(tapered).DoesNotContain((1, 3));
    }

    // ── the swept volume ───────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_swept_limb_is_continuous_along_its_whole_path()
    {
        List<Vec3> path = [.. Enumerable.Range(0, 20).Select(i => new Vec3(i * 0.5, i, 0))];
        var cells = SweptVolume.Sweep(path, 2.0, 0.6).ToHashSet();

        // Every course from the base to the tip carries wood — a limb with a gap in it is a floating branch.
        for (var y = 0; y < 19; y++)
            await Assert.That(cells.Any(cell => cell.Y == y)).IsTrue();
        await Assert.That(cells.Count(cell => cell.Y == 0)).IsGreaterThan(cells.Count(cell => cell.Y == 18));
    }

    [Test]
    public async Task A_limb_thinner_than_a_block_still_fills_one()
    {
        var cells = SweptVolume.Sweep([new Vec3(0, 0, 0)], 0.2, 0.2).ToList();
        await Assert.That(cells.Count).IsEqualTo(1);
    }

    // ── the grown tree ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_grown_tree_has_a_trunk_that_climbs_the_whole_way()
    {
        var tree = TreeSkeleton.Grow(new TreeShape(Height: 24, Leader: 0.8), seed: 5);
        var trunk = tree.Limbs.Single(limb => limb.Level == 0);

        await Assert.That(trunk.Path[0].Y).IsEqualTo(0).Within(0.001);
        // The trunk is one continuous spine, not a stub that forks and stops — the tell of a generated tree.
        await Assert.That(trunk.Path[^1].Y).IsGreaterThan(10);
        await Assert.That(trunk.EndRadius).IsLessThan(trunk.StartRadius);   // and it thins as it climbs
        await Assert.That(tree.Limbs.Count).IsGreaterThan(3);
        await Assert.That(tree.Tips.Count).IsGreaterThan(2);
    }

    [Test]
    public async Task A_seed_always_grows_the_same_tree()
    {
        var shape = new TreeShape(Height: 20);
        var first = TreeSkeleton.Grow(shape, seed: 12);
        var again = TreeSkeleton.Grow(shape, seed: 12);
        var other = TreeSkeleton.Grow(shape, seed: 13);

        await Assert.That(first.Limbs.Count).IsEqualTo(again.Limbs.Count);
        await Assert.That(first.Limbs[0].Path[^1]).IsEqualTo(again.Limbs[0].Path[^1]);
        await Assert.That(first.Limbs[0].Path[^1]).IsNotEqualTo(other.Limbs[0].Path[^1]);
    }

    [Test]
    public async Task A_smaller_tree_is_thinner_and_sparser_not_a_scaled_down_big_one()
    {
        var big = TreeSkeleton.Grow(new TreeShape(Height: 34), seed: 4);
        var sapling = TreeSkeleton.Grow(new TreeShape(Height: 8), seed: 4);

        await Assert.That(sapling.Limbs.Count).IsLessThan(big.Limbs.Count);
        await Assert.That(sapling.Limbs[0].StartRadius).IsLessThan(big.Limbs[0].StartRadius);
    }

    [Test]
    public async Task A_tree_grows_into_three_dimensions_not_one_plane()
    {
        // The prototype grew in a side elevation; a tree that stayed planar would read as scenery flat.
        var tree = TreeSkeleton.Grow(new TreeShape(Height: 26, Levels: 2), seed: 9);
        var spread = tree.Tips.Select(tip => tip.Position.Z).ToList();
        await Assert.That(spread.Max() - spread.Min()).IsGreaterThan(2);
    }

    // ── the crown ──────────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_leaf_cluster_sits_beyond_its_tip_never_back_down_the_branch()
    {
        var tips = new[] { new TreeTip(new Vec3(0, 10, 0), new Vec3(0, 1, 0)) };
        var cluster = TreeCrown.Clusters(tips, leafSize: 0.5, treeSize: 1, seed: 2)[0];
        await Assert.That(cluster.Center.Y).IsGreaterThan(10);
        await Assert.That(cluster.Radii.Y).IsLessThan(cluster.Radii.X);   // an oblate disc, not a ball
    }

    [Test]
    public async Task Neighbouring_clusters_keep_a_seam_of_air_between_them()
    {
        // Two overlapping clusters. Cells near the midline belong to neither, which is what keeps a crown
        // reading as one patch per branch instead of a single merged mass.
        List<LeafCluster> clusters =
        [
            new(new Vec3(-3, 0, 0), new Vec3(5, 3, 5), 0),
            new(new Vec3(3, 0, 0), new Vec3(5, 3, 5), 1),
        ];
        var seam = Enumerable.Range(-1, 3).Count(y => TreeCrown.OwnerAt(clusters, new Vec3(0, y, 0), 3) is null);
        await Assert.That(seam).IsEqualTo(3);

        // …while each cluster's own middle is solidly owned.
        await Assert.That(TreeCrown.OwnerAt(clusters, new Vec3(-3, 0, 0), 3)).IsEqualTo(0);
        await Assert.That(TreeCrown.OwnerAt(clusters, new Vec3(3, 0, 0), 3)).IsEqualTo(1);
    }

    [Test]
    public async Task A_cluster_is_dense_inside_the_airiness_is_the_seams()
    {
        List<LeafCluster> one = [new(new Vec3(0, 0, 0), new Vec3(6, 4, 6), 0)];
        var inside = Cells(-6, 6).Where(p => Quadric(p, 5) < 0.5).ToList();
        var filled = inside.Count(p => TreeCrown.OwnerAt(one, p, 7) is not null);
        await Assert.That(filled / (double)inside.Count).IsGreaterThan(0.85);
    }

    // ── the channel bed ────────────────────────────────────────────────────────────────────────────
    private static readonly double[][] Straight = [[4, 20], [36, 20]];

    [Test]
    public async Task A_channel_bed_is_a_bowl_deepest_on_the_centerline()
    {
        // The whole reason water needs its own carve rather than the path's flat repaint: the fill has to sit
        // in a U, so the deepest cut is on the line the author drew and it rises to a single block at the shore.
        var cells = WaterBed.Cells(Straight, radius: 4, depth: 4, ChannelForm.Canal, seed: 5).ToList();
        await Assert.That(cells).IsNotEmpty();

        // Somewhere along the run, the centerline is cut to the full depth and the band's outermost cells to one.
        var onLine = cells.Where(cell => cell.Z == 20).ToList();
        await Assert.That(onLine.Max(cell => cell.Depth)).IsEqualTo(4);
        await Assert.That(cells.Min(cell => cell.Depth)).IsEqualTo(1);

        // At a fixed x the bed only ever shallows towards either bank — a bowl, never a wall.
        foreach (var column in cells.GroupBy(cell => cell.X))
        {
            var ordered = column.OrderBy(cell => cell.Z).ToList();
            var peak = ordered.Select(cell => cell.Depth).ToList().IndexOf(ordered.Max(cell => cell.Depth));
            for (var i = 1; i <= peak; i++) await Assert.That(ordered[i].Depth).IsGreaterThanOrEqualTo(ordered[i - 1].Depth);
            for (var i = peak + 1; i < ordered.Count; i++) await Assert.That(ordered[i].Depth).IsLessThanOrEqualTo(ordered[i - 1].Depth);
        }
    }

    [Test]
    public async Task A_deeper_channel_cuts_a_deeper_centerline()
    {
        int Centre(double depth)
            => WaterBed.Cells(Straight, radius: 3, depth, ChannelForm.Canal, seed: 5).Where(cell => cell.Z == 20).Max(cell => cell.Depth);

        await Assert.That(Centre(6)).IsGreaterThan(Centre(2));
    }

    [Test]
    public async Task A_stream_shallows_towards_its_ends()
    {
        // A stream runs out into riffles, so its ends are cut shallower than its middle — where a canal of the
        // same depth is still on the bottom.
        var stream = WaterBed.Cells(Straight, radius: 3, depth: 5, ChannelForm.Stream, seed: 5).Where(cell => cell.Z == 20).ToList();
        var middle = stream.Where(cell => Math.Abs(cell.X - 20) <= 1).Max(cell => cell.Depth);
        var end = stream.Where(cell => cell.X <= 7).Max(cell => cell.Depth);
        await Assert.That(end).IsLessThan(middle);
    }

    [Test]
    public async Task The_same_channel_carves_the_same_bed()
    {
        var one = WaterBed.Cells(Straight, radius: 3, depth: 3, ChannelForm.Natural, seed: 5).ToList();
        var two = WaterBed.Cells(Straight, radius: 3, depth: 3, ChannelForm.Natural, seed: 5).ToList();
        await Assert.That(one).IsEquivalentTo(two);
    }

    [Test]
    public async Task The_beach_lies_outside_the_water_and_a_stream_spreads_a_wider_one_than_a_canal()
    {
        // The shore is the band past the water — none of its cells are ones the bed carves — and how wide it
        // runs is the channel's own read: a stream spreads into flats where a canal keeps a clean, narrow bank.
        var water = WaterBed.Cells(Straight, radius: 4, depth: 3, ChannelForm.Canal, seed: 5)
            .Select(cell => (cell.X, cell.Z)).ToHashSet();
        var canalShore = WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Canal, shoreWidth: 3, seed: 5).ToList();
        var streamShore = WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Stream, shoreWidth: 3, seed: 5).ToList();

        await Assert.That(canalShore).IsNotEmpty();
        await Assert.That(canalShore.Any(cell => water.Contains(cell))).IsFalse();   // never the bed's cells
        await Assert.That(streamShore.Count).IsGreaterThan(canalShore.Count);        // a stream's flats are wider
    }

    [Test]
    public async Task No_beach_width_is_no_beach()
    {
        await Assert.That(WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Natural, shoreWidth: 0, seed: 5)).IsEmpty();
    }
}
