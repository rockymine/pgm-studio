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
    /// cells. Wool = the width-gated production menu; spawn = the straight/one-bend {I, L}.</summary>
    public static IReadOnlyList<ShapeFamily> Families(BoxKind kind, int cw) => kind switch
    {
        BoxKind.Wool => FillMenu.FamiliesFor(cw),
        BoxKind.Spawn => SpawnBoxEmitter.Families,
        _ => [],                                          // hub / frontline / mid profiles land at G41-C
    };

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
}
