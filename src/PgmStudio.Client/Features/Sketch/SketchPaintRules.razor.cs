using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The board's paint rules (<c>docs/world-export/terrain-painting.md</c>, TP28), listed in the order a
/// column meets them and edited in place. Every edit sends the whole list to the bridge's <c>setPaint</c>.</summary>
public partial class SketchPaintRules
{
    [Parameter] public IJSObjectReference? Handle { get; set; }

    /// <summary>The rules as the bridge last announced them, JSON array text.</summary>
    [Parameter] public string PaintJson { get; set; } = "[]";

    /// <summary>The board's modifier ids, which a rule may be laid near.</summary>
    [Parameter] public IReadOnlyList<string> ModifierIds { get; set; } = [];

    [Parameter] public IReadOnlyList<StyleDto> Styles { get; set; } = [];
    [Parameter] public IReadOnlyList<PaintBlockDto> Blocks { get; set; } = [];

    private const string Id = "id", Name = "name", Off = "off", Material = "material", Depth = "depth",
        Slope = "slope", Height = "height", Noise = "noise", Near = "near", Modifier = "modifier", Width = "width",
        Cell = "cell", Above = "above";

    private string? error;

    private List<JsonObject> Rules
    {
        get
        {
            try { return JsonNode.Parse(PaintJson) is JsonArray rules ? [.. rules.OfType<JsonObject>()] : []; }
            catch (JsonException) { return []; }
        }
    }

    private IReadOnlyList<SelectOption> NearOptions => [.. ModifierIds.Select(id => new SelectOption(id, id))];

    private static string? Text(JsonObject rule, string field) => rule[field]?.GetValue<string>();

    private static double? Number(JsonNode? node, string field) =>
        node?[field] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static double? Pair(JsonObject rule, string field, int end) =>
        rule[field] is JsonArray pair && pair.Count > end && pair[end] is JsonValue value
        && value.TryGetValue<double>(out var number) ? number : null;

    private SlotFill SlotOf(JsonObject rule) => SlotFill.Of(rule[Material] as JsonObject, Styles);

    private static void SetOrRemove(JsonObject rule, string field, JsonNode? value)
    {
        if (value is null) rule.Remove(field); else rule[field] = value;
    }

    private Task Fill(int index, SlotFill fill) => Edit(index, rule => SetOrRemove(rule, Material, fill.ToMaterial(Styles)));

    private Task SetPair(int index, string field, int end, double? value) => Edit(index, rule =>
    {
        var pair = rule[field] as JsonArray;
        JsonNode? first = pair is { Count: > 0 } ? pair[0]?.DeepClone() : null;
        JsonNode? second = pair is { Count: > 1 } ? pair[1]?.DeepClone() : null;
        if (end == 0) first = value is { } low ? JsonValue.Create((int)low) : null;
        else second = value is { } high ? JsonValue.Create((int)high) : null;
        SetOrRemove(rule, field, first is null && second is null ? null : new JsonArray(first, second));
    });

    private Task SetNear(int index, string modifier) => Edit(index, rule =>
        SetOrRemove(rule, Near, string.IsNullOrEmpty(modifier) ? null
            : new JsonObject { [Modifier] = modifier, [Width] = Number(rule[Near], Width) ?? 3 }));

    private Task SetPatches(int index, bool on) => Edit(index, rule =>
        SetOrRemove(rule, Noise, on ? new JsonObject { [Cell] = 8, [Above] = 0.5, ["seed"] = 1 } : null));

    private Task Add()
    {
        var rules = Rules;
        var taken = rules.Select(rule => Text(rule, Id)).ToHashSet();
        var number = 1;
        while (taken.Contains($"rule-{number}")) number++;
        rules.Add(new JsonObject { [Id] = $"rule-{number}" });
        return Send(rules);
    }

    private Task Remove(int index)
    {
        var rules = Rules;
        rules.RemoveAt(index);
        return Send(rules);
    }

    private Task Move(int index, int by)
    {
        var rules = Rules;
        var to = index + by;
        if (to < 0 || to >= rules.Count) return Task.CompletedTask;
        (rules[index], rules[to]) = (rules[to], rules[index]);
        return Send(rules);
    }

    private Task Edit(int index, Action<JsonObject> edit)
    {
        var rules = Rules;
        if (index < 0 || index >= rules.Count) return Task.CompletedTask;
        edit(rules[index]);
        return Send(rules);
    }

    private async Task Send(List<JsonObject> rules)
    {
        if (Handle is null) return;
        var array = new JsonArray([.. rules.Select(rule => (JsonNode)rule.DeepClone())]);
        error = await Handle.InvokeAsync<string?>("setPaint", array.ToJsonString());
    }
}
