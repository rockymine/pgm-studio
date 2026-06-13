using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class ConfigureActivity
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public EventCallback OnComplete { get; set; }

    private static readonly (string id, string label, string title)[] Layers =
    [
        ("surface", "Surface", "Highest solid block per column"),
        ("y0", "Y=0", "Non-air blocks at world y=0"),
        ("bedrock", "Bedrock", "Lowest bedrock block per column"),
        ("base", "Base", "Lowest non-air block per column"),
    ];

    private int step = 1;
    private string scanLayer = "surface";
    private readonly HashSet<int> excluded = new();
    private readonly List<Island> islands = new();
    private string? error;

    private sealed record Island(int Id, int BlockCount);

    protected override async Task OnParametersSetAsync()
    {
        step = 1; excluded.Clear(); islands.Clear();
        try
        {
            var state = await Http.GetFromJsonAsync<JsonElement>($"api/configure/{Slug}/state");
            scanLayer = state.TryGetProperty("scan_layer", out var sl) && sl.ValueKind == JsonValueKind.String ? sl.GetString() ?? "surface" : "surface";
            if (state.TryGetProperty("exclude_islands", out var ex) && ex.ValueKind == JsonValueKind.Array)
                foreach (var i in ex.EnumerateArray()) excluded.Add(i.GetInt32());

            var resp = await Http.GetAsync($"api/map/{Slug}/islands");
            if (resp.IsSuccessStatusCode)
            {
                var arr = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (arr.ValueKind == JsonValueKind.Array)
                    foreach (var isl in arr.EnumerateArray())
                        islands.Add(new Island(isl.GetProperty("id").GetInt32(), isl.GetProperty("block_count").GetInt32()));
            }
        }
        catch (Exception ex) { error = ex.Message; }
    }

    private async Task ToggleIsland(int id, bool isExcluded)
    {
        if (await Patch("exclude-island", new Dictionary<string, object?> { ["island_id"] = id, ["excluded"] = isExcluded }))
        {
            if (isExcluded) excluded.Add(id); else excluded.Remove(id);
            StateHasChanged();
        }
    }

    private async Task Next()
    {
        if (step == 1)
            await Patch("scan-layer", new Dictionary<string, object?> { ["scan_layer"] = scanLayer, ["exclude_blocks"] = Array.Empty<int>() });
        if (step < 3) step++;
    }

    private void Prev() { if (step > 1) step--; }

    private async Task Finish()
    {
        await Patch("scan-layer", new Dictionary<string, object?> { ["confirmed"] = true });
        await OnComplete.InvokeAsync();
    }

    private async Task<bool> Patch(string path, object body)
    {
        error = null;
        var resp = await Http.PatchAsJsonAsync($"api/configure/{Slug}/{path}", body);
        if (resp.IsSuccessStatusCode) return true;
        error = $"error {(int)resp.StatusCode}";
        StateHasChanged();
        return false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
