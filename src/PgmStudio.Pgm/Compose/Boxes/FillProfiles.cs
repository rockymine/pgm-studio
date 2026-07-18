using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The per-<see cref="BoxKind"/> fill profile as data: which approach families a box kind admits at a given
/// interface width, and the footprint gate that decides whether a family fits a box. This is the single source
/// the box menu and the footprint budget read, in place of the per-kind logic that used to be scattered across
/// <see cref="FillMenu"/> (wool) and <see cref="SpawnBoxEmitter"/> (spawn). It composes those rather than
/// duplicating them: the <b>wool</b> profile is the §4 width→menu rule (<see cref="FillMenu.FamiliesFor"/>);
/// the <b>spawn</b> profile is the un-escalated {I, L}. Two rows today (wool, spawn) — the hub/frontline rows,
/// and their open-variant patterns, land at G41-C. The one filler entry point over this data is
/// <see cref="BoxFiller"/>.
/// </summary>
public static class FillProfiles
{
    /// <summary>The families box <paramref name="kind"/> may fill at interface width <paramref name="cw"/>
    /// cells. Wool = the width-gated production menu; spawn = the straight/one-bend {I, L}. The hub is not here:
    /// it is a <b>terminal-free body</b>, so its profile is a <see cref="Compound"/> menu (<see cref="HubForms"/>),
    /// not the <see cref="ShapeFamily"/> approach taxonomy.</summary>
    public static IReadOnlyList<ShapeFamily> Families(BoxKind kind, int cw) => kind switch
    {
        BoxKind.Wool => FillMenu.FamiliesFor(cw),
        BoxKind.Spawn => SpawnBoxEmitter.Families,
        _ => [],                                          // hub = HubForms (Compound-typed); frontline lands at G89
    };

    /// <summary>The <b>hub's</b> fill profile — its authored form menu as data (the <see cref="Compound"/> bodies
    /// a hub may be, map-generation.md §5.5). Compound-typed, not <see cref="ShapeFamily"/>, because the hub is a
    /// terminal-free body rather than an approach; it composes <see cref="HubBoxEmitter.Forms"/> the way the
    /// wool/spawn rows compose their menus.</summary>
    public static IReadOnlyList<CompoundRead> HubForms => HubBoxEmitter.Forms;

    /// <summary>True when <paramref name="family"/> is admitted by <paramref name="kind"/> at
    /// <paramref name="cw"/> <b>and</b> its minimum box fits a footprint of <paramref name="w"/>×
    /// <paramref name="h"/> cells — the profile's footprint gate.</summary>
    public static bool Fits(BoxKind kind, ShapeFamily family, int cw, int w, int h)
    {
        if (!Families(kind, cw).Contains(family)) return false;
        var (minW, minH) = ShapeEmitter.MinBox(family, cw);
        return w >= minW && h >= minH;
    }

    /// <summary>The families <paramref name="kind"/> admits at <paramref name="cw"/> whose minimum box fits a
    /// footprint of <paramref name="w"/>×<paramref name="h"/> cells — the legal fill set for that box.</summary>
    public static IReadOnlyList<ShapeFamily> FittingFamilies(BoxKind kind, int cw, int w, int h) =>
        Families(kind, cw).Where(f => Fits(kind, f, cw, w, h)).ToList();

    /// <summary>
    /// The spawn box's allowed footprints — the <b>size rule as data</b>, the size facet of the profile (the
    /// counterpart to the family allowlist above). A spawn is one of these <b>small fixed SP boxes</b>
    /// (docs/contracts/map-generation.md §4 — a spawn is "small … never large", ~10×10 direct · 10×20 run-up ·
    /// 20×20 L), <b>sampled, never stretched to absorb a land-budget share</b> the way the ported grower did.
    /// Each entry is a <c>(family, run, turn)</c> the emitter turns into the box footprint via
    /// <see cref="SpawnBoxEmitter.Box"/> — edit this table to retune the spawn's size or add a variant; the size
    /// stays a rule, never a solver.
    /// </summary>
    public static readonly IReadOnlyList<(ShapeFamily Family, int RunCells, int TurnCells)> SpawnSizes =
    [
        (ShapeFamily.I, 0, 0),   // direct — the room sits at the hub edge (~10×10)
        (ShapeFamily.I, 2, 0),   // a short run-up back from the hub (~10×20)
        (ShapeFamily.L, 2, 2),   // a one-lane L hook (~20×20)
    ];

    /// <summary>The land (cells) a spawn <paramref name="size"/> occupies at corridor width <paramref name="cw"/>
    /// — its footprint, for the budget accounting. The spawn no longer claims a weighted share; it spends only
    /// this small fixed amount, and the freed budget is what stops the maps being crammed.</summary>
    public static int SpawnLand((ShapeFamily Family, int RunCells, int TurnCells) size, int cw)
    {
        var (w, h) = SpawnBoxEmitter.Box(size.Family, cw, size.RunCells, size.TurnCells);
        return w * h;
    }
}
