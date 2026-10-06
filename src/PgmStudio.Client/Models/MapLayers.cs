using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Models;

/// <summary>A map's authoring layers as the client opens them: the three in pipeline order, which a map holds,
/// and the tool a click on the map opens.</summary>
public static class MapLayers
{
    /// <summary>The three authoring layers in pipeline order, each with the word a page shows it under.</summary>
    public static readonly (string Id, string Label)[] All =
        [(MapStage.Plan, "Plan"), (MapStage.Sketch, "Sketch"), (MapStage.Configure, "Configure")];

    /// <summary>Whether <paramref name="map"/> holds the layer <paramref name="layer"/>.</summary>
    public static bool Holds(MapSummary map, string layer) => layer switch
    {
        MapStage.Plan => map.HasPlan,
        MapStage.Sketch => map.HasSketch,
        _ => map.HasSurface,
    };

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
