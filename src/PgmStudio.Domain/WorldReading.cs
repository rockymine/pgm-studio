namespace PgmStudio.Domain;

/// <summary>A wall sign and what it says, its text already joined out of the four lines the format stores.
/// </summary>
public readonly record struct ReadSign(int X, int Y, int Z, string Text);

/// <summary>An armour stand, at the position its feet stand at — the head's wool colour where it wears one,
/// and the name it was given where it has one.</summary>
public readonly record struct ReadStand(int X, double FeetY, int Z, string? HeadWool, string? CustomName);

/// <summary>An item frame holding wool, resolved to the block it is mounted on and the colour it shows.
/// </summary>
public readonly record struct ReadFrame(int X, int Y, int Z, string Color);

/// <summary> A world, read. <para><b>The boundary between reading a world and deriving from one.</b> Decoding
/// Anvil chunks and interpreting the NBT inside them is the world package's; what a monument or a core
/// <em>is</em> is a derivation, and derivations do not belong beside the format they happened to be read out of.
/// This is what crosses: blocks by position, and the three things in a world that carry an author's words or
/// intent rather than only material.</para>
/// <para>Nothing here knows the format it came from. A reading assembled by hand in a test is the same input as
/// one decoded from a region file, which is what lets the derivations be tested without a world. </para>
/// <para><b>Blocks</b> — Every non-air block in the region that was read, by position. Air is absent rather than
/// present as id 0, so a lookup that misses is a lookup at air.</para></summary>
public sealed record WorldReading(
    IReadOnlyDictionary<(int X, int Y, int Z), (int Id, int Data)> Blocks,
    IReadOnlyList<ReadSign> Signs,
    IReadOnlyList<ReadStand> Stands,
    IReadOnlyList<ReadFrame> Frames)
{
    /// <summary>A reading of nothing, for a derivation asked about a region no world covers.</summary>
    public static readonly WorldReading Empty =
        new(new Dictionary<(int, int, int), (int, int)>(), [], [], []);
}
