using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The style &amp; theme library's routable host (B44). It holds no library state of its own: the two halves
/// each own the data they browse, and this only says which one is showing, so the route is the only thing that
/// has to agree between them.
/// </summary>
public partial class LibraryTool
{
    /// <summary>Which half is showing — the route segment, absent meaning styles.</summary>
    [Parameter] public string? Tab { get; set; }

    internal const string StylesTab = "styles";
    internal const string ThemesTab = "themes";

    private bool OnThemes => string.Equals(Tab, ThemesTab, StringComparison.OrdinalIgnoreCase);
}
