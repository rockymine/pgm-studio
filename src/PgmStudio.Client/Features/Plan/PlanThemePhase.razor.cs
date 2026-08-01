using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Plan;

/// <summary>
/// The Theme rail phase (docs/world-export/terrain-painting.md TP10), two steps: <b>Create</b> defines named
/// terrain-paint themes and previews each one's materials (rim/wall/surface/fill swatches rendered by the
/// server through the real materials + block palette); <b>Apply</b> picks the map default, assigns themes to
/// pieces/boxes, and shows the themes on the actual map top-down. It owns no plan state — every read and write
/// goes through the plan-bridge <see cref="Handle"/>; previews are server-rendered SVG.
/// </summary>
public partial class PlanThemePhase
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    [Inject] public HttpClient Http { get; set; } = default!;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Steps = ["Create", "Apply"];

    private int step;
    private ThemesState? State;
    private string? Selected;
    private string NewName = "";
    private string RenameTo = "";
    private string ThemeJsonText = "";
    private string? JsonError;

    private Dictionary<string, string> Swatches = new();
    private string? MapSvg;
    private bool MapLoading;

    private sealed record ThemesState(
        Dictionary<string, JsonElement> Themes,
        string MapTheme,
        List<PieceRef> Pieces,
        List<BoxRef> Boxes,
        Dictionary<string, string> PieceThemes,
        Dictionary<string, string> BoxThemes);

    private sealed record PieceRef(string Id, string Role);
    private sealed record BoxRef(string Id, string Kind, List<string> Members);
    private sealed record MapPreview(string Svg);

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && State is null)
        {
            await Load();
            if (step == 0) await LoadSwatches();
        }
    }

    private Task OnBackStep() => step == 0 ? OnBack.InvokeAsync() : Goto(0);

    private async Task Load()
    {
        if (Handle is null) return;
        var json = await Handle.InvokeAsync<string>("getThemes");
        State = JsonSerializer.Deserialize<ThemesState>(json, Web);
        if (State is not null)
        {
            if (Selected is null || !State.Themes.ContainsKey(Selected))
                Selected = State.Themes.Keys.FirstOrDefault();
            RefreshJsonText();
        }
        StateHasChanged();
    }

    private void RefreshJsonText()
    {
        JsonError = null;
        ThemeJsonText = Selected is not null && State is not null && State.Themes.TryGetValue(Selected, out var el)
            ? JsonSerializer.Serialize(el, Pretty) : "";
    }

    // ── step navigation: each step lazily loads its preview ──
    private async Task Goto(int i)
    {
        step = i;
        if (i == 0) await LoadSwatches();
        else await LoadMapPreview();
    }

    private Task OnNextStep() => step == 0 ? Goto(1) : OnBack.InvokeAsync();

    private async Task LoadSwatches()
    {
        Swatches = new();
        if (Handle is null || Selected is null || State is null || !State.Themes.TryGetValue(Selected, out var el)) return;
        try
        {
            var resp = await Http.PostAsync("api/terrain/theme-preview",
                new StringContent(el.GetRawText(), Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
                Swatches = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new();
        }
        catch { /* leave the swatches empty on a preview failure */ }
        StateHasChanged();
    }

    private async Task LoadMapPreview()
    {
        if (Handle is null) return;
        MapLoading = true; StateHasChanged();
        try
        {
            var planJson = await Handle.InvokeAsync<string>("exportJson");
            var resp = await Http.PostAsync("api/terrain/theme-map-preview",
                new StringContent(planJson, Encoding.UTF8, "application/json"));
            MapSvg = resp.IsSuccessStatusCode ? (await resp.Content.ReadFromJsonAsync<MapPreview>())?.Svg : null;
        }
        catch { MapSvg = null; }
        MapLoading = false; StateHasChanged();
    }

    // ── theme registry (Create step) ──
    private async Task Select(string id) { Selected = id; RenameTo = ""; RefreshJsonText(); await LoadSwatches(); }

    private async Task AddTheme()
    {
        if (Handle is null) return;
        Selected = await Handle.InvokeAsync<string>("defineTheme", NewName);
        NewName = "";
        await Load();
        await LoadSwatches();
    }

    private async Task DeleteTheme(string id)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("deleteTheme", id);
        if (Selected == id) Selected = null;
        await Load();
        await LoadSwatches();
    }

    private async Task RenameTheme()
    {
        if (Handle is null || Selected is null || string.IsNullOrWhiteSpace(RenameTo)) return;
        Selected = await Handle.InvokeAsync<string>("renameTheme", Selected, RenameTo);
        RenameTo = "";
        await Load();
    }

    private async Task ApplyThemeJson()
    {
        if (Handle is null || Selected is null) return;
        JsonError = await Handle.InvokeAsync<string?>("setThemeJson", Selected, ThemeJsonText);
        if (JsonError is null) { await Load(); await LoadSwatches(); }
        else StateHasChanged();
    }

    // ── application (Apply step) ──
    private async Task OnMapThemeChanged(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", (string?)e.Value ?? "");
        await Load();
        await LoadMapPreview();
    }

    private async Task AssignPiece(string pieceId, string? themeId)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("assignPiece", pieceId, themeId ?? "");
        await Load();
        await LoadMapPreview();
    }

    private async Task AssignBox(string boxId, string? themeId)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("assignBox", boxId, themeId ?? "");
        await Load();
        await LoadMapPreview();
    }
}
