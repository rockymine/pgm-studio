using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The editor for one biome field: which kind it is, the numbers that kind states, and the biomes it picks
/// between.
///
/// <para>A field places no block — it is the byte a client reads to tint grass, leaves and water — so there is
/// no geometry to draw and the whole of it is its numbers plus a patch of ground showing what they do. The
/// card is that patch, drawn through the export's own palette, so a <c>solid</c> row reads as a flat green and
/// a <c>cell</c> row as the regions it actually lays down.</para>
/// </summary>
public partial class BiomeEditor
{
    [Parameter, EditorRequired] public string Entry { get; set; } = "";
    [Parameter] public EventCallback<string?> OnSaved { get; set; }
    [Parameter] public EventCallback<string> OnName { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private static readonly LibraryKind Kind = LibraryKinds.Biomes;

    private const string FieldPart = "field";

    private long? editingId;
    private string draftName = "";
    private string? note;
    private string? card;
    private string loaded = "";

    /// <summary>The field being edited, as the document it is stored as — the same shape the pass reads, so
    /// the picture and the save cannot disagree about what was authored.</summary>
    private JsonObject drawn = Started(BiomeKinds.Solid, Plains, Plains);

    /// <summary>The biomes on offer, with the ground colour each tints — served rather than listed here, so a
    /// picker cannot offer one the export writes as something else.</summary>
    private IReadOnlyList<BiomeOptionDto> biomes = [];

    /// <summary>What a field starts on before the offered list has arrived. Plains is the byte every chunk is
    /// created with, so it is the one id the client may name without being told.</summary>
    private const int Plains = 1;

    private string Kinds => drawn["kind"]?.GetValue<string>() ?? BiomeKinds.Solid;

    private IReadOnlyList<EditorPart> Outline => [new(FieldPart, "The field", "sun")];

    private string Footnote =>
        $"{BiomeKinds.Describe(Kinds)} A map takes a copy, so editing this retints nothing already built.";

    protected override async Task OnParametersSetAsync()
    {
        if (loaded == Entry) return;
        loaded = Entry;
        if (biomes.Count == 0) biomes = await Library.BiomesAsync();
        note = null;
        if (long.TryParse(Entry, out var id)) await Load(id);
        else StartNew();
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private int First => biomes.Count > 0 ? biomes[0].Id : Plains;
    private int Second => biomes.Count > 1 ? biomes[1].Id : First;

    /// <summary>A field of the named kind, stating something an author can see straight away — each kind
    /// starts from a state that draws rather than from an empty one they have to fill before the card says
    /// anything.</summary>
    private static JsonObject Started(string kind, int first, int second) => kind switch
    {
        BiomeKinds.Cell => new JsonObject
        {
            ["kind"] = BiomeKinds.Cell, ["seed"] = 1, ["cellSize"] = 32, ["jitter"] = 80,
            ["palette"] = new JsonArray(first, second),
        },
        BiomeKinds.Noise => new JsonObject
        {
            ["kind"] = BiomeKinds.Noise, ["seed"] = 1, ["scale"] = 48, ["octaves"] = 2,
            ["stops"] = new JsonArray(first, second),
        },
        _ => new JsonObject { ["kind"] = BiomeKinds.Solid, ["id"] = first },
    };

    private void StartNew()
    {
        editingId = null;
        draftName = "";
        drawn = Started(BiomeKinds.Solid, First, Second);
    }

    private async Task Load(long id)
    {
        if (await Library.GetAsync<BiomePatternSummary>(Kind, id) is not { } row)
        {
            note = "That pattern could not be read.";
            return;
        }
        (editingId, draftName) = (row.Id, row.Name);
        drawn = JsonNode.Parse(row.Params) as JsonObject ?? Started(BiomeKinds.Solid, First, Second);
    }

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    /// <summary>Switching kind starts the new one fresh rather than carrying numbers across: a cell's region
    /// size and a noise field's scale are different quantities under a shared word, and keeping one as the
    /// other is how a field comes out at a scale nobody chose.</summary>
    private Task SetKind(string kind)
    {
        drawn = Started(kind, First, Second);
        return Preview();
    }

    /// <summary>The numbers a field states, by the names it states them under. Named rather than written at
    /// the control, because a Razor attribute cannot carry a quoted string inside a quoted one.</summary>
    private const string SeedField = "seed";
    private const string CellSizeField = "cellSize";
    private const string JitterField = "jitter";
    private const string ScaleField = "scale";
    private const string OctavesField = "octaves";

    /// <summary>The offered biomes as select rows, which is what a palette entry is picked from.</summary>
    private IReadOnlyList<SelectOption> BiomeOptions =>
        [.. biomes.Select(offered => new SelectOption(offered.Id.ToString(), offered.Name))];

    private int Number(string name) => drawn[name]?.GetValue<int>() ?? 0;

    private Task SetNumber(string name, int value)
    {
        drawn[name] = value;
        return Preview();
    }

    /// <summary>Which list this kind picks between — a cell's palette or a noise field's bands.</summary>
    private string ListField => Kinds == BiomeKinds.Cell ? "palette" : "stops";

    private IReadOnlyList<int> Picked =>
        drawn[ListField] is JsonArray list ? [.. list.Select(entry => entry?.GetValue<int>() ?? First)] : [];

    private Task SetAt(int at, int id)
    {
        var picked = new List<int>(Picked);
        if (at < 0) picked.Add(id); else if (at < picked.Count) picked[at] = id; else return Task.CompletedTask;
        drawn[ListField] = new JsonArray([.. picked.Select(entry => JsonValue.Create(entry))]);
        return Preview();
    }

    /// <summary>A field of one biome is a <c>solid</c> under another name, so the last entry stays.</summary>
    private Task DropAt(int at)
    {
        var picked = new List<int>(Picked);
        if (at < 0 || at >= picked.Count || picked.Count <= 1) return Task.CompletedTask;
        picked.RemoveAt(at);
        drawn[ListField] = new JsonArray([.. picked.Select(entry => JsonValue.Create(entry))]);
        return Preview();
    }

    private int SolidId => drawn["id"]?.GetValue<int>() ?? First;

    private Task SetSolid(int id)
    {
        drawn["id"] = id;
        return Preview();
    }

    private string Hex(int id) => biomes.FirstOrDefault(offered => offered.Id == id)?.Hex ?? "#555555";

    /// <summary>Re-draw the field as it stands, through the same path a save would take.</summary>
    private async Task Preview()
    {
        card = (await Library.DraftPreviewAsync<StyleCardDto>(Kind, drawn))?.Card;
        StateHasChanged();
    }

    private BiomePatternSaveRequest Draft(string name) => new(name, Kinds, drawn.ToJsonString());

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(draftName)) return;
        var request = Draft(draftName.Trim());
        var saved = editingId is { } id
            ? await Library.UpdateAsync<BiomeSaved>(Kind, id, request)
            : await Library.CreateAsync<BiomeSaved>(Kind, request);
        if (saved is null) { note = "That could not be saved."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{Kind.Slug}/{saved.Id}");
        else editingId = saved.Id;
    }

    private async Task SaveAsCopy()
    {
        if (await Library.CreateAsync<BiomeSaved>(Kind, Draft($"{draftName.Trim()} copy")) is not { } saved)
        {
            note = "That could not be copied.";
            return;
        }
        await OnSaved.InvokeAsync("copied");
        Nav.NavigateTo($"/library/{Kind.Slug}/{saved.Id}");
    }

    /// <summary>The one drawn a save's answer is read for.</summary>
    private sealed record BiomeSaved(long Id);

    /// <summary>Forget the open pattern. Nothing asks first: a map holds a snapshot rather than a key into
    /// this library, so no board changes when the row goes.</summary>
    private async Task Delete()
    {
        if (editingId is not { } id) return;
        if (await Library.DeleteAsync(Kind, id) is { Deleted: false })
        { note = "That could not be forgotten."; return; }
        Nav.NavigateTo($"/library/{Kind.Slug}");
    }
}
