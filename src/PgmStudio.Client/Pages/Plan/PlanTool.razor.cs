using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PgmStudio.Geom;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages.Plan;

public partial class PlanTool
{
    [Inject] private HttpClient Http { get; set; } = default!;

    /// <summary>When routed as <c>/maps/{slug}/plan</c>, the plan is a <c>stage=plan</c> map row and the editor
    /// loads/saves its <c>plan_json</c> artifact (GET/PUT <c>/api/map/{slug}/plan</c>) in place — no fork doctrine.
    /// Null on the bare <c>/plan-editor</c> route (the generator-candidate pool via <c>/api/plans</c>).</summary>
    [Parameter] public string? Slug { get; set; }

    private bool MapBacked => Slug is { Length: > 0 };

    // ── Phases (rail, map-backed plans only): Info (Identity + Settings steps) · Draw (the canvas). Draw
    //    stays mounted while Info is up (hidden, not torn down) so the plan doc + zoom survive the trip. The
    //    bare /plan-editor candidate route has no phase host — it stays on the Draw workspace. ──
    [SupplyParameterFromQuery] public string? Phase { get; set; }
    private string active = "draw";
    private bool InfoActive => MapBacked && active == "info";
    // The Draw workspace stays mounted; it's hidden (not removed) only while a map-backed plan is on Info.
    private bool DrawHidden => MapBacked && active != "draw";
    // Map-backed Draw always shows its sidebar (part of the workspace); the bare route folds it via the rail.
    private bool SidebarOpen => MapBacked || leftOpen;
    private Task GoInfo() => SetPhase("info");
    private Task GoDraw() => SetPhase("draw");

    // A blank map-backed plan lands on Info (?phase=info) to name it; opening an existing one goes to Draw.
    protected override void OnInitialized() { if (Phase == "info") active = "info"; }

    private async Task SetPhase(string p)
    {
        active = p;
        // The canvas was display:none on Info; nudge a resize so its <svg> re-reads the viewport size
        // (preserves zoom/pan — only re-measures), mirroring the Sketch tool's return-to-Draw.
        if (p == "draw" && handle is not null) { try { await handle.InvokeVoidAsync("resize"); } catch { } }
    }

    private ElementReference svgRef, wrapRef, cursorRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<PlanTool>? selfRef;

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

    // Plan-store binding: the DB row the editor currently holds (null = a fresh unsaved plan), its origin
    // (generated | authored | imported — drives the fork-on-save doctrine), and the open-from-DB browser.
    private long? planDbId;
    private string? planOrigin;
    private bool saving;
    private string? saveState;          // a transient "Saved" / error note under the toolbar
    private bool showOpenDb;
    private bool dbBusy;
    private string? dbError;
    private List<PlanSummary> dbPlans = [];

    // Read-only 3-D height preview (G27): whether the iso view is on, and whether it couldn't initialise
    // (no WebGL / the preview module failed to load) so the toggle is disabled.
    private bool threeD;
    private bool isoUnavailable;

    // The left panel is a rail-selected activity — "settings" (plan name / globals / reference / overlays)
    // or "validation" (the evaluator score + fired rules) — plus a collapse flag. Each rail icon toggles its
    // own panel: clicking the active-and-open one collapses the sidebar, clicking any other case opens that
    // panel (switching is just clicking the other icon). The Rules evidence layer follows an open validation
    // panel, so validation's icon doubles as that layer's toggle.
    private string leftPanel = "settings";
    private bool leftOpen = true;

    private async Task SelectActivity(string which)
    {
        if (leftPanel == which && leftOpen)
            leftOpen = false;
        else
            (leftPanel, leftOpen) = (which, true);
        if (handle is not null) await handle.InvokeVoidAsync("setOverlay", "violations", leftOpen && leftPanel == "validation");
    }

    // Map-backed Draw: an in-sidebar switch between the Settings (reference + overlays) and Validation
    // panels — the rail is phases, not activities, so the panel toggle moves into the sidebar. No collapse
    // (the sidebar is part of the Draw workspace); the violations layer follows the Validation panel.
    private async Task SetPanel(string which)
    {
        leftPanel = which;
        if (handle is not null) await handle.InvokeVoidAsync("setOverlay", "violations", leftPanel == "validation");
    }

