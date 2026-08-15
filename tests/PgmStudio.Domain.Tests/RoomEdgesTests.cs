using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The facts an edge carries. Asserted as relations between them rather than as a transcription of the four
/// tables: a copy of the table would pass against a table that had been copied wrong, which is the failure
/// putting them on the enum exists to prevent.
/// </summary>
public sealed class RoomEdgesTests
{
    private static readonly RoomEdge[] Every = [RoomEdge.NegZ, RoomEdge.PosZ, RoomEdge.NegX, RoomEdge.PosX];

    [Test]
    public async Task An_outward_step_is_one_block_on_exactly_one_axis()
    {
        foreach (var edge in Every)
        {
            var (x, z) = edge.Outward();
            await Assert.That(Math.Abs(x) + Math.Abs(z)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Inward_is_the_outward_step_reversed()
    {
        foreach (var edge in Every)
        {
            var (outX, outZ) = edge.Outward();
            await Assert.That(edge.Inward()).IsEqualTo((-outX, -outZ));
        }
    }

    [Test]
    public async Task The_opposite_edge_looks_the_other_way_and_is_its_own_inverse()
    {
        foreach (var edge in Every)
        {
            await Assert.That(edge.Opposite().Outward()).IsEqualTo(edge.Inward());
            await Assert.That(edge.Opposite().Opposite()).IsEqualTo(edge);
            await Assert.That(edge.Opposite()).IsNotEqualTo(edge);
        }
    }

    [Test]
    public async Task A_wall_runs_along_the_axis_its_face_does_not_look_down()
    {
        foreach (var edge in Every)
        {
            var (outX, outZ) = edge.Outward();
            // AlongX is the wall's own run, which is the axis the outward step leaves alone.
            await Assert.That(edge.AlongX()).IsEqualTo(outX == 0);
            await Assert.That(edge.AlongX()).IsEqualTo(edge.Opposite().AlongX());
        }
    }

    [Test]
    public async Task Positive_names_the_edge_whose_face_looks_toward_the_high_side()
    {
        foreach (var edge in Every)
        {
            var (outX, outZ) = edge.Outward();
            await Assert.That(edge.Positive()).IsEqualTo(outX + outZ > 0);
            await Assert.That(edge.Positive()).IsNotEqualTo(edge.Opposite().Positive());
        }
    }

    [Test]
    public async Task The_four_edges_are_two_axes_and_two_sides_apiece()
    {
        var seen = Every
            .Select(edge => (edge.AlongX(), edge.Positive()))
            .Distinct()
            .Count();
        await Assert.That(seen).IsEqualTo(4);
    }
}
