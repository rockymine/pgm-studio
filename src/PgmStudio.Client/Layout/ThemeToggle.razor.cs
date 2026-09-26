using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Layout;

public partial class ThemeToggle
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private async Task Toggle() => await JS.InvokeVoidAsync("studioTheme.toggle");
}
