using Microsoft.JSInterop;

namespace PgmStudio.Client.Components;

/// <summary>
/// The read-only 3-D preview of a canvas, as its host holds it: whether it is on, whether it could be shown,
/// and why not. The bridge does the drawing and reports an unavailable preview asynchronously, so this holds
/// only what the toggle and its note read.
/// </summary>
/// <param name="bridge">The host's bridge handle at the moment of the call, or null before it is mounted.</param>
/// <param name="changed">Re-renders the host: the toggle sits in a child component, and the host's own markup
/// reads <see cref="ThreeD"/>.</param>
public sealed class IsoView(Func<IJSObjectReference?> bridge, Action changed)
{
    /// <summary>Whether the canvas is showing the isometric preview.</summary>
    public bool ThreeD { get; private set; }

    /// <summary>The preview could not be shown, so the toggle is disabled.</summary>
    public bool Unavailable { get; private set; }

    /// <summary>The build's own sentence; null when WebGL itself is missing.</summary>
    private string? why;

    /// <summary>The chip beside the toggle: what stopped the preview, in two words.</summary>
    public string Note => why is null ? "No WebGL" : "3-D unavailable";

    /// <summary>The whole sentence, on hover.</summary>
    public string NoteTitle => why ?? "The 3-D preview needs WebGL, which this browser doesn't support.";

    /// <summary>Fall back to 2-D and disable the toggle. <paramref name="reason"/> is empty when WebGL itself
    /// is missing and the build's own sentence when the board would not build — two different things to do
    /// about it, so the note says which.</summary>
    public void MarkUnavailable(string? reason)
    {
        ThreeD = false;
        Unavailable = true;
        why = string.IsNullOrWhiteSpace(reason) ? null : reason;
        changed();
    }

    public async Task ToggleAsync()
    {
        if (Unavailable) return;
        ThreeD = !ThreeD;
        changed();
        if (bridge() is not { } handle) return;
        // The bridge reports an unavailable preview asynchronously; this catch only guards a hard interop
        // failure so the toggle can never trip Blazor's unhandled-error boundary.
        try { await handle.InvokeVoidAsync("setView", ThreeD ? "iso" : "2d"); }
        catch { MarkUnavailable(null); }
    }

    /// <summary>Back to the 2-D drawing, for a phase that only works there.</summary>
    public async Task LeaveAsync()
    {
        if (!ThreeD) return;
        ThreeD = false;
        if (bridge() is { } handle) await handle.InvokeVoidAsync("setView", "2d");
    }

    public Task RotateAsync() => bridge()?.InvokeVoidAsync("rotateIso").AsTask() ?? Task.CompletedTask;
}
