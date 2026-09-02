namespace PgmStudio.Contracts;

/// <summary>
/// A board's ground cells counted by the theme that paints them (<c>GET /map/{slug}/themes/census</c>): how
/// many cells each theme owns, what it is made of, and which theme borders which. <c>render/surface</c> reads
/// a board against its tone families too, but only as a legend baked into a picture; this is the count a
/// board that mashes its themes has no gate for.
/// </summary>
/// <param name="Themes">How many distinct themes the board's ground carries.</param>
/// <param name="Palette">How many distinct <c>id:data</c> surface blocks the whole board carries, over every
/// theme.</param>
/// <param name="ByTheme">One row per theme, largest first.</param>
/// <param name="Adjacency">Every pair of themes that share a border, largest first.</param>
public sealed record ThemeCensusResultDto(
    int Themes, int Palette, IReadOnlyList<ThemeCensusDto> ByTheme, IReadOnlyList<ThemeBorderDto> Adjacency);

/// <summary>One theme's share of the board — a registered theme id, or the map default where no shape
/// claims a cell.</summary>
/// <param name="Id">The theme id.</param>
/// <param name="Cells">How many ground cells it paints.</param>
/// <param name="Share">That, over the board's ground cells, 0–1.</param>
/// <param name="Materials">The distinct surface blocks its cells carry in the built world, as <c>id:data
/// name</c>, most frequent first, cut at twelve.</param>
/// <param name="MaterialCount">How many distinct surface blocks it carries, whether or not the list above
/// was cut.</param>
public sealed record ThemeCensusDto(
    string Id, int Cells, double Share, IReadOnlyList<string> Materials, int MaterialCount);

/// <summary>Two themes that share a border.</summary>
/// <param name="A">One theme, ordered before <paramref name="B"/> so a pair is named once.</param>
/// <param name="B">The other.</param>
/// <param name="Cells">How many 4-neighbour cell pairs cross from one to the other.</param>
public sealed record ThemeBorderDto(string A, string B, int Cells);
