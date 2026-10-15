using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The id every play piece of an intent carries: a spawn, a wool, a destroyable, a core, a build area, a water
/// lane, an iron cube and an approach wall. A piece is
/// addressed by it wherever one piece is meant — a sketch selection, a move, a place that holds it — so it is
/// stated once and kept through every edit.
///
/// <para>A piece stored without one is given one from what names it: <c>spawn-{team}</c>,
/// <c>wool-{owner}-{colour}</c>, <c>destroyable-{owner}</c>, <c>core-{owner}</c>, the same words the sketch
/// names a room's shape by, and <c>build-{n}</c>, <c>water-lane-{n}</c>, <c>iron-{n}</c>, <c>wall-{n}</c> by
/// position in their list, with <c>-2</c>, <c>-3</c>… where two pieces would share one. An id the intent
/// already states is kept as stated. The observer is the one piece of its kind and is <see cref="Observer"/>.</para>
/// </summary>
public static class PlayIds
{
    /// <summary>The observer's id: a map has one observer, so the id is the word for it.</summary>
    public const string Observer = "observer";

    /// <summary>The intent with every play piece carrying an id: stated ones kept, the rest minted.</summary>
    public static MapIntent Assign(MapIntent intent)
    {
        var taken = new HashSet<string>(Stated(intent), StringComparer.Ordinal);
        string Mint(string stated, string named)
        {
            if (stated.Length > 0) return stated;
            var id = named;
            for (var suffix = 2; !taken.Add(id); suffix++) id = $"{named}-{suffix}";
            return id;
        }

        return intent with
        {
            Spawns = [.. intent.Spawns.Select(spawn => spawn with { Id = Mint(spawn.Id, $"spawn-{spawn.Team}") })],
            Wools = intent.Wools?.Select(wool => wool with
            {
                Id = Mint(wool.Id, wool.Color.Length > 0 ? $"wool-{wool.Owner}-{wool.Color}" : $"wool-{wool.Owner}"),
            }).ToList(),
            Destroyables = intent.Destroyables?.Select(goal => goal with
            {
                Id = Mint(goal.Id, $"destroyable-{goal.Owner}"),
            }).ToList(),
            Cores = intent.Cores?.Select(core => core with { Id = Mint(core.Id, $"core-{core.Owner}") }).ToList(),
            Build = intent.Build is { } build
                ? build with { Areas = [.. build.Areas.Select((area, at) => area with { Id = Mint(area.Id, $"build-{at + 1}") })] }
                : null,
            WaterLanes = intent.WaterLanes is { } lanes
                ? lanes with { Rects = [.. lanes.Rects.Select((lane, at) => lane with { Id = Mint(lane.Id, $"water-lane-{at + 1}") })] }
                : null,
            Structures = intent.Structures is { } structures
                ? structures with
                {
                    IronCubes = [.. structures.IronCubes.Select((cube, at) => cube with { Id = Mint(cube.Id, $"iron-{at + 1}") })],
                    Walls = [.. structures.Walls.Select((wall, at) => wall with { Id = Mint(wall.Id, $"wall-{at + 1}") })],
                }
                : null,
        };
    }

    /// <summary>A finding for every id two play pieces state, under <see cref="RequestRules.Taken"/>.</summary>
    public static IEnumerable<Finding> Duplicates(MapIntent intent, string member = "")
    {
        var prefix = member.Length == 0 ? "" : member + ".";
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, field) in Named(intent))
        {
            if (id.Length == 0) continue;
            if (seen.TryGetValue(id, out var first))
                yield return new Finding(RequestRules.Taken,
                    $"the id '{id}' at {prefix}{field} is already the id of {prefix}{first}", Field: prefix + field,
                    Subjects: [id]);
            else seen[id] = field;
        }
    }

    /// <summary>Every id the intent's pieces state.</summary>
    public static IEnumerable<string> Stated(MapIntent intent) =>
        Named(intent).Select(entry => entry.Id).Where(id => id.Length > 0);

    private static IEnumerable<(string Id, string Field)> Named(MapIntent intent)
    {
        for (var at = 0; at < intent.Spawns.Count; at++) yield return (intent.Spawns[at].Id, $"spawns[{at}].id");
        for (var at = 0; at < (intent.Wools?.Count ?? 0); at++) yield return (intent.Wools![at].Id, $"wools[{at}].id");
        for (var at = 0; at < (intent.Destroyables?.Count ?? 0); at++)
            yield return (intent.Destroyables![at].Id, $"destroyables[{at}].id");
        for (var at = 0; at < (intent.Cores?.Count ?? 0); at++) yield return (intent.Cores![at].Id, $"cores[{at}].id");
        for (var at = 0; at < (intent.Build?.Areas.Count ?? 0); at++)
            yield return (intent.Build!.Areas[at].Id, $"build.areas[{at}].id");
        for (var at = 0; at < (intent.WaterLanes?.Rects.Count ?? 0); at++)
            yield return (intent.WaterLanes!.Rects[at].Id, $"waterLanes.rects[{at}].id");
        for (var at = 0; at < (intent.Structures?.IronCubes.Count ?? 0); at++)
            yield return (intent.Structures!.IronCubes[at].Id, $"structures.ironCubes[{at}].id");
        for (var at = 0; at < (intent.Structures?.Walls.Count ?? 0); at++)
            yield return (intent.Structures!.Walls[at].Id, $"structures.walls[{at}].id");
        if (intent.Observer is not null) yield return (Observer, "observer");
    }
}
