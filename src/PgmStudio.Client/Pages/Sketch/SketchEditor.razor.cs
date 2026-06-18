using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchEditor
{
    [Parameter] public string Slug { get; set; } = "";

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SketchEditor>? selfRef;

    private string tool = "move";
    private string op = "add";
    private string mode = "rot_180";
    private bool mirrorOn = true;
    private bool shapesOn = false;
    private bool chunksOn = true;
    private string islandLabel = "";
    private string? selectedId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountSketch", svgRef, wrapRef, coordsRef, zoomRef, selfRef);
    }

    private async Task SetTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private async Task SetOperation(string o)
    {
        op = o;
        if (handle is not null) await handle.InvokeVoidAsync("setOperation", o);
    }

    private async Task OnModeChange(ChangeEventArgs e)
    {
        mode = e.Value?.ToString() ?? "rot_180";
        if (handle is not null) await handle.InvokeVoidAsync("setMode", mode);
    }

    private async Task ToggleMirror()
    {
        mirrorOn = !mirrorOn;
        if (handle is not null) await handle.InvokeVoidAsync("setMirrorVisible", mirrorOn);
    }

    private async Task ToggleShapes()
    {
        shapesOn = !shapesOn;
        if (handle is not null) await handle.InvokeVoidAsync("setShapesVisible", shapesOn);
    }

    private async Task ToggleChunks()
    {
        chunksOn = !chunksOn;
        if (handle is not null) await handle.InvokeVoidAsync("setChunkVisible", chunksOn);
    }

    private async Task OnFit()
    {
        if (handle is not null) await handle.InvokeVoidAsync("fitToBbox");
    }

    /// <summary>A shape was selected on the canvas (null = deselected). Backs a future inspector.</summary>
    [JSInvokable]
    public void OnShapeSelected(string? id) => selectedId = id;

    /// <summary>The canvas changed the active tool itself (e.g. auto-switch to select after a draw);
    /// keep the toolbar highlight truthful.</summary>
    [JSInvokable]
    public void OnToolChanged(string t)
    {
        tool = t;
        StateHasChanged();
    }

    /// <summary>The layout changed; the bridge reports the live island count. (Persistence = S2d.)</summary>
    [JSInvokable]
    public void OnDirty(int islandCount)
    {
        islandLabel = islandCount == 1 ? "1 island" : $"{islandCount} islands";
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
}
