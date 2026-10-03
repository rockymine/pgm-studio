using System.Net.Http.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Maps
{
    /// <summary>The stage a map stands at, or absent for every stage.</summary>
    [SupplyParameterFromQuery] public string? Stage { get; set; }

    /// <summary>Who made it: <see cref="People"/>, <see cref="Agents"/>, or absent for everyone.</summary>
    [SupplyParameterFromQuery(Name = "by")] public string? By { get; set; }

    /// <summary>The authors picked, by name; a map credited to any one of them is shown.</summary>
    [SupplyParameterFromQuery(Name = "author")] public string[]? Author { get; set; }

    /// <summary>A gamemode word, <see cref="NoMode"/>, or absent for any.</summary>
    [SupplyParameterFromQuery(Name = "mode")] public string? ModeQuery { get; set; }

    [SupplyParameterFromQuery(Name = "sort")] public string? Sort { get; set; }

    private const string People = "people", Agents = "agents", NoMode = "none";

    private static readonly (string Word, string Label)[] StageWords =
    [
        (MapStage.Plan, "Plan"), (MapStage.Sketch, "Sketch"), (MapStage.Configure, "Configure"), (MapStage.Edit, "Finished"),
    ];

    private static readonly IReadOnlyList<SelectOption> SortOptions =
    [
        new("changed", "Last changed"), new("name", "Name"), new("author", "Author"), new("stage", "Stage"),
    ];

    private List<MapSummary>? maps;
    private string search = "";
    private bool creatingSketch;
    private bool creatingPlan;
    private string? loadError;

    private string? CurrentStage => MapStage.IsValid(Stage) ? Stage : null;
    private string? Kind => By is People or Agents ? By : null;
    private string? Mode => string.IsNullOrEmpty(ModeQuery) ? null : ModeQuery;
    private string SortKey => SortOptions.Any(o => o.Value == Sort) ? Sort! : "changed";
    private IReadOnlyList<string> Picked => Author ?? [];

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

    /// <summary>The authoring layers a map holds, in pipeline order, each a direct link into that tool.</summary>
    private static IEnumerable<(string Id, string Label)> Layers(MapSummary map)
    {
        if (map.HasPlan) yield return (MapStage.Plan, "Plan");
        if (map.HasSketch) yield return (MapStage.Sketch, "Sketch");
        if (map.HasSurface) yield return (MapStage.Configure, "Configure");
    }

    /// <summary>The tool a row opens: the one at the map's stage, or for a finished map the last layer it
    /// holds, and nothing when it holds none.</summary>
    private static string? Opens(MapSummary map) =>
        map.Stage == MapStage.Edit ? Layers(map).Select(layer => layer.Id).LastOrDefault() : map.Stage;

    private void Open(MapSummary map)
    {
        if (Opens(map) is { } layer) Nav.NavigateTo($"maps/{map.Slug}/{layer}");
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

    /// <summary>Every author not yet picked, with how many maps credit them.</summary>
    private IReadOnlyList<SelectOption> AuthorOptions =>
        maps is null ? [] :
        [.. maps.SelectMany(map => Credited(map).Select(NameOf).Distinct())
            .GroupBy(name => name)
            .Where(group => !Picked.Contains(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SelectOption(group.Key, group.Key, Note: group.Count() == 1 ? "1 map" : $"{group.Count()} maps"))];

    /// <summary>The gamemodes any map is played for, in the order the studio names them.</summary>
    private IReadOnlyList<string> ModesPresent =>
        maps is null ? [] : [.. maps.SelectMany(map => map.Gamemodes).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>Whether a map passes every filter but <paramref name="skip"/>, with that one filter's value
    /// replaced by the one given — what a chip's count answers.</summary>
    private bool Passes(MapSummary map, string? skip = null, string? stage = null, string? by = null, string? mode = null)
    {
        var credits = Credited(map);
        var stageIs = skip == "stage" ? stage : CurrentStage;
        if (stageIs is not null && map.Stage != stageIs) return false;

        var kindIs = skip == "by" ? by : Kind;
        if (kindIs == People && !credits.Any(author => !IsAgent(author))) return false;
        if (kindIs == Agents && !credits.Any(IsAgent)) return false;

        if (Picked.Count > 0 && !credits.Any(author => Picked.Contains(NameOf(author)))) return false;

        var modeIs = skip == "mode" ? mode : Mode;
        if (modeIs == NoMode && map.Gamemodes.Count > 0) return false;
        if (modeIs is not null and not NoMode && !map.Gamemodes.Contains(modeIs)) return false;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = map.Name + " " + map.Slug + " " + string.Join(" ", credits.Select(NameOf));
            if (!text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private int Count(string skip, string? stage = null, string? by = null, string? mode = null) =>
        maps?.Count(map => Passes(map, skip, stage, by, mode)) ?? 0;

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
                "stage" => [.. shown.OrderBy(map => Array.IndexOf(MapStage.All, map.Stage)).ThenByDescending(map => map.UpdatedAt)],
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

    private void AddAuthor(string name)
    {
        if (string.IsNullOrEmpty(name) || Picked.Contains(name)) return;
        SetAuthors([.. Picked, name]);
    }

    private void RemoveAuthor(string name) => SetAuthors([.. Picked.Where(picked => picked != name)]);

    private void SetAuthors(string[] names) =>
        Nav.NavigateTo(Nav.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["author"] = names.Length == 0 ? null : names,
        }), replace: true);

    private void ClearFilters()
    {
        search = "";
        Nav.NavigateTo(Nav.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["stage"] = null, ["by"] = null, ["author"] = null, ["mode"] = null,
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
