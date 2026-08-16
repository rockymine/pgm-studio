namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing pass's own placement rules — the ids its census reasons cite, served by
/// <c>GET /api/rules</c> from the docstrings here the way every gate family's are.</summary>
public static class DressingRules
{
    /// <summary>A prop rests nearer to the road than its kind's standoff allows: a tree 3 blocks, a boulder 2,
    /// measured from its resting cells to the nearest paved cell (Chebyshev; exactly-at-distance stands). The
    /// numbers are each kind's own <see cref="PlacedProp.RouteStandoff"/> — the author's ruling, stated on the
    /// type so there is exactly one place they live.</summary>
    /// <remarks>Move the tree or boulder until its trunk or resting footprint keeps its kind's distance from
    /// the paved edge — measured to the spline the band actually follows, not the drawn polyline. The whole
    /// prop is declined and the census names the offending cell, so the drop can be checked on the canvas.</remarks>
    public const string RoadStandoff = "DR-ROAD";
}

/// <summary>What kind of thing claimed a cell of ground during the dressing pass. The kind is what lets one
/// rule differ by claimant where the rules genuinely differ: a building collides with water and with another
/// building but never with a road (a road is meant to run to its porch), and a prop's standoff is stated
/// against the road rather than against everything.</summary>
public enum ClaimKind
{
    /// <summary>A water channel's bed and beach — carved ground nothing else may take.</summary>
    Water,
    /// <summary>A path's paved cells. The one kind a building ignores, and the one a standoff measures to.</summary>
    Route,
    /// <summary>A raised building's stamped footprint.</summary>
    Structure,
    /// <summary>A seated tree's or boulder's cells — each an exclusion for the props after it.</summary>
    Scatter,
}

/// <summary>
/// The dressing pass's running record of who claimed which cell of ground — the one set every placement
/// checks and joins, carrying <em>what kind of thing</em> claimed each cell rather than the bare fact of a
/// claim. The first claimant of a cell keeps it: the pass places in priority order, so a later claim on a
/// held cell is exactly the collision the placement rules exist to refuse.
/// </summary>
public sealed class GroundClaims
{
    private readonly Dictionary<(int X, int Z), ClaimKind> cells = [];

    public void Claim(int x, int z, ClaimKind kind) => cells.TryAdd((x, z), kind);

    /// <summary>Whether anything at all holds the cell — the occupancy question every prop asks before
    /// resting on it, and the gate that keeps cover from growing through a road or a wall.</summary>
    public bool Holds(int x, int z) => cells.ContainsKey((x, z));

    /// <summary>Whether the cell is held by something other than <paramref name="kind"/> — the building's
    /// question, asked with <see cref="ClaimKind.Route"/>: pavement never blocks a house.</summary>
    public bool HoldsOtherThan(int x, int z, ClaimKind kind) =>
        cells.TryGetValue((x, z), out var held) && held != kind;

    /// <summary>The nearest cell of <paramref name="kind"/> strictly nearer than <paramref name="standoff"/>
    /// blocks (Chebyshev) of the given cell, or null where the standoff is kept. Walked in growing square
    /// rings so the cell named in a refusal is the closest offender, deterministically.</summary>
    public (int X, int Z)? NearerThan(int x, int z, ClaimKind kind, int standoff)
    {
        for (var ring = 0; ring < standoff; ring++)
            for (var dx = -ring; dx <= ring; dx++)
                for (var dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                    var candidate = (x + dx, z + dz);
                    if (cells.TryGetValue(candidate, out var held) && held == kind) return candidate;
                }
        return null;
    }
}
