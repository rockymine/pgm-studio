using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Models;

/// <summary>A map's authoring layers as the client opens them: the two in pipeline order, which a map holds,
/// and the tool a click on the map opens.</summary>
public static class MapLayers
{
    /// <summary>The two authoring layers in pipeline order, each with the word a page shows it under.</summary>
    public static readonly (string Id, string Label)[] All = [(MapStage.Sketch, "Sketch"), (MapStage.Configure, "Configure")];

    /// <summary>Whether <paramref name="map"/> holds the layer <paramref name="layer"/>.</summary>
    public static bool Holds(MapSummary map, string layer) => layer switch
    {
        MapStage.Sketch => map.HasSketch,
        _ => map.HasSurface,
    };

    /// <summary>What a map that does not hold <paramref name="layer"/> is told about it: that it is not there yet,
    /// and for Configure what makes it — the world, which the Sketch tool's finish builds.</summary>
    public static string Missing(string layer) => layer switch
    {
        MapStage.Configure => "No world yet: Build & open Configure, on Sketch's last step, builds it",
        _ => $"No {LabelOf(layer).ToLowerInvariant()} yet",
    };

    /// <summary>Whether <paramref name="map"/> was imported from a world rather than drawn in the studio: it holds a
    /// world and no sketch layout, the same test <c>GET /api/map/{slug}/origin</c> answers.</summary>
    public static bool Imported(MapSummary map) => map.HasSurface && !map.HasSketch;

    /// <summary>The tool a click on <paramref name="map"/> opens: the one at its stage, or for a finished map the
    /// last layer it holds, and nothing when it holds none.</summary>
    public static string? Opens(MapSummary map) =>
        map.Stage == MapStage.Edit ? All.Where(layer => Holds(map, layer.Id)).Select(layer => layer.Id).LastOrDefault()
            : map.Stage;

    /// <summary>The stage a map is filtered and labelled by: its own, with <see cref="MapStage.Edit"/> read as
    /// <see cref="MapStage.Configure"/>.</summary>
    public static string StageOf(MapSummary map) => map.Stage == MapStage.Edit ? MapStage.Configure : map.Stage;

    /// <summary>The word a page shows <paramref name="stage"/> under.</summary>
    public static string LabelOf(string stage) => All.FirstOrDefault(layer => layer.Id == stage).Label ?? stage;

    /// <summary>Who a map is credited to as an author, contributors left out.</summary>
    public static IReadOnlyList<MapAuthorDto> Credited(MapSummary map) =>
        [.. map.Authors.Where(author => author.Role != "contributor")];

    /// <summary>The name an author is shown under: their own, else the start of their account id.</summary>
    public static string NameOf(MapAuthorDto author) =>
        author.Name is { Length: > 0 } name ? name : author.Uuid[..Math.Min(8, author.Uuid.Length)];
}
