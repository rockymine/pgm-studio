using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages;

// New-map authoring concept showcase. Re-runs lucide after render so the data-lucide icons resolve,
// and scrolls the left-nav target into view (plain hash anchors get intercepted by Blazor's router).
public partial class Authoring
{
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private async Task Scroll(string id) => await JS.InvokeVoidAsync("studio.scrollToId", id);
}
