using PgmStudio.Api.Endpoints;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api;

/// <summary>
/// The rules a studio measures rather than enforces: walks, distances, gap widths and front lines, which
/// describe how a board will play and are the author's to weigh. Each is a <see cref="RuleLevels.Measure"/>
/// unless a setting says otherwise, so its finding never stops the work and is read as the number it
/// measured against its range. <c>docs/refusals.md</c> § <i>Which rules stop the work</i> states the tiers.
/// </summary>
public static class MeasuredRules
{
    /// <summary>Families measured whole: goals, wool, the middle and contact, lanes, front lines, the mid band.</summary>
    private static readonly HashSet<string> Families = ["GO", "WL", "CT", "LN", "FR", "MD"];

    /// <summary>Rules of those families that state a fault in the pieces rather than a measure of the ground.</summary>
    private static readonly HashSet<string> Enforced = [LayoutRules.WoolRoomTouchesSpawn];

    /// <summary>Every rule id the studio measures.</summary>
    public static IEnumerable<string> Measured() =>
        RuleCatalog.Read(RulesEndpoint.Declaring)
            .Where(rule => Families.Contains(rule.Family) && !Enforced.Contains(rule.Rule))
            .Select(rule => rule.Rule);
}
