using PgmStudio.Contracts;

namespace PgmStudio.Api.Services;

/// <summary>
/// Whether one block can hold a thing placed into it, read off the scan's vertical segments: the block itself
/// clear, and a solid block on one of its six faces for the placement to go against. A monument is the block a
/// player puts a wool into, so the <c>block-seat</c> route and the intent gate ask this one read.
/// <para>Each run's whole span is read rather than its top, because a Y inside a solid run is the case the two
/// differ on: the floor below it is a real floor and the block is still occupied.</para>
/// </summary>
public static class BlockSeats
{
    public static async Task<BlockSeatDto> ReadAsync(
        FeatureData features, long mapId, int x, int y, int z, CancellationToken ct)
    {
        // The block's own column plus its four neighbours: a placement needs a block on any one of six faces,
        // so the read is five columns wide rather than one.
        var columns = await features.SegmentRowsAsync(mapId, q => q.Where(s =>
            (s.WorldX == x && (s.WorldZ == z || s.WorldZ == z - 1 || s.WorldZ == z + 1))
            || (s.WorldZ == z && (s.WorldX == x - 1 || s.WorldX == x + 1))), ct);

        bool Solid(int atX, int atY, int atZ) => columns.Any(
            run => run.WorldX == atX && run.WorldZ == atZ && run.WorldYStart <= atY && atY <= run.WorldYEnd);

        var scanned = columns.Any(run => run.WorldX == x && run.WorldZ == z);
        var clear = !Solid(x, y, z);
        var pedestal = Solid(x, y - 1, z);
        var support = pedestal || Solid(x, y + 1, z)
                      || Solid(x - 1, y, z) || Solid(x + 1, y, z) || Solid(x, y, z - 1) || Solid(x, y, z + 1);
        return new BlockSeatDto(scanned, clear, support, pedestal);
    }
}
