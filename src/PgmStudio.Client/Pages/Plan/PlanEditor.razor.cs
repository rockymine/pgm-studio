using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Plan;

public partial class PlanEditor
{
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference svgRef, wrapRef, cursorRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<PlanEditor>? selfRef;

    // Compile & test drawer: the compiled pair (pretty-printed for preview, raw for the draft chain),
    // structural errors when the compile is blocked, and the walk-test loop's per-step draft state.
    private bool showCompile;
    private bool compiling;
    private string compileTab = "layout";
    private bool copied;
    private string? compiledLayout, compiledIntent;   // pretty-printed for the preview panes
    private string? compiledLayoutRaw, compiledIntentRaw;   // verbatim, posted to the draft pipeline
    private string? compileError;                     // a malformed / transport failure message
    private List<InspectFinding> compileErrors = [];  // 422 structural findings (compile blocked)

    private string? draftSlug;
    private bool draftBusy;
    private string draftStep = "";
    private string? draftError;

    private string tool = "select";
    private string role = "piece";
    private string zoomLabel = "—";
    private string? importError;

    // Globals mirrored from the plan document (the JS bridge is the source of truth; these drive the form).
    private string planName = "Untitled plan";
    private string symmetry = "rot_180";
    private double cell = 5, surface = 9, headroom = 11, maxPlayers = 12;

    private PlanSelection? sel;

    // Derived-structure overlay toggles (mirrored from the bridge's persisted prefs) + the live lint feed.
    private bool overlayInterfaces = true, overlayGaps = true, overlayFrontline = true;
    private List<InspectFinding> findings = [];

    private record RolePalette(string Id, string Label, string Color);
    private static readonly RolePalette[] Roles =
    [
        new("piece", "Piece", "#7c8899"),
        new("wool-room", "Wool room", "#3fae74"),
        new("spawn", "Spawn", "#8f7bd6"),
    ];

