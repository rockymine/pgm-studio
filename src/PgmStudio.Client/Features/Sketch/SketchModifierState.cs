using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The modifier stack as the bridge announces it (<c>OnModifiers</c>): the layout's <c>modifiers</c> in
/// the order they run, the one picked, the step the ground is read at and the columns each modifier moved.</summary>
/// <param name="Step">How many of the modifiers that are on the ground is read after (0 is the shapes alone),
/// or null when it is read after all of them.</param>
/// <param name="Changes">Columns each modifier moved, by modifier id; empty while the server has not counted.</param>
public sealed record SketchModifierState(
    IReadOnlyList<JsonObject> Modifiers, string? SelectedId, int? Step = null,
    IReadOnlyDictionary<string, int>? Changes = null)
{
    public static readonly SketchModifierState Empty = new([], null);

    public JsonObject? Selected => Modifiers.FirstOrDefault(modifier => Id(modifier) == SelectedId);

    /// <summary>The modifiers a step counts: the ones not switched off, in the order they run.</summary>
    public IReadOnlyList<JsonObject> Running => [.. Modifiers.Where(modifier => !Off(modifier))];

    /// <summary>The step the strip stands at: the last one when the ground is read after every modifier.</summary>
    public int StepAt => Step ?? Running.Count;

    /// <summary>What the strip calls a step: the shapes alone at 0, else the modifier it ends with.</summary>
    public string StepName(int step) =>
        step <= 0 ? "the shapes" : step <= Running.Count ? Name(Running[step - 1]) : "";

    /// <summary>How many columns the modifier named <paramref name="id"/> moved, or null when not counted.</summary>
    public int? ChangedBy(string id) => Changes is not null && Changes.TryGetValue(id, out var columns) ? columns : null;

    public static SketchModifierState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root) return Empty;
            var modifiers = root["modifiers"] is JsonArray list ? list.OfType<JsonObject>().ToList() : [];
            var changes = root["changes"] is JsonObject counted
                ? counted.Where(entry => entry.Value is JsonValue value && value.TryGetValue<int>(out _))
                         .ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<int>())
                : null;
            int? step = root["step"] is JsonValue stepped && stepped.TryGetValue<int>(out var at) ? at : null;
            return new SketchModifierState(modifiers, root["selectedId"]?.GetValue<string>(), step, changes);
        }
        catch (JsonException) { return Empty; }
    }

    public static string Id(JsonObject modifier) => modifier["id"]?.GetValue<string>() ?? "";
    public static string Op(JsonObject modifier) => modifier["op"]?.GetValue<string>() ?? "";
    public static bool Off(JsonObject modifier) => modifier["off"]?.GetValue<bool>() == true;

    /// <summary>What a row calls the modifier: its own name, or its id.</summary>
    public static string Name(JsonObject modifier) =>
        modifier["name"]?.GetValue<string>() is { Length: > 0 } named ? named : Id(modifier);

    /// <summary>The dock tool that traces a new modifier of <paramref name="op"/>.</summary>
    public static string Tool(string op) => $"modifier:{op}";

    /// <summary>Whether a traced area is an open line with a width rather than an outline.</summary>
    public static bool IsLine(JsonObject area) => area["kind"]?.GetValue<string>() == MaskKinds.Line;

    public static string Icon(string op) => op switch
    {
        ModifierOps.Mound => "terrain-hill",
        ModifierOps.Crater => "terrain-hollow",
        ModifierOps.Level => "terrain-level",
        ModifierOps.Noise => "terrain-roughness",
        ModifierOps.Ridge => "terrain-ridge",
        ModifierOps.Butte => "terrain-butte",
        ModifierOps.Spire => "terrain-spire",
        ModifierOps.Scarp => "terrain-scarp",
        ModifierOps.Canyon => "terrain-canyon",
        ModifierOps.River => "terrain-river",
        ModifierOps.Lake => "terrain-lake",
        ModifierOps.Road => "terrain-road",
        ModifierOps.Gullies => "terrain-gullies",
        ModifierOps.Warp => "terrain-warp",
        ModifierOps.Profile => "terrain-profile",
        ModifierOps.Coast => "terrain-coast",
        ModifierOps.Lumps => "terrain-lumps",
        ModifierOps.Shatter => "terrain-shatter",
        ModifierOps.Tunnel => "terrain-tunnel",
        ModifierOps.Chamber => "terrain-chamber",
        ModifierOps.Shaft => "terrain-shaft",
        _ => "shapes",
    };

    /// <summary>What an operation does, as the description the inspector heads with.</summary>
    public static string Blurb(string op) => op switch
    {
        ModifierOps.Mound => "Raises the ground inside its area into a rounded rise, highest at its deepest point. A negative rise with Only lower sinks a dish.",
        ModifierOps.Crater => "Sinks a bowl to its floor, flat at its centre and rounded into the ground over its skirt.",
        ModifierOps.Level => "Brings the ground inside its area to one height and eases it back into the ground around: a plaza, a fighting floor.",
        ModifierOps.Noise => "Adds noise inside its area, in one or more patch sizes.",
        ModifierOps.Ridge => "Raises a crest along the line, its foot wandering by noise.",
        ModifierOps.Butte => "Lifts a flat top inside its area with a steep face and a talus slope round it: a mesa, a plateau.",
        ModifierOps.Spire => "Raises a narrow peak that tapers from the edge of its area to a point.",
        ModifierOps.Scarp => "Lifts one side of the line as a step, with a face on the line and a talus slope below it.",
        ModifierOps.Canyon => "Cuts a trench along the line: a floor, walls and, if asked, ledges. It only cuts.",
        ModifierOps.River => "Cuts a bed along the line and fills it with water, the banks easing into the ground over its skirt.",
        ModifierOps.Lake => "Cuts a basin inside its area and fills it with water to one level, the shore easing into the ground over its skirt.",
        ModifierOps.Road => "Grades the ground along the line so it climbs no faster than its grade, easing into the ground over its skirt.",
        ModifierOps.Gullies => "Cuts channels down the slope inside its area, meandering as they fall.",
        ModifierOps.Warp => "Shifts the ground inside its area by noise, so straight forms bend.",
        ModifierOps.Profile => "Sets the ground by its distance from the line: a floor, then steps of face and bench out to a plateau.",
        ModifierOps.Coast => "Floods its area as a sea, its floor falling from the shore, and eases the ground around down to a beach over its skirt.",
        ModifierOps.Lumps => "Strews up to its count of hummocks and fallen blocks over its area, each its own size, height and heading.",
        ModifierOps.Shatter => "Breaks the ground inside its area into patches, each lifted and tilted on its own, with clefts cut between them.",
        ModifierOps.Tunnel => "Carves a passage along the line, open to the sky where its cover runs thin. It is cut only in the built world.",
        ModifierOps.Chamber => "Carves a domed hall under its area, keeping its cover of ground over it. It is cut only in the built world.",
        ModifierOps.Shaft => "Carves a well from its floor up to the sky, with a ladder up its south wall. It is cut only in the built world.",
        _ => "",
    };
}
