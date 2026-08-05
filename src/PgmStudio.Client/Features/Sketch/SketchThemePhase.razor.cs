using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

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
///
/// <para>A sketch's themes are its own, but they need not be authored here: a theme can be pulled in from the
/// style/theme library — which is where a reusable one is built and browsed — and one built here can be pushed
/// back out to it. Both directions go through the library's theme JSON, the same form a map snapshots, so
/// neither side has to know how the other stores a theme.</para>
/// </summary>
public partial class SketchThemePhase
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    /// <summary>The Create step's "Apply →" — hands off to the host's canvas theme-apply mode (G157), where the
    /// themes defined here are placed on the plan.</summary>
    [Parameter] public EventCallback OnApply { get; set; }
    /// <summary>The Rooms step — the shells the map's stamped cages and spawns take (structures.md §9). Part of
    /// the same phase because a room shell is a finish, like the paint these two steps author.</summary>
    [Parameter] public EventCallback OnRooms { get; set; }
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    // Both steps show in the flow bar; Create is this component, Apply is the host's canvas mode.
    private static readonly string[] Steps = ["Create", "Apply", "Rooms"];

    // Flow-bar step click: Apply (1) hands off to the canvas mode; Create (0) is where we already are.
    private Task OnStep(int i) => i switch
    {
        1 => OnApply.InvokeAsync(),
        2 => OnRooms.InvokeAsync(),
        _ => Task.CompletedTask,
    };

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
    private ThemePreviewDto? Preview;

    // The library side: what it holds, and which of its themes the "pull one in" control is pointed at.
    private IReadOnlyList<ThemeSummary> LibraryThemes = [];
    private long LibraryPick;
    private string? LibraryNote;

    // The theme registry + map default this Create step reads (the assignment fields the payload also carries are
    // consumed by the Apply-step rail now, not here).
    private sealed record ThemesState(Dictionary<string, JsonElement> Themes, string MapTheme);

    /// <summary>One paintable bucket as the Create step shows it: what it is (the shared
    /// <see cref="ThemeBucketInfo"/> the library's composer describes it by too), whether it paints, how deep it
    /// reaches, and the material node its editor writes. Assembled per render from the theme node, so it is a
    /// view of the JSON rather than a second copy of it.</summary>
    private sealed record Bucket(
        ThemeBucketInfo Info, bool Enabled, EventCallback Toggle,
        int Depth, EventCallback<ChangeEventArgs> SetDepth, JsonObject Material)
    {
        public string Id => Info.Id;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Handle is not null && State is null)
        {
            Blocks = await Library.BlocksAsync();
            await LoadLibrary();
            await Load();
            await LoadPreview();
        }
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
            foreach (var info in ThemeBucketInfo.All)
                yield return info.HasDepth ? TopBand(info) : Bare(info);
        }
    }

    private Bucket TopBand(ThemeBucketInfo info)
    {
        var band = JsonEdit.Child(Theme!, info.Id, () => new JsonObject
        {
            [ThemeFields.Material] = ThemeFields.Solid(1),
            [ThemeFields.Depth] = 1,
            [ThemeFields.Enabled] = true,
        });
        return new Bucket(
            info, JsonEdit.Bool(band, ThemeFields.Enabled, true),
            EventCallback.Factory.Create(this, () => ToggleBand(band)),
            JsonEdit.Int(band, ThemeFields.Depth, 1),
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetBandDepth(band, e)),
            JsonEdit.Child(band, ThemeFields.Material, () => ThemeFields.Solid(1)));
    }

    // The wall's depth is the riser it finds and the fill takes what is left, so neither carries one; the wall's
    // toggle is a theme-level flag rather than a property of its material, and the fill never turns off.
    private Bucket Bare(ThemeBucketInfo info) => new(
        info,
        !info.CanDisable || Flag(ThemeFields.WallEnabled, true),
        info.CanDisable ? EventCallback.Factory.Create(this, ToggleWall) : EventCallback.Empty,
        Depth: 0, EventCallback.Factory.Create<ChangeEventArgs>(this, _ => Task.CompletedTask),
        JsonEdit.Child(Theme, info.Id, () => ThemeFields.Solid(1)));

    private Task ToggleBand(JsonObject band)
    {
        JsonEdit.Set(band, ThemeFields.Enabled, !JsonEdit.Bool(band, ThemeFields.Enabled, true));
        return ThemeEdited();
    }

    private Task SetBandDepth(JsonObject band, ChangeEventArgs e)
    {
        JsonEdit.Set(band, ThemeFields.Depth, Math.Max(1, ParseInt(e, 1)));
        return ThemeEdited();
    }

    // ── theme-level knobs ──────────────────────────────────────────────────────────────────────────
    private bool Flag(string field, bool fallback) => JsonEdit.Bool(Theme, field, fallback);

    private Task ToggleFlag(string field, bool fallback)
    {
        if (Theme is null) return Task.CompletedTask;
        JsonEdit.Set(Theme, field, !JsonEdit.Bool(Theme, field, fallback));
        return ThemeEdited();
    }

    private Task ToggleWall() => ToggleFlag(ThemeFields.WallEnabled, true);
    private Task ToggleClosed() => ToggleFlag(ThemeFields.Closed, false);
    private Task ToggleWallFaces() => ToggleFlag(ThemeFields.WallOnTerrainFaces, true);

    private JsonObject BedrockNode => JsonEdit.Child(Theme!, ThemeFields.Bedrock,
        () => new JsonObject { [ThemeFields.Relative] = false, [ThemeFields.Value] = 1 });

    private string BedrockMode =>
        Theme is not null && JsonEdit.Bool(BedrockNode, ThemeFields.Relative, false) ? RelativeBedrock : AbsoluteBedrock;

    private int BedrockValue => Theme is null ? 1 : JsonEdit.Int(BedrockNode, ThemeFields.Value, 1);

    private Task SetBedrockMode(ChangeEventArgs e)
    {
        if (Theme is null) return Task.CompletedTask;
        JsonEdit.Set(BedrockNode, ThemeFields.Relative, (string?)e.Value == RelativeBedrock);
        return ThemeEdited();
    }

    private Task SetBedrockValue(ChangeEventArgs e)
    {
        if (Theme is null) return Task.CompletedTask;
        JsonEdit.Set(BedrockNode, ThemeFields.Value, Math.Max(0, ParseInt(e, 1)));
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
        await LoadPreview();
    }

    private async Task LoadPreview()
    {
        Preview = Theme is null ? null : await Library.ThemePreviewAsync(Theme.ToJsonString());
        StateHasChanged();
    }

    private string Swatch(string bucket) => Preview?.Buckets.GetValueOrDefault(bucket) ?? "";

    // ── theme registry (Create step) ──
    private async Task Select(string id) { Selected = id; RenameTo = ""; AdoptSelected(); await LoadPreview(); }

    private async Task AddTheme()
    {
        if (Handle is null) return;
        Selected = await Handle.InvokeAsync<string>("defineTheme", NewName);
        NewName = "";
        await Load();
        await LoadPreview();
    }

    private async Task DeleteSelected()
    {
        if (Handle is null || Selected is null) return;
        await Handle.InvokeVoidAsync("deleteTheme", Selected);
        Selected = null;
        await Load();
        await LoadPreview();
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
        if (JsonError is null) { await Load(); await LoadPreview(); }
        else StateHasChanged();
    }

    // ── the library bridge ────────────────────────────────────────────────────────────────────────
    // A pull copies the library theme's JSON into a new sketch theme; a push lifts the open one back out. Both
    // travel as the painter's theme JSON, so the sketch never learns how the library stores a theme and the
    // library never learns how a sketch names one. A pulled theme is a copy: editing it here does not reach
    // back into the library, and a library edit does not repaint a sketch that already took one.
    private async Task LoadLibrary() => LibraryThemes = await Library.ThemesAsync();

    private ThemeSummary? PickedLibraryTheme => LibraryThemes.FirstOrDefault(theme => theme.Id == LibraryPick);

    private void PickLibraryTheme(ChangeEventArgs e)
        => LibraryPick = long.TryParse((string?)e.Value, out var id) ? id : 0;

    private async Task PullFromLibrary()
    {
        if (Handle is null || PickedLibraryTheme is not { } picked) return;
        var themeJson = await Library.ThemeJsonAsync(picked.Id);
        if (themeJson is null) { LibraryNote = "That theme could not be read."; return; }

        Selected = await Handle.InvokeAsync<string>("defineTheme", picked.Name);
        JsonError = await Handle.InvokeAsync<string?>("setThemeJson", Selected, themeJson);
        LibraryNote = JsonError is null ? $"Copied “{picked.Name}” in as {Selected}." : null;
        await Load();
        await LoadPreview();
    }

    private async Task PushToLibrary()
    {
        if (Selected is null || Theme is null) return;
        var id = await Library.ImportThemeAsync(Selected, Theme.ToJsonString());
        LibraryNote = id is null
            ? "The library refused this theme."
            : $"Saved “{Selected}” to the library, one style per bucket.";
        await LoadLibrary();
        StateHasChanged();
    }
}
