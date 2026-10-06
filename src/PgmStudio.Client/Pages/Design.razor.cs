using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Design
{
    /// <summary>One colour or measure of <c>tokens.css</c>: the custom property and what it is for.</summary>
    private sealed record Token(string Name, string Use);

    private static readonly Token[] Surfaces =
    [
        new("--bg-base", "Page background"), new("--bg-panel", "Shell, panels, top bar"),
        new("--bg-deep", "Recessed surface, studio bar"), new("--bg-canvas", "Map viewport"),
        new("--bg-selected", "Selected row, active fill"), new("--bg-multi", "Multi-selection tint"),
        new("--border", "Rules and outlines"),
    ];

    private static readonly Token[] TextColours =
    [
        new("--text-muted", "Section headers, metadata"), new("--text-secondary", "Form labels"),
        new("--text-primary", "Body text"), new("--text-bright", "Headings, emphasis"),
        new("--text-strong", "Active chrome"), new("--code-color", "Inline code"),
    ];

    private static readonly Token[] AccentAndStatus =
    [
        new("--accent", "Focus and primary action"), new("--accent-light", "Icon and text accent"),
        new("--accent-lighter", "Text on light-blue fills"), new("--agent", "An agent's mark"),
        new("--color-success", "Ready, done"), new("--color-success-bg", "Success fill"),
        new("--color-warning", "Review, caution"), new("--color-warning-bg", "Warning fill"),
        new("--color-error", "Refused, destructive"), new("--color-error-bg", "Error fill"),
    ];

    private static readonly Token[] CanvasShapes =
    [
        new("--canvas-island", "Island"), new("--canvas-add-fill", "Added shape"),
        new("--canvas-sub-fill", "Carved shape"), new("--canvas-result-fill", "Computed outline"),
        new("--canvas-axis", "Symmetry axis"), new("--canvas-symmetry", "Symmetry marker"),
        new("--canvas-chunk", "Chunk grid"), new("--canvas-contour-index", "Relief contour"),
    ];

    private static readonly Token[] CanvasRoles =
    [
        new("--canvas-role-piece", "Piece"), new("--canvas-role-wool-room", "Wool room"),
        new("--canvas-role-spawn", "Spawn"), new("--canvas-role-buffer", "Buffer"),
        new("--canvas-objective-spawn", "Spawn marker"), new("--canvas-objective-wool", "Wool marker"),
        new("--canvas-objective-destroyable", "Destroyable"), new("--canvas-objective-core", "Core"),
        new("--canvas-building-fill", "Building"), new("--canvas-prop-flora-fill", "Flora"),
    ];

    private static readonly Token[] TextSizes =
    [
        new("--font-2xl", "Page hero"), new("--font-xl", "Page heading"), new("--font-lg", "Body default"),
        new("--font-md", "Inputs, list labels"), new("--font-base", "Badges, buttons, panels"),
        new("--font-sm", "Helper text, field labels"), new("--font-xs", "Section headers"),
    ];

    private static readonly Token[] Spacing =
    [
        new("--space-1", "4px"), new("--space-2", "8px"), new("--space-3", "12px"),
        new("--space-4", "16px"), new("--space-5", "20px"), new("--space-6", "24px"),
    ];

    private static readonly Token[] Radii =
    [
        new("--radius-sm", "3px · swatches, chips"), new("--radius-md", "6px · frames, cards"),
        new("--radius-pill", "Capsules"),
    ];

    private static readonly Token[] Shadows =
    [
        new("--shadow-ring", "Selection outline"), new("--shadow-float", "Menus and overlays"),
        new("--shadow-raised", "Raised callout"),
    ];

    private static readonly Token[] IconSizes =
    [
        new("--icon-xs", "Dense chrome"), new("--icon-sm", "Inline with text"), new("--icon-md", "Default"),
        new("--icon-lg", "Standalone, nav rail"), new("--icon-xl", "Landing cards"),
    ];

    /// <summary>The sections the page lists in its rail, in page order, under the group each belongs to.</summary>
    private static readonly (string Group, (string Id, string Label)[] Sections)[] Contents =
    [
        ("Foundations", [("tokens", "Tokens")]),
        ("Components", [
            ("primitives", "Primitives"), ("forms", "Forms"), ("data", "Data and lists"),
            ("navigation", "Layout and navigation"), ("filters", "Filter rail"),
            ("chrome", "Editor chrome"), ("feedback", "Feedback and problems")]),
        ("Pages", [("pages", "Cards and the landing")]),
        ("Rules", [("rules", "Usage rules")]),
    ];

    private static readonly IReadOnlyList<string> FlowSteps = ["Colours", "Spawn & room", "Monuments"];

    private static readonly IReadOnlyList<DockItem> DockFamily =
    [
        new("rect", "Rectangle", Icon: "rectangle-horizontal"),
        new("polygon", "Polygon", Icon: "pentagon"),
        new("lasso", "Lasso", Icon: "lasso"),
    ];

    private static readonly IReadOnlyList<SelectOption> SymmetryOptions = Select.Words(
        ["Mirror", "Rotate 90", "Rotate 180"], word => word, word => $"{word} symmetry");

    private static readonly IReadOnlyList<SwatchRow<string>.Swatch> TeamColours =
    [
        new("red", "#FF5555", "Red"), new("blue", "#5555FF", "Blue"),
        new("green", "#55FF55", "Green"), new("yellow", "#FFFF55", "Yellow"),
    ];

    private static readonly IReadOnlyList<BoardKeyEntry> BoardKeySample =
    [
        new("Piece", "#7c8899", false), new("Wool room", "#3fae74", false),
        new("Spawn", "#8f7bd6", false), new("Buffer", "#f2792b", true),
    ];

    [Inject] private RuleBook Book { get; set; } = default!;

    /// <summary>A sample of the problem list, built on three real layout rules from <c>GET /api/rules</c> so each
    /// row's title is the rule's own; empty until the rules have loaded.</summary>
    private IReadOnlyList<Problem> problemSample = [];

    private static readonly (string Where, Severity Severity)[] SampleFindings =
    [
        ("the red and the blue wool room share 1 block of ground", Severity.Refusal),
        ("the lane at 12, 40 is 3 blocks wide against 5 at the spawn", Severity.Complaint),
        ("the path at 30, 8 crosses 2 blocks of void", Severity.Decline),
    ];

    protected override async Task OnInitializedAsync()
    {
        var layout = (await Book.RulesAsync()).Values.Where(RuleWords.IsLayoutRule).Take(SampleFindings.Length).ToList();
        problemSample = [.. layout.Select((rule, index) =>
            Problem.Of(new Finding(rule.Rule, SampleFindings[index].Where, SampleFindings[index].Severity), index))];
    }

    private readonly HashSet<string> chipsOn = ["plan", "wool"];
    private readonly List<AuthorRow> authors = [new() { Name = "Annealing Team", Contribution = "Layout" }];

    private bool drawerOpen;
    private bool flyoutOpen;
    private string flyoutKey = "rect";
    private string symmetry = "Mirror";
    private string teamColour = "red";
    private double slider = 40;
    private string? toast;
    private bool busy;
    private int tick;

    private void Toggle(string key)
    {
        if (!chipsOn.Remove(key)) chipsOn.Add(key);
    }

    private void ShowToast() => toast = $"Saved {++tick}";

    private async Task RunBusy()
    {
        busy = true;
        await Task.Delay(1500);
        busy = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
