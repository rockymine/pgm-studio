using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchTool
{
    [Parameter] public string Slug { get; set; } = "";

    private ElementReference svgRef, wrapRef;
    /// <summary>The floating top-left readout. Its three elements are handed to the canvas on mount, which
    /// then writes cursor / size / zoom into them directly — per mousemove, far too often to render.</summary>
    private CanvasReadout? readout;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SketchTool>? selfRef;

    private string tool = "move";
    private string op = "add";
    private string mode = "rot_180";
    private double centerX = 0;     // symmetry centre
    private double centerZ = 0;
    private bool mirrorOn = true;
    private bool shapesOn = false;
    private bool chunksOn = true;
    private bool blocksOn = false;   // S23: the rasterized block-footprint preview
    private bool reliefOn = false;   // the height contours of whatever relief the groups carry
    private bool snapOn = true;
    private bool threeD = false;
    private bool isoUnavailable = false;   // 3-D preview couldn't be shown (no WebGL, or the build refused)
    private string? isoUnavailableWhy;     // the build's own sentence; null when WebGL itself is missing
    private string groupLabel = "";
    private bool canUndo, canRedo;
    /// <summary>The theme in hand while the Apply step is up. The canvas paints it on a click and can lift
    /// another into it, so the tool holds it and the rail reads it.</summary>
    private string themeBrush = "";

    // ── Phases (rail): Info (Identity + Settings steps) · Draw (the canvas). Draw stays mounted while
    //    Info is up (hidden, not torn down) so the drawing state + zoom survive the trip. ──
    [SupplyParameterFromQuery] public string? Phase { get; set; }
    private string active = "draw";
    private bool InfoActive => active == "info";
    private bool DrawActive => active == "draw";
    private Task GoInfo() => SetPhase("info");
    private Task GoDraw() => SetPhase("draw");

    // ── Theme phase: the map's whole finish, in one step on the live canvas. A theme is taken in hand from the
    //    strip and put on a shape by clicking it; the inspector shows what is in hand, what the selection
    //    carries, and — with nothing selected — what the board falls back to, the room shells among it.
    //    Authoring a theme is the library's, so the phase picks and places rather than defining. ──
    private bool ThemeActive => active == "theme";
    private Task GoTheme() { tool = "select"; return SetPhase("theme"); }

    /// <summary>The board's theme ids in registry order, its map default, and which shape carries which — read
    /// from the bridge once per change and handed to the strip and the inspector, so the two views of one
    /// registry cannot disagree about it.</summary>
    private List<string> themeIds = [];
    private string mapThemeId = "";
    private Dictionary<string, string> shapeThemes = [];
    /// <summary>Bumped on every registry change, so a view keyed on a theme's name refreshes when the theme
    /// under that name is replaced.</summary>
    private int themeRevision;
    /// <summary>How many shapes the whole board carries, over every layer — the denominator the themed count
    /// is read against, and counted where that count is, so the two cannot be over different sets.</summary>
    private int themedShapeTotal;
    /// <summary>Whether the inspector is showing the add-from-library panel; the strip's + toggles it.</summary>
    private bool themeAddOpen;

    private async Task ReadThemes()
    {
        if (handle is null) return;
        ApplyThemes(await handle.InvokeAsync<string>("getThemes"));
    }

    private void ApplyThemes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        themeIds = root.TryGetProperty("themes", out var themes) && themes.ValueKind == JsonValueKind.Object
            ? [.. themes.EnumerateObject().Select(p => p.Name)] : [];
        mapThemeId = root.TryGetProperty("mapTheme", out var mt) && mt.ValueKind == JsonValueKind.String
            ? mt.GetString() ?? "" : "";
        shapeThemes = [];
        if (root.TryGetProperty("shapeThemes", out var assigned) && assigned.ValueKind == JsonValueKind.Object)
            foreach (var p in assigned.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String) shapeThemes[p.Name] = p.Value.GetString() ?? "";
        themedShapeTotal = root.TryGetProperty("shapeCount", out var count) && count.ValueKind == JsonValueKind.Number
            ? count.GetInt32() : 0;
    }

    /// <summary>Step the theme in hand through the registry, empty hand included — so one pair of keys reaches
    /// every theme however many there are, and putting one down is a step like any other. Answers only in the
    /// phase that hands a brush out: a brush armed anywhere else would make a click paint where it selects.</summary>
    private Task CycleTheme(int by)
    {
        if (!ThemeActive || themeIds.Count == 0) return Task.CompletedTask;
        var ring = themeIds.Count + 1;                       // the registry, plus the empty hand
        var at = themeBrush.Length == 0 ? 0 : themeIds.IndexOf(themeBrush) + 1;
        var next = ((at + by) % ring + ring) % ring;
        return SetThemeBrush(next == 0 ? "" : themeIds[next - 1]);
    }

    // ── Dressing phase (decoration.md) ──
    // One step, not two. Dressing has nothing to define up front: every part of it is a thing put somewhere,
    // so the phase is the canvas with its own placing tools and an inspector for whatever is under the cursor.
    // The Theme phase keeps its create/apply split because a theme genuinely is a recipe authored once.
    private bool DressingActive => active == "dressing";
    private string dressingJson = "";
    private Task GoDressing() { tool = DressingTools.Tree; return SetPhase("dressing"); }

    // ── Relief phase (docs/world-export/relief.md §15) ──
    // One step, like Dressing and for the same reason: every part of a relief is a thing stated somewhere, so
    // the phase is the canvas with its own tools and an inspector for whatever is under the cursor. It sits
    // between Draw and Theme because a relief is geometry — it changes what the rasterizer emits — so it has
    // to precede the two passes that read the built surface.
    private bool ReliefActive => active == "relief";
    private string reliefJson = "";
    // Bumped on every relief change so a reading already on screen knows it is describing terrain that has
    // since moved — a stale measurement reads as current, which is worse than none.
    private int reliefRevision;
    private Task GoRelief() { tool = ReliefTools.Point; return SetPhase("relief"); }

    /// <summary>Whether the canvas is being used to place a scope rather than to draw — today only Theme,
    /// which selects shapes it does not edit.</summary>
    private bool ScopeApplyActive => ThemeActive;

    /// <summary>The tools that make a shape, and so the only ones the operation decides anything for. Measure
    /// reads, split cuts what is already there, and move/select do not draw at all — with one of those armed
    /// the operation is set but idle, which is what the pill dims to say.</summary>
    private static readonly string[] DrawTools = ["rectangle", "polygon", "lasso"];

    private bool DrawToolActive => DrawTools.Contains(tool);

    private bool Carving => op == "subtract";

    // The button already prints the mode it is in, so its tooltip has only the other half to give: what the
    // click does. Naming the destination and not the state keeps it to two words.
    private string OpTitle => Carving ? "Switch to Build" : "Switch to Carve";

    private Task ToggleOperation() => SetOperation(Carving ? "add" : "subtract");

    /// <summary>The colour the draw group wears — the same fill the canvas gives a shape of that operation,
    /// so the palette states what the drawing will look like. The group fades itself (DockGroup.Idle) when
    /// the armed tool draws nothing; the colour is the mode either way.</summary>
    private string OpAccent => Carving ? "var(--canvas-sub-fill)" : "var(--canvas-add-fill)";

    // The same toggle shows the bare voxelization while drawing and the paint on top of it once theming.
    private string BlocksChipTitle => ScopeApplyActive
        ? "Show the blocks the export places — the rasterized footprint painted by its themes"
        : "Show the rasterized block footprint — the exact cells the shapes voxelize into";

    // The shapes the current selection themes: a group's members, else the single selected shape, else none.
    private IReadOnlyList<string> ScopeTargetShapeIds =>
        selectedGroupId is not null ? (SelectedGroup?.ShapeIds ?? (IReadOnlyList<string>)[])
        : selectedShapeId is not null ? new[] { selectedShapeId }
        : [];

    // A freshly-created sketch lands on Info (?phase=info) to name it; opening an existing one goes
    // straight to Draw.
    protected override void OnInitialized() { if (Phase == "info") active = "info"; }

    // Switching phases only flips which body renders: the canvas observes its own wrap and re-measures
    // (and runs a deferred fit) when Draw un-hides. Nudging it from here would run against the still-
    // hidden DOM — this method returns before the phase div is re-rendered.
    private async Task SetPhase(string p)
    {
        active = p;
        await PushCanvasMode(p);
    }

    // What the canvas is in a given phase, pushed from the one place a phase changes so no route in or out can
    // leave the wrong mode behind. Editing geometry belongs to Draw alone, so every other phase gets the canvas
    // as a selection surface; and only Theme previews the finished paint, because while the shapes are still
    // being drawn the Blocks overlay should show the voxelization it is there to show, not a finishing pass.
    private async Task PushCanvasMode(string phase)
    {
        if (handle is null) return;
        // Geometry is Draw's alone. Theme assigns paint to it and Relief states ground inside it, and both
        // reach a group by picking one — so in both the canvas picks and never edits, or the gesture that
        // selects a group is also the gesture that reshapes it. Dressing places props rather than shapes,
        // so it is not select-only in that sense: its own tools are armed and the shape tools are simply not
        // offered.
        await handle.InvokeVoidAsync("setSelectOnly", phase is "theme" or "relief");
        // Both finishing phases show the paint: Theme is authoring it, and Dressing is placing things on it,
        // which is a judgement about the finish as much as about the planting.
        await handle.InvokeVoidAsync("setPaintPreview", phase is "theme" or "dressing");
        await handle.InvokeVoidAsync("setDressingMode", phase == "dressing");
        // Entering Relief turns the contour overlay on with it: the phase shows the statement and the surface
        // it produced at once, which is the only way a mark can be tuned by eye. Leaving does not turn it off
        // again — the chip is the author's, and a contour view they asked for should survive a phase change.
        await handle.InvokeVoidAsync("setReliefMode", phase == "relief");
        // What a plain click picks. A group is the unit in Draw, where a landmass is what moves, and in
        // Relief, where one relief is solved per group. In Theme the job is naming one shape, so a click
        // picks the shape and the group is reached with Alt or from the tree.
        await handle.InvokeVoidAsync("setPickUnit", phase == "theme" ? "shape" : "group");
        // A brush and the panel that fills it only exist while the phase that hands one out is up.
        if (phase != "theme") { themeAddOpen = false; await SetThemeBrush(""); }
        if (phase == "relief") reliefOn = true;
        if (phase == "theme") await ReadThemes();
        await PushPhaseOverlays(phase);
    }

    // ── the overlays a phase offers, and which of them it turns on as it is entered ──
    // A phase shows the layer it works on and the layer it works against. An overlay that would draw a fact
    // another shown layer already carries is not offered at all: the contour layer is the relief without the
    // blocks, so wherever the blocks are shown it states the same thing twice. Snap is not among these — it
    // modifies a drag rather than showing anything, so it sits in the dock beside the tools that drag.
    private const string ChipShapes = "shapes";
    private const string ChipMirror = "mirror";
    private const string ChipChunks = "chunks";
    private const string ChipBlocks = "blocks";
    private const string ChipRelief = "relief";

    /// <summary>What a phase puts in the layer bar, and which of those it switches on as it is entered.</summary>
    private sealed record PhaseOverlay(string[] Offered, string[] On);

    private static readonly Dictionary<string, PhaseOverlay> Overlays = new()
    {
        ["info"]     = new([ChipShapes, ChipMirror, ChipChunks, ChipBlocks], []),
        ["draw"]     = new([ChipShapes, ChipMirror, ChipChunks, ChipBlocks], []),
        // The contour layer comes on with the phase's canvas mode, so it is offered here but not pushed again.
        ["relief"]   = new([ChipRelief, ChipShapes, ChipMirror, ChipChunks, ChipBlocks], [ChipShapes]),
        ["theme"]    = new([ChipBlocks, ChipShapes, ChipMirror, ChipChunks], [ChipBlocks, ChipShapes]),
        ["dressing"] = new([ChipBlocks, ChipShapes, ChipMirror, ChipChunks], [ChipBlocks, ChipShapes]),
    };

    private static PhaseOverlay OverlaysOf(string phase) => Overlays.GetValueOrDefault(phase, Overlays["draw"]);

    /// <summary>Whether this phase puts a chip in the layer bar at all.</summary>
    private bool ChipOffered(string chip) => OverlaysOf(active).Offered.Contains(chip);

    /// <summary>Switch on what the phase being entered works on. What it does not name is left as the author
    /// had it: a chip they switched by hand answers a question they asked, and arriving in a phase is not a
    /// reason to forget the answer.</summary>
    private async Task PushPhaseOverlays(string phase)
    {
        if (handle is null) return;
        var on = OverlaysOf(phase).On;
        if (on.Contains(ChipShapes) && !shapesOn) { shapesOn = true; await handle.InvokeVoidAsync("setShapesVisible", true); }
        if (on.Contains(ChipBlocks) && !blocksOn) { blocksOn = true; await handle.InvokeVoidAsync("setBlocksVisible", true); }
    }

    // Layout pushed from the bridge (OnLayout) + the current selection (OnShapeSelected/OnGroupSelected).
    private List<SketchGroupRow> groups = [];
    private List<SketchShapeRow> shapes = [];
    private List<SketchLayerRow> layerRows = [];
    private string? activeLayerId;
    private string? selectedShapeId;
    private string? selectedGroupId;
    private int selectedVertexIdx = -1;
    private double selectedVertexHeight;
    private List<SketchSlopeControl> slopeControls = [];   // shift-marked surface-slope controls (2–3)

    private SketchShapeRow? SelectedShape => shapes.FirstOrDefault(s => s.Id == selectedShapeId);
    private SketchGroupRow? SelectedGroup => groups.FirstOrDefault(i => i.Id == selectedGroupId);

    private Task Undo() => handle?.InvokeVoidAsync("undo").AsTask() ?? Task.CompletedTask;
    private Task Redo() => handle?.InvokeVoidAsync("redo").AsTask() ?? Task.CompletedTask;
    private Task ShowKeys() => JS.InvokeVoidAsync("studio.showKeys").AsTask();

    /// <summary>The brush the canvas lifted, or the one it was handed back.</summary>
    [JSInvokable]
    public void OnThemeBrush(string id)
    {
        themeBrush = id ?? "";
        StateHasChanged();
    }

    /// <summary>Take a theme in hand from the strip, or put down the one already held by taking it again.</summary>
    private Task TakeTheme(string id) => SetThemeBrush(id == themeBrush ? "" : id);

    private async Task SetThemeBrush(string id)
    {
        themeBrush = id ?? "";
        if (handle is not null) await handle.InvokeVoidAsync("setThemeBrush", themeBrush);
        StateHasChanged();
    }

    /// <summary>What the undo stack can do now, so the tool can say so.</summary>
    [JSInvokable]
    public void OnHistory(bool undo, bool redo)
    {
        (canUndo, canRedo) = (undo, redo);
        StateHasChanged();
    }

    /// <summary>A chord this tool registered. The registry holds the label and the group; this holds what the
    /// chord does, so the two halves of a binding sit in the one language each is written in.</summary>
    [JSInvokable]
    public async Task OnShortcut(string id)
    {
        switch (id)
        {
            case "sketch.phase.info": await GoInfo(); break;
            case "sketch.phase.draw": await GoDraw(); break;
            case "sketch.phase.relief": await GoRelief(); break;
            case "sketch.phase.theme": await GoTheme(); break;
            case "sketch.phase.dressing": await GoDressing(); break;
            case "sketch.tool.select": await SetTool("select"); break;
            case "sketch.tool.move": await SetTool("move"); break;
            case "sketch.tool.rectangle": await SetTool("rectangle"); break;
            case "sketch.tool.polygon": await SetTool("polygon"); break;
            case "sketch.tool.lasso": await SetTool("lasso"); break;
            case "sketch.tool.measure": await SetTool("measure"); break;
            case "sketch.tool.split": await SetTool("split"); break;
            case "sketch.op": await ToggleOperation(); break;
            case "sketch.fit": await OnFit(); break;
            case "sketch.chip.shapes": await ToggleShapes(); break;
            case "sketch.chip.mirror": await ToggleMirror(); break;
            case "sketch.chip.chunks": await ToggleChunks(); break;
            case "sketch.chip.blocks": await ToggleBlocks(); break;
            case "sketch.chip.relief": await ToggleRelief(); break;
            case "sketch.chip.snap": await ToggleSnap(); break;
            case "sketch.theme.next": await CycleTheme(1); break;
            case "sketch.theme.prev": await CycleTheme(-1); break;

            case "sketch.save": await Finish(); break;
        }
        StateHasChanged();
    }

    /// <summary>Every chord this tool answers. `label` and `group` are what the `?` sheet and the command
    /// palette render, and the registry refuses an entry without them.</summary>
    private static readonly object[] Shortcuts =
    [
        new { id = "sketch.phase.info",      keys = "1", label = "Go to Info",     group = "Phases" },
        new { id = "sketch.phase.draw",      keys = "2", label = "Go to Draw",     group = "Phases" },
        new { id = "sketch.phase.relief",    keys = "3", label = "Go to Relief",   group = "Phases" },
        new { id = "sketch.phase.theme",     keys = "4", label = "Go to Theme",    group = "Phases" },
        new { id = "sketch.phase.dressing",  keys = "5", label = "Go to Dressing", group = "Phases" },
        new { id = "sketch.tool.select",     keys = "v", label = "Select",  group = "Tools" },
        new { id = "sketch.tool.move",       keys = "h", label = "Pan",     group = "Tools" },
        new { id = "sketch.tool.rectangle",  keys = "r", label = "Rectangle", group = "Tools" },
        new { id = "sketch.tool.polygon",    keys = "p", label = "Polygon", group = "Tools" },
        new { id = "sketch.tool.lasso",      keys = "l", label = "Lasso",   group = "Tools" },
        new { id = "sketch.tool.measure",    keys = "m", label = "Measure", group = "Tools" },
        new { id = "sketch.tool.split",      keys = "x", label = "Split",   group = "Tools" },
        new { id = "sketch.op",              keys = "b", label = "Flip build ⇄ carve", group = "Tools" },
        new { id = "sketch.fit",             keys = "f", label = "Fit the working bounds", group = "Canvas" },
        new { id = "sketch.chip.shapes",     keys = "alt+1", label = "Show every shape",   group = "Overlays" },
        new { id = "sketch.chip.mirror",     keys = "alt+2", label = "Show the mirror",    group = "Overlays" },
        new { id = "sketch.chip.chunks",     keys = "alt+3", label = "Show the chunk grid", group = "Overlays" },
        new { id = "sketch.chip.blocks",     keys = "alt+4", label = "Show the blocks",    group = "Overlays" },
        new { id = "sketch.chip.relief",     keys = "alt+5", label = "Show the contours",  group = "Overlays" },
        new { id = "sketch.chip.snap",       keys = "alt+6", label = "Snap while dragging", group = "Tools" },
        new { id = "sketch.theme.next",      keys = "]", label = "Take the next theme in hand",     group = "Theme" },
        new { id = "sketch.theme.prev",      keys = "[", label = "Take the previous theme in hand", group = "Theme" },
        new { id = "sketch.save",            keys = "mod+s", label = "Save the sketch", group = "Everywhere", inField = true },
    ];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>(
            "studio.mountSketch", svgRef, wrapRef, readout!.Cursor, readout.Zoom, readout.Size, selfRef, Slug);
        // Restore the saved layout (empty {} for a fresh sketch); the bridge handles an empty state.
        try
        {
            var state = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/sketch");
            await handle.InvokeVoidAsync("load", state);
            // Sync the Setup controls with the loaded setup (the canvas already uses it).
            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("setup", out var su)
                && su.ValueKind == JsonValueKind.Object)
            {
                if (su.TryGetProperty("mirror_mode", out var mm) && mm.GetString() is { Length: > 0 } m) mode = m;
                if (su.TryGetProperty("center", out var ce) && ce.ValueKind == JsonValueKind.Object)
                {
                    if (ce.TryGetProperty("cx", out var cxv)) centerX = cxv.GetDouble();
                    if (ce.TryGetProperty("cz", out var czv)) centerZ = czv.GetDouble();
                }
                StateHasChanged();
            }
        }
        catch { /* no saved layout / map not found — start blank */ }
        await LoadObjectives();
        await JS.InvokeVoidAsync("studio.registerKeys", KeyOwner, selfRef,
            System.Text.Json.JsonSerializer.Serialize(Shortcuts));
    }

    /// <summary>The name this tool's chords are registered and dropped under.</summary>
    private const string KeyOwner = "sketch-tool";

    /// <summary>Where the map's destroyables and cores stand, from the intent that places them. The sketch
    /// draws the ground and does not own an objective, so these arrive as markers the canvas is handed: an
    /// author refining the ground can see what it has to carry. A map with no intent has none.</summary>
    private async Task LoadObjectives()
    {
        if (handle is null) return;
        JsonElement intent;
        try { intent = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/intent"); }
        catch { return; }
        if (intent.ValueKind != JsonValueKind.Object) return;

        var markers = new List<object>();
        foreach (var (key, kind) in new[] { ("destroyables", "destroyable"), ("cores", "core") })
            if (intent.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var entry in list.EnumerateArray())
                    if (entry.TryGetProperty("anchor", out var at) && at.ValueKind == JsonValueKind.Object
                        && at.TryGetProperty("x", out var x) && at.TryGetProperty("z", out var z))
                        markers.Add(new { kind, x = x.GetDouble(), z = z.GetDouble() });

        await handle.InvokeVoidAsync("setObjectives", JsonSerializer.Serialize(markers));
    }

    private async Task SetTool(string t)
    {
        tool = t;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private async Task SetOperation(string o)
    {
        op = o;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setOperation", o);
    }

    private async Task OnModeChange(ChangeEventArgs e)
    {
        mode = e.Value?.ToString() ?? "rot_180";
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setMode", mode);
    }

    private async Task OnCenterX(double v)
    {
        centerX = v;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setCenter", centerX, centerZ);
    }

    private async Task OnCenterZ(double v)
    {
        centerZ = v;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setCenter", centerX, centerZ);
    }

    private async Task ToggleMirror()
    {
        mirrorOn = !mirrorOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setMirrorVisible", mirrorOn);
    }

    private async Task ToggleShapes()
    {
        shapesOn = !shapesOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setShapesVisible", shapesOn);
    }

    private async Task ToggleChunks()
    {
        chunksOn = !chunksOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setChunkVisible", chunksOn);
    }

    private async Task ToggleBlocks()
    {
        blocksOn = !blocksOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setBlocksVisible", blocksOn);
    }

    private async Task ToggleRelief()
    {
        reliefOn = !reliefOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setReliefVisible", reliefOn);
    }

    private async Task ToggleSnap()
    {
        snapOn = !snapOn;
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("setSnap", snapOn);
    }

    private async Task OnFit()
    {
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null) await handle.InvokeVoidAsync("fitToBbox");
    }

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

    /// <summary>The layers the built board is made of, as the preview payload spells them, and which of them
    /// are switched off. Only the 3-D view has them — a 2-D drawing is one plan whatever it stacks.</summary>
    private IReadOnlyList<string> isoLayers = [];
    private readonly HashSet<string> isoHidden = [];

    private async Task ToggleIsoLayer(string id)
    {
        if (!isoHidden.Remove(id)) isoHidden.Add(id);
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null)
            await handle.InvokeVoidAsync("setIsoLayerShown", id, !isoHidden.Contains(id));
        StateHasChanged();
    }

    private Task SetHeight((string Id, double Base, double Floor) e)
        => handle?.InvokeVoidAsync("setHeight", e.Id, e.Base, e.Floor).AsTask() ?? Task.CompletedTask;

    private Task SetPathBand((string Id, double Radius, string Edge, int Seed) e)
        => handle?.InvokeVoidAsync("setPathBand", e.Id, e.Radius, e.Edge, e.Seed).AsTask() ?? Task.CompletedTask;

    private Task SetVertexHeight((string Id, int Idx, double Height) e)
    {
        // Keep the inspector's bound value in sync (the bridge doesn't echo it back) so the field shows
        // the committed height rather than reverting to the value from when the vertex was selected.
        selectedVertexHeight = e.Height;
        return handle?.InvokeVoidAsync("setVertexHeight", e.Id, e.Idx, e.Height).AsTask() ?? Task.CompletedTask;
    }

    // Edit one slope control's height in place (before Apply) — no bridge call, just the local model the
    // inspector's per-control input binds to.
    private void SetSlopeHeight((int Idx, double Height) e)
    {
        var control = slopeControls.FirstOrDefault(c => c.Idx == e.Idx);
        if (control is not null) control.Height = e.Height;
    }

    // Fit the surface plane through the marked controls (with their edited heights) and fill every vertex.
    private Task ApplySlope()
        => handle is null || selectedShapeId is null || slopeControls.Count < 2
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("applySlope", selectedShapeId, JsonSerializer.Serialize(slopeControls)).AsTask();

    // ── Panel / inspector actions → the JS bridge ──────────────────────────────

    private Task SelectShape(string id) => handle?.InvokeVoidAsync("selectShape", id).AsTask() ?? Task.CompletedTask;
    private Task SelectGroup(string id) => handle?.InvokeVoidAsync("selectGroup", id).AsTask() ?? Task.CompletedTask;
    private Task Rotate(double deg) => handle?.InvokeVoidAsync("rotateSelected", deg).AsTask() ?? Task.CompletedTask;
    private Task ToggleOp(string id) => handle?.InvokeVoidAsync("toggleOp", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleOverride(string id) => handle?.InvokeVoidAsync("toggleOverride", id).AsTask() ?? Task.CompletedTask;
    private Task DeleteShape(string id) => handle?.InvokeVoidAsync("deleteShape", id).AsTask() ?? Task.CompletedTask;
    private Task PromoteShape(string id) => handle?.InvokeVoidAsync("promoteShape", id).AsTask() ?? Task.CompletedTask;

    // ── Layer panel actions (S7b) ──────────────────────────────────────────────
    private Task SelectLayer(string id) => handle?.InvokeVoidAsync("switchLayer", id).AsTask() ?? Task.CompletedTask;
    private Task AddLayer() => handle?.InvokeVoidAsync("addLayer").AsTask() ?? Task.CompletedTask;
    private Task DeleteLayer(string id) => handle?.InvokeVoidAsync("deleteLayer", id).AsTask() ?? Task.CompletedTask;
    private Task RenameLayer((string Id, string Name) e) => handle?.InvokeVoidAsync("renameLayer", e.Id, e.Name).AsTask() ?? Task.CompletedTask;
    private Task SetLayerBaseY((string Id, double BaseY) e) => handle?.InvokeVoidAsync("setLayerBaseY", e.Id, e.BaseY).AsTask() ?? Task.CompletedTask;
    private Task ToggleMirrors(string groupId) => handle?.InvokeVoidAsync("toggleMirrors", groupId).AsTask() ?? Task.CompletedTask;
    private Task RenameGroup((string Id, string Name) e) => handle?.InvokeVoidAsync("renameGroup", e.Id, e.Name).AsTask() ?? Task.CompletedTask;

    // ── Bridge callbacks ───────────────────────────────────────────────────────

    /// <summary>A shape was selected on the canvas/panel (null = deselected).</summary>
    [JSInvokable]
    public void OnShapeSelected(string? id) { selectedShapeId = id; selectedVertexIdx = -1; slopeControls = []; StateHasChanged(); }

    /// <summary>The shift-marked surface-slope control set changed on the canvas — each entry is a vertex index
    /// + its current height, which the inspector lets the author edit before fitting the plane.</summary>
    [JSInvokable]
    public void OnSlopeControls(string? shapeId, string json)
    {
        slopeControls = shapeId is null ? [] : (JsonSerializer.Deserialize<List<SketchSlopeControl>>(json) ?? []);
        StateHasChanged();
    }

    /// <summary>A polygon vertex was click-selected on the canvas (null shapeId = cleared).</summary>
    [JSInvokable]
    public void OnVertexSelected(string? shapeId, int idx, double height)
    {
        selectedVertexIdx = shapeId is null ? -1 : idx;
        selectedVertexHeight = height;
        StateHasChanged();
    }

    /// <summary>A group was selected in the panel (null = deselected).</summary>
    [JSInvokable]
    public void OnGroupSelected(string? id) { selectedGroupId = id; StateHasChanged(); }

    /// <summary>The theme registry or an assignment changed on the bridge — the strip and the inspector are
    /// both drawn from what this reads.</summary>
    [JSInvokable]
    public void OnThemes(string json)
    {
        ApplyThemes(json);
        themeRevision++;
        // A brush naming a theme the board no longer has is an empty hand, not a stale one.
        if (themeBrush.Length > 0 && !themeIds.Contains(themeBrush)) themeBrush = "";
        StateHasChanged();
    }

    /// <summary>The placed dressing or its selection changed on the canvas — the props, the selected id, and
    /// the selected prop itself, which is what the inspector and the list both read.</summary>
    [JSInvokable]
    public void OnDressing(string json) { dressingJson = json; StateHasChanged(); }

    [JSInvokable]
    public void OnRelief(string json) { reliefJson = json; reliefRevision++; StateHasChanged(); }

    /// <summary>Which layers the board the preview just built is made of, and which of them it is leaving
    /// out. The bridge keeps a hidden layer hidden across a rebuild, so this is where the two agree again
    /// after an edit — including a layer the author deleted, which comes back neither listed nor hidden.
    /// </summary>
    [JSInvokable]
    public void OnIsoLayers(string json)
    {
        var read = JsonSerializer.Deserialize<IsoLayersMessage>(json);
        isoLayers = read?.Layers ?? [];
        isoHidden.Clear();
        foreach (var id in read?.Hidden ?? []) isoHidden.Add(id);
        StateHasChanged();
    }

    private sealed record IsoLayersMessage(IReadOnlyList<string> Layers, IReadOnlyList<string> Hidden);

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
    private string IsoNote => isoUnavailableWhy is null ? "no WebGL" : "3-D unavailable";

    /// <summary>The whole sentence, on hover.</summary>
    private string IsoNoteTitle => isoUnavailableWhy
        ?? "The 3-D height preview needs WebGL, which this browser can't provide.";

    /// <summary>The bridge pushed the current group→shape tree (on every layout change).</summary>
    [JSInvokable]
    public void OnLayout(string json)
    {
        var dto = JsonSerializer.Deserialize<SketchLayoutDto>(json);
        groups = dto?.Groups ?? [];
        shapes = dto?.Shapes ?? [];
        StateHasChanged();
    }

    /// <summary>The bridge pushed the layer list + active id (on layer add/switch/delete/edit).</summary>
    [JSInvokable]
    public void OnLayers(string json)
    {
        var dto = JsonSerializer.Deserialize<SketchLayersDto>(json);
        layerRows = dto?.Layers ?? [];
        activeLayerId = dto?.Active;
        StateHasChanged();
    }

    /// <summary>The canvas changed the active tool itself (e.g. auto-switch to select after a draw);
    /// keep the toolbar highlight truthful.</summary>
    [JSInvokable]
    public void OnToolChanged(string t)
    {
        tool = t;
        StateHasChanged();
    }

    /// <summary>The layout changed; update the group-count label and schedule a debounced save.</summary>
    [JSInvokable]
    public void OnDirty(int groupCount)
    {
        groupLabel = groupCount == 1 ? "1 group" : $"{groupCount} groups";
        StateHasChanged();
        ScheduleSave();
    }

    // ── Persistence: debounced PUT of the bridge's getState() ───────────────────

    private CancellationTokenSource? saveCts;

    private void ScheduleSave()
    {
        saveCts?.Cancel();
        saveCts = new CancellationTokenSource();
        _ = SaveDebouncedAsync(saveCts.Token);
    }

    private async Task SaveDebouncedAsync(CancellationToken token)
    {
        try { await Task.Delay(800, token); } catch (TaskCanceledException) { return; }
        await SaveAsync(token);
    }

    /// <summary>What the last save did, for the topbar to say. Null while every save has landed.</summary>
    private string? saveError;

    /// <summary>Store the bridge's state, and <b>read the answer</b>.
    ///
    /// <para>A refused or failed PUT is a completed HTTP round-trip, so nothing is thrown and the status is
    /// the only thing that says the drawing is not in the studio. Unread, the tool believes every edit
    /// landed, keeps drawing over a board the server last accepted several edits ago, and the author finds
    /// out by reloading — which is exactly when the work goes. The message is the server's own, because it
    /// is the one that knows which shape it could not take.</para></summary>
    private async Task SaveAsync(CancellationToken token)
    {
        if (handle is null) return;
        var was = saveError;
        try
        {
            var state = await handle.InvokeAsync<JsonElement>("getState", token);
            var resp = await Http.PutAsJsonAsync($"api/map/{Slug}/sketch", state, token);
            if (resp.IsSuccessStatusCode) saveError = null;
            else
            {
                var refusal = await resp.Content.ReadFromJsonAsync<RefusalDto>(token);
                saveError = "Not saved — " + (refusal?.Message is { Length: > 0 } why ? why
                                              : refusal?.Error is { Length: > 0 } label ? label
                                              : $"the studio answered {(int)resp.StatusCode}.");
            }
        }
        catch (TaskCanceledException) { return; }        // superseded by a later edit; that one reports
        catch { saveError = "Not saved — the studio could not be reached."; }
        if (saveError != was) await InvokeAsync(StateHasChanged);
    }

    // ── Finish: flush the layout, rasterize server-side, continue to Configure ──

    private bool finishing;
    private string? finishError;

    private async Task Finish()
    {
        if (handle is null) return;
        finishing = true;
        finishError = null;
        StateHasChanged();

        saveCts?.Cancel();          // flush the latest layout before the server rasterizes it
        await SaveAsync(CancellationToken.None);

        try
        {
            var resp = await Http.PostAsync($"api/map/{Slug}/sketch/finish", null);
            if (resp.IsSuccessStatusCode)
            {
                // Land back on the Configure overview (the draft is now a configure-stage map) and offer to
                // continue into the wizard there — rather than force-marching straight into it.
                Nav.NavigateTo($"maps?stage=configure&just={Slug}");
                return;
            }
            var refusal = await resp.Content.ReadFromJsonAsync<RefusalDto>();
            finishError = refusal?.Error is { Length: > 0 } label ? label : "Finish failed.";
        }
        catch { finishError = "Finish failed."; }

        finishing = false;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        saveCts?.Cancel();
        // Best-effort final flush of the last (<800 ms) change before tearing the handle down.
        await SaveAsync(CancellationToken.None);
        // A draft left with nothing drawn is discarded so an abandoned "New sketch" click doesn't linger on
        // the dashboard. The client gates on empty geometry to skip the call for real work; the server
        // re-checks the full pristine condition (default name, no authors, no shapes) before deleting.
        if (shapes.Count == 0 && groups.Count == 0)
        {
            try { await Http.DeleteAsync($"api/map/{Slug}/sketch/discard-if-empty"); } catch { }
        }
        try { await JS.InvokeVoidAsync("studio.unregisterKeys", KeyOwner); } catch { }
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }
}