    private string OffsetLabel => sel?.At is { Length: 2 } a ? $"{a[0]}, {a[1]}" : "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountPlan", svgRef, wrapRef, cursorRef, selfRef);
        await handle.InvokeVoidAsync("setRole", role);
        try { SyncMeta(await handle.InvokeAsync<string>("getMeta")); } catch { /* start with defaults */ }
        try { SyncOverlays(await handle.InvokeAsync<string>("getOverlays")); } catch { /* keep defaults */ }
        StateHasChanged();
    }

    // ── toolbar ────────────────────────────────────────────────────────────────

    private async Task PickTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private async Task PickRole(string r)
    {
        role = r;
        tool = "piece";
        if (handle is not null) { await handle.InvokeVoidAsync("setRole", r); await handle.InvokeVoidAsync("setTool", "piece"); }
    }

    private Task Fit() => handle?.InvokeVoidAsync("fit").AsTask() ?? Task.CompletedTask;

    // ── derived-structure overlays + lint ────────────────────────────────────────

    private async Task ToggleOverlay(string key)
    {
        var on = key switch
        {
            "interfaces" => overlayInterfaces = !overlayInterfaces,
            "gaps" => overlayGaps = !overlayGaps,
            "frontline" => overlayFrontline = !overlayFrontline,
            _ => true,
        };
        if (handle is not null) await handle.InvokeVoidAsync("setOverlay", key, on);
    }

    private Task HighlightFinding(InspectFinding f)
        => handle is not null ? handle.InvokeVoidAsync("highlightSubjects", JsonSerializer.Serialize(f.Subjects ?? [])).AsTask() : Task.CompletedTask;

    private void SyncOverlays(string json)
    {
        var o = JsonSerializer.Deserialize<OverlayDto>(json);
        if (o is null) return;
        overlayInterfaces = o.Interfaces;
        overlayGaps = o.Gaps;
        overlayFrontline = o.Frontline;
    }

    // ── globals form ─────────────────────────────────────────────────────────────

    private async Task OnName(ChangeEventArgs e)
    {
        planName = e.Value?.ToString() ?? "Untitled plan";
        if (handle is not null) await handle.InvokeVoidAsync("setName", planName);
    }

    private async Task OnSymmetry(ChangeEventArgs e)
    {
        symmetry = e.Value?.ToString() ?? "rot_180";
        if (handle is not null) await handle.InvokeVoidAsync("setGlobal", "symmetry", symmetry);
    }

    private Task OnCell(double v) { cell = v; return SetGlobal("cell", v); }
    private Task OnSurface(double v) { surface = v; return SetGlobal("surface", v); }
    private Task OnHeadroom(double v) { headroom = v; return SetGlobal("headroom", v); }
    private Task OnMaxPlayers(double v) { maxPlayers = v; return SetGlobal("maxPlayers", v); }

    private Task SetGlobal(string key, double value)
        => handle?.InvokeVoidAsync("setGlobal", key, value).AsTask() ?? Task.CompletedTask;

    // ── inspector edits ──────────────────────────────────────────────────────────

    private Task OnPieceId(ChangeEventArgs e)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setPieceId", sel.Id, e.Value?.ToString() ?? "").AsTask() : Task.CompletedTask;

    private Task OnPieceRole(ChangeEventArgs e)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setPieceRole", sel.Id, e.Value?.ToString() ?? "piece").AsTask() : Task.CompletedTask;

    private Task StepSurface(int delta)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("stepPieceSurface", sel.Id, delta).AsTask() : Task.CompletedTask;

    private Task ToggleMirrors()
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("togglePieceMirrors", sel.Id).AsTask() : Task.CompletedTask;

    private Task OnZoneId(ChangeEventArgs e)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setZoneId", sel.Id, e.Value?.ToString() ?? "").AsTask() : Task.CompletedTask;

    private Task CycleFacing()
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("cycleFacing", sel.Index).AsTask() : Task.CompletedTask;

    private Task DeleteSelected() => handle?.InvokeVoidAsync("deleteSelected").AsTask() ?? Task.CompletedTask;

    // ── plan file / lifecycle ────────────────────────────────────────────────────

    private async Task NewPlan()
    {
        importError = null;
        if (handle is null) return;
        await handle.InvokeVoidAsync("newDoc");
        SyncMeta(await handle.InvokeAsync<string>("getMeta"));
        sel = null;
        StateHasChanged();
    }

    private async Task OnImport(InputFileChangeEventArgs e)
    {
        importError = null;
        if (handle is null) return;
        try
        {
            using var reader = new StreamReader(e.File.OpenReadStream(1024 * 1024));
            var text = await reader.ReadToEndAsync();
            var err = await handle.InvokeAsync<string?>("importJson", text);
            if (err is not null) { importError = err; }
            else { SyncMeta(await handle.InvokeAsync<string>("getMeta")); sel = null; }
        }
        catch { importError = "Could not read the file."; }
        StateHasChanged();
    }

    private async Task ExportPlan()
    {
        if (handle is null) return;
        var json = await handle.InvokeAsync<string>("exportJson");
        var slug = string.IsNullOrWhiteSpace(planName) ? "plan" : planName.Trim().ToLowerInvariant().Replace(' ', '-');
        await JS.InvokeVoidAsync("studio.downloadText", $"{slug}.plan.json", json, "application/json");
    }

    private void SyncMeta(string json)
    {
        var m = JsonSerializer.Deserialize<MetaDto>(json);
        if (m?.Globals is null) return;
        planName = m.Name ?? "Untitled plan";
        symmetry = m.Globals.Symmetry ?? "rot_180";
        cell = m.Globals.Cell;
        surface = m.Globals.Surface;
        headroom = m.Globals.Headroom;
        maxPlayers = m.Globals.MaxPlayers;
    }

    // ── compile & test (the walk-test loop) ──────────────────────────────────────

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private async Task OpenCompile()
    {
        showCompile = true;
        await Compile();
    }

    private void CloseCompile() => showCompile = false;

    // Post the current plan to /api/plan/compile. A 422 renders its structural findings in place of the JSON;
    // a 400 (malformed) / transport failure shows a message. A 200 stores the compiled pair for preview + the
    // draft chain. Compiling resets any prior draft so a fresh compile starts the loop over.
    private async Task Compile()
    {
        if (handle is null) return;
        compiling = true;
        compileError = null;
        compileErrors = [];
        compiledLayout = compiledIntent = compiledLayoutRaw = compiledIntentRaw = null;
        draftSlug = null; draftError = null; draftBusy = false;
        StateHasChanged();

        try
        {
            var planJson = await handle.InvokeAsync<string>("exportJson");
            using var resp = await Http.PostAsync("api/plan/compile", new StringContent(planJson, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var layout = doc.GetProperty("layout");
                var intent = doc.GetProperty("intent");
                compiledLayoutRaw = layout.GetRawText();
                compiledIntentRaw = intent.GetRawText();
                compiledLayout = JsonSerializer.Serialize(layout, Pretty);
                compiledIntent = JsonSerializer.Serialize(intent, Pretty);
            }
            else if ((int)resp.StatusCode == 422)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                compileErrors = doc.TryGetProperty("findings", out var f)
                    ? JsonSerializer.Deserialize<List<InspectFinding>>(f.GetRawText()) ?? []
                    : [];
            }
            else
            {
                compileError = $"compile failed (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
            }
        }
        catch (Exception ex) { compileError = ex.Message; }
        compiling = false;
        StateHasChanged();
    }

    private async Task CopyTab()
    {
        var text = compileTab == "layout" ? compiledLayout : compiledIntent;
        if (text is null) return;
        copied = await JS.InvokeAsync<bool>("studio.copyText", text);
        StateHasChanged();
    }

    private async Task DownloadTab()
    {
        var text = compileTab == "layout" ? compiledLayout : compiledIntent;
        if (text is null) return;
        var slug = string.IsNullOrWhiteSpace(planName) ? "plan" : planName.Trim().ToLowerInvariant().Replace(' ', '-');
        await JS.InvokeVoidAsync("studio.downloadText", $"{slug}.{compileTab}.json", text, "application/json");
    }

    // Drive the existing draft pipeline from a successful compile: create a draft, push the compiled layout,
    // rasterize it, then push the compiled intent. Any non-2xx step aborts with a message naming that step.
    private async Task CreateDraft()
    {
        if (compiledLayoutRaw is null || compiledIntentRaw is null) return;
        draftBusy = true; draftError = null; draftSlug = null;

        try
        {
            draftStep = "Creating draft"; StateHasChanged();
            using var createResp = await Http.PostAsJsonAsync("api/sketch", new { name = planName });
            if (!await Ok(createResp, "create draft")) return;
            var slug = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
            if (string.IsNullOrEmpty(slug)) { draftError = "create draft: no slug returned"; return; }

            draftStep = "Saving layout"; StateHasChanged();
            using var layoutResp = await Http.PutAsync($"api/map/{slug}/sketch", new StringContent(compiledLayoutRaw, Encoding.UTF8, "application/json"));
            if (!await Ok(layoutResp, "save layout")) return;

            draftStep = "Rasterizing"; StateHasChanged();
            using var finishResp = await Http.PostAsync($"api/map/{slug}/sketch/finish", null);
            if (!await Ok(finishResp, "finish (rasterize)")) return;

            draftStep = "Applying intent"; StateHasChanged();
            using var intentResp = await Http.PutAsync($"api/map/{slug}/intent", new StringContent(compiledIntentRaw, Encoding.UTF8, "application/json"));
            if (!await Ok(intentResp, "apply intent")) return;

            draftSlug = slug;
        }
        catch (Exception ex) { draftError = ex.Message; }
        finally { draftBusy = false; StateHasChanged(); }
    }

    private async Task<bool> Ok(HttpResponseMessage resp, string step)
    {
        if (resp.IsSuccessStatusCode) return true;
        draftError = $"{step} failed (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
        return false;
    }

    // Fetch the draft's world export (a {slug}/ ZIP) and save it — checking the status first so a non-2xx
    // error body never lands on disk as a bogus export.
    private async Task DownloadWorld()
    {
        if (draftSlug is null) return;
        draftError = null;
        HttpResponseMessage resp;
        try { resp = await Http.GetAsync($"api/map/{draftSlug}/export"); }
        catch (Exception ex) { draftError = ex.Message; StateHasChanged(); return; }

        if (!resp.IsSuccessStatusCode)
        {
            draftError = $"export failed (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
            StateHasChanged();
            return;
        }

        var filename = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? (resp.Content.Headers.ContentType?.MediaType == "application/zip" ? $"{draftSlug}.zip" : $"{draftSlug}.xml");
        var mime = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var streamRef = new DotNetStreamReference(stream);
        await JS.InvokeVoidAsync("studio.downloadStream", filename, streamRef, mime);
    }

    private static string Trunc(string s) => s.Length > 200 ? s[..200] + "…" : s;

    // ── bridge callbacks ─────────────────────────────────────────────────────────

    [JSInvokable]
    public void OnSelect(string? json)
    {
        sel = json is null ? null : JsonSerializer.Deserialize<PlanSelection>(json);
        StateHasChanged();
    }

    [JSInvokable]
    public void OnTool(string t) { tool = t; StateHasChanged(); }

    [JSInvokable]
    public void OnZoom(int pct) { zoomLabel = $"{pct}%"; StateHasChanged(); }

    [JSInvokable]
    public void OnMeta(string json) { SyncMeta(json); StateHasChanged(); }

    [JSInvokable]
    public void OnFindings(string json)
    {
        findings = JsonSerializer.Deserialize<List<InspectFinding>>(json) ?? [];
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }

    // DTOs pushed from the bridge.
    private sealed class PlanSelection
    {
        [JsonPropertyName("kind")] public string Kind { get; set; } = "";
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("surface")] public int Surface { get; set; }
        [JsonPropertyName("surfaceSet")] public bool SurfaceSet { get; set; }
        [JsonPropertyName("mirrors")] public bool Mirrors { get; set; }
        [JsonPropertyName("markerKind")] public string MarkerKind { get; set; } = "";
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("piece")] public string Piece { get; set; } = "";
        [JsonPropertyName("at")] public double[]? At { get; set; }
        [JsonPropertyName("facing")] public string Facing { get; set; } = "";
    }

    private sealed class MetaDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("globals")] public GlobalsDto? Globals { get; set; }
    }

    private sealed class GlobalsDto
    {
        [JsonPropertyName("cell")] public int Cell { get; set; } = 5;
        [JsonPropertyName("symmetry")] public string? Symmetry { get; set; }
        [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; } = 12;
        [JsonPropertyName("surface")] public int Surface { get; set; } = 9;
        [JsonPropertyName("headroom")] public int Headroom { get; set; } = 11;
    }

    private sealed class OverlayDto
    {
        [JsonPropertyName("interfaces")] public bool Interfaces { get; set; } = true;
        [JsonPropertyName("gaps")] public bool Gaps { get; set; } = true;
        [JsonPropertyName("frontline")] public bool Frontline { get; set; } = true;
    }

    // A validation finding pushed from the bridge (already error-first ordered) for the lint panel.
    private sealed class InspectFinding
    {
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("rule")] public string? Rule { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("subjects")] public string[]? Subjects { get; set; }
    }
}
