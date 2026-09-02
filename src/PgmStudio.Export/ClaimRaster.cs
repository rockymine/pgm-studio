using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Export;

/// <summary>
/// What the dressing pass would say about every cell of a board, read off its own claims rather than guessed
/// at: a prop's footprint where one landed, the keep-out holding a cell nothing claims, or the ground both
/// leave free — the same question <see cref="Decorator"/> asks one cell at a time while it places a prop,
/// turned into a grid a preview can show whole. A placement is looked up on this raster rather than tried.
///
/// <para>A cell's class is decided in the order the pass itself asks it. A claim, once made, is final — the
/// dressing pass never asks a keep-out or a clearance about ground it already covered — so a claimed cell
/// always shows the claim. An unclaimed cell is read the way <see cref="Decorator"/>'s own seating asks it:
/// a goal's clearance before the map's keep-out mask, which is what lets an author reading this raster find
/// the rule a placement beside a goal would actually be refused under.</para>
/// </summary>
public static class ClaimRaster
{
    public const int Free = 0, Water = 1, Route = 2, Structure = 3, Tree = 4, Boulder = 5, Flora = 6,
        SpawnKeepOut = 7, DoorApproach = 8, GoalClearance = 9, WoolRoomKeepOut = 10, StructureKeepOut = 11;

    /// <summary>The class every cell can be, indexed by the digit it prints as: <c>0</c>–<c>9</c> then
    /// <c>a</c>, <c>b</c>.</summary>
    public static readonly IReadOnlyList<string> Classes =
    [
        "free", "water", "route", "structure", "tree", "boulder", "flora",
        "spawn keep-out", "door approach", "goal clearance", "wool-room keep-out", "structure keep-out",
    ];

    /// <summary>The character one class prints as — the digits through <c>9</c>, then <c>a</c>, <c>b</c>, the
    /// same base <see cref="Classes"/> is ordered by.</summary>
    public static char Digit(int classIndex) =>
        classIndex < 10 ? (char)('0' + classIndex) : (char)('a' + classIndex - 10);

    /// <summary>Rows over the board's own surface — its bounding box, one character per cell, void outside
    /// the surface as a space. <see cref="Rows"/> runs <paramref name="MinZ"/> to its far edge, each row
    /// <paramref name="MinX"/> to its far edge.</summary>
    public sealed record Grid(int MinX, int MinZ, int Width, int Height, IReadOnlyList<string> Rows);

    /// <summary>Classify every cell of <paramref name="surface"/>: the prop that claims it first, then a
    /// goal's clearance, then the map's keep-out mask, then free — the order <see cref="Decorator"/> asks a
    /// candidate site the same three questions in, so a reader finds the rule a placement would actually be
    /// refused under.</summary>
    public static Grid Read(
        IReadOnlyList<PlacementClaim> placements, IReadOnlyDictionary<(int X, int Z), int> surface,
        Func<int, int, KeepOut?> keptClearAt, Func<int, int, bool> goalClearanceAt)
    {
        if (surface.Count == 0) return new Grid(0, 0, 0, 0, []);
        var minX = surface.Keys.Min(cell => cell.X);
        var maxX = surface.Keys.Max(cell => cell.X);
        var minZ = surface.Keys.Min(cell => cell.Z);
        var maxZ = surface.Keys.Max(cell => cell.Z);
        var width = maxX - minX + 1;
        var claimed = ClaimedClasses(placements);

        var rows = new List<string>(maxZ - minZ + 1);
        for (var z = minZ; z <= maxZ; z++)
        {
            var row = new char[width];
            for (var x = minX; x <= maxX; x++)
                row[x - minX] = surface.ContainsKey((x, z))
                    ? Digit(ClassAt((x, z), claimed, keptClearAt, goalClearanceAt))
                    : ' ';
            rows.Add(new string(row));
        }
        return new Grid(minX, minZ, width, maxZ - minZ + 1, rows);
    }

    private static int ClassAt(
        (int X, int Z) cell, IReadOnlyDictionary<(int X, int Z), int> claimed,
        Func<int, int, KeepOut?> keptClearAt, Func<int, int, bool> goalClearanceAt)
    {
        if (claimed.TryGetValue(cell, out var claimedClass)) return claimedClass;
        if (goalClearanceAt(cell.X, cell.Z)) return GoalClearance;
        return keptClearAt(cell.X, cell.Z) switch
        {
            KeepOut.Spawn => SpawnKeepOut,
            KeepOut.Approach => DoorApproach,
            KeepOut.WoolRoom => WoolRoomKeepOut,
            KeepOut.Structure or KeepOut.Built => StructureKeepOut,
            _ => Free,
        };
    }

    /// <summary>Every claim's own class, keyed by the cells it covers — a <see cref="ProvenancePass.Structure"/>
    /// claim is <see cref="Structure"/> whatever placed it, and everything else is read off its
    /// <see cref="Domain.StampId.Kind"/>. Later claims overwrite earlier ones, the same rule
    /// <see cref="WorldProvenance"/> composites a world by.</summary>
    private static Dictionary<(int X, int Z), int> ClaimedClasses(IReadOnlyList<PlacementClaim> placements)
    {
        var claimed = new Dictionary<(int X, int Z), int>();
        foreach (var claim in placements)
        {
            var claimedClass = claim.Pass == ProvenancePass.Structure ? Structure : claim.Owner.Kind switch
            {
                "water" => Water,
                "stroke" => Route,
                "tree" => Tree,
                "boulder" => Boulder,
                "flora" => Flora,
                _ => Structure,
            };
            foreach (var cell in claim.Cells) claimed[cell] = claimedClass;
        }
        return claimed;
    }
}
