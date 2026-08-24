namespace PgmStudio.Domain;

/// <summary>The outward direction a room wall faces: the wall on the low-Z side faces <see cref="NegZ"/>.
/// Also the direction a spawn cube's door opens toward.</summary>
public enum RoomEdge { NegZ, PosZ, NegX, PosX }

/// <summary>
/// What an edge <em>is</em>, asked once rather than re-derived at every wall that needs it: which axis a wall
/// on it runs along, which side of a rectangle it names, and which way it looks.
///
/// <para>These are facts about the direction and nothing else, so they belong beside the enum rather than on
/// any one type that happens to carry one. The alternative is what was here before — a four-arm switch
/// wherever an outward step was wanted and an inline <c>is NegZ or PosZ</c> wherever an axis was, in the
/// stamper, the frame resolver, the plan splitter and the dressing symmetry alike, none of them able to be
/// wrong in a way the others would notice.</para>
/// </summary>
public static class RoomEdges
{
    /// <summary>The edge a spawn's door stands on, from the facing word a plan states it with. The words are
    /// relative to the piece's own front, which is <c>−z</c>: <c>front</c> (or anything unrecognised),
    /// <c>back</c>, <c>left</c>, <c>right</c>.</summary>
    public static RoomEdge OfFacing(string? facing) => facing switch
    {
        "back" => RoomEdge.PosZ,
        "left" => RoomEdge.NegX,
        "right" => RoomEdge.PosX,
        _ => RoomEdge.NegZ,
    };

    /// <summary>The edge's own name, for a document that has to carry which side a door is on.</summary>
    public static string Word(this RoomEdge edge) => edge switch
    {
        RoomEdge.NegZ => "-z",
        RoomEdge.PosZ => "+z",
        RoomEdge.NegX => "-x",
        _ => "+x",
    };

    /// <summary>An edge back from <see cref="Word"/>, or null where the word names none.</summary>
    public static RoomEdge? OfWord(string? word) => word switch
    {
        "-z" => RoomEdge.NegZ,
        "+z" => RoomEdge.PosZ,
        "-x" => RoomEdge.NegX,
        "+x" => RoomEdge.PosX,
        _ => null,
    };

    /// <summary>Whether a <b>wall</b> on this edge runs east–west, so its along axis is x and the line it
    /// stands on is a z. That is the axis of the wall, not of the edge: a wall facing ±z runs along x, which
    /// is the opposite of what the edge's own name reads as, and is the whole reason this is asked in one
    /// place.</summary>
    public static bool AlongX(this RoomEdge edge) => edge is RoomEdge.NegZ or RoomEdge.PosZ;

    /// <summary>Whether the edge names the high side of its axis — the +x or +z wall, the one standing on a
    /// rectangle's max line.</summary>
    public static bool Positive(this RoomEdge edge) => edge is RoomEdge.PosZ or RoomEdge.PosX;

    /// <summary>The step from a cell of a wall on this edge to the cell outside the building — out into the
    /// weather, the direction the wall's face looks.</summary>
    public static (int X, int Z) Outward(this RoomEdge edge) => edge switch
    {
        RoomEdge.NegZ => (0, -1),
        RoomEdge.PosZ => (0, 1),
        RoomEdge.NegX => (-1, 0),
        _ => (1, 0),
    };

    /// <summary>The step from a cell of a wall on this edge to the cell behind it — into the building. What
    /// something standing against the wall rather than in it is offset by.</summary>
    public static (int X, int Z) Inward(this RoomEdge edge)
    {
        var (x, z) = edge.Outward();
        return (-x, -z);
    }

    /// <summary>The edge across the room from this one: the wall a door on this side is walked toward, and
    /// the way something hung on this wall has to look to be seen.</summary>
    public static RoomEdge Opposite(this RoomEdge edge) => edge switch
    {
        RoomEdge.NegZ => RoomEdge.PosZ,
        RoomEdge.PosZ => RoomEdge.NegZ,
        RoomEdge.NegX => RoomEdge.PosX,
        _ => RoomEdge.NegX,
    };
}
