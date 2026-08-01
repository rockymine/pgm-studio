using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Plan;

/// <summary>
/// The Theme rail phase (docs/world-export/terrain-painting.md TP10): defines named terrain-paint themes, the
/// map default, and per-piece / per-box assignments. It owns no plan state — every read and write goes through
/// the plan-bridge <see cref="Handle"/> (the JS plan doc), and after each edit it re-pulls the bridge's theme
/// state, so the phase always mirrors the document the compiler will read.
/// </summary>
public partial class PlanThemePhase
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private ThemesState? State;
    private string? Selected;
    private string NewName = "";
    private string RenameTo = "";
    private string ThemeJsonText = "";
    private string? JsonError;

    private sealed record ThemesState(
        Dictionary<string, JsonElement> Themes,
        string MapTheme,
        List<PieceRef> Pieces,
        List<BoxRef> Boxes,
        Dictionary<string, string> PieceThemes,
        Dictionary<string, string> BoxThemes);

    private sealed record PieceRef(string Id, string Role);
    private sealed record BoxRef(string Id, string Kind, List<string> Members);

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && State is null) await Load();
    }

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

    private void Select(string id) { Selected = id; RenameTo = ""; RefreshJsonText(); }

    private async Task AddTheme()
    {
        if (Handle is null) return;
        var id = await Handle.InvokeAsync<string>("defineTheme", NewName);
        NewName = "";
        Selected = id;
        await Load();
    }

    private async Task DeleteTheme(string id)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("deleteTheme", id);
        if (Selected == id) Selected = null;
        await Load();
    }

    private async Task RenameTheme()
    {
        if (Handle is null || Selected is null || string.IsNullOrWhiteSpace(RenameTo)) return;
        var id = await Handle.InvokeAsync<string>("renameTheme", Selected, RenameTo);
        Selected = id;
        RenameTo = "";
        await Load();
    }

    private async Task OnMapThemeChanged(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", (string?)e.Value ?? "");
        await Load();
    }

    private async Task ApplyThemeJson()
    {
        if (Handle is null || Selected is null) return;
        var error = await Handle.InvokeAsync<string?>("setThemeJson", Selected, ThemeJsonText);
        JsonError = error;
        if (error is null) await Load();
        else StateHasChanged();
    }

    private async Task AssignPiece(string pieceId, string? themeId)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("assignPiece", pieceId, themeId ?? "");
        await Load();
    }

    private async Task AssignBox(string boxId, string? themeId)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("assignBox", boxId, themeId ?? "");
        await Load();
    }
}