    // Globals mirrored from the plan document (the JS bridge is the source of truth; these drive the form).
    private string planName = "Untitled plan";
    private string symmetry = "rot_180";
    private double cell = 5, surface = 9, headroom = 11, maxPlayers = 12;

    // Surface-stepper increment (blocks per ± click on a piece's surface). An editor preference persisted by
    // the bridge (default 2 per EL1), not part of the plan.
    private double surfaceStep = 2;

    private PlanSelection? sel;

    // Derived-structure overlay toggles (mirrored from the bridge's persisted prefs). The Rules (violations)
    // layer is not here — it is driven by the "validation" activity, not a settings-panel toggle.
    private bool overlayInterfaces = true, overlayFrontline = true, overlayLabels;
    private bool heightMap;

    // The live evaluator feed (score + fired rules), pushed from the bridge's /api/plan/evaluate poll. Null when
    // the plan is malformed (the evaluate endpoint 400s) or before the first response.
    private EvaluationDto? evaluation;
    // The violation whose evidence is isolated on the canvas (index into the current feed's Violations), or null
    // for the all-violations overlay. Cleared whenever a new feed arrives — a stale index would isolate the wrong
    // rule (the canvas resets its own focus in lockstep from the same response).
    private int? selectedViolation;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Reference (tracing) backdrop: the traceable maps for the picker + the current placement, both mirrored
    // from the plan doc via the meta sync. The bridge owns the doc; these drive the sidebar form.
    private List<MapSummary> traceMaps = [];
    private string? refMap;
    private double refOpacity = 0.5, refScale = 1, refOffsetX, refOffsetZ;
    private string? refError;

    private record RolePalette(string Id, string Label, string Color);

