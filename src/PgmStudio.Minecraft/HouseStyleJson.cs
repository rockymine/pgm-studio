using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft;

/// <summary>
/// Serialization for a room style (docs/world-export/structures.md §7). The JSON <b>is</b> the record graph
/// round-tripped: each part's course stack and extent, plus the knobs that are not materials — the eave, the
/// roof hole and the door.
///
/// <para>This is the form a map <b>snapshots</b>. A map's bound room style is a copy kept with the map rather
/// than a foreign key into the library, the same rule the applied terrain theme follows (M0011): a library
/// edit must never silently rebuild a shipped map's spawn rooms. Nothing consumed it while a room style was
/// only a library row, which is why it lands with the binding rather than before it.</para>
/// </summary>
public static class HouseStyleJson
{
    /// <summary>Canonical options: camelCase names, compact, enums as their camelCase names — so
    /// <c>eave</c> reads <c>"flush"</c> and <c>door</c> reads <c>"stainedGlassPane"</c> rather than an
    /// ordinal nobody could edit by hand or diff usefully.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(HouseStyle style) => JsonSerializer.Serialize(style, Options);

    public static HouseStyle Deserialize(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty house style JSON");
        Upgrade(node);
        return JsonSerializer.Deserialize<HouseStyle>(node, Options)!;
    }

    /// <summary>
    /// Carry a stored style forward onto the current record, in place.
    ///
    /// <para><b>A snapshot outlives the shape it was written in.</b> A map keeps its bound style rather than a
    /// key into the library, so every style ever stored is still out there in a layout blob, and a field that
    /// moves has to be carried rather than dropped — the reader falls back to the built-in shell on anything it
    /// cannot make sense of (<see cref="DeserializeOr"/>), so a shape change without an upgrade is not an error
    /// but a map that quietly stops looking like itself.</para>
    ///
    /// <para>In place and public because a style is snapshotted in <b>two</b> places: on its own, and inside a
    /// house prop in a dressing document. Both readers call this one walk, the way both call
    /// <see cref="TerrainThemeJson.Upgrade"/> — a second upgrade path is a second thing to forget.</para>
    /// </summary>
    public static void Upgrade(JsonNode? node)
    {
        if (node is not JsonObject style) return;

        // The sill, the floor and its zoning were three fields beside everything else; they are the one thing
        // a building stands on. A sill resolving to air was how "no footing" was said before it was a state.
        if (style["foundation"] is null && (style["sill"] ?? style["floor"] ?? style["surface"]) is not null)
        {
            var foundation = new JsonObject();
            if (style["floor"] is { } plate) foundation["plate"] = plate.DeepClone();
            if (style["surface"] is { } surface) foundation["surface"] = surface.DeepClone();
            foundation["footing"] = IsAir(style["sill"]) ? null : style["sill"]?.DeepClone();
            style["foundation"] = foundation;
        }
        style.Remove("sill");
        style.Remove("floor");
        style.Remove("surface");

        // The roof's eleven statements stood beside everything else, one of them named `roof` and holding the
        // material the other ten describe the shape of. They are one part, so that material becomes the part's
        // body and the shape moves in around it — read out before the key it sits under is written over. A
        // stored style names only what it changes, so any one of the old fields alone is the old shape.
        var stored = style["roof"];
        var wasFlat = IsMaterial(stored);
        if (wasFlat || RoofFields.Any(style.ContainsKey))
        {
            style.Remove("roof");                          // detached, so whichever it was can be re-seated
            var roof = !wasFlat && stored is JsonObject part ? part : new JsonObject();
            if (wasFlat) roof["body"] = stored;
            Carry(style, "verge", roof, "verge");
            Carry(style, "gable", roof, "gable");
            Carry(style, "gableWindows", roof, "gableWindows");
            Carry(style, "form", roof, "form");
            Carry(style, "pitch", roof, "pitch");
            Carry(style, "overhang", roof, "overhang");
            Carry(style, "roofHole", roof, "hole");
            Carry(style, "ridgeCap", roof, "ridgeCap");
            Carry(style, "roofSlab", roof, "slab");
            Carry(style, "roofSlabData", roof, "slabData");
            style["roof"] = roof;
        }

        // The doorway's four were four fields beside everything else. `doorEdge` is not one of them and does
        // not move: it is the wall the whole building fronts on — the one a shed roof falls toward and a porch
        // stands against — so it becomes the style's own `front` rather than the doorway's.
        if (style.ContainsKey("doorEdge")) Carry(style, "doorEdge", style, "front");
        if (DoorFields.Any(style.ContainsKey))
        {
            var doorway = style["doorway"] as JsonObject ?? new JsonObject();
            style.Remove("doorway");
            Carry(style, "door", doorway, "door");
            Carry(style, "doorHead", doorway, "head");
            Carry(style, "doorWidth", doorway, "width");
            Carry(style, "doorHeight", doorway, "height");
            style["doorway"] = doorway;
        }

        // A room part was a bare `courses` list, the third spelling of one rule. Its bands now sit under a
        // stack of their own, and a course's height is a band's thickness — one word for one thing.
        Restack(style["wall"]);
        Restack((style["foundation"] as JsonObject)?["plate"]);
        if (style["storeys"] is JsonArray storeys)
            foreach (var storey in storeys) Restack((storey as JsonObject)?["wall"]);

        // Every material in a style is a material like any other, so the material walk runs over a style too.
        // It had not, which is why a style carrying a pre-`bands` voronoi never read forward.
        TerrainThemeJson.Upgrade(style);
    }

    /// <summary>One room part read forward onto its band stack, if it is still written as a course list.</summary>
    private static void Restack(JsonNode? node)
    {
        if (node is not JsonObject part || part["courses"] is not JsonArray courses) return;
        part["stack"] = TerrainThemeJson.Stacked(courses, "height");
        part.Remove("courses");
    }

    /// <summary>The fields the roof's shape was stated in before it was a part of its own. Any of them names
    /// the old shape, because a stored style writes only what it changes.</summary>
    private static readonly string[] RoofFields =
        ["verge", "gable", "gableWindows", "form", "pitch", "overhang", "roofHole", "ridgeCap",
         "roofSlab", "roofSlabData"];

    /// <summary>The fields the way in was stated in before it was a part of its own.</summary>
    private static readonly string[] DoorFields = ["door", "doorHead", "doorWidth", "doorHeight"];

    /// <summary>Moves one stored field onto the part it now belongs to, under the name it now has. A field the
    /// style never named is left unnamed, so the part's own default stands where the record's always did.
    /// </summary>
    private static void Carry(JsonObject style, string from, JsonObject part, string to)
    {
        if (!style.ContainsKey(from)) return;
        part[to] = style[from]?.DeepClone();
        style.Remove(from);
    }

    /// <summary>Whether a stored value is a material rather than a group of fields — how a roof written before
    /// its statements were one part is told from one written after.</summary>
    private static bool IsMaterial(JsonNode? node) => node is JsonObject value && value.ContainsKey("kind");

    /// <summary>Whether a stored material is the bare air one that used to stand in for no footing.</summary>
    private static bool IsAir(JsonNode? material) =>
        material is JsonObject solid
        && solid["kind"]?.GetValue<string>() == "solid"
        && solid["id"]?.GetValue<int>() == Blocks.Air;

    /// <summary>A stored style, or <paramref name="fallback"/> when the text is absent or cannot be read. A
    /// snapshot is a hand-editable leaf inside a map's layout, so a malformed one is a map that exports with
    /// the built-in shell rather than a map that refuses to export.</summary>
    public static HouseStyle DeserializeOr(string? json, HouseStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return Deserialize(json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }
}
