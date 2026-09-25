using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <see cref="ClaimRaster"/>: what every cell of a board reads as, off synthetic claims and keep-out functions
/// rather than a built world. <see cref="PgmStudio.Api.Tests.DressingPreviewTests"/> is the sibling that reads
/// the same classification through the <c>/sketch/dressing</c> preview.
/// </summary>
public sealed class ClaimRasterTests
{
    private static Dictionary<(int X, int Z), int> Ground(int minX, int minZ, int maxX, int maxZ)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            surface[(x, z)] = 8;
        return surface;
    }

    private static PlacementClaim Claim(string kind, ProvenancePass pass, params (int X, int Z)[] cells) =>
        new(new StampId(kind, "u", 0), pass, cells);

    [Test]
    public async Task The_grid_spans_the_surfaces_own_bounding_box()
    {
        var surface = Ground(-5, -3, 6, 2);
        var grid = ClaimRaster.Read([], surface, (_, _) => null, (_, _) => false);

        await Assert.That(grid.MinX).IsEqualTo(-5);
        await Assert.That(grid.MinZ).IsEqualTo(-3);
        await Assert.That(grid.Width).IsEqualTo(12);   // -5..6 inclusive
        await Assert.That(grid.Height).IsEqualTo(6);   // -3..2 inclusive
        await Assert.That(grid.Rows.Count).IsEqualTo(grid.Height);
        await Assert.That(grid.Rows.All(row => row.Length == grid.Width)).IsTrue();
    }

    [Test]
    public async Task A_cell_a_claim_and_a_keep_out_both_hold_shows_the_claim()
    {
        var surface = Ground(0, 0, 4, 0);
        var claims = new[] { Claim("tree", ProvenancePass.Prop, (2, 0)) };
        var grid = ClaimRaster.Read(claims, surface, (_, _) => KeepOut.Structure, (_, _) => false);

        await Assert.That(grid.Rows[0][2]).IsEqualTo('4');   // tree, not 'b' (structure keep-out)
    }

    [Test]
    public async Task A_cell_outside_the_surface_is_a_space_whatever_else_holds_it()
    {
        var surface = Ground(0, 0, 2, 0);
        // A claim naming a cell the surface never had ground on — a board never builds one, but the raster
        // must still read it as void rather than let a stray claim widen the grid or paint over the gap.
        var claims = new[] { Claim("tree", ProvenancePass.Prop, (5, 5)) };
        var grid = ClaimRaster.Read(claims, surface, (_, _) => KeepOut.Structure, (_, _) => true);

        await Assert.That(grid.Rows[0]).DoesNotContain('5');
        foreach (var row in grid.Rows) await Assert.That(row.Contains(' ')).IsFalse();
    }

    [Test]
    public async Task An_unclaimed_cell_with_no_keep_out_and_no_clearance_is_free()
    {
        var surface = Ground(0, 0, 0, 0);
        var grid = ClaimRaster.Read([], surface, (_, _) => null, (_, _) => false);

        await Assert.That(grid.Rows[0]).IsEqualTo("0");
    }

    [Test]
    public async Task A_goals_clearance_is_asked_before_the_keep_out_mask_on_an_unclaimed_cell()
    {
        // The same order Decorator's own seating asks a candidate site: a goal's clearance covers ground the
        // keep-out mask also holds as built, and the rule an author looks up beside a goal names the goal.
        var surface = Ground(0, 0, 0, 0);
        var grid = ClaimRaster.Read([], surface, (_, _) => KeepOut.Built, (_, _) => true);

        await Assert.That(grid.Rows[0]).IsEqualTo("9");
    }

    [Test]
    [Arguments(KeepOut.Spawn, '7')]
    [Arguments(KeepOut.Approach, '8')]
    [Arguments(KeepOut.WoolRoom, 'a')]
    [Arguments(KeepOut.Structure, 'b')]
    [Arguments(KeepOut.Built, 'b')]
    public async Task Every_keep_out_prints_its_own_digit(KeepOut keepOut, char digit)
    {
        var surface = Ground(0, 0, 0, 0);
        var grid = ClaimRaster.Read([], surface, (_, _) => keepOut, (_, _) => false);

        await Assert.That(grid.Rows[0]).IsEqualTo(digit.ToString());
    }

    [Test]
    [Arguments("water", ProvenancePass.Prop, '1')]
    [Arguments("stroke", ProvenancePass.Prop, '2')]
    [Arguments("tree", ProvenancePass.Prop, '4')]
    [Arguments("boulder", ProvenancePass.Prop, '5')]
    [Arguments("flora", ProvenancePass.Prop, '6')]
    [Arguments("house", ProvenancePass.Structure, '3')]
    public async Task Every_claimed_kind_prints_its_own_digit(string kind, ProvenancePass pass, char digit)
    {
        var surface = Ground(0, 0, 0, 0);
        var grid = ClaimRaster.Read([Claim(kind, pass, (0, 0))], surface, (_, _) => null, (_, _) => false);

        await Assert.That(grid.Rows[0]).IsEqualTo(digit.ToString());
    }

    [Test]
    public async Task The_first_claim_on_a_cell_keeps_it()
    {
        // `GroundClaims` is a TryAdd, and this answers the placement question, so the road under a porch is
        // still the road a tree stands off — reading it the other way round would hide it under the house.
        var surface = Ground(0, 0, 0, 0);
        var claims = new[]
        {
            Claim("stroke", ProvenancePass.Prop, (0, 0)),
            Claim("house", ProvenancePass.Structure, (0, 0)),
        };
        var grid = ClaimRaster.Read(claims, surface, (_, _) => null, (_, _) => false);

        await Assert.That(grid.Rows[0]).IsEqualTo("2");   // the route, which was laid first
    }

    // ── the raster read forwards: where a prop may seat ────────────────────────────────────────────────

    [Test]
    public async Task A_footprint_seats_only_where_the_whole_box_is_free()
    {
        // Free ground x 0..4 with a boulder at x 2: a tree's 1x1 seats at 0, 1, 3 and 4; its 2x1 only at
        // 0 and 3, since the box is laid from its minimum corner.
        var surface = Ground(0, 0, 4, 0);
        var grid = ClaimRaster.Read([Claim("boulder", ProvenancePass.Prop, (2, 0))], surface,
                                    (_, _) => null, (_, _) => false);

        await Assert.That(ClaimRaster.Seat(grid, "tree", standoff: 0, width: 1, depth: 1).Rows[0])
            .IsEqualTo("11011");
        await Assert.That(ClaimRaster.Seat(grid, "tree", standoff: 0, width: 2, depth: 1).Rows[0])
            .IsEqualTo("10010");
    }

    [Test]
    public async Task Ground_a_later_kind_claims_is_free_ground_for_an_earlier_one()
    {
        // Cover is placed last, so a bed of flora is not what stops a tree — on the next pass the flora
        // meets the tree. A tree's claim does stop the flora, which is the same rule read the other way.
        var surface = Ground(0, 0, 2, 0);
        var flora = ClaimRaster.Read([Claim("flora", ProvenancePass.Prop, (1, 0))], surface,
                                     (_, _) => null, (_, _) => false);
        var tree = ClaimRaster.Read([Claim("tree", ProvenancePass.Prop, (1, 0))], surface,
                                    (_, _) => null, (_, _) => false);

        await Assert.That(ClaimRaster.Seat(flora, "tree", standoff: 0, width: 1, depth: 1).Rows[0]).IsEqualTo("111");
        await Assert.That(ClaimRaster.Seat(tree, "flora", standoff: 0, width: 1, depth: 1).Rows[0]).IsEqualTo("101");
        await Assert.That(ClaimRaster.Seat(tree, "tree", standoff: 0, width: 1, depth: 1).Rows[0]).IsEqualTo("101")
            .Because("a prop of the same kind is an exclusion like any other");
    }

    private static HouseProp House(string id, int minX, int minZ, int maxX, int maxZ) =>
        new()
        {
            Id = id, Wings = [new AuthoredWing([[minX, minZ], [maxX, maxZ]])],
            Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } },
        };

    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Top) World(
        IReadOnlyDictionary<(int X, int Z), int> surface)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        foreach (var (cell, height) in surface)
        {
            for (var y = 0; y < height - 1; y++) world.SetBlock(cell.X, y, cell.Z, Blocks.Stone);
            world.SetBlock(cell.X, height - 1, cell.Z, Blocks.Grass);
            top[cell] = height;
        }
        return (world, top);
    }

    /// <summary>A yard beside a quarry: the seat query asks a building's site to be level by the pass's own
    /// arithmetic, so every anchor it offers is one the pass seats, and a footprint straddling the quarry's
    /// edge is refused <c>DR-SLOPE</c> rather than offered and then declined. What it still cannot ask is named
    /// in the answer.</summary>
    [Test]
    public async Task A_seat_straddling_a_quarry_edge_is_refused_as_the_pass_refuses_it()
    {
        const int size = 24;
        var surface = Ground(0, 0, size - 1, size - 1);
        for (var z = 0; z < size; z++)
        for (var x = 12; x < size; x++)
            surface[(x, z)] = 30;                                // the yard stands 22 over the quarry floor

        var style = House("probe", 0, 0, 4, 4).Style;
        var grid = ClaimRaster.Read([], surface, (_, _) => null, (_, _) => false);
        var seats = ClaimRaster.Seat(grid, "house", standoff: 0, width: 5, depth: 5,
            new ClaimRaster.Level(surface, SiteLevel.Limit(style)));

        await Assert.That(seats.Refused.Select(because => because.Rule)).Contains(DressingRules.SiteNotLevel);
        await Assert.That(seats.Unasked).IsEquivalentTo([DressingRules.RouteCrossed, DressingRules.WayThrough]);
        await Assert.That(ClaimRaster.RenderSeats(seats)).Contains("NOT ASKED  DR-CROSS, DR-WAY");

        var disagreed = new List<string>();
        for (var row = 0; row + 4 < grid.Height; row += 3)
        for (var column = 0; column + 4 < grid.Width; column++)
        {
            var raster = seats.Rows[row][column] == '1';
            var (world, top) = World(surface);
            var tally = Decorator.Decorate(world, new DressingContext(top, [House("new", column, row, column + 4, row + 4)]));
            var refused = tally.Declines.Any(finding => finding.SubjectIds.Contains("new"));
            if (raster == !refused) continue;
            disagreed.Add($"({column}, {row}) raster {(raster ? "seats" : "refuses")}, pass "
                + string.Join("/", tally.Declines.Where(f => f.SubjectIds.Contains("new")).Select(f => f.Rule)));
        }
        await Assert.That(string.Join("; ", disagreed.Take(6))).IsEqualTo("");
    }

    /// <summary>A building is asked about the way past it too, and a 15-block lane is the case that says so:
    /// a 5×5 house stamps 7 across, so it seats only where eight blocks of lane are left beside it — which on
    /// a lane with void either side means hugged to a wall. Two anchors at each wall rather than one, because
    /// the band runs along the walls and a roof may oversail the coast by its eave.</summary>
    [Test]
    public async Task A_building_seats_in_a_lane_only_where_it_leaves_the_passage()
    {
        // A lane 15 across and long enough that its ends never decide anything.
        var surface = Ground(0, 0, 14, 39);
        var grid = ClaimRaster.Read([], surface, (_, _) => null, (_, _) => false);

        var seats = ClaimRaster.Seat(grid, "house", standoff: 0, width: 5, depth: 5);

        await Assert.That(seats.Rows[20]).IsEqualTo("110000000110000")
            .Because("eight of lane are left only from the two anchors at each wall");
        await Assert.That(ClaimRaster.Seat(grid, "tree", standoff: 0, width: 5, depth: 5).Rows[20])
            .IsEqualTo("111111111110000").Because("a tree has no way past to leave");
    }

    /// <summary>The forward read and the pass answer one question. Every anchor on a board with a coast, a
    /// hole in it and a building already standing is asked both ways: the raster offers a seat exactly where
    /// the pass raises no <c>DR-PASS</c> complaint about a building put there — the grouping included, since a
    /// candidate beside the standing one is judged as the block of buildings they make.</summary>
    [Test]
    public async Task Every_seat_the_raster_offers_is_one_the_pass_does_not_complain_about()
    {
        const int size = 30;
        var surface = Ground(0, 0, size - 1, size - 1);
        for (var z = 12; z <= 17; z++)
        for (var x = 12; x <= 17; x++)
            surface.Remove((x, z));                              // a hole through the middle
        for (var z = 0; z < 6; z++)
        for (var x = size - 6; x < size; x++)
            surface.Remove((x, z));                              // a bite out of one corner

        var standing = House("old", 2, 22, 8, 27);
        var (built, builtTop) = World(surface);
        var already = Decorator.Decorate(built, new DressingContext(builtTop, [standing]));
        var grid = ClaimRaster.Read(already.Placements, surface, (_, _) => null, (_, _) => false);
        var seats = ClaimRaster.Seat(grid, "house", standoff: 0, width: 5, depth: 5);

        var offered = 0;
        var disagreed = new List<string>();
        for (var row = 0; row + 4 < grid.Height; row++)
        for (var column = 0; column + 4 < grid.Width; column++)
        {
            var raster = seats.Rows[row][column] == '1';
            if (raster) offered++;

            var (world, top) = World(surface);
            var tally = Decorator.Decorate(world, new DressingContext(top,
                [standing, House("new", column, row, column + 4, row + 4)]));
            var refused = tally.Declines.Any(finding => finding.SubjectIds.Contains("new"));
            if (raster == !refused) continue;
            disagreed.Add($"({column}, {row}) raster {(raster ? "seats" : "refuses")}, pass "
                + string.Join("/", tally.Declines.Where(f => f.SubjectIds.Contains("new")).Select(f => f.Rule)));
        }

        await Assert.That(offered).IsGreaterThan(0).Because("a board with no seat proves nothing");
        await Assert.That(string.Join("; ", disagreed.Take(6))).IsEqualTo("");
    }

    [Test]
    public async Task A_route_refuses_the_cells_nearer_than_the_standoff_and_lets_the_one_at_it_stand()
    {
        // A route cell at x 0, a tree's standoff of 3: x 1 and x 2 are strictly nearer and refuse, x 3 is
        // exactly at the standoff and stands — GroundClaims.NearerThan's own comparison.
        var surface = Ground(0, 0, 5, 0);
        var grid = ClaimRaster.Read([Claim("stroke", ProvenancePass.Prop, (0, 0))], surface,
                                    (_, _) => null, (_, _) => false);

        var seating = ClaimRaster.Seat(grid, "tree", standoff: 3, width: 1, depth: 1);
        await Assert.That(seating.Rows[0]).IsEqualTo("000111");
        await Assert.That(seating.Refused.Single(because => because.Rule == DressingRules.RoadStandoff).Cells)
            .IsEqualTo(2);
    }

    [Test]
    public async Task Every_cell_is_either_a_seat_or_one_rules_refusal()
    {
        var surface = Ground(0, 0, 9, 9);
        var grid = ClaimRaster.Read([Claim("stroke", ProvenancePass.Prop, (0, 0), (1, 0))], surface,
                                    (x, _) => x == 9 ? KeepOut.Spawn : null, (_, z) => z == 9);

        var seating = ClaimRaster.Seat(grid, "tree", standoff: 3, width: 1, depth: 1);

        await Assert.That(seating.Seats + seating.Refused.Sum(because => because.Cells)).IsEqualTo(100);
        await Assert.That(seating.Refused.Select(because => because.Rule))
            .Contains(DressingRules.KeptClear).And.Contains(ObjectiveRules.PropInClearance);
        await Assert.That(ClaimRaster.RenderSeats(seating)).Contains("SEATS  tree, footprint 1x1, 3 blocks off a route");
    }
}
