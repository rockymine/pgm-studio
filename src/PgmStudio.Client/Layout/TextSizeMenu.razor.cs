using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Layout;

public partial class TextSizeMenu
{
    private static readonly (string Value, string Label)[] Sizes =
    [
        ("small", "Small"),
        ("default", "Default"),
        ("large", "Large"),
        ("larger", "Larger"),
    ];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool open;
    private string current = "default";

    private async Task ToggleAsync()
    {
        if (!open) current = await JS.InvokeAsync<string>("studioTextSize.get");
        open = !open;
    }

    private async Task SetAsync(string size)
    {
        await JS.InvokeVoidAsync("studioTextSize.set", size);
        current = size;
        open = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await JS.InvokeVoidAsync("studio.icons");
    }
}
