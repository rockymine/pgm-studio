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
/// <param name="Blurb">What the kind is, on the chooser card.</param>
/// <param name="DraftPreview">Whether a draft draws at <c>{route}/preview</c>. A style does not: a style
/// <em>is</em> a material, so it draws as a bare one at <c>terrain/material-preview</c> with no row involved.</param>
/// <param name="Composed">Whether the kind answers its own composed document at <c>{route}/{id}/json</c> —
/// the form a map snapshots. Only a theme and a house compose to one; a style is already a document, and a
/// part is only ever part of one.</param>
public sealed record LibraryKind(
    string Slug, string Route, string Title, string One, string Icon, string Blurb,
    bool DraftPreview = true, bool Composed = false);

/// <summary>The six libraries, in the order they compose: a style is one material, a theme is a finish made of
/// styles, a roof, a storey and a porch are the parts a house binds, and a house is the whole building.</summary>
public static class LibraryKinds
{
    // The route segments as constants, because a switch over a kind needs them at compile time.
    public const string StylesSlug = "styles";
    public const string ThemesSlug = "themes";
    public const string RoofsSlug = "roofs";
    public const string StoreysSlug = "storeys";
    public const string PorchesSlug = "porches";
    public const string HousesSlug = "houses";

    public static readonly LibraryKind Styles = new(
        StylesSlug, "styles", "Styles", "style", "paintbrush",
        "One named material recipe — a solid block, a layer stack, a team tint or one of the patterns. It "
        + "is the unit a theme and a house course both reuse.",
        DraftPreview: false);

    public static readonly LibraryKind Themes = new(
        ThemesSlug, "themes", "Themes", "theme", "layers",
        "The whole finish: one style per bucket — the rim capping every edge, the wall on the riser under "
        + "it, the surface band, the fill body — plus how deep the paint reaches.",
        Composed: true);

    public static readonly LibraryKind Roofs = new(
        RoofsSlug, "roof-styles", "Roofs", "roof", "triangle",
        "Everything above the eave: which form the roof takes, how steeply it climbs, how far it oversails, "
        + "and what its body, its edge and its gable face are made of.");

    public static readonly LibraryKind Storeys = new(
        StoreysSlug, "storey-styles", "Storeys", "storey", "brick-wall",
        "One room: the air a player stands in, the wall around it, the windows through that wall and how "
        + "its own floor is divided. A house stacks these in order.");

    public static readonly LibraryKind Porches = new(
        PorchesSlug, "porch-styles", "Porches", "porch", "door-open",
        "The strip of footprint the walls give up, and what stands on it. Its deck is the house's floor and "
        + "its canopy the roof's material, so what is left to it is its shape.");

    /// <summary>The row is a <c>room_style</c> and composes to a <c>HouseStyle</c>; the surface calls it what
    /// the thing is.</summary>
    public static readonly LibraryKind Houses = new(
        HousesSlug, "room-styles", "Houses", "house", "house",
        "A whole building: a stack of storeys under a roof, with a porch, openings and the foundation it "
        + "stands on. It finishes a wool cage or a spawn cube without touching its size.",
        Composed: true);

    public static readonly IReadOnlyList<LibraryKind> All = [Styles, Themes, Roofs, Storeys, Porches, Houses];

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
