using PgmStudio.Analysis.Playability;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

/// <summary>
/// The goals an author has stated, as places on the board.
///
/// <para>A wool, a destroyable and a core each state where they stand from the moment they are authored, but
/// only some of them reach the map document: a destroyable's region is the box the stamper built its blocks
/// from, so one whose box is not cast yet is left out of the document rather than given a guessed region —
/// which is right for the contract and leaves every read taken before a build blind to that goal. The intent
/// is where those places are, and this is the one reading of it, so the playability reads and the export gate
/// answer over the same set.</para>
/// </summary>
internal static class DeclaredGoals
{
    /// <summary>Every wool, destroyable and core the intent states, named and owned the way the document
    /// would name and own them. Spawns are not here: a spawn is in the document from the start.</summary>
    public static List<NavPoint> Of(MapIntent? intent)
    {
        var goals = new List<NavPoint>();
        if (intent is null) return goals;

        foreach (var wool in intent.Wools ?? [])
            goals.Add(new NavPoint("wool", Colour(wool), IntentNaming.TeamId(wool.Owner),
                (int)wool.Spawn.X, (int)wool.Spawn.Z));

        foreach (var destroyable in intent.Destroyables ?? [])
            goals.Add(new NavPoint("destroyable", destroyable.Name, IntentNaming.TeamId(destroyable.Owner),
                (int)destroyable.Anchor.X, (int)destroyable.Anchor.Z));

        foreach (var core in intent.Cores ?? [])
            goals.Add(new NavPoint("core", IntentNaming.TeamId(core.Owner), IntentNaming.TeamId(core.Owner),
                (int)core.Anchor.X, (int)core.Anchor.Z));

        return goals;
    }

    /// <summary>A wool's colour as the document spells it — the owner team's own colour where the author
    /// stated none, which is the rule the wool slice writes under.</summary>
    private static string Colour(WoolIntent wool)
        => wool.Color.Length > 0 ? wool.Color : IntentNaming.Slug(wool.Owner);
}
