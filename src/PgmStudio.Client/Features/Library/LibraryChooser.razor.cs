using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's front page. It reads every kind's list rather than a count endpoint, because the card shows
/// what the library holds as well as how much of it — the newest row's own picture is the sample.
/// </summary>
public partial class LibraryChooser
{
    private readonly Dictionary<string, IReadOnlyList<LibraryRow>> held = [];
    private bool loading = true;

    protected override async Task OnInitializedAsync()
    {
        foreach (var kind in LibraryKinds.All) held[kind.Slug] = await Library.ListAsync<LibraryRow>(kind);
        loading = false;
    }

    /// <summary>The newest row's card picture, which is what the kind currently looks like.</summary>
    private string? Sample(LibraryKind kind) =>
        held.TryGetValue(kind.Slug, out var rows) ? rows.FirstOrDefault()?.Preview : null;

    private string CountLabel(LibraryKind kind)
    {
        if (loading) return "…";
        var count = held.TryGetValue(kind.Slug, out var rows) ? rows.Count : 0;
        return count == 1 ? $"1 {kind.One}" : $"{count} {kind.Title.ToLowerInvariant()}";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
