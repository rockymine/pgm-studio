using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Features.Library;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The picked modifier's numbers and mask. Each edit sends the field it changed to the bridge as a patch
/// of the modifier, and a mask edit sends the whole mask, which is what the bridge replaces.</summary>
public partial class SketchModifierInspector
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter, EditorRequired] public JsonObject Modifier { get; set; } = [];

    /// <summary>The drawn shapes a modifier may be told to leave out.</summary>
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];

    /// <summary>The groups the shapes sit in, which name them where two shapes would read alike.</summary>
    [Parameter] public IReadOnlyList<SketchGroupRow> Groups { get; set; } = [];

    /// <summary>How many columns this modifier moved, as the server last counted; null when it has not.</summary>
    [Parameter] public int? Changed { get; set; }

    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;

    private const string Name = "name", Off = "off", Mode = "mode", Rise = "rise", Power = "power", Join = "join",
        Soft = "soft", Floor = "floor", Flat = "flat", Slope = "slope", Melt = "melt", Top = "top", Inner = "inner",
        Outer = "outer", Seed = "seed", Cell = "cell", Octaves = "octaves", Wander = "wander", Cliff = "cliff",
        Talus = "talus", Apron = "apron", Jag = "jag", Taper = "taper", Height = "height", Side = "side",
        Depth = "depth", Bed = "bed", Wall = "wall", Ledges = "ledges", Water = "water", Bank = "bank",
        Level = "level", Shore = "shore", Grade = "grade", Shoulder = "shoulder", Count = "count",
        Sharpness = "sharpness", Amount = "amount", Width = "width", Leaving = "leaves", Shelf = "shelf",
        Across = "across", Heights = "heights", Stretch = "stretch", Angle = "angle", Tilt = "tilt", Gap = "gap",
        StepsField = "steps", Start = "start", End = "end", BedMaterial = "bed_material";

    /// <summary>The width a line is traced with, which a mask that states none is read at.</summary>
    private const double DefaultLineWidth = 6;

    private static readonly IReadOnlyList<SelectOption> Modes =
    [
        new("", "Both ways"), new(ModifierModes.Lift, "Only raise"), new(ModifierModes.Cut, "Only lower"),
    ];

    private static readonly IReadOnlyList<SelectOption> Sides =
    [
        new("1", "Right of the line"), new("-1", "Left of the line"),
    ];

    private static readonly IReadOnlyList<SelectOption> ProfileSides =
    [
        new("", "Both sides"), new("1", "Right of the line"), new("-1", "Left of the line"),
    ];

    private string? error;
    private IReadOnlyList<PaintBlockDto> blocks = [];
    private IReadOnlyList<StyleDto> styles = [];

    private string Id => SketchModifierState.Id(Modifier);
    private string Op => SketchModifierState.Op(Modifier);
    private JsonObject Mask => Modifier["mask"] as JsonObject ?? [];
    private bool Mirrored => Mask["mirror"]?.GetValue<bool>() != false;
    private List<JsonObject> Areas => Mask["areas"] is JsonArray areas ? [.. areas.OfType<JsonObject>()] : [];
    private List<JsonObject> Sizes => Modifier["sizes"] is JsonArray sizes ? [.. sizes.OfType<JsonObject>()] : [];
    private List<JsonObject> Steps => Modifier[StepsField] is JsonArray steps ? [.. steps.OfType<JsonObject>()] : [];
    private bool HasLine => Areas.Any(SketchModifierState.IsLine);

    /// <summary>The ids of the shapes the mask leaves out.</summary>
    private HashSet<string> Leaves => Mask[Leaving] is JsonArray leaves
        ? [.. leaves.Select(leaf => leaf?.GetValue<string>()).OfType<string>()]
        : [];

    /// <summary>The shapes that are ground, which are the ones a modifier has columns of to leave out.</summary>
    private IReadOnlyList<SketchShapeRow> Leavable => [.. Shapes.Where(shape => shape.Operation != "subtract")];

    /// <summary>Whether this operation makes water, and so states the bed under it.</summary>
    private bool WetOp => Op is ModifierOps.River or ModifierOps.Lake or ModifierOps.Coast;

    private JsonObject? BedJson => Modifier[BedMaterial] as JsonObject;
    private SlotFill BedSlot => SlotFill.Of(BedJson, styles);

    /// <summary>The unbound row: the default bank where no bed is stated, and the material itself where one is
    /// that no block or pattern names.</summary>
    private string BedUnbound => BedJson is not null && !BedSlot.Bound ? "Custom" : "Default bank";

    protected override async Task OnInitializedAsync()
    {
        if (!WetOp) return;
        blocks = await Library.BlocksAsync();
        styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
    }

    /// <summary>Lays the picked block or pattern as the bed, or takes the bed back to the default bank.</summary>
    private Task FillBed(SlotFill fill) =>
        fill.ToMaterial(styles) is { } material ? Patch(new JsonObject { [BedMaterial] = material }) : Clear(BedMaterial);

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static double? Number(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static string ChangedLine(int columns) =>
        $"Changed {columns} column{(columns == 1 ? "" : "s")}.";

    /// <summary>What the mask holds, and how it is changed: on the board, as every outline is.</summary>
    private string AreaLine
    {
        get
        {
            var lines = Areas.Count(SketchModifierState.IsLine);
            var outlines = Areas.Count - lines;
            if (lines == 0 && outlines == 0) return "No outline: it works on the whole board.";
            var parts = new List<string>();
            if (outlines > 0) parts.Add(outlines == 1 ? "one outline" : $"{outlines} outlines");
            if (lines > 0) parts.Add(lines == 1 ? "one line" : $"{lines} lines");
            var held = string.Join(" and ", parts);
            return $"{char.ToUpperInvariant(held[0])}{held[1..]}. Drag {(Areas.Count == 1 ? "it" : "them")}, or their points, on the board.";
        }
    }

    private string LineWidthLabel(int index)
    {
        var lines = Areas.Take(index + 1).Count(SketchModifierState.IsLine);
        return Areas.Count(SketchModifierState.IsLine) > 1 ? $"Line {lines} width" : "Line width";
    }

    /// <summary>What a shape is called in the list of what the modifier may leave out: its kind and size, and the
    /// group it belongs to where there is one.</summary>
    private string ShapeLabel(SketchShapeRow shape)
    {
        var group = Groups.FirstOrDefault(entry => entry.ShapeIds.Contains(shape.Id));
        var named = $"{shape.Type} {shape.Dim}";
        return group is null ? named : $"{group.Name}: {named}";
    }

    private Task SetNumber(string field, double value) => Patch(new JsonObject { [field] = value });

    private Task Clear(string field) => Patch(new JsonObject { [field] = null });

    private Task SetText(string field, string? text) =>
        Patch(new JsonObject { [field] = string.IsNullOrWhiteSpace(text) ? null : text.Trim() });

    private Task SetBool(string field, bool on) => Patch(new JsonObject { [field] = on });

    private Task SetSide(string side) => Patch(new JsonObject { [Side] = side == "-1" ? -1 : 1 });

    private Task SetProfileSide(string side) =>
        Patch(new JsonObject { [Side] = side switch { "-1" => -1, "1" => 1, _ => null } });

    /// <summary>A stated <c>[least, most]</c>, or the operation's own range where none is stated.</summary>
    private (double Least, double Most) RangeOf(string field, double least, double most) =>
        Modifier[field] is JsonArray { Count: 2 } range && range[0] is JsonValue low && low.TryGetValue<double>(out var from)
        && range[1] is JsonValue high && high.TryGetValue<double>(out var to) ? (from, to) : (least, most);

    private Task SetRange(string field, double least, double most) =>
        Patch(new JsonObject { [field] = new JsonArray(Math.Min(least, most), Math.Max(least, most)) });

    private Task SetStep(int index, string field, double value) => EditSteps(steps => steps[index]![field] = value);

    private Task RemoveStep(int index) => EditSteps(steps => steps.RemoveAt(index));

    /// <summary>A new step a few blocks beyond the last, climbing six more.</summary>
    private Task AddStep() => EditSteps(steps =>
    {
        var last = steps.OfType<JsonObject>().LastOrDefault();
        var (end, rise) = last is null ? (0.0, 0.0) : (Number(last, End) ?? 0, Number(last, Rise) ?? 0);
        steps.Add(new JsonObject { [Start] = end + 4, [End] = end + 7, [Rise] = rise + 6 });
    });

    private Task EditSteps(Action<JsonArray> edit)
    {
        var steps = Modifier[StepsField]?.DeepClone() as JsonArray ?? [];
        edit(steps);
        return Patch(new JsonObject { [StepsField] = steps });
    }

    private Task SetMirror(bool on) => PatchMask(mask => mask["mirror"] = on);

    private Task SetLineWidth(int index, double width) =>
        PatchMask(mask => mask["areas"]![index]![Width] = width);

    /// <summary>Ticks or clears one shape in the mask's <c>leaves</c>. The key is dropped when none is left, and
    /// a shape that has since been deleted is dropped with it.</summary>
    private Task SetLeaves(string shapeId, bool leave)
    {
        var present = Shapes.Select(shape => shape.Id).ToHashSet();
        var kept = Leaves.Where(present.Contains).ToHashSet();
        if (leave) kept.Add(shapeId); else kept.Remove(shapeId);
        return PatchMask(mask =>
        {
            if (kept.Count == 0) mask.Remove(Leaving);
            else mask[Leaving] = new JsonArray([.. Shapes.Where(shape => kept.Contains(shape.Id)).Select(shape => (JsonNode)shape.Id)]);
        });
    }

    private Task PatchMask(Action<JsonObject> edit)
    {
        var mask = (JsonObject)Mask.DeepClone();
        edit(mask);
        return Patch(new JsonObject { ["mask"] = mask });
    }

    private Task SetSize(int index, string field, double value) => EditSizes(sizes => sizes[index]![field] = value);

    private Task RemoveSize(int index) => EditSizes(sizes => sizes.RemoveAt(index));

    private Task AddSize() => EditSizes(sizes =>
    {
        var largest = sizes.OfType<JsonObject>().Select(size => Number(size, Cell) ?? 12).DefaultIfEmpty(12).Max();
        sizes.Add(new JsonObject { [Cell] = largest * 3, [Octaves] = 2, [Rise] = 2 });
    });

    private Task EditSizes(Action<JsonArray> edit)
    {
        var sizes = Modifier["sizes"]?.DeepClone() as JsonArray ?? [];
        edit(sizes);
        return Patch(new JsonObject { ["sizes"] = sizes });
    }

    private async Task Patch(JsonObject patch)
    {
        if (Handle is null) return;
        error = await Handle.InvokeAsync<string?>("updateModifier", Id, patch.ToJsonString());
    }

    private Task AddOutline() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("addOutline", Id).AsTask();

    private Task Close() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("selectModifier", (string?)null).AsTask();

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteModifier", Id).AsTask();
}
