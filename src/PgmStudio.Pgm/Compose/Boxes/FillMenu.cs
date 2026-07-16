using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>One row of the width→fill production rule: what an interface of this width reads as, and which
/// fills it makes legal (docs/contracts/map-generation.md §4). Widths are not strictly quantized — a touch
/// tapers to the nearest rung.</summary>
public sealed record FillMenuRow(int WidthCells, string Reads, IReadOnlyList<ShapeFamily> Families, string Note);

/// <summary>
/// The fill menu — the §4 <c>w2/w4/w6</c> table as data: an interface width gates what may fill the box
/// behind it. The reference frame is <c>cell = 5 blocks</c>, <c>lane = 2 cells</c>, so w2 = 1 lane = 10
/// blocks (G2's corridor minimum). A w2 touch is a chokepoint continuing a single lane, which admits every
/// terminal-capped family (each docks through a one-lane entry); w4 is the unstable middle that must resolve
/// (split into lane + build-lane, or twist); w6 is multi-access. The w4/w6 rows resolve into multi-shape
/// patterns, which are not emittable yet — they are recorded so the data does not pretend a wide touch is
/// just a wide lane.
/// </summary>
public static class FillMenu
{
    /// <summary>Families a flush-docked wool box may emit in production today. Two exclusions, each a
    /// named gap, not a taste. (The <b>donut</b> was the third: its attachment stub and tucked room
    /// corner-touch a ring leg as emitted, and the grower's old pairwise contact gate rejected any Corner
    /// verdict — but both touches are ¾-solid inside corners of one connected mass, the mask holds no
    /// diagonal pinch, and once the corner law reads the mask instead of the pair the donut is admitted.)
    /// The <b>scythe</b> carries a bay whose mouth is its own docking edge, so a flush dock seals the bay
    /// against the host into an enclosed void walled by the wool room — exactly WL8's forbidden motif.
    /// Its legal connections are shape-relative (the G80 docking modes, map-generation.md §4): a host on
    /// the entry's unoccupied edge parallel to the entry↔entry-run seam, or across the combined colinear
    /// head edges of entry + entry-run — both survive the entry shift, which carries the dock with it. A
    /// host touching the wool room is a hard violation (reject); the declared-bay alternative is deferred
    /// to the elevation stage (G81: raise the wool so the entry dock is the sole approach, terrain
    /// stepping up entry → room). The <b>clamp</b> is
    /// different: its wool-in-bay is the authored, allowlisted WL8 motif — the bay is a deliberate hole
    /// granting the wool two approaches. What gates it is docking: a fill satisfies exactly one entry
    /// through one interface, forcing the clamp to rotate with the other entry dangling in the void; it
    /// needs both entries satisfied along the short entry edge (a wider interface or two — the G80
    /// docking modes, map-generation.md §4). All three stay fully emittable for harnesses and tests.</summary>
    public static readonly IReadOnlyList<ShapeFamily> ProductionFamilies =
    [
        ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.U, ShapeFamily.H, ShapeFamily.Donut,
    ];

    /// <summary>The §4 table. Row order is part of the deterministic sampling contract (a family draw
    /// resolves against a row's list by index).</summary>
    public static readonly IReadOnlyList<FillMenuRow> Rows =
    [
        new(2, "chokepoint", ProductionFamilies,
            "one lane's touch (10 blocks, G2 minimum) — a single terminal-capped shape docks through it"),
        new(4, "unstable middle", [],
            "two lanes: resolves as 10 terrain + 10 build-lane, or a 20 stub that twists to L/I — a pattern, not a family"),
        new(6, "multi-access", [],
            "three lanes: two 10-strands with a hole / terrain-build-terrain — patterns over several shapes"),
    ];

    /// <summary>The families an interface of <paramref name="widthCells"/> admits (taper to the nearest
    /// rung). Wider rungs return empty until multi-shape patterns are emittable — the caller treats an empty
    /// menu as a directed signal, never a crash.</summary>
    public static IReadOnlyList<ShapeFamily> FamiliesFor(int widthCells)
    {
        var row = Rows.OrderBy(r => Math.Abs(r.WidthCells - widthCells)).ThenBy(r => r.WidthCells).First();
        return row.Families;
    }
}
