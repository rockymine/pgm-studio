using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The In game gallery: every view of the board, a picture each, and one open large at a time.</summary>
public partial class SketchInGamePhase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public string Slug { get; set; } = "";

    /// <summary>The views to show, or null while they are being read.</summary>
    [Parameter] public MapViewsDto? Views { get; set; }

    /// <summary>Why the views could not be read, or null.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Bumped by the host whenever the board may have changed since the pictures were asked for.</summary>
    [Parameter] public int Round { get; set; }

    [Parameter] public EventCallback OnBack { get; set; }

    /// <summary>Place a view of the author's own, on the canvas.</summary>
    [Parameter] public EventCallback OnPlace { get; set; }

    /// <summary>Stop keeping a view.</summary>
    [Parameter] public EventCallback<MapViewDto> OnLetGo { get; set; }

    private MapViewDto? open;
    private ElementReference lightbox;
    private bool focusLightbox;
    private bool lettingGo;

    private void Open(MapViewDto view)
    {
        open = view;
        focusLightbox = true;
    }

    private void Close() => open = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!focusLightbox || open is null) return;
        focusLightbox = false;
        await lightbox.FocusAsync();
    }

    private void OnKey(KeyboardEventArgs key)
    {
        switch (key.Key)
        {
            case "Escape": Close(); break;
            case "ArrowLeft": Step(-1); break;
            case "ArrowRight": Step(1); break;
        }
    }

    /// <summary>Open the view <paramref name="by"/> places along from the one open, wrapping at either end.</summary>
    private void Step(int by)
    {
        if (open is null || Views is not { Views.Count: > 0 } all) return;
        var at = Math.Max(0, IndexOf(all.Views, open.Id));
        open = all.Views[((at + by) % all.Views.Count + all.Views.Count) % all.Views.Count];
    }

    private static int IndexOf(IReadOnlyList<MapViewDto> views, string id)
    {
        for (var i = 0; i < views.Count; i++)
            if (views[i].Id == id) return i;
        return -1;
    }

    private async Task LetGo(MapViewDto view)
    {
        lettingGo = true;
        try { await OnLetGo.InvokeAsync(view); }
        finally { lettingGo = false; }
        open = null;
    }

    private string FullSize(MapViewDto view) =>
        $"api/map/{Slug}/render/eye?{view.Query}&width=1920&height=1080&round={Round}";

    /// <summary>Where the eye stands and what it looks at, in the block coordinates the canvas reads.</summary>
    private static string Where(MapViewDto view)
    {
        var looking = string.Create(CultureInfo.InvariantCulture, $"looking at {view.LookX}, {view.LookZ}");
        if (view.FromX is not { } x || view.FromZ is not { } z) return $"{looking} — the eye finds its own place";
        var height = view.Y is { } y ? string.Create(CultureInfo.InvariantCulture, $", eye at y {y:0.#}") : "";
        return string.Create(CultureInfo.InvariantCulture, $"standing at {x}, {z}{height}, {looking}");
    }
}
