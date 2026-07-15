using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Configure;

// World · Scan sub-step (N01): a read-only look at the already-extracted world. The canvas is the reused
// edit-page EditorCanvas (its navigation toolbar + pan/zoom, and its island-base ↔ surface layer toggle),
// so nothing here re-extracts; the panels just summarise the cleaned-base detection. No intent is written —
// the World slice (confirmed symmetry) is authored in the later sub-steps.
public partial class WorldScanPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private int islandCount;
    private string? symType;
    private double symConfidence;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var isl = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Wizard.Slug}/islands");
            if (isl.ValueKind == JsonValueKind.Array) islandCount = isl.GetArrayLength();
        }
        catch { /* no scan data → leave the summary blank */ }
        try
        {
            var sym = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Wizard.Slug}/symmetry");
            if (sym.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                symType = p.TryGetProperty("type", out var t) ? t.GetString() : null;
                symConfidence = p.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
            }
        }
        catch { /* symmetry just stays blank */ }
    }
}
