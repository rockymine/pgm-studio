using System.Text.Json.Nodes;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;

/// <summary>
/// The teams an objective follows from the islands it stands on (docs/tools/configure.md, "Objectives follow the
/// islands"). A wool is defended by the team whose island it spawns on and captured, at each monument, by the
/// team whose island the monument stands on; a core or a destroyable is owned by the team whose island it stands
/// on. Reassigning islands after the objectives were confirmed leaves them with the old teams, so a step that
/// changes island teams asks this what no longer agrees and offers to bring the objectives along.
/// </summary>
internal static class ObjectiveTeams
{
    /// <summary>What <see cref="Reassign"/> would change, one sentence each; empty where every objective agrees
    /// with its island or stands where no island has a team.</summary>
    public static List<string> Disagreements(JsonObject intent, List<IslandDto> islands, Dictionary<string, string> islandTeams) =>
        Reassign((JsonObject)intent.DeepClone(), islands, islandTeams);

    /// <summary>Give each objective in <paramref name="intent"/> the team its island has, and answer what changed.
    /// A wool's monuments are re-teamed against its new defender: a monument takes its island's team, and on a
    /// two-team map one standing where no island has a team takes the one team that captures.</summary>
    public static List<string> Reassign(JsonObject intent, List<IslandDto> islands, Dictionary<string, string> islandTeams)
    {
        var changed = new List<string>();
        var teams = (intent["teams"] as JsonArray)?.OfType<JsonObject>().Select(t => Ctx.S(t, "id")).ToList() ?? [];
        string? At(JsonObject? point) =>
            point is null ? null : AuthoringContext.IslandTeamAt(Ctx.D(point, "x"), Ctx.D(point, "z"), islands, islandTeams);

        foreach (var wool in (intent["wools"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var color = Ctx.S(wool, "color");
            var owner = Ctx.S(wool, "owner");
            if (At(wool["spawn"] as JsonObject) is { } defender && defender != owner)
            {
                changed.Add($"The {color} wool is defended by {defender}, not {owner}.");
                wool["owner"] = owner = defender;
            }
            var capturers = teams.Where(team => team != owner).ToList();
            foreach (var monument in (wool["monuments"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var team = Ctx.S(monument, "team");
                var placing = At(monument["location"] as JsonObject)
                    ?? (capturers.Count == 1 ? capturers[0] : null);
                if (placing is null || placing == team || placing == owner) continue;
                changed.Add($"The {color} wool's monument is where {placing} places it, not {team}.");
                monument["team"] = placing;
            }
        }
        foreach (var (key, kind) in new[] { ("cores", "core"), ("destroyables", "destroyable") })
            foreach (var goal in (intent[key] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var owner = Ctx.S(goal, "owner");
                if (At(goal["anchor"] as JsonObject) is { } island && island != owner)
                {
                    changed.Add($"A {kind} of {owner} stands on {island}'s island.");
                    goal["owner"] = island;
                }
            }
        return changed;
    }
}
