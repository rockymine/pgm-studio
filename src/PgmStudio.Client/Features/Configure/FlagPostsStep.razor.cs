using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Configure;

using F = FlagAuthoring;

// Flags · posts. The ingest scan read every banner the world holds (flag_candidate), so a flag's posts are mostly
// confirmed rather than placed: a banner clicked becomes a post, standing in the banner's own block and facing the
// way it faces. A post the scan has no banner for is placed with the point tool in the first air over the ground,
// which is where a banner would stand — and the flag's first post has to be a banner, because PGM loads a flag only
// where one stands at its starting post (OB36, asked at export).
public partial class FlagPostsStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<F.Flag> flags = new();
    private List<AuthoringContext.Team> teams = new();
    private List<FlagSuggestionDto> banners = new();
    private F.Defaults defaults = F.Defaults.Empty;
    private bool loading = true;
    private int selectedFlag = -1;
    private int selectedPost = -1;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private F.Flag? SelectedFlag => selectedFlag >= 0 && selectedFlag < flags.Count ? flags[selectedFlag] : null;
    private F.Post? SelectedPost => SelectedFlag is { } flag && selectedPost >= 0 && selectedPost < flag.Posts.Count
        ? flag.Posts[selectedPost] : null;

    private static readonly IReadOnlyList<SelectOption> ColorOptions =
        [.. GameColors.DyeColors.Select(color => new SelectOption(color.Value, color.Label))];

    private static readonly IReadOnlyList<SelectOption> CarrierOptions =
    [
        new(FlagCarriers.Lowered, "Fewer hearts", "Carrying the flag lowers the carrier's health to the hearts below."),
        new(FlagCarriers.Absorption, "A moment's shield", "Picking the flag up gives absorption III for three seconds."),
        new(FlagCarriers.Boost, "More hearts", "Carrying the flag raises the carrier's health to the hearts below."),
        new(FlagCarriers.None, "Nothing", "Carrying the flag does nothing to the carrier."),
    ];

    private IReadOnlyList<SelectOption> OwnerOptions =>
        [new(Nobody, "Nobody — King of the Flag"), .. AuthoringContext.TeamOptions(teams)];

    // The picker's word for a flag nobody owns: a Select option cannot carry the empty string.
    private const string Nobody = "nobody";

    private string TeamName(string id) => teams.FirstOrDefault(team => team.Id == id)?.Name is { Length: > 0 } name ? name : id;

    /// <summary>The colour a flag is drawn in: its own dye, or its team's colour.</summary>
    private string Hex(F.Flag flag) => flag.Team.Length > 0 && flag.Color.Length == 0
        ? GameColors.ChatHex(teams.FirstOrDefault(team => team.Id == flag.Team)?.Color)
        : GameColors.DyeHex(flag.Color);

    /// <summary>Banners no flag stands at yet — the confirm list.</summary>
    private IEnumerable<FlagSuggestionDto> Unused =>
        banners.Where(banner => !flags.Any(flag => flag.Posts.Any(post => F.Block(post) == (banner.X, banner.Y, banner.Z))));

    private bool OnBanner(F.Post post) => banners.Any(banner => (banner.X, banner.Y, banner.Z) == F.Block(post));

    private static string NameOf(F.Flag flag, int index) => flag.Name.Length > 0 ? flag.Name : $"Flag {index + 1}";
    private static string PostName(F.Post post, int index) => post.Name.Length > 0 ? post.Name : $"Post {index + 1}";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (await Http.GetFromJsonAsync<FlagSuggestionsDto>($"api/map/{Slug}/flag-suggestions") is { } suggestions)
            {
                var d = suggestions.Defaults;
                defaults = new F.Defaults(d.PointsRate, d.Carrier, d.CarrierHearts);
                banners = [.. suggestions.Banners];
            }
        }
        catch { banners = new(); }   // no scan, or no banners gathered
        teams = AuthoringContext.LoadTeams(Wizard.Intent);
        flags = F.ParseFlags(Wizard.Intent, defaults);
        if (flags.Count > 0) { selectedFlag = 0; selectedPost = flags[0].Posts.Count > 0 ? 0 : -1; }
        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private void OnCanvasSelect(string? id)
    {
        // "post-{flag}-{post}"
        if (id?.Split('-') is ["post", var f, var p] && int.TryParse(f, out var flag) && int.TryParse(p, out var post)
            && flag < flags.Count && post < flags[flag].Posts.Count)
        {
            selectedFlag = flag;
            selectedPost = post;
        }
    }

    private void SelectFlag(int index)
    {
        selectedFlag = index;
        selectedPost = flags[index].Posts.Count > 0 ? 0 : -1;
        _ = Paint();
    }

    private void SelectPost(int index) { selectedPost = index; _ = Paint(); }

    private F.Flag AddFlag()
    {
        var flag = new F.Flag
        {
            Name = flags.Count == 0 ? "Flag" : $"Flag {flags.Count + 1}",
            PointsRate = defaults.PointsRate, Carrier = defaults.Carrier, CarrierHearts = defaults.CarrierHearts,
        };
        flags.Add(flag);
        selectedFlag = flags.Count - 1;
        selectedPost = -1;
        Write();
        _ = Paint();
        return flag;
    }

    // A banner becomes a post standing in its block, facing the way it faces. The first post a flag takes from a
    // banner gives the flag the banner's colour, since that is the colour PGM copies into the flag players carry.
    private void AddBanner(FlagSuggestionDto banner)
    {
        var flag = SelectedFlag ?? AddFlag();
        if (flag.Posts.Count == 0 && flag.Team.Length == 0) flag.Color = banner.Color.Replace('_', ' ');
        flag.Posts.Add(new F.Post { X = banner.X + 0.5, Y = banner.Y, Z = banner.Z + 0.5, Yaw = banner.Yaw });
        selectedPost = flag.Posts.Count - 1;
        Write();
        _ = Paint();
    }

    // A placed post stands in the first air over the ground of the clicked column, where its banner would.
    private async Task OnPointPick((double X, double Z) at)
    {
        var flag = SelectedFlag ?? AddFlag();
        if (await ColumnFloor.RestingYAsync(Http, Slug, at.X, at.Z) is not { } y) return;
        flag.Posts.Add(new F.Post { X = Math.Floor(at.X) + 0.5, Y = y, Z = Math.Floor(at.Z) + 0.5 });
        selectedPost = flag.Posts.Count - 1;
        Write();
        await Paint();
    }

    private void SetName(F.Flag flag, string name) { flag.Name = name.Trim(); Write(); }
    private void SetColor(F.Flag flag, string color) { flag.Color = color; Write(); _ = Paint(); }
    private void SetRate(F.Flag flag, double rate) { flag.PointsRate = rate; Write(); }
    private void SetCarrier(F.Flag flag, string carrier) { flag.Carrier = carrier; Write(); }

    // A team's flag takes its team's colour and asks nothing of the carrier unless the author says so; a flag
    // given back to nobody is white and lowers its carrier, as a new one does.
    private void SetOwner(F.Flag flag, string owner)
    {
        flag.Team = owner == Nobody ? "" : owner;
        flag.Color = flag.Team.Length > 0 ? "" : "white";
        flag.Carrier = flag.Team.Length > 0 ? FlagCarriers.None : defaults.Carrier;
        Write();
        _ = Paint();
    }
    private void SetHearts(F.Flag flag, double hearts) { flag.CarrierHearts = (int)hearts; Write(); }
    private void SetPostName(F.Post post, string name) { post.Name = name.Trim(); Write(); }

    private void MakeStart(F.Flag flag, F.Post post)
    {
        flag.Posts.Remove(post);
        flag.Posts.Insert(0, post);
        selectedPost = 0;
        Write();
        _ = Paint();
    }

    private void RemovePost(F.Flag flag, F.Post post)
    {
        flag.Posts.Remove(post);
        selectedPost = Math.Min(selectedPost, flag.Posts.Count - 1);
        Write();
        _ = Paint();
    }

    private void RemoveFlag(F.Flag flag)
    {
        flags.Remove(flag);
        selectedFlag = Math.Min(selectedFlag, flags.Count - 1);
        selectedPost = SelectedFlag is { Posts.Count: > 0 } ? 0 : -1;
        Write();
        _ = Paint();
    }

    private void Write() { F.WriteFlags(Wizard.Intent, flags); Wizard.MarkDirty(); }

    // Posts draw as markers in their flag's colour, the selected flag's brighter; a banner no flag stands at yet
    // draws grey beside them, so confirming one is recognising the marker already on the map.
    private async Task Paint()
    {
        if (canvas is null) return;
        var shapes = new List<object>();
        for (var f = 0; f < flags.Count; f++)
            for (var p = 0; p < flags[f].Posts.Count; p++)
            {
                var post = flags[f].Posts[p];
                var (x, _, z) = F.Block(post);
                shapes.Add(Marker($"post-{f}-{p}", x, z, Hex(flags[f]), f == selectedFlag,
                                  $"{NameOf(flags[f], f)} · {PostName(post, p)}{(p == 0 ? " (start)" : "")}"));
            }
        var n = 0;
        foreach (var banner in Unused)
            shapes.Add(Marker($"banner-{n++}", banner.X, banner.Z, "#8a8f98", false, $"Banner · {banner.Color}"));
        await canvas.SetAuthorRegionsAsync(shapes);
    }

    private static object Marker(string id, int x, int z, string color, bool primary, string label) => new
    {
        id,
        type = "point",
        marker = true,
        primary,
        color,
        label,
        bounds = new { min_x = (double)x, min_z = (double)z, max_x = x + 1.0, max_z = z + 1.0 },
    };
}
