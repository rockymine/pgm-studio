using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// The <c>&lt;score&gt;</c> a board ends at, what a kill pays, and the score boxes it pays through. Without one PGM
/// builds no score module and nothing a point, a flag, a box or a kill pays is counted, so a board whose objectives
/// pay states one. One slice writes it because everything that pays pays into it.
/// </summary>
public static class ScoreGenerator
{
    private const string BoxRegionSuffix = "score-box";

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        var boxes = intent.ScoreBoxes ?? [];
        var limit = Limit(intent);
        var kills = intent.KillPoints is > 0 ? intent.KillPoints : null;
        if (limit is null && boxes.Count == 0 && kills is null) return;

        var score = new Dict();
        if (limit is { } stated) score["limit"] = (long)stated;
        if (kills is { } paid) score["kills"] = (long)paid;
        if (boxes.Count > 0) score["boxes"] = Boxes(doc, intent, boxes);
        doc["score"] = score;
    }

    /// <summary>
    /// The limit the match ends at: the author's where they state one, the corpus's default on a board that
    /// carries a point that pays, a team's flag or a flag that pays, and none at all otherwise. Zero is an author asking for no limit — a
    /// board played to a time limit instead — and is honoured rather than replaced.
    /// </summary>
    private static int? Limit(MapIntent intent)
    {
        if (intent.ScoreLimit is { } stated) return stated > 0 ? stated : null;
        if (intent.ControlPoints?.Any(p => p.CaptureBox is not null && p.Points > 0) ?? false)
            return ObjectiveDefaults.ControlPointScoreLimit;
        if (intent.Flags?.Any(flag => flag.Posts.Count > 0 && flag.Owned) ?? false)
            return ObjectiveDefaults.CtfScoreLimit;
        if (intent.Flags?.Any(flag => flag.Posts.Count > 0 && flag.PaysPerSecond > 0) ?? false)
            return ObjectiveDefaults.FlagScoreLimit;
        return null;
    }

    // Each box is a cuboid region filtered to its team — a team id is itself the filter matching its members —
    // and, sending its scorer home, a portal on the same region landing on the team's spawn point at its yaw.
    private static List<object?> Boxes(Dict doc, MapIntent intent, List<ScoreBoxIntent> boxes)
    {
        var written = new List<object?>();
        var portals = ObjectiveRegion.List(doc, "portals");
        foreach (var (box, i) in boxes.Select((box, i) => (box, i)))
        {
            var team = IntentNaming.TeamId(box.Team);
            var regionId = ObjectiveRegion.Emit(doc, $"{IntentNaming.Slug(box.Team)}-{i + 1}", box.Box, BoxRegionSuffix);
            written.Add(new Dict { ["region"] = regionId, ["points"] = (long)box.Points, ["filter"] = team });

            if (!box.Home || intent.Spawns.FirstOrDefault(spawn => IntentNaming.TeamId(spawn.Team) == team) is not { } spawn)
                continue;
            portals.Add(new Dict
            {
                ["region"] = regionId,
                ["destination"] = $"{IntentNaming.Slug(spawn.Team)}-spawn-point",
                ["yaw"] = "@" + spawn.Yaw.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["filter"] = team,
            });
        }
        return written;
    }

    // A rebuild replaces what the last one wrote: the score, the box regions and the portals on them.
    private static void Clear(Dict doc)
    {
        var regions = DocAccess.Regions(doc);
        var boxRegions = ((doc.GetValueOrDefault("score") as Dict)?.GetValueOrDefault("boxes") as List<object?> ?? [])
            .OfType<Dict>().Select(box => box.GetValueOrDefault("region") as string).OfType<string>().ToHashSet();
        foreach (var id in boxRegions) regions.Remove(id);
        if (doc.GetValueOrDefault("portals") is List<object?> portals)
            portals.RemoveAll(portal => portal is Dict d && d.GetValueOrDefault("region") as string is { } r && boxRegions.Contains(r));
        doc.Remove("score");
    }
}
