using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Index
{
    private const int RecentMaps = 3;

    /// <summary>The tools a map is not needed to reach, in the order the studio's bar lists them.</summary>
    private static readonly (string Name, string Text, string Icon, string Href)[] Tools =
    [
        ("Generator", "Generate whole layouts from players, size and symmetry.", "sparkles", "generator"),
        ("Shape catalog", "Every shape the generator can place, and the settings that draw it.", "shapes", "catalog"),
        ("Library", "Materials, themes and buildings shared by every map.", "library", "library"),
        ("Rules", "Every refusal the studio can raise, and what each one asks of the map.", "scale", "rules"),
    ];

    /// <summary>The maps offered on the page, newest first, each with the tool it opens in and the moment shown.</summary>
    private List<(MapSummary Map, string Opens, DateTime At)> recent = [];

    /// <summary>The newest map in the studio that has a world to draw, which the hero picture shows.</summary>
    private MapSummary? hero;

    /// <summary>Whether <paramref name="map"/> has been built — a sketch layout and the world compiled from it —
    /// which is what puts ground under its picture; anything less shows the placeholder rather than asking.</summary>
    private static bool Drawable(MapSummary map) => map.HasSketch && map.HasSurface;

    /// <summary>Whether <see cref="recent"/> holds maps the caller changed themselves rather than the latest overall.</summary>
    private bool mine;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var maps = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps") ?? [];
            hero = maps.Where(Drawable).OrderByDescending(map => map.UpdatedAt).FirstOrDefault();
            mine = maps.Any(map => map.YouWroteAt is not null);
            recent = maps
                .Where(map => !mine || map.YouWroteAt is not null)
                .Select(map => (Map: map, Opens: MapLayers.Opens(map), At: map.YouWroteAt ?? map.UpdatedAt))
                .Where(entry => entry.Opens is not null)
                .OrderByDescending(entry => entry.At)
                .Take(RecentMaps)
                .Select(entry => (entry.Map, entry.Opens!, entry.At))
                .ToList();
        }
        catch { /* the list is an offer — without it the page still opens every tool */ }
    }

    /// <summary>Who the map is by and when it last changed, as the card's one muted line.</summary>
    private static string Meta(MapSummary map, DateTime at) =>
        MapLayers.Credited(map) is { Count: > 0 } credits
            ? $"{MapLayers.NameOf(credits[0])} · edited {Moments.Ago(at)}"
            : $"edited {Moments.Ago(at)}";

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
