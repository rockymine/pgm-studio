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

    [Test]
    public async Task A_sweep_sample_always_fills_at_least_the_block_it_sits_in()
    {
        // The ball's membership test measures to a cell's integer coordinate, so a centre near a cell corner
        // is √3/2 = 0.866 from every candidate. At a twig's radius that selects nothing, and a limb swept at
        // one comes out as a dotted line — the block it sits in has to be stamped whatever the radius.
        foreach (var radius in new[] { 0.5, 0.55, 0.6, 0.7, 0.87, 1.4 })
            for (var step = 0; step < 8; step++)
            {
                var corner = new Vec3(0.5 - step * 0.01, 0.5, 0.5);
                await Assert.That(SweptVolume.Ball(corner, radius).Any()).IsTrue();
            }
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
    public async Task A_cluster_is_perforated_lace_not_a_solid_ball()
    {
        // A hand-built crown is 24% block over its own volume, every leaf of it with air on some side. A
        // cluster measured on its own sits above that — the crown's own figure is the union of clusters and
        // the seams between them — but nowhere near solid: lace, not a brush stroke.
        List<LeafCluster> one = [new(new Vec3(0, 0, 0), new Vec3(6, 4, 6), 0)];
        var inside = Cells(-6, 6).Where(p => Quadric(p, 5) < 0.5).ToList();
        var filled = inside.Count(p => TreeCrown.OwnerAt(one, p, 7) is not null);

        await Assert.That(filled / (double)inside.Count).IsGreaterThan(0.3);
        await Assert.That(filled / (double)inside.Count).IsLessThan(0.65);
    }

    [Test]
    public async Task A_leaf_that_cannot_reach_wood_is_never_emitted()
    {
        HashSet<(int X, int Y, int Z)> wood = [(0, 0, 0), (0, 1, 0), (0, 2, 0)];
        // Two leaves on the wood, one chained off those, and one across a gap that nothing holds.
        List<(int X, int Y, int Z)> drawn = [(1, 2, 0), (1, 3, 0), (2, 4, 0), (7, 7, 7)];

        var held = TreeCrown.Rooted(drawn, wood);

        await Assert.That(held).Contains((1, 2, 0));
        await Assert.That(held).Contains((2, 4, 0));       // reached through the chain, not directly
        await Assert.That(held).DoesNotContain((7, 7, 7)); // floating, so it is not placed at all
    }

    [Test]
    public async Task A_branch_leaves_a_vertical_trunk_by_the_angle_it_was_given()
    {
        // The angle a limb leaves by is measured against its parent, so it has to survive the parent being
        // vertical — the case a yaw-and-pitch turn loses entirely, and the one every trunk is in.
        var tree = TreeSkeleton.Grow(new TreeShape(Height: 24, BranchAngle: 1.05), seed: 6);
        var laterals = tree.Limbs.Where(limb => limb.Level == 1).ToList();

        var offVertical = laterals.Select(limb =>
        {
            var span = limb.Path[^1] - limb.Path[0];
            return Math.Acos(Math.Clamp(span.Y / Math.Max(1e-9, span.Length), -1, 1)) * 180 / Math.PI;
        }).Average();

        // The corpus puts an author's first-order limbs at 59° off vertical and their children at 67°.
        await Assert.That(offVertical).IsGreaterThan(40);
        await Assert.That(offVertical).IsLessThan(85);
    }

    [Test]
    public async Task A_grown_tree_holds_every_leaf_it_places_at_every_size()
    {
        // The gate the hand-built corpus supports, over the sizes and seeds the inspector can ask for:
        // foliage reaches wood, a quarter of it touches wood outright, and none of it is a solid. The wood
        // contact is checked as the corpus's own 30.3% was computed — over the whole sweep — with a floor
        // under each tree, because an author's own trees vary either side of that figure too and a per-tree
        // assertion at the aggregate would be stricter than the ground truth it comes from.
        var contact = new List<double>();
        foreach (var height in new double[] { 6, 12, 20, 32, 40 })
            for (uint seed = 1; seed <= 4; seed++)
            {
                var shape = new TreeShape(Height: height, Leader: 0.55, Flow: 0.45);
                var tree = TreeSkeleton.Grow(shape, seed);
                var wood = new HashSet<(int X, int Y, int Z)>();
                foreach (var limb in tree.Limbs)
                    foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                        wood.Add(cell);

                var clusters = TreeCrown.Clusters(tree.Tips, leafSize: 0.6, shape.Size, seed);
                var (min, max) = TreeCrown.Bounds(clusters);
                var drawn = new List<(int X, int Y, int Z)>();
                for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
                for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
                for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
                    if (!wood.Contains((x, y, z)) && TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), seed) is not null)
                        drawn.Add((x, y, z));
                var leaves = TreeCrown.Rooted(drawn, wood);

                await Assert.That(leaves.Count).IsGreaterThan(0);
                var touching = leaves.Count(leaf => Neighbours(leaf).Any(wood.Contains));
                var occupied = leaves.Sum(leaf => Neighbours(leaf).Count(c => leaves.Contains(c) || wood.Contains(c)));

                contact.Add(touching / (double)leaves.Count);
                await Assert.That(touching / (double)leaves.Count).IsGreaterThan(0.20);
                // A hand-built crown carries 6.2 occupied neighbours per leaf; this catches a return to the
                // 12.5 a solid measures rather than certifying the last of the gap is closed.
                await Assert.That(occupied / (double)leaves.Count).IsLessThan(9.5);
                // And the wood itself is one network, so no limb is left floating in the air.
                await Assert.That(Pieces(wood)).IsEqualTo(1);
            }

        await Assert.That(contact.Average()).IsGreaterThan(0.25);
    }

    [Test]
    public async Task A_whorled_tree_carries_its_crown_lower_than_a_staggered_one()
    {
        // The measure the corpus separates conifers from broadleaves on: a hand-built conifer keeps 60-77% of
        // its foliage in the lower half of its crown, a broadleaf 43-61%. The two forms have to separate here
        // or a whorl is decoration rather than a silhouette.
        var lower = new List<double>();
        foreach (var whorled in new[] { false, true })
        {
            var share = new List<double>();
            foreach (var height in new double[] { 16, 22, 28 })
                for (uint seed = 1; seed <= 3; seed++)
                {
                    var shape = new TreeShape(Height: height, Leader: 0.55, Flow: 0.45, Whorled: whorled);
                    var tree = TreeSkeleton.Grow(shape, seed);
                    var wood = new HashSet<(int X, int Y, int Z)>();
                    foreach (var limb in tree.Limbs)
                        foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                            wood.Add(cell);
                    var clusters = TreeCrown.Clusters(tree.Tips, leafSize: 0.6, shape.Size, seed);
                    var (min, max) = TreeCrown.Bounds(clusters);
                    var drawn = new List<(int X, int Y, int Z)>();
                    for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
                    for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
                    for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
                        if (!wood.Contains((x, y, z)) && TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), seed) is not null)
                            drawn.Add((x, y, z));
                    var leaves = TreeCrown.Rooted(drawn, wood);

                    var foot = leaves.Min(cell => cell.Y);
                    var span = Math.Max(1, leaves.Max(cell => cell.Y) - foot);
                    share.Add(leaves.Count(cell => cell.Y - foot <= span / 2.0) / (double)leaves.Count);
                }
            lower.Add(share.Average());
        }

        await Assert.That(lower[1]).IsGreaterThan(lower[0] + 0.1);   // the whorled one, by a clear margin
        await Assert.That(lower[1]).IsGreaterThan(0.55);             // and inside the corpus's conifer band
    }

    private static IEnumerable<(int X, int Y, int Z)> Neighbours((int X, int Y, int Z) cell)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        for (var dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0 || dz != 0) yield return (cell.X + dx, cell.Y + dy, cell.Z + dz);
    }

    private static int Pieces(HashSet<(int X, int Y, int Z)> cells)
    {
        var pending = new HashSet<(int X, int Y, int Z)>(cells);
        var count = 0;
        while (pending.Count > 0)
        {
            var seed = pending.First();
            pending.Remove(seed);
            var queue = new Queue<(int X, int Y, int Z)>([seed]);
            while (queue.Count > 0)
                foreach (var next in Neighbours(queue.Dequeue()))
                    if (pending.Remove(next)) queue.Enqueue(next);
            count++;
        }
        return count;
    }

    // ── the channel bed ────────────────────────────────────────────────────────────────────────────
    private static readonly double[][] Straight = [[4, 20], [36, 20]];

    [Test]
    public async Task A_channel_bed_is_a_bowl_deepest_on_the_centerline()
    {
        // The whole reason water needs its own carve rather than the path's flat repaint: the fill has to sit
        // in a U, so the deepest cut is on the line the author drew and it rises to a single block at the shore.
        var cells = WaterBed.Cells(Straight, radius: 4, depth: 4, ChannelForm.Canal, edge: 0, seed: 5).ToList();
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
            => WaterBed.Cells(Straight, radius: 3, depth, ChannelForm.Canal, edge: 0, seed: 5).Where(cell => cell.Z == 20).Max(cell => cell.Depth);

        await Assert.That(Centre(6)).IsGreaterThan(Centre(2));
    }

    [Test]
    public async Task A_stream_beads_along_its_arc_where_a_canal_holds_one_width()
    {
        // Parity with the decoration prototype's `drawChannel`: a stream is not one taper end-to-end, it beads —
        // its width pinches to half the radius and swells back on a fixed beat down the run, and never exceeds
        // the nominal width. A canal holds one width the whole way. The half-width per column across the run is
        // what shows it, on the interior columns so an end-cap disc doesn't read as a pinch.
        List<int> HalfWidths(ChannelForm form) => [.. WaterBed.Cells(Straight, radius: 5, depth: 4, form, edge: 0, seed: 5)
            .Where(cell => cell.X is >= 10 and <= 30).GroupBy(cell => cell.X).OrderBy(group => group.Key)
            .Select(group => group.Max(cell => Math.Abs(cell.Z - 20)))];

        var canal = HalfWidths(ChannelForm.Canal);
        var stream = HalfWidths(ChannelForm.Stream);

        await Assert.That(canal.Max() - canal.Min()).IsLessThanOrEqualTo(1);          // one width the whole way
        await Assert.That(stream.Max() - stream.Min()).IsGreaterThanOrEqualTo(2);     // pinches and swells
        await Assert.That(stream.Max()).IsLessThanOrEqualTo(5);                       // never past the nominal
    }

    [Test]
    public async Task A_stream_runs_shallower_than_a_canal_of_the_same_depth()
    {
        // The other half of the prototype's stream: it runs shallow throughout, a whole length of riffle.
        int Deepest(ChannelForm form)
            => WaterBed.Cells(Straight, radius: 5, depth: 6, form, edge: 0, seed: 5).Max(cell => cell.Depth);
        await Assert.That(Deepest(ChannelForm.Stream)).IsLessThan(Deepest(ChannelForm.Canal));
    }

    [Test]
    public async Task The_same_channel_carves_the_same_bed()
    {
        var one = WaterBed.Cells(Straight, radius: 3, depth: 3, ChannelForm.Natural, edge: 0.8, seed: 5).ToList();
        var two = WaterBed.Cells(Straight, radius: 3, depth: 3, ChannelForm.Natural, edge: 0.8, seed: 5).ToList();
        await Assert.That(one).IsEquivalentTo(two);
    }

    [Test]
    public async Task The_beach_lies_just_outside_the_water_never_on_it()
    {
        // The shore rides just past the water edge — none of its cells are ones the bed carves — so where a
        // channel's width pinches or swells the beach rides in and out with it (one shore law, the water drives).
        var water = WaterBed.Cells(Straight, radius: 4, depth: 3, ChannelForm.Stream, edge: 0.8, seed: 5)
            .Select(cell => (cell.X, cell.Z)).ToHashSet();
        var shore = WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Stream, shoreWidth: 3, edge: 0.8, wander: true, seed: 5).ToList();

        await Assert.That(shore).IsNotEmpty();
        await Assert.That(shore.Any(cell => water.Contains(cell))).IsFalse();
    }

    [Test]
    public async Task The_beach_wraps_the_water_symmetrically_rather_than_drifting_onto_one_bank()
    {
        // The fix for a beach that read as a shifted gravel path: the width is parameterised along the arc, so at
        // each x down a straight run both banks carry the same beach — the outer beach edge is as far above the
        // water as below it. A per-cell spatial field would open one bank and close the other.
        var shore = WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Canal, shoreWidth: 3, edge: 0, wander: true, seed: 5).ToList();
        var lopsided = 0;
        foreach (var column in shore.GroupBy(cell => cell.X))
        {
            var above = column.Where(cell => cell.Z > 20).Select(cell => cell.Z - 20).DefaultIfEmpty(0).Max();
            var below = column.Where(cell => cell.Z < 20).Select(cell => 20 - cell.Z).DefaultIfEmpty(0).Max();
            if (Math.Abs(above - below) > 1) lopsided++;
        }
        await Assert.That(lopsided).IsEqualTo(0);
    }

    [Test]
    public async Task An_even_beach_holds_one_width_on_both_banks_the_whole_way()
    {
        // Wander off: the beach is a clean band of the full width hugging the water on both banks — every column
        // the water crosses carries the same beach above and below, with no drop-outs.
        var even = WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Canal, shoreWidth: 3, edge: 0, wander: false, seed: 5).ToList();
        foreach (var column in even.Where(cell => cell.X is >= 12 and <= 28).GroupBy(cell => cell.X))
        {
            var above = column.Count(cell => cell.Z > 20);
            var below = column.Count(cell => cell.Z < 20);
            await Assert.That(above).IsEqualTo(below);   // the same even beach on each bank
            await Assert.That(above).IsGreaterThan(0);   // and it never drops out
        }
    }

    [Test]
    public async Task No_beach_width_is_no_beach()
    {
        await Assert.That(WaterBed.ShoreCells(Straight, radius: 4, ChannelForm.Natural, shoreWidth: 0, edge: 0.8, wander: true, seed: 5)).IsEmpty();
    }
}
