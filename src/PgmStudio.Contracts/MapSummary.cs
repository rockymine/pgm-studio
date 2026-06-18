namespace PgmStudio.Contracts;

/// <summary>One entry in the map list (GET /api/maps).</summary>
public sealed record MapSummary(
    string Slug,
    string Name,
    string? Gamemode,
    string? Version,
    string? Objective,
    string Stage);

/// <summary>Per-stage map counts for the dashboard landing cards (GET /api/maps/stage-counts).</summary>
public sealed record MapStageCounts(int Sketch, int Configure, int Edit);
