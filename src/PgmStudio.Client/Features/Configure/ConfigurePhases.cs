namespace PgmStudio.Client.Features.Configure;

/// <summary>A Configure-wizard phase + its steps (mirrors new-map-authoring.md §12 / the concept page).
/// <c>Task</c> is the N-series task that builds the phase's real body.</summary>
public record ConfigurePhase(string Id, string Icon, string Title, string[] Steps, string Task);

public static class ConfigurePhases
{
    /// <summary>
    /// The phases on the activity rail, in linear order. Steps mirror the concept sections; a phase with no
    /// steps (Identity) is a single-step form (the flow bar shows just its name).
    /// <para>
    /// <b>The objective phases are a group, not a choice.</b> A PGM map may carry wools, destroyables and
    /// cores at once, in any combination, so each objective kind gets its own phase and every one of them is
    /// offered on every map — an author adds a gamemode by filling a phase in, and a map that arrived with
    /// one gamemode is not barred from a second. They share a single completeness gate
    /// (<see cref="IsObjective"/>): the map needs <i>an</i> objective, not one of each.
    /// </para>
    /// </summary>
    public static readonly ConfigurePhase[] All =
    [
        new("info",   "book-open-text", "Identity",        [],                                                "N00"),
        new("world",  "settings-2",     "World",           ["Scan", "Islands", "Symmetry"],                   "N01"),
        new("teams",  "users",          "Teams",           ["Teams & islands", "Spawn point", "Protection"],  "N02"),
        new("build",  "pickaxe",        "Build",           ["Build height", "Buildable layer"],               "N03"),
        new("wools",  "goal",           "Wools",           ["Objectives", "Spawn", "Monuments", "Room"],      "N04"),
        new("cores",  "flame",          "Cores",           ["Objectives", "Casing"],                          "N12"),
        new("review", "badge-check",    "Review & Export", ["Pre-flight", "Region tree", "XML"],              "N05"),
    ];

    /// <summary>The intent keys the objective phases author, in rail order. Every one of them contributes to
    /// the shared objective gate, so a DTC map is never held up by an empty wool slice.</summary>
    public static readonly string[] ObjectiveSlices = ["wools", "destroyables", "cores"];

    /// <summary>Whether a phase authors one of the map's objectives (and so shares the objective gate).</summary>
    public static bool IsObjective(string phaseId) => phaseId is "wools" or "cores";

    public static int IndexOf(string id) => Array.FindIndex(All, p => p.Id == id);
}
