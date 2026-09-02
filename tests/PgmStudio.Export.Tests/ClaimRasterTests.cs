using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
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
    public async Task A_later_claim_over_the_same_cell_wins()
    {
        var surface = Ground(0, 0, 0, 0);
        var claims = new[]
        {
            Claim("tree", ProvenancePass.Prop, (0, 0)),
            Claim("boulder", ProvenancePass.Prop, (0, 0)),
        };
        var grid = ClaimRaster.Read(claims, surface, (_, _) => null, (_, _) => false);

        await Assert.That(grid.Rows[0]).IsEqualTo("5");   // the boulder, placed after the tree
    }
}
