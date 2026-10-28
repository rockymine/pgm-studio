using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Components;

/// <summary>The studio's menu as an editor's bar reaches it (StudioShell's "StudioMenu"): whether it is open, and how
/// to open it.</summary>
public sealed record StudioMenu(bool Open, EventCallback OpenIt)
{
    /// <summary>The menu outside a StudioShell, which nothing opens.</summary>
    public static readonly StudioMenu Closed = new(false, EventCallback.Empty);
}
