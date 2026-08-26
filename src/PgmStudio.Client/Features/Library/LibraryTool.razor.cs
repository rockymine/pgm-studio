using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's routable host. It holds no library state of its own: each kind's browser and editor own what
/// they read, and this only says which route is showing, so the URL is the only thing that has to agree
/// between them.
/// </summary>
public partial class LibraryTool
{
    /// <summary>Which library is open — the route segment, absent meaning the chooser.</summary>
    [Parameter] public string? Kind { get; set; }

    /// <summary>Which entry is open — a row id, or <c>new</c>. Absent means the browse grid.</summary>
    [Parameter] public string? Entry { get; set; }

    private string? entryName;
    private string? saveState;

    private LibraryKind? Open => LibraryKinds.Of(Kind);

    private string PageName => Open is null ? "Library" : $"{Open.Title} — library";

    private string EntryName => string.IsNullOrWhiteSpace(entryName)
        ? Entry == "new" ? $"New {Open?.One}" : "Entry"
        : entryName;

    /// <summary>The crumb follows the name as it is typed, so the trail names what is being edited.</summary>
    private void OnEntryName(string name) => entryName = name;

    private void OnSaved(string? state) => saveState = state;

    /// <summary>A move between kinds or entries is a different document, so nothing carries over.</summary>
    protected override void OnParametersSet()
    {
        entryName = null;
        saveState = null;
    }
}
