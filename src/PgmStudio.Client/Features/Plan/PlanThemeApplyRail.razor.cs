using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Plan;

/// <summary>
/// The Theme rail's Apply step (docs/world-export/terrain-painting.md TP10, G157) as the left rail beside the
/// shared plan canvas. The canvas is the selector — click a box/piece, Ctrl-click several — and this rail picks
/// the theme to assign, previews it, and applies or removes it on the current selection; the map default sits
/// under everything. It owns no plan state: it reads the theme registry and the current assignments through the
/// plan-bridge <see cref="Handle"/> and writes back through it, and the bridge keeps the canvas paint overlay
/// live. The theme is <em>created</em> in the previous step; here it is only chosen and placed.
/// </summary>
public partial class PlanThemeApplyRail
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The box/piece shapes the current canvas selection covers — what Apply/Remove act on.</summary>
    [Parameter] public IReadOnlyList<PlanTool.ThemeShape> Shapes { get; set; } = [];
    [Inject] public HttpClient Http { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private List<string> Themes = [];
    private string MapTheme = "";
    private Dictionary<string, string> PieceThemes = new();
    private Dictionary<string, string> BoxThemes = new();
    private string? Picked;                             // the theme selected to apply
    private Dictionary<string, string> Swatches = new();   // picked theme's per-bucket preview SVG
    private JsonElement pickedNode;
    private bool loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && !loaded) { loaded = true; await Load(); }
    }

    private async Task Load()
    {
        if (Handle is null) return;
        var json = await Handle.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Themes = root.TryGetProperty("themes", out var th) && th.ValueKind == JsonValueKind.Object
            ? th.EnumerateObject().Select(p => p.Name).ToList() : [];
        MapTheme = root.TryGetProperty("mapTheme", out var mt) && mt.ValueKind == JsonValueKind.String ? mt.GetString() ?? "" : "";
        PieceThemes = ReadMap(root, "pieceThemes");
        BoxThemes = ReadMap(root, "boxThemes");
        if (Picked is null || !Themes.Contains(Picked)) Picked = Themes.FirstOrDefault();
        await LoadPickedSwatches();
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

    // The theme a shape paints with today (a box's own theme, or a piece's own override), or "" when unthemed.
    private string ThemeOf(PlanTool.ThemeShape shape)
        => shape.Kind == "box" ? BoxThemes.GetValueOrDefault(shape.Id, "") : PieceThemes.GetValueOrDefault(shape.Id, "");

    private async Task Pick(string id) { Picked = id; await LoadPickedSwatches(); StateHasChanged(); }

    // The picked theme's per-bucket preview, through the real materials + palette (same render the Create step
    // uses), so the swatch shows the paint that will land.
    private async Task LoadPickedSwatches()
    {
        Swatches = new();
        if (Handle is null || Picked is null) return;
        var themesJson = await Handle.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(themesJson);
        if (!doc.RootElement.TryGetProperty("themes", out var th) || !th.TryGetProperty(Picked, out var node)) return;
        pickedNode = node.Clone();
        try
        {
            var resp = await Http.PostAsync("api/terrain/theme-preview",
                new StringContent(node.GetRawText(), Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
                Swatches = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new();
        }
        catch { /* leave empty on a preview failure */ }
    }

    // Apply the picked theme to every selected shape (or clear it with themeId=""). Boxes and pieces go through
    // their own bridge method; the bridge re-derives the scopes and refreshes the canvas paint overlay.
    private Task Apply() => AssignAll(Picked ?? "");
    private Task Remove() => AssignAll("");

    private async Task AssignAll(string themeId)
    {
        if (Handle is null) return;
        foreach (var shape in Shapes)
            await Handle.InvokeVoidAsync(shape.Kind == "box" ? "assignBox" : "assignPiece", shape.Id, themeId);
        await Load();   // refresh the current-theme readout; the overlay refreshes itself
    }

    private async Task OnMapDefault(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", (string?)e.Value ?? "");
        await Load();
    }
}
