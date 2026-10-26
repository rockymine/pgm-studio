using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>What the Decoration phase calls each prop kind: its glyph, its name for one, the heading its placements
/// are listed under, and the gesture that places one. The list and the inspector read the same row, so a prop is
/// named the same wherever it is shown.</summary>
public static class PropKindNames
{
    public sealed record Names(string Icon, string One, string Many, string Gesture);

    /// <summary>Every kind, in the order the Placed list shows them.</summary>
    public static readonly IReadOnlyList<(string Kind, Names Names)> All =
    [
        (PropKinds.Stroke, new("spline", "Stroke", "Paths and paint", "Drag along the board to lay this.")),
        (PropKinds.Fluid, new("waves", "Fluid", "Channels", "Drag along the board to dig this.")),
        (PropKinds.House, new("home", "Building", "Buildings", "Drag a rectangle on the board for the building's footprint.")),
        (PropKinds.Tree, new("trees", "Tree", "Trees", "The next click on the board places this.")),
        (PropKinds.Boulder, new("mountain", "Boulder", "Boulders", "The next click on the board places this.")),
        (PropKinds.Flora, new("flower", "Ground cover", "Ground cover", "Drag a ring on the board where this grows.")),
        (PropKinds.Chest, new("box", "Chest", "Chests", "The next click on the board places this.")),
    ];

    private static readonly Names Unknown = new("shapes", "Decoration", "Other", "Place this on the board.");

    public static Names Of(string kind) => All.FirstOrDefault(entry => entry.Kind == kind).Names ?? Unknown;
}
