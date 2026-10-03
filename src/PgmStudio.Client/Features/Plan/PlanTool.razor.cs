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
    protected override void OnInitialized() => active = Phase == "info" ? "info" : "draw";

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
    /// <summary>The floating top-left readout. Its cursor element is handed to the canvas on mount,
    /// which then writes the world position into it directly — per mousemove, far too often to render.</summary>
    private CanvasReadout? readout;
    private IJSObjectReference? handle;
    private DotNetObjectReference<PlanTool>? selfRef;

    // Compile & test drawer: the compiled pair (pretty-printed for preview, raw for the draft chain),
    // structural errors when the compile is blocked, and the walk-test loop's per-step draft state.
    private bool showCompile;
    /// <summary>Whether the first render's load chain has finished. The canvas and the toolbar are in the DOM
    /// before the plan document is — nine interop round-trips and up to four reads separate them — so until
    /// this is set the editor is holding the bridge's blank default, and compiling it would post a plan with
    /// no pieces and be told so. True on /plans/new the moment the chain ends, since a plan drawn from
    /// scratch has nothing to wait for.</summary>
    private bool documentLoaded;

    private bool compiling;
    private string compileTab = PlanTabId;
    private bool copied;
    private string? compiledPlan;                     // the plan document this compile was run on
    private string? compiledLayout, compiledIntent;   // pretty-printed for the preview panes
    private string? compiledLayoutRaw, compiledIntentRaw;   // verbatim, posted to the draft pipeline
    private string? compileError;                     // a malformed / transport failure message
    private List<Finding> compileErrors = [];  // 422 structural findings (compile blocked)
    private List<Finding> compileWarnings = [];  // completeness complaints that did not block the compile

    private string? draftSlug;
    private bool draftBusy;
    private string draftStep = "";
    private string? draftError;

    // What the open map already holds, so the build can tell an origination from a rebuild before it runs.
    // A map with a sketch or a world has downstream work that the build replaces, which is worth saying
    // out loud once rather than discovering afterwards; a plan that has never been built has nothing to
    // lose and gets no interruption. Null until the fetch lands, and on a plan row, where there is no map to
    // ask about.
    private MapState? state;
    private bool confirmingRebuild;

    // The groups whose relief the rebuilt board has no island for, as the layout write refused them (409) —
    // offered back as a choice, since discarding hand-drawn terrain is the author's call and not the build's.
    private IReadOnlyList<string>? orphanedRelief;

    // The sketch-drawn shapes the last rebuild did not keep, as the layout write answered them.
    private IReadOnlyList<string> droppedShapes = [];

    private bool Rebuilds => state is { Artifacts: { } held } && (held.Sketch || held.World);
    private string BuildLabel => Rebuilds ? "Rebuild this map" : MapBacked ? "Build the map" : "Create draft";

    /// <summary>What the drawer's footer button says. <see cref="BuildLabel"/> answers only whether the map is
    /// built, which is the right word for the one state where the button can act; every other state is about
    /// the compile, and naming it is what keeps a disabled control from promising the build it cannot do. A
    /// refusal count points at the findings the drawer already lists above the footer.</summary>
    private string DraftLabel
        => compiling ? "Compiling…"
         : compileErrors.Count > 0
             ? $"Fix {compileErrors.Count} problem{(compileErrors.Count == 1 ? "" : "s")} first"
         : compileError is not null ? "Couldn't compile"
         : compiledLayout is null ? "Compile first"
         : BuildLabel;

    private async Task LoadStateAsync()
    {
        if (!MapBacked) { state = null; return; }
        try { state = await Http.GetFromJsonAsync<MapState>($"api/map/{Slug}/state"); }
        catch { state = null; }   // unreachable / not a map row → treat as a first build, never block one
    }

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

    private async Task<HttpContent> PlanBodyAsync()
        => new StringContent(await handle!.InvokeAsync<string>("exportJson"), Encoding.UTF8, "application/json");
    private bool showOpenDb;
    private bool dbBusy;
    private string? dbError;
    private List<PlanSummary> dbPlans = [];

    // Read-only 3-D height preview: whether the iso view is on, whether it couldn't be shown (so the
    // toggle is disabled), and why — the build's own sentence, or null when WebGL itself is missing.
    private bool threeD;
    private bool isoUnavailable;
    private string? isoUnavailableWhy;

    // The Draw sidebar holds one of three panels — "settings" (the tracing reference), "validation" (the
    // evaluator score + fired rules) or "feasibility" (the producibility read) — switched by the chips at its
    // head, and folds away to give the canvas the width. Each panel's overlay follows its panel being shown.
    private string leftPanel = "settings";
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

    private record RolePalette(string Id, string Label, string Color);

    // The role taxonomy: true pieces (terrain-producing roles) vs technical pieces (non-generating annotations).
    // Both are drawn from the palette; markers (wool/spawn/iron/wall) and the build zone are separate tools.
    private static readonly RolePalette[] GeneratingRoles =
    [
        new("piece", "Piece", "var(--canvas-role-piece)"),
        new("spawn", "Spawn", "var(--canvas-role-spawn)"),
        new("wool-room", "Wool room", "var(--canvas-role-wool-room)"),
    ];
    private static readonly RolePalette[] TechnicalRoles =
    [
        new("buffer", "Buffer", "var(--canvas-role-buffer)"),
    ];
    // Every assignable role, for the inspector's role dropdown (a piece can become any of them).
    private static readonly RolePalette[] Roles = [.. GeneratingRoles, .. TechnicalRoles];

    // The typed box kinds an envelope may carry — the partition vocabulary, offered on the box tool and in the
    // inspector's kind dropdown.
    private static readonly RolePalette[] BoxKinds =
    [
        new("hub", "Hub", "var(--canvas-box-hub)"),
        new("wool", "Wool", "var(--canvas-box-wool)"),
        new("spawn", "Spawn", "var(--canvas-box-spawn)"),
        new("frontline", "Front line", "var(--canvas-box-frontline)"),
        new("mid", "Mid", "var(--canvas-box-mid)"),
    ];

    // The kind armed for the box tool (the last one drawn), mirrored into the bridge.
    private string boxKind = "hub";

    // The kind armed for the zone tool — build (open from the first tick) or water-lane (opens mid-match).
    private string zoneKind = "build";

    private static readonly IReadOnlyList<SelectOption> RoleOptions =
        [.. Roles.Select(role => new SelectOption(role.Id, role.Label))];

    private static readonly IReadOnlyList<SelectOption> BoxKindOptions =
        [.. BoxKinds.Select(kind => new SelectOption(kind.Id, kind.Label))];

    private IReadOnlyList<SelectOption> TraceMapOptions
        => [.. traceMaps.Select(map => new SelectOption(map.Slug, map.Name))];

    private string OffsetLabel => sel?.At is { Length: 2 } a ? $"{a[0]}, {a[1]}" : "";

    /// <summary>The inspector's glyph for a marker kind — the same icon its dock item wears, so the panel
    /// and the tool that placed it read as one thing.</summary>
    private static string MarkerIcon(string kind) =>
        AllMarkerItems.FirstOrDefault(item => item.Key == kind)?.Icon ?? "flag";

    /// <summary>Parse a picked number, keeping the current value when the pick is unreadable.</summary>
    private static int Num(object? value, int fallback)
        => int.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;

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
        state = null;
        showCompile = showOpenDb = false;
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
        [.. GeneratingRoles.Select(r => new DockItem(r.Id, PieceName(r), Swatch: r.Color))];

    private static string PieceName(RolePalette role) => role.Id switch
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
        .. TechnicalRoles.Select(r => new DockItem(r.Id, r.Label, SwatchClass: $"canvas-dock-swatch--{r.Id}")),
    ];

    private static readonly DockItem[] AllMarkerItems =
    [
        new("spawn", "Spawn marker", Icon: "flag"),
        new("wool", "Wool marker", Icon: "square"),
        new("iron", "Iron marker", Icon: "pickaxe"),
        new("destroyable", "Destroyable", Icon: "gem"),
        new("core", "Core", Icon: "flame"),
        new("wall", "Wall", Icon: "brick-wall"),
    ];

    /// <summary>The markers this plan may place. A destroyable is defended by one team and broken by the
    /// others, which only means something when there are two: at four teams PGM treats every goal as shared,
    /// and what that should play like is undecided. So the two are offered for the 2-team symmetries only
    /// (OB14).</summary>
    private DockItem[] MarkerItems => ObjectivesOfferable
        ? AllMarkerItems
        : [.. AllMarkerItems.Where(m => m.Key is not ("destroyable" or "core"))];

    private static DockItem[] BoxItems =>
        [.. BoxKinds.Select(k => new DockItem(k.Id, $"{k.Label} box",
                                              Swatch: k.Color, SwatchClass: "canvas-dock-swatch--box"))];

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

    // ── 3-D height preview ─────────────────────────────────────────────────

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

    private Task OnPieceRole(string role)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setPieceRole", sel.Id, role).AsTask() : Task.CompletedTask;

    private Task StepSurface(int delta)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("stepPieceSurface", sel.Id, delta).AsTask() : Task.CompletedTask;

    private Task ToggleMirrors()
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("togglePieceMirrors", sel.Id).AsTask() : Task.CompletedTask;

    private Task OnZoneId(ChangeEventArgs e)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setZoneId", sel.Id, e.Value?.ToString() ?? "").AsTask() : Task.CompletedTask;

    private Task OnBoxId(ChangeEventArgs e)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setBoxId", sel.Id, e.Value?.ToString() ?? "").AsTask() : Task.CompletedTask;

    private Task OnBoxKind(string kind)
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("setBoxKind", sel.Id, kind).AsTask() : Task.CompletedTask;

    private Task ToggleBoxMembers()
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("toggleBoxMembers", sel.Id).AsTask() : Task.CompletedTask;

    private Task CycleFacing()
        => sel is not null && handle is not null ? handle.InvokeVoidAsync("cycleFacing", sel.Index).AsTask() : Task.CompletedTask;

    // ── objective markers: the structure each one builds ─────────────────────────────────────────
    //
    // A core and a destroyable are placed as a bare marker and take the generator's defaults; these are the
    // knobs that vary them. The plan is where they belong, because a plan states the gameplay against the
    // terrain it also lays down — the marker's piece is the ground its structure floats over.
    //
    // What is stored is only what differs. Setting a field back to its default passes null, which removes
    // the key, so a plan the author never varied stays the bare markers it was written as and a default that
    // later moves moves for every plan that never disagreed with it.

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

    private Task SetMarkerField(string key, object? value)
        => sel is not null && handle is not null
            ? handle.InvokeVoidAsync("setMarkerField", sel.MarkerKind, sel.Index, key, value).AsTask()
            : Task.CompletedTask;

    /// <summary>Store a number, or clear it when it equals the default it would fall back to anyway.</summary>
    private Task SetMarkerNumber(string key, int value, int fallback)
        => SetMarkerField(key, value == fallback ? null : value);

    private Task SetMarkerText(string key, string? value, string fallback)
        => SetMarkerField(key, string.IsNullOrWhiteSpace(value) || value.Trim() == fallback ? null : value.Trim());

    // ── the effective value of each knob: what the author set, else what the generator will use ──
    private string DestroyableStyle => sel?.Style ?? vocabulary.Destroyable.Style;
    private string DestroyableMaterials => sel?.Materials ?? vocabulary.Destroyable.Materials;
    private int DestroyableFloat => sel?.Float ?? vocabulary.Destroyable.Float;

    private int CoreLava => sel?.Lava ?? vocabulary.Core.Lava;
    private int CoreLavaHeight => sel?.LavaHeight ?? vocabulary.Core.LavaHeight;

    /// <summary>The obsidian the two stated numbers imply, so the author reads the structure they are
    /// building rather than the interior alone.</summary>
    private string CoreCasingReadout
    {
        get
        {
            var (size, height) = CoreCasing.Of(CoreLava, CoreLavaHeight, CoreOpenTop);
            return $"{size}×{size}×{height} obsidian, {CoreLava}×{CoreLava}×{CoreLavaHeight} lava inside";
        }
    }
    /// <summary>The dye a wool marker states, or empty where it states none. Unlike the other knobs there is
    /// no default to fall back to: an unstated colour is resolved at compile time against the marker's team and
    /// the wools before it, which the editor cannot know from one marker.</summary>
    private string WoolColor => sel?.Color ?? "";

    private IReadOnlyList<SelectOption> WoolColorOptions
        => [.. vocabulary.Wool.Colors.Select(dye => new SelectOption(dye.Name, dye.Label))];

    private IReadOnlyList<SelectOption> DestroyableStyleOptions
        => [.. vocabulary.Destroyable.Styles.Select(design => new SelectOption(design, design))];

    private IReadOnlyList<SelectOption> DestroyableMaterialOptions
        => [.. vocabulary.Destroyable.MaterialChoices.Select(material => new SelectOption(material, material))];

    private IReadOnlyList<SelectOption> LavaOptions
        => [.. vocabulary.Core.LavaRange.Select(size => new SelectOption(size.ToString(), $"{size} × {size}"))];

    private IReadOnlyList<SelectOption> LavaHeightOptions
        => [.. vocabulary.Core.LavaHeightRange.Select(height => new SelectOption(height.ToString(), height.ToString()))];

    /// <summary>The swatch beside the picker: the stated dye's own colour, or the neutral the auto option
    /// stands for, since no one colour is what "auto" resolves to.</summary>
    private string WoolSwatch
        => vocabulary.Wool.Colors.FirstOrDefault(c => c.Name == WoolColor)?.Hex ?? "var(--border)";

    private int CoreFloat => sel?.Float ?? vocabulary.Core.Float;
    private int CoreLeak => sel?.Leak ?? vocabulary.Core.Leak;
    private bool CoreOpenTop => sel?.OpenTop ?? vocabulary.Core.OpenTop;

    /// <summary>How far players must dig under the casing before its lava can leak — the whole point of the
    /// float/leak pair, which says nothing when either is read alone.</summary>
    private int CoreDigDepth => CoreDig.Depth(CoreLeak, CoreFloat);

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
                var outcome = await Document.PutAsync($"api/map/{Slug}/plan", PlanBodyAsync);
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

    // ── compile & test (the walk-test loop) ──────────────────────────────────────

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private async Task OpenCompile()
    {
        showCompile = true;
        confirmingRebuild = false;
        await LoadStateAsync();   // re-read on open: a build in this session changes the answer
        await Compile();
    }

    private void CloseCompile() { showCompile = false; confirmingRebuild = false; }

    // A finding can point at what it is about only if it named something: a rule about the plan as a whole
    // (or one whose subject the canvas cannot draw) has nothing to show.
    private static bool HasSubjects(Finding finding) => finding.SubjectIds.Count > 0;

    private static string? ShowFindingTitle(Finding finding)
        => HasSubjects(finding) ? "Show on the canvas" : null;

    // Click a compile finding to see what it is about. A finding names its subjects — pieces, zones, markers —
    // and the canvas can pulse them, but the compile drawer is modal and dims the board behind it, so the
    // drawer closes first: the click means "show me", and the answer is on the canvas rather than in the list.
    private async Task ShowFinding(Finding finding)
    {
        if (handle is null || !HasSubjects(finding)) return;
        CloseCompile();
        // Render the closed drawer before the pulse starts, so the whole 1.6s ramp plays on a board the
        // author can actually see rather than beginning under the backdrop.
        StateHasChanged();
        await Task.Yield();
        await handle.InvokeVoidAsync("highlightSubjects", JsonSerializer.Serialize(finding.SubjectIds));
    }

    // The build button. On a map that already holds a sketch or a world it asks first, because the same
    // click means two different things — originating the map, or replacing a board someone has since been
    // working on — and only the second is worth a sentence.
    private async Task BuildRequested()
    {
        if (Rebuilds && !confirmingRebuild) { confirmingRebuild = true; return; }
        confirmingRebuild = false;
        await CreateDraft();
    }

    private void CancelRebuild() => confirmingRebuild = false;

    private void KeepRelief() => orphanedRelief = null;

    // The author accepted the loss the layout write refused over: the same build, with ?force=true.
    private async Task DiscardReliefAndRebuild()
    {
        orphanedRelief = null;
        await CreateDraft(discardRelief: true);
    }

    // Post the current plan to /api/plan/compile. A 422 renders its structural findings in place of the JSON;
    // a 400 (malformed) / transport failure shows a message. A 200 stores the compiled pair for preview + the
    // draft chain. Compiling resets any prior draft so a fresh compile starts the loop over.
    private async Task Compile()
    {
        if (handle is null) return;
        compiling = true;
        compileError = null;
        compileErrors = [];
        compileWarnings = [];
        compiledPlan = compiledLayout = compiledIntent = compiledLayoutRaw = compiledIntentRaw = null;
        compileTab = PlanTabId;
        draftSlug = null; draftError = null; draftBusy = false;
        orphanedRelief = null; droppedShapes = [];
        StateHasChanged();

        try
        {
            var planJson = await handle.InvokeAsync<string>("exportJson");
            // Kept whatever the compile answers: the plan is the one file that exists either way, and a plan
            // that will not compile is the one an author most needs a copy of.
            compiledPlan = Reindent(planJson);
            using var resp = await Http.PostAsync("api/plan/compile", new StringContent(planJson, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                // The compiled pair and what the compile remarked on come out of one read: `warnings` is a
                // member of the success rather than a wrapper around it, and the body is a stream.
                var (compiled, warnings) = await ServerWarnings.AnsweredAsync<CompiledPlanDto>(resp);
                compiledLayoutRaw = compiled?.Layout.GetRawText();
                compiledIntentRaw = compiled?.Intent.GetRawText();
                compiledLayout = compiled is null ? null : JsonSerializer.Serialize(compiled.Layout, Pretty);
                compiledIntent = compiled is null ? null : JsonSerializer.Serialize(compiled.Intent, Pretty);
                compileWarnings = [.. warnings];
                compileTab = LayoutTabId;
            }
            else if ((int)resp.StatusCode == 422)
            {
                var refusal = await resp.Content.ReadFromJsonAsync<RefusalDto>();
                compileErrors = [.. refusal?.Findings ?? []];
            }
            else
            {
                compileError = $"Couldn't compile the plan (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
            }
        }
        catch (Exception ex) { compileError = ex.Message; }
        compiling = false;
        StateHasChanged();
    }

    // ── the drawer's files ───────────────────────────────────────────────────────
    // Every file a plan can be read out of is offered from the one drawer: the plan document, and the pair
    // it compiled into once there is one. The tab id doubles as the downloaded file's middle extension
    // (`<name>.plan.json`, `.layout.json`, `.intent.json`).

    private const string PlanTabId = "plan";
    private const string LayoutTabId = "layout";
    private const string IntentTabId = "intent";

    private IEnumerable<(string Id, string Label)> CompileTabs
    {
        get
        {
            yield return (PlanTabId, "Plan");
            if (compiledLayout is null) yield break;
            yield return (LayoutTabId, "Layout");
            yield return (IntentTabId, "Game settings");
        }
    }

    private string? TabText => compileTab switch
    {
        LayoutTabId => compiledLayout,
        IntentTabId => compiledIntent,
        _ => compiledPlan,
    };

    /// <summary>The plan document as the panes show every other file — the bridge exports it compact.</summary>
    private static string Reindent(string json)
    {
        try { return JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Pretty); }
        catch (JsonException) { return json; }
    }

    private async Task CopyTab()
    {
        if (TabText is not { } text) return;
        copied = await JS.InvokeAsync<bool>("studio.copyText", text);
        StateHasChanged();
    }

    private async Task DownloadTab()
    {
        if (TabText is not { } text) return;
        var slug = string.IsNullOrWhiteSpace(planName) ? "plan" : planName.Trim().ToLowerInvariant().Replace(' ', '-');
        await JS.InvokeVoidAsync("studio.downloadText", $"{slug}.{compileTab}.json", text, "application/json");
    }

    // Drive the draft pipeline from a successful compile: push the compiled layout, rasterize it, then push
    // the compiled intent. Any non-2xx step aborts with a message naming that step.
    //
    // A map-backed plan builds onto its OWN row: one map carries plan → sketch → configure, keeps its plan
    // blob beside the layout it compiled into, and re-running refreshes it in place instead of leaving a
    // trail of near-identical maps. The intent write carries the plan's name, so the map's identity follows
    // the plan without a second call. A plan row has no map, so building one originates the map: there the
    // build IS the map's creation.
    //
    // Either way the plan itself is written to the map first, so the map carries the document its layout was
    // compiled from and opens in the plan editor on the board it was built from.
    //
    // Both writes go through their from-plan route rather than the plain PUT, for the same reason: a
    // compiled pair states what the plan states and nothing else, so a straight replace deleted everything
    // the map had accumulated on top. The layout carries its finish across (SketchLayout.CarryFinish — the
    // themes, room shells and dressing) and the intent carries its authored slices (IntentCarry — the
    // authors, island team assignments and confirmed symmetry). Rebuilding changes the board and the
    // structure the plan describes; it is not an answer to anything else about the map.
    private async Task CreateDraft(bool discardRelief = false)
    {
        if (handle is null || compiledLayoutRaw is null || compiledIntentRaw is null) return;
        draftBusy = true; draftError = null; draftSlug = null; orphanedRelief = null; droppedShapes = [];

        try
        {
            var slug = Slug;
            if (!MapBacked)
            {
                draftStep = "Creating draft"; StateHasChanged();
                using var createResp = await Http.PostAsJsonAsync("api/sketch", new { name = planName });
                if (!await Ok(createResp, "create the draft")) return;
                slug = (await createResp.Content.ReadFromJsonAsync<OriginatedDto>())?.Slug;
                if (string.IsNullOrEmpty(slug)) { draftError = "Couldn't create the draft. The server didn't return its name."; return; }
            }

            draftStep = "Saving the plan"; StateHasChanged();
            if (MapBacked)
            {
                var outcome = await Document.PutAsync($"api/map/{slug}/plan", PlanBodyAsync);
                if (!outcome.Landed) { draftError = outcome.Message; return; }
            }
            else
            {
                using var planResp = await Http.PutAsync($"api/map/{slug}/plan", await PlanBodyAsync());
                if (!await Ok(planResp, "save the plan")) return;
            }

            draftStep = "Saving the layout"; StateHasChanged();
            using var layoutResp = await Http.PutAsync(
                discardRelief ? $"api/map/{slug}/sketch/from-plan?force=true" : $"api/map/{slug}/sketch/from-plan",
                new StringContent(compiledLayoutRaw, Encoding.UTF8, "application/json"));
            // A 409 here is relief the rebuilt board has no island for, one finding per group: asked back
            // rather than reported as a failure, since the way through is a choice the author makes.
            if (layoutResp.StatusCode == System.Net.HttpStatusCode.Conflict
                && await layoutResp.Content.ReadFromJsonAsync<RefusalDto>() is { } refusal
                && refusal.Findings.SelectMany(finding => finding.SubjectIds).ToList() is { Count: > 0 } groups)
            {
                orphanedRelief = groups;
                return;
            }
            if (!await Ok(layoutResp, "save the layout")) return;
            droppedShapes = (await layoutResp.Content.ReadFromJsonAsync<SketchFromPlanDto>())?.Dropped ?? [];

            draftStep = "Building the world"; StateHasChanged();
            using var finishResp = await Http.PostAsync($"api/map/{slug}/sketch/finish", null);
            if (!await Ok(finishResp, "build the world")) return;

            draftStep = "Applying game settings"; StateHasChanged();
            using var intentResp = await Http.PutAsync($"api/map/{slug}/intent/from-plan", new StringContent(compiledIntentRaw, Encoding.UTF8, "application/json"));
            if (!await Ok(intentResp, "apply the game settings")) return;

            draftSlug = slug;
            await LoadStateAsync();   // the map now holds a sketch and a world — the next build is a rebuild
        }
        catch (Exception ex) { draftError = ex.Message; }
        finally { draftBusy = false; StateHasChanged(); }
    }

    private async Task<bool> Ok(HttpResponseMessage resp, string step)
    {
        if (resp.IsSuccessStatusCode) return true;
        draftError = $"Couldn't {step} (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
        return false;
    }

    // Save the draft's world export, or say why it was refused.
    private async Task DownloadWorld()
    {
        if (draftSlug is null) return;
        draftError = await MapDownload.SaveAsync(Http, JS, draftSlug);
        StateHasChanged();
    }

    private static string Trunc(string s) => s.Length > 200 ? s[..200] + "…" : s;

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

    /// <summary>The bridge couldn't show the read-only 3-D preview; fall back to 2-D and disable the toggle.
    /// <paramref name="reason"/> is empty when WebGL itself is missing and the build's own sentence when the
    /// board would not build — two different things to do about it, so the note says which.</summary>
    [JSInvokable]
    public void OnIsoUnavailable(string? reason)
    {
        threeD = false;
        isoUnavailable = true;
        isoUnavailableWhy = string.IsNullOrWhiteSpace(reason) ? null : reason;
        StateHasChanged();
    }

    /// <summary>The chip beside the toggle: what stopped the preview, in two words.</summary>
    private string IsoNote => isoUnavailableWhy is null ? "No WebGL" : "3-D unavailable";

    /// <summary>The whole sentence, on hover.</summary>
    private string IsoNoteTitle => isoUnavailableWhy
        ?? "The 3-D preview needs WebGL, which this browser doesn't support.";

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
        // Objective-marker structure fields. Null means the author never set it, so the inspector shows the
        // generator's default — a marker states only what it varies.
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("style")] public string? Style { get; set; }
        [JsonPropertyName("materials")] public string? Materials { get; set; }
        [JsonPropertyName("lava")] public int? Lava { get; set; }
        [JsonPropertyName("lavaHeight")] public int? LavaHeight { get; set; }
        [JsonPropertyName("float")] public int? Float { get; set; }
        [JsonPropertyName("leak")] public int? Leak { get; set; }
        [JsonPropertyName("openTop")] public bool? OpenTop { get; set; }
        /// <summary>The building on a role piece, <c>[x, z, w, h]</c> in blocks from the piece's minimum
        /// corner, on a <c>footprint</c> selection.</summary>
        [JsonPropertyName("footprint")] public double[]? Footprint { get; set; }
        [JsonPropertyName("color")] public string? Color { get; set; }
        [JsonPropertyName("boxKind")] public string BoxKind { get; set; } = "";
        [JsonPropertyName("zoneKind")] public string ZoneKind { get; set; } = "";
        [JsonPropertyName("members")] public List<string>? Members { get; set; }
        [JsonPropertyName("membersNamed")] public bool MembersNamed { get; set; }
        /// <summary>How many pieces and zones a <c>multi</c> selection holds.</summary>
        [JsonPropertyName("count")] public int Count { get; set; }
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
