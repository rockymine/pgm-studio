using System.Text.Json;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// Everything the Theme phase shows of the board's finish, as one value the bridge announces on every change
/// of it — an edit, a load and a step taken back alike — so the strip and the inspector cannot disagree about
/// the registry.
/// </summary>
/// <param name="Ids">The board's theme ids, in registry order.</param>
/// <param name="Documents">Each theme's material document as JSON text, by theme id.</param>
/// <param name="MapTheme">The theme that paints ground no shape covers, or empty.</param>
/// <param name="ShapeThemes">Every shape that carries a theme, by shape id.</param>
/// <param name="Sources">Which library row each board theme was copied from, by theme id.</param>
/// <param name="ShapeCount">How many shapes the whole board carries over every layer — the denominator the
/// themed count is read against, counted where that count is.</param>
/// <param name="RoomStyles">The shell snapshot bound to each room kind, as JSON text. A kind bound to no
/// building at all is in <see cref="OpenRooms"/> instead, and one in neither uses the built-in shell.</param>
/// <param name="OpenRooms">The room kinds bound to no building: a pad on open ground with nothing over it.</param>
/// <param name="BiomeSource">Which library row the board's biome was copied from, or 0 for a board that states
/// none and for one whose field came from outside the library.</param>
/// <param name="BiomeJson">The board's biome field as JSON text, or empty for a board that states none.</param>
public sealed record SketchThemes(
    IReadOnlyList<string> Ids,
    IReadOnlyDictionary<string, string> Documents,
    string MapTheme,
    IReadOnlyDictionary<string, string> ShapeThemes,
    IReadOnlyDictionary<string, long> Sources,
    int ShapeCount,
    IReadOnlyDictionary<string, string> RoomStyles,
    IReadOnlySet<string> OpenRooms,
    long BiomeSource,
    string BiomeJson)
{
    /// <summary>A board with nothing stated, which is what the host holds until the bridge announces one.</summary>
    public static readonly SketchThemes Empty = new(
        [], new Dictionary<string, string>(), "", new Dictionary<string, string>(), new Dictionary<string, long>(), 0,
        new Dictionary<string, string>(), new HashSet<string>(), 0, "");

    public static SketchThemes Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ids = new List<string>();
        var documents = new Dictionary<string, string>();
        if (Object(root, "themes") is { } themes)
            foreach (var theme in themes.EnumerateObject())
            {
                ids.Add(theme.Name);
                documents[theme.Name] = theme.Value.GetRawText();
            }

        var shapeThemes = new Dictionary<string, string>();
        if (Object(root, "shapeThemes") is { } assigned)
            foreach (var shape in assigned.EnumerateObject())
                if (shape.Value.ValueKind == JsonValueKind.String) shapeThemes[shape.Name] = shape.Value.GetString() ?? "";

        var sources = new Dictionary<string, long>();
        if (Object(root, "themeSources") is { } copied)
            foreach (var source in copied.EnumerateObject())
                if (source.Value.ValueKind == JsonValueKind.Number) sources[source.Name] = source.Value.GetInt64();

        // A kind that is absent has never been asked, and one holding a JSON null was asked for no building at
        // all: the two bind differently, so the object is read for presence rather than for a value.
        var rooms = new Dictionary<string, string>();
        var open = new HashSet<string>();
        if (Object(root, "roomStyles") is { } styles)
            foreach (var style in styles.EnumerateObject())
                if (style.Value.ValueKind == JsonValueKind.Null) open.Add(style.Name);
                else rooms[style.Name] = style.Value.GetRawText();

        long biomeSource = 0;
        var biomeJson = "";
        if (Object(root, "biome") is { } biome)
        {
            if (biome.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Number)
                biomeSource = source.GetInt64();
            if (Object(biome, "field") is { } field) biomeJson = field.GetRawText();
        }

        return new SketchThemes(
            ids,
            documents,
            root.TryGetProperty("mapTheme", out var mapTheme) && mapTheme.ValueKind == JsonValueKind.String
                ? mapTheme.GetString() ?? "" : "",
            shapeThemes,
            sources,
            root.TryGetProperty("shapeCount", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 0,
            rooms,
            open,
            biomeSource,
            biomeJson);
    }

    private static JsonElement? Object(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.Object ? child : null;
}
