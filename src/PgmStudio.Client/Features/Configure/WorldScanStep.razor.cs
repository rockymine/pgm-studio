using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Configure;

// World · Scan step (N01): a read-only look at the already-extracted world. The canvas is the reused
// edit-page WorldCanvas (its navigation toolbar + pan/zoom, and its island-base ↔ surface layer toggle),
// so nothing here re-extracts; the panels just summarise the cleaned-base detection. No intent is written —
// the World slice (confirmed symmetry) is authored in the later steps.
public partial class WorldScanStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private int islandCount;
    private string? symType;
    private double symConfidence;

    protected override async Task OnInitializedAsync()
    {
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
}
