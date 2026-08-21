using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Configure;

// World · Islands step (N01): review the detected islands and exclude the stray ones (decor / observer
// towers). Islands are selectable from the list or by clicking the canvas (the reused WorldCanvas in
// island-select mode over the island base layer); the selected island gets an accent border and
// its centre / block count / exclude toggle show in the inspector. Excluding re-runs symmetry server-side
// (PATCH /configure/{slug}/exclude-island) — never a re-scan.
public partial class WorldIslandsStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Convert any new <i data-lucide> placeholders to SVG after each render. This component re-renders on
    // its own (the parent wizard doesn't), so its list-row icons would otherwise only appear once some
    // other render — e.g. a canvas fit — happened to re-run the icon factory globally.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private List<IslandDto> islands = new();
    private readonly HashSet<int> excluded = new();
    private int? selectedId;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private List<IslandDto> Included => islands.Where(i => !excluded.Contains(i.Id)).ToList();
    private List<IslandDto> ExcludedList => islands.Where(i => excluded.Contains(i.Id)).ToList();
    private IslandDto? Selected => selectedId is { } id ? islands.FirstOrDefault(i => i.Id == id) : null;
    private bool IsExcluded(IslandDto i) => excluded.Contains(i.Id);

    protected override async Task OnInitializedAsync()
    {
        await LoadIslands();
        await LoadExcluded();
    }

    private async Task LoadIslands()
    {
        try
        {
            islands = (await AuthoringContext.LoadIslandsAsync(Http, Slug))
                .OrderByDescending(i => i.BlockCount).ToList();
        }
        catch { islands = new(); }
    }

    private async Task LoadExcluded()
    {
        try
        {
            var state = await Http.GetFromJsonAsync<ConfigureStateDto>($"api/configure/{Slug}/state");
            excluded.Clear();
            foreach (var island in state?.ExcludeIslands ?? []) excluded.Add(island);
        }
        catch { /* no config yet → nothing excluded */ }
    }

    // The canvas renders its islands on mount; apply the loaded exclusions once it's ready.
    private async Task OnCanvasReady()
    {
        if (canvas is not null) await canvas.SetExcludedIslandsAsync(excluded.ToList());
    }

    private async Task Select(int? id)
    {
        selectedId = id;
        if (canvas is not null) await canvas.SetSelectedIslandAsync(id);
    }

    private async Task ToggleExclude(IslandDto isl)
    {
        var willExclude = !excluded.Contains(isl.Id);
        // Saves immediately (re-runs symmetry server-side) — reflected in the topbar as Saving… → Saved.
        var ok = await Wizard.TrackInstantSaveAsync(async () =>
        {
            var resp = await Http.PatchAsJsonAsync($"api/configure/{Slug}/exclude-island",
                new Dictionary<string, object?> { ["island_id"] = isl.Id, ["excluded"] = willExclude });
            resp.EnsureSuccessStatusCode();
        });
        if (!ok) return;
        if (willExclude) excluded.Add(isl.Id); else excluded.Remove(isl.Id);
        if (canvas is not null) await canvas.SetExcludedIslandsAsync(excluded.ToList());
    }

    // Display label: a plain positional identifier for the list.
    private string Label(IslandDto isl) => $"Island {islands.IndexOf(isl) + 1}";
}
