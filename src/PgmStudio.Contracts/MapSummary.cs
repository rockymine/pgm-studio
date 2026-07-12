namespace PgmStudio.Contracts;

/// <summary>One entry in the map list (GET /api/maps). <paramref name="HasSurface"/> is true when the map
/// has a cached surface-layer artifact, so a top-down block render (GET /api/map/{slug}/layers/top-surface)
/// is available — e.g. as a reference backdrop for tracing.</summary>
public sealed record MapSummary(
    string Slug,
    string Name,
    string? Gamemode,
    string? Version,
    string? Objective,
    string Stage,
    bool HasSurface = false);

/// <summary>Per-stage map counts for the dashboard landing cards (GET /api/maps/stage-counts).</summary>
public sealed record MapStageCounts(int Sketch, int Configure, int Edit);
