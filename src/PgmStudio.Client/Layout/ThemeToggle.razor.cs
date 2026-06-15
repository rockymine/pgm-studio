using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Layout;

public partial class ThemeToggle
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private async Task Toggle() => await JS.InvokeVoidAsync("studioTheme.toggle");

    // Render the lucide sun/moon placeholders into SVGs after the button is in the DOM.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await JS.InvokeVoidAsync("studio.icons");
    }
}