    // The G48 taxonomy: true pieces (terrain-producing roles) vs technical pieces (non-generating annotations).
    // Both are drawn from the palette; markers (wool/spawn/iron/wall) and the build zone are separate tools.
    private static readonly RolePalette[] GeneratingRoles =
    [
        new("piece", "Piece", "#7c8899"),
        new("spawn", "Spawn", "#8f7bd6"),
        new("wool-room", "Wool room", "#3fae74"),
    ];
    private static readonly RolePalette[] TechnicalRoles =
    [
        new("buffer", "Buffer", "#f2792b"),
        new("connector", "Connector", "#2dd4bf"),
    ];
    // Every assignable role, for the inspector's role dropdown (a piece can become any of them).
    private static readonly RolePalette[] Roles = [.. GeneratingRoles, .. TechnicalRoles];

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
        // The Rules layer follows an open validation panel, not the persisted overlay flag — sync it to the initial state.
        try { await handle.InvokeVoidAsync("setOverlay", "violations", leftOpen && leftPanel == "validation"); } catch { }
        try { heightMap = await handle.InvokeAsync<bool>("getHeightMap"); } catch { /* keep default off */ }
        try { surfaceStep = await handle.InvokeAsync<double>("getSurfaceStep"); } catch { /* keep default 2 */ }
        try
        {
            var all = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps");
            traceMaps = all?.Where(m => m.HasSurface).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        }
        catch { /* picker just stays empty */ }
        // A map-backed plan (/maps/{slug}/plan) loads its artifact; the bare route honours the generator
        // hand-off (?plan=<id> loads that candidate).
        if (MapBacked) await LoadFromMap(Slug!);
        else if (PlanIdFromQuery() is { } planId) await LoadFromDb(planId);
        StateHasChanged();
    }

    /// <summary>The <c>plan</c> query-string id, if the editor was opened with one (generator hand-off).</summary>
    private long? PlanIdFromQuery()
    {
        var query = new Uri(Nav.Uri).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "plan" && long.TryParse(kv[1], out var id)) return id;
        }
        return null;
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

    // ── 3-D height preview (G27) ─────────────────────────────────────────────────

    private async Task Toggle3D()
    {
        if (isoUnavailable) return;
        threeD = !threeD;
        if (handle is null) return;
        // The bridge reports an unavailable preview asynchronously via OnIsoUnavailable; this catch only
        // guards a hard interop failure so the toggle can never trip Blazor's unhandled-error boundary.
        try { await handle.InvokeVoidAsync("setView", threeD ? "iso" : "2d"); }
        catch { threeD = false; isoUnavailable = true; StateHasChanged(); }
    }

    private Task RotateIso() => handle?.InvokeVoidAsync("rotateIso").AsTask() ?? Task.CompletedTask;

    // ── reference (tracing) backdrop ─────────────────────────────────────────────

    private async Task OnPickReferenceMap(ChangeEventArgs e)
    {
        refError = null;
        if (handle is null) return;
        var slug = e.Value?.ToString();
        var arg = string.IsNullOrEmpty(slug) ? null : slug;
        var err = await handle.InvokeAsync<string?>("setReferenceMap", arg);
        if (err is not null) refError = err;   // the bridge fires OnMeta on success, which re-syncs the form
        StateHasChanged();
    }

    private async Task OnRefOpacity(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var v)) refOpacity = v;
        if (handle is not null) await handle.InvokeVoidAsync("setReferenceParam", "opacity", refOpacity);
    }

    private Task OnRefScale(double v) { refScale = v; return RefParam("scale", v); }
    private Task OnRefOffsetX(double v) { refOffsetX = v; return RefParam("offsetX", v); }
    private Task OnRefOffsetZ(double v) { refOffsetZ = v; return RefParam("offsetZ", v); }

    private Task RefParam(string key, double v)
        => handle?.InvokeVoidAsync("setReferenceParam", key, v).AsTask() ?? Task.CompletedTask;

    private async Task RecenterReference()
    {
        refOffsetX = refOffsetZ = 0; refScale = 1;
        if (handle is not null) await handle.InvokeVoidAsync("recenterReference");
    }

    private async Task ClearReference()
    {
        refMap = null; refError = null;
        if (handle is not null) await handle.InvokeVoidAsync("clearReference");
    }

    // ── derived-structure overlays + lint ────────────────────────────────────────

    private async Task ToggleOverlay(string key)
    {
        var on = key switch
        {
            "interfaces" => overlayInterfaces = !overlayInterfaces,
            "labels" => overlayLabels = !overlayLabels,
            "frontline" => overlayFrontline = !overlayFrontline,
            _ => true,
        };
        if (handle is not null) await handle.InvokeVoidAsync("setOverlay", key, on);
    }

    private async Task ToggleHeightMap()
    {
        heightMap = !heightMap;
        if (handle is not null) await handle.InvokeVoidAsync("setHeightMap", heightMap);
    }

    // Click a violation row to isolate its evidence on the canvas; click it again to restore the all-violations
    // overlay. -1 tells the canvas "show all".
    private async Task SelectViolation(int index)
    {
        selectedViolation = selectedViolation == index ? null : index;
        if (handle is not null) await handle.InvokeVoidAsync("focusViolation", selectedViolation ?? -1);
    }

    private void SyncOverlays(string json)
    {
        var o = JsonSerializer.Deserialize<OverlayDto>(json);
        if (o is null) return;
        overlayInterfaces = o.Interfaces;
        overlayLabels = o.Labels;
        overlayFrontline = o.Frontline;
    }

    // ── globals form ─────────────────────────────────────────────────────────────

    private Task OnName(ChangeEventArgs e) => OnNameChanged(e.Value?.ToString() ?? "Untitled plan");

    // Value-typed entry points shared by the Info phase (map-backed) and the bare-route sidebar controls.
    private async Task OnNameChanged(string v)
    {
        planName = v;
        if (handle is not null) await handle.InvokeVoidAsync("setName", planName);
    }

    /// <summary>
    /// Whether the destroyable/core tools are offered (OB14). A destroyable is one team's to defend and every
    /// other team's to break — a shape that only means something at two teams. PGM's own test is the same:
    /// it marks a goal shared exactly when the team count is not 2, and at four teams every goal is shared,
    /// which is a design nobody has settled. So the tools appear for the order-2 symmetries only, never for
    /// <c>rot_90</c> (order 4) or an unmirrored plan (order 1). Asks <see cref="Symmetry.Order"/> rather than
    /// naming the modes, so a new symmetry mode is classified rather than silently allowed.
    /// </summary>
    private bool ObjectivesOfferable => Symmetry.Order(symmetry) == 2;

    private Task OnSymmetry(ChangeEventArgs e) => OnSymmetryChanged(e.Value?.ToString() ?? "rot_180");

    private async Task OnSymmetryChanged(string v)
    {
        symmetry = v;
        // Leaving a hidden tool armed would let the next click place a marker the plan may not carry.
        if (!ObjectivesOfferable && tool is "destroyable" or "core") await PickTool("select");
        if (handle is not null) await handle.InvokeVoidAsync("setGlobal", "symmetry", symmetry);
    }

    private Task OnCell(double v) { cell = v; return SetGlobal("cell", v); }
    private Task OnSurface(double v) { surface = v; return SetGlobal("surface", v); }
    private Task OnHeadroom(double v) { headroom = v; return SetGlobal("headroom", v); }
    private Task OnMaxPlayers(double v) { maxPlayers = v; return SetGlobal("maxPlayers", v); }

    private Task SetGlobal(string key, double value)
        => handle?.InvokeVoidAsync("setGlobal", key, value).AsTask() ?? Task.CompletedTask;

    // Surface-stepper increment (editor preference; the bridge clamps to a whole number ≥ 1 and persists it).
    // The globals field sets any value; the inspector's quick-preset chips switch the common ones in-context.
    private static readonly int[] StepPresets = [1, 2, 3];

    private async Task OnSurfaceStep(double v)
    {
        surfaceStep = v < 1 ? 1 : v;
        if (handle is not null) surfaceStep = await handle.InvokeAsync<double>("setSurfaceStep", surfaceStep);
    }

    private Task SetSurfaceStep(int s) => OnSurfaceStep(s);

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
        ResetDbBinding();
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
            // A file import is a fresh, not-yet-persisted plan — saving it creates a new authored row.
            else { SyncMeta(await handle.InvokeAsync<string>("getMeta")); ResetDbBinding(); sel = null; }
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

    // ── plan store (DB save / open-from-DB) ──────────────────────────────────────

    private void ResetDbBinding() { planDbId = null; planOrigin = null; saveState = null; }

    // Save the current plan to the DB. The server applies the fork-or-mutate doctrine: a fresh or authored
    // plan is written in place; a loaded generated/imported plan forks a new authored row. The response is
    // adopted so the editor flips onto whatever row now holds the work.
    private async Task SavePlan()
    {
        if (handle is null || saving) return;
        saving = true; saveState = null;
        StateHasChanged();
        try
        {
            var planJson = await handle.InvokeAsync<string>("exportJson");
            // A map-backed plan mutates its artifact in place — it is already the authored map row, no forking.
            if (MapBacked)
            {
                using var mapResp = await Http.PutAsync($"api/map/{Slug}/plan", new StringContent(planJson, Encoding.UTF8, "application/json"));
                saveState = mapResp.IsSuccessStatusCode ? "Saved" : $"Save failed (HTTP {(int)mapResp.StatusCode}).";
                return;
            }
            using var resp = await Http.PostAsJsonAsync("api/plans", new PlanSaveRequest(planJson, planDbId));
            if (resp.IsSuccessStatusCode)
            {
                var saved = await resp.Content.ReadFromJsonAsync<PlanDetail>();
                if (saved is not null)
                {
                    planDbId = saved.Id;
                    planOrigin = saved.Origin;
                    saveState = "Saved";
                }
            }
            else { saveState = $"Save failed (HTTP {(int)resp.StatusCode})."; }
        }
        catch { saveState = "Save failed."; }
        finally { saving = false; StateHasChanged(); }
    }

    private async Task OpenDbBrowser()
    {
        showOpenDb = true;
        dbBusy = true; dbError = null; dbPlans = [];
        StateHasChanged();
        try { dbPlans = await Http.GetFromJsonAsync<List<PlanSummary>>("api/plans") ?? []; }
        catch { dbError = "Could not load plans."; }
        finally { dbBusy = false; StateHasChanged(); }
    }

    private void CloseDbBrowser() => showOpenDb = false;

    private async Task LoadFromDb(long id)
    {
        if (handle is null) return;
        importError = null;
        try
        {
            var detail = await Http.GetFromJsonAsync<PlanDetail>($"api/plans/{id}");
            if (detail is null) { dbError = "Plan not found."; return; }
            var err = await handle.InvokeAsync<string?>("importJson", detail.PlanJson);
            if (err is not null) { importError = err; return; }
            SyncMeta(await handle.InvokeAsync<string>("getMeta"));
            planDbId = detail.Id;
            planOrigin = detail.Origin;
            saveState = null;
            sel = null;
            showOpenDb = false;
        }
        catch { dbError = "Could not open the plan."; }
        StateHasChanged();
    }

    // Load a map-backed plan's artifact (GET /api/map/{slug}/plan). An empty {} body means the map has no
    // stored plan yet — a fresh blank plan; the editor keeps its default doc rather than importing garbage.
    private async Task LoadFromMap(string slug)
    {
        if (handle is null) return;
        importError = null;
        try
        {
            var json = await Http.GetStringAsync($"api/map/{slug}/plan");
            if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "{}")
            {
                var err = await handle.InvokeAsync<string?>("importJson", json);
                if (err is not null) { importError = err; return; }
            }
            SyncMeta(await handle.InvokeAsync<string>("getMeta"));
            // The map row's name (metadata) is authoritative for a map-backed plan — the Info phase saves a
            // rename there, not into the plan artifact. Sync the doc's name from it so the rename survives a
            // reload even though the artifact wasn't re-saved; a later plan-save then persists it into the doc.
            try
            {
                var meta = await Http.GetFromJsonAsync<JsonElement>($"api/map/{slug}");
                if (meta.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } metaName && metaName != planName)
                {
                    planName = metaName;
                    await handle.InvokeVoidAsync("setName", planName);
                }
            }
            catch { /* metadata unreachable — keep the doc's name */ }
            sel = null;
        }
        catch { importError = "Could not open the plan."; }
        StateHasChanged();
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

        var r = m.Reference;
        refMap = string.IsNullOrEmpty(r?.Map) ? null : r!.Map;
        refOffsetX = r?.Offset is { Length: 2 } o ? o[0] : 0;
        refOffsetZ = r?.Offset is { Length: 2 } o2 ? o2[1] : 0;
        refScale = r is null ? 1 : r.Scale;
        refOpacity = r is null ? 0.5 : r.Opacity;
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

    /// <summary>The bridge couldn't initialise the read-only 3-D preview (WebGL unavailable, or the
    /// preview module failed to load); fall back to 2-D and disable the toggle.</summary>
    [JSInvokable]
    public void OnIsoUnavailable()
    {
        threeD = false;
        isoUnavailable = true;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnZoom(int pct) { zoomLabel = $"{pct}%"; StateHasChanged(); }

    [JSInvokable]
    public void OnMeta(string json) { SyncMeta(json); StateHasChanged(); }

    [JSInvokable]
    public void OnEvaluation(string json)
    {
        evaluation = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<EvaluationDto>(json, Web);
        selectedViolation = null;   // a fresh feed — the canvas drops its focus too, so the two stay in step
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
        [JsonPropertyName("reference")] public ReferenceDto? Reference { get; set; }
    }

    private sealed class ReferenceDto
    {
        [JsonPropertyName("map")] public string? Map { get; set; }
        [JsonPropertyName("offset")] public double[]? Offset { get; set; }
        [JsonPropertyName("scale")] public double Scale { get; set; } = 1;
        [JsonPropertyName("opacity")] public double Opacity { get; set; } = 0.5;
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
        [JsonPropertyName("labels")] public bool Labels { get; set; }
        [JsonPropertyName("frontline")] public bool Frontline { get; set; } = true;
        [JsonPropertyName("violations")] public bool Violations { get; set; } = true;
    }

    // A structural finding from /api/plan/compile — the 422 errors that block a compile (the compile-drawer list).
    private sealed class InspectFinding
    {
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("rule")] public string? Rule { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("subjects")] public string[]? Subjects { get; set; }
    }
}
