using System.Net.Http.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Maps
{
    /// <summary>The stage a map stands at, or absent for every stage.</summary>
    [SupplyParameterFromQuery] public string? Stage { get; set; }

    /// <summary>The authors picked, by name; a map credited to any one of them is shown.</summary>
    [SupplyParameterFromQuery(Name = "author")] public string[]? Author { get; set; }

    /// <summary>The gamemodes picked, each a gamemode word or <see cref="NoGamemode"/>; a map played for any
    /// one of them is shown.</summary>
    [SupplyParameterFromQuery(Name = "gamemode")] public string[]? Gamemode { get; set; }

    [SupplyParameterFromQuery(Name = "sort")] public string? Sort { get; set; }

    private const string NoGamemode = "none";

    /// <summary>The stages a map is filtered by. Configure is the last: a map whose stage is
    /// <see cref="MapStage.Edit"/>, which only the corpus import sets, is listed under it, the tool its row
    /// opens.</summary>
    private static readonly (string Word, string Label)[] StageWords =
    [
        (MapStage.Plan, "Plan"), (MapStage.Sketch, "Sketch"), (MapStage.Configure, "Configure"),
    ];

    private static readonly (string Title, bool Agents)[] AuthorGroups = [("Agents", true), ("People", false)];

    private static readonly IReadOnlyList<SelectOption> SortOptions =
    [
        new("changed", "Last changed"), new("name", "Name"), new("author", "Author"), new("stage", "Stage"),
    ];

    private List<MapSummary>? maps;
    private string search = "";
    private string authorSearch = "";
    private bool creatingSketch;
    private bool creatingPlan;
    private string? loadError;

    private string? CurrentStage => StageWords.Any(entry => entry.Word == Stage) ? Stage : null;
    private string SortKey => SortOptions.Any(o => o.Value == Sort) ? Sort! : "changed";
    private IReadOnlyList<string> Picked => Author ?? [];
    private IReadOnlyList<string> PickedGamemodes => Gamemode ?? [];
    private bool Filtering => CurrentStage is not null || Picked.Count > 0 || PickedGamemodes.Count > 0;

    /// <summary>The stage a map is filtered and labelled by: its own, with <see cref="MapStage.Edit"/> read as
    /// <see cref="MapStage.Configure"/>.</summary>
    private static string StageOf(MapSummary map) => map.Stage == MapStage.Edit ? MapStage.Configure : map.Stage;

    private async Task NewPlan()
    {
        if (creatingPlan) return;
        creatingPlan = true;
        try
        {
            var resp = await Http.PostAsJsonAsync("api/plan", new { name = "Untitled plan" });
            if (resp.IsSuccessStatusCode
                && await resp.Content.ReadFromJsonAsync<OriginatedDto>() is { Slug: { Length: > 0 } slug })
            {
                Nav.NavigateTo($"maps/{slug}/plan?phase=info");
                return;
            }
        }
        catch (HttpRequestException) { /* the button re-enables so the user can retry */ }
        creatingPlan = false;
    }

    private async Task NewSketch()
    {
        if (creatingSketch) return;
        creatingSketch = true;
        try
        {
            var resp = await Http.PostAsJsonAsync("api/sketch", new { name = "Untitled sketch" });
            if (resp.IsSuccessStatusCode
                && await resp.Content.ReadFromJsonAsync<OriginatedDto>() is { Slug: { Length: > 0 } slug })
            {
                Nav.NavigateTo($"maps/{slug}/sketch?phase=info");
                return;
            }
        }
        catch (HttpRequestException) { /* the button re-enables so the user can retry */ }
        creatingSketch = false;
    }


    private void Open(MapSummary map)
    {
        if (MapLayers.Opens(map) is { } layer) Nav.NavigateTo($"maps/{map.Slug}/{layer}");
    }

    private static string LayerTitle(string layer) => layer switch
    {
        MapStage.Plan => "Open this map's plan.",
        MapStage.Sketch => "Open this map's sketch.",
        _ => "Open this map's world in Configure.",
    };

    private static string StageLabel(string stage) =>
        StageWords.FirstOrDefault(entry => entry.Word == stage).Label ?? stage;

    /// <summary>Who a map is credited to as an author, contributors left out.</summary>
    private static IReadOnlyList<MapAuthorDto> Credited(MapSummary map) =>
        [.. map.Authors.Where(author => author.Role != "contributor")];

    private static string NameOf(MapAuthorDto author) =>
        author.Name is { Length: > 0 } name ? name : author.Uuid[..Math.Min(8, author.Uuid.Length)];

    /// <summary>A credit with no account behind it, which is how an agent is credited.</summary>
    private static bool IsAgent(MapAuthorDto author) => string.IsNullOrEmpty(author.Uuid);

    private MapAuthorDto? CreditNamed(string name) =>
        maps?.SelectMany(Credited).FirstOrDefault(author => NameOf(author) == name);

    /// <summary>Every credited author of one kind whose name matches the author search, in name order.</summary>
    private IReadOnlyList<string> AuthorsIn(bool agents) =>
        maps is null ? [] :
        [.. maps.SelectMany(Credited)
            .Where(author => IsAgent(author) == agents)
            .Select(NameOf)
            .Where(name => name.Contains(authorSearch.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The gamemodes any map is played for, in the order the studio names them.</summary>
    private IReadOnlyList<string> GamemodesPresent =>
        maps is null ? [] : [.. maps.SelectMany(map => map.Gamemodes).Distinct().Order(StringComparer.Ordinal)];

    private static string GamemodeLabel(string gamemode) => gamemode == NoGamemode ? "Not set" : gamemode.ToUpperInvariant();

    /// <summary>Whether a map passes every filter, or — for a count — every filter but <paramref name="skip"/>,
    /// with the one value given in its place.</summary>
    private bool Passes(MapSummary map, string? skip = null, string? stage = null, string? author = null, string? gamemode = null)
    {
        var credits = Credited(map);
        var stageIs = skip == "stage" ? stage : CurrentStage;
        if (stageIs is not null && StageOf(map) != stageIs) return false;

        IReadOnlyList<string> authors = skip == "author" ? author is null ? [] : [author] : Picked;
        if (authors.Count > 0 && !credits.Any(credit => authors.Contains(NameOf(credit)))) return false;

        IReadOnlyList<string> gamemodes = skip == "gamemode" ? gamemode is null ? [] : [gamemode] : PickedGamemodes;
        if (gamemodes.Count > 0 && !gamemodes.Any(word => word == NoGamemode ? map.Gamemodes.Count == 0 : map.Gamemodes.Contains(word)))
            return false;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = map.Name + " " + map.Slug + " " + string.Join(" ", credits.Select(NameOf));
            if (!text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private int Count(string skip, string? stage = null, string? author = null, string? gamemode = null) =>
        maps?.Count(map => Passes(map, skip, stage, author, gamemode)) ?? 0;

    private List<MapSummary> Shown
    {
        get
        {
            var shown = (maps ?? []).Where(map => Passes(map));
            return SortKey switch
            {
                "name" => [.. shown.OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)],
                "author" => [.. shown.OrderBy(map => Credited(map).Select(NameOf).FirstOrDefault() ?? "￿", StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(map => map.Name, StringComparer.OrdinalIgnoreCase)],
                "stage" => [.. shown.OrderBy(map => Array.IndexOf(MapStage.All, StageOf(map))).ThenByDescending(map => map.UpdatedAt)],
                _ => [.. shown.OrderByDescending(map => map.UpdatedAt)],
            };
        }
    }

    private string CountLabel =>
        maps is null ? "…" : Shown.Count == maps.Count ? $"{maps.Count}" : $"{Shown.Count} of {maps.Count}";

    /// <summary>Change one filter in the address, which the page reads every filter from.</summary>
    private void Go(string key, string? value) =>
        Nav.NavigateTo(Nav.GetUriWithQueryParameter(key, value), replace: true);

    private void SetSort(string key) => Go("sort", key == "changed" ? null : key);

    private void ToggleAuthor(string name) =>
        SetList("author", Picked.Contains(name) ? [.. Picked.Where(picked => picked != name)] : [.. Picked, name]);

    /// <summary>Pick, or drop, every author of a group at once.</summary>
    private void PickAll(IReadOnlyList<string> group, bool on) =>
        SetList("author", on ? [.. Picked.Union(group)] : [.. Picked.Except(group)]);

    private void ToggleGamemode(string gamemode) =>
        SetList("gamemode", PickedGamemodes.Contains(gamemode)
            ? [.. PickedGamemodes.Where(picked => picked != gamemode)]
            : [.. PickedGamemodes, gamemode]);

    /// <summary>Write a filter that holds several values to the address; an empty one leaves it.</summary>
    private void SetList(string key, string[] values) =>
        Nav.NavigateTo(Nav.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [key] = values.Length == 0 ? null : values,
        }), replace: true);

    private void ClearFilters()
    {
        search = "";
        authorSearch = "";
        Nav.NavigateTo(Nav.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["stage"] = null, ["author"] = null, ["gamemode"] = null,
        }), replace: true);
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        maps = null;
        loadError = null;
        try { maps = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps"); }
        catch (HttpRequestException) { loadError = "Couldn't load the maps. The studio may be restarting; try again in a moment."; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
