using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Houses;

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
    /// ordinal nobody could edit by hand or diff usefully.
    /// <see cref="JsonSerializerOptions.AllowOutOfOrderMetadataProperties"/> reads a course material's
    /// <c>kind</c> wherever it falls in the object rather than only as the first key — a discriminator's
    /// position is a serialization detail, and key order carries no meaning in JSON, so any tool that
    /// reorders a style must not change what it says.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(HouseStyle style) => JsonSerializer.Serialize(style, Options);

    public static HouseStyle Deserialize(string json)
    {
        // Absent and empty are the same fault to an author, and both are the "will not parse" case
        // docs/refusals.md describes. JsonNode.Parse raises ArgumentNullException on a null string — the one
        // type a caller's catch does not name — so this escaped the gate above as a stack trace.
        if (string.IsNullOrWhiteSpace(json)) throw new JsonException("no house style JSON was posted");
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty house style JSON");
        Upgrade(node);
        RefuseStatedNulls(node, typeof(HouseStyle), "");
        return Read(node);
    }

    /// <summary>Read a style and say what of it went unread. The upgrade above removes every retired name it
    /// carries forward, so anything still unmatched afterwards is a name no version of this record ever had —
    /// a typo, or a field from a document this is not. Reported rather than refused: see
    /// <see cref="PgmStudio.Domain.DocumentShape"/>.</summary>
    public static HouseStyle Deserialize(string json, out IReadOnlyList<string> unread)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new JsonException("no house style JSON was posted");
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty house style JSON");
        Upgrade(node);
        RefuseStatedNulls(node, typeof(HouseStyle), "");
        var style = Read(node);
        unread = PgmStudio.Domain.DocumentShape.Unread(node, style);
        return style;
    }

    /// <summary>
    /// Refuse a part written as <c>null</c> where the record cannot hold one.
    ///
    /// <para>A style's parts split two ways with nothing in the document marking which: <c>porch</c> and
    /// <c>doorEdge</c> are nullable and a null is how they say "not this part", while <c>roof</c>,
    /// <c>windows</c>, <c>gableWindows</c>, <c>storeys</c> and a door's <c>head</c> are not — they carry an
    /// initializer a JSON null bypasses, leaving the record holding a null the stamper dereferences. So a
    /// document reads as though stating null were the way to drop any part, and for half of them it was an
    /// unhandled <see cref="NullReferenceException"/> — raised inside the validation gate itself, which reads
    /// <c>Roof.GableWindows</c> before it can name anything.</para>
    ///
    /// <para>Reading such a null as the default would be worse than the crash: an author writing
    /// <c>"gableWindows": null</c> means <i>none</i>, and would silently be given the default pair. So it is
    /// refused, by the field's own path, pointing at the form that says it safely. Nullable parts are
    /// untouched — serialization emits their nulls, and refusing those would break every round trip.</para>
    /// </summary>
    private static void RefuseStatedNulls(JsonNode node, Type type, string path)
    {
        if (node is not JsonObject obj) return;
        foreach (var (name, value) in obj)
        {
            var property = type.GetProperties().FirstOrDefault(
                p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (property is null) continue;
            var where = path.Length == 0 ? name : $"{path}.{name}";
            if (value is null)
            {
                if (Nullability.Create(property).ReadState is not NullabilityState.NotNull) continue;
                throw new PgmStudio.Domain.DocumentFault(where,
                    $"field '{where}' is stated as null, and this part of a style is always present — "
                    + "drop it from the document, or say it is not wanted in the part's own words "
                    + "(a window or a door head takes \"form\": \"none\")");
            }
            RefuseStatedNulls(value, property.PropertyType, where);
        }
    }

    private static readonly NullabilityInfoContext Nullability = new();

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

    /// <summary>Whether a stored material is a bare air block, which is how a blob written before footing
    /// was optional states that there is none.</summary>
    private static bool IsAir(JsonNode? material) =>
        material is JsonObject solid
        && solid["kind"]?.GetValue<string>() == "solid"
        && solid["id"]?.GetValue<int>() == Blocks.Air;

    /// <summary>A stored style, or <paramref name="fallback"/> when the text is absent or cannot be read. A
    /// snapshot is a hand-editable leaf inside a map's layout, so a malformed one is a map that exports with
    /// the built-in shell rather than a map that refuses to export.</summary>
    /// <summary>Deserialize, carrying a material's missing <c>kind</c> across as the refusal it is. A style's
    /// courses are terrain materials, so the same fault reaches this reader as reaches the theme's, and
    /// System.Text.Json reports it as <see cref="NotSupportedException"/> — the one type the endpoints' catch
    /// does not name, which sent it to the unhandled-fault middleware as the studio's own.</summary>
    private static HouseStyle Read(JsonNode node)
    {
        try { return JsonSerializer.Deserialize<HouseStyle>(node, Options)!; }
        catch (NotSupportedException ex) { throw new JsonException(TerrainThemeJson.MaterialKindFault, ex); }
    }

    public static HouseStyle DeserializeOr(string? json, HouseStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return Deserialize(json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }
}
