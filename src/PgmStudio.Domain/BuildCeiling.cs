namespace PgmStudio.Domain;

/// <summary>
/// <b>How high a player may build, and how far above that a goal marker hangs.</b> Two numbers, both the
/// author's, and both stated here rather than at the one site that happens to apply them — the mistake
/// <c>headroom</c> made was not the arithmetic but that nothing named what the arithmetic was for.
///
/// <para><b>The measurement is what makes it right.</b> The ceiling is
/// <see cref="OverGround"/> blocks over the <em>highest ground the map actually builds</em> — the terrain
/// alone. Not a house standing on it, not a tree, not a stamped structure. That distinction is the whole
/// correction: the old cap was the plan's flat nominal <c>surface</c> plus a slack, computed from a ground
/// level the relief solve then abandons, so boards came out with a ceiling under their own terrain
/// (<c>B104</c>, <c>B176</c>). Measuring the built terrain instead gives a cap that rises with the map, and
/// measuring only the terrain keeps it from being pushed up by whatever was placed on it.</para>
///
/// <para><b>Twenty is <c>G6</c>'s floor, not a guess.</b> The rule asks for at least twenty blocks of build
/// clearance over the island surface, and warns in the same breath that a generous cap over flat terrain is
/// the sky-layer smell — players dig to bedrock, defend from above, and the match stalls into coverless sky
/// bow-fighting. So it sits at the floor of the band rather than in the middle of it.</para>
/// </summary>
public static class BuildCeiling
{
    /// <summary>Blocks of build clearance over the highest terrain surface (<c>G6</c>).</summary>
    public const int OverGround = 20;

    /// <summary>Blocks between the ceiling and a goal marker's floor. The marker is a sky sign — a player
    /// crossing open ground reads where the goal is from it — so it hangs just out of reach of the highest
    /// legal build rather than at a fixed altitude or over whatever happens to have been stamped under it.
    /// One number for every goal kind, which is what lets a destroyable and a core share a marker rule
    /// instead of each reasoning about its own structure's height.</summary>
    public const int MarkerOver = 5;

    /// <summary>The ceiling for a map whose highest terrain column is <paramref name="highestGround"/>.</summary>
    public static int Of(int highestGround) => highestGround + OverGround;
}
