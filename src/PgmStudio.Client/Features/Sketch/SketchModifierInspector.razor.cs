using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The picked modifier's numbers and mask. Each edit sends the field it changed to the bridge as a patch
/// of the modifier, and a mask edit sends the whole mask, which is what the bridge replaces.</summary>
public partial class SketchModifierInspector
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter, EditorRequired] public JsonObject Modifier { get; set; } = [];
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private const string Name = "name", Off = "off", Mode = "mode", Rise = "rise", Power = "power", Join = "join",
        Soft = "soft", Floor = "floor", Flat = "flat", Slope = "slope", Melt = "melt", Top = "top", Inner = "inner",
        Outer = "outer", Seed = "seed", Cell = "cell", Octaves = "octaves", Kind = "kind", At = "at", Rx = "rx",
        Rz = "rz", Angle = "angle", Points = "points";

    private static readonly IReadOnlyList<SelectOption> Modes =
    [
        new("", "Both ways"), new(ModifierModes.Lift, "Only raise"), new(ModifierModes.Cut, "Only lower"),
    ];

    private string? error;

    private string Id => SketchModifierState.Id(Modifier);
    private string Op => SketchModifierState.Op(Modifier);
    private JsonObject Mask => Modifier["mask"] as JsonObject ?? [];
    private bool Mirrored => Mask["mirror"]?.GetValue<bool>() != false;
    private List<JsonObject> Areas => Mask["areas"] is JsonArray areas ? [.. areas.OfType<JsonObject>()] : [];
    private List<JsonObject> Sizes => Modifier["sizes"] is JsonArray sizes ? [.. sizes.OfType<JsonObject>()] : [];

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static double? Number(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static double? Centre(JsonObject area, int axis) =>
        area[At] is JsonArray at && at.Count == 2 && at[axis] is JsonValue value && value.TryGetValue<double>(out var number)
            ? number : null;

    private static string AreaLabel(string kind) => kind switch
    {
        MaskKinds.Polygon => "Outline",
        MaskKinds.Board => "Whole board",
        _ => "Ellipse",
    };

    private Task SetNumber(string field, double value) => Patch(new JsonObject { [field] = value });

    private Task Clear(string field) => Patch(new JsonObject { [field] = null });

    private Task SetText(string field, string? text) =>
        Patch(new JsonObject { [field] = string.IsNullOrWhiteSpace(text) ? null : text.Trim() });

    private Task SetBool(string field, bool on) => Patch(new JsonObject { [field] = on });

    private Task SetMirror(bool on)
    {
        var mask = (JsonObject)Mask.DeepClone();
        mask["mirror"] = on;
        return Patch(new JsonObject { ["mask"] = mask });
    }

    private Task SetCentre(int index, int axis, double value)
    {
        var area = Areas[index];
        double[] at = [Centre(area, 0) ?? 0, Centre(area, 1) ?? 0];
        at[axis] = value;
        return EditArea(index, edited => edited[At] = new JsonArray(at[0], at[1]));
    }

    private Task SetArea(int index, string field, double value) => EditArea(index, edited => edited[field] = value);

    private Task RemoveArea(int index) => EditAreas(areas => areas.RemoveAt(index));

    /// <summary>A new ellipse beside the last area, the size of the first ellipse in the mask.</summary>
    private Task AddArea() => EditAreas(areas =>
    {
        var like = areas.OfType<JsonObject>().LastOrDefault(area => area[Kind]?.GetValue<string>() == MaskKinds.Ellipse);
        var radius = like is null ? 12 : Number(like, Rx) ?? 12;
        var x = (like is null ? 0 : Centre(like, 0) ?? 0) + radius * 1.5;
        var z = like is null ? 0 : Centre(like, 1) ?? 0;
        areas.Add(new JsonObject { [Kind] = MaskKinds.Ellipse, [At] = new JsonArray(x, z), [Rx] = radius, [Rz] = radius });
    });

    private Task EditArea(int index, Action<JsonObject> edit) => EditAreas(areas => edit((JsonObject)areas[index]!));

    private Task EditAreas(Action<JsonArray> edit)
    {
        var mask = (JsonObject)Mask.DeepClone();
        if (mask["areas"] is not JsonArray areas) mask["areas"] = areas = [];
        edit(areas);
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

    private Task Close() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("selectModifier", (string?)null).AsTask();

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteModifier", Id).AsTask();
}
