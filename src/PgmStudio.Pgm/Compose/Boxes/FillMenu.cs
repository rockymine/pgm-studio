using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>One row of the width→fill production rule: what an interface this many <see cref="Lanes"/> wide
/// reads as, and which fills it makes legal (docs/generator/model.md §2). Widths are not strictly quantized —
/// a touch tapers to the nearest rung.</summary>
public sealed record FillMenuRow(int Lanes, string Reads, IReadOnlyList<ShapeFamily> Families, string Note);

/// <summary>
/// The fill menu — the §4 <c>w2/w4/w6</c> table as data: an interface width gates what may fill the box
/// behind it. The rungs are counted in <b>lanes</b>, not cells, so the table reads the same however wide a
/// lane is on the board being composed: a <b>one-lane</b> touch is a chokepoint continuing a single lane,
/// which admits every terminal-capped family (each docks through a one-lane entry); two lanes is the
/// unstable middle that must resolve (split into lane + build-lane, or twist); three is multi-access. The
/// two- and three-lane rows resolve into multi-shape patterns, which are not emittable yet — they are
/// recorded so the data does not pretend a wide touch is just a wide lane. The names <c>w2/w4/w6</c> are the
/// same three rungs counted in cells at a two-cell lane, which is the narrowest lane the producibility floor
/// admits; the rung a box lands on is computed from the board's own lane width, never from those names.
/// </summary>
public static class FillMenu
{
    /// <summary>Families a flush-docked wool box may emit in production today. One exclusion, a named gap,
    /// not a taste. (The <b>donut</b> was excluded once: its attachment stub and tucked room corner-touch a
    /// ring leg as emitted, and the grower's old pairwise contact gate rejected any Corner verdict — but
    /// both touches are ¾-solid inside corners of one connected mass, the mask holds no diagonal pinch, and
    /// once the corner law reads the mask instead of the pair the donut is admitted. The <b>clamp</b> was
    /// excluded while it was defined as a dual-host wool: now its two legs meet the host on one edge and the
    /// wool is clamped inside as a cut cell, so it docks through a single mouth like the U — admitted.)
    /// The <b>scythe</b> carries a bay whose mouth is its own docking edge, so a flush dock seals the bay
    /// against the host into an enclosed void walled by the wool room — exactly WL8's forbidden motif.
    /// Its legal connections are shape-relative (the G80 docking modes, map-generation.md §4): a host on
    /// the entry's unoccupied edge parallel to the entry↔entry-run seam, or across the combined colinear
    /// head edges of entry + entry-run — both survive the entry shift, which carries the dock with it. A
    /// host touching the wool room is a hard violation (reject); the declared-bay alternative is deferred
    /// to the elevation stage (G81: raise the wool so the entry dock is the sole approach, terrain
    /// stepping up entry → room). It stays fully emittable for harnesses and tests.</summary>
    public static readonly IReadOnlyList<ShapeFamily> ProductionFamilies =
    [
        ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.U, ShapeFamily.H, ShapeFamily.Donut, ShapeFamily.Clamp,
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

    /// <summary>The families an interface of <paramref name="widthCells"/> admits where one lane is
    /// <paramref name="laneCells"/> cells, tapering to the nearest rung. A touch one lane wide is a
    /// chokepoint on any grid; wider rungs return empty until multi-shape patterns are emittable — the caller
    /// treats an empty menu as a directed signal, never a crash.</summary>
    public static IReadOnlyList<ShapeFamily> FamiliesFor(int widthCells, int laneCells)
    {
        var lane = Math.Max(1, laneCells);
        var row = Rows.OrderBy(r => Math.Abs(r.Lanes * lane - widthCells)).ThenBy(r => r.Lanes).First();
        return row.Families;
    }
}
