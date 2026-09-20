namespace PgmStudio.Domain;

/// <summary>
/// <b>How high a player may build, and how far above that a goal marker hangs.</b> Two numbers, both the
/// author's, and both stated here rather than at the one site that happens to apply them — the mistake
/// <c>headroom</c> made was not the arithmetic but that nothing named what the arithmetic was for.
///
/// <para><b>The measurement is the terrain, averaged.</b> The ceiling is <see cref="OverGround"/> blocks over
/// the <em>mean</em> top of the built terrain columns — the ground the match is played on, taken as a whole
/// rather than at its highest point. Measuring what the relief actually built rather than the plan's flat
/// nominal <c>surface</c> is what stops a board coming out with a ceiling under its own ground
/// (<c>B104</c>, <c>B176</c>); averaging rather than maximising is what stops one peak deciding the sky over
/// the whole board, which is the generous cap <c>G6</c> warns is the sky-layer smell.</para>
///
/// <para><b>Nothing standing on the ground is in it.</b> Not a tree, not a house, not a spawn hall or a wool
/// cage, not a made thing an author hung in the air, and not an objective — a goal floats over the ground by
/// design, so a ceiling derived from one could never be beneath it and the over-ceiling complaint could
/// never fire. The surface is the terrain's own, before anything is placed on it.</para>
///
/// <para><b>Twenty is <c>G6</c>'s floor, not a guess.</b> The rule asks for at least twenty blocks of build
/// clearance over the island surface, and warns in the same breath that a generous cap over flat terrain is
/// the sky-layer smell — players dig to bedrock, defend from above, and the match stalls into coverless sky
/// bow-fighting. So it sits at the floor of the band rather than in the middle of it.</para>
/// </summary>
public static class BuildCeiling
{
    /// <summary>Blocks of build clearance over the terrain's mean surface (<c>G6</c>).</summary>
    public const int OverGround = 20;

    /// <summary>Blocks between the ceiling and a goal marker's floor. The marker is a sky sign — a player
    /// crossing open ground reads where the goal is from it — so it hangs just out of reach of the highest
    /// legal build rather than at a fixed altitude or over whatever happens to have been stamped under it.
    /// One number for every goal kind, which is what lets a destroyable and a core share a marker rule
    /// instead of each reasoning about its own structure's height.</summary>
    public const int MarkerOver = 5;

    /// <summary>The surface a ceiling is measured from: the mean top of the terrain columns, rounded to the
    /// nearest block. A board with no terrain at all answers 0.</summary>
    public static int Surface(IEnumerable<int> terrainTops)
    {
        long total = 0;
        var columns = 0;
        foreach (var top in terrainTops) { total += top; columns++; }
        return columns == 0 ? 0 : (int)Math.Round((double)total / columns, MidpointRounding.AwayFromZero);
    }

    /// <summary>The ceiling for a map whose terrain surface averages <paramref name="surface"/>.</summary>
    public static int Of(int surface) => surface + OverGround;
}
