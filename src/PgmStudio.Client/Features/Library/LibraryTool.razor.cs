using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The style library's routable host (B44, G34b). It holds no library state of its own: each half owns the
/// data it browses, and this only says which one is showing, so the route is the only thing that has to agree
/// between them.
/// </summary>
public partial class LibraryTool
{
    /// <summary>Which half is showing — the route segment, absent meaning styles.</summary>
    [Parameter] public string? Tab { get; set; }

    internal const string StylesTab = "styles";
    internal const string ThemesTab = "themes";
    internal const string PartsTab = "parts";
    internal const string RoomsTab = "rooms";

    /// <summary>Styles is the one with no segment of its own, so it is what nothing else claims.</summary>
    private bool On(string tab) => tab == StylesTab
        ? !On(ThemesTab) && !On(PartsTab) && !On(RoomsTab)
        : string.Equals(Tab, tab, StringComparison.OrdinalIgnoreCase);

    private string TabName =>
        On(ThemesTab) ? "Themes" : On(PartsTab) ? "House parts" : On(RoomsTab) ? "Rooms" : "Styles";
}
