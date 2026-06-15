using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class SliceView : IAsyncDisposable
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public RegionNode? Node { get; set; }
    /// <summary>Raised when the user drags the Y line (point/block only). Wired by editing activities to
    /// patch the region's Y; when unwired the slice is display-only (no draggable line).</summary>
    [Parameter] public EventCallback<int> OnYChanged { get; set; }

    private const int HalfWidth = 10;   // 21 columns total (the point ± 10)

    private ElementReference canvasRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SliceView>? selfRef;
    private string? shownId;
    private string? shownAxis;
    private string axis = "nz";         // view direction: nz/pz look along Z (primary X), nx/px along X (primary Z)
    private bool show;
    private bool editable;

    protected override void OnParametersSet()
    {
        show = !string.IsNullOrEmpty(Slug) && Node?.Type is "point" or "block" or "rectangle";
        editable = Node?.Type is "point" or "block" && OnYChanged.HasDelegate;
        // a newly-selected region resets the axis to its sensible default (user toggle persists otherwise)
        if (Node is not null && Node.Id != shownId) axis = DefaultAxis(Node);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!show)
        {
            if (handle is not null) { await DisposeHandleAsync(); shownId = null; shownAxis = null; }
            return;
        }
        if (handle is null)
        {
            selfRef ??= DotNetObjectReference.Create(this);
            handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountSliceView", canvasRef, selfRef, Slug);
            shownId = null; shownAxis = null;
        }
        if (Node is not null && (Node.Id != shownId || axis != shownAxis) && Window(Node) is { } w)
        {
            shownId = Node.Id; shownAxis = axis;
            await handle.InvokeVoidAsync("update", w);
        }
    }

    [JSInvokable]
    public Task OnSliceY(int y) => OnYChanged.InvokeAsync(y);

    private void SetAxis(string a) => axis = a;   // re-render → OnAfterRender pushes the new window

    private static string DefaultAxis(RegionNode n)
    {
        if (n.Type is "point" or "block") return "nz";
        double? D(string k) => n.Coords.GetValueOrDefault(k) is { } v ? Convert.ToDouble(v) : null;
        if (D("min_x") is { } mnx && D("max_x") is { } mxx && D("min_z") is { } mnz && D("max_z") is { } mxz)
            return Math.Abs(mxx - mnx) >= Math.Abs(mxz - mnz) ? "nz" : "nx";   // primary = the longer horizontal axis
        return "nz";
    }

    // The /segments window for this region under the current look axis: a point's column ± HalfWidth
    // along the primary axis at its other coord; a rectangle's footprint (axis only picks the projection).
    // markerY non-null ⇒ a draggable Y line.
    private Dictionary<string, object?>? Window(RegionNode n)
    {
        double? D(string k) => n.Coords.GetValueOrDefault(k) is { } v ? Convert.ToDouble(v) : null;

        var zLook = axis is "nz" or "pz";   // look along Z → primary X (window spans X); else primary Z

        if (n.Type is "point" or "block")
        {
            if (D("x") is not { } x || D("z") is not { } z) return null;
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            int? markerY = editable && D("y") is { } y ? (int)Math.Floor(y) : null;
            return zLook
                ? new() { ["axis"] = axis, ["xmin"] = ix - HalfWidth, ["xmax"] = ix + HalfWidth, ["zmin"] = iz, ["zmax"] = iz, ["markerY"] = markerY }
                : new() { ["axis"] = axis, ["xmin"] = ix, ["xmax"] = ix, ["zmin"] = iz - HalfWidth, ["zmax"] = iz + HalfWidth, ["markerY"] = markerY };
        }

        if (D("min_x") is not { } mnx || D("max_x") is not { } mxx || D("min_z") is not { } mnz || D("max_z") is not { } mxz)
            return null;
        int x0 = (int)Math.Floor(Math.Min(mnx, mxx)), x1 = (int)Math.Floor(Math.Max(mnx, mxx));
        int z0 = (int)Math.Floor(Math.Min(mnz, mxz)), z1 = (int)Math.Floor(Math.Max(mnz, mxz));
        return new() { ["axis"] = axis, ["xmin"] = x0, ["xmax"] = x1, ["zmin"] = z0, ["zmax"] = z1, ["markerY"] = (int?)null };
    }

    private async Task DisposeHandleAsync()
    {
        if (handle is null) return;
        try { await handle.InvokeVoidAsync("dispose"); } catch { }
        try { await handle.DisposeAsync(); } catch { }
        handle = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHandleAsync();
        selfRef?.Dispose();
    }
}
