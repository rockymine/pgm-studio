using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's front page: each kind's count, and its newest rows' own pictures as the sample of what it holds.
/// </summary>
public partial class LibraryChooser
{
    private IReadOnlyDictionary<string, LibraryCount> held = new Dictionary<string, LibraryCount>();
    private bool loading = true;

    /// <summary>What the library is, in the glossary's own words.</summary>
    private static string LibraryDefinition =>
        PgmStudio.Vocabulary.Glossary.Terms.FirstOrDefault(entry => entry.Term == "library")?.Definition ?? "";

    protected override async Task OnInitializedAsync()
    {
        held = await Library.CountsAsync();
        loading = false;
    }

    /// <summary>The libraries by what they dress: the ground, the buildings on it, and what grows or lies there.</summary>
    private static readonly (string Group, string[] Slugs)[] Groups =
    [
        ("Ground", [LibraryKinds.StylesSlug, LibraryKinds.ThemesSlug, LibraryKinds.BiomesSlug]),
        ("Buildings", [LibraryKinds.HousesSlug, LibraryKinds.RoofsSlug, LibraryKinds.StoreysSlug, LibraryKinds.PorchesSlug]),
        ("Nature", [LibraryKinds.TreesSlug, LibraryKinds.BouldersSlug]),
    ];

    /// <summary>The newest rows' card pictures, up to three, which is what the kind currently looks like.</summary>
    private IReadOnlyList<string> Samples(LibraryKind kind) =>
        held.TryGetValue(kind.Route, out var count) ? count.Newest : [];

    private string CountLabel(LibraryKind kind)
    {
        if (loading) return string.Empty;
        return held.TryGetValue(kind.Route, out var count) ? count.Count.ToString() : "0";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
