using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The modifier stack as the bridge announces it (<c>OnModifiers</c>): the layout's <c>modifiers</c> in
/// the order they run, and the one picked.</summary>
public sealed record SketchModifierState(IReadOnlyList<JsonObject> Modifiers, string? SelectedId)
{
    public static readonly SketchModifierState Empty = new([], null);

    public JsonObject? Selected => Modifiers.FirstOrDefault(modifier => Id(modifier) == SelectedId);

    public static SketchModifierState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root) return Empty;
            var modifiers = root["modifiers"] is JsonArray list ? list.OfType<JsonObject>().ToList() : [];
            return new SketchModifierState(modifiers, root["selectedId"]?.GetValue<string>());
        }
        catch (JsonException) { return Empty; }
    }

    public static string Id(JsonObject modifier) => modifier["id"]?.GetValue<string>() ?? "";
    public static string Op(JsonObject modifier) => modifier["op"]?.GetValue<string>() ?? "";
    public static bool Off(JsonObject modifier) => modifier["off"]?.GetValue<bool>() == true;

    /// <summary>What a row calls the modifier: its own name, or its id.</summary>
    public static string Name(JsonObject modifier) =>
        modifier["name"]?.GetValue<string>() is { Length: > 0 } named ? named : Id(modifier);

    /// <summary>Whether the author named the modifier, so its row says the operation beside the name; an unnamed
    /// one's id already is the operation's name.</summary>
    public static bool Named(JsonObject modifier) => modifier["name"]?.GetValue<string>() is { Length: > 0 };

    /// <summary>The dock tool that traces a new modifier of <paramref name="op"/>.</summary>
    public static string Tool(string op) => $"modifier:{op}";

    public static string Icon(string op) => op switch
    {
        ModifierOps.Mound => "mountain",
        ModifierOps.Crater => "circle",
        ModifierOps.Level => "ruler",
        ModifierOps.Noise => "grip",
        _ => "shapes",
    };

    /// <summary>What an operation does, in a sentence the inspector heads with.</summary>
    public static string Blurb(string op) => op switch
    {
        ModifierOps.Mound => "Raises the ground inside the mask, highest where the mask is deepest and falling to nothing at its rim.",
        ModifierOps.Crater => "Sinks a bowl to its floor, flat in the middle and rounded into the ground over its melt.",
        ModifierOps.Level => "Eases the ground to one height inside the mask and back out over its skirt: a plaza, a battlefield floor.",
        ModifierOps.Noise => "Adds noise inside the mask, in one or more sizes.",
        _ => "",
    };
}
