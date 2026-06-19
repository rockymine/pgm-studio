using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

// The PersonRow markup template stays in OverviewActivity.razor (it is Razor markup); all the
// state and behaviour for the Overview activity lives here in the code-behind partial.
public partial class OverviewActivity : IAsyncDisposable
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public EventCallback<string?> OnStatus { get; set; }

    private ElementReference svgRef, wrapRef;
    private IJSObjectReference? canvasHandle;

    private const string AvatarEmpty = "data:image/gif;base64,R0lGODlhEAAQAAAAACwAAAAAEAAQAAABEIQBADs=";

    private sealed class Person { public string Uuid = ""; public string Name = ""; public string Contribution = ""; public bool Error; }

    private string name = "", version = "", gamemode = "", objective = "";
    private readonly List<Person> authors = new();
    private readonly List<Person> contributors = new();
    private bool dirty;
    private string? saveStatus;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            name = Str(doc, "name"); version = Str(doc, "version");
            gamemode = Str(doc, "gamemode"); objective = Str(doc, "objective");
            authors.Clear(); contributors.Clear();
            if (doc.TryGetProperty("authors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var a in arr.EnumerateArray())
                {
                    var p = new Person { Uuid = Str(a, "uuid"), Name = Str(a, "name"), Contribution = Str(a, "contribution") };
                    (Str(a, "role") == "contributor" ? contributors : authors).Add(p);
                }
            dirty = false; saveStatus = null;
            await ReportStatus();
            // Resolve any stored uuid that has no cached name → fill the display name (best-effort).
            foreach (var p in authors.Concat(contributors).Where(p => p.Uuid.Length > 0 && p.Name.Length == 0))
                _ = ResolveByUuid(p);
        }
        catch { saveStatus = "Failed to load."; }
    }

    /// <summary>Resolve a stored uuid to its current username for display (does not mark dirty).</summary>
    private async Task ResolveByUuid(Person p)
    {
        try
        {
            var r = await Http.GetFromJsonAsync<JsonElement>($"api/minecraft/player?uuid={Uri.EscapeDataString(p.Uuid)}");
            p.Name = Str(r, "name");
            StateHasChanged();
        }
        catch { /* leave the uuid showing if Mojang is unreachable / renamed-away */ }
    }

    /// <summary>On blur of a name field: look the typed value up via Mojang, storing the canonical
    /// uuid (the persisted identity) and the resolved name. Clears the uuid + flags an error on miss.</summary>
    private async Task ResolveName(Person p)
    {
        var val = p.Name.Trim();
        if (val.Length == 0) { p.Uuid = ""; p.Error = false; return; }
        var isUuid = val.Contains('-') && val.Length > 30;
        var q = isUuid ? $"uuid={Uri.EscapeDataString(val)}" : $"name={Uri.EscapeDataString(val)}";
        try
        {
            var r = await Http.GetFromJsonAsync<JsonElement>($"api/minecraft/player?{q}");
            p.Uuid = Str(r, "uuid"); p.Name = Str(r, "name"); p.Error = false;
        }
        catch { p.Uuid = ""; p.Error = true; }
        Dirty(); StateHasChanged();
    }

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private void Dirty() { dirty = true; saveStatus = null; }

    private void AddPerson(List<Person> list) { list.Add(new Person()); Dirty(); }
    private void RemovePerson(List<Person> list, Person p) { list.Remove(p); Dirty(); }

    private async Task Save()
    {
        saveStatus = "Saving…"; StateHasChanged();
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["version"] = version,
            ["gamemode"] = gamemode,
            ["objective"] = objective,
            ["authors"] = authors.Select(p => Author(p, "author"))
                .Concat(contributors.Select(p => Author(p, "contributor"))).ToList(),
        };
        var resp = await Http.PatchAsJsonAsync($"api/map/{Slug}/metadata", payload);
        if (resp.IsSuccessStatusCode) { dirty = false; saveStatus = "Saved."; await ReportStatus(); }
        else saveStatus = $"Save failed ({(int)resp.StatusCode}).";
        StateHasChanged();
    }

    private static Dictionary<string, object?> Author(Person p, string role) => new()
    {
        ["uuid"] = p.Uuid, ["name"] = p.Name, ["role"] = role, ["contribution"] = p.Contribution,
    };

    // Required identity fields drive the rail status dot (yellow = incomplete).
    private Task ReportStatus()
    {
        var complete = !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(gamemode);
        return OnStatus.InvokeAsync(complete ? null : "yellow");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender)
            canvasHandle = await JS.InvokeAsync<IJSObjectReference>("studio.mountOverview", svgRef, wrapRef, Slug);
    }

    public async ValueTask DisposeAsync()
    {
        if (canvasHandle is not null)
        {
            try { await canvasHandle.InvokeVoidAsync("dispose"); } catch { }
            try { await canvasHandle.DisposeAsync(); } catch { }
        }
    }
}
