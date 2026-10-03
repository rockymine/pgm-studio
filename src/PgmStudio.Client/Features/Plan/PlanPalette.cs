using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Plan;

/// <summary>
/// How the plan editor names and tints the words the document is written in. The sets themselves — a piece's
/// roles, a box's kinds — are <c>PlanRoles</c> and <c>PlanBoxKinds</c> in <c>PgmStudio.Vocabulary</c>; this
/// holds only what the editor adds to a word: the label an author reads and the canvas token it is drawn in
/// (<c>--canvas-role-*</c>, <c>--canvas-box-*</c>). The dock, the role dropdown and the inspector all read it.
/// </summary>
internal static class PlanPalette
{
    /// <summary>One word, as a picker shows it.</summary>
    public sealed record Entry(string Id, string Label, string Color);

    /// <summary>The roles that produce terrain.</summary>
    public static readonly IReadOnlyList<Entry> GeneratingRoles =
        [.. PlanRoles.All.Where(PlanRoles.IsGenerating).Select(Role)];

    /// <summary>The roles that annotate: no terrain, no part in the graph.</summary>
    public static readonly IReadOnlyList<Entry> TechnicalRoles =
        [.. PlanRoles.All.Where(PlanRoles.IsAnnotation).Select(Role)];

    /// <summary>Every role a piece can be assigned.</summary>
    public static readonly IReadOnlyList<Entry> Roles = [.. PlanRoles.All.Select(Role)];

    /// <summary>The kinds an envelope may carry.</summary>
    public static readonly IReadOnlyList<Entry> BoxKinds = [.. PlanBoxKinds.All.Select(Box)];

    public static readonly IReadOnlyList<SelectOption> RoleOptions =
        [.. Roles.Select(role => new SelectOption(role.Id, role.Label))];

    public static readonly IReadOnlyList<SelectOption> BoxKindOptions =
        [.. BoxKinds.Select(kind => new SelectOption(kind.Id, kind.Label))];

    /// <summary>Every marker the dock can arm, with the icon the inspector's header wears for it too.</summary>
    public static readonly DockItem[] AllMarkerItems =
    [
        new("spawn", "Spawn marker", Icon: "flag"),
        new("wool", "Wool marker", Icon: "square"),
        new("iron", "Iron marker", Icon: "pickaxe"),
        new("destroyable", "Destroyable", Icon: "gem"),
        new("core", "Core", Icon: "flame"),
        new("wall", "Wall", Icon: "brick-wall"),
    ];

    /// <summary>The inspector's glyph for a marker kind — the same icon its dock item wears, so the panel
    /// and the tool that placed it read as one thing.</summary>
    public static string MarkerIcon(string kind) =>
        AllMarkerItems.FirstOrDefault(item => item.Key == kind)?.Icon ?? "flag";

    private static Entry Role(string role) => new(role, RoleLabel(role), $"var(--canvas-role-{role})");

    private static Entry Box(string kind) => new(kind, BoxLabel(kind), $"var(--canvas-box-{kind})");

    private static string RoleLabel(string role) => role switch
    {
        PlanRoles.WoolRoom => "Wool room",
        _ => Capitalised(role),
    };

    private static string BoxLabel(string kind) => kind switch
    {
        PlanBoxKinds.Frontline => "Front line",
        _ => Capitalised(kind),
    };

    private static string Capitalised(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
