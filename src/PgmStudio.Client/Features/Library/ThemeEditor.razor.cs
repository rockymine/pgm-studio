using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// Composes one terrain finish. The draft it edits is the <see cref="ThemeSaveRequest"/> itself, so what is
/// previewed and what is saved are the same value and the picture cannot promise a theme the save would not
/// produce.
///
/// <para>An unbound bucket is one the theme says nothing about: it is left out of the bindings and resolves to
/// stone, the unthemed ground <see cref="PgmStudio.Minecraft.Painting.TerrainTheme"/> defaults to. Bound and
/// switched off are different answers, and a bucket gives either without the other — only an unbound bucket
/// that still paints is dropped, because that one says nothing.</para>
///
/// <para>A theme does not nest, so every bucket is drawn at once and the outline reaches a section rather
/// than choosing which one exists.</para>
/// </summary>
public partial class ThemeEditor
{
    [Parameter, EditorRequired] public string Entry { get; set; } = "";
    [Parameter] public EventCallback<string?> OnSaved { get; set; }
    [Parameter] public EventCallback<string> OnName { get; set; }

    private const string AbsoluteBedrock = "absolute";
    private const string RelativeBedrock = "relative";

    /// <summary>Bound to nothing — a bucket that resolves to stone, stored by leaving the binding out rather
    /// than by a row that says so.</summary>
    private const long Unbound = 0;

    private string importJson = "";
    private string? importError;

    /// <summary>Whether the paste-a-theme section shows. Importing creates a row, so it is offered while one is
    /// being started and not once there is a row to edit.</summary>
    private bool CanImport => editingId is null;

    /// <summary>The outline row the geometry knobs sit on, which is not a bucket.</summary>
    private const string GroundPart = "ground";

    private IReadOnlyList<StyleDto> styles = [];
    private ThemeSaveRequest? draft;
    private long? editingId;
    private string draftName = "";
    private string selected = ThemeBuckets.Rim;
    private string? note;
    private ThemePreviewDto? preview;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    private ThemeBucketDto Binding(string bucket) =>
        draft!.Buckets.First(binding => binding.Bucket == bucket);

    /// <summary>The section a row reaches, marked while that row is the picked one — a scroll that lands
    /// mid-column still says which row was asked for.</summary>
    private string SectionCls(string part) => part == selected ? "lib-section lib-section--picked" : "lib-section";

    /// <summary>What one bucket alone paints, from the same preview the picture comes from; empty until the
    /// first preview lands.</summary>
    private string Swatch(string bucket) => preview?.Buckets.GetValueOrDefault(bucket) ?? "";

    private bool CanSave => draft is not null && !string.IsNullOrWhiteSpace(draftName);

    private IReadOnlyList<EditorPart> Outline
    {
        get
        {
            if (draft is null) return [];
            List<EditorPart> rows = [.. ThemeBucketInfo.All.Select(info =>
            {
                var binding = Binding(info.Id);
                var bound = StyleOf(binding.StyleId);
                return new EditorPart(info.Id, info.Title, "layers",
                    Badge: !binding.Enabled ? "off" : bound?.Name ?? "built-in");
            })];
            // The row names the section; what the section says is in the section, so the badge is the one
            // word that tells two themes apart at a glance rather than the whole sentence.
            rows.Add(new EditorPart(GroundPart, "Ground and edges", "mountain",
                Badge: RimEdgeModes.Canonical(draft.RimEdges)));
            return rows;
        }
    }

    private string Footnote => draft is null
        ? ""
        : $"{draft.Buckets.Count(binding => binding.Enabled && binding.StyleId != Unbound)} of "
          + $"{ThemeBucketInfo.All.Count} buckets bound";

