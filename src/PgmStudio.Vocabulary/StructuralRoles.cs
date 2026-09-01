namespace PgmStudio.Vocabulary;

/// <summary>
/// What a locked annotation shape on a sketch stands for. A shape carrying one of these is not terrain the
/// author drew — it is a piece the plan already placed, projected in from the compile so it stays visible
/// while the ground around it is refined, and the rasterizer skips it.
///
/// <para>Three parties spell them: the plan compiler writes the word, the rasterizer reads it to know what
/// not to carve, and the canvas reads it to know what to draw. A room is two of them — the
/// <see cref="Spawn"/> or <see cref="WoolRoom"/> region it stands on, and the <see cref="Building"/> raised
/// inside that region (docs/world-export/structures.md WX1).</para>
/// </summary>
public static class StructuralRoles
{
    /// <summary>A spawn piece: the team's protection region and the ground its room stands on.</summary>
    public const string Spawn = "spawn";

    /// <summary>A wool-room piece — the same thing for a cage.</summary>
    public const string WoolRoom = "woolRoom";

    /// <summary>The footprint the shell is stamped on, inside whichever region raises it. It carries no
    /// height of its own: the region shape is what a group's relief is held against.</summary>
    public const string Building = "building";

    /// <summary>The three, region words first.</summary>
    public static readonly string[] All = [Spawn, WoolRoom, Building];
}
