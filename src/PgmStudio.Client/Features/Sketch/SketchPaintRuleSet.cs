using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The layout's paint rules (<c>docs/world-export/terrain-painting.md</c>, TP28) as the bridge announces
/// them, and every edit to them: the Palette phase's list orders and switches them, its inspector edits one, and
/// both send the whole list to the bridge's <c>setPaint</c>, so each change is one undo step.</summary>
public static class SketchPaintRuleSet
{
    public const string Id = "id", Name = "name", Off = "off", Material = "material", Depth = "depth",
        Slope = "slope", Height = "height", Noise = "noise", Near = "near", Modifier = "modifier", Width = "width",
        Cell = "cell", Above = "above";

    /// <summary>The rules in the order a column meets them; none where the text does not read.</summary>
    public static List<JsonObject> Parse(string? paintJson)
    {
        try { return JsonNode.Parse(paintJson ?? "[]") is JsonArray rules ? [.. rules.OfType<JsonObject>()] : []; }
        catch (JsonException) { return []; }
    }

    public static string? Text(JsonObject rule, string field) => rule[field]?.GetValue<string>();

    public static string IdOf(JsonObject rule) => Text(rule, Id) ?? "";

    /// <summary>What a rule is called: its name, or its id where it has none.</summary>
    public static string NameOf(JsonObject rule) => Text(rule, Name) is { Length: > 0 } name ? name : IdOf(rule);

    public static bool IsOff(JsonObject rule) => rule[Off]?.GetValue<bool>() == true;

    public static void SetOrRemove(JsonObject rule, string field, JsonNode? value)
    {
        if (value is null) rule.Remove(field); else rule[field] = value;
    }

    /// <summary>The next free <c>rule-N</c>.</summary>
    public static string NextId(IEnumerable<JsonObject> rules)
    {
        var taken = rules.Select(IdOf).ToHashSet();
        var number = 1;
        while (taken.Contains($"rule-{number}")) number++;
        return $"rule-{number}";
    }

    /// <summary>Add a rule at the end and answer its id.</summary>
    public static async Task<(string Id, string? Error)> AddAsync(IJSObjectReference? handle, string? paintJson)
    {
        var rules = Parse(paintJson);
        var id = NextId(rules);
        rules.Add(new JsonObject { [Id] = id });
        return (id, await SendAsync(handle, rules));
    }

    public static Task<string?> RemoveAsync(IJSObjectReference? handle, string? paintJson, string id)
    {
        var rules = Parse(paintJson);
        rules.RemoveAll(rule => IdOf(rule) == id);
        return SendAsync(handle, rules);
    }

    /// <summary>Move a rule <paramref name="by"/> places in the order a column meets them.</summary>
    public static Task<string?> MoveAsync(IJSObjectReference? handle, string? paintJson, string id, int by)
    {
        var rules = Parse(paintJson);
        var from = rules.FindIndex(rule => IdOf(rule) == id);
        var to = from + by;
        if (from < 0 || to < 0 || to >= rules.Count) return Task.FromResult<string?>(null);
        (rules[from], rules[to]) = (rules[to], rules[from]);
        return SendAsync(handle, rules);
    }

    public static Task<string?> EditAsync(IJSObjectReference? handle, string? paintJson, string id, Action<JsonObject> edit)
    {
        var rules = Parse(paintJson);
        var rule = rules.FirstOrDefault(each => IdOf(each) == id);
        if (rule is null) return Task.FromResult<string?>(null);
        edit(rule);
        return SendAsync(handle, rules);
    }

    /// <summary>Hand the whole list to the bridge, answering why it was refused or null.</summary>
    private static async Task<string?> SendAsync(IJSObjectReference? handle, List<JsonObject> rules)
    {
        if (handle is null) return null;
        var array = new JsonArray([.. rules.Select(rule => (JsonNode)rule.DeepClone())]);
        return await handle.InvokeAsync<string?>("setPaint", array.ToJsonString());
    }
}
