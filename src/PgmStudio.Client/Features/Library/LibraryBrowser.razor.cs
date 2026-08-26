using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>One row of any library. The six list routes each answer id, name and a card picture; a style also
/// answers its kind, which is the only thing a browse grid filters on beyond the name.</summary>
public sealed record LibraryRow(long Id, string Name, string Preview, string? Kind = null);

/// <summary>
/// The browse grid, for every kind. It owns the name search and nothing else: the rows arrive already
/// narrowed by whatever the kind filters on, which only <see cref="LibraryKinds.Styles"/> does.
/// </summary>
public partial class LibraryBrowser
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public LibraryKind Kind { get; set; } = default!;
    [Parameter] public IReadOnlyList<LibraryRow> Rows { get; set; } = [];
    [Parameter] public bool Loading { get; set; }

    /// <summary>Cards wide enough for a cut-open sample rather than a swatch.</summary>
    [Parameter] public bool Wide { get; set; }

    /// <summary>What the kind filters by, beside the search box.</summary>
    [Parameter] public RenderFragment? Filters { get; set; }

    /// <summary>The tag a card carries over its picture, where the kind has one.</summary>
    [Parameter] public Func<LibraryRow, string>? Badge { get; set; }

    private string search = "";

    private IReadOnlyList<LibraryRow> Shown => string.IsNullOrWhiteSpace(search)
        ? Rows
        : [.. Rows.Where(row => row.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))];

    private void OnSearch(ChangeEventArgs e) => search = e.Value as string ?? "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
