using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class Toast
{
    /// <summary>The message to show; bound to the host's error string. A new value (re)shows the toast.</summary>
    [Parameter] public string? Message { get; set; }
    [Parameter] public string Kind { get; set; } = "error";
    [Parameter] public int DurationMs { get; set; } = 5000;

    private bool showing;
    private string? lastShown;
    private int token;

    protected override void OnParametersSet()
    {
        if (Message is null) { lastShown = null; showing = false; return; }
        if (Message == lastShown) return;          // already showing this one (parent re-render) — leave it
        lastShown = Message;
        showing = true;
        _ = DismissAfter(++token);                 // fire-and-forget timer; token cancels stale ones
    }

    private async Task DismissAfter(int t)
    {
        await Task.Delay(DurationMs);
        if (t != token) return;                    // superseded by a newer message
        showing = false;
        StateHasChanged();
    }
}
