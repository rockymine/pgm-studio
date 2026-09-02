using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The editor for the two recipes a placement names: a tree and a boulder.
///
/// <para>One component for both, the way <c>HousePartEditor</c> serves the three house parts — they share a
/// frame, a save path and a card, and differ only in the fields between. What they have in common is more
/// interesting than what separates them: each is what a <em>click</em> puts down, so neither has a geometry to
/// draw and both are wholly described by their numbers plus a picture of the result.</para>
/// </summary>
public partial class PropRecipeEditor
{
    [Parameter, EditorRequired] public LibraryKind Kind { get; set; } = default!;
    [Parameter, EditorRequired] public string Entry { get; set; } = "";
    [Parameter] public EventCallback<string?> OnSaved { get; set; }
    [Parameter] public EventCallback<string> OnName { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private long? editingId;
    private string draftName = "";
    private string? note;
    private string? card;
    private string loaded = "";
    private string selected = KnobsPart;

    private TreeStyleSaveRequest? tree;
    private BoulderStyleSaveRequest? boulder;

    /// <summary>A boulder's rock is a full terrain material, edited as its own node the way every other
    /// material in the library is — so an erratic may be cut from any of the fourteen kinds.</summary>
    private JsonObject rock = ThemeFields.Solid(1);

    private IReadOnlyList<PaintBlockDto> blocks = [];
    private IReadOnlyList<StyleDto> styles = [];

    private const string KnobsPart = "knobs";

    private bool IsTree => Kind.Slug == LibraryKinds.TreesSlug;

    private IReadOnlyList<EditorPart> Outline =>
        [new(KnobsPart, IsTree ? "The tree" : "The rock", IsTree ? "trees" : "mountain")];

    private string Footnote => IsTree
        ? "A placement names this recipe. Retuning it retunes every tree wearing it."
        : "A placement names this recipe. Retuning it retunes every boulder wearing it.";

    protected override async Task OnParametersSetAsync()
    {
        // The guard first, and the fetches after it: a parameter set that re-enters during a fetch would
        // otherwise pass a guard the first one had not reached yet, and load the row twice over itself.
        if (loaded == $"{Kind.Slug}/{Entry}") return;
        loaded = $"{Kind.Slug}/{Entry}";

        if (blocks.Count == 0) blocks = await Library.BlocksAsync();
        if (styles.Count == 0) styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
        note = null;
        selected = KnobsPart;
        (tree, boulder) = (null, null);
        if (long.TryParse(Entry, out var id)) await Load(id);
        else StartNew();
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private void StartNew()
    {
        editingId = null;
        draftName = "";
        rock = ThemeFields.Solid(1);
        if (IsTree) tree = new TreeStyleSaveRequest("", TreeForms.Template, "oak", "oak", Height: 12);
        else boulder = new BoulderStyleSaveRequest("", BoulderForms.Round, 4, Mossy: true, rock.ToJsonString());
    }

    private async Task Load(long id)
    {
        if (IsTree)
        {
            if (await Library.GetAsync<TreeStyleDetail>(Kind, id) is not { } detail)
            {
                note = "That recipe could not be read.";
                return;
            }
            (editingId, draftName) = (detail.Id, detail.Name);
            tree = new TreeStyleSaveRequest(
                detail.Name, detail.Form, detail.Species, detail.Wood, detail.Height, detail.Stems,
                detail.Leader, detail.Flow, detail.BranchAngle, detail.Levels, detail.Whorled, detail.LeafSize,
                detail.Body);
            return;
        }

        if (await Library.GetAsync<BoulderStyleDetail>(Kind, id) is not { } rockDetail)
        {
            note = "That recipe could not be read.";
            return;
        }
        (editingId, draftName) = (rockDetail.Id, rockDetail.Name);
        rock = JsonNode.Parse(rockDetail.Rock) as JsonObject ?? ThemeFields.Solid(1);
        boulder = new BoulderStyleSaveRequest(
            rockDetail.Name, rockDetail.Form, rockDetail.Size, rockDetail.Mossy, rockDetail.Rock);
    }

    private Task Choose(string part) { selected = part; return Task.CompletedTask; }

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    private Task Tree(Func<TreeStyleSaveRequest, TreeStyleSaveRequest> edit)
    {
        if (tree is null) return Task.CompletedTask;
        tree = edit(tree);
        return Preview();
    }

    /// <summary>Switching form keeps every field, because the three are three trees rather than one: an author
    /// who tries the grown skeleton and goes back finds the species they had chosen still chosen, and a copied
    /// body survives a look at what the same recipe would be as a template.</summary>
    private Task SetTreeForm(string form) => Tree(t => t with { Form = TreeForms.Canonical(form) });

    private Task Boulder(Func<BoulderStyleSaveRequest, BoulderStyleSaveRequest> edit)
    {
        if (boulder is null) return Task.CompletedTask;
        boulder = edit(boulder);
        return Preview();
    }

    private Task RockChanged() => Boulder(b => b with { Rock = rock.ToJsonString() });

    /// <summary>Re-draw the recipe as it stands, composed server-side by exactly the path a save would take, so
    /// the picture and the save cannot disagree.</summary>
    private async Task Preview()
    {
        card = Draft() is { } draft
            ? (await Library.DraftPreviewAsync<StyleCardDto>(Kind, draft))?.Card
            : null;
        StateHasChanged();
    }

    private object? Draft(string? name = null) => IsTree
        ? tree is null ? null : tree with { Name = name ?? draftName }
        : boulder is null ? null : boulder with { Name = name ?? draftName };

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(draftName)) return;
        if (Draft(draftName.Trim()) is not { } request) { note = "That could not be saved."; return; }
        var saved = editingId is { } id
            ? await Library.UpdateAsync<RecipeSaved>(Kind, id, request)
            : await Library.CreateAsync<RecipeSaved>(Kind, request);
        if (saved is null) { note = "That could not be saved."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{Kind.Slug}/{saved.Id}");
        else editingId = saved.Id;
    }

    private async Task SaveAsCopy()
    {
        if (Draft($"{draftName.Trim()} copy") is not { } request) return;
        if (await Library.CreateAsync<RecipeSaved>(Kind, request) is not { } saved)
        {
            note = "That could not be copied.";
            return;
        }
        await OnSaved.InvokeAsync("copied");
        Nav.NavigateTo($"/library/{Kind.Slug}/{saved.Id}");
    }

    /// <summary>The one field a save's answer is read for.</summary>
    private sealed record RecipeSaved(long Id);

    /// <summary>Forget the open recipe. Nothing asks first, because nothing binds it: a placement names a key
    /// in its own document's registry, which the pull copied, so a map keeps its trees when the row they were
    /// pulled from goes.</summary>
    private async Task Delete()
    {
        if (editingId is not { } id) return;
        if (await Library.DeleteAsync(Kind, id) is { Deleted: false }) { note = "That could not be forgotten."; return; }
        Nav.NavigateTo($"/library/{Kind.Slug}");
    }

    private static double Number(ChangeEventArgs e, double fallback)
        => double.TryParse(e.Value?.ToString(), out var value) ? value : fallback;

    private static double Share(ChangeEventArgs e, double fallback)
        => double.TryParse(e.Value?.ToString(), out var value) ? Math.Clamp(value / 100, 0, 1) : fallback;

    /// <summary>The slider is in degrees because that is the thing being chosen; the recipe stores the radian
    /// the grower reads, held to the range it builds in.</summary>
    private static double Radians(ChangeEventArgs e, double fallback)
        => double.TryParse(e.Value?.ToString(), out var value)
            ? Math.Clamp(value * Math.PI / 180, 0.2, 1.5) : fallback;
}
