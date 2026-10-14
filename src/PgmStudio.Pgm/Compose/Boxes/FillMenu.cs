using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>One row of the width→fill production rule: what an interface this many <see cref="Lanes"/> wide
/// reads as, and which fills it makes legal (docs/generator/model.md §2). Widths are not strictly quantized —
/// a touch tapers to the nearest rung.</summary>
public sealed record FillMenuRow(int Lanes, string Reads, IReadOnlyList<CompoundForm> Families, string Note);

/// <summary>
/// The fill menu — the §4 <c>w2/w4/w6</c> table as data: an interface width gates what may fill the box
/// behind it. The rungs are counted in <b>lanes</b>, not cells, so the table reads the same however wide a
/// lane is on the board being composed: a <b>one-lane</b> touch is a chokepoint continuing a single lane,
/// which admits every room-capped family (each docks through a one-lane entry); two lanes is the
/// unstable middle that must resolve (split into lane + build-lane, or twist); three is multi-access. The
/// two- and three-lane rows resolve into multi-shape patterns, which are not emittable yet — they are
/// recorded so the data does not pretend a wide touch is just a wide lane. The names <c>w2/w4/w6</c> are the
/// same three rungs counted in cells at a two-cell lane, which is the narrowest lane the producibility floor
/// admits; the rung a box lands on is computed from the board's own lane width, never from those names.
/// </summary>
public static class FillMenu
{
    /// <summary>The wool box's forms. The scythe is left off: its bay opens onto its own docking edge, so a
    /// flush dock seals the bay against the host into a hole walled by the wool room, the motif WL8 forbids. It
    /// stays emittable for the catalog and the tests.</summary>
    private static readonly IReadOnlyList<CompoundForm> Wool =
    [
        CompoundForm.I, CompoundForm.L, CompoundForm.Z, CompoundForm.U, CompoundForm.H, CompoundForm.Donut, CompoundForm.Clamp,
    ];

    /// <summary>The spawn box's forms: a straight lane or one bend. A spawn never forks or folds.</summary>
    private static readonly IReadOnlyList<CompoundForm> Spawn = [CompoundForm.I, CompoundForm.L];

    /// <summary>The hub's forms. A hub stays rectangle-ish, so its menu is the compact forms plus the wide holed
    /// ones an elongated hub affords; P, Double-hole and G need a width of 9 and are refused below it.</summary>
    private static readonly IReadOnlyList<CompoundForm> Hub =
    [
        CompoundForm.I, CompoundForm.L, CompoundForm.U, CompoundForm.Donut, CompoundForm.P, CompoundForm.DoubleHole, CompoundForm.G,
    ];

    /// <summary>The front line's forms: the wide I across the hub's face, the fat L and the U.</summary>
    private static readonly IReadOnlyList<CompoundForm> Frontline = [CompoundForm.I, CompoundForm.L, CompoundForm.U];

    /// <summary>The forms a box of <paramref name="kind"/> fills with. The order is part of the deterministic
    /// sampling contract: a draw resolves against the list by index.</summary>
    public static IReadOnlyList<CompoundForm> For(BoxKind kind) => kind switch
    {
        BoxKind.Wool => Wool,
        BoxKind.Spawn => Spawn,
        BoxKind.Hub => Hub,
        BoxKind.Frontline => Frontline,
        _ => [],
    };

    /// <summary>The §4 table. Row order is part of the deterministic sampling contract (a family draw
    /// resolves against a row's list by index).</summary>
    public static readonly IReadOnlyList<FillMenuRow> Rows =
    [
        new(2, "chokepoint", Wool,
            "one lane's touch (10 blocks, G2 minimum) — a single room-capped shape docks through it"),
        new(4, "unstable middle", [],
            "two lanes: resolves as 10 terrain + 10 build-lane, or a 20 stub that twists to L/I — a pattern, not a family"),
        new(6, "multi-access", [],
            "three lanes: two 10-strands with a hole / terrain-build-terrain — patterns over several shapes"),
    ];

    /// <summary>The families an interface of <paramref name="widthCells"/> admits where one lane is
    /// <paramref name="woolLaneWidth"/> cells, tapering to the nearest rung. A touch one lane wide is a
    /// chokepoint on any grid; wider rungs return empty until multi-shape patterns are emittable — the caller
    /// treats an empty menu as a directed signal, never a crash.</summary>
    public static IReadOnlyList<CompoundForm> FamiliesFor(int widthCells, int woolLaneWidth)
    {
        var lane = Math.Max(1, woolLaneWidth);
        var row = Rows.OrderBy(r => Math.Abs(r.Lanes * lane - widthCells)).ThenBy(r => r.Lanes).First();
        return row.Families;
    }
}
