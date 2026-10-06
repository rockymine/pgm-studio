using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Configure;

// World · Scan step: a look at the already-extracted world. The canvas is the reused edit-page WorldCanvas
// (its navigation toolbar + pan/zoom, and its island-base ↔ surface layer toggle), and the panels summarise
// the cleaned-base detection. No intent is written — the World slice (confirmed symmetry) is authored in the
// later steps. A map imported from a world can take a new download of it here (POST …/import-url), which
// replaces the scan and leaves the intent alone.
public partial class WorldScanStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private int islandCount;
    private string? symType;
    private double symConfidence;

    private bool imported;
    private string urlInput = "";
    private bool reimporting;
    private bool reimported;
    private string? reimportError;
    private int worldVersion;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var origin = await Http.GetFromJsonAsync<MapOriginDto>($"api/map/{Wizard.Slug}/origin");
            imported = origin is { Sketch: false };
        }
        catch { /* no origin → no re-import offered */ }
        await LoadSummaryAsync();
    }

    private async Task LoadSummaryAsync()
    {
        symType = null; symConfidence = 0;
        try
        {
            islandCount = (await AuthoringContext.LoadIslandsAsync(Http, Wizard.Slug)).Count;
        }
        catch { /* no scan data → leave the summary blank */ }
        try
        {
            var sym = await Http.GetFromJsonAsync<SymmetryDto>($"api/map/{Wizard.Slug}/symmetry");
            if (sym?.Primary is { } primary) { symType = primary.Type; symConfidence = primary.Confidence; }
        }
        catch { /* symmetry just stays blank */ }
    }

    /// <summary>Fetch the world again from the link and read it over this map's scan, then redraw the canvas
    /// and the summary from what was read. A refusal is shown in the step's own words.</summary>
    private async Task Reimport()
    {
        var url = urlInput.Trim();
        if (url.Length == 0 || reimporting) return;
        reimporting = true; reimported = false; reimportError = null; StateHasChanged();
        try
        {
            var response = await Http.PostAsJsonAsync($"api/map/{Wizard.Slug}/import-url",
                new Dictionary<string, object?> { ["url"] = url });
            if (!response.IsSuccessStatusCode)
            {
                var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>();
                reimportError = refusal?.Message is { Length: > 0 } said
                    ? char.ToUpperInvariant(said[0]) + said[1..] + "."
                    : $"Couldn't import the world. (HTTP {(int)response.StatusCode})";
                return;
            }
            await LoadSummaryAsync();
            worldVersion++;
            reimported = true;
        }
        catch { reimportError = "Couldn't import the world. Check the link and try again."; }
        finally { reimporting = false; StateHasChanged(); }
    }
}