    protected override async Task OnInitializedAsync()
        => styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);

    /// <summary>What the draft was loaded for. A parameter set that does not move the route is the host
    /// re-rendering — reloading there would re-read the row, report the name back up, and re-render the host
    /// again.</summary>
    private string? loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (loaded == Entry) return;
        loaded = Entry;
        note = null;
        selected = ThemeBuckets.Rim;
        if (long.TryParse(Entry, out var id))
        {
            if (await Library.GetAsync<ThemeDetail>(LibraryKinds.Themes, id) is not { } detail)
            {
                note = "That theme could not be read.";
                draft = null;
                return;
            }
            editingId = detail.Id;
            draftName = detail.Name;
            // The stored theme names only the buckets it overrides; the editor shows all four, so the ones it
            // does not name come back as unbound.
            draft = new ThemeSaveRequest(
                detail.Name, detail.BedrockRelative, detail.BedrockValue, detail.RimEdges,
                detail.WallOnTerrainFaces,
                [.. ThemeBucketInfo.All.Select(info =>
                    detail.Buckets.FirstOrDefault(binding => binding.Bucket == info.Id)
                    ?? new ThemeBucketDto(info.Id, Unbound, Depth: 1, Enabled: true))]);
        }
        else
        {
            editingId = null;
            draftName = "";
            draft = new ThemeSaveRequest(
                "", BedrockRelative: false, BedrockValue: 1, RimEdgeModes.Drop, WallOnTerrainFaces: true,
                [.. ThemeBucketInfo.All.Select(info =>
                    new ThemeBucketDto(info.Id, Unbound, Depth: 1, Enabled: true))]);
        }
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    /// <summary>Take a whole painter theme JSON into the library: one style per bucket plus the bindings, which
    /// is what makes it editable here rather than a stored blob. It creates a row, so the editor moves to it.</summary>
    private async Task ImportJson()
    {
        importError = null;
        var id = await Library.ImportThemeAsync(draftName.Trim(), importJson);
        if (id is null) { importError = "The library could not read that theme."; return; }
        await OnSaved.InvokeAsync("saved");
        Nav.NavigateTo($"/library/{LibraryKinds.ThemesSlug}/{id}");
    }

    private void Pick(string part) => selected = part;

    // ── bucket bindings ────────────────────────────────────────────────────────────────────────────
    private Task BindStyle(string bucket, long styleId)
        => Rebind(bucket, binding => binding with { StyleId = styleId });

    private Task ToggleBucket(string bucket)
        => Rebind(bucket, binding => binding with { Enabled = !binding.Enabled });

    private Task SetDepth(string bucket, ChangeEventArgs e)
        => Rebind(bucket, binding => binding with { Depth = Math.Max(1, Parse(e, binding.Depth)) });

    private Task Rebind(string bucket, Func<ThemeBucketDto, ThemeBucketDto> edit)
    {
        if (draft is null) return Task.CompletedTask;
        draft = draft with
        {
            Buckets = [.. draft.Buckets.Select(binding => binding.Bucket == bucket ? edit(binding) : binding)],
        };
        return Preview();
    }

    // ── geometry knobs ─────────────────────────────────────────────────────────────────────────────
    private string BedrockMode => draft!.BedrockRelative ? RelativeBedrock : AbsoluteBedrock;

    /// <summary>How far down the paint reaches: a count up from the bottom, or everything the buckets did
    /// not claim.</summary>
    private static readonly IReadOnlyList<SelectOption> BedrockModes =
    [
        new(AbsoluteBedrock, "blocks up from the bottom"),
        new(RelativeBedrock, "everything under the painted depth"),
    ];

    private Task SetBedrockMode(string mode)
        => Knob(theme => theme with { BedrockRelative = mode == RelativeBedrock });

    private Task SetBedrockValue(ChangeEventArgs e)
        => Knob(theme => theme with { BedrockValue = Math.Max(0, Parse(e, theme.BedrockValue)) });

    private Task SetRimEdges(string edges)
        => Knob(theme => theme with { RimEdges = RimEdgeModes.Canonical(edges) });

    private Task ToggleWallFaces()
        => Knob(theme => theme with { WallOnTerrainFaces = !theme.WallOnTerrainFaces });

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
        preview = draft is null
            ? null
            : await Library.DraftPreviewAsync<ThemePreviewDto>(LibraryKinds.Themes, Saveable(draft));
        StateHasChanged();
    }

    /// <summary>An unbound bucket that still paints says nothing — it is stored by being left out. An unbound
    /// bucket that is <em>off</em> says a great deal, so its binding is kept.</summary>
    private ThemeSaveRequest Saveable(ThemeSaveRequest current) => current with
    {
        Name = string.IsNullOrWhiteSpace(draftName) ? current.Name : draftName.Trim(),
        Buckets = [.. current.Buckets.Where(binding => binding.StyleId != Unbound || !binding.Enabled)],
    };

    private async Task Save()
    {
        if (draft is null || !CanSave) return;
        var request = Saveable(draft);
        var saved = editingId is { } id
            ? await Library.UpdateAsync<ThemeDetail>(LibraryKinds.Themes, id, request)
            : await Library.CreateAsync<ThemeDetail>(LibraryKinds.Themes, request);
        if (saved is null) { note = "The library refused that theme."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{LibraryKinds.ThemesSlug}/{saved.Id}");
        else editingId = saved.Id;
    }

    private async Task SaveAsCopy()
    {
        if (draft is null) return;
        var copy = await Library.CreateAsync<ThemeDetail>(LibraryKinds.Themes,
            Saveable(draft) with { Name = $"{draftName.Trim()} copy" });
        if (copy is null) { note = "The library refused that theme."; return; }
        Nav.NavigateTo($"/library/{LibraryKinds.ThemesSlug}/{copy.Id}");
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        await Library.DeleteAsync(LibraryKinds.Themes, id);
        Nav.NavigateTo($"/library/{LibraryKinds.ThemesSlug}");
    }
}
