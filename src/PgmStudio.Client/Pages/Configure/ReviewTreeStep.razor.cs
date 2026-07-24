using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.Configure;

// Review & Export · Region tree step (N07): the read-only inspect/debug view of the full generated
// region tree, between Pre-flight and XML. For intent maps the shaping steps drop the tree (structure is a
// generated artifact), so it surfaces here. Reuses the editor's RegionTree component; writes nothing.
public partial class ReviewTreeStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private List<RegionGroup>? groups;
    private bool loading = true;
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Wizard.Slug}/regions/tree");
            groups = doc.TryGetProperty("groups", out var g) ? RegionGroup.ParseGroups(g) : new();
        }
        catch (Exception ex) { error = ex.Message; groups = new(); }
        loading = false;
    }

    // The full generated region count — every node across the groups, including compound children + the
    // transform source (matches the doc's regions, the number shown by the read-only badge).
    private int RegionCount => groups?.Sum(g => g.Regions.Sum(CountNode)) ?? 0;
    private static int CountNode(RegionNode n) => 1 + n.Children.Sum(CountNode) + (n.Source is not null ? CountNode(n.Source) : 0);
}
