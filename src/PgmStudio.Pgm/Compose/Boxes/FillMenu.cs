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
    /// <summary>Families a flush-docked wool box may emit in production today. Three exclusions, each a
    /// named gap, not a taste: the <b>donut</b>'s attachment stub and tucked room corner-touch a ring leg
    /// as emitted, and the grower's pairwise contact gate (Land-or-None per pair) rejects any Corner
    /// verdict — but both touches are ¾-solid inside corners of one connected mass (a third piece carries
    /// the land interface; the editor's PC-C lint suppresses exactly this case as harmless). The real
    /// corner failure is the cell-level diagonal pinch — two tiles meeting only at a point with void or
    /// build zone on both opposite diagonals — and the donut's mask has none, so the family waits on the
    /// authoring gate learning to read the mask instead of the pair, not on a geometry variant. The
    /// <b>scythe</b> and the <b>clamp</b> carry a bay
    /// whose mouth is their own docking edge, so a flush dock seals the bay against the host into an
    /// enclosed void walled by the wool room — exactly WL8's forbidden motif. The emitter's entry shift
    /// takes the tail OFF the mouth row, so a shifted scythe cannot flush-dock at all: production entry
    /// waits for a host that wraps the box corner (a side-edge dock) or for sealed bays to be declarable
    /// as holes. All three stay fully emittable for harnesses and tests.</summary>
    public static readonly IReadOnlyList<ShapeFamily> ProductionFamilies =
    [
        ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.U, ShapeFamily.H,
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
