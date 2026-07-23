using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Configure;

// World · Islands step (N01): review the detected islands and exclude the stray ones (decor / observer
// towers). Islands are selectable from the list or by clicking the canvas (the reused EditorCanvas in
// island-select mode over the island base layer); the selected island gets an accent border and
// its centre / block count / exclude toggle show in the inspector. Excluding re-runs symmetry server-side
// (PATCH /configure/{slug}/exclude-island) — never a re-scan.
public partial class WorldIslandsPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Convert any new <i data-lucide> placeholders to SVG after each render. This component re-renders on
    // its own (the parent wizard doesn't), so its list-row icons would otherwise only appear once some
    // other render — e.g. a canvas fit — happened to re-run the icon factory globally.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private sealed record Island(int Id, int BlockCount, int MinX, int MinZ, int MaxX, int MaxZ)
    {
        public double CentreX => (MinX + MaxX + 1) / 2.0;
        public double CentreZ => (MinZ + MaxZ + 1) / 2.0;
    }

    private List<Island> islands = new();
    private readonly HashSet<int> excluded = new();
    private int? selectedId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private List<Island> Included => islands.Where(i => !excluded.Contains(i.Id)).ToList();
    private List<Island> ExcludedList => islands.Where(i => excluded.Contains(i.Id)).ToList();
    private Island? Selected => selectedId is { } id ? islands.FirstOrDefault(i => i.Id == id) : null;
    private bool IsExcluded(Island i) => excluded.Contains(i.Id);

    protected override async Task OnInitializedAsync()
    {
        await LoadIslands();
        await LoadExcluded();
    }

    private async Task LoadIslands()
    {
        try
        {
            var arr = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/islands");
            if (arr.ValueKind == JsonValueKind.Array)
                islands = arr.EnumerateArray().Select(e =>
                {
                    var b = e.GetProperty("bounds");
                    return new Island(
                        e.GetProperty("id").GetInt32(), e.GetProperty("block_count").GetInt32(),
                        b[0].GetInt32(), b[1].GetInt32(), b[2].GetInt32(), b[3].GetInt32());
                }).OrderByDescending(i => i.BlockCount).ToList();
        }
        catch { islands = new(); }
    }

    private async Task LoadExcluded()
    {
        try
        {
            var s = await Http.GetFromJsonAsync<JsonElement>($"api/configure/{Slug}/state");
            excluded.Clear();
            if (s.TryGetProperty("exclude_islands", out var ex) && ex.ValueKind == JsonValueKind.Array)
                foreach (var i in ex.EnumerateArray())
                    if (i.ValueKind == JsonValueKind.Number) excluded.Add(i.GetInt32());
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

    private async Task ToggleExclude(Island isl)
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
    private string Label(Island isl) => $"Island {islands.IndexOf(isl) + 1}";
}
