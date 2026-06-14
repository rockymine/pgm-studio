using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class EditorCanvas
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string? Category { get; set; }
    [Parameter] public EventCallback<string?> OnSelect { get; set; }

    /// <summary>When set, draw tools are enabled and a drawn shape creates a region in this category
    /// (C5). Null = read-only (move/select only), e.g. the Regions browser.</summary>
    [Parameter] public string? DrawCategory { get; set; }
    /// <summary>Fired after a drawn region is created (and the canvas reloaded) so the host activity
    /// can refresh its sidebar tree/list.</summary>
    [Parameter] public EventCallback OnRegionCreated { get; set; }

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<EditorCanvas>? selfRef;
    private string tool = "move";

    protected override async Task OnInitializedAsync()
    {
        // The orbit toggle only applies while drawing, and only on maps with a confirmed symmetry — fetch
        // the primary mode so the toolbar chip can label itself ("Orbit 90" / "Orbit x" …). Default: on.
        if (DrawCategory is null) return;
        try
        {
            var sym = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/symmetry");
            if (sym.TryGetProperty("status", out var st) && st.GetString() == "confirmed"
                && sym.TryGetProperty("primary", out var pr) && pr.ValueKind == JsonValueKind.Object
                && pr.TryGetProperty("type", out var ty))
                orbitMode = ty.GetString();
        }
        catch { orbitMode = null; }   // no symmetry artifact / asymmetric map → no orbit chip
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender)
        {
            selfRef = DotNetObjectReference.Create(this);
            handle = await JS.InvokeAsync<IJSObjectReference>(
                "studio.mountCanvas", svgRef, wrapRef, coordsRef, zoomRef, selfRef, Slug, Category);
        }
    }

    /// <summary>F3: the map's confirmed symmetry mode (e.g. "rot_90"), or null when no orbit is available.</summary>
    private string? orbitMode;
    /// <summary>F3: whether a drawn region is mirrored into its symmetry orbit. Toggled from the toolbar.</summary>
    private bool orbitOn = true;

    private void ToggleOrbit() => orbitOn = !orbitOn;

    /// <summary>Toolbar label for the orbit chip — "Orbit 90" / "Orbit 180" / "Orbit x" / "Orbit z" …</summary>
    private string OrbitLabel() => orbitMode switch
    {
        "rot_90" => "Orbit 90",
        "rot_180" => "Orbit 180",
        { } m when m.StartsWith("mirror_") => $"Orbit {m["mirror_".Length..]}",
        _ => "Orbit",
    };

    private async Task SetTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private bool blocksOn;

    /// <summary>C6: toggle the top-surface block-colour overlay. Stays off if the map has no scan data.</summary>
    private async Task ToggleBlocks()
    {
        if (handle is null) return;
        var ok = await handle.InvokeAsync<bool>("setBlocks", !blocksOn);
        if (ok) blocksOn = !blocksOn;
    }

    /// <summary>Highlight the given region ids on the canvas (called by the activity when the sidebar selects).</summary>
    public async Task SetSelectionAsync(IEnumerable<string> ids)
    {
        // Box the array as a single object — InvokeVoidAsync's `params object?[]` would otherwise
        // spread a string[] into separate JS arguments (the canvas would get one string, not the list).
        if (handle is not null) await handle.InvokeVoidAsync("setSelection", (object)ids.ToArray());
    }

    public async Task ResizeAsync()
    {
        if (handle is not null) await handle.InvokeVoidAsync("resize");
    }

    [JSInvokable] public Task OnCanvasSelect(string? id) => OnSelect.InvokeAsync(id);

    /// <summary>C5: a draw tool completed a shape → create the region, fill its symmetry orbit (F3),
    /// reload the canvas, notify the host.</summary>
    [JSInvokable]
    public async Task OnRegionDraw(JsonElement draw)
    {
        if (DrawCategory is null || handle is null) return;
        var resp = await Http.PostAsJsonAsync($"api/map/{Slug}/regions", BuildPayload(draw, DrawCategory));
        if (!resp.IsSuccessStatusCode) return;

        // F3: when the orbit toggle is on, create the source's counterpart(s) so the drawn region appears
        // in every symmetric position. No-op (server-side) on asymmetric maps; skipped entirely when off.
        if (orbitOn)
        {
            var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (created.TryGetProperty("id", out var idEl) && idEl.GetString() is { } newId)
                await Http.PostAsJsonAsync($"api/map/{Slug}/regions/{newId}/orbit", new { category = DrawCategory });
        }

        await SetTool("select");
        StateHasChanged();                              // refresh the toolbar highlight (JSInvokable won't auto-render)
        await handle.InvokeVoidAsync("load", Slug);     // re-render the canvas with the new region(s)
        await OnRegionCreated.InvokeAsync();
    }

    // Convert an EditorCanvas drawResult into a createRegion payload (port of drawResultToPayload).
    private static Dictionary<string, object?> BuildPayload(JsonElement d, string category)
    {
        double N(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        var type = d.TryGetProperty("type", out var t) ? t.GetString() ?? "rectangle" : "rectangle";
        var p = new Dictionary<string, object?> { ["category"] = category, ["type"] = type };
        switch (type)
        {
            case "cylinder": p["base_x"] = N("base_x"); p["base_y"] = 0; p["base_z"] = N("base_z"); p["radius"] = N("radius"); p["height"] = 10; break;
            case "circle": p["center_x"] = N("center_x"); p["center_z"] = N("center_z"); p["radius"] = N("radius"); break;
            case "point" or "block": p["x"] = N("min_x") + 0.5; p["y"] = 0; p["z"] = N("min_z") + 0.5; break;
            default: p["min_x"] = N("min_x"); p["min_z"] = N("min_z"); p["max_x"] = N("max_x"); p["max_z"] = N("max_z"); break;   // rectangle, cuboid
        }
        return p;
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
}
