using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Plan;

/// <summary>
/// The Theme rail phase (docs/world-export/terrain-painting.md TP10), two steps. <b>Create</b> is the theme
/// editor: one section per paintable bucket — rim, wall, surface, fill — each with its toggle, its depth and a
/// <see cref="MaterialEditor"/> that switches the bucket between a solid block, a layer stack, a team tint and
/// the patterns, previewed against the real block palette as it is edited. <b>Apply</b> picks the map default,
/// assigns themes to pieces/boxes, and shows the themes on the actual map top-down.
///
/// <para>It owns no plan state: reads and writes go through the plan-bridge <see cref="Handle"/>, and the theme
/// being edited is the wire JSON itself as a <see cref="JsonObject"/> — the editor mutates that node and hands
/// it back, so there is no model of a theme here to fall out of step with the painter's.</para>
/// </summary>
public partial class PlanThemePhase
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    /// <summary>The Create step's "Apply →" — hands off to the host's canvas theme-apply mode (G157), where the
    /// themes defined here are placed on the plan.</summary>
    [Parameter] public EventCallback OnApply { get; set; }
    [Inject] public HttpClient Http { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Steps = ["Create"];

    private const string AbsoluteBedrock = "absolute";
    private const string RelativeBedrock = "relative";

    private ThemesState? State;
    private string? Selected;
    private string NewName = "";
    private string RenameTo = "";
    private string ThemeJsonText = "";
    private string? JsonError;

    /// <summary>The selected theme's node — what the bucket editors mutate.</summary>
    private JsonObject? Theme;
    private IReadOnlyList<PaintBlockDto> Blocks = [];
    private Dictionary<string, string> Swatches = new();

    // The theme registry + map default this Create step reads (the assignment fields the payload also carries are
    // consumed by the Apply-step rail now, not here).
    private sealed record ThemesState(Dictionary<string, JsonElement> Themes, string MapTheme);

    /// <summary>One paintable bucket as the Create step shows it: what it is, whether it paints, how deep it
    /// reaches, and the material node its editor writes. Assembled per render from the theme node, so it is a
    /// view of the JSON rather than a second copy of it.</summary>
    private sealed record Bucket(
        string Id, string Title, string Blurb, string FallsTo,
        bool CanDisable, bool Enabled, EventCallback Toggle,
        bool HasDepth, int Depth, EventCallback<ChangeEventArgs> SetDepth,
        JsonObject Material);

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && State is null)
        {
            await LoadBlocks();
            await Load();
            await LoadSwatches();
        }
    }

    private async Task LoadBlocks()
    {
        try { Blocks = await Http.GetFromJsonAsync<List<PaintBlockDto>>("api/terrain/blocks") ?? []; }
        catch { Blocks = []; }
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
            AdoptSelected();
        }
        StateHasChanged();
    }

    // Take the selected theme's JSON as the node the editors mutate, and mirror it into the JSON view.
    private void AdoptSelected()
    {
        JsonError = null;
        Theme = null;
        ThemeJsonText = "";
        if (Selected is null || State is null || !State.Themes.TryGetValue(Selected, out var element)) return;
        Theme = JsonNode.Parse(element.GetRawText()) as JsonObject;
        ThemeJsonText = JsonSerializer.Serialize(element, Pretty);
    }

    // ── the buckets ────────────────────────────────────────────────────────────────────────────────
    // Rim and surface carry their own depth and toggle (a TopBand); the wall's depth is the riser it finds, so
    // it is a bare material plus a toggle; fill is the base every unclaimed block falls to and never turns off.
    private IEnumerable<Bucket> Buckets
    {
        get
        {
            if (Theme is null) yield break;
            yield return TopBand(ThemeFields.Rim, "Rim",
                "The cap on the top course of every edge column — what the ground reads as from across the void.",
                "the surface");
            yield return TopBand(ThemeFields.Surface, "Surface",
                "The stack finishing the top of interior columns, claimed downward — grass over two dirt.",
                "the fill");

            var wallEnabled = Flag(ThemeFields.WallEnabled, true);
            yield return new Bucket(
                ThemeFields.Wall, "Wall",
                "The exposed riser under the rim, down to the shallowest drop. A team tint here is what makes a team's ground read as theirs.",
                "the fill", CanDisable: true, wallEnabled,
                EventCallback.Factory.Create(this, ToggleWall),
                HasDepth: false, 0, EventCallback.Factory.Create<ChangeEventArgs>(this, _ => Task.CompletedTask),
                ThemeNode.Child(Theme, ThemeFields.Wall, () => ThemeFields.Solid(1)));

            yield return new Bucket(
                ThemeFields.Fill, "Fill",
                "Every block no other bucket claimed — the body of the terrain, under the surface and behind the wall.",
                "nothing", CanDisable: false, Enabled: true, EventCallback.Empty,
                HasDepth: false, 0, EventCallback.Factory.Create<ChangeEventArgs>(this, _ => Task.CompletedTask),
                ThemeNode.Child(Theme, ThemeFields.Fill, () => ThemeFields.Solid(1)));
        }
    }

    private Bucket TopBand(string field, string title, string blurb, string fallsTo)
    {
        var band = ThemeNode.Child(Theme!, field, () => new JsonObject
        {
            [ThemeFields.Material] = ThemeFields.Solid(1),
            [ThemeFields.Depth] = 1,
            [ThemeFields.Enabled] = true,
        });
        return new Bucket(
            field, title, blurb, fallsTo,
            CanDisable: true, ThemeNode.Bool(band, ThemeFields.Enabled, true),
            EventCallback.Factory.Create(this, () => ToggleBand(band)),
            HasDepth: true, ThemeNode.Int(band, ThemeFields.Depth, 1),
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetBandDepth(band, e)),
            ThemeNode.Child(band, ThemeFields.Material, () => ThemeFields.Solid(1)));
    }

    private Task ToggleBand(JsonObject band)
    {
        ThemeNode.Set(band, ThemeFields.Enabled, !ThemeNode.Bool(band, ThemeFields.Enabled, true));
        return ThemeEdited();
    }

    private Task SetBandDepth(JsonObject band, ChangeEventArgs e)
    {
        ThemeNode.Set(band, ThemeFields.Depth, Math.Max(1, ParseInt(e, 1)));
        return ThemeEdited();
    }

    // ── theme-level knobs ──────────────────────────────────────────────────────────────────────────
    private bool Flag(string field, bool fallback) => ThemeNode.Bool(Theme, field, fallback);

    private Task ToggleFlag(string field, bool fallback)
    {
        if (Theme is null) return Task.CompletedTask;
        ThemeNode.Set(Theme, field, !ThemeNode.Bool(Theme, field, fallback));
        return ThemeEdited();
    }

    private Task ToggleWall() => ToggleFlag(ThemeFields.WallEnabled, true);
    private Task ToggleClosed() => ToggleFlag(ThemeFields.Closed, false);
    private Task ToggleWallFaces() => ToggleFlag(ThemeFields.WallOnTerrainFaces, true);

    private JsonObject BedrockNode => ThemeNode.Child(Theme!, ThemeFields.Bedrock,
        () => new JsonObject { [ThemeFields.Relative] = false, [ThemeFields.Value] = 1 });

    private string BedrockMode =>
        Theme is not null && ThemeNode.Bool(BedrockNode, ThemeFields.Relative, false) ? RelativeBedrock : AbsoluteBedrock;

    private int BedrockValue => Theme is null ? 1 : ThemeNode.Int(BedrockNode, ThemeFields.Value, 1);

    private Task SetBedrockMode(ChangeEventArgs e)
    {
        if (Theme is null) return Task.CompletedTask;
        ThemeNode.Set(BedrockNode, ThemeFields.Relative, (string?)e.Value == RelativeBedrock);
        return ThemeEdited();
    }

    private Task SetBedrockValue(ChangeEventArgs e)
    {
        if (Theme is null) return Task.CompletedTask;
        ThemeNode.Set(BedrockNode, ThemeFields.Value, Math.Max(0, ParseInt(e, 1)));
        return ThemeEdited();
    }

    private static int ParseInt(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    // ── the edit loop: write the node back to the plan, then re-preview ────────────────────────────
    private async Task ThemeEdited()
    {
        if (Handle is null || Selected is null || Theme is null) return;
        JsonError = await Handle.InvokeAsync<string?>("setThemeJson", Selected, Theme.ToJsonString());
        ThemeJsonText = JsonSerializer.Serialize(JsonNode.Parse(Theme.ToJsonString()), Pretty);
        if (State is not null && JsonError is null)
            State.Themes[Selected] = JsonSerializer.Deserialize<JsonElement>(Theme.ToJsonString());
        await LoadSwatches();
    }

    private async Task LoadSwatches()
    {
        Swatches = new();
        if (Theme is null) { StateHasChanged(); return; }
        try
        {
            var resp = await Http.PostAsync("api/terrain/theme-preview",
                new StringContent(Theme.ToJsonString(), Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
                Swatches = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new();
        }
        catch { /* leave the swatches empty on a preview failure */ }
        StateHasChanged();
    }

    // ── theme registry (Create step) ──
    private async Task Select(string id) { Selected = id; RenameTo = ""; AdoptSelected(); await LoadSwatches(); }

    private async Task AddTheme()
    {
        if (Handle is null) return;
        Selected = await Handle.InvokeAsync<string>("defineTheme", NewName);
        NewName = "";
        await Load();
        await LoadSwatches();
    }

    private async Task DeleteSelected()
    {
        if (Handle is null || Selected is null) return;
        await Handle.InvokeVoidAsync("deleteTheme", Selected);
        Selected = null;
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
}
