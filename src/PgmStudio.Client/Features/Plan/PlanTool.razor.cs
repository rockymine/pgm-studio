using PgmStudio.Vocabulary;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Geom;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Plan;

public partial class PlanTool
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private StudioAccess Access { get; set; } = default!;

    /// <summary>Whether this editor shows boxes: the composer's model of a layout, which only an admin works
    /// with. A member's canvas does not draw or pick them, and the Boxes tool, its chord, the box inspector and
    /// the Generator check are absent; the boxes stay in the plan either way. Resolved when the canvas mounts,
    /// before the plan is loaded into it.</summary>
    private bool showBoxes;

    /// <summary>When routed as <c>/maps/{slug}/plan</c>, the plan is a <c>stage=plan</c> map row and the editor
    /// loads/saves its <c>plan_json</c> artifact (GET/PUT <c>/api/map/{slug}/plan</c>) in place — no fork doctrine.
    /// Null on a plan-row route.</summary>
    [Parameter] public string? Slug { get; set; }

    /// <summary>The plan row <c>/plans/{id}</c> opens (<c>GET</c>/<c>POST /api/plans</c>). Null on
    /// <c>/plans/new</c>, which holds a blank plan until Save stores it as a row.</summary>
    [Parameter] public long? PlanId { get; set; }

    private bool MapBacked => Slug is { Length: > 0 };

    /// <summary>What the route asks the editor to hold: a map, a plan row, or a new plan.</summary>
    private string RouteKey => MapBacked ? $"map:{Slug}" : PlanId is { } id ? $"plan:{id}" : NewKey;

    private const string NewKey = "new";

    /// <summary>What the editor holds now, in <see cref="RouteKey"/>'s terms. It leads the route when Save
    /// forks a row or Open picks one, and the URL is brought after it; it trails the route when the address
    /// changes under a mounted editor, and <see cref="BindAsync"/> loads what the route names.</summary>
    private string? boundKey;

    /// <summary>The page component outlives a navigation between its own routes, and a route parameter
    /// the new route does not name keeps its old value; both are cleared so each route states its binding.</summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        Slug = null;
        PlanId = null;
        return base.SetParametersAsync(parameters);
    }

    // ── Phases (the rail): Info (Identity + Settings steps) · Draw (the canvas). Draw stays mounted while
    //    Info is up (hidden, not torn down) so the plan doc + zoom survive the trip. ──
    [SupplyParameterFromQuery] public string? Phase { get; set; }
    private string active = "draw";
    private bool InfoActive => active == "info";
    private bool DrawActive => active == "draw";
    // The Draw workspace stays mounted; it's hidden (not removed) on Info.
    private bool DrawHidden => active != "draw";
    private Task GoInfo() => SetPhase("info");
    private Task GoDraw() => SetPhase("draw");

    // A blank plan lands on Info (?phase=info) to name it; opening an existing one goes to Draw.
    protected override void OnInitialized()
    {
        iso = new IsoView(() => handle, StateHasChanged);
        active = Phase == "info" ? "info" : "draw";
    }

    /// <summary>A navigation between this component's routes rebinds the mounted editor rather than
    /// remounting it. The first binding is the first render's, once the canvas exists; a navigation that lands
    /// while a binding loads is taken up when it finishes.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (handle is null || !documentLoaded || RouteKey == boundKey) return;
        documentLoaded = false;
        try { await BindToRouteAsync(); }
        finally { documentLoaded = true; }
        StateHasChanged();
    }

    private async Task BindToRouteAsync()
    {
        do await BindAsync();
        while (handle is not null && RouteKey != boundKey);
    }

    // Switching phases only flips which body renders: the canvas observes its own wrap and re-measures
    // (and runs a deferred fit) when Draw un-hides. Nudging it from here would run against the still-
    // hidden DOM — this method returns before the phase div is re-rendered.
    private Task SetPhase(string p)
    {
        active = p;
        return Task.CompletedTask;
    }

    private ElementReference svgRef, wrapRef;
    private PlanBuildDrawer? buildDrawer;

    private Task OpenCompile() => buildDrawer?.OpenAsync() ?? Task.CompletedTask;

    /// <summary>The floating top-left readout. Its cursor element is handed to the canvas on mount,
    /// which then writes the world position into it directly — per mousemove, far too often to render.</summary>
    private CanvasReadout? readout;
    private IJSObjectReference? handle;
    private DotNetObjectReference<PlanTool>? selfRef;

    /// <summary>Whether the first render's load chain has finished. The canvas and the toolbar are in the DOM
    /// before the plan document is — nine interop round-trips and up to four reads separate them — so until
    /// this is set the editor is holding the bridge's blank default, and compiling it would post a plan with
    /// no pieces and be told so. True on /plans/new the moment the chain ends, since a plan drawn from
    /// scratch has nothing to wait for.</summary>
    private bool documentLoaded;

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

    /// <summary>The write of a map-backed plan's artifact, which states the revision this tab loaded: a second
    /// tab's save is refused (<c>RQ5</c>, 409) instead of being overwritten, and the tab is then superseded
    /// until it loads the plan again.</summary>
    private DocumentSave Document => document ??= new(Http);
    private DocumentSave? document;

    private bool showOpenDb;
    private bool dbBusy;
    private string? dbError;
    private List<PlanSummary> dbPlans = [];

    // Read-only 3-D height preview.
    private IsoView iso = default!;

    // The Draw sidebar holds one of three panels — "validation" (the evaluator score + fired rules),
    // "settings" (the tracing reference, on an open studio only, since tracing another author's map is
    // copying it) or "feasibility" (the producibility read, admins only) — switched by the chips at its head,
    // and folds away to give the canvas the width. Each panel's overlay follows its panel being shown.
    private string leftPanel = "validation";
    private bool showReference;
    private bool sidebarOpen = true;

    private Task SetPanel(string which)
    {
        leftPanel = which;
        return SyncPanelOverlays();
    }

    private Task ToggleSidebar()
    {
        sidebarOpen = !sidebarOpen;
        return SyncPanelOverlays();
    }

    private Task SyncPanelOverlays()
        => SyncPanelOverlays(sidebarOpen && leftPanel == "validation", sidebarOpen && leftPanel == "feasibility");

    /// <summary>Point each canvas overlay at the panel that owns it: the Rules layer follows Validation, the
    /// nearest-miss evidence follows Feasibility. Leaving a panel drops its overlay, so the canvas never carries
    /// evidence for something the author can no longer see the reason for.</summary>
    private async Task SyncPanelOverlays(bool rules, bool feasible)
    {
        if (handle is null) return;
        await handle.InvokeVoidAsync("setOverlay", "violations", rules);
        if (!feasible && isolatedBox is not null)
        {
            isolatedBox = null;
            await handle.InvokeVoidAsync("showNearestMiss", string.Empty);
        }
    }

    // Globals mirrored from the plan document (the JS bridge is the source of truth; these drive the form).
    private string planName = "Untitled plan";
    private string symmetry = "rot_180";
    private double cell = 4, surface = 9, maxPlayers = 12;

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

    // The live producibility feed ("could the composer have produced this?"), pushed from the bridge's
    // /api/plan/feasibility poll. Null when the plan is malformed or before the first response.
    private FeasibilityDto? feasibility;
    // The box whose nearest-miss cells are painted on the canvas, or null for none. Cleared with every fresh feed
    // (the bridge drops the overlay in lockstep, so the two never disagree).
    private string? isolatedBox;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Reference (tracing) backdrop: the traceable maps for the picker + the current placement, both mirrored
    // from the plan doc via the meta sync. The bridge owns the doc; these drive the sidebar form.
    private List<MapSummary> traceMaps = [];
    private string? refMap;
    private double refOpacity = 0.5, refScale = 1, refOffsetX, refOffsetZ;
    private string? refError;

    // The kind armed for the box tool (the last one drawn), mirrored into the bridge.
    private string boxKind = "hub";

    // The kind armed for the zone tool — build (open from the first tick) or water-lane (opens mid-match).
    private string zoneKind = "build";

    private IReadOnlyList<SelectOption> TraceMapOptions
        => [.. traceMaps.Select(map => new SelectOption(map.Slug, map.Name))];

    /// <summary>The name this tool's chords are registered and dropped under.</summary>
    private const string KeyOwner = "plan-tool";

    /// <summary>Every chord this tool answers. The registry requires the label and the group, which is what
    /// keeps the `?` sheet and the command palette complete without a second list to maintain.</summary>
    private object[] Shortcuts =>
    [
        new { id = "plan.tool.select", keys = "v", label = "Select", group = "Tools" },
        new { id = "plan.tool.pan",    keys = "h", label = "Pan",    group = "Tools" },
        new { id = "plan.tool.piece",  keys = "r", label = "Piece",  group = "Tools" },
        new { id = "plan.tool.zone",   keys = "z", label = "Zone",   group = "Tools" },
        .. showBoxes ? (object[])[new { id = "plan.tool.box", keys = "g", label = "Box", group = "Tools" }] : [],
        new { id = "plan.tool.wall",   keys = "w", label = "Wall",   group = "Tools" },
        new { id = "plan.fit",         keys = "f", label = "Zoom to fit", group = "Canvas" },
        new { id = "plan.save",        keys = "mod+s", label = "Save", group = "Everywhere", inField = true },
    ];

    /// <summary>A chord this tool registered. The registry holds the words; this holds what the chord does.</summary>
    [JSInvokable]
    public async Task OnShortcut(string id)
    {
        // The chords that draw or save stand down where the caller may not write.
        if (writeClosed && id is "plan.tool.piece" or "plan.tool.zone" or "plan.tool.box" or "plan.tool.wall" or "plan.save")
            return;
        switch (id)
        {
            case "plan.tool.select": await PickTool("select"); break;
            case "plan.tool.pan":    await PickTool("pan"); break;
            case "plan.tool.piece":  await PickTool("piece"); break;
            case "plan.tool.zone":   await PickTool("zone"); break;
            case "plan.tool.box":    await PickTool("box"); break;
            case "plan.tool.wall":   await PickTool("wall"); break;
            case "plan.fit":         await Fit(); break;
            case "plan.save":        await SavePlan(); break;
        }
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        // Everything the editor needs before it can be compiled, in one block, so the loading state ends
        // whatever the block does: a mount that threw leaves the tool broken, and a Compile stuck on
        // "Loading…" would hide that behind a state that never resolves.
        try
        {
            handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountPlan", svgRef, wrapRef, readout!.Cursor, selfRef);
            await handle.InvokeVoidAsync("setReadOnly", writeClosed);
            await handle.InvokeVoidAsync("setRole", role);
            try { showBoxes = await Access.IsAdminAsync(); } catch { showBoxes = false; }
            try { showReference = (await Access.MeAsync()).Mode == AccessModes.Open; } catch { showReference = false; }
            await handle.InvokeVoidAsync("setBoxesShown", showBoxes);
            try { SyncMeta(await handle.InvokeAsync<string>("getMeta")); } catch { /* start with defaults */ }
            try { SyncOverlays(await handle.InvokeAsync<string>("getOverlays")); } catch { /* keep defaults */ }
            // The Rules layer follows an open validation panel, not the persisted overlay flag — sync it to the initial state.
            try { await handle.InvokeVoidAsync("setOverlay", "violations", sidebarOpen && leftPanel == "validation"); } catch { }
            await JS.InvokeVoidAsync("studio.registerKeys", KeyOwner, selfRef,
                System.Text.Json.JsonSerializer.Serialize(Shortcuts));
            try { heightMap = await handle.InvokeAsync<bool>("getHeightMap"); } catch { /* keep default off */ }
            try { surfaceStep = await handle.InvokeAsync<double>("getSurfaceStep"); } catch { /* keep default 2 */ }
            try
            {
                var all = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps");
                traceMaps = all?.Where(m => m.HasSurface).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            }
            catch { /* picker just stays empty */ }
            await LoadObjectiveVocabularyAsync();
            await BindToRouteAsync();
        }
        finally { documentLoaded = true; }
        StateHasChanged();
    }

    /// <summary>Load what the route names into the editor: a map's plan artifact, a plan row, or a blank plan.
    /// A map left behind is offered the same discard the editor's disposal gives it, and everything read off
    /// the previous binding — the selection, the open drawers, the map state — is dropped with it.</summary>
    private async Task BindAsync()
    {
        if (handle is null) return;
        var previous = boundKey;
        if (previous is not null && previous.StartsWith("map:", StringComparison.Ordinal))
            await DiscardIfEmptyAsync(previous["map:".Length..]);

        boundKey = RouteKey;
        sel = null;
        buildDrawer?.Reset();
        showOpenDb = false;
        importError = null;
        ResetDbBinding();
        if (previous is not null) active = Phase == "info" ? "info" : "draw";

        if (MapBacked) await LoadFromMap(Slug!);
        else if (PlanId is { } planId) await LoadFromDb(planId);
        else if (previous is not null)
        {
            await handle.InvokeVoidAsync("newDoc");
            SyncMeta(await handle.InvokeAsync<string>("getMeta"));
        }
    }

    /// <summary>Bring the address to the plan row the editor now holds, without loading it again: a save
    /// that forked, an Open, a New and an Import each change the row first and the URL after.</summary>
    private void ShowBindingInUrl(bool replace = false)
    {
        if (MapBacked) return;
        boundKey = planDbId is { } id ? $"plan:{id}" : NewKey;
        if (boundKey == RouteKey) return;
        Nav.NavigateTo(planDbId is { } row ? $"plans/{row}" : "plans/new", replace: replace);
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

    /// <summary>Arm the box tool with <paramref name="k"/> — the kind every box drawn from now on takes, until
    /// another is picked (the same arm-a-kind model the piece roles use).</summary>
    private async Task PickBoxKind(string k)
    {
        boxKind = k;
        tool = "box";
        if (handle is not null) { await handle.InvokeVoidAsync("armBoxKind", k); await handle.InvokeVoidAsync("setTool", "box"); }
    }

    // ── the dock's four collapsed families ─────────────────────────────────────
    //
    // Every drawing tool here is one of four questions — which piece role · which technical annotation ·
    // which marker · which box kind — and within a family only one is ever armed. So each family shows the
    // option last picked from it and keeps the rest behind a chevron. These four fields ARE that memory; the
    // canvas's own armed state (tool / role / boxKind) says which family is live, not which option each one
    // would offer if it were.
    //
    // Keys are per-family, not global: "spawn" is a piece role in one and a marker in another, and each
    // family's pick handler knows which it means.

    private string terrainActive = "piece";
    private string technicalActive = "zone";
    private string markerActive = "spawn";
    private string boxActive = "hub";

    /// <summary>Which family has its flyout showing — at most one, so opening a second closes the first.</summary>
    private string? openFlyout;

    private void ToggleFlyout(string family) => openFlyout = openFlyout == family ? null : family;

    private void CloseFlyouts() => openFlyout = null;

    // A flyout shows one family with nothing around it to say which family, so a name that collides across
    // families says which it is: a piece role, a marker and a box kind can all be called "spawn". Where a
    // name is unique in the whole dock (buffer, destroyable, core) it stands alone.
    private static DockItem[] TerrainItems =>
        [.. PlanPalette.GeneratingRoles.Select(role => new DockItem(role.Id, PieceName(role), Swatch: role.Color))];

    private static string PieceName(PlanPalette.Entry role) => role.Id switch
    {
        "piece" => "Piece",
        "wool-room" => "Wool room",
        _ => $"{role.Label} piece",
    };

    // The build area leads: it is the thing the buffer is drawn in and around, and it is the one an author
    // reaches for first. The water lane sits next to it because it is the same drawing — a rect over the void
    // — differing only in when it opens.
    private static DockItem[] TechnicalItems =>
    [
        new("zone", "Build area", SwatchClass: "canvas-dock-swatch--build"),
        new("water-lane", "Water lane", SwatchClass: "canvas-dock-swatch--water-lane"),
        .. PlanPalette.TechnicalRoles.Select(role => new DockItem(role.Id, role.Label, SwatchClass: $"canvas-dock-swatch--{role.Id}")),
    ];

    /// <summary>The markers this plan may place. A destroyable is defended by one team and broken by the
    /// others, which only means something when there are two: at four teams PGM treats every goal as shared,
    /// and what that should play like is undecided. So the two are offered for the 2-team symmetries only
    /// (OB14).</summary>
    private DockItem[] MarkerItems => ObjectivesOfferable
        ? PlanPalette.AllMarkerItems
        : [.. PlanPalette.AllMarkerItems.Where(m => m.Key is not ("destroyable" or "core"))];

    private static DockItem[] BoxItems =>
        [.. PlanPalette.BoxKinds.Select(kind => new DockItem(kind.Id, $"{kind.Label} box",
                                                             Swatch: kind.Color, SwatchClass: "canvas-dock-swatch--box"))];

    /// <summary>Whether the technical family's option is the armed tool. The two zone kinds share the zone
    /// tool and differ by the armed kind; the rest are piece roles, so the test differs by which is in the
    /// slot.</summary>
    private bool TechnicalArmed => technicalActive switch
    {
        "zone" => tool == "zone" && zoneKind == "build",
        "water-lane" => tool == "zone" && zoneKind == "water-lane",
        _ => tool == "piece" && role == technicalActive,
    };

    private async Task PickTerrain(string key)
    {
        terrainActive = key;
        openFlyout = null;
        await PickRole(key);
    }

    private async Task PickTechnical(string key)
    {
        technicalActive = key;
        openFlyout = null;
        if (key is "zone" or "water-lane") await PickZoneKind(key == "water-lane" ? "water-lane" : "build");
        else await PickRole(key);
    }

    /// <summary>Arm the zone tool for one kind. Both draw the same rect, so the kind is armed alongside the
    /// tool rather than being a tool of its own.</summary>
    private async Task PickZoneKind(string kind)
    {
        zoneKind = kind;
        if (handle is not null) await handle.InvokeVoidAsync("setZoneKind", kind);
        await PickTool("zone");
    }

    private async Task PickMarker(string key)
    {
        markerActive = key;
        openFlyout = null;
        await PickTool(key);
    }

    private async Task PickBox(string key)
    {
        boxActive = key;
        openFlyout = null;
        await PickBoxKind(key);
    }

    private Task Fit() => handle?.InvokeVoidAsync("fit").AsTask() ?? Task.CompletedTask;

    // ── reference (tracing) backdrop ─────────────────────────────────────────────

    private async Task OnPickReferenceMap(string slug)
    {
        refError = null;
        if (handle is null) return;
        var arg = string.IsNullOrEmpty(slug) ? null : slug;
        var err = await handle.InvokeAsync<string?>("setReferenceMap", arg);
        if (err is not null) refError = err;   // the bridge fires OnMeta on success, which re-syncs the form
        StateHasChanged();
    }

    private async Task OnRefOpacity(double opacity)
    {
        refOpacity = opacity;
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

    // Value-typed entry points the Info phase raises; the host owns the bridge they write through.
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

    private async Task OnSymmetryChanged(string v)
    {
        symmetry = v;
        // Leaving a hidden tool armed would let the next click place a marker the plan may not carry — and
        // leaving one in the marker family's slot would show a tool the family no longer offers.
        if (!ObjectivesOfferable)
        {
            if (tool is "destroyable" or "core") await PickTool("select");
            if (markerActive is "destroyable" or "core") markerActive = "spawn";
        }
        if (handle is not null) await handle.InvokeVoidAsync("setGlobal", "symmetry", symmetry);
    }

    private Task OnCell(double v) { cell = v; return SetGlobal("cell", v); }
    private Task OnSurface(double v) { surface = v; return SetGlobal("surface", v); }
    private Task OnMaxPlayers(double v) { maxPlayers = v; return SetGlobal("maxPlayers", v); }

    private Task SetGlobal(string key, double value)
        => handle?.InvokeVoidAsync("setGlobal", key, value).AsTask() ?? Task.CompletedTask;

    // Surface-stepper increment (editor preference; the bridge clamps to a whole number ≥ 1 and persists it).
    // The globals field sets any value; the inspector's quick-preset chips switch the common ones in-context.
    private async Task OnSurfaceStep(double v)
    {
        surfaceStep = v < 1 ? 1 : v;
        if (handle is not null) surfaceStep = await handle.InvokeAsync<double>("setSurfaceStep", surfaceStep);
    }

    // ── objective markers: the defaults their inspector shows ────────────────────────────────────

    /// <summary>The objective vocabulary + defaults (<c>GET /api/objectives/vocabulary</c>). Fetched rather
    /// than hardcoded: the client cannot reach <c>ObjectiveDefaults</c>, and a second copy of these numbers
    /// would show an author a structure the stamper does not build.</summary>
    private ObjectiveVocabularyDto vocabulary = Unloaded;

    /// <summary>What the inspector reads until the vocabulary arrives, or where the studio does not answer:
    /// no choices and zeros, so the panel renders and states nothing it cannot back.</summary>
    private static readonly ObjectiveVocabularyDto Unloaded = new(
        new DestroyableVocabularyDto([], [], "", "", 0),
        new CoreVocabularyDto(0, 0, [], [], 0, 0, false),
        new WoolVocabularyDto([], "auto"));

    private async Task LoadObjectiveVocabularyAsync()
    {
        try { vocabulary = await Http.GetFromJsonAsync<ObjectiveVocabularyDto>("api/objectives/vocabulary") ?? Unloaded; }
        catch { /* keep what is held — the inspector still renders */ }
    }

    // ── plan file / lifecycle ────────────────────────────────────────────────────

    private async Task NewPlan()
    {
        importError = null;
        if (handle is null) return;
        await handle.InvokeVoidAsync("newDoc");
        SyncMeta(await handle.InvokeAsync<string>("getMeta"));
        ResetDbBinding();
        sel = null;
        ShowBindingInUrl();
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
            else { SyncMeta(await handle.InvokeAsync<string>("getMeta")); ResetDbBinding(); sel = null; ShowBindingInUrl(); }
        }
        catch { importError = "Couldn't read the file."; }
        StateHasChanged();
    }

    // ── plan store (DB save / open-from-DB) ──────────────────────────────────────

    private void ResetDbBinding() { planDbId = null; planOrigin = null; saveState = null; }

    /// <summary>What the origin badge means for the next Save.</summary>
    private string OriginTitle => planOrigin == "authored"
        ? "Saving updates this plan"
        : "Saving a generated or imported plan creates a new copy";

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
            // A map-backed plan mutates its artifact in place — it is already the authored map row, no forking.
            if (MapBacked)
            {
                var outcome = await Document.PutAsync($"api/map/{Slug}/plan", () => PlanExport.BodyAsync(handle!));
                saveState = outcome.Landed ? "Saved" : outcome.Message;
                return;
            }
            var planJson = await handle.InvokeAsync<string>("exportJson");
            using var resp = await Http.PostAsJsonAsync("api/plans", new PlanSaveRequest(planJson, planDbId));
            if (resp.IsSuccessStatusCode)
            {
                var saved = await resp.Content.ReadFromJsonAsync<PlanDetail>();
                if (saved is not null)
                {
                    planDbId = saved.Id;
                    planOrigin = saved.Origin;
                    saveState = "Saved";
                    ShowBindingInUrl(replace: true);
                }
            }
            else { saveState = $"Couldn't save (HTTP {(int)resp.StatusCode}). Try again."; }
        }
        catch { saveState = "Couldn't save. Try again."; }
        finally { saving = false; StateHasChanged(); }
    }

    private async Task OpenDbBrowser()
    {
        showOpenDb = true;
        dbBusy = true; dbError = null; dbPlans = [];
        StateHasChanged();
        try { dbPlans = await Http.GetFromJsonAsync<List<PlanSummary>>("api/plans") ?? []; }
        catch { dbError = "Couldn't load plans. Try again."; }
        finally { dbBusy = false; StateHasChanged(); }
    }

    private void CloseDbBrowser() => showOpenDb = false;

    // A generated row made by an older composer opens exactly as stored — only its descriptor has stopped
    // reproducing it, which matters when re-composing that request, not when loading the row.
    private static string StaleTitle(PlanSummary p) =>
        $"Made by generator version {p.ComposerVersion ?? "unknown"}. "
        + "Generating it again today would give a different layout.";

    // Open a plan row from the browser: load it, then bring the address to it.
    private async Task OpenFromDb(long id)
    {
        await LoadFromDb(id);
        if (planDbId == id) ShowBindingInUrl();
    }

    private async Task LoadFromDb(long id)
    {
        if (handle is null) return;
        importError = null;
        try
        {
            var detail = await Http.GetFromJsonAsync<PlanDetail>($"api/plans/{id}");
            if (detail is null) { importError = dbError = "Plan not found."; return; }
            var err = await handle.InvokeAsync<string?>("importJson", detail.PlanJson);
            if (err is not null) { importError = err; return; }
            SyncMeta(await handle.InvokeAsync<string>("getMeta"));
            planDbId = detail.Id;
            planOrigin = detail.Origin;
            saveState = null;
            sel = null;
            showOpenDb = false;
        }
        catch { importError = dbError = "Couldn't open the plan."; }
        StateHasChanged();
    }

    // Load a map-backed plan's artifact (GET /api/map/{slug}/plan). An empty {} body means the map has no
    // stored plan yet — a fresh blank plan; the editor keeps its default doc rather than importing garbage.
    // That default is blank: the bridge restores no cached document at mount, so
    // "no stored plan" renders an empty board instead of whatever this browser last drew.
    private async Task LoadFromMap(string slug)
    {
        if (handle is null) return;
        importError = null;
        try
        {
            Document.Forget();
            using var read = await Http.GetAsync($"api/map/{slug}/plan");
            read.EnsureSuccessStatusCode();
            Document.Hold(read);
            var json = await read.Content.ReadAsStringAsync();
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
                var meta = await Http.GetFromJsonAsync<MapDocumentDto>($"api/map/{slug}");
                if (meta?.Name is { Length: > 0 } metaName && metaName != planName)
                {
                    planName = metaName;
                    await handle.InvokeVoidAsync("setName", planName);
                }
            }
            catch { /* metadata unreachable — keep the doc's name */ }
            sel = null;
        }
        catch { importError = "Couldn't open the plan."; }
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
        maxPlayers = m.Globals.MaxPlayers;

        var r = m.Reference;
        refMap = string.IsNullOrEmpty(r?.Map) ? null : r!.Map;
        refOffsetX = r?.Offset is { Length: 2 } o ? o[0] : 0;
        refOffsetZ = r?.Offset is { Length: 2 } o2 ? o2[1] : 0;
        refScale = r is null ? 1 : r.Scale;
        refOpacity = r is null ? 0.5 : r.Opacity;
    }

    // ── bridge callbacks ─────────────────────────────────────────────────────────

    [JSInvokable]
    public void OnSelect(string? json)
    {
        sel = json is null ? null : JsonSerializer.Deserialize<PlanSelection>(json);
        if (sel is { Kind: "box" } && !showBoxes) sel = null;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnTool(string t) { tool = t; StateHasChanged(); }

    /// <summary>The bridge couldn't show the read-only 3-D preview; <paramref name="reason"/> is empty when
    /// WebGL itself is missing and the build's own sentence otherwise.</summary>
    [JSInvokable]
    public void OnIsoUnavailable(string? reason) => iso.MarkUnavailable(reason);

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

    [JSInvokable]
    public void OnFeasibility(string json)
    {
        feasibility = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<FeasibilityDto>(json, Web);
        isolatedBox = null;         // the bridge clears the canvas evidence on the same response
        StateHasChanged();
    }

    /// <summary>Paint one box's nearest-miss cells on the canvas, or clear them by clicking the same box again.
    /// A box the emitters reproduce has no miss to show, so it does not isolate.</summary>
    private async Task IsolateBox(BoxFeasibilityDto box)
    {
        if (handle is null) return;
        if (isolatedBox == box.BoxId || box.Nearest is null)
        {
            isolatedBox = null;
            await handle.InvokeVoidAsync("showNearestMiss", string.Empty);
            return;
        }
        isolatedBox = box.BoxId;
        await handle.InvokeVoidAsync("showNearestMiss",
            JsonSerializer.Serialize(new { extra = box.Nearest.Extra, missing = box.Nearest.Missing }, Web));
    }

    /// <summary>Whether the caller may not write here, as the shell answers it (a <see cref="WriteGate"/> in
    /// the body hands it over). Closed until the answer is in: the canvas picks and changes nothing.</summary>
    private bool writeClosed = true;

    private async Task OnWriteReason(string? reason)
    {
        writeClosed = reason is not null;
        if (handle is not null) await handle.InvokeVoidAsync("setReadOnly", writeClosed);
    }

    // A "New plan" draft never saved is discarded so an abandoned click doesn't linger on the dashboard; the
    // server decides whether it is untouched (default name, never saved, no one else credited).
    private async Task DiscardIfEmptyAsync(string slug)
    {
        if (writeClosed) return;
        try { await Http.DeleteAsync($"api/map/{slug}/discard-if-empty"); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (MapBacked) await DiscardIfEmptyAsync(Slug!);
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }

    // DTOs pushed from the bridge.
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
        [JsonPropertyName("cell")] public int Cell { get; set; } = 4;
        [JsonPropertyName("symmetry")] public string? Symmetry { get; set; }
        [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; } = 12;
        [JsonPropertyName("surface")] public int Surface { get; set; } = 9;
    }

    private sealed class OverlayDto
    {
        [JsonPropertyName("interfaces")] public bool Interfaces { get; set; } = true;
        [JsonPropertyName("labels")] public bool Labels { get; set; }
        [JsonPropertyName("frontline")] public bool Frontline { get; set; } = true;
        [JsonPropertyName("violations")] public bool Violations { get; set; } = true;
    }

    // A structural finding from /api/plan/compile — the 422 errors that block a compile (the compile-drawer list).
}
