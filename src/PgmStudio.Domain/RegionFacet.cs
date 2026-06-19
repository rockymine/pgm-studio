namespace PgmStudio.Domain;

/// <summary>
/// One region's derived categorisation: a gameplay <see cref="Category"/>, usage <see cref="Roles"/>,
/// and an optional <see cref="Subtype"/> refining the category (spec §2 — e.g. spawn → point|protection).
/// </summary>
public sealed record RegionFacet(string Category, List<string> Roles, string? Subtype = null);
