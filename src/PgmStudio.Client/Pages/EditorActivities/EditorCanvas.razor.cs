using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class EditorCanvas
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string? Category { get; set; }
    [Parameter] public EventCallback<string?> OnSelect { get; set; }

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<EditorCanvas>? selfRef;
    private string tool = "move";

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

    private async Task SetTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
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
