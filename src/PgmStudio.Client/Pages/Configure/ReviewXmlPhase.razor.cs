using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

// Review & Export · XML sub-step (N06): the final sub-step. Shows the generated PGM map.xml, segmented into
// containers (Full document + teams/spawns/wools/filters/regions/apply rules) picked on the left. The
// flow-bar Next becomes Export (download), enabled only when the pre-flight export gate is open — GET /xml
// returns 409 for an un-traversable intent map, which blocks both the preview and the download. Writes nothing.
public partial class ReviewXmlPhase : IDisposable
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool loading = true;
    private string? xml;          // the full generated document (null when blocked / errored)
    private string? blocked;      // the 409 traversability message (export gate closed)
    private string? error;        // any other failure (e.g. a 500 codec error)
    private string? downloadError; // a failure of the Export download itself (preview stays visible)
    private List<Container> containers = new();
    private string selected = "full";

    private sealed record Container(string Key, string Label, string Icon, string Xml, int Count);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resp = await Http.GetAsync($"api/map/{Wizard.Slug}/xml");
            if (resp.IsSuccessStatusCode)
            {
                xml = await resp.Content.ReadAsStringAsync();
                containers = BuildContainers(xml);
                Wizard.RegisterExport(true, DownloadAsync);
            }
            else if ((int)resp.StatusCode == 409)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                blocked = doc.TryGetProperty("message", out var m) ? m.GetString() : "the spawn↔wool chain isn't connected";
                Wizard.RegisterExport(false, null);
            }
            else
            {
                error = $"export failed (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
                Wizard.RegisterExport(false, null);
            }
        }
        catch (Exception ex) { error = ex.Message; Wizard.RegisterExport(false, null); }
        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // The flow-bar Export action — fetch the server export (a {slug}/ ZIP with map.xml + level.dat +
    // region/ for sketch maps, or plain map.xml otherwise) and save it. Fetching (rather than a blind
    // anchor click) lets a non-2xx response surface as an in-app error instead of writing the JSON error
    // body to disk as a bogus "export" file.
    private async Task DownloadAsync()
    {
        downloadError = null;
        HttpResponseMessage resp;
        try { resp = await Http.GetAsync($"api/map/{Wizard.Slug}/export"); }
        catch (Exception ex) { downloadError = ex.Message; StateHasChanged(); return; }

        if (!resp.IsSuccessStatusCode)
        {
            downloadError = $"export failed (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
            StateHasChanged();
            return;
        }

        var filename = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? (resp.Content.Headers.ContentType?.MediaType == "application/zip" ? $"{Wizard.Slug}.zip" : $"{Wizard.Slug}.xml");
        var mime = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var streamRef = new DotNetStreamReference(stream);
        await JS.InvokeVoidAsync("studio.downloadStream", filename, streamRef, mime);
    }

    private string SelectedXml => containers.FirstOrDefault(c => c.Key == selected)?.Xml ?? xml ?? "";
    private void Select(string key) => selected = key;

    public void Dispose() => Wizard.ClearExport();

    // ── segmentation ──
    // Slice the pretty-printed document into its top-level containers by tag (the writer indents top-level
    // children four spaces, so a `^    </tag>` uniquely closes the block). Apply rules live inside <regions>,
    // so they're pulled out on their own.
    private static List<Container> BuildContainers(string doc)
    {
        var list = new List<Container> { new("full", "Full document", "file-code", doc, 0) };
        void Add(string key, string label, string icon, string tag, string itemPattern)
        {
            if (Block(doc, tag) is { } block)
                list.Add(new(key, label, icon, block, Regex.Matches(block, itemPattern).Count));
        }
        Add("teams", "Teams", "users", "teams", @"<team\b");
        Add("spawns", "Spawns", "dot", "spawns", @"<spawn\b|<default\b");
        Add("wools", "Wools", "square", "wools", @"<wool\b");
        Add("filters", "Filters", "filter", "filters", @"(?m)^        <[a-zA-Z]");   // direct children (8-space)
        Add("regions", "Regions", "shapes", "regions", "id=\"");                    // total regions
        var applies = Regex.Matches(doc, @"(?m)^        <apply\b[^>]*?/>").Select(m => m.Value).ToList();
        if (applies.Count > 0)
            list.Add(new("apply", "Apply rules", "zap", string.Join("\n", applies), applies.Count));
        return list;
    }

    private static string? Block(string doc, string tag)
    {
        var m = Regex.Match(doc, $@"(?ms)^    <{tag}\b(?:[^>]*/>|.*?^    </{tag}>)");
        return m.Success ? m.Value : null;
    }

    private static string Trunc(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
