using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Dressing phase (docs/world-export/decoration.md), two steps. <b>Create</b> is the recipe editor: one
/// section per part of a dressing — the ground cover, the boulders, the trees — each switched on by being
/// present at all, and previewed by <em>growing</em> a sample patch through the real pass rather than by
/// drawing an impression of it. <b>Apply</b> picks the map default and assigns dressings on the canvas.
///
/// <para>It owns no sketch state: reads and writes go through the sketch-bridge <see cref="Handle"/>, and the
/// recipe being edited is the wire JSON itself as a <see cref="JsonObject"/> — the form mutates that node and
/// hands it back, so there is no model of a dressing here to fall out of step with the pass's. The sibling of
/// <see cref="SketchThemePhase"/>, and deliberately shaped the same: an author who has used one already knows
/// this one.</para>
/// </summary>
public partial class SketchDressingPhase
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    /// <summary>The Create step's "Apply →" — hands off to the host's canvas apply mode, where the dressings
    /// defined here are placed on the map.</summary>
    [Parameter] public EventCallback OnApply { get; set; }
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Steps = ["Create", "Apply"];

    private const string TallHint = " — the one part of the cover that hides a player, so it is mirrored";
    private const string SpeciesHint = " — none picked draws from all of them";
    private const string SeedHint = " — the same seed always grows the same arrangement";
    private const string SampleLabel = "56×56";

    private Task OnStep(int i) => i == 1 ? OnApply.InvokeAsync() : Task.CompletedTask;

    private DressingsState? State;
    private string? Selected;
    private string NewName = "";
    private string RenameTo = "";
    private string DressingJsonText = "";
    private string? JsonError;

    /// <summary>The selected recipe's node — what the part forms mutate.</summary>
    private JsonObject? Dressing;
    private IReadOnlyList<PaintBlockDto> Blocks = [];
    private IReadOnlyList<TreeSpeciesDto> Species = [];
    private DressingPreviewDto? Preview;

    /// <summary>The map's own theme JSON, so the preview grows on the finish this map actually paints rather
    /// than on the built-in default — a meadow recipe over a snowfield theme grows nothing, and an author has
    /// to be able to see that here rather than at export.</summary>
    private string? MapThemeJson;

    private sealed record DressingsState(Dictionary<string, JsonElement> Dressings, string MapDressing);

    private sealed record ThemesState(Dictionary<string, JsonElement> Themes, string MapTheme);

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && State is null)
        {
            Blocks = await Library.BlocksAsync();
            Species = await Library.SpeciesAsync();
            await LoadMapTheme();
            await Load();
            await LoadPreview();
        }
    }

    private async Task Load()
    {
        if (Handle is null) return;
        var json = await Handle.InvokeAsync<string>("getDressings");
        State = JsonSerializer.Deserialize<DressingsState>(json, Web);
        if (State is not null)
        {
            if (Selected is null || !State.Dressings.ContainsKey(Selected))
                Selected = State.Dressings.Keys.FirstOrDefault();
            AdoptSelected();
        }
        StateHasChanged();
    }

    // The map default's theme, if it has one — what the sample patch is finished with before it is dressed.
    private async Task LoadMapTheme()
    {
        if (Handle is null) return;
        var themes = JsonSerializer.Deserialize<ThemesState>(await Handle.InvokeAsync<string>("getThemes"), Web);
        MapThemeJson = themes is not null && themes.Themes.TryGetValue(themes.MapTheme, out var theme)
            ? theme.GetRawText() : null;
    }

    private void AdoptSelected()
    {
        JsonError = null;
        Dressing = null;
        DressingJsonText = "";
        if (Selected is null || State is null || !State.Dressings.TryGetValue(Selected, out var element)) return;
        Dressing = JsonNode.Parse(element.GetRawText()) as JsonObject;
        DressingJsonText = JsonSerializer.Serialize(element, Pretty);
    }

    // ── the parts ──────────────────────────────────────────────────────────────────────────────────
    // A part is on by being present. The pass reads an absent part as "grow none of this", so there is no
    // enabled flag to keep in step with whether the part has any settings.
    private Task TogglePart(DressingPartInfo part)
    {
        if (Dressing is null) return Task.CompletedTask;
        JsonEdit.Toggle(Dressing, part.Id, part.New);
        return Edited();
    }

    // ── field edits ────────────────────────────────────────────────────────────────────────────────
    // A share is authored on a 0–100 slider and stored as a 0–1 fraction, which is what the record holds.
    private static int Slider(JsonObject node, string field, double fallback)
        => (int)Math.Round(JsonEdit.Double(node, field, fallback) * 100);

    private static string Pct(JsonObject node, string field, double fallback)
        => $"{Slider(node, field, fallback)}%";

    private Task SetShare(JsonObject node, string field, ChangeEventArgs e)
    {
        JsonEdit.Set(node, field, Math.Clamp(Parse(e, 0) / 100.0, 0, 1));
        return Edited();
    }

    private Task SetCount(JsonObject node, string field, ChangeEventArgs e, int min, int max)
    {
        JsonEdit.Set(node, field, Math.Clamp(Parse(e, min), min, max));
        return Edited();
    }

    private Task SetText(JsonObject node, string field, string value)
    {
        JsonEdit.Set(node, field, value);
        return Edited();
    }

    private Task ToggleFlag(JsonObject node, string field, bool fallback)
    {
        JsonEdit.Set(node, field, !JsonEdit.Bool(node, field, fallback));
        return Edited();
    }

    private Task PickRock(JsonObject node, PaintBlockDto block)
    {
        JsonEdit.Set(node, DressingFields.BlockId, block.Id);
        JsonEdit.Set(node, DressingFields.BlockData, block.Data);
        return Edited();
    }

    private Task ToggleSpecies(JsonObject node, string name)
    {
        var picked = JsonEdit.Texts(node, DressingFields.Species);
        if (!picked.Add(name)) picked.Remove(name);
        JsonEdit.SetTexts(node, DressingFields.Species, picked.Order());
        return Edited();
    }

    /// <summary>Another arrangement from the same knobs. Every field is a hash of the cell and the seed, so the
    /// seed is the only way to ask for a different roll of the same recipe — and an author who likes the
    /// density but not this particular clump needs exactly that.</summary>
    private Task Reroll(JsonObject node)
    {
        JsonEdit.Set(node, DressingFields.Seed, JsonEdit.Int(node, DressingFields.Seed, 0) + 1);
        return Edited();
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    // ── the edit loop: write the node back to the sketch, then re-grow the preview ─────────────────
    private async Task Edited()
    {
        if (Handle is null || Selected is null || Dressing is null) return;
        JsonError = await Handle.InvokeAsync<string?>("setDressingJson", Selected, Dressing.ToJsonString());
        DressingJsonText = JsonSerializer.Serialize(JsonNode.Parse(Dressing.ToJsonString()), Pretty);
        if (State is not null && JsonError is null)
            State.Dressings[Selected] = JsonSerializer.Deserialize<JsonElement>(Dressing.ToJsonString());
        await LoadPreview();
    }

    private async Task LoadPreview()
    {
        Preview = Dressing is null ? null : await Library.DressingPreviewAsync(Dressing.ToJsonString(), MapThemeJson);
        StateHasChanged();
    }

    // ── the registry ───────────────────────────────────────────────────────────────────────────────
    private async Task Select(string id) { Selected = id; RenameTo = ""; AdoptSelected(); await LoadPreview(); }

    private async Task AddDressing()
    {
        if (Handle is null) return;
        Selected = await Handle.InvokeAsync<string>("defineDressing", NewName);
        NewName = "";
        await Load();
        await LoadPreview();
    }

    private async Task DeleteSelected()
    {
        if (Handle is null || Selected is null) return;
        await Handle.InvokeVoidAsync("deleteDressing", Selected);
        Selected = null;
        await Load();
        await LoadPreview();
    }

    private async Task RenameDressing()
    {
        if (Handle is null || Selected is null || string.IsNullOrWhiteSpace(RenameTo)) return;
        Selected = await Handle.InvokeAsync<string>("renameDressing", Selected, RenameTo);
        RenameTo = "";
        await Load();
    }

    private async Task ApplyDressingJson()
    {
        if (Handle is null || Selected is null) return;
        JsonError = await Handle.InvokeAsync<string?>("setDressingJson", Selected, DressingJsonText);
        if (JsonError is null) { await Load(); await LoadPreview(); }
        else StateHasChanged();
    }
}
