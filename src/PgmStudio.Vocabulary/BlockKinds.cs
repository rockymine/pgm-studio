namespace PgmStudio.Vocabulary;

/// <summary>
/// The geometry a block carries, where a field names a block for the shape of it rather than for what it is
/// made of. A stair turns a corner by its own facing, a slab fills half its cube and reads the half bit, a log
/// takes an axis — and a field asking for one of those gets nothing it can use from a block of another kind.
///
/// <para>Three parties spell them: the gate that refuses a field holding the wrong kind, the catalogue that
/// answers which kind each field takes, and the picker that offers the ids of one.</para>
/// </summary>
public static class BlockKinds
{
    /// <summary>A stair: two bits of facing and an upside-down flag, which is what a corner is turned by.</summary>
    public const string Stair = "stair";

    /// <summary>A single slab — half a cube, seated in the upper or the lower half. A double slab is a full
    /// cube wearing the name and is never one of these.</summary>
    public const string Slab = "slab";

    /// <summary>A log: bark on four sides and an axis in its data nibble.</summary>
    public const string Log = "log";

    /// <summary>The three, in the order the catalogue lists them.</summary>
    public static readonly string[] All = [Stair, Slab, Log];
}
