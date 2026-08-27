using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Theme phase's Apply-step controls (docs/tools/sketch.md, the Theme phase), shown under the island tree
/// in the sketch sidebar. The tree is the selector — an island or one of its shapes — and this picks the theme,
/// previews it, and applies or removes it on that selection; the map default sits under everything. It owns no
/// state: it reads the theme registry + per-shape assignments through the sketch-bridge <see cref="Handle"/> and
/// writes back through it (a shape override, or an island's every member). Themes are <em>created</em> in the
/// previous step; here they are only chosen and placed.
/// </summary>
public partial class SketchThemeApplyRail
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The selected island (its every shape is themed), else null.</summary>
    [Parameter] public string? SelectedIslandId { get; set; }
    /// <summary>The selected single shape (themed on its own), else null.</summary>
    [Parameter] public string? SelectedShapeId { get; set; }
    /// <summary>The shape ids the current selection covers — for reading back its current theme.</summary>
    [Parameter] public IReadOnlyList<string> TargetShapeIds { get; set; } = [];
    /// <summary>The theme in hand, held by the tool because the canvas can lift one into it.</summary>
    [Parameter] public string? Brush { get; set; }
    [Parameter] public EventCallback<string> BrushChanged { get; set; }
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private List<string> Themes = [];
    private string MapTheme = "";
    private Dictionary<string, string> ShapeThemes = new();
    private string? Picked => string.IsNullOrEmpty(Brush) ? null : Brush;
    private string? previewedFor;                           // the theme the preview currently shows
    private ThemePreviewDto? Preview;

    // Reload the registry + assignments whenever the selection changes; refresh swatches only when the picked
    // theme changes (that one carries an HTTP round-trip).
    protected override async Task OnParametersSetAsync()
    {
        if (Handle is null) return;
        await Load();
    }

    private async Task Load()
    {
        var json = await Handle!.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Themes = root.TryGetProperty("themes", out var th) && th.ValueKind == JsonValueKind.Object
            ? th.EnumerateObject().Select(p => p.Name).ToList() : [];
        MapTheme = root.TryGetProperty("mapTheme", out var mt) && mt.ValueKind == JsonValueKind.String ? mt.GetString() ?? "" : "";
        ShapeThemes = ReadMap(root, "shapeThemes");
        // A brush naming a theme the map no longer has falls back to the first one, and an empty registry
        // leaves it empty. Raised only when the value actually moves — the tool re-renders this component on
        // every brush change, so asking for one it already holds would never settle.
        var want = Brush is { Length: > 0 } held && Themes.Contains(held) ? held : Themes.FirstOrDefault() ?? "";
        if (want != (Brush ?? "")) { await BrushChanged.InvokeAsync(want); return; }
        if (Picked != previewedFor) await LoadPickedPreview();
        StateHasChanged();
    }

    private static Dictionary<string, string> ReadMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>();
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String) map[p.Name] = p.Value.GetString() ?? "";
        return map;
    }

    private bool HasSelection => SelectedIslandId is not null || SelectedShapeId is not null;

    // What the selection paints today: the theme shared by every target shape, "" when none, or "~mixed~" when
    // the members disagree (an island whose shapes carry different themes).
    private string SelectionTheme()
    {
        if (TargetShapeIds.Count == 0) return "";
        var first = ShapeThemes.GetValueOrDefault(TargetShapeIds[0], "");
        return TargetShapeIds.All(id => ShapeThemes.GetValueOrDefault(id, "") == first) ? first : "~mixed~";
    }

    /// <summary>Take a theme in hand. The canvas paints it on a click, and the Apply button places it on
    /// whatever the tree has picked — a click on the board and a click in the tree are different targets, so
    /// both stay.</summary>
    private Task Pick(string id) => BrushChanged.InvokeAsync(Picked == id ? "" : id);

    // The picked theme's preview, through the real materials + palette (the same render the Create step uses),
    // so what the rail shows is the paint that will land.
    private async Task LoadPickedPreview()
    {
        Preview = null;
        previewedFor = Picked;
        if (Handle is null || Picked is null) return;
        var themesJson = await Handle.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(themesJson);
        if (!doc.RootElement.TryGetProperty("themes", out var th) || !th.TryGetProperty(Picked, out var node)) return;
        Preview = await Library.ThemePreviewAsync(node.GetRawText());
    }

    private string Swatch(string bucket) => Preview?.Buckets.GetValueOrDefault(bucket) ?? "";

    private Task Apply() => Assign(Picked ?? "");
    private Task Remove() => Assign("");

    // Assign (or clear) the picked theme on the selection — an island writes every member shape, a shape just
    // itself. The bridge re-derives the readout; the export resolves the shape → theme at paint time.
    private async Task Assign(string themeId)
    {
        if (Handle is null) return;
        if (SelectedIslandId is not null) await Handle.InvokeVoidAsync("assignIsland", SelectedIslandId, themeId);
        else if (SelectedShapeId is not null) await Handle.InvokeVoidAsync("assignShape", SelectedShapeId, themeId);
        await Load();
    }

    private async Task OnMapDefault(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", (string?)e.Value ?? "");
        await Load();
    }
}
