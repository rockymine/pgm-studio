using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// The zones an author states (<see cref="MapIntent.Zones"/>), written through the one writer every zone goes
/// through (<see cref="ZoneWriter"/>): a region of the zone's rects and an apply rule for each thing it says.
/// Before the build slice, because PGM stops at the first rule that decides and a zone's "no placing" has to
/// decide inside a build region too.
/// </summary>
public static class ZoneGenerator
{
    /// <summary>The prefix of every region a stated zone writes.</summary>
    public const string RegionPrefix = "zone-";
    private const string EnterMessage = "You may not enter this area!";
    private const string EditMessage = "You may not edit this area!";

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        foreach (var zone in intent.Zones ?? [])
        {
            if (zone.Area.Count == 0) continue;
            var team = zone.Team.Length > 0 ? IntentNaming.TeamId(zone.Team) : null;
            var id = RegionPrefix + IntentNaming.Slug(zone.Id) + (team is null ? "" : $"-{IntentNaming.Slug(zone.Team)}");
            ZoneWriter.Region(doc, id, "zone", zone.Area);

            var enter = zone.Enter switch
            {
                ZoneEntries.Nobody => "never",
                ZoneEntries.Team when team is not null => team,
                _ => null,
            };
            var edits = !zone.Place || !zone.Break;
            ZoneWriter.Rules(doc, id, new ZoneRules
            {
                Enter = enter,
                EnterMessage = enter is null ? null : zone.EnterMessage.Length > 0 ? zone.EnterMessage : EnterMessage,
                BlockPlace = zone.Place ? null : "never",
                BlockBreak = zone.Break ? null : "never",
                EditMessage = !edits ? null : zone.EditMessage.Length > 0 ? zone.EditMessage : EditMessage,
                Kit = zone.Kit.Length > 0 ? zone.Kit : null,
                Harmless = !zone.Harm,
            });
        }
    }

    // A rebuild replaces what the last one wrote: every zone region, the rules over them, and the damage filters a
    // harmless zone added.
    private static void Clear(Dict doc)
    {
        static bool Mine(string? id) => id is not null && id.StartsWith(RegionPrefix, StringComparison.Ordinal);
        var regions = DocAccess.Regions(doc);
        foreach (var id in regions.Keys.Where(Mine).ToList()) regions.Remove(id);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(rule => rule is Dict d && Mine(d.GetValueOrDefault("region") as string));

        var harmless = ZoneWriter.HarmlessPrefix + RegionPrefix;
        var filters = DocAccess.Filters(doc);
        foreach (var id in filters.Keys.Where(id => id.StartsWith(harmless, StringComparison.Ordinal)
                                                    || id.StartsWith("__in-" + RegionPrefix, StringComparison.Ordinal)).ToList())
            filters.Remove(id);
        if (doc.GetValueOrDefault("damage") is Dict damage && damage.GetValueOrDefault("filters") is List<object?> denies)
        {
            denies.RemoveAll(filter => filter is string id && id.StartsWith(harmless, StringComparison.Ordinal));
            if (denies.Count == 0 && damage.Count == 1) doc.Remove("damage");
        }
    }
}
