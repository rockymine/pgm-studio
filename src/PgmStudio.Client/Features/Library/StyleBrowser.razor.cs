using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's style half (B44): browse every saved material recipe by kind, and author one in the rail.
///
/// <para>The thing being edited is the material's own JSON node, exactly as the sketch's theme phase edits a
/// bucket's — a style <em>is</em> a material, so the same <see cref="MaterialEditor"/> authors both and neither
/// surface owns a second model of one. What this adds is the library's half: a name, the kind read back off the
/// node the editor rewrote, and the save.</para>
/// </summary>
public partial class StyleBrowser
{
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private IReadOnlyList<StyleDto> styles = [];
    private IReadOnlyList<PaintBlockDto> blocks = [];
    private readonly HashSet<string> kindFilter = [];
    private bool loading = true;
    private string? note;

    /// <summary>The material node open in the rail, or null when nothing is being edited. Null id = a style
    /// that is not in the library yet.</summary>
    private JsonObject? draft;
    private long? editingId;
    private string draftName = "";
    private MaterialPreviewDto? preview;

    private IReadOnlyList<StyleDto> Shown =>
        kindFilter.Count == 0 ? styles : styles.Where(style => kindFilter.Contains(style.Kind)).ToList();

    private int CountOf(string kind) => styles.Count(style => style.Kind == kind);

    // The kind is whatever the material node says it is — the editor's kind switch rewrites the node wholesale,
    // so reading it back is the only way the two cannot disagree.
    private string DraftKind => JsonEdit.KindOf(draft);

    private bool CanSave => draft is not null && !string.IsNullOrWhiteSpace(draftName);

    protected override async Task OnInitializedAsync()
    {
        blocks = await Library.BlocksAsync();
        await Reload();
    }

    private async Task Reload()
    {
        loading = true;
        styles = await Library.StylesAsync();
        loading = false;
        StateHasChanged();
    }

    // ── filters ────────────────────────────────────────────────────────────────────────────────────
    private void ToggleKind(string kind)
    {
        if (!kindFilter.Add(kind)) kindFilter.Remove(kind);
    }

    private void ClearKinds() => kindFilter.Clear();

    // ── the rail ───────────────────────────────────────────────────────────────────────────────────
    private async Task StartNew(string kind)
    {
        editingId = null;
        draftName = "";
        note = null;
        draft = ThemeFields.NewMaterial(kind);
        await Preview();
    }

    private async Task Edit(StyleDto style)
    {
        editingId = style.Id;
        draftName = style.Name;
        note = null;
        draft = JsonNode.Parse(style.Params) as JsonObject ?? ThemeFields.NewMaterial(style.Kind);
        await Preview();
    }

    private void Close()
    {
        draft = null;
        editingId = null;
        preview = null;
        note = null;
    }

    private Task DraftEdited() => Preview();

    private async Task Preview()
    {
        preview = draft is null ? null : await Library.MaterialPreviewAsync(draft.ToJsonString());
        StateHasChanged();
    }

    // ── saving ─────────────────────────────────────────────────────────────────────────────────────
    private async Task Save()
    {
        if (draft is null || !CanSave) return;
        var request = new StyleSaveRequest(draftName.Trim(), DraftKind, draft.ToJsonString());
        var saved = editingId is { } id
            ? await Library.UpdateStyleAsync(id, request)
            : await Library.CreateStyleAsync(request);
        if (saved is null) { note = "The library refused that style."; return; }

        // An edit reaches every theme binding this style — the library is the shared copy, and a map's applied
        // theme is its own snapshot, so nothing already exported moves.
        note = editingId is null ? "Added to the library." : "Saved. Every theme binding it now paints this.";
        editingId = saved.Id;
        await Reload();
    }

    private async Task SaveAsCopy()
    {
        if (draft is null || !CanSave) return;
        var copy = await Library.CreateStyleAsync(
            new StyleSaveRequest($"{draftName.Trim()} copy", DraftKind, draft.ToJsonString()));
        if (copy is null) { note = "The library refused that style."; return; }
        editingId = copy.Id;
        draftName = copy.Name;
        note = "Saved as a new style; the one it was copied from is unchanged.";
        await Reload();
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        if (await Library.DeleteStyleAsync(id) is { } inUse)
        {
            note = $"Still bound by {string.Join(", ", inUse.Themes)} — unbind it there first.";
            return;
        }
        Close();
        await Reload();
    }
}
