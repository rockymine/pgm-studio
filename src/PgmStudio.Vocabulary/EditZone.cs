namespace PgmStudio.Vocabulary;

/// <summary>
/// Where a column's editability comes from — the answer the editability pass gives per column of a map, and
/// a closed set because three parties spell it: the pass raises it, the HTTP surface answers it and the
/// canvas colours it.
///
/// <para>PGM decides a block edit by walking the map's region-filter applications in priority order and
/// taking the first one whose region holds the block and whose filter does not abstain. Nothing matching
/// means the edit stands, so a map grants building by <b>not</b> forbidding it, and the four words below are
/// the four ways that comes out. They are ordered by how much of a decision each one is: a zone the author
/// drew, ground that qualifies by itself, a permission somebody has to satisfy, and a refusal.</para>
/// </summary>
public static class EditZone
{
    /// <summary>Inside a build zone the author drew: no block rule reaches it, so anything may be placed or
    /// broken. This is the map saying <em>here</em>.</summary>
    public const string BuildZone = "build_zone";

    /// <summary>Editable because nothing forbids it — no region the author drew reaches this column and no
    /// rule denies it. On a map that enforces the void these are exactly the columns carrying a block at
    /// y=0, which is the whole of PGM's <c>&lt;void/&gt;</c> test: the terrain is its own permission. On a map
    /// that enforces nothing it is the whole board, which is the same reading and worth seeing.</summary>
    public const string Ground = "ground";

    /// <summary>Editable, but only for somebody and only for something: a spawn admitting its own ore, a wool
    /// room admitting the attacking team and a list of materials. A filter stands between the player and the
    /// block, so this is neither open ground nor a refusal.</summary>
    public const string Filtered = "filtered";

    /// <summary>Nothing may be placed and nothing broken — a blanket <c>never</c>, or the void outside every
    /// build zone with no exception carved for what stands over it.</summary>
    public const string Sealed = "sealed";

    /// <summary>The zones in the order above, which is also the order the legend reads.</summary>
    public static readonly string[] All = [BuildZone, Ground, Filtered, Sealed];

    /// <summary>The legend colour of each zone, shared by the answer and the overlay so the picture and the
    /// numbers cannot disagree. Two greens for the two ways a column is simply editable, amber for a
    /// permission, red for a refusal.</summary>
    public static readonly Dictionary<string, string> Colors = new()
    {
        [BuildZone] = "#4caf50", [Ground] = "#8bc34a", [Filtered] = "#fbc02d", [Sealed] = "#c62828",
    };

    /// <summary>The index of a zone in <see cref="All"/> — the digit a grid row carries.</summary>
    public static int IndexOf(string zone) => Array.IndexOf(All, zone);
}
