using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// Authors one material. The thing being edited is the material's own JSON node, exactly as the sketch's theme
/// phase edits a bucket's — a style <em>is</em> a material, so one <see cref="MaterialEditor"/> authors both.
/// What this adds is the library's half: a name, the kind read back off the node the editor rewrote, the
/// nest as an outline, and the save.
/// </summary>
public partial class StyleEditor
{
    /// <summary>The row being edited — an id, or <c>new</c>.</summary>
    [Parameter, EditorRequired] public string Entry { get; set; } = "";

    /// <summary>Reports what the last save said, for the topbar.</summary>
    [Parameter] public EventCallback<string?> OnSaved { get; set; }

    /// <summary>Reports the name as it is typed, for the crumb trail.</summary>
    [Parameter] public EventCallback<string> OnName { get; set; }

    /// <summary>What a kind is and what it takes — the outline is the material tree, and the tree's shape is
    /// the schema's.</summary>
    [Inject] public MaterialSchema Schema { get; set; } = default!;

    private const string Plan = "plan", Section = "section", Both = "both";

    private IReadOnlyList<PaintBlockDto> blocks = [];
    private IReadOnlyList<StyleDto> styles = [];
    private JsonObject draft = ThemeFields.Solid(1);
    private long? editingId;
    private string draftName = "";
    private string selected = "";
    private string view = Both;
    private string? note;
    private MaterialPreviewDto? preview;

    /// <summary>The kind is whatever the node says it is — the editor's kind switch rewrites the node
    /// wholesale, so reading it back is the only way the two cannot disagree.</summary>
    private string DraftKind => JsonEdit.KindOf(draft);

    private bool CanSave => !string.IsNullOrWhiteSpace(draftName);

    private IReadOnlyList<EditorPart> Outline =>
    [
        .. MaterialTree.Walk(draft, Schema, Schema.NameOf(DraftKind))
            .Select(found => new EditorPart(
                found.Path, found.Label,
                Badge: Schema.NameOf(JsonEdit.KindOf(found.Node)),
                Swatch: null, Depth: found.Depth)),
    ];

    /// <summary>The node the outline has picked, or null once a list has been shortened past it.</summary>
    private JsonObject? Picked => MaterialTree.At(draft, selected);

    private string Footnote => $"{Outline.Count} material{(Outline.Count == 1 ? "" : "s")} · {Schema.NameOf(DraftKind)}";

    protected override async Task OnInitializedAsync()
    {
        blocks = await Library.BlocksAsync();
        styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
    }

    /// <summary>The styles a slot here may be filled from — every one but the row being edited. A style
    /// filled into itself would land the saved version inside the draft of the same thing, which is a copy
    /// wearing the name of its original and reads as a loop even though it is not one.</summary>
    private IReadOnlyList<StyleDto> Fillable =>
        editingId is { } id ? [.. styles.Where(style => style.Id != id)] : styles;

    /// <summary>What the draft was loaded for. A parameter set that does not move the route is the host
    /// re-rendering — reloading there would re-read the row, report the name back up, and re-render the host
    /// again.</summary>
    private string? loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (loaded == Entry) return;
        loaded = Entry;
        note = null;
        selected = "";
        if (long.TryParse(Entry, out var id))
        {
            if (await Library.GetAsync<StyleDto>(LibraryKinds.Styles, id) is not { } row)
            {
                note = "That style could not be read.";
                return;
            }
            editingId = row.Id;
            draftName = row.Name;
            draft = JsonNode.Parse(row.Params) as JsonObject ?? ThemeFields.Solid(1);
        }
        else
        {
            editingId = null;
            draftName = "";
            draft = ThemeFields.Solid(1);
        }
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    private void Pick(string path) => selected = path;

    private Task DraftEdited() => Preview();

    private async Task Preview()
    {
        preview = await Library.MaterialPreviewAsync(draft.ToJsonString());
        StateHasChanged();
    }

    private async Task Save()
    {
        if (!CanSave) return;
        var request = new StyleSaveRequest(draftName.Trim(), DraftKind, draft.ToJsonString());
        var saved = editingId is { } id
            ? await Library.UpdateAsync<StyleDto>(LibraryKinds.Styles, id, request)
            : await Library.CreateAsync<StyleDto>(LibraryKinds.Styles, request);
        if (saved is null) { note = "The library refused that style."; return; }

        // An edit reaches every theme binding this style — the library is the shared copy, and a map's applied
        // theme is its own snapshot, so nothing already exported moves.
        note = editingId is null ? "Added to the library." : "Saved. Every theme binding it now paints this.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{LibraryKinds.StylesSlug}/{saved.Id}");
        else editingId = saved.Id;
    }

    private async Task SaveAsCopy()
    {
        if (!CanSave) return;
        var copy = await Library.CreateAsync<StyleDto>(LibraryKinds.Styles,
            new StyleSaveRequest($"{draftName.Trim()} copy", DraftKind, draft.ToJsonString()));
        if (copy is null) { note = "The library refused that style."; return; }
        Nav.NavigateTo($"/library/{LibraryKinds.StylesSlug}/{copy.Id}");
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        if (await Library.DeleteAsync(LibraryKinds.Styles, id) is { Deleted: false } refused)
        {
            note = refused.BoundBy.Count > 0
                ? $"Still bound by {string.Join(", ", refused.BoundBy)} — unbind it there first."
                : "That style could not be forgotten.";
            return;
        }
        Nav.NavigateTo($"/library/{LibraryKinds.StylesSlug}");
    }
}
