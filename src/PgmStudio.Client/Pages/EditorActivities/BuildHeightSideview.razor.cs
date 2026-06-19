using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class BuildHeightSideview : IAsyncDisposable
{
    [Parameter] public string Slug { get; set; } = "";
    /// <summary>Current max build height (null = no ceiling). Pushed to the canvas line when it changes.</summary>
    [Parameter] public double? Height { get; set; }
    /// <summary>Fired when the user drags the line; carries the new (rounded) height.</summary>
    [Parameter] public EventCallback<double?> HeightChanged { get; set; }

    private string axis = "nz";   // side-view direction: nz/pz/nx/px (camera on −/+ side of Z/X)
    private ElementReference sideviewRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<BuildHeightSideview>? selfRef;
    private double? pushed;        // last height pushed to the canvas — so a drag isn't echoed back to it

    protected override async Task OnParametersSetAsync()
    {
        // The host changed Height (typed a value): move the line. A drag-originated change already set
        // `pushed`, so it's a no-op here and we don't fight the canvas.
        if (handle is not null && Height != pushed)
        {
            pushed = Height;
            await handle.InvokeVoidAsync("setBuildHeight", Height);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || handle is not null) return;
        selfRef ??= DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountSideview", sideviewRef, selfRef, Slug, axis);
        pushed = Height;
        if (Height is { } y) await handle.InvokeVoidAsync("setBuildHeight", y);
    }

    private async Task SetAxis(string a)
    {
        if (axis == a) return;
        axis = a;
        if (handle is not null) await handle.InvokeVoidAsync("loadAxis", a);
    }

    /// <summary>Invoked from the side-view canvas when the user drags the build-height line.</summary>
    [JSInvokable]
    public async Task OnHeightChanged(double y)
    {
        var v = (double)(int)Math.Round(y);
        pushed = v;   // the canvas already moved; don't echo it back through OnParametersSet
        await HeightChanged.InvokeAsync(v);
    }

    public async ValueTask DisposeAsync()
    {
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
            handle = null;
        }
        selfRef?.Dispose();
    }
}
