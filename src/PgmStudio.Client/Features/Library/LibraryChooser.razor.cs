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
        var lists = LibraryKinds.All.Select(async kind => (kind.Slug, Rows: await Library.ListAsync<LibraryRow>(kind)));
        foreach (var (slug, rows) in await Task.WhenAll(lists)) held[slug] = rows;
        loading = false;
    }

    /// <summary>The libraries by what they dress: the ground, the buildings on it, and what grows or lies there.</summary>
    private static readonly (string Group, string[] Slugs)[] Groups =
    [
        ("Terrain", [LibraryKinds.StylesSlug, LibraryKinds.ThemesSlug, LibraryKinds.BiomesSlug]),
        ("Buildings", [LibraryKinds.HousesSlug, LibraryKinds.RoofsSlug, LibraryKinds.StoreysSlug, LibraryKinds.PorchesSlug]),
        ("Nature", [LibraryKinds.TreesSlug, LibraryKinds.BouldersSlug]),
    ];

    /// <summary>The newest rows' card pictures, up to three, which is what the kind currently looks like.</summary>
    private List<string> Samples(LibraryKind kind) =>
        held.TryGetValue(kind.Slug, out var rows)
            ? [.. rows.Select(row => row.Preview).Where(preview => !string.IsNullOrEmpty(preview)).Take(3)]
            : [];

    private string CountLabel(LibraryKind kind)
    {
        if (loading) return string.Empty;
        return held.TryGetValue(kind.Slug, out var rows) ? rows.Count.ToString() : "0";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
