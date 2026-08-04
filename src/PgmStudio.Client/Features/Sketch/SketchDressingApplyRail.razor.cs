using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Dressing phase's Apply-step controls, shown under the island tree. The tree is the selector — an island
/// or one of its shapes — and this picks the dressing, previews it, and places or clears it on that selection;
/// the map default sits under everything. It owns no state: it reads the registry and the per-shape
/// assignments through the sketch-bridge <see cref="Handle"/> and writes back through it. The twin of
/// <see cref="SketchThemeApplyRail"/>, because placing what grows on a shape is the same act as placing what
/// it is painted with.
/// </summary>
public partial class SketchDressingApplyRail
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The selected island (its every shape is dressed), else null.</summary>
    [Parameter] public string? SelectedIslandId { get; set; }
    /// <summary>The selected single shape (dressed on its own), else null.</summary>
    [Parameter] public string? SelectedShapeId { get; set; }
    /// <summary>The shape ids the current selection covers — for reading back what it grows today.</summary>
    [Parameter] public IReadOnlyList<string> TargetShapeIds { get; set; } = [];
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    /// <summary>What an island whose shapes disagree reports — not a dressing id, so it can never be one.</summary>
    private const string MixedMarker = "~mixed~";

    private List<string> Dressings = [];
    private string MapDressing = "";
    private Dictionary<string, string> ShapeDressings = new();
    private string? Picked;
    private string? previewedFor;
    private DressingPreviewDto? Preview;
    private string? mapThemeJson;

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is null) return;
        await Load();
    }

    private async Task Load()
    {
        var json = await Handle!.InvokeAsync<string>("getDressings");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Dressings = root.TryGetProperty("dressings", out var registry) && registry.ValueKind == JsonValueKind.Object
            ? registry.EnumerateObject().Select(p => p.Name).ToList() : [];
        MapDressing = root.TryGetProperty("mapDressing", out var md) && md.ValueKind == JsonValueKind.String
            ? md.GetString() ?? "" : "";
        ShapeDressings = ReadMap(root, "shapeDressings");
        if (Picked is null || !Dressings.Contains(Picked)) Picked = Dressings.FirstOrDefault();
        if (Picked != previewedFor) await LoadPickedPreview();
        StateHasChanged();
    }

    private static Dictionary<string, string> ReadMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>();
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String) map[property.Name] = property.Value.GetString() ?? "";
        return map;
    }

    private bool HasSelection => SelectedIslandId is not null || SelectedShapeId is not null;

    // What the selection grows today: the dressing shared by every target shape, "" when none, or the mixed
    // marker when the members disagree (an island whose shapes carry different dressings).
    private string SelectionDressing()
    {
        if (TargetShapeIds.Count == 0) return "";
        var first = ShapeDressings.GetValueOrDefault(TargetShapeIds[0], "");
        return TargetShapeIds.All(id => ShapeDressings.GetValueOrDefault(id, "") == first) ? first : MixedMarker;
    }

    private async Task Pick(string id) { Picked = id; await LoadPickedPreview(); StateHasChanged(); }

    // The picked dressing grown over a sample patch — the same render the Create step shows, so what the rail
    // promises and what the editor showed cannot differ.
    private async Task LoadPickedPreview()
    {
        Preview = null;
        previewedFor = Picked;
        if (Handle is null || Picked is null) return;

        mapThemeJson ??= await MapThemeJson();
        using var doc = JsonDocument.Parse(await Handle.InvokeAsync<string>("getDressings"));
        if (!doc.RootElement.TryGetProperty("dressings", out var registry) || !registry.TryGetProperty(Picked, out var node)) return;
        Preview = await Library.DressingPreviewAsync(node.GetRawText(), mapThemeJson);
    }

    private async Task<string?> MapThemeJson()
    {
        using var doc = JsonDocument.Parse(await Handle!.InvokeAsync<string>("getThemes"));
        var root = doc.RootElement;
        if (!root.TryGetProperty("mapTheme", out var id) || id.ValueKind != JsonValueKind.String) return null;
        return root.TryGetProperty("themes", out var themes) && themes.TryGetProperty(id.GetString() ?? "", out var theme)
            ? theme.GetRawText() : null;
    }

    private Task Apply() => Assign(Picked ?? "");
    private Task Remove() => Assign("");

    // Place (or clear) the picked dressing on the selection — an island writes every member shape, a shape just
    // itself. The bridge re-derives the readout; the export resolves shape → dressing when the pass runs.
    private async Task Assign(string dressingId)
    {
        if (Handle is null) return;
        if (SelectedIslandId is not null) await Handle.InvokeVoidAsync("assignIslandDressing", SelectedIslandId, dressingId);
        else if (SelectedShapeId is not null) await Handle.InvokeVoidAsync("assignShapeDressing", SelectedShapeId, dressingId);
        await Load();
    }

    private async Task OnMapDefault(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapDressing", (string?)e.Value ?? "");
        await Load();
    }
}
