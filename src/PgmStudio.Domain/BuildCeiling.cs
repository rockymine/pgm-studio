namespace PgmStudio.Domain;

/// <summary>
/// <b>How high a player may build, and how far above that a goal marker hangs.</b> Two numbers, both the
/// author's, and both stated here rather than at the one site that happens to apply them — the mistake
/// <c>headroom</c> made was not the arithmetic but that nothing named what the arithmetic was for.
///
/// <para><b>The measurement is what makes it right.</b> The ceiling is <see cref="OverGround"/> blocks over
/// the <em>highest thing the map actually builds and a player meets</em>: the terrain, and the buildings
/// standing on it — a spawn hall, a wool cage, a house's ridge. Measuring what was built rather than the
/// plan's flat nominal <c>surface</c> is what stops a board coming out with a ceiling under its own terrain
/// (<c>B104</c>, <c>B176</c>), and clearing the roofs is what stops one coming out with a ceiling a player
/// cannot build over the town in.</para>
///
/// <para><b>Two kinds of block are deliberately not in it, each for its own reason.</b> A <b>made thing</b> —
/// a balloon, a ship, a sculpture drawn out of layers — is neither ground nor a building but scenery the
/// author hung in the air, so a ceiling tracking it would follow whatever altitude was felt like: a balloon
/// at y97 asks for 117 on a board whose terrain tops at 33. An <b>objective</b> floats over the ground by
/// design — a core on the ground cannot leak and a destroyable on it is trivially covered — so a ceiling
/// derived from one could never be beneath it, and the over-ceiling complaint that exists to say a goal
/// stands out of a player's reach could never fire. <see cref="Floating"/> names the second set.</para>
///
/// <para><b>Twenty is <c>G6</c>'s floor, not a guess.</b> The rule asks for at least twenty blocks of build
/// clearance over the island surface, and warns in the same breath that a generous cap over flat terrain is
/// the sky-layer smell — players dig to bedrock, defend from above, and the match stalls into coverless sky
/// bow-fighting. So it sits at the floor of the band rather than in the middle of it.</para>
/// </summary>
public static class BuildCeiling
{
    /// <summary>Blocks of build clearance over the highest built surface (<c>G6</c>).</summary>
    public const int OverGround = 20;

    /// <summary>The <see cref="StampId.Kind"/>s a ceiling does not rise for: the objectives, which stand off
    /// the ground on purpose. Everything else a stamp writes is a building, and the ceiling clears it.</summary>
    public static readonly IReadOnlySet<string> Floating =
        new HashSet<string>(StringComparer.Ordinal) { "destroyable", "core" };

    /// <summary>Blocks between the ceiling and a goal marker's floor. The marker is a sky sign — a player
    /// crossing open ground reads where the goal is from it — so it hangs just out of reach of the highest
    /// legal build rather than at a fixed altitude or over whatever happens to have been stamped under it.
    /// One number for every goal kind, which is what lets a destroyable and a core share a marker rule
    /// instead of each reasoning about its own structure's height.</summary>
    public const int MarkerOver = 5;

    /// <summary>The ceiling for a map whose highest terrain column is <paramref name="highestGround"/>.</summary>
    public static int Of(int highestGround) => highestGround + OverGround;
}
