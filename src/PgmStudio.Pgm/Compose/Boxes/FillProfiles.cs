using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The per-<see cref="BoxKind"/> fill profile as data: which approach families a box kind admits at a given
/// interface width, and the footprint gate that decides whether a family fits a box. This is the single source
/// the box menu and the footprint budget read, rather than each kind answering for itself in
/// <see cref="FillMenu"/> (wool) and <see cref="SpawnBoxEmitter"/> (spawn). It composes those rather than
/// duplicating them: the <b>wool</b> profile is the §4 width→menu rule (<see cref="FillMenu.FamiliesFor"/>);
/// the <b>spawn</b> profile is the un-escalated {I, L}. Two rows today (wool, spawn) — the hub/frontline rows,
/// and their open-variant patterns, are not rows here. The one filler entry point over this data is
/// <see cref="BoxFiller"/>.
/// </summary>
public static class FillProfiles
{
    /// <summary>The forms box <paramref name="kind"/> may fill at interface width <paramref name="laneWidth"/>
    /// cells: the wool menu gated by width (<see cref="FillMenu.FamiliesFor"/>), the spawn's whole menu. The hub
    /// and the front line fill through their own emitters and are not gated here.</summary>
    public static IReadOnlyList<CompoundForm> Families(BoxKind kind, int laneWidth, int woolLaneWidth) => kind switch
    {
        BoxKind.Wool => FillMenu.FamiliesFor(laneWidth, woolLaneWidth),
        BoxKind.Spawn => FillMenu.For(BoxKind.Spawn),
        _ => [],
    };

    /// <summary>The hub's wall/lane width in cells (the branch and holed forms use it; the solid I
    /// ignores it). One value so the allocator and the filler emit the hub compound at the <b>same</b> width and thus
    /// read the same free-edge intervals — a divergence would seat neighbours on runs the fill does not produce.</summary>
    public const int HubLaneWidthFloor = 2;

    /// <summary>True when <paramref name="family"/> is admitted by <paramref name="kind"/> at
    /// <paramref name="laneWidth"/> <b>and</b> its minimum box fits a footprint of <paramref name="w"/>×
    /// <paramref name="h"/> cells — the profile's footprint gate.</summary>
    public static bool Fits(BoxKind kind, CompoundForm family, int laneWidth, int woolLaneWidth, int w, int h)
    {
        if (!Families(kind, laneWidth, woolLaneWidth).Contains(family)) return false;
        var (minW, minH) = ShapeEmitter.MinBox(family, laneWidth);
        return w >= minW && h >= minH;
    }

    /// <summary>The families <paramref name="kind"/> admits at <paramref name="laneWidth"/> whose minimum box fits a
    /// footprint of <paramref name="w"/>×<paramref name="h"/> cells — the legal fill set for that box.</summary>
    public static IReadOnlyList<CompoundForm> FittingFamilies(BoxKind kind, int laneWidth, int woolLaneWidth, int w, int h) =>
        Families(kind, laneWidth, woolLaneWidth).Where(f => Fits(kind, f, laneWidth, woolLaneWidth, w, h)).ToList();

    /// <summary>
    /// The spawn box's allowed footprints — the <b>size rule as data</b>, the size facet of the profile (the
    /// counterpart to the family allowlist above). A spawn is one of these <b>small fixed SP boxes</b>
    /// (docs/generator/model.md §5 — a spawn is "small … never large", ~10×10 direct · 10×20 run-up ·
    /// 20×20 L), <b>sampled, never stretched to absorb a land-budget share</b>.
    /// Each entry is a <c>(family, run, turn)</c> the emitter turns into the box footprint via
    /// <see cref="SpawnBoxEmitter.Box"/> — edit this table to retune the spawn's size or add a variant; the size
    /// stays a rule, never a solver.
    /// </summary>
    public static readonly IReadOnlyList<(CompoundForm Family, int RunCells, int TurnCells)> SpawnSizes =
    [
        (CompoundForm.I, 0, 0),   // direct — the room sits at the hub edge (~10×10)
        (CompoundForm.I, 2, 0),   // a short run-up back from the hub (~10×20)
        (CompoundForm.L, 2, 2),   // a one-lane L hook (~20×20)
    ];
}
