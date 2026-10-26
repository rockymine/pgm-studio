using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// One kind of library row: where it is reached, and what it is called on the surfaces that offer it. The
/// rail, the chooser, the browse grid and <see cref="TerrainLibraryClient"/> all read this table, so a route
/// and its label are stated once.
/// </summary>
/// <param name="Slug">The <c>/library/{slug}</c> segment.</param>
/// <param name="Route">The <c>api/{route}</c> stem every verb hangs off.</param>
/// <param name="Title">The kind in the rail, the crumbs and the browse heading.</param>
/// <param name="One">The kind in the singular — a New button, a count and an empty state read it.</param>
/// <param name="Icon">The lucide glyph.</param>
/// <param name="Term">The glossary term the kind is, whose definition the chooser card and the browse heading
/// show.</param>
/// <param name="DraftPreview">Whether a draft draws at <c>{route}/preview</c>. A style does not: a style
/// <em>is</em> a material, so it draws as a bare one at <c>terrain/material-preview</c> with no row involved.</param>
/// <param name="Composed">Whether the kind answers its own composed document at <c>{route}/{id}/json</c> —
/// the form a map snapshots. Only a theme and a house compose to one; a style is already a document, and a
/// part is only ever part of one.</param>
public sealed record LibraryKind(
    string Slug, string Route, string Title, string One, string Icon, string Term,
    bool DraftPreview = true, bool Composed = false)
{
    /// <summary>What the kind is, in the glossary's own words, so a kind is defined once.</summary>
    public string Blurb => Glossary.Terms.FirstOrDefault(entry => entry.Term == Term)?.Definition ?? "";
}

/// <summary>The libraries, in the order they compose: a style is one material, a theme is a finish made of
/// styles, a roof, a storey and a porch are the parts a house binds, a house is the whole building, a tree and
/// a boulder are what a click puts down, and a biome is the colour a column carries.</summary>
public static class LibraryKinds
{
    // The route segments as constants, because a switch over a kind needs them at compile time.
    public const string StylesSlug = "styles";
    public const string ThemesSlug = "themes";
    public const string RoofsSlug = "roofs";
    public const string StoreysSlug = "storeys";
    public const string PorchesSlug = "porches";
    public const string HousesSlug = "houses";
    public const string TreesSlug = "trees";
    public const string BouldersSlug = "boulders";
    public const string BiomesSlug = "biomes";

    public static readonly LibraryKind Styles = new(
        StylesSlug, "styles", "Patterns", "pattern", "paintbrush",
        "pattern",
        DraftPreview: false);

    public static readonly LibraryKind Themes = new(
        ThemesSlug, "themes", "Palettes", "palette", "layers",
        "palette",
        Composed: true);

    public static readonly LibraryKind Roofs = new(
        RoofsSlug, "roof-styles", "Roofs", "roof", "triangle",
        "roof form");

    public static readonly LibraryKind Storeys = new(
        StoreysSlug, "storey-styles", "Storeys", "storey", "brick-wall",
        "storey");

    public static readonly LibraryKind Porches = new(
        PorchesSlug, "porch-styles", "Porches", "porch", "door-open",
        "porch");

    /// <summary>The row is a <c>room_style</c> and composes to a <c>HouseStyle</c>; the surface calls it what
    /// the thing is.</summary>
    public static readonly LibraryKind Houses = new(
        HousesSlug, "room-styles", "Houses", "house", "house",
        "house",
        Composed: true);

    /// <summary>A tree is a recipe rather than a placement: what is put on the canvas is a position, and what
    /// stands there is one of these. The three forms are three trees, not one with a switch.</summary>
    public static readonly LibraryKind Trees = new(
        TreesSlug, "tree-styles", "Trees", "tree", "trees",
        "recipe",
        Composed: true);

    public static readonly LibraryKind Boulders = new(
        BouldersSlug, "boulder-styles", "Boulders", "boulder", "mountain",
        "boulder",
        Composed: true);

    /// <summary>A biome field is a recipe like a material is — a kind, a scale and a palette — so it is named
    /// once here and picked wherever it is applied, rather than authored in the phase that applies it. It
    /// places no block: what a row states is which byte each column carries, and the card shows the ground
    /// that byte tints.</summary>
    public static readonly LibraryKind Biomes = new(
        BiomesSlug, "biome-patterns", "Biomes", "biome", "sun",
        "biome");

    public static readonly IReadOnlyList<LibraryKind> All =
        [Styles, Themes, Roofs, Storeys, Porches, Houses, Trees, Boulders, Biomes];

    /// <summary>The kind a route segment names, or null where it names none.</summary>
    public static LibraryKind? Of(string? slug) =>
        All.FirstOrDefault(kind => string.Equals(kind.Slug, slug, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What a delete answered, for every library alike: the row is gone, or it is still bound by these
/// compositions. One shape because every caller asks the same question of it — a per-kind return is what let
/// three of the six answer "did it work" in three different ways.
/// </summary>
/// <param name="Deleted">Whether the row is gone.</param>
/// <param name="BoundBy">The compositions still binding it, by name — what an author has to unbind first.</param>
public readonly record struct LibraryDelete(bool Deleted, IReadOnlyList<string> BoundBy)
{
    public static LibraryDelete Gone => new(true, []);

    /// <summary>The request never completed, so nothing is known about what binds the row.</summary>
    public static LibraryDelete Failed => new(false, []);
}
