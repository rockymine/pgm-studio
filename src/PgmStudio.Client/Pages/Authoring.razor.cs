using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages;

// New-map authoring concept showcase. Like Design, it only needs to (re)run lucide after render
// so the data-lucide icons resolve. No state, no interactivity — UI concept only.
public partial class Authoring
{
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
