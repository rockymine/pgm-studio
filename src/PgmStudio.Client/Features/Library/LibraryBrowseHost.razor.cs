using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// Loads one library's rows and hands them to <see cref="LibraryBrowser"/>. All six list routes answer id,
/// name and a card picture, so one row shape reads every one of them.
/// </summary>
public partial class LibraryBrowseHost
{
    /// <summary>The kinds a style may be, for the filter row — the schema's list rather than a second one.</summary>
    [Inject] public MaterialSchema Schema { get; set; } = default!;

    [Parameter, EditorRequired] public LibraryKind Kind { get; set; } = default!;

    private IReadOnlyList<LibraryRow> rows = [];
    private readonly HashSet<string> kindFilter = [];
    private bool loading = true;

    protected override async Task OnParametersSetAsync()
    {
        loading = true;
        kindFilter.Clear();
        rows = await Library.ListAsync<LibraryRow>(Kind);
        loading = false;
    }

    private IReadOnlyList<LibraryRow> Shown => kindFilter.Count == 0
        ? rows
        : [.. rows.Where(row => row.Kind is { } kind && kindFilter.Contains(kind))];

    private void ToggleKind(string kind)
    {
        if (!kindFilter.Add(kind)) kindFilter.Remove(kind);
    }
}
