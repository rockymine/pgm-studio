using PgmStudio.Domain;
using PgmStudio.Analysis.Playability;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

/// <summary>
/// The goals an author has stated, as places on the board.
///
/// <para>A wool, a destroyable, a core and a control point each state where they stand from the moment they
/// are authored, but only some of them reach the map document: a destroyable's region is the box the stamper
/// built its blocks from, so one whose box is not cast yet is left out of the document rather than given a
/// guessed region — which is right for the contract and leaves every read taken before a build blind to that
/// goal. The intent is where those places are, and this is the one reading of it, so the playability reads and
/// the export gate answer over the same set.</para>
///
/// <para><b>A control point is a goal that nobody owns.</b> A wool, a destroyable and a core each belong to
/// the team that must defend them, and the reads use that to let a team walk up to its own goal without
/// standing on it; a hill belongs to whoever is standing on it, so it carries no owner and every team is
/// simply required to reach it. That is the difference the three families and this one have, and it is the
/// whole of it.</para>
/// </summary>
public static class DeclaredGoals
{
    /// <summary>Every wool, destroyable, core and control point the intent states, named and owned the way
    /// the document would name and own them — through the generators' own naming, so a goal the document also
    /// carries is recognised as the same goal rather than counted twice. Spawns are not here: a spawn is in
    /// the document from the start.</summary>
    /// <param name="doc">The map document, read only for the naming a wool's colour falls back to.</param>
    /// <param name="intent">What the author stated, or null on a map that has none.</param>
    public static List<NavPoint> Of(Dictionary<string, object?> doc, MapIntent? intent)
    {
        var goals = new List<NavPoint>();
        if (intent is null) return goals;

        foreach (var wool in intent.Wools ?? [])
            goals.Add(new NavPoint("wool", WoolGenerator.ColorSlug(doc, wool), IntentNaming.TeamId(wool.Owner),
                (int)wool.Spawn.X, (int)wool.Spawn.Z));

        foreach (var destroyable in intent.Destroyables ?? [])
            goals.Add(new NavPoint("destroyable", destroyable.Name, IntentNaming.TeamId(destroyable.Owner),
                (int)destroyable.Anchor.X, (int)destroyable.Anchor.Z));

        foreach (var core in intent.Cores ?? [])
            goals.Add(new NavPoint("core", IntentNaming.TeamId(core.Owner), IntentNaming.TeamId(core.Owner),
                (int)core.Anchor.X, (int)core.Anchor.Z));

        // A point states no height — its pad is cut into whatever ground the build solves under it — so it is
        // seated at the cell's own lowest place rather than at a height nobody wrote.
        var points = intent.ControlPoints ?? [];
        foreach (var (point, name) in points.Zip(ControlPointNaming.Displayed(points.Select(p => p.Name))))
            goals.Add(new NavPoint("point", name, "", (int)point.Anchor.X, (int)point.Anchor.Z));

        return goals;
    }

}
