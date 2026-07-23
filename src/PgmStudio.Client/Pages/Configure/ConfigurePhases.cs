namespace PgmStudio.Client.Pages.Configure;

/// <summary>A Configure-wizard phase + its steps (mirrors new-map-authoring.md §12 / the concept page).
/// <c>Task</c> is the N-series task that builds the phase's real body.</summary>
public record ConfigurePhase(string Id, string Icon, string Title, string[] Steps, string Task);

public static class ConfigurePhases
{
    // The six phases on the activity rail, in linear order. Steps mirror the concept sections;
    // a phase with no steps (Map Info) is a single-step form (the flow bar shows just its name).
    public static readonly ConfigurePhase[] All =
    [
        new("info",   "book-open-text", "Map Info",        [],                                                "N00"),
        new("world",  "settings-2",     "World",           ["Scan", "Islands", "Symmetry"],                   "N01"),
        new("teams",  "users",          "Teams",           ["Teams & islands", "Spawn point", "Protection"],  "N02"),
        new("build",  "pickaxe",        "Build",           ["Build height", "Buildable layer"],               "N03"),
        new("wools",  "goal",           "Wools",           ["Objectives", "Spawn", "Monuments", "Room"],      "N04"),
        new("review", "badge-check",    "Review & Export", ["Pre-flight", "Region tree", "XML"],              "N05"),
    ];

    public static int IndexOf(string id) => Array.FindIndex(All, p => p.Id == id);
}
