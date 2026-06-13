namespace PgmStudio.Contracts;

/// <summary>One entry in the map list (GET /api/maps).</summary>
public sealed record MapSummary(
    string Slug,
    string Name,
    string? Gamemode,
    string? Version,
    string? Objective);
