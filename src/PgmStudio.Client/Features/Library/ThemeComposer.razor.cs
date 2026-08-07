using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's theme half (B44): a theme is composed, not drawn — four buckets bound to saved styles plus the
/// geometry knobs. The draft it edits is the <see cref="ThemeSaveRequest"/> itself, so what is previewed and
/// what is saved are the same value and the picture cannot promise a theme the save would not produce.
///
/// <para>An unbound bucket is one the theme does not override: it is simply left out of the bindings and keeps
/// the built-in finish. That is what makes the library worth having for the case it was asked for — a rim and a
/// fill bound once and reused, with only the surface and the wall differing between themes.</para>
///
/// <para>Bound and switched off are different answers, and a bucket gives either without the other. A theme
/// needs neither a rim nor a wall — surface over fill is a complete finish — so the toggle is offered whether
/// or not a style is bound, and an unbound bucket that is off keeps its binding on save. Only an unbound bucket
/// that still paints is dropped, because that one really does say nothing.</para>
/// </summary>
public partial class ThemeComposer
{
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private const string AbsoluteBedrock = "absolute";
    private const string RelativeBedrock = "relative";

    /// <summary>Bound to nothing — a bucket's "keep the built-in finish" binding, which is stored by leaving the
    /// binding out rather than by a row that says so.</summary>
    private const long Unbound = 0;

    private IReadOnlyList<ThemeSummary> themes = [];
    private IReadOnlyList<StyleDto> styles = [];
    private bool loading = true;
    private string newName = "";
    private string? note;

    /// <summary>The theme open in the rail, or null when nothing is. Null id = not in the library yet.</summary>
    private ThemeSaveRequest? draft;
    private long? editingId;
    private string draftName = "";
    private ThemePreviewDto? preview;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    protected override async Task OnInitializedAsync() => await Reload();

    private async Task Reload()
    {
        loading = true;
        themes = await Library.ThemesAsync();
        styles = await Library.StylesAsync();
        loading = false;
        StateHasChanged();
    }

    // ── the draft ──────────────────────────────────────────────────────────────────────────────────
    // Every bucket is present in the draft so the rail can show all four; the unbound ones are dropped on save.
    private static ThemeSaveRequest EmptyDraft(string name) => new(
        name, BedrockRelative: false, BedrockValue: 1, RimEdges: RimEdgeModes.Drop, WallOnTerrainFaces: true,
        ThemeBucketInfo.All.Select(info => new ThemeBucketDto(info.Id, Unbound, Depth: 1, Enabled: true)).ToList());

    private ThemeBucketDto Binding(string bucket)
        => draft!.Buckets.First(binding => binding.Bucket == bucket);

    private async Task StartNew()
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        editingId = null;
        draftName = newName.Trim();
        newName = "";
        note = null;
        draft = EmptyDraft(draftName);
        await Preview();
    }

    private async Task Edit(long id)
    {
        var detail = await Library.ThemeAsync(id);
        if (detail is null) { note = "That theme could not be read."; return; }
        editingId = detail.Id;
        draftName = detail.Name;
        note = null;
        // The stored theme names only the buckets it overrides; the rail shows all four, so the ones it does not
        // name come back as unbound.
        draft = new ThemeSaveRequest(
            detail.Name, detail.BedrockRelative, detail.BedrockValue, detail.RimEdges, detail.WallOnTerrainFaces,
            ThemeBucketInfo.All.Select(info =>
                detail.Buckets.FirstOrDefault(binding => binding.Bucket == info.Id)
                ?? new ThemeBucketDto(info.Id, Unbound, Depth: 1, Enabled: true)).ToList());
        await Preview();
    }

    private void Close()
    {
        draft = null;
        editingId = null;
        preview = null;
        note = null;
    }

    // ── bucket bindings ────────────────────────────────────────────────────────────────────────────
    private Task BindStyle(string bucket, ChangeEventArgs e)
        => Rebind(bucket, binding => binding with
        {
            StyleId = long.TryParse((string?)e.Value, out var id) ? id : Unbound,
        });

    private Task ToggleBucket(string bucket)
        => Rebind(bucket, binding => binding with { Enabled = !binding.Enabled });

    private Task SetDepth(string bucket, ChangeEventArgs e)
        => Rebind(bucket, binding => binding with { Depth = Math.Max(1, Parse(e, binding.Depth)) });

    private Task Rebind(string bucket, Func<ThemeBucketDto, ThemeBucketDto> edit)
    {
        if (draft is null) return Task.CompletedTask;
        draft = draft with
        {
            Buckets = draft.Buckets
                .Select(binding => binding.Bucket == bucket ? edit(binding) : binding)
                .ToList(),
        };
        return Preview();
    }

    // ── geometry knobs ─────────────────────────────────────────────────────────────────────────────
    private string BedrockMode => draft!.BedrockRelative ? RelativeBedrock : AbsoluteBedrock;

    private Task SetBedrockMode(ChangeEventArgs e) => Knob(d => d with { BedrockRelative = (string?)e.Value == RelativeBedrock });
    private Task SetBedrockValue(ChangeEventArgs e) => Knob(d => d with { BedrockValue = Math.Max(0, Parse(e, d.BedrockValue)) });
    private Task SetRimEdges(ChangeEventArgs e) => Knob(d => d with { RimEdges = RimEdgeModes.Canonical((string?)e.Value) });
    private Task ToggleWallFaces() => Knob(d => d with { WallOnTerrainFaces = !d.WallOnTerrainFaces });

    private Task Knob(Func<ThemeSaveRequest, ThemeSaveRequest> edit)
    {
        if (draft is null) return Task.CompletedTask;
        draft = edit(draft);
        return Preview();
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    // ── preview + save ─────────────────────────────────────────────────────────────────────────────
    // Both go through the same request value: the preview is what the save would compose to.
    private async Task Preview()
    {
        preview = draft is null ? null : await Library.ThemeDraftPreviewAsync(Saveable(draft));
        StateHasChanged();
    }

    // An unbound bucket that still paints says nothing — it is stored by being left out, which is what "keeps
    // the built-in finish" means. An unbound bucket that is *off* says a great deal, so its binding is kept:
    // the row carries the toggle alone, and the theme paints no rim at all.
    private ThemeSaveRequest Saveable(ThemeSaveRequest current) => current with
    {
        Name = string.IsNullOrWhiteSpace(draftName) ? current.Name : draftName.Trim(),
        Buckets = current.Buckets.Where(binding => binding.StyleId != Unbound || !binding.Enabled).ToList(),
    };

    private async Task Save()
    {
        if (draft is null || string.IsNullOrWhiteSpace(draftName)) return;
        var request = Saveable(draft);
        var saved = editingId is { } id
            ? await Library.UpdateThemeAsync(id, request)
            : await Library.CreateThemeAsync(request);
        if (saved is null) { note = "The library refused that theme."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        editingId = saved.Id;
        await Reload();
    }

    private async Task SaveAsCopy()
    {
        if (draft is null) return;
        var copy = await Library.CreateThemeAsync(Saveable(draft) with { Name = $"{draftName.Trim()} copy" });
        if (copy is null) { note = "The library refused that theme."; return; }
        editingId = copy.Id;
        draftName = copy.Name;
        note = "Saved as a new theme; the one it was copied from is unchanged.";
        await Reload();
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        await Library.DeleteThemeAsync(id);
        Close();
        await Reload();
    }
}
